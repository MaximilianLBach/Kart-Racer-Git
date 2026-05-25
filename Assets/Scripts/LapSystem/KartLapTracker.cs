using UnityEngine;
using Unity.Netcode;

public class KartLapTracker : NetworkBehaviour
{
    private int mapTotalLaps;

    [Header("Networked Progress")]
    public NetworkVariable<int> currentLap = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // We replace the strict index with a simple counter. 
    // This tracks if we have driven out onto the track so we can't cheat by reversing over the finish line.
    public NetworkVariable<int> checkpointsHitThisLap = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> lastHitCheckpointIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Respawn System")]
    public V5KartController kartController; 
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    
    // NEW: We save the floor direction of the checkpoint!
    private Vector3 respawnGravityDir = Vector3.down; 

    public override void OnNetworkSpawn()
    {
        if (TrackManager.Instance != null)
        {
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
                // Take a "Snapshot" of this checkpoint's location and rotation
                respawnPosition = hitCheckpoint.transform.position;
                respawnRotation = hitCheckpoint.transform.rotation;

                // NEW: Take a snapshot of the gravity! (-transform.up is the floor of the checkpoint)
                respawnGravityDir = -hitCheckpoint.transform.up;

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
        // 1. Kill all falling momentum
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

        // 4. NEW: Force the kart to adopt the checkpoint's gravity so we stick to the wall!
        kartController.currentGravityDir = respawnGravityDir;
    }

    [ServerRpc]
    private void HitCheckpointServerRpc(int indexHit, ServerRpcParams rpcParams = default)
    {
        // We assume Checkpoint Index 0 is the Start/Finish line
        if (indexHit == 0)
        {
            // Have they hit at least 1 other checkpoint out on the track?
            if (checkpointsHitThisLap.Value > 0)
            {
                currentLap.Value++;
                
                // Reset their progress for the next lap
                checkpointsHitThisLap.Value = 0; 
                lastHitCheckpointIndex.Value = 0;

                if (currentLap.Value > mapTotalLaps)
                {
                    if (RaceManager.Instance != null)
                    {
                        // Tell the server this specific kart is done!
                        RaceManager.Instance.PlayerCrossedFinishLine(OwnerClientId);
                    }
                }
            }
        }
        else
        {
            // We hit a normal checkpoint out on the track (e.g., branching paths)
            // As long as we aren't just sitting still inside the exact same checkpoint box, count it as progress!
            if (indexHit != lastHitCheckpointIndex.Value)
            {
                checkpointsHitThisLap.Value++;
                lastHitCheckpointIndex.Value = indexHit;
            }
        }
    }
}