using UnityEngine;

public class TrackManager : MonoBehaviour
{
    // This makes the TrackManager a "Singleton" so any kart can instantly find it
    public static TrackManager Instance;

    [Header("Map Specific Rules")]
    public int totalCheckpoints = 5; // Change this for each different map!
    public int totalLaps = 3;        // Change this if it's a long or short track!

    private void Awake()
    {
        // When the scene loads, this script announces itself as the boss
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
}