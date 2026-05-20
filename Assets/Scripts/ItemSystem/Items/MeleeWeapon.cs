using UnityEngine;
using Unity.Netcode;

public class MeleeWeapon : MonoBehaviour
{
    [Tooltip("Drag the KartInventory from this kart here")]
    public KartInventory myInventory; 

    private void OnTriggerEnter(Collider other)
    {
        // 1. Only the server is allowed to register hits to prevent cheating/double-hits
        if (!NetworkManager.Singleton.IsServer) return;

        // 2. If the hammer isn't currently activated, it's just harmless air
        if (!myInventory.isHammerActive.Value) return;

        // 3. Did we hit a kart?
        V5KartController hitKart = other.GetComponentInParent<V5KartController>();
        
        if (hitKart != null)
        {
            // Don't hit ourselves!
            if (hitKart.OwnerClientId == myInventory.OwnerClientId) return;

            // Force them to spin out!
            hitKart.TakeHitClientRpc();
            
            // Optional: If you want the hammer to disappear after ONE hit, uncomment this:
            // myInventory.isHammerActive.Value = false;
        }
    }
}