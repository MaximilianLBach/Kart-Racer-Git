using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ItemBox : NetworkBehaviour
{
    [Header("Settings")]
    public float respawnTime = 3f;
    public GameObject visualMesh; // The spinning box model
    public Collider boxCollider;

    // A networked boolean so all players see the box disappear at the same time
    public NetworkVariable<bool> isActive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // Whenever the isActive variable changes, run the UpdateVisuals function
        isActive.OnValueChanged += (oldVal, newVal) => UpdateVisuals(newVal);
        UpdateVisuals(isActive.Value);
    }

    private void UpdateVisuals(bool active)
    {
        visualMesh.SetActive(active);
        boxCollider.enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the Server handles picking up items to prevent 2 players grabbing it at the exact same millisecond
        if (!IsServer || !isActive.Value) return;

        // Check if the object that hit us has an inventory
        KartInventory inventory = other.GetComponentInParent<KartInventory>();
        if (inventory != null)
        {
            // Give them a random item (excluding 'None' at index 0)
            ItemType randomItem = (ItemType)Random.Range(1, 5); 
            
            bool pickedUp = inventory.GiveItem(randomItem);

            if (pickedUp)
            {
                isActive.Value = false;
                StartCoroutine(RespawnRoutine());
            }
        }
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        isActive.Value = true; // Box comes back!
    }
}