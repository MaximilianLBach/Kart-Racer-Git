using UnityEngine;
using Unity.Netcode;

public class SpawnLogic : MonoBehaviour
{
    [Header("Assign your spawn points here!")]
    public Transform[] spawnPoints;
    
    private int spawnIndex = 0;

    private void Start()
    {
        // We tell the NetworkManager to route all new connections through our ApprovalCheck method
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // 1. Approve the connection and tell NGO to spawn the default Player Prefab
        response.Approved = true;
        response.CreatePlayerObject = true;

        // 2. Safety check: make sure we actually assigned spawn points in the editor
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("No spawn points assigned in the KartSpawnManager!");
            return;
        }

        // 3. Grab the current spawn point based on our index
        Transform currentSpawn = spawnPoints[spawnIndex];

        // 4. Assign the starting position and rotation BEFORE the kart is created
        response.Position = currentSpawn.position;
        response.Rotation = currentSpawn.rotation;

        // 5. Increase the index for the next player who joins.
        // The "% spawnPoints.Length" makes it loop back to 0 if we run out of unique spots!
        spawnIndex = (spawnIndex + 1) % spawnPoints.Length;
    }
}
