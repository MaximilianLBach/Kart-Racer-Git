using UnityEngine;
using Unity.Netcode;
using TMPro; // Required for TextMeshPro!

public class KartDebugUI : NetworkBehaviour
{
    [Header("References")]
    public KartInventory inventory;
    public TextMeshProUGUI debugText;

    void Update()
    {
        // Only update the UI for the local player. Remote players don't need to see our UI.
        if (!IsOwner || inventory == null || debugText == null) return;

        string displayText = "--- INVENTORY ---\n";

        if (inventory.storedItems.Count == 0)
        {
            displayText += "Empty";
        }
        else
        {
            // Loop through our NetworkList of integers and cast them back to names
            for (int i = 0; i < inventory.storedItems.Count; i++)
            {
                ItemType itemName = (ItemType)inventory.storedItems[i];

                if (i == 0)
                {
                    // The first item is the active one!
                    displayText += $"[ > {itemName.ToString().ToUpper()} < ]\n"; 
                }
                else
                {
                    // Items waiting in line
                    displayText += $"{i + 1}. {itemName}\n";
                }
            }
        }

        debugText.text = displayText;
    }
}