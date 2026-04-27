using UnityEngine;
using Unity.Netcode;

public class KartLapTracker : NetworkBehaviour
{
    // We removed the public totalCheckpoints/totalLaps from here!
    // The kart will figure it out automatically when it spawns.
    private int mapTotalCheckpoints; 
    private int mapTotalLaps;

    [Header("Networked Progress")]
    public NetworkVariable<int> currentLap = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> nextCheckpointIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // When the kart spawns, ask the scene's TrackManager for the rules!
        if (TrackManager.Instance != null)
        {
            mapTotalCheckpoints = TrackManager.Instance.totalCheckpoints;
            mapTotalLaps = TrackManager.Instance.totalLaps;
        }
        else
        {
            Debug.LogError("No TrackManager found in the scene! The kart doesn't know the rules!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint hitCheckpoint = other.GetComponent<Checkpoint>();
            if (hitCheckpoint != null)
            {
                HitCheckpointServerRpc(hitCheckpoint.checkpointIndex);
            }
        }
    }

    [ServerRpc]
    private void HitCheckpointServerRpc(int indexHit, ServerRpcParams rpcParams = default)
    {
        // VALIDATION: Did they hit the correct next gate?
        if (indexHit == nextCheckpointIndex.Value)
        {
            nextCheckpointIndex.Value++;

            // LAP COMPLETION: Did they hit the final gate?
            if (nextCheckpointIndex.Value >= mapTotalCheckpoints)
            {
                nextCheckpointIndex.Value = 0; 
                currentLap.Value++;

                // RACE FINISHED
                if (currentLap.Value > mapTotalLaps)
                {
                    Debug.Log($"Player {OwnerClientId} finished the race!");
                }
            }
        }
    }
}