using Utilities;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class AxleInfo
{
    public WheelCollider leftWheel;
    public WheelCollider rightWheel;
    public bool motor;
    public bool steering;
    public WheelFrictionCurve originalForwardDirection;
    public WheelFrictionCurve originalSidewaysDirection;
}

public class KartControllerV3 : MonoBehaviour
{
    [Header("Axle Information")]
    [SerializeField] AxleInfo[] axleInfos;

    [Header("Motor Attributes")]
    [SerializeField] float maxMotorTorque = 3000f;
    [SerializeField] float maxSpeed;

    [Header("Steering Attributes")]
    [SerializeField] float maxSteeringAngle = 30f;
    [SerializeField] AnimationCurve turnCurve;
    [SerializeField] float turnStrength = 1500f;

    [Header("Braking and Drifting")]
    [SerializeField] float driftSteerMultiplier = 1.5f; // Change in steering during a drift
    [SerializeField] float brakeTorque = 10000f;

    [Header("Physics")]
    [SerializeField] Transform centerOfMass;
    [SerializeField] float downForce = 100f;
    [SerializeField] float gravity = Physics.gravity.y;
    [SerializeField] float lateralGScale = 10f; // Scaling factor for lateral G forces;

    [Header("Banking")]
    [SerializeField] float maxBankAngle = 5f;
    [SerializeField] float bankSpeed = 2f;

    [Header("Refs")]
    [SerializeField] InputReader input;
    Rigidbody rb;

    Vector3 kartVelocity;
    float brakeVelocity;
    float driftVelocity;

    RaycastHit hit;
    const float thresholdSpeed = 10f;
    const float centerOfMassOffset = -0.5f;
    Vector3 originalCenterOfMass;

     public bool IsGrounded = true;
    public Vector3 Velocity => kartVelocity;
    public float MaxSpeed => maxSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        input.Enable();

        rb.centerOfMass = centerOfMass.localPosition;
        originalCenterOfMass = centerOfMass.localPosition;

        foreach (AxleInfo axleInfo in axleInfos)
        {
            axleInfo.originalForwardDirection = axleInfo.leftWheel.forwardFriction;
            axleInfo.originalSidewaysDirection = axleInfo.leftWheel.sidewaysFriction;
        }
    }

    void FixedUpdate()
    {
        float verticalInput = AdjustInput(input.Move.y);
        float horizontalInput = AdjustInput(input.Move.x);

        float motor = maxMotorTorque * verticalInput;
        float steering = maxSteeringAngle * horizontalInput;

        UpdateAxles(motor, steering);
        UpdateBanking(horizontalInput);

        kartVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        if(IsGrounded)
        {
            HandleGroundedMovement(verticalInput, horizontalInput);
        } 
        else
        {
            HandleAirbornMovement(verticalInput, horizontalInput);    
        }
    }

    void HandleGroundedMovement(float verticalInput, float horizontalInput)
    {
         // Turn logic
        if (Mathf.Abs(verticalInput) > 0.1f || Mathf.Abs(kartVelocity.z) > 1) {
            float turnMultiplier = Mathf.Clamp01(turnCurve.Evaluate(kartVelocity.magnitude / maxSpeed));
            rb.AddTorque(Vector3.up * (horizontalInput * Mathf.Sign(kartVelocity.z) * turnStrength * 100f * turnMultiplier));
        }  

        // Acceleration Logic
        if (!input.IsBraking) {
            float targetSpeed = verticalInput * maxSpeed;
            Vector3 forwardWithoutY = transform.forward.With(y: 0).normalized;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, forwardWithoutY * targetSpeed,  Time.deltaTime);
        }  

        // Downforce - always push the cart down, using lateral Gs to scale the force if the Kart is moving sideways fast
        float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
        float lateralG = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.right));
        float downForceFactor = Mathf.Max(speedFactor, lateralG / lateralGScale);
        rb.AddForce(-transform.up * (downForce * rb.mass * downForceFactor));

         // Shift Center of Mass
        float speed = rb.linearVelocity.magnitude;
        Vector3 centerOfMassAdjustment = (speed > thresholdSpeed) 
            ? new Vector3(0f, 0f, Mathf.Abs(verticalInput) > 0.1f ? Mathf.Sign(verticalInput) * centerOfMassOffset : 0f)
            : Vector3.zero;
        rb.centerOfMass = originalCenterOfMass + centerOfMassAdjustment;
    }

    void HandleAirbornMovement(float verticalInput, float horizontalInput)
    {
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, rb.linearVelocity + Vector3.down * gravity, Time.deltaTime * gravity);
    }

    void UpdateBanking(float horizontalInput) {
        // Bank the Kart in the opposite direction of the turn
        float targetBankAngle = horizontalInput * -maxBankAngle;
        Vector3 currentEuler = transform.localEulerAngles;
        currentEuler.z = Mathf.LerpAngle(currentEuler.z, targetBankAngle, Time.deltaTime * bankSpeed);
        transform.localEulerAngles = currentEuler;
    }

    void UpdateAxles(float motor, float steering)
    {
        foreach (AxleInfo axleInfo in axleInfos)
        {
            HandleSteering(axleInfo, steering);
            HandleMotor(axleInfo, motor);
            HandleBrakesAndDrift(axleInfo);
            UpdateWheelVisuals(axleInfo.leftWheel);
            UpdateWheelVisuals(axleInfo.rightWheel);
        }
    }

     void UpdateWheelVisuals(WheelCollider collider)
    {
        if (collider.transform.childCount == 0) return;
        Transform visual = collider.transform.GetChild(0);

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        visual.position = position;
        visual.rotation = rotation;
    }

    void HandleSteering(AxleInfo axleInfo, float steering) 
    {
        if (axleInfo.steering) 
        {
            axleInfo.leftWheel.steerAngle = steering;
            axleInfo.rightWheel.steerAngle = steering;
        }
    }

    void HandleMotor(AxleInfo axleInfo, float motor) 
    {
        if (axleInfo.motor) 
        {
            axleInfo.leftWheel.motorTorque = motor;
            axleInfo.rightWheel.motorTorque = motor;
        }
    }

    void HandleBrakesAndDrift(AxleInfo axleInfo )
    {
        if(axleInfo.motor)
        {
            if(input.IsBraking)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationX;

                float newZ = Mathf.SmoothDamp(rb.linearVelocity.z, 0, ref brakeVelocity, 1f);
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, newZ);

                axleInfo.leftWheel.brakeTorque = brakeTorque;
                axleInfo.rightWheel.brakeTorque = brakeTorque;
                ApplyDriftFriction(axleInfo.leftWheel);
                ApplyDriftFriction(axleInfo.rightWheel);
            } else
            {
                rb.constraints = RigidbodyConstraints.None;
                    
                axleInfo.leftWheel.brakeTorque = 0;
                axleInfo.rightWheel.brakeTorque = 0;
                ResetDriftFriction(axleInfo.leftWheel);
                ResetDriftFriction(axleInfo.rightWheel);
            }
        }
    }

    void ResetDriftFriction(WheelCollider wheel) {
        AxleInfo axleInfo = axleInfos.FirstOrDefault(axle => axle.leftWheel == wheel || axle.rightWheel == wheel);
        if (axleInfo == null) return;
        
        wheel.forwardFriction = axleInfo.originalForwardDirection;
        wheel.sidewaysFriction = axleInfo.originalSidewaysDirection;
    }

    void ApplyDriftFriction(WheelCollider wheel) {
        if (wheel.GetGroundHit(out var hit)) {
            wheel.forwardFriction = UpdateFriction(wheel.forwardFriction);
            wheel.sidewaysFriction = UpdateFriction(wheel.sidewaysFriction);
            IsGrounded = true;
        }
    }

    WheelFrictionCurve UpdateFriction(WheelFrictionCurve friction) {
        friction.stiffness = input.IsBraking ? Mathf.SmoothDamp(friction.stiffness, .5f, ref driftVelocity, Time.deltaTime * 2f) : 1f;
        return friction;
    }

    float AdjustInput(float input)
    {
        return input switch
        {
            >= .7f => 1f,
            <= -.7f => -1f,
            _ => input
        };
    }
}
