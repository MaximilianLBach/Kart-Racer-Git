using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class UGSManager : MonoBehaviour
{
    // A singleton makes it easy for our future Lobby scripts to check if we are connected
    public static UGSManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this alive when we load the Race Track!
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            // 1. Create a random profile name so our Local Clones don't clash with each other
            InitializationOptions options = new InitializationOptions();
            string randomProfile = "Player_" + System.Guid.NewGuid().ToString().Substring(0, 5);
            options.SetProfile(randomProfile);

            // 2. Initialize the cloud connection
            await UnityServices.InitializeAsync(options);
            Debug.Log("Unity Services Initialized Successfully.");

            // 3. Log the player in anonymously 
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Logged into Unity Cloud! Player ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        }
    }
}