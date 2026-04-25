using UnityEngine;
using Unity.Netcode;

public class V4KartController : NetworkBehaviour
{
    private float moveInput;
    private float turnInput;
    private bool isCarGrounded;

    public float alignSpeed = 10f;
    public float airDrag;
    public float groundDrag;
    public float fwdSpeed;
    public float revSpeed;
    public float turnSpeed;
    public LayerMask GroundLayer;

    public Rigidbody sphereRB;

    public Camera playerCamera;
    public AudioListener playerAudioListener;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Only the local player needs the physics sphere detached to drive
            sphereRB.transform.position = transform.position;
            sphereRB.transform.parent = null;

            if (playerCamera != null) playerCamera.enabled = true;
            if (playerAudioListener != null) playerAudioListener.enabled = true;
        }
        else
        {
            // Remote players don't need a physics sphere at all! 
            // We destroy it so it doesn't clutter the scene or cause rogue collisions.
            Destroy(sphereRB.gameObject);

            if (playerCamera != null) playerCamera.enabled = false;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
        }
    }

    void Update()
    {
        // If this is a remote player's car, do absolutely nothing.
        // The NetworkTransform component handles moving and rotating it visually!
        if (!IsOwner) return;

        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        float scaledMoveInput = moveInput * (moveInput > 0 ? fwdSpeed : revSpeed);

        // set cars position to sphere
        transform.position = sphereRB.transform.position;

        // set cars rotation
        float newRotation = turnInput * turnSpeed * Time.deltaTime * moveInput;
        transform.Rotate(0, newRotation, 0, Space.World);

        // Raycast GroundCheck
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
    }

    private void FixedUpdate()
    {
        // Only the local player calculates physical forces
        if (!IsOwner) return;

        float scaledMoveInput = moveInput * (moveInput > 0 ? fwdSpeed : revSpeed);

        if(isCarGrounded)
        {
            // move car
            sphereRB.AddForce(transform.forward * scaledMoveInput, ForceMode.Acceleration);
        } 
        else
        {
            sphereRB.AddForce(-transform.up * 9.8f);
        }
    }
}