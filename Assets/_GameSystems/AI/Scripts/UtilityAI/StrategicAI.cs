// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: StrategicAI.cs
// Description: AI strategic decision-making using Utility AI with reinforcement
//              learning weight integration. Scores all possible actions each turn
//              and executes the highest-scoring option.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.AI
{
    /// <summary>
    /// Represents a scored strategic action that the AI can take.
    /// Each action has an identifier, description, utility score, and an execute callback.
    /// </summary>
    [System.Serializable]
    public class AIAction
    {
        /// <summary>Unique identifier for this action type (e.g. "eco_expand", "mil_attack").</summary>
        public string actionId;

        /// <summary>Human-readable description of the action for debugging.</summary>
        public string description;

        /// <summary>Utility score (higher = more likely to be chosen).</summary>
        public float score;

        /// <summary>Callback to execute the action. Invoked when this action is selected.</summary>
        public System.Action execute;

        /// <summary>
        /// Creates a new AIAction.
        /// </summary>
        /// <param name="actionId">Unique action identifier.</param>
        /// <param name="description">Description for logging.</param>
        /// <param name="score">Initial utility score.</param>
        /// <param name="execute">Execution callback.</param>
        public AIAction(string actionId, string description, float score, System.Action execute)
        {
            this.actionId = actionId;
            this.description = description;
            this.score = score;
            this.execute = execute;
        }
    }

    /// <summary>
    /// MonoBehaviour implementing a Utility AI for strategic decision-making.
    /// Evaluates multiple strategic dimensions (economic, military, defense, diplomacy, market)
    /// each turn, applies reinforcement learning weight modifiers, adds noise for unpredictability,
    /// and executes the highest-scoring action.
    /// </summary>
    public class StrategicAI : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // RL Weight Keys (constants for dictionary access)
        // --------------------------------------------------------------------- //

        /// <summary>RL weight key for economic expansion actions.</summary>
        public const string RL_KEY_ECO = "eco";
        /// <summary>RL weight key for military aggression actions.</summary>
        public const string RL_KEY_AGGRO = "aggro";
        /// <summary>RL weight key for defensive actions.</summary>
        public const string RL_KEY_DEFENSE = "defense";
        /// <summary>RL weight key for diplomatic actions.</summary>
        public const string RL_KEY_DIPLOMACY = "diplomacy";
        /// <summary>RL weight key for market trading actions.</summary>
        public const string RL_KEY_MARKET = "market";

        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>Fired when an action is evaluated. Parameters: actionId, score.</summary>
        public event Action<string, float> OnActionEvaluated;

        /// <summary>Fired when an action is executed. Parameter: actionId.</summary>
        public event Action<string> OnActionExecuted;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("RL Integration")]
        [Tooltip("Learning rate for weight updates from game outcomes (0.0 to 1.0).")]
        [SerializeField, Range(0.01f, 0.5f)] private float learningRate = 0.1f;

        [Header("Utility Weights (base, before RL modification)")]
        [Tooltip("Base weight for economic expansion scoring.")]
        [SerializeField, Range(0f, 2f)] private float baseEconomicWeight = 1.0f;

        [Tooltip("Base weight for military aggression scoring.")]
        [SerializeField, Range(0f, 2f)] private float baseAggressionWeight = 0.8f;

        [Tooltip("Base weight for defense scoring.")]
        [SerializeField, Range(0f, 2f)] private float baseDefenseWeight = 1.0f;

        [Tooltip("Base weight for diplomacy scoring.")]
        [SerializeField, Range(0f, 2f)] private float baseDiplomacyWeight = 0.6f;

        [Tooltip("Base weight for market trading scoring.")]
        [SerializeField, Range(0f, 2f)] private float baseMarketWeight = 0.5f;

        [Header("Noise")]
        [Tooltip("Maximum random noise added to scores (0.0 to 0.1) to prevent predictability.")]
        [SerializeField, Range(0f, 0.2f)] private float maxNoise = 0.1f;

        [Header("Market Trading")]
        [Tooltip("Price percentile threshold below which the AI considers buying (default: 30th).")]
        [SerializeField, Range(0f, 0.5f)] private float buyPercentileThreshold = 0.3f;

        [Tooltip("Price percentile threshold above which the AI considers selling (default: 70th).")]
        [SerializeField, Range(0.5f, 1f)] private float sellPercentileThreshold = 0.7f;

        [Header("Identity")]
        [Tooltip("The nation ID this AI controls.")]
        [SerializeField] private string nationId;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Reinforcement learning weight modifiers for each strategic dimension.
        /// These are updated over time based on game outcomes via <see cref="UpdateWeightsFromOutcome"/>.
        /// All weights start at 1.0 (neutral modifier).
        /// </summary>
        private readonly Dictionary<string, float> rlWeights = new Dictionary<string, float>
        {
            { RL_KEY_ECO, 1.0f },
            { RL_KEY_AGGRO, 1.0f },
            { RL_KEY_DEFENSE, 1.0f },
            { RL_KEY_DIPLOMACY, 1.0f },
            { RL_KEY_MARKET, 1.0f }
        };

        /// <summary>Cached list of candidate actions for the current turn.</summary>
        private readonly List<AIAction> _candidateActions = new List<AIAction>();

        /// <summary>The action selected last turn (for outcome tracking).</summary>
        private AIAction _lastExecutedAction = null;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Gets the nation ID this AI controls.</summary>
        public string NationId => nationId;

        /// <summary>Gets the RL weight modifiers as a read-only dictionary.</summary>
        public IReadOnlyDictionary<string, float> RLWeights => rlWeights;

        /// <summary>Gets the last executed action (may be null on first turn).</summary>
        public AIAction LastExecutedAction => _lastExecutedAction;

        // --------------------------------------------------------------------- //
        // Public Methods - Core AI Loop
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Evaluates all possible strategic actions given the current game state.
        /// Each action is scored based on:
        /// <list type="bullet">
        ///   <item><b>Economic expansion</b>: weighted by economic strength × RL weight</item>
        ///   <item><b>Military aggression</b>: weighted by military ratio × RL weight</item>
        ///   <item><b>Defense</b>: weighted by threat level × RL weight</item>
        ///   <item><b>Diplomacy</b>: weighted by RL weight</item>
        ///   <item><b>Market trading</b>: buy when price &lt; 30th percentile, sell when &gt; 70th</item>
        /// </list>
        /// Random noise (0 to maxNoise) is added to each score to prevent predictability.
        /// </summary>
        /// <param name="state">Current game state snapshot.</param>
        /// <returns>The highest-scoring AIAction.</returns>
        public AIAction EvaluateActions(GameState state)
        {
            if (state == null)
            {
                Debug.LogWarning("[StrategicAI] Cannot evaluate: state is null.");
                return null;
            }

            _candidateActions.Clear();

            NationState self = state.GetSelfState();
            if (self == null)
            {
                Debug.LogWarning("[StrategicAI] Cannot evaluate: no self state found.");
                return null;
            }

            float militaryRatio = state.GetMilitaryRatio();
            float avgEconomy = state.GetAverageEconomicStrength();

            // --- Action 1: Economic Expansion ---
            float ecoScore = CalculateEconomicScore(self, avgEconomy);
            _candidateActions.Add(new AIAction(
                "eco_expand",
                $"Economic expansion (strength: {self.economicStrength:F0}, avg: {avgEconomy:F0})",
                ecoScore,
                () => ExecuteEconomicExpansion(state)
            ));

            // --- Action 2: Military Aggression ---
            float aggroScore = CalculateAggressionScore(self, militaryRatio);
            _candidateActions.Add(new AIAction(
                "mil_attack",
                $"Military aggression (ratio: {militaryRatio:F2})",
                aggroScore,
                () => ExecuteMilitaryAggression(state)
            ));

            // --- Action 3: Defense ---
            float defenseScore = CalculateDefenseScore(self);
            _candidateActions.Add(new AIAction(
                "defend",
                $"Defense (threat: {self.threatLevel:F2})",
                defenseScore,
                () => ExecuteDefense(state)
            ));

            // --- Action 4: Diplomacy ---
            float diplomacyScore = CalculateDiplomacyScore(self, state);
            _candidateActions.Add(new AIAction(
                "diplomacy",
                $"Diplomatic action (influence: {self.diplomaticInfluence:F0})",
                diplomacyScore,
                () => ExecuteDiplomacy(state)
            ));

            // --- Action 5: Market Trading ---
            float marketScore = CalculateMarketScore(state);
            _candidateActions.Add(new AIAction(
                "market_trade",
                "Market trading (buy low, sell high)",
                marketScore,
                () => ExecuteMarketTrade(state)
            ));

            // --- Add random noise to all scores ---
            foreach (var action in _candidateActions)
            {
                float noise = UnityEngine.Random.Range(0f, maxNoise);
                action.score += noise;
            }

            // --- Sort by score descending ---
            _candidateActions.Sort((a, b) => b.score.CompareTo(a.score));

            // --- Fire evaluation events ---
            foreach (var action in _candidateActions)
            {
                OnActionEvaluated?.Invoke(action.actionId, action.score);
            }

            // Log the top action
            if (_candidateActions.Count > 0)
            {
                Debug.Log($"[StrategicAI] Top action: {_candidateActions[0].actionId} (score: {_candidateActions[0].score:F3})");
            }

            return _candidateActions.Count > 0 ? _candidateActions[0] : null;
        }

        /// <summary>
        /// Executes the highest-scoring action from the evaluated candidates.
        /// Stores the executed action for later outcome tracking.
        /// </summary>
        /// <param name="state">Current game state.</param>
        /// <returns>The executed AIAction, or null if no actions were available.</returns>
        public AIAction ExecuteBestAction(GameState state)
        {
            AIAction bestAction = EvaluateActions(state);

            if (bestAction == null)
            {
                Debug.LogWarning("[StrategicAI] No valid action to execute.");
                return null;
            }

            _lastExecutedAction = bestAction;

            Debug.Log($"[StrategicAI] Executing: {bestAction.actionId} - {bestAction.description}");
            bestAction.execute?.Invoke();
            OnActionExecuted?.Invoke(bestAction.actionId);

            return bestAction;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - RL Integration
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Updates reinforcement learning weights based on the outcome of the last executed action.
        /// Uses a simple reward-weighted update with the configured learning rate.
        /// </summary>
        /// <param name="result">The game result from the last action.</param>
        public void UpdateWeightsFromOutcome(GameResult result)
        {
            if (result == null)
            {
                Debug.LogWarning("[StrategicAI] Cannot update weights: result is null.");
                return;
            }

            if (_lastExecutedAction == null)
            {
                Debug.LogWarning("[StrategicAI] Cannot update weights: no action was executed.");
                return;
            }

            float compositeReward = result.GetCompositeReward();
            string weightKey = MapActionToWeightKey(_lastExecutedAction.actionId);

            if (string.IsNullOrEmpty(weightKey) || !rlWeights.ContainsKey(weightKey))
                return;

            // Simple reward-based weight update: weight += learningRate * reward
            float oldWeight = rlWeights[weightKey];
            float delta = learningRate * Mathf.Clamp(compositeReward, -2f, 2f);
            float newWeight = Mathf.Clamp(oldWeight + delta, 0.1f, 3.0f);
            rlWeights[weightKey] = Mathf.Round(newWeight * 1000f) / 1000f;

            // Slightly decay other weights to prevent unbounded growth
            foreach (var key in rlWeights.Keys.ToList())
            {
                if (key != weightKey)
                {
                    rlWeights[key] = Mathf.Max(0.1f, rlWeights[key] - (learningRate * 0.05f));
                }
            }

            Debug.Log($"[StrategicAI] RL Update: action={_lastExecutedAction.actionId}, " +
                      $"reward={compositeReward:F3}, weight[{weightKey}]: {oldWeight:F3} -> {rlWeights[weightKey]:F3}");
        }

        /// <summary>
        /// Gets the RL weight for a specific strategic dimension.
        /// </summary>
        /// <param name="key">Weight key (use RL_KEY_* constants).</param>
        /// <returns>The current weight value, or 1.0 if key not found.</returns>
        public float GetRLWeight(string key)
        {
            if (rlWeights.ContainsKey(key)) return rlWeights[key];
            return 1.0f;
        }

        /// <summary>
        /// Manually sets an RL weight. Useful for loading saved weights or testing.
        /// </summary>
        /// <param name="key">Weight key.</param>
        /// <param name="value">New weight value.</param>
        public void SetRLWeight(string key, float value)
        {
            if (rlWeights.ContainsKey(key))
            {
                rlWeights[key] = Mathf.Clamp(value, 0.1f, 3.0f);
            }
        }

        // --------------------------------------------------------------------- //
        // Private Methods - Score Calculations
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Calculates score for economic expansion.
        /// Higher when the AI's economy is below average (catch-up incentive).
        /// </summary>
        private float CalculateEconomicScore(NationState self, float avgEconomy)
        {
            // Incentivize expansion when behind economically
            float economicStrength = self.economicStrength > 0f ? self.economicStrength : 1f;
            float ratio = avgEconomy > 0f ? economicStrength / avgEconomy : 1f;

            // Score is higher when economy is weaker (need to catch up)
            // but still has a base level
            float score = baseEconomicWeight * (2f - Mathf.Clamp(ratio, 0.1f, 2f));

            // Territory scarcity bonus: fewer territories = higher expansion priority
            float territoryBonus = self.territoryCount < 3 ? 0.3f : 0f;

            return (score + territoryBonus) * rlWeights[RL_KEY_ECO];
        }

        /// <summary>
        /// Calculates score for military aggression.
        /// Higher when the AI has a military advantage and enemies are weak.
        /// </summary>
        private float CalculateAggressionScore(NationState self, float militaryRatio)
        {
            float ratio = Mathf.Clamp(militaryRatio, 0.01f, 3f);

            // Aggression is attractive when we have military superiority
            // but not so much that it's wasteful
            float advantageFactor = ratio > 1f ? Mathf.Min(ratio - 1f, 1f) : 0f;

            // Reduce aggression when own territory is small (need to expand economy first)
            float territoryPenalty = self.territoryCount < 2 ? -0.5f : 0f;

            float score = baseAggressionWeight * advantageFactor + territoryPenalty;
            score = Mathf.Max(0f, score);

            return score * rlWeights[RL_KEY_AGGRO];
        }

        /// <summary>
        /// Calculates score for defensive actions.
        /// Higher when threat level is elevated or military is weak.
        /// </summary>
        private float CalculateDefenseScore(NationState self)
        {
            float threat = self.threatLevel;

            // Defense is critical when under threat
            float threatFactor = Mathf.Clamp(threat, 0f, 1f);

            // Extra urgency when territory is small and threatened
            float urgencyBonus = (self.territoryCount <= 2 && threat > 0.3f) ? 0.5f : 0f;

            return baseDefenseWeight * (threatFactor + urgencyBonus) * rlWeights[RL_KEY_DEFENSE];
        }

        /// <summary>
        /// Calculates score for diplomatic actions.
        /// Based on diplomatic influence and number of potential allies.
        /// </summary>
        private float CalculateDiplomacyScore(NationState self, GameState state)
        {
            float influence = self.diplomaticInfluence;
            float influenceFactor = Mathf.Clamp(influence / 100f, 0f, 1f);

            // More attractive when there are neutral nations to befriend
            int neutralCount = state.nations.FindAll(n => !n.isSelf).Count;
            float opportunityFactor = Mathf.Min(neutralCount * 0.1f, 0.5f);

            return baseDiplomacyWeight * (influenceFactor + opportunityFactor) * rlWeights[RL_KEY_DIPLOMACY];
        }

        /// <summary>
        /// Calculates score for market trading actions.
        /// Checks current prices against historical percentiles.
        /// </summary>
        private float CalculateMarketScore(GameState state)
        {
            if (state.resourcePrices == null || state.resourcePrices.Count == 0)
                return 0f;

            float score = 0f;
            int opportunityCount = 0;

            foreach (var kvp in state.resourcePrices)
            {
                float price = kvp.Value;

                // In a full implementation, we'd query the DynamicPricingEngine for percentiles
                // Here we use a simplified heuristic based on price deviation from 100 (baseline)
                float deviation = Mathf.Abs(price - 100f) / 100f;

                if (deviation > 0.2f) // Price is 20%+ away from baseline
                {
                    opportunityCount++;
                    score += deviation * 0.3f;
                }
            }

            score += opportunityCount * 0.1f;
            return baseMarketWeight * score * rlWeights[RL_KEY_MARKET];
        }

        // --------------------------------------------------------------------- //
        // Private Methods - Action Execution (Stubs for game integration)
        // --------------------------------------------------------------------- //

        /// <summary>Executes economic expansion: build, improve cities, invest in production.</summary>
        private void ExecuteEconomicExpansion(GameState state)
        {
            Debug.Log($"[StrategicAI] Action: Economic expansion for '{nationId}'.");
            // Game-specific implementation: queue city improvements, build economy buildings, etc.
        }

        /// <summary>Executes military aggression: attack weak enemies, expand territory.</summary>
        private void ExecuteMilitaryAggression(GameState state)
        {
            Debug.Log($"[StrategicAI] Action: Military aggression by '{nationId}'.");
            // Game-specific implementation: select targets, move armies, declare war if needed
        }

        /// <summary>Executes defensive actions: fortify positions, build defensive structures.</summary>
        private void ExecuteDefense(GameState state)
        {
            Debug.Log($"[StrategicAI] Action: Defense for '{nationId}'.");
            // Game-specific implementation: fortify borders, build walls, reposition units
        }

        /// <summary>Executes diplomatic actions: form alliances, trade agreements, or negotiations.</summary>
        private void ExecuteDiplomacy(GameState state)
        {
            Debug.Log($"[StrategicAI] Action: Diplomacy for '{nationId}'.");
            // Game-specific implementation: send proposals, improve relations
        }

        /// <summary>Executes market trading: buy undervalued or sell overvalued resources.</summary>
        private void ExecuteMarketTrade(GameState state)
        {
            Debug.Log($"[StrategicAI] Action: Market trading for '{nationId}'.");
            // Game-specific implementation: execute buy/sell orders via DynamicPricingEngine
        }

        // --------------------------------------------------------------------- //
        // Private Methods - Helpers
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Maps an action ID to its corresponding RL weight key.
        /// </summary>
        private string MapActionToWeightKey(string actionId)
        {
            switch (actionId)
            {
                case "eco_expand": return RL_KEY_ECO;
                case "mil_attack": return RL_KEY_AGGRO;
                case "defend": return RL_KEY_DEFENSE;
                case "diplomacy": return RL_KEY_DIPLOMACY;
                case "market_trade": return RL_KEY_MARKET;
                default: return null;
            }
        }
    }
}
