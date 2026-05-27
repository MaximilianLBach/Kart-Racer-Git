using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Networking.Transport.Relay;
using System.Collections.Generic;

[System.Serializable]
public struct MapInfo
{
    public string displayName;   // What the player sees on the UI (e.g., "Snow Peak")
    public string sceneName;     // The exact name of the Unity Scene file (e.g., "SnowMap")
}
public class LobbyManager : MonoBehaviour
{
    [Header("Settings")]
    public int maxPlayers = 4;


    [Header("UI Feedback")]
    public TMPro.TextMeshProUGUI statusText;

    [Header("Map Selection")]
    public MapInfo[] availableMaps;
    public TMPro.TextMeshProUGUI mapDisplayUI; // The text on the computer screen
    private int currentMapIndex = 0;
    private string selectedSceneName = ""; // The hidden scene name we will load

    private Lobby currentLobby;
    private float heartbeatTimer;
    private bool isLobbyHost;


    private void Start()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

        UpdateMapUI();
    }

    public void NextMap()
    {
        if (availableMaps.Length == 0) return;
        
        currentMapIndex++;
        if (currentMapIndex >= availableMaps.Length) currentMapIndex = 0; // Wrap around to the start
        
        UpdateMapUI();
    }

    public void PreviousMap()
    {
        if (availableMaps.Length == 0) return;
        
        currentMapIndex--;
        if (currentMapIndex < 0) currentMapIndex = availableMaps.Length - 1; // Wrap around to the end
        
        UpdateMapUI();
    }

    private void UpdateMapUI()
    {
        if (availableMaps.Length == 0) return;

        // Update the visual text on the monitor
        if (mapDisplayUI != null)
        {
            mapDisplayUI.text = $"Map: {availableMaps[currentMapIndex].displayName}";
        }

        // Lock in the actual scene name for the server to use
        selectedSceneName = availableMaps[currentMapIndex].sceneName;
    }

    private void Update()
    {
        if (isLobbyHost && currentLobby != null)
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer > 15f)
            {
                heartbeatTimer = 0f;
                LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    public async void CreateRace()
    {
        if (statusText) statusText.text = "Starting Server...";

        try
        {
            // 1. Setup Relay
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // 2. Setup Lobby
            CreateLobbyOptions lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync("Kart Race", maxPlayers, lobbyOptions);
            isLobbyHost = true;

            // 3. Launch
            if (statusText) statusText.text = "Loading Track...";
            NetworkManager.Singleton.StartHost();
            await System.Threading.Tasks.Task.Delay(1000); // Buffer for Relay port binding
            NetworkManager.Singleton.SceneManager.LoadScene(selectedSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            if (statusText) statusText.text = "Failed to Host!";
        }
    }

    public async void JoinRace()
    {
        if (statusText) statusText.text = "Connecting...";

        // 1. Clean up potential ghost data from previous disconnects
        if (currentLobby != null)
        {
            try { await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId); } 
            catch { }
            currentLobby = null;
        }

        try
        {
            // 2. Find Lobby & Extract Code
            currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            string joinCode = currentLobby.Data["JoinCode"].Value;

            // 3. Connect to Relay
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // 4. Launch
            if (!NetworkManager.Singleton.StartClient())
            {
                if (statusText) statusText.text = "Socket Error!";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            if (statusText) statusText.text = "No Lobbies Found!";
            currentLobby = null;
        }
    }

    private async void HandleClientDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (statusText) statusText.text = "Connection Dropped!";
            if (currentLobby != null && !isLobbyHost)
            {
                try { await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId); } 
                catch { }
                currentLobby = null;
            }
        }
    }

    private void OnApplicationQuit() => CleanupLobby();
    private void OnDestroy() => CleanupLobby();

    private async void CleanupLobby()
    {
        if (currentLobby != null)
        {
            try
            {
                if (isLobbyHost) await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                else await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
            }
            catch { }
            currentLobby = null;
        }
    }
}