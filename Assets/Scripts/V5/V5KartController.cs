using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class V5KartController : NetworkBehaviour
{
    // --- NETWORK VARIABLES FOR VISUAL SYNC ---
    // We broadcast inputs so remote clients can run the AnimateVisuals() locally
    [HideInInspector] public NetworkVariable<float> netMoveInputRaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector] public NetworkVariable<float> netTurnInput = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector] public NetworkVariable<bool> netIsDrifting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [HideInInspector] public NetworkVariable<int> netDriftDirection = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private float moveInput;
    private float moveInputRaw;
    private float turnInput;
    private bool isCarGrounded;
    private Vector3 currentGravityDir = Vector3.down;
    private bool isInAntiGravZone = false;

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
    private Vector3 lastPosition; // Used to calculate velocity for remote clients

    [Header("Physics Settings")]
    public float airDrag;
    public float groundDrag;
    public float fwdSpeed;
    public float revSpeed;
    public float turnSpeed;
    public LayerMask GroundLayer;

    public Rigidbody sphereRB;

    [Header("Multiplayer Setup")]
    public GameObject xrOrigin;
    public AudioListener playerAudioListener;

    [Header("Input Action References")]
    public InputActionReference moveAction;
    public InputActionReference turnAction;
    public InputActionReference driftAction;

    [Header("Collision Setup")]
    public Collider kartVisualCollider; 
    public Collider kartSphereCollider;

    void OnEnable() 
    {
        moveAction.action.Enable();
        turnAction.action.Enable();
        driftAction.action.Enable();

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

    public override void OnNetworkSpawn()
    {
        if (kartVisualCollider != null && kartSphereCollider != null)
        {
            Physics.IgnoreCollision(kartVisualCollider, kartSphereCollider);
        }
        
        if (IsOwner)
        {
            // Only the local player needs the physics sphere detached to drive
            sphereRB.transform.position = transform.position;
            sphereRB.transform.parent = null;

            if (xrOrigin != null) xrOrigin.SetActive(true);
            if (playerAudioListener != null) playerAudioListener.enabled = true;
        }
        else
        {
            // Remote players don't need a physics sphere at all! 
            // We destroy it so it doesn't clutter the scene or cause rogue collisions.
            Destroy(sphereRB.gameObject);

            if (xrOrigin != null) xrOrigin.SetActive(false);
            if (playerAudioListener != null) playerAudioListener.enabled = false;
        }

        lastPosition = transform.position;
    }

    private void OnDriftStarted(InputAction.CallbackContext context)
    {
        // Only the owner can trigger actual drift logic
        if (!IsOwner) return;

        if (isCarGrounded && turnInput != 0 && moveInputRaw > 0 && !isDrifting)
        {
            isDrifting = true;
            driftDirection = turnInput > 0 ? 1 : -1;
            driftPower = 0f;
        }
    }

    private void OnDriftCanceled(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

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
        if (IsOwner)
        {
            // 1. OWNER LOGIC: Read hardware inputs
            moveInputRaw = moveAction.action.ReadValue<float>();
            turnInput = turnAction.action.ReadValue<float>();

            // Broadcast inputs to remote clients
            netMoveInputRaw.Value = moveInputRaw;
            netTurnInput.Value = turnInput;
            netIsDrifting.Value = isDrifting;
            netDriftDirection.Value = driftDirection;

            // Boost decay
            if (currentBoost > 0)
            {
                currentBoost -= Time.deltaTime * boostDecayRate;
                if (currentBoost < 0) currentBoost = 0;
            }

            float actualTurnSpeed = 0f;

            // Drift steering calculation
            if (isDrifting)
            {
                float powerMultiplier = (turnInput == driftDirection) ? 1.5f : 0.5f;
                driftPower += Time.deltaTime * 100f * powerMultiplier;

                float baseTurn = driftDirection * baseDriftTurnSpeed;
                float playerControl = turnInput * driftControlMultiplier;
                actualTurnSpeed = baseTurn + playerControl;
            }
            else
            {
                actualTurnSpeed = turnInput * turnSpeed;
            }

            moveInput = moveInputRaw;
            moveInput *= moveInput > 0 ? fwdSpeed : revSpeed;
            moveInput += currentBoost;

            // Move & Rotate Kart
            transform.position = sphereRB.transform.position;
            float newRotation = actualTurnSpeed * Time.deltaTime * moveInputRaw;
            transform.Rotate(0, newRotation, 0, Space.World);

            // Ground alignment
            RaycastHit hit;
            isCarGrounded = Physics.Raycast(transform.position, -transform.up, out hit, 1.5f, GroundLayer);

            if(isCarGrounded)
            {
                isInAntiGravZone = hit.collider.CompareTag("AntiGravTrack");

                if (isInAntiGravZone)
                {
                    currentGravityDir = -hit.normal; // Wall becomes "Down"
                }
                else
                {
                    currentGravityDir = Vector3.down; // Earth is "Down"
                }

                if (IsOwner) sphereRB.linearDamping = groundDrag;

                Quaternion targetGroundRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetGroundRotation, Time.deltaTime * alignSpeed);
            }
            else
            {
                if (IsOwner) sphereRB.linearDamping = airDrag;

                Quaternion levelRotation = Quaternion.FromToRotation(transform.up, -currentGravityDir) * transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, levelRotation, Time.deltaTime * airAlignSpeed * 0.5f);
            }
        }
        else
        {
            // 2. REMOTE CLIENT LOGIC: Read the networked variables so AnimateVisuals works
            moveInputRaw = netMoveInputRaw.Value;
            turnInput = netTurnInput.Value;
            isDrifting = netIsDrifting.Value;
            driftDirection = netDriftDirection.Value;
        }

        // 3. EVERYONE runs visuals locally based on the shared input data
        AnimateVisuals();
    }

    private void FixedUpdate()
    {
        // Only the local player calculates physical forces
        if (!IsOwner) return;

        if(isCarGrounded)
        {
            sphereRB.AddForce(transform.forward * moveInput, ForceMode.Acceleration);
            
            float stickForce = isInAntiGravZone ? 20f : 9.8f; 
            sphereRB.AddForce(currentGravityDir * stickForce, ForceMode.Acceleration);
        } 
        else
        {
            sphereRB.AddForce(currentGravityDir * 9.8f, ForceMode.Acceleration);
        }
    }

    private void AnimateVisuals()
    {
        // Calculate velocity (Owner has the physics sphere, Remotes just calculate position difference over time)
        Vector3 kartVelocity = Vector3.zero;
        if (IsOwner && sphereRB != null)
        {
            kartVelocity = sphereRB.linearVelocity;
        }
        else
        {
            kartVelocity = (transform.position - lastPosition) / Time.deltaTime;
        }
        lastPosition = transform.position;

        // Spin wheels based on forward movement
        float forwardSpeed = Vector3.Dot(kartVelocity, transform.forward);
        currentWheelSpin += forwardSpeed * wheelSpinSpeed * Time.deltaTime;

        Quaternion steerRot = Quaternion.Euler(0, 0, turnInput * maxSteerAngle);
        Quaternion spinRot = Quaternion.Euler(currentWheelSpin, 0, 0);

        if (frontLeftWheel != null) frontLeftWheel.localRotation = steerRot * spinRot;
        if (frontRightWheel != null) frontRightWheel.localRotation = steerRot * spinRot;
        if (backLeftWheel != null) backLeftWheel.localRotation = spinRot;
        if (backRightWheel != null) backRightWheel.localRotation = spinRot;

        if (steeringWheel != null)
            steeringWheel.localEulerAngles = new Vector3(0, turnInput * -45f, 0);

        // Lean chassis
        if (kartBody != null)
        {
            float targetLean = turnInput * -5f;
            float targetYOffset = 0f;

            float basePitch = -90f;

            if (isDrifting)
            {
                targetYOffset = driftDirection * 25f;
                targetLean = driftDirection * -15f; 
            }

            Quaternion targetBodyRotation = Quaternion.Euler(basePitch, targetYOffset, targetLean);
            kartBody.localRotation = Quaternion.Slerp(kartBody.localRotation, targetBodyRotation, Time.deltaTime * 8f);
        }
    }
}