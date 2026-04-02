using UnityEngine;

public class V2KartController : MonoBehaviour
{
    private float moveInput;
    private float turnInput;
    private bool isCarGrounded;

    

    public float alignSpeed = 10f;

    [Header("Drift & Boost")]
    public bool isDrifting;
    private int driftDirection;
    private float driftPower;
    private float currentBoost;
    public float boostDecayRate = 20f; // How fast the boost fades away


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

    void Start()
    {
        //detach rigidbody from car
        sphereRB.transform.parent = null;
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        //Decay the boost over time
        if (currentBoost > 0)
        {
            currentBoost -= Time.deltaTime * boostDecayRate;
            if (currentBoost < 0) currentBoost = 0;
        }

        //Start Drifting if we press Space (Jump), are grounded, moving forward, and turning
        if (Input.GetButtonDown("Jump") && isCarGrounded && turnInput != 0 && moveInput > 0 && !isDrifting)
        {
            isDrifting = true;
            driftDirection = turnInput > 0 ? 1 : -1; // 1 for right, -1 for left
            driftPower = 0f;
        }

        float actualTurnSpeed = 0f;

        if (isDrifting)
        {
            // Accumulate drift power (fill faster if turning INTO the drift)
            float powerMultiplier = (turnInput == driftDirection) ? 1.5f : 0.5f;
            driftPower += Time.deltaTime * 100f * powerMultiplier;

            // Steering controls how tight/wide the drift is, rather than turning normally
            float driftControl = 1f + (turnInput * driftDirection * 0.5f); 
            actualTurnSpeed = driftDirection * turnSpeed * driftControl;

            // Release Drift & Apply Boost
            if (Input.GetButtonUp("Jump"))
            {
                isDrifting = false;
                
                // Tiers of boost based on how long you drifted
                if (driftPower > 150f) currentBoost = 40f;      // Tier 3
                else if (driftPower > 100f) currentBoost = 25f; // Tier 2
                else if (driftPower > 50f) currentBoost = 15f;  // Tier 1
                
                driftPower = 0f;
            }
        }
        else
        {
            // Normal Steering
            actualTurnSpeed = turnInput * turnSpeed;
        }

        //adjust speed for car
        moveInput *= moveInput > 0 ? fwdSpeed : revSpeed;

        moveInput += currentBoost;

        //set cars position to sphere
        transform.position = sphereRB.transform.position;

        //set cars rotation
        float newRotation = actualTurnSpeed * Time.deltaTime * Input.GetAxisRaw("Vertical");
        transform.Rotate(0, newRotation, 0, Space.World);

        //Raycast GroundCheck
        RaycastHit hit;
        isCarGrounded = Physics.Raycast(transform.position, -transform.up, out hit, 1f, GroundLayer);


        if(isCarGrounded)
        {
            sphereRB.linearDamping = groundDrag;

            // rotate car to be parallel to ground
            Quaternion targetGroundRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetGroundRotation, Time.deltaTime * alignSpeed);
        }
        else
        {
            sphereRB.linearDamping = airDrag;

            // level out car when in the air
            Quaternion levelRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, levelRotation, Time.deltaTime * alignSpeed * 0.5f);
        }

        AnimateVisuals();
    }

    private void FixedUpdate()
    {
        if(isCarGrounded)
        {
            // move car
            sphereRB.AddForce(transform.forward * moveInput, ForceMode.Acceleration);
        } else
        {
            sphereRB.AddForce(-transform.up * 9.8f);
        }

        
    }

    private void OnGUI()
    {
        // Make the text a bit bigger and easier to read
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;

        // Calculate actual physical speed (how fast the sphere is moving)
        float currentSpeed = sphereRB.linearVelocity.magnitude;

        // Draw the text on the screen (x, y, width, height)
        GUI.Label(new Rect(20, 20, 300, 40), "Speed: " + Mathf.RoundToInt(currentSpeed), style);
        
        // Draw the current boost power right below it
        GUI.Label(new Rect(20, 60, 300, 40), "Boost Reserve: " + Mathf.RoundToInt(currentBoost), style);
    }

    private void AnimateVisuals()
    {
        // 1. Calculate how fast the car is physically rolling forward/backward
        float forwardSpeed = Vector3.Dot(sphereRB.linearVelocity, transform.forward);
        currentWheelSpin += forwardSpeed * wheelSpinSpeed * Time.deltaTime;

        // 2. Tire Spinning & Steering
        // Create the two rotations independently
        Quaternion steerRot = Quaternion.Euler(0, 0, turnInput * maxSteerAngle); // Z-axis steering
        Quaternion spinRot = Quaternion.Euler(0, currentWheelSpin, 0);           // Y-axis spinning

        // Multiply them together to combine them without wobble (Order matters!)
        if (frontLeftWheel != null) 
            frontLeftWheel.localRotation = steerRot * spinRot;
            
        if (frontRightWheel != null) 
            frontRightWheel.localRotation = steerRot * spinRot;

        // Back wheels just get the spin rotation
        if (backLeftWheel != null) 
            backLeftWheel.localRotation = spinRot;
            
        if (backRightWheel != null) 
            backRightWheel.localRotation = spinRot;

        // 3. Steering Wheel Turning
        if (steeringWheel != null)
        {
            steeringWheel.localEulerAngles = new Vector3(-25, 90, turnInput * -45f);
        }

        // 4. Kart Leaning & Drift Crab-Walking
        if (kartBody != null)
        {
            float targetLean = turnInput * -5f; // Slight lean away from the turn during normal driving
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
