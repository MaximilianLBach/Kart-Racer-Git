using UnityEngine;
using Unity.Netcode;

public class KartLapTracker : NetworkBehaviour
{
    [Header("Networked Progress")]
    public NetworkVariable<bool> isFinished = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private int mapTotalCheckpoints; 
    private int mapTotalLaps;
    private bool hasFinishedRace = false;

    [Header("Networked Progress")]
    public NetworkVariable<int> currentLap = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> nextCheckpointIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Respawn System")]
    public V5KartController kartController; // We need this to stop the sphere's momentum!
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;

    public override void OnNetworkSpawn()
    {
        if (TrackManager.Instance != null)
        {
            mapTotalCheckpoints = TrackManager.Instance.totalCheckpoints;
            mapTotalLaps = TrackManager.Instance.totalLaps;
        }

        // Set the initial respawn point to the starting line
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        // 1. DID WE HIT A CHECKPOINT?
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint hitCheckpoint = other.GetComponent<Checkpoint>();
            if (hitCheckpoint != null)
            {
                // Take a "Snapshot" of this checkpoint to use as our new respawn pad!
                respawnPosition = hitCheckpoint.transform.position;
                respawnRotation = hitCheckpoint.transform.rotation;

                HitCheckpointServerRpc(hitCheckpoint.checkpointIndex);
            }
        }
        
        // 2. DID WE FALL OFF THE TRACK?
        else if (other.CompareTag("DeathZone"))
        {
            RespawnKart();
        }
    }

    private void RespawnKart()
    {
        // 1. Kill all falling momentum so we don't instantly slide off again
        if (kartController.sphereRB != null)
        {
            kartController.sphereRB.linearVelocity = Vector3.zero;
            kartController.sphereRB.angularVelocity = Vector3.zero;
            
            // 2. Teleport the invisible physics sphere
            kartController.sphereRB.transform.position = respawnPosition;
        }

        // 3. Teleport and align the visual kart
        transform.position = respawnPosition;
        transform.rotation = respawnRotation;
    }

    [ServerRpc]
    private void HitCheckpointServerRpc(int indexHit, ServerRpcParams rpcParams = default)
    {
        if (indexHit == nextCheckpointIndex.Value)
        {
            nextCheckpointIndex.Value++;

            if (nextCheckpointIndex.Value >= mapTotalCheckpoints)
            {
                nextCheckpointIndex.Value = 0; 
                currentLap.Value++;

                if (currentLap.Value > mapTotalLaps)
             {
                 if (!hasFinishedRace)
                 {
                     hasFinishedRace = true;
                     isFinished.Value = true; // Tell everyone this kart is done!
                     
                     // Tell the server we crossed the finish line!
                     RaceManager.Instance.PlayerFinished(OwnerClientId);
                 }
             }
            }
        }
    }
}