using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using UnityEngine.XR.Content.Interaction;

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
    public Vector3 currentGravityDir = Vector3.down;
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

    [Header("VR Controls")]
    public XRKnob vrSteeringWheel;
    public Transform driftStick;

    [Header("Status Effects")]
    public NetworkVariable<bool> isInvincible = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool isSpinningOut = false;

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

    private void OnDriftStarted(InputAction.CallbackContext context) => StartDrift();
    private void OnDriftCanceled(InputAction.CallbackContext context) => StopDrift();

    // Make these public so our VR Lever can trigger them from the Inspector!
    public void StartDrift()
    {
        if (!IsOwner) return;

        if (isCarGrounded && turnInput != 0 && moveInputRaw > 0 && !isDrifting)
        {
            isDrifting = true;
            driftDirection = turnInput > 0 ? 1 : -1;
            driftPower = 0f;
        }
    }

    public void StopDrift()
    {
        if (!IsOwner) return;

        if (isDrifting)
        {
            isDrifting = false;
            
            if (driftPower > 150f) currentBoost = 60f;
            else if (driftPower > 100f) currentBoost = 50f;
            else if (driftPower > 50f) currentBoost = 40f;
            
            driftPower = 0f;
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            // --- NEW: Block driving inputs if we are spinning out ---
            if (!isSpinningOut) 
            {
                // 1. OWNER LOGIC: Read hardware inputs
                moveInputRaw = moveAction.action.ReadValue<float>();

                if (vrSteeringWheel != null)
                {
                    // XR Knob outputs 0 (Full Left) to 1 (Full Right). Center is 0.5.
                    // We multiply and subtract to map this nicely to -1 (Left) and 1 (Right)!
                    turnInput = (vrSteeringWheel.value - 0.5f) * 2f;
                }
                else
                {
                    turnInput = turnAction.action.ReadValue<float>();
                }

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

                // Rotate Kart based on steering
                float newRotation = actualTurnSpeed * Time.deltaTime * moveInputRaw;
                transform.Rotate(0, newRotation, 0, Space.Self);
            }
            else
            {
                // If we ARE spinning out, force all driving variables to zero!
                moveInput = 0f;
                moveInputRaw = 0f;
                turnInput = 0f;
                isDrifting = false;
            }

            // --- The following code runs NO MATTER WHAT so the kart stays attached to the track ---

            // Broadcast inputs to remote clients
            netMoveInputRaw.Value = moveInputRaw;
            netTurnInput.Value = turnInput;
            netIsDrifting.Value = isDrifting;
            netDriftDirection.Value = driftDirection;

            // Move Kart to follow the physics sphere
            transform.position = sphereRB.transform.position;

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
        {
            // Only animate the wheel via code for REMOTE players. 
            // Your real VR hands are already handling the rotation locally!
            if (!IsOwner)
            {
                steeringWheel.localEulerAngles = new Vector3(0, turnInput * -45f, 0);
            }
        }

        if (driftStick != null)
        {
            if (!IsOwner)
            {
                // We use the exact angles you typed into the XRLever in the Inspector!
                // isDrifting = True (-32.1 degrees). isDrifting = False (13.7 degrees).
                float targetStickAngle = isDrifting ? -32.1f : 13.7f;
                driftStick.localEulerAngles = new Vector3(targetStickAngle, 0, 0);
            }
        }

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

    [ClientRpc]
    public void ApplyBoostClientRpc()
    {
        if (IsOwner)
        {
            // Instantly fill your boost tank (uses your existing drift boost logic!)
            currentBoost = 80f; 
        }
    }

    [ClientRpc]
    public void ActivateInvincibilityClientRpc()
    {
        if (IsServer) isInvincible.Value = true;
        
        // TODO: Add shiny particle effects here!

        // Automatically turn it off after 5 seconds
        if (IsServer) Invoke(nameof(EndInvincibility), 5f);
    }

    private void EndInvincibility()
    {
        isInvincible.Value = false;
    }

    // Called by the Shell script when it hits you
    [ClientRpc]
    public void TakeHitClientRpc()
    {
        if (isInvincible.Value) return; // Star power saves you!

        if (IsOwner)
        {
            // Kill all momentum
            sphereRB.linearVelocity = Vector3.zero;
            moveInputRaw = 0f;
            currentBoost = 0f;
            
            // Trigger the spin animation
            StartCoroutine(SpinOutRoutine());
        }
    }

    private System.Collections.IEnumerator SpinOutRoutine()
    {
        isSpinningOut = true;

        // --- Spin Settings ---
        float totalSpinDegrees = 1080f; // 1080 degrees = exactly 3 full rotations
        float spinDuration = 1.5f;      // How many seconds the spin lasts overall
        
        float elapsedTime = 0f;
        float spunSoFar = 0f;

        while (elapsedTime < spinDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // 1. Calculate our time progress from 0.0 to 1.0 (0% to 100%)
            float t = elapsedTime / spinDuration;

            // 2. Apply a "Cubic Ease-Out" math curve. 
            // This equation makes the rotation start incredibly fast, then smoothly decelerate to 0.
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);

            // 3. Figure out exactly how many degrees we SHOULD have spun by this exact millisecond
            float targetSpinSoFar = totalSpinDegrees * easeOutT;

            // 4. Calculate the difference between where we are, and where the curve says we should be
            float degreesThisFrame = targetSpinSoFar - spunSoFar;

            // 5. Apply the rotation locally so we stay glued to the anti-gravity walls!
            transform.Rotate(0, degreesThisFrame, 0, Space.Self);
            
            // 6. Save our progress for the next frame
            spunSoFar += degreesThisFrame;

            yield return null;
        }

        isSpinningOut = false;
    }
}