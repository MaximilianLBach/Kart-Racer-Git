using UnityEngine;
using Unity.Netcode;

public class KartSpawner : NetworkBehaviour 
{
    [Header("Podium Spawn Points (0 = 1st, 1 = 2nd, 2 = 3rd)")]
    // We replaced the hidden List with a visible Array!
    [SerializeField] private Transform[] orderedSpawnPoints; 

    [SerializeField] private NetworkObject kartPrefab;

    private int nextSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
        SpawnExistingPlayersDelay();
    }

    private async void SpawnExistingPlayersDelay()
    {
        await System.Threading.Tasks.Task.Delay(500);

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayer(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (orderedSpawnPoints == null || orderedSpawnPoints.Length == 0) return;

        int placement = 0; 

        // SCENARIO A: We have a leaderboard from a previous race!
        if (RaceManager.FinalLeaderboard != null && RaceManager.FinalLeaderboard.Count > 0)
        {
            if (RaceManager.FinalLeaderboard.Contains(clientId))
            {
                // You raced! Here is your earned spot.
                placement = RaceManager.FinalLeaderboard.IndexOf(clientId);
            }
            else
            {
                // You just joined the server during the podium, go to the back!
                placement = orderedSpawnPoints.Length - 1; 
            }
        }
        // SCENARIO B: This is the very first race. No leaderboard exists yet!
        else
        {
            // Just hand out the spots in order: 0, 1, 2, 3...
            placement = nextSpawnIndex % orderedSpawnPoints.Length;
        }

        // Safety clamp: Ensure we never ask for an array index that doesn't exist
        placement = Mathf.Clamp(placement, 0, orderedSpawnPoints.Length - 1);
        
        // Always increment the fallback index just in case
        nextSpawnIndex++; 

        // Spawn them!
        Transform spawn = orderedSpawnPoints[placement];
        NetworkObject spawnedKart = Instantiate(kartPrefab, spawn.position, spawn.rotation);
        spawnedKart.SpawnWithOwnership(clientId);
    }
}