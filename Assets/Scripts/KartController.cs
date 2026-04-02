using UnityEngine;

using UnityEngine.InputSystem;

public class KartController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody sphereRB;
    public Transform kartModel;

    [Header("Settings")]
    public float speed = 30f;
    public float turnSpeed = 100f;
    public float groundCheckDistance = 0.6f;
    public LayerMask groundLayer;

    private float moveInput;
    private float steerInput;
    private bool isGrounded;

    void Update()
    {
        // 1. Get Input from Keyboard/Gamepad
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            moveInput = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            steerInput = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
        }

        // 2. Make the Kart model follow the Sphere's position
        transform.position = sphereRB.transform.position;

        // 3. Steer the Kart (Only if moving)
        if (sphereRB.linearVelocity.magnitude > 0.1f)
        {
            float newRotation = steerInput * turnSpeed * Time.deltaTime * (moveInput >= 0 ? 1 : -1);
            transform.Rotate(0, newRotation, 0, Space.World);
        }

        // 4. Ground Check (Raycast)
        isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer);
        
        // 5. Tilt Kart to match ground slope
        if (isGrounded)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation, 0.1f);
        }
    }

    void FixedUpdate()
    {
        if (isGrounded)
        {
            // Apply forward force to the Sphere based on where the Kart is facing
            sphereRB.AddForce(transform.forward * moveInput * speed, ForceMode.Acceleration);
        }
        else
        {
            // Add extra gravity so it doesn't float too much
            sphereRB.AddForce(Vector3.down * 20f, ForceMode.Acceleration);
        }
    }
}