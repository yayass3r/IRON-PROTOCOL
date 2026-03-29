// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: TurnManager.cs
// Namespace: IronProtocol.GameSystems.Multiplayer
// Description: Async multiplayer turn synchronization manager. Handles match
//              lifecycle, turn submission, opponent turn receipt, replay
//              requests, and server polling for async turn-based play.
// ============================================================================

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace IronProtocol.GameSystems.Multiplayer
{
    // ========================================================================
    // Serializable Data Structures
    // ========================================================================

    /// <summary>
    /// Represents a single unit movement order within a turn.
    /// </summary>
    [Serializable]
    public class UnitMove
    {
        /// <summary>Unique identifier of the unit being moved.</summary>
        public string unitId;

        /// <summary>Origin hex coordinate (Q, R in axial coordinates).</summary>
        public int fromQ, fromR;

        /// <summary>Destination hex coordinate (Q, R in axial coordinates).</summary>
        public int toQ, toR;

        /// <summary>Whether this move includes a path through enemy ZoC (costs extra).</summary>
        public bool throughEnemyZone;
    }

    /// <summary>
    /// Represents a single unit attack order within a turn.
    /// </summary>
    [Serializable]
    public class UnitAttack
    {
        /// <summary>Unique identifier of the attacking unit.</summary>
        public string attackerId;

        /// <summary>Unique identifier of the defending unit (if known).</summary>
        public string defenderId;

        /// <summary>Target hex coordinate (Q, R in axial coordinates).</summary>
        public int targetQ, targetR;
    }

    /// <summary>
    /// Represents a trade order placed on the market during a turn.
    /// </summary>
    [Serializable]
    public class TradeOrder
    {
        /// <summary>Unique identifier for the resource being traded.</summary>
        public string resourceId;

        /// <summary>Whether this is a buy (true) or sell (false) order.</summary>
        public bool isBuyOrder;

        /// <summary>Quantity of the resource to trade.</summary>
        public float quantity;

        /// <summary>Maximum price the player is willing to pay (buy) or minimum (sell).</summary>
        public float priceLimit;
    }

    /// <summary>
    /// Serializable container for all actions a player takes during one turn.
    /// Submitted to the server and received from opponents.
    /// </summary>
    [Serializable]
    public class TurnData
    {
        /// <summary>The player who submitted this turn data.</summary>
        public string playerId;

        /// <summary>The turn number this data corresponds to.</summary>
        public int turnNumber;

        /// <summary>Timestamp when the turn was submitted (UTC ticks).</summary>
        public long submittedAt;

        /// <summary>All unit movement orders issued this turn.</summary>
        public List<UnitMove> moves = new List<UnitMove>();

        /// <summary>All unit attack orders issued this turn.</summary>
        public List<UnitAttack> attacks = new List<UnitAttack>();

        /// <summary>All trade orders placed on the market this turn.</summary>
        public List<TradeOrder> trades = new List<TradeOrder>();

        /// <summary>
        /// Serializes this TurnData to a JSON string for network transmission.
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// Deserializes a TurnData from a JSON string.
        /// </summary>
        /// <param name="json">JSON string to parse.</param>
        /// <returns>Parsed TurnData, or null if deserialization fails.</returns>
        public static TurnData FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<TurnData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TurnData] Failed to deserialize: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Response from the server after submitting a turn.
    /// </summary>
    [Serializable]
    public class TurnSubmitResponse
    {
        public bool success;
        public string message;
        public int confirmedTurnNumber;
    }

    /// <summary>
    /// Response from the server during update polling.
    /// </summary>
    [Serializable]
    public class PollResponse
    {
        public bool opponentTurnReady;
        public string opponentTurnJson;
        public int latestTurnNumber;
        public bool matchEnded;
        public string winnerId;
    }

    // ========================================================================
    // TurnManager
    // ========================================================================

    /// <summary>
    /// MonoBehaviour that manages asynchronous multiplayer turn synchronization.
    /// Handles match lifecycle, turn data submission/receipt, replay requests,
    /// and periodic server polling for opponent turn availability.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields
        // ------------------------------------------------------------------

        [Header("Server Configuration")]
        [Tooltip("Base URL for the multiplayer API server.")]
        [SerializeField] private string serverBaseUrl = "https://api.ironprotocol.game";

        [Tooltip("API key for server authentication.")]
        [SerializeField] private string apiKey;

        [Header("Polling")]
        [Tooltip("How often (in seconds) to poll the server for opponent turn updates.")]
        [SerializeField] private float pollIntervalSeconds = 10f;

        [Tooltip("Whether automatic polling is enabled.")]
        [SerializeField] private bool enableAutoPolling = true;

        // ------------------------------------------------------------------
        // Public State
        // ------------------------------------------------------------------

        /// <summary>The unique identifier for the current multiplayer match.</summary>
        public string CurrentMatchId { get; private set; }

        /// <summary>The local player's unique identifier.</summary>
        public string CurrentPlayerId { get; private set; }

        /// <summary>The turn number we expect to process next.</summary>
        public int ExpectedTurnNumber { get; private set; } = 1;

        /// <summary>Whether the local player has submitted their turn for the current round.</summary>
        public bool HasSubmittedTurn { get; private set; } = false;

        /// <summary>Whether the opponent's turn has been received for the current round.</summary>
        public bool HasOpponentTurn { get; private set; } = false;

        /// <summary>Whether the match is currently active.</summary>
        public bool IsMatchActive { get; private set; } = false;

        /// <summary>The most recently received opponent turn data.</summary>
        public TurnData LastOpponentTurnData { get; private set; }

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when a match is successfully started. Parameter: matchId.</summary>
        public event Action<string> OnMatchStarted;

        /// <summary>Fired when the local player's turn is successfully submitted. Parameter: turnNumber.</summary>
        public event Action<int> OnTurnSubmitted;

        /// <summary>Fired when the opponent's turn data is received. Parameter: TurnData.</summary>
        public event Action<TurnData> OnOpponentTurnReceived;

        /// <summary>Fired when both turns are available and the round can be resolved.</summary>
        public event Action OnRoundReady;

        /// <summary>Fired when a replay is loaded. Parameter: turn number.</summary>
        public event Action<int> OnReplayLoaded;

        /// <summary>Fired when the match ends. Parameter: winnerPlayerId.</summary>
        public event Action<string> OnMatchEnded;

        /// <summary>Fired when a server error occurs. Parameter: error message.</summary>
        public event Action<string> OnError;

        // ------------------------------------------------------------------
        // Private State
        // ------------------------------------------------------------------

        private Coroutine _pollingCoroutine;
        private TurnData _pendingSubmitData;
        private bool _isSubmitting = false;

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void OnDestroy()
        {
            StopPolling();
        }

        // ------------------------------------------------------------------
        // Public API - Match Management
        // ------------------------------------------------------------------

        /// <summary>
        /// Starts or joins a multiplayer match with the given identifier.
        /// Initializes polling and resets turn state.
        /// </summary>
        /// <param name="matchId">The unique match identifier from the server.</param>
        public void StartMatch(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                Debug.LogError("[TurnManager] Cannot start match with null or empty matchId.");
                return;
            }

            CurrentMatchId = matchId;
            ExpectedTurnNumber = 1;
            HasSubmittedTurn = false;
            HasOpponentTurn = false;
            IsMatchActive = true;
            LastOpponentTurnData = null;

            Debug.Log($"[TurnManager] Match started: {matchId}");

            OnMatchStarted?.Invoke(matchId);

            // Begin polling for updates
            if (enableAutoPolling)
            {
                StartPolling();
            }
        }

        /// <summary>
        /// Ends the current match and stops all server communication.
        /// </summary>
        public void EndMatch()
        {
            IsMatchActive = false;
            StopPolling();
            Debug.Log("[TurnManager] Match ended.");
        }

        /// <summary>
        /// Sets the local player's unique identifier.
        /// Must be called before submitting turns.
        /// </summary>
        /// <param name="playerId">The player's unique identifier.</param>
        public void SetPlayerId(string playerId)
        {
            CurrentPlayerId = playerId;
            Debug.Log($"[TurnManager] Player ID set: {playerId}");
        }

        // ------------------------------------------------------------------
        // Public API - Turn Submission
        // ------------------------------------------------------------------

        /// <summary>
        /// Submits the player's turn data to the server.
        /// The data includes all movement, attack, and trade orders.
        /// </summary>
        /// <param name="turnData">The complete turn data to submit.</param>
        public void SubmitTurn(TurnData turnData)
        {
            if (!IsMatchActive)
            {
                Debug.LogWarning("[TurnManager] Cannot submit turn – no active match.");
                return;
            }

            if (string.IsNullOrEmpty(CurrentPlayerId))
            {
                Debug.LogError("[TurnManager] Cannot submit turn – player ID not set.");
                return;
            }

            if (_isSubmitting)
            {
                Debug.LogWarning("[TurnManager] Turn submission already in progress.");
                return;
            }

            // Populate metadata
            turnData.playerId = CurrentPlayerId;
            turnData.turnNumber = ExpectedTurnNumber;
            turnData.submittedAt = DateTime.UtcNow.Ticks;

            _pendingSubmitData = turnData;
            _isSubmitting = true;

            StartCoroutine(SubmitTurnCoroutine(turnData));
        }

        /// <summary>
        /// Called when the opponent's turn data is received from the server.
        /// Stores the data and checks if both turns are ready for resolution.
        /// </summary>
        /// <param name="turnData">The opponent's turn data.</param>
        public void OnOpponentTurnReceived(TurnData turnData)
        {
            if (turnData == null)
            {
                Debug.LogWarning("[TurnManager] Null opponent turn data received.");
                return;
            }

            if (turnData.turnNumber != ExpectedTurnNumber)
            {
                Debug.LogWarning($"[TurnManager] Opponent turn {turnData.turnNumber} doesn't match expected {ExpectedTurnNumber}.");
                return;
            }

            LastOpponentTurnData = turnData;
            HasOpponentTurn = true;

            Debug.Log($"[TurnManager] Opponent turn received: Turn {turnData.turnNumber} from {turnData.playerId}");
            Debug.Log($"  Moves: {turnData.moves?.Count ?? 0}, Attacks: {turnData.attacks?.Count ?? 0}, Trades: {turnData.trades?.Count ?? 0}");

            OnOpponentTurnReceived?.Invoke(turnData);

            // Check if both turns are ready
            if (HasSubmittedTurn && HasOpponentTurn)
            {
                Debug.Log("[TurnManager] Both turns ready – round can be resolved.");
                OnRoundReady?.Invoke();
            }
        }

        // ------------------------------------------------------------------
        // Public API - Replay
        // ------------------------------------------------------------------

        /// <summary>
        /// Requests a replay of a specific turn from the server.
        /// </summary>
        /// <param name="turnNumber">The turn number to replay (1-based).</param>
        public void RequestReplay(int turnNumber)
        {
            if (string.IsNullOrEmpty(CurrentMatchId))
            {
                Debug.LogWarning("[TurnManager] Cannot request replay – no active match.");
                return;
            }

            StartCoroutine(RequestReplayCoroutine(turnNumber));
        }

        // ------------------------------------------------------------------
        // Public API - Polling Control
        // ------------------------------------------------------------------

        /// <summary>
        /// Starts the automatic server polling coroutine.
        /// Checks for opponent turn updates every <see cref="pollIntervalSeconds"/>.
        /// </summary>
        public void StartPolling()
        {
            if (_pollingCoroutine != null)
            {
                StopCoroutine(_pollingCoroutine);
            }

            _pollingCoroutine = StartCoroutine(PollForUpdates());
            Debug.Log($"[TurnManager] Polling started (interval: {pollIntervalSeconds}s)");
        }

        /// <summary>Stops the automatic server polling coroutine.</summary>
        public void StopPolling()
        {
            if (_pollingCoroutine != null)
            {
                StopCoroutine(_pollingCoroutine);
                _pollingCoroutine = null;
                Debug.Log("[TurnManager] Polling stopped.");
            }
        }

        /// <summary>
        /// Advances the expected turn number after both turns have been resolved.
        /// </summary>
        public void AdvanceToNextTurn()
        {
            ExpectedTurnNumber++;
            HasSubmittedTurn = false;
            HasOpponentTurn = false;
            LastOpponentTurnData = null;
            Debug.Log($"[TurnManager] Advanced to turn {ExpectedTurnNumber}");
        }

        // ------------------------------------------------------------------
        // Coroutines
        // ------------------------------------------------------------------

        /// <summary>
        /// Periodically polls the server for opponent turn updates.
        /// Runs continuously until <see cref="StopPolling"/> is called or the match ends.
        /// </summary>
        private IEnumerator PollForUpdates()
        {
            while (IsMatchActive)
            {
                // Don't poll if we already have both turns
                if (HasSubmittedTurn && HasOpponentTurn)
                {
                    yield return new WaitForSeconds(pollIntervalSeconds);
                    continue;
                }

                // Don't poll if we already have the opponent's turn
                if (HasOpponentTurn)
                {
                    yield return new WaitForSeconds(pollIntervalSeconds);
                    continue;
                }

                string url = $"{serverBaseUrl}/matches/{CurrentMatchId}/poll?playerId={CurrentPlayerId}&expectedTurn={ExpectedTurnNumber}";

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    }
                    request.SetRequestHeader("Content-Type", "application/json");

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        PollResponse response = JsonUtility.FromJson<PollResponse>(request.downloadHandler.text);
                        if (response != null)
                        {
                            // Check if match has ended
                            if (response.matchEnded && !string.IsNullOrEmpty(response.winnerId))
                            {
                                IsMatchActive = false;
                                OnMatchEnded?.Invoke(response.winnerId);
                                yield break;
                            }

                            // Check if opponent turn is available
                            if (response.opponentTurnReady && !string.IsNullOrEmpty(response.opponentTurnJson))
                            {
                                TurnData opponentData = TurnData.FromJson(response.opponentTurnJson);
                                if (opponentData != null)
                                {
                                    OnOpponentTurnReceived(opponentData);
                                }
                            }

                            // Update expected turn if server reports a higher number
                            if (response.latestTurnNumber > ExpectedTurnNumber)
                            {
                                ExpectedTurnNumber = response.latestTurnNumber;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[TurnManager] Poll failed: {request.error}");
                        OnError?.Invoke($"Poll failed: {request.error}");
                    }
                }

                yield return new WaitForSeconds(pollIntervalSeconds);
            }
        }

        /// <summary>
        /// Sends the player's turn data to the server via HTTP POST.
        /// </summary>
        private IEnumerator SubmitTurnCoroutine(TurnData turnData)
        {
            string url = $"{serverBaseUrl}/matches/{CurrentMatchId}/turns";
            string jsonBody = turnData.ToJson();

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    TurnSubmitResponse response = JsonUtility.FromJson<TurnSubmitResponse>(request.downloadHandler.text);
                    HasSubmittedTurn = true;
                    _isSubmitting = false;

                    Debug.Log($"[TurnManager] Turn {turnData.turnNumber} submitted successfully.");
                    OnTurnSubmitted?.Invoke(turnData.turnNumber);

                    // Check if both turns are now ready
                    if (HasOpponentTurn)
                    {
                        Debug.Log("[TurnManager] Both turns ready – round can be resolved.");
                        OnRoundReady?.Invoke();
                    }
                }
                else
                {
                    _isSubmitting = false;
                    Debug.LogError($"[TurnManager] Turn submission failed: {request.error}");
                    OnError?.Invoke($"Turn submission failed: {request.error}");
                }
            }
        }

        /// <summary>
        /// Requests replay data for a specific turn from the server.
        /// </summary>
        private IEnumerator RequestReplayCoroutine(int turnNumber)
        {
            string url = $"{serverBaseUrl}/matches/{CurrentMatchId}/replay?turn={turnNumber}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[TurnManager] Replay data received for turn {turnNumber}.");
                    OnReplayLoaded?.Invoke(turnNumber);

                    // The game would deserialize and replay the turn data here
                    // ReplayData replayData = JsonUtility.FromJson<ReplayData>(request.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning($"[TurnManager] Replay request failed for turn {turnNumber}: {request.error}");
                    OnError?.Invoke($"Replay request failed: {request.error}");
                }
            }
        }
    }
}
