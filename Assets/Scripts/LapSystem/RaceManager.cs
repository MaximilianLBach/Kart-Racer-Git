using UnityEngine;
using Unity.Netcode;
using System.Collections;
using TMPro;
using System.Collections.Generic;

public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance;

    [Header("UI Hookup")]
    public TextMeshProUGUI countdownText;

    [Header("Leaderboard")]
    public List<ulong> raceLeaderboard = new List<ulong>();

    public static List<ulong> FinalLeaderboard = new List<ulong>();

    public enum RaceState
    {
        WaitingForPlayers,
        Countdown,
        Racing,
        Finished
    }

    // This automatically syncs the state from the Host to all Clients
    public NetworkVariable<RaceState> CurrentState = new NetworkVariable<RaceState>(RaceState.WaitingForPlayers);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // The physical VR button will call this!
    public void HostStartCountdown()
    {
        if (!IsServer || CurrentState.Value != RaceState.WaitingForPlayers) return;
        
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        CurrentState.Value = RaceState.Countdown;
        
        // TODO: Play 3... 2... 1... UI/Audio here via an RPC
        UpdateScreenClientRpc("3");
        yield return new WaitForSeconds(1f);
        UpdateScreenClientRpc("2");
        yield return new WaitForSeconds(1f);
        UpdateScreenClientRpc("1");
        yield return new WaitForSeconds(1f);
        
        UpdateScreenClientRpc("GO!!!");
        CurrentState.Value = RaceState.Racing;

        yield return new WaitForSeconds(2f);
        UpdateScreenClientRpc("");
    }

    [ClientRpc]
    private void UpdateScreenClientRpc(string textToDisplay)
    {
        if (countdownText != null)
        {
            countdownText.text = textToDisplay;
        }
    }

    public void AllPlayersFinished()
    {
        if (!IsServer) return;
        CurrentState.Value = RaceState.Finished;
        StartCoroutine(PodiumTransitionRoutine());
    }

    private IEnumerator PodiumTransitionRoutine()
    {
        // Wait 5 seconds to let players cross the line and celebrate
        yield return new WaitForSeconds(5f);
        
        // --- NEW CLEANUP CODE ---
        // Find every single kart in the track scene
        V5KartController[] allKarts = FindObjectsByType<V5KartController>(FindObjectsInactive.Exclude);
        
        foreach (V5KartController kart in allKarts)
        {
            // Tell the server to completely destroy the kart for all players
            if (kart.NetworkObject != null && kart.NetworkObject.IsSpawned)
            {
                kart.NetworkObject.Despawn();
            }
        }

        // Give the network a tiny fraction of a second to sync the destruction
        yield return new WaitForSeconds(0.1f);
        // ------------------------
        FinalLeaderboard = new List<ulong>(raceLeaderboard);
        // Now load the scene! The old karts are dead, and the Podium Spawner will build fresh ones.
        NetworkManager.Singleton.SceneManager.LoadScene("PodiumScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void PlayerCrossedFinishLine(ulong clientId)
    {
        if (!IsServer) return;

        // Prevent adding the same player twice if they keep driving over the line
        if (raceLeaderboard.Contains(clientId)) return;

        // Add them to the leaderboard. Their position is just the count of the list!
        raceLeaderboard.Add(clientId);
        int placement = raceLeaderboard.Count;

        Debug.Log($"Player {clientId} finished in position {placement}!");

        // Target ONLY the player who just finished to show them their screen
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };

        NotifyPlayerFinishedClientRpc(placement, clientRpcParams);

        // Did the last player just cross the line?
        if (raceLeaderboard.Count >= NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            AllPlayersFinished();
        }
    }

    [ClientRpc]
    private void NotifyPlayerFinishedClientRpc(int placement, ClientRpcParams clientRpcParams = default)
    {
        // 1. Change the hologram text to show their placement
        if (countdownText != null)
        {
            countdownText.text = $"FINISHED!\nPosition: {placement}";
        }

        // 2. Find their local kart and freeze it
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null && 
            NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent(out V5KartController localKart))
        {
            localKart.hasFinishedRace = true;
        }
    }
}