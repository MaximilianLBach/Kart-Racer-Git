using UnityEngine;
using Unity.Netcode;

public class ShellProjectile : NetworkBehaviour
{
    public float speed = 30f;
    private ulong shooterId;

    public void Fire(ulong ownerId)
    {
        shooterId = ownerId;
    }

    void Update()
    {
        // The server moves the shell, and it auto-syncs via a NetworkTransform component
        if (IsServer)
        {
            transform.position += transform.forward * speed * Time.deltaTime;
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
            
            // Destroy the shell
            GetComponent<NetworkObject>().Despawn();
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Environment")) 
        {
            // (Optional) Add bouncing logic here later! For now, destroy if it hits a wall.
            GetComponent<NetworkObject>().Despawn();
        }
    }
}