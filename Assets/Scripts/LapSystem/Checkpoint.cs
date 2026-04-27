using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Order")]
    [Tooltip("0 is the starting line, 1 is the next gate, etc.")]
    public int checkpointIndex;
}
