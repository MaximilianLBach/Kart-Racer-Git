using UnityEngine;
using Unity.Netcode;

public class ShellProjectile : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float speed = 40f;
    public float hoverHeight = 0.5f; 
    public float shellRadius = 0.5f; // How fat the shell is (for wall detection)
    
    [Header("Bouncing Settings")]
    public int maxBounces = 3;
    private int currentBounces = 0;

    [Header("Layers")]
    [Tooltip("The layer your floor/anti-grav walls are on")]
    public LayerMask trackLayer; 
    [Tooltip("The layer your boundaries/walls are on")]
    public LayerMask wallLayer;  

    private ulong shooterId;
    private Vector3 currentDirection;

    public void Fire(ulong ownerId)
    {
        shooterId = ownerId;
        // When fired, our initial direction is exactly where we are facing
        currentDirection = transform.forward;
    }

    void Update()
    {
        // Only the Server moves the shell. Clients just watch it via NetworkTransform!
        if (!IsServer) return;

        // 1. CHECK FOR WALL BOUNCES (Look ahead by our speed amount)
        RaycastHit wallHit;
        if (Physics.SphereCast(transform.position, shellRadius, currentDirection, out wallHit, speed * Time.deltaTime + 0.1f, wallLayer))
        {
            // Bounce! Reflect our direction off the wall's normal
            currentDirection = Vector3.Reflect(currentDirection, wallHit.normal);
            
            // Flatten the bounce so it doesn't accidentally fly up into the sky
            currentDirection.y = 0; 
            currentDirection.Normalize(); 

            // Point the visual model in the new direction
            transform.rotation = Quaternion.LookRotation(currentDirection);

            currentBounces++;
            if (currentBounces >= maxBounces)
            {
                // Poof! Destroy the shell after too many bounces.
                // TODO: Add a particle effect spawn here later!
                GetComponent<NetworkObject>().Despawn();
                return; // Stop running code this frame
            }
        }

        // 2. MOVE FORWARD
        transform.position += currentDirection * speed * Time.deltaTime;

        // 3. TRACK ALIGNMENT (Anti-Gravity Hovering)
        // Shoot a raycast from slightly above the shell, downwards
        RaycastHit trackHit;
        if (Physics.Raycast(transform.position + (transform.up * 1f), -transform.up, out trackHit, 3f, trackLayer))
        {
            // Snap to the exact hover height above the track
            transform.position = trackHit.point + (trackHit.normal * hoverHeight);

            // Rotate the shell so its "Up" matches the wall/floor (just like the kart)
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, trackHit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);

            // Project our forward direction flat against the new track angle so it drives up walls smoothly
            currentDirection = Vector3.ProjectOnPlane(currentDirection, trackHit.normal).normalized;
        }
        else
        {
            // Fallback: If it flies off a ramp into the air, apply some fake gravity
            transform.position += Vector3.down * 15f * Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Did we hit a kart?
        V5KartController hitKart = other.GetComponentInParent<V5KartController>();
        
        if (hitKart != null)
        {
            // Don't hit the person who shot it!
            if (hitKart.OwnerClientId == shooterId) return;

            // Tell that specific kart to spin out
            hitKart.TakeHitClientRpc();
            
            GetComponent<NetworkObject>().Despawn();
        }
    }
}