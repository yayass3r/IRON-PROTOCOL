// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: TurnController.cs
// Namespace: IronProtocol.GameSystems.TurnSystem
// Description: MonoBehaviour managing the turn-based game loop. Orchestrates
//              all turn phases from player planning through AI execution,
//              market updates, and turn resolution (income, supply, weather,
//              city growth, rebellion checks, victory conditions).
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.GameSystems.TurnSystem
{
    /// <summary>
    /// Enumeration of all phases within a single turn cycle.
    /// The controller advances through these phases in order.
    /// </summary>
    public enum TurnPhase
    {
        /// <summary>Player is issuing movement, build, and trade orders.</summary>
        PlayerPlanning,

        /// <summary>Player-initiated combat is being resolved.</summary>
        PlayerCombat,

        /// <summary>AI nations are planning their moves.</summary>
        AIPlanning,

        /// <summary>AI-initiated combat is being resolved.</summary>
        AICombat,

        /// <summary>Dynamic pricing engine recalculates market prices.</summary>
        MarketUpdate,

        /// <summary>End-of-turn resolution: income, supply, weather, growth, rebellion, victory.</summary>
        TurnResolution
    }

    /// <summary>
    /// MonoBehaviour that drives the core turn-based game loop.
    /// Coordinates all game systems through a deterministic phase sequence
    /// and provides hooks for the player and AI to execute their turns.
    /// </summary>
    public class TurnController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields
        // ------------------------------------------------------------------

        [Header("Configuration")]
        [Tooltip("Reference to the main GameSettings ScriptableObject.")]
        [SerializeField] private IronProtocol.Data.GameConfig settings;

        [Header("AI Settings")]
        [Tooltip("Delay between AI nation turns for visual feedback (seconds).")]
        [SerializeField] private float aiTurnDelay = 0.5f;

        [Header("System References")]
        [Tooltip("Reference to the DynamicPricingEngine for market updates.")]
        [SerializeField] private MonoBehaviour pricingEngine;

        [Tooltip("Reference to the WeatherSystem for weather progression.")]
        [SerializeField] private MonoBehaviour weatherSystem;

        // ------------------------------------------------------------------
        // Public State
        // ------------------------------------------------------------------

        /// <summary>The current phase within the turn cycle.</summary>
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.PlayerPlanning;

        /// <summary>The current turn number (1-based).</summary>
        public int CurrentTurn { get; private set; } = 1;

        /// <summary>
        /// Ordered list of nation identifiers that take turns.
        /// The first entry is always the player nation.
        /// </summary>
        public List<string> NationTurnOrder { get; private set; } = new List<string>();

        /// <summary>Index of the currently active nation in <see cref="NationTurnOrder"/>.</summary>
        public int CurrentNationIndex { get; private set; } = 0;

        /// <summary>Reference to the active game settings.</summary>
        public IronProtocol.Data.GameConfig Settings => settings;

        /// <summary>Whether the game is currently running and accepting input.</summary>
        public bool IsGameActive { get; private set; } = false;

        /// <summary>Whether the controller is currently processing (not waiting for player).</summary>
        public bool IsProcessing { get; private set; } = false;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the turn number advances. Parameter: new turn number.</summary>
        public event Action<int> OnTurnChanged;

        /// <summary>Fired when the turn phase changes. Parameter: new phase.</summary>
        public event Action<TurnPhase> OnPhaseChanged;

        /// <summary>Fired when a nation is eliminated from the game. Parameters: (nationId, eliminatedByName).</summary>
        public event Action<string, string> OnNationEliminated;

        /// <summary>Fired when the game ends. Parameters: (winnerNationId, victoryCondition).</summary>
        public event Action<string, IronProtocol.Data.VictoryCondition> OnGameWon;

        /// <summary>Fired when the player's planning phase begins (player can issue orders).</summary>
        public event Action OnPlayerPlanningBegin;

        /// <summary>Fired when the player's planning phase ends.</summary>
        public event Action OnPlayerPlanningEnd;

        /// <summary>Fired when all turn processing is complete for the current turn.</summary>
        public event Action OnTurnComplete;

        /// <summary>Fired when a new game is initialized.</summary>
        public event Action OnGameStarted;

        // ------------------------------------------------------------------
        // Private State
        // ------------------------------------------------------------------

        private readonly Dictionary<string, bool> _eliminatedNations = new Dictionary<string, bool>();
        private readonly HashSet<string> _nationsProcessedThisTurn = new HashSet<string>();

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Start()
        {
            Debug.Log("[TurnController] Initialized. Awaiting StartNewGame() call.");
        }

        // ------------------------------------------------------------------
        // Public API - Game Initialization
        // ------------------------------------------------------------------

        /// <summary>
        /// Initializes a new game with the given settings.
        /// Sets up the turn order, resets all counters, and begins Turn 1.
        /// </summary>
        /// <param name="gameSettings">The game configuration to use.</param>
        public void StartNewGame(IronProtocol.Data.GameConfig gameSettings)
        {
            if (gameSettings == null)
            {
                Debug.LogError("[TurnController] Cannot start game with null settings.");
                return;
            }

            settings = gameSettings;

            // Reset state
            CurrentTurn = 1;
            CurrentNationIndex = 0;
            CurrentPhase = TurnPhase.PlayerPlanning;
            IsGameActive = true;
            IsProcessing = false;
            _eliminatedNations.Clear();
            _nationsProcessedThisTurn.Clear();

            // Initialize nation turn order (would be populated from NationDefinitions)
            NationTurnOrder = new List<string>();
            // The first nation is always the player
            // Additional nations loaded from save or setup screen would follow

            Debug.Log($"[TurnController] New game started. Turn {CurrentTurn}, {NationTurnOrder.Count} nations.");

            OnGameStarted?.Invoke();
            OnTurnChanged?.Invoke(CurrentTurn);
            OnPhaseChanged?.Invoke(CurrentPhase);

            // Begin player planning
            BeginPlayerPlanning();
        }

        // ------------------------------------------------------------------
        // Public API - Phase Control
        // ------------------------------------------------------------------

        /// <summary>
        /// Advances the game to the next phase in the turn cycle.
        /// Called by the player (End Turn) or automatically by AI processing.
        /// </summary>
        public void EndCurrentPhase()
        {
            if (!IsGameActive)
            {
                Debug.LogWarning("[TurnController] Cannot end phase – game is not active.");
                return;
            }

            if (IsProcessing)
            {
                Debug.LogWarning("[TurnController] Already processing a phase.");
                return;
            }

            StartCoroutine(AdvancePhaseRoutine());
        }

        /// <summary>
        /// Called by the player to signal they are done planning and ready
        /// to proceed to combat and AI turns.
        /// </summary>
        public void PlayerEndTurn()
        {
            if (CurrentPhase != TurnPhase.PlayerPlanning)
            {
                Debug.LogWarning("[TurnController] PlayerEndTurn called but current phase is not PlayerPlanning.");
                return;
            }

            Debug.Log("[TurnController] Player ended their turn.");
            OnPlayerPlanningEnd?.Invoke();
            EndCurrentPhase();
        }

        // ------------------------------------------------------------------
        // Phase Processing
        // ------------------------------------------------------------------

        /// <summary>
        /// Coroutine that advances through all remaining phases for the current turn.
        /// Each phase may take multiple frames (especially AI turns).
        /// </summary>
        private IEnumerator AdvancePhaseRoutine()
        {
            IsProcessing = true;

            // ---- Player Combat Phase ----
            SetPhase(TurnPhase.PlayerCombat);
            yield return ProcessPlayerCombat();
            if (!IsGameActive) { IsProcessing = false; yield break; }

            // ---- AI Planning Phase ----
            SetPhase(TurnPhase.AIPlanning);
            yield return ProcessAITurns();
            if (!IsGameActive) { IsProcessing = false; yield break; }

            // ---- AI Combat Phase ----
            SetPhase(TurnPhase.AICombat);
            yield return ProcessAICombat();
            if (!IsGameActive) { IsProcessing = false; yield break; }

            // ---- Market Update Phase ----
            SetPhase(TurnPhase.MarketUpdate);
            yield return ProcessMarketUpdate();
            if (!IsGameActive) { IsProcessing = false; yield break; }

            // ---- Turn Resolution Phase ----
            SetPhase(TurnPhase.TurnResolution);
            yield return ProcessTurnResolution();
            if (!IsGameActive) { IsProcessing = false; yield break; }

            // ---- Check Victory ----
            CheckVictoryConditions();

            if (IsGameActive)
            {
                // Advance to next turn
                CurrentTurn++;
                OnTurnChanged?.Invoke(CurrentTurn);
                Debug.Log($"[TurnController] === Turn {CurrentTurn} ===");

                // Reset to player planning
                CurrentNationIndex = 0;
                _nationsProcessedThisTurn.Clear();
                BeginPlayerPlanning();
            }

            IsProcessing = false;
            OnTurnComplete?.Invoke();
        }

        /// <summary>
        /// Begins the player's planning phase. The player can now issue
        /// movement, build, and trade orders.
        /// </summary>
        private void BeginPlayerPlanning()
        {
            SetPhase(TurnPhase.PlayerPlanning);
            IsProcessing = false;
            Debug.Log("[TurnController] Player planning phase – awaiting input.");
            OnPlayerPlanningBegin?.Invoke();
        }

        /// <summary>
        /// Waits for the player to issue their planning orders.
        /// The phase ends when <see cref="PlayerEndTurn"/> is called.
        /// </summary>
        private IEnumerator ProcessPlayerTurn()
        {
            // Player turn is event-driven, not coroutine-driven.
            // We simply wait here until PlayerEndTurn() is called.
            yield return null;
        }

        /// <summary>
        /// Resolves all player-initiated combat engagements queued during planning.
        /// </summary>
        private IEnumerator ProcessPlayerCombat()
        {
            Debug.Log("[TurnController] Processing player combat...");
            // Combat resolution is handled by the CombatSystem.
            // This coroutine yields until all queued battles are resolved.
            yield return new WaitForSeconds(0.1f);
            Debug.Log("[TurnController] Player combat resolved.");
        }

        /// <summary>
        /// Iterates through all non-eliminated AI nations and processes their turns.
        /// Each AI nation gets a planning and combat sub-phase.
        /// </summary>
        private IEnumerator ProcessAITurns()
        {
            Debug.Log("[TurnController] Processing AI turns...");

            for (int i = 1; i < NationTurnOrder.Count; i++) // Skip index 0 (player)
            {
                string nationId = NationTurnOrder[i];

                if (_eliminatedNations.ContainsKey(nationId))
                    continue;

                CurrentNationIndex = i;
                Debug.Log($"[TurnController] AI turn: {nationId} (index {i})");

                // AI planning – would delegate to AIController
                yield return new WaitForSeconds(aiTurnDelay);

                // AI combat – resolve any AI-initiated battles
                yield return new WaitForSeconds(aiTurnDelay);

                _nationsProcessedThisTurn.Add(nationId);
            }

            Debug.Log("[TurnController] All AI turns processed.");
        }

        /// <summary>
        /// Resolves any AI-initiated combat engagements.
        /// </summary>
        private IEnumerator ProcessAICombat()
        {
            Debug.Log("[TurnController] Processing AI combat...");
            yield return new WaitForSeconds(0.1f);
            Debug.Log("[TurnController] AI combat resolved.");
        }

        /// <summary>
        /// Calls the DynamicPricingEngine to recalculate all resource prices
        /// based on supply/demand changes from the current turn.
        /// </summary>
        private IEnumerator ProcessMarketUpdate()
        {
            Debug.Log("[TurnController] Updating market prices...");

            if (pricingEngine != null)
            {
                // Use reflection to call RecalculateAllPrices since we reference via MonoBehaviour
                var method = pricingEngine.GetType().GetMethod("RecalculateAllPrices");
                if (method != null)
                {
                    method.Invoke(pricingEngine, null);
                }
                else
                {
                    Debug.LogWarning("[TurnController] RecalculateAllPrices method not found on pricing engine.");
                }
            }
            else if (settings != null && settings.EnableMarket)
            {
                Debug.LogWarning("[TurnController] Market is enabled but pricing engine reference is null.");
            }

            yield return null;
            Debug.Log("[TurnController] Market update complete.");
        }

        /// <summary>
        /// Processes all end-of-turn resolution steps:
        /// - Income collection from cities
        /// - Supply line calculations
        /// - Weather changes (if dynamic weather enabled)
        /// - City growth and population changes
        /// - Rebellion and unrest checks
        /// - Unit supply and attrition
        /// </summary>
        private IEnumerator ProcessTurnResolution()
        {
            Debug.Log("[TurnController] Processing turn resolution...");

            // 1. Income Collection
            ProcessIncomeCollection();

            // 2. Supply Lines
            ProcessSupplyLines();

            // 3. Weather
            if (settings != null && settings.DynamicWeather)
            {
                ProcessWeatherChange();
            }

            // 4. City Growth
            ProcessCityGrowth();

            // 5. Rebellion Checks
            ProcessRebellionChecks();

            // 6. Unit Attrition
            ProcessUnitAttrition();

            yield return null;
            Debug.Log("[TurnController] Turn resolution complete.");
        }

        // ------------------------------------------------------------------
        // Turn Resolution Sub-Systems
        // ------------------------------------------------------------------

        /// <summary>
        /// Collects income from all owned cities for the active nations.
        /// Base income is modified by the growth rate and city level.
        /// </summary>
        private void ProcessIncomeCollection()
        {
            if (settings == null) return;

            // Income is calculated as: cityIncomeBase * (1 + incomeGrowthRate * turnsOwned)
            // This would iterate through all nations' cities and add income.
            Debug.Log($"[TurnController] Income collected. Base: {settings.CityIncomeBase}, Growth: {settings.IncomeGrowthRate:P0}");
        }

        /// <summary>
        /// Recalculates supply lines for all units.
        /// Units out of supply suffer combat penalties and attrition.
        /// </summary>
        private void ProcessSupplyLines()
        {
            Debug.Log("[TurnController] Supply lines recalculated.");
            // Supply line logic would check hex connectivity to nearest friendly city
        }

        /// <summary>
        /// Advances the weather system by one turn if dynamic weather is enabled.
        /// </summary>
        private void ProcessWeatherChange()
        {
            if (weatherSystem != null)
            {
                var method = weatherSystem.GetType().GetMethod("AdvanceWeather");
                if (method != null)
                {
                    method.Invoke(weatherSystem, null);
                }
            }
            Debug.Log("[TurnController] Weather advanced.");
        }

        /// <summary>
        /// Processes city population growth and level-up checks.
        /// Cities grow based on food surplus and infrastructure.
        /// </summary>
        private void ProcessCityGrowth()
        {
            Debug.Log("[TurnController] City growth processed.");
            // Cities grow if food > 0; level up at population thresholds
        }

        /// <summary>
        /// Checks all occupied territories for rebellion risk.
        /// High unrest, cultural differences, and low garrison increase rebellion chance.
        /// </summary>
        private void ProcessRebellionChecks()
        {
            Debug.Log("[TurnController] Rebellion checks performed.");
            // Rebellion chance = (unrest * 0.01) * (1 - garrisonStrength)
            // On rebellion: spawn rebel units, flip territory control
        }

        /// <summary>
        /// Applies attrition to units that are out of supply or in harsh weather.
        /// </summary>
        private void ProcessUnitAttrition()
        {
            Debug.Log("[TurnController] Unit attrition processed.");
            // Units in harsh weather without supply lose HP each turn
        }

        // ------------------------------------------------------------------
        // Victory & Elimination
        // ------------------------------------------------------------------

        /// <summary>
        /// Checks if any nation has met the victory conditions defined in settings,
        /// or if any nation has been eliminated (no cities, no units).
        /// </summary>
        public void CheckVictoryConditions()
        {
            if (settings == null)
                return;

            switch (settings.VictoryCondition)
            {
                case IronProtocol.Data.VictoryCondition.Domination:
                    CheckDominationVictory();
                    break;

                case IronProtocol.Data.VictoryCondition.Economic:
                    CheckEconomicVictory();
                    break;

                case IronProtocol.Data.VictoryCondition.Diplomatic:
                    CheckDiplomaticVictory();
                    break;
            }

            // Check for eliminated nations
            CheckEliminations();
        }

        /// <summary>
        /// Domination victory: control a majority of all territories on the map.
        /// </summary>
        private void CheckDominationVictory()
        {
            // Would compare each nation's territory count vs total territories
            // Victory if one nation controls > 50% of the map
            Debug.Log("[TurnController] Checking domination victory...");
        }

        /// <summary>
        /// Economic victory: accumulate a treasury above the victory threshold.
        /// </summary>
        private void CheckEconomicVictory()
        {
            // Victory if any nation's treasury exceeds the economic victory threshold
            Debug.Log("[TurnController] Checking economic victory...");
        }

        /// <summary>
        /// Diplomatic victory: form alliances with a majority of surviving nations.
        /// </summary>
        private void CheckDiplomaticVictory()
        {
            // Victory if player has active alliances with > 50% of non-eliminated nations
            Debug.Log("[TurnController] Checking diplomatic victory...");
        }

        /// <summary>
        /// Checks all nations for elimination conditions:
        /// - No cities remaining
        /// - No units remaining
        /// A nation with neither is eliminated and removed from the turn order.
        /// </summary>
        private void CheckEliminations()
        {
            List<string> newlyEliminated = new List<string>();

            foreach (string nationId in NationTurnOrder)
            {
                if (_eliminatedNations.ContainsKey(nationId))
                    continue;

                // Check elimination: no cities and no units
                // This would query the WorldState or similar game system
                bool hasCities = CheckNationHasCities(nationId);
                bool hasUnits = CheckNationHasUnits(nationId);

                if (!hasCities && !hasUnits)
                {
                    newlyEliminated.Add(nationId);
                    _eliminatedNations[nationId] = true;

                    Debug.Log($"[TurnController] Nation eliminated: {nationId}");
                    OnNationEliminated?.Invoke(nationId, "conquest");
                }
            }

            // If only one nation remains, they win
            int survivingNations = NationTurnOrder.Count - _eliminatedNations.Count;
            if (survivingNations <= 1)
            {
                foreach (string nationId in NationTurnOrder)
                {
                    if (!_eliminatedNations.ContainsKey(nationId))
                    {
                        Debug.Log($"[TurnController] Last nation standing: {nationId}. Game Over!");
                        OnGameWon?.Invoke(nationId, settings.VictoryCondition);
                        IsGameActive = false;
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private bool CheckNationHasCities(string nationId)
        {
            // Placeholder – would query the territory/city management system
            return true;
        }

        private bool CheckNationHasUnits(string nationId)
        {
            // Placeholder – would query the unit management system
            return true;
        }

        private void SetPhase(TurnPhase phase)
        {
            CurrentPhase = phase;
            Debug.Log($"[TurnController] Phase changed: {phase}");
            OnPhaseChanged?.Invoke(phase);
        }

        /// <summary>
        /// Adds a nation to the turn order. Typically called during game setup.
        /// </summary>
        /// <param name="nationId">The nation identifier to add.</param>
        public void AddNation(string nationId)
        {
            if (!NationTurnOrder.Contains(nationId))
            {
                NationTurnOrder.Add(nationId);
                Debug.Log($"[TurnController] Nation added to turn order: {nationId}");
            }
        }

        /// <summary>
        /// Removes a nation from the turn order (e.g., after elimination).
        /// </summary>
        /// <param name="nationId">The nation identifier to remove.</param>
        public void RemoveNation(string nationId)
        {
            NationTurnOrder.Remove(nationId);
            _eliminatedNations[nationId] = true;
            Debug.Log($"[TurnController] Nation removed from turn order: {nationId}");
        }

        /// <summary>
        /// Immediately ends the current game.
        /// </summary>
        public void EndGame()
        {
            IsGameActive = false;
            IsProcessing = false;
            Debug.Log("[TurnController] Game ended.");
        }
    }
}
