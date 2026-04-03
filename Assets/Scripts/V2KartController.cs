using UnityEngine;
using UnityEngine.InputSystem; // 1. Added namespace

public class V2KartController : MonoBehaviour
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

    // --- NEW INPUT SYSTEM FIELDS ---
    [Header("Input Action References")]
    public InputActionReference moveAction;
    public InputActionReference turnAction;
    public InputActionReference driftAction;

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

    void Start()
    {
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

        transform.position = sphereRB.transform.position;

        // Using moveInputRaw here so rotation only happens when accelerating/reversing
        float newRotation = actualTurnSpeed * Time.deltaTime * moveInputRaw;
        transform.Rotate(0, newRotation, 0, Space.World);

        RaycastHit hit;
        isCarGrounded = Physics.Raycast(transform.position, -transform.up, out hit, 1f, GroundLayer);

        if(isCarGrounded)
        {
            sphereRB.linearDamping = groundDrag;
            Quaternion targetGroundRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetGroundRotation, Time.deltaTime * alignSpeed);
        }
        else
        {
            sphereRB.linearDamping = airDrag;
            Quaternion levelRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, levelRotation, Time.deltaTime * airAlignSpeed * 0.5f);
        }

        AnimateVisuals();
    }

    private void FixedUpdate()
    {
        if(isCarGrounded)
        {
            sphereRB.AddForce(transform.forward * moveInput, ForceMode.Acceleration);
        } 
        else
        {
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