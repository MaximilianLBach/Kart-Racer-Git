using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleMusicManager : MonoBehaviour
{
    public static SimpleMusicManager Instance;

    [Header("Audio Component")]
    [SerializeField] private AudioSource audioSource;

    [Header("Playlists")]
    [SerializeField] private AudioClip[] menuPlaylist;
    [SerializeField] private AudioClip[] snowMapPlaylist;     // <-- NEW
    [SerializeField] private AudioClip[] shroomVroomPlaylist; // <-- NEW
    [SerializeField] private AudioClip[] podiumPlaylist;
    [SerializeField] private AudioClip[] defaultRacePlaylist; // <-- Fallback

    private AudioClip[] currentPlaylist;
    private int lastPlayedIndex = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. Check for the Menu and Podium
        if (scene.name == "MainMenu") 
        {
            SwitchPlaylist(menuPlaylist);
        }
        else if (scene.name == "PodiumScene")
        {
            SwitchPlaylist(podiumPlaylist);
        }
        // 2. Check for your specific maps!
        // IMPORTANT: Make sure these strings match your exact Unity scene file names
        else if (scene.name == "SnowMap") 
        {
            SwitchPlaylist(snowMapPlaylist);
        }
        else if (scene.name == "ShroomVroom") 
        {
            SwitchPlaylist(shroomVroomPlaylist);
        }
        // 3. The Fallback
        else
        {
            SwitchPlaylist(defaultRacePlaylist);
        }
    }

    private void SwitchPlaylist(AudioClip[] newPlaylist)
    {
        if (newPlaylist == null || newPlaylist.Length == 0) return;

        currentPlaylist = newPlaylist;
        lastPlayedIndex = -1; 
        PlayRandomTrack();
    }

    private void Update()
    {
        if (currentPlaylist != null && currentPlaylist.Length > 0 && !audioSource.isPlaying)
        {
            PlayRandomTrack();
        }
    }

    private void PlayRandomTrack()
    {
        if (currentPlaylist == null || currentPlaylist.Length == 0) return;

        int randomIndex = 0;

        if (currentPlaylist.Length > 1)
        {
            do
            {
                randomIndex = Random.Range(0, currentPlaylist.Length);
            } while (randomIndex == lastPlayedIndex);
        }

        lastPlayedIndex = randomIndex;
        
        audioSource.clip = currentPlaylist[randomIndex];
        audioSource.Play();
    }
}