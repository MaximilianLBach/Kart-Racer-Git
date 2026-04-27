using UnityEngine;
using Unity.Netcode;
using TMPro; 
using System.Collections;
using System.Collections.Generic;

public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance;

    [Header("UI References")]
    public GameObject hostStartButton;
    public TextMeshProUGUI countdownText;
    public GameObject scoreboardPanel;
    public TextMeshProUGUI scoreboardText;

    [Header("Race State")]
    public NetworkVariable<bool> isRaceActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private float raceStartTime;
    private int totalPlayersAtStart; // NEW: Tracks how many racers there are

    private struct RaceResult
    {
        public ulong clientId;
        public float finishTime;
    }
    private List<RaceResult> raceResults = new List<RaceResult>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        hostStartButton.SetActive(IsServer);
        scoreboardPanel.SetActive(false);
        countdownText.text = "Waiting for Host...";
    }

    public void StartCountdownButton()
    {
        if (IsServer)
        {
            hostStartButton.SetActive(false);
            
            // Lock in the number of players connected right as the host hits Start
            totalPlayersAtStart = NetworkManager.Singleton.ConnectedClientsIds.Count;
            
            StartCoroutine(CountdownRoutine());
        }
    }

    private IEnumerator CountdownRoutine()
    {
        UpdateCountdownUIClientRpc("3");
        yield return new WaitForSeconds(1f);
        
        UpdateCountdownUIClientRpc("2");
        yield return new WaitForSeconds(1f);
        
        UpdateCountdownUIClientRpc("1");
        yield return new WaitForSeconds(1f);
        
        UpdateCountdownUIClientRpc("GO!");
        
        raceStartTime = Time.time;
        isRaceActive.Value = true;

        yield return new WaitForSeconds(1f);
        UpdateCountdownUIClientRpc(""); 
    }

    [ClientRpc]
    private void UpdateCountdownUIClientRpc(string text)
    {
        countdownText.text = text;
    }

    public void PlayerFinished(ulong clientId)
    {
        if (!IsServer) return;

        float timeTaken = Time.time - raceStartTime;
        raceResults.Add(new RaceResult { clientId = clientId, finishTime = timeTaken });

        // Tell this specific player their time, and tell them to wait
        ShowWaitingScreenClientRpc(clientId, timeTaken);

        // Check if EVERYONE is finished
        if (raceResults.Count >= totalPlayersAtStart)
        {
            raceResults.Sort((a, b) => a.finishTime.CompareTo(b.finishTime));
            
            string board = "FINAL RESULTS\n\n";
            for (int i = 0; i < raceResults.Count; i++)
            {
                board += $"{i + 1}. Player {raceResults[i].clientId} - {raceResults[i].finishTime:F2}s\n";
            }

            UpdateScoreboardClientRpc(board);
        }
    }

    [ClientRpc]
    private void ShowWaitingScreenClientRpc(ulong finishedClientId, float time)
    {
        // Only show this message to the person who just finished
        if (NetworkManager.Singleton.LocalClientId == finishedClientId)
        {
            countdownText.text = $"Finished in {time:F2}s!\nWaiting for other racers...";
        }
    }

    [ClientRpc]
    private void UpdateScoreboardClientRpc(string boardText)
    {
        countdownText.text = ""; // Clear the waiting text
        scoreboardPanel.SetActive(true);
        scoreboardText.text = boardText;
    }
}