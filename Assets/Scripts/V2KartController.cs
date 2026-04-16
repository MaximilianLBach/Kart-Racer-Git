using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem; // 1. Added namespace
using System.Collections.Generic;



//Network variables should be value objects
public struct InputPayload : INetworkSerializable
{
    public int tick;
    public Vector3 inputVector;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref inputVector);
    }
}

public struct StatePayload : INetworkSerializable
{
    public int tick;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
        serializer.SerializeValue(ref velocity);
        serializer.SerializeValue(ref angularVelocity);
    }
}

public class V2KartController : NetworkBehaviour 
{
    private float moveInput;
    private float moveInputRaw; // Store the raw -1 to 1 for rotation logic
    private float turnInput;
    private bool isCarGrounded;

    public float alignSpeed = 10f;
    public float airAlignSpeed = 1f;

    [Header("Drift & Boost")]
    public bool isDrifting;
    public float baseDriftTurnSpeed = 40f; 
    public float driftControlMultiplier = 20f;
    private int driftDirection;
    private float driftPower;
    private float currentBoost;
    public float boostDecayRate = 20f;

    [Header("Visuals & Animations")]
    public Transform kartBody;      
    public Transform frontLeftWheel;   
    public Transform frontRightWheel;
    public Transform backLeftWheel;
    public Transform backRightWheel;
    public Transform steeringWheel;

    public float wheelSpinSpeed = 50f;
    public float maxSteerAngle = 30f;
    private float currentWheelSpin;

    public float airDrag;
    public float groundDrag;
    public float fwdSpeed;
    public float revSpeed;
    public float turnSpeed;
    public LayerMask GroundLayer;

    public Rigidbody sphereRB;

    //Camera settings
    [SerializeField] Camera playerCamera;
   


    // --- NEW INPUT SYSTEM FIELDS ---
    [Header("Input Action References")]
    public InputActionReference moveAction;
    public InputActionReference turnAction;
    public InputActionReference driftAction;


    //Netcode general
    NetworkTimer timer;
    const float k_serverTickRate = 60f; // 60 FPS
    const int k_bufferSize = 1024;

    //Netcode client specific
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    //Netcode Server specific
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> ServerInputQueue;

    void OnEnable() 
    {
        moveAction.action.Enable();
        turnAction.action.Enable();
        driftAction.action.Enable();

        // Subscribe to Drift button events
        driftAction.action.started += OnDriftStarted;
        driftAction.action.canceled += OnDriftCanceled;
    }

    void OnDisable() 
    {
        moveAction.action.Disable();
        turnAction.action.Disable();
        driftAction.action.Disable();

        driftAction.action.started -= OnDriftStarted;
        driftAction.action.canceled -= OnDriftCanceled;
    }

    private void Awake()
    {
        // Initialize General Netcode variables
        timer = new NetworkTimer(k_serverTickRate);

        // Initialize Client-specific buffers
        clientStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(k_bufferSize);

        // Initialize Server-specific buffers/queues
        serverStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
        ServerInputQueue = new Queue<InputPayload>();
    }

    public override void OnNetworkSpawn()
    {
        if(!IsOwner)
        {
            playerCamera.enabled = false;
            if (playerCamera.GetComponent<AudioListener>() != null)
            {
                playerCamera.GetComponent<AudioListener>().enabled = false;

            }
            //sphereRB.isKinematic = true;
        }
        else
        {
            playerCamera.enabled = true;
            playerCamera.GetComponent<AudioListener>().enabled = true;
            sphereRB.isKinematic = false;
        }


        sphereRB.transform.parent = null;
    }

    // Logic for pressing the drift button
    private void OnDriftStarted(InputAction.CallbackContext context)
    {
        if (isCarGrounded && turnInput != 0 && moveInputRaw > 0 && !isDrifting)
        {
            isDrifting = true;
            driftDirection = turnInput > 0 ? 1 : -1;
            driftPower = 0f;
        }
    }

    // Logic for releasing the drift button
    private void OnDriftCanceled(InputAction.CallbackContext context)
    {
        if (isDrifting)
        {
            isDrifting = false;
            
            if (driftPower > 150f) currentBoost = 40f;
            else if (driftPower > 100f) currentBoost = 25f;
            else if (driftPower > 50f) currentBoost = 15f;
            
            driftPower = 0f;
        }
    }

    void Update()
    {

        // 2. Read values from the new system
        moveInputRaw = moveAction.action.ReadValue<float>();
        turnInput = turnAction.action.ReadValue<float>();

        if (currentBoost > 0)
        {
            currentBoost -= Time.deltaTime * boostDecayRate;
            if (currentBoost < 0) currentBoost = 0;
        }

        float actualTurnSpeed = 0f;

        

        AnimateVisuals();

        //Netcode
        timer.Update(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        while (timer.ShouldTick())
        {
            HandleClientTick();
            HandleServerTick();
        }

        
    }

    void HandleServerTick()
    {
        var bufferIndex = -1;
        if (ServerInputQueue.Count > 0) Debug.Log("Server processing " + ServerInputQueue.Count + " inputs");
        while (ServerInputQueue.Count > 0) 
        {
            InputPayload inputPayload = ServerInputQueue.Dequeue();

            bufferIndex = inputPayload.tick % k_bufferSize;

            StatePayload statePayload = SimulateMovement(inputPayload);
            serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
    }

    StatePayload SimulateMovement(InputPayload inputPayload)
    {
        var oldMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;

        Move(inputPayload);
        Physics.Simulate(timer.MinTimeBetweenTicks);
        Physics.simulationMode = oldMode;

        return new StatePayload()
        {
            tick = inputPayload.tick,
            position = transform.position,
            rotation = transform.rotation,
            velocity = sphereRB.linearVelocity,
            angularVelocity = sphereRB.angularVelocity
        };
    }

    [ClientRpc]
    void SendToClientRpc(StatePayload statePayload)
    {
        if (!IsOwner) return;
        lastServerState = statePayload;
    }

    void HandleClientTick()
    {
        if (!IsClient) return;

        var currentTick = timer.CurrentTick;
        var bufferIndex = currentTick % k_bufferSize;

        InputPayload inputPayload = new InputPayload()
        {
            tick = currentTick,
            inputVector = new Vector3(turnInput, moveInputRaw, 0)
        };

        clientInputBuffer.Add(inputPayload, bufferIndex);
        SendToServerRpc(inputPayload);

        StatePayload statePayload = ProcessMovement(inputPayload);
        clientStateBuffer.Add(statePayload, bufferIndex);

        //HandleServerReconciliation();


    }

    [ServerRpc]
    void SendToServerRpc(InputPayload input)
    {
        ServerInputQueue.Enqueue(input);
    }
    
    StatePayload ProcessMovement(InputPayload input)
    {
        Move(input);

        return new StatePayload()
        {
            tick = input.tick,
            position = transform.position,
            rotation = transform.rotation,
            velocity = sphereRB.linearVelocity,
            angularVelocity = sphereRB.angularVelocity
        };
    }

    void Move(InputPayload input)
    {
        float moveRaw = input.inputVector.y;
        float turn = input.inputVector.x;

        if (currentBoost > 0)
        {
            // Use MinTimeBetweenTicks instead of deltaTime
            currentBoost -= timer.MinTimeBetweenTicks * boostDecayRate;
            if (currentBoost < 0) currentBoost = 0;
        }

        if (isDrifting)
        {
            float powerMultiplier = (turn == driftDirection) ? 1.5f : 0.5f;
            driftPower += timer.MinTimeBetweenTicks * 100f * powerMultiplier;
        }

        // 1. Raycast for grounding (Must be here so Server can verify)
        RaycastHit hit;
        isCarGrounded = Physics.Raycast(transform.position, -transform.up, out hit, 1.5f, GroundLayer);

        // 2. Calculate Speed
        float currentMoveInput = moveRaw;
        currentMoveInput *= (currentMoveInput > 0) ? fwdSpeed : revSpeed;
        currentMoveInput += currentBoost;

        // 3. Rotation (Using MinTimeBetweenTicks)
        float actualTurnSpeed = isDrifting ? (driftDirection * baseDriftTurnSpeed + turn * driftControlMultiplier) : (turn * turnSpeed);
        float newRotation = actualTurnSpeed * timer.MinTimeBetweenTicks * moveRaw;
        transform.Rotate(0, newRotation, 0, Space.World);

        // 4. Ground Alignment logic should also be here
        if (isCarGrounded)
        {
            sphereRB.linearDamping = groundDrag;
            Quaternion targetGroundRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetGroundRotation, timer.MinTimeBetweenTicks * alignSpeed);

            sphereRB.AddForce(transform.forward * currentMoveInput, ForceMode.Acceleration);
        }
        else
        {
            sphereRB.linearDamping = airDrag;
            sphereRB.AddForce(-transform.up * 9.8f);
        }
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;
        float currentSpeed = sphereRB.linearVelocity.magnitude;
        GUI.Label(new Rect(20, 20, 300, 40), "Speed: " + Mathf.RoundToInt(currentSpeed), style);
        GUI.Label(new Rect(20, 60, 300, 40), "Boost Reserve: " + Mathf.RoundToInt(currentBoost), style);
    }

    private void AnimateVisuals()
    {
        float forwardSpeed = Vector3.Dot(sphereRB.linearVelocity, transform.forward);
        currentWheelSpin += forwardSpeed * wheelSpinSpeed * Time.deltaTime;

        Quaternion steerRot = Quaternion.Euler(0, 0, turnInput * maxSteerAngle);
        Quaternion spinRot = Quaternion.Euler(0, currentWheelSpin, 0);

        if (frontLeftWheel != null) frontLeftWheel.localRotation = steerRot * spinRot;
        if (frontRightWheel != null) frontRightWheel.localRotation = steerRot * spinRot;
        if (backLeftWheel != null) backLeftWheel.localRotation = spinRot;
        if (backRightWheel != null) backRightWheel.localRotation = spinRot;

        if (steeringWheel != null)
            steeringWheel.localEulerAngles = new Vector3(-25, 90, turnInput * -45f);

        if (kartBody != null)
        {
            float targetLean = turnInput * -5f;
            float targetYOffset = 0f;

            if (isDrifting)
            {
                targetYOffset = driftDirection * 25f;
                targetLean = driftDirection * -15f; 
            }

            Quaternion targetBodyRotation = Quaternion.Euler(0, targetYOffset, targetLean);
            kartBody.localRotation = Quaternion.Slerp(kartBody.localRotation, targetBodyRotation, Time.deltaTime * 8f);
        }
    }
}