using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class KartInventory : NetworkBehaviour
{
    [Header("Inventory State")]
    public int maxItems = 3;
    [Header("Melee Weapons")]
    public GameObject hammerVisual; 
    public NetworkVariable<bool> isHammerActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // CHANGE 1: Store as 'int' instead of 'ItemType' to bypass the IEquatable error
    public NetworkList<int> storedItems;

    [Header("Item Spawning")]
    public Transform itemSpawnPoint; 
    public GameObject ProjectilePrefab; 

    [Header("Input")]
    public InputActionReference useItemAction;

    private V5KartController kartController;

    private void Awake()
    {
        // CHANGE 2: Initialize as int list
        storedItems = new NetworkList<int>();
        kartController = GetComponent<V5KartController>();
    }

    private void OnEnable()
    {
        useItemAction.action.Enable();
        useItemAction.action.performed += OnUseItemPressed;
    }

    private void OnDisable()
    {
        useItemAction.action.Disable();
        useItemAction.action.performed -= OnUseItemPressed;
    }

    public bool GiveItem(ItemType newItem)
    {
        if (storedItems.Count < maxItems)
        {
            // CHANGE 3: Cast the ItemType to an int when saving it to the network
            storedItems.Add((int)newItem);
            return true; // Successfully picked up
        }
        return false; // Inventory full
    }

    private void OnUseItemPressed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (storedItems.Count > 0)
        {
            UseItemServerRpc();
        }
    }

    [ServerRpc]
    private void UseItemServerRpc()
    {
        if (storedItems.Count == 0) return;

        // CHANGE 4: Cast the int back to an ItemType so our switch statement understands it!
        ItemType itemToUse = (ItemType)storedItems[0];
        storedItems.RemoveAt(0);

        switch (itemToUse)
        {
            case ItemType.Boost:
                kartController.ApplyBoostClientRpc();
                break;
            
            case ItemType.Projectile:
                GameObject shell = Instantiate(ProjectilePrefab, itemSpawnPoint.position, itemSpawnPoint.rotation);
                shell.GetComponent<NetworkObject>().Spawn();
                shell.GetComponent<ShellProjectile>().Fire(OwnerClientId); 
                break;
            
            case ItemType.Invincibility:
                kartController.ActivateInvincibilityClientRpc();
                break;

            case ItemType.Hammer:
            // 1. Turn it on
            isHammerActive.Value = true;
            
            // 2. Tell the server to turn it off automatically after 30 seconds
            Invoke(nameof(DeactivateHammer), 30f);
            break;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Whenever the server changes this boolean, turn the visual model on/off locally!
        isHammerActive.OnValueChanged += (oldVal, newVal) => hammerVisual.SetActive(newVal);
        
        // Ensure it starts in the correct state
        hammerVisual.SetActive(isHammerActive.Value);
    }

    private void DeactivateHammer()
    {
        isHammerActive.Value = false;
    }

}