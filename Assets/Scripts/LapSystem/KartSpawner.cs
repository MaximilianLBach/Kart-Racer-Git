using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

// 1. MUST BE NetworkBehaviour
public class KartSpawner : NetworkBehaviour 
{
    private List<Transform> spawnPoints = new List<Transform>();
    private int nextSpawnIndex = 0;

    [SerializeField] private NetworkObject kartPrefab;

    // 2. This will now fire automatically when the scene is loaded by NetworkManager
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
    
        GameObject[] points = GameObject.FindGameObjectsWithTag("SpawnPoint");
        foreach (GameObject go in points) spawnPoints.Add(go.transform);
        
        Debug.Log($"KartSpawner: Found {spawnPoints.Count} spawn points.");
    
        // 1. Subscribe to future players
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;


        // 2. FORCE-SPAWN THE HOST IMMEDIATELY
        // We add a tiny delay to ensure the scene is fully initialized
        SpawnExistingPlayersDelay();
    }

private async void SpawnExistingPlayersDelay()
    {
        // Give the physics engine and scene 500ms to fully wake up
        await System.Threading.Tasks.Task.Delay(500);

        // Loop through everyone (Host is included in this list automatically)
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayer(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        Debug.Log($"SpawnPlayer triggered for ClientID: {clientId}");

        if (spawnPoints.Count == 0) return;

        Transform spawn = spawnPoints[nextSpawnIndex % spawnPoints.Count];
        nextSpawnIndex++;

        NetworkObject spawnedKart = Instantiate(kartPrefab, spawn.position, spawn.rotation);
        spawnedKart.SpawnWithOwnership(clientId);
        
        Debug.Log("Kart spawned successfully.");
    }
}