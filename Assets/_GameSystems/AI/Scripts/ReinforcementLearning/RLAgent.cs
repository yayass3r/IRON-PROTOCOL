// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: RLAgent.cs
// Description: Simple reinforcement learning agent with epsilon-greedy action
//              selection, temporal difference policy updates, and JSON
//              serialization for saving/loading trained policies.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace IronProtocol.AI.ReinforcementLearning
{
    /// <summary>
    /// Represents the current state of the game as observed by the RL agent.
    /// Features are a fixed-size float array representing normalized game metrics.
    /// </summary>
    [System.Serializable]
    public class RLState
    {
        /// <summary>
        /// Feature vector representing the game state.
        /// Each index maps to a specific game metric (e.g., [0]=military ratio, [1]=economic strength, etc.)
        /// All values should be normalized to [0, 1] for stable learning.
        /// </summary>
        public float[] features;

        /// <summary>Gets the number of features in this state representation.</summary>
        public int FeatureCount => features?.Length ?? 0;

        /// <summary>
        /// Creates a new RLState with the specified feature vector.
        /// </summary>
        /// <param name="features">Array of normalized feature values.</param>
        public RLState(float[] features)
        {
            this.features = features ?? new float[0];
        }

        /// <summary>Creates an empty RLState. Used for serialization.</summary>
        public RLState() { }

        /// <summary>
        /// Gets a feature value by index. Returns 0 if index is out of range.
        /// </summary>
        public float GetFeature(int index)
        {
            if (features == null || index < 0 || index >= features.Length) return 0f;
            return features[index];
        }

        /// <summary>
        /// Computes a hash key from the discretized feature vector for state lookup.
        /// Features are rounded to 1 decimal place to reduce state space.
        /// </summary>
        public string GetDiscreteKey()
        {
            if (features == null || features.Length == 0) return "empty";

            List<string> parts = new List<string>(features.Length);
            foreach (float f in features)
            {
                parts.Add(Mathf.Round(f * 10f).ToString());
            }
            return string.Join("_", parts);
        }

        /// <summary>Creates a copy of this state with its own feature array.</summary>
        public RLState Clone()
        {
            float[] copy = new float[features?.Length ?? 0];
            if (features != null) Array.Copy(features, copy, features.Length);
            return new RLState(copy);
        }
    }

    /// <summary>
    /// Represents a single action that the RL agent can take, with an associated reward.
    /// </summary>
    [System.Serializable]
    public class RLAction
    {
        /// <summary>
        /// Index of the action in the agent's action space.
        /// Maps to strategic actions: 0=economic, 1=military, 2=defense, 3=diplomacy, 4=market.
        /// </summary>
        public int actionIndex;

        /// <summary>
        /// Reward received after taking this action.
        /// Updated during policy training via temporal difference learning.
        /// </summary>
        public float reward;

        /// <summary>Human-readable label for this action index.</summary>
        public string label;

        /// <summary>
        /// Creates a new RLAction.
        /// </summary>
        /// <param name="actionIndex">Action space index.</param>
        /// <param name="reward">Initial reward value.</param>
        /// <param name="label">Human-readable label.</param>
        public RLAction(int actionIndex, float reward = 0f, string label = null)
        {
            this.actionIndex = actionIndex;
            this.reward = reward;
            this.label = label ?? $"Action_{actionIndex}";
        }
    }

    /// <summary>
    /// Serializable container for saving/loading policy data.
    /// </summary>
    [System.Serializable]
    public class RLPolicyData
    {
        /// <summary>Version of the policy format for forward compatibility.</summary>
        public int version = 1;

        /// <summary>The number of actions in the action space.</summary>
        public int actionCount;

        /// <summary>The number of state features expected.</summary>
        public int featureCount;

        /// <summary>
        /// Serialized Q-table: state key -> array of Q-values (one per action).
        /// </summary>
        public Dictionary<string, float[]> qTable;

        /// <summary>Number of training episodes completed.</summary>
        public int trainingEpisodes;

        /// <summary>Total cumulative reward across all episodes.</summary>
        public float totalCumulativeReward;

        /// <summary>Timestamp when this policy was last updated.</summary>
        public string lastUpdated;
    }

    /// <summary>
    /// MonoBehaviour implementing a simple reinforcement learning agent using
    /// tabular Q-learning with epsilon-greedy exploration and temporal difference updates.
    /// Designed to integrate with the StrategicAI's weight system for adaptive decision-making.
    /// </summary>
    public class RLAgent : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Constants
        // --------------------------------------------------------------------- //

        /// <summary>Number of actions in the action space.</summary>
        public const int ActionCount = 5;

        /// <summary>Action index labels for debugging.</summary>
        public static readonly string[] ActionLabels = new string[]
        {
            "economic_expansion",
            "military_aggression",
            "defense",
            "diplomacy",
            "market_trading"
        };

        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>Fired when the agent selects an action. Parameters: stateKey, actionIndex.</summary>
        public event Action<string, int> OnActionSelected;

        /// <summary>Fired when the policy is updated from a new experience.</summary>
        public event Action OnPolicyUpdated;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Learning Parameters")]
        [Tooltip("Learning rate (alpha). Higher = faster learning, less stable. Range: 0.01-0.5.")]
        [SerializeField, Range(0.01f, 0.5f)] private float learningRate = 0.1f;

        [Tooltip("Discount factor (gamma). Higher = values future rewards more. Range: 0.0-0.99.")]
        [SerializeField, Range(0f, 0.99f)] private float discountFactor = 0.95f;

        [Header("Exploration")]
        [Tooltip("Epsilon for epsilon-greedy exploration. Chance of random action. Range: 0.0-0.5.")]
        [SerializeField, Range(0f, 0.5f)] private float epsilon = 0.1f;

        [Tooltip("Minimum epsilon value (floor for epsilon decay).")]
        [SerializeField, Range(0f, 0.1f)] private float epsilonMin = 0.01f;

        [Tooltip("Epsilon decay rate per episode. Multiply epsilon by this after each episode.")]
        [SerializeField, Range(0.9f, 0.999f)] private float epsilonDecay = 0.995f;

        [Header("State Space")]
        [Tooltip("Expected number of state features. Actions with wrong size are rejected.")]
        [SerializeField] private int expectedFeatureCount = 6;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Q-table mapping discretized state keys to action-value arrays.
        /// Key = discretized state string, Value = float array of Q-values (one per action).
        /// </summary>
        private readonly Dictionary<string, float[]> _qTable = new Dictionary<string, float[]>();

        /// <summary>Cumulative reward across the current training episode.</summary>
        private float _episodeReward;

        /// <summary>Total number of training episodes completed.</summary>
        private int _totalEpisodes;

        /// <summary>Total cumulative reward across all episodes.</summary>
        private float _totalCumulativeReward;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Current learning rate (alpha).</summary>
        public float LearningRate => learningRate;

        /// <summary>Current discount factor (gamma).</summary>
        public float DiscountFactor => discountFactor;

        /// <summary>Current exploration rate (epsilon).</summary>
        public float Epsilon => epsilon;

        /// <summary>Number of unique states in the Q-table.</summary>
        public int StateCount => _qTable.Count;

        /// <summary>Total training episodes completed.</summary>
        public int TotalEpisodes => _totalEpisodes;

        /// <summary>Cumulative reward in the current episode.</summary>
        public float EpisodeReward => _episodeReward;

        // --------------------------------------------------------------------- //
        // Public Methods - Action Selection
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Selects an action using epsilon-greedy policy.
        /// With probability epsilon, selects a random action (exploration).
        /// Otherwise, selects the action with the highest Q-value (exploitation).
        /// </summary>
        /// <param name="state">The current game state as observed by the agent.</param>
        /// <returns>The selected action index (0 to ActionCount-1).</returns>
        public int SelectAction(RLState state)
        {
            if (state == null || state.FeatureCount == 0)
            {
                Debug.LogWarning("[RLAgent] SelectAction: null or empty state. Returning random action.");
                return UnityEngine.Random.Range(0, ActionCount);
            }

            string stateKey = state.GetDiscreteKey();
            float[] qValues = GetOrCreateQValues(stateKey);

            // Epsilon-greedy: explore with probability epsilon
            if (UnityEngine.Random.value < epsilon)
            {
                int randomAction = UnityEngine.Random.Range(0, ActionCount);
                OnActionSelected?.Invoke(stateKey, randomAction);
                return randomAction;
            }

            // Exploit: select the action with the highest Q-value
            int bestAction = GetBestAction(qValues);
            OnActionSelected?.Invoke(stateKey, bestAction);
            return bestAction;
        }

        /// <summary>
        /// Gets the best action index from a Q-value array.
        /// Ties are broken randomly.
        /// </summary>
        /// <param name="qValues">Q-value array for the current state.</param>
        /// <returns>Index of the best action.</returns>
        public int GetBestAction(float[] qValues)
        {
            if (qValues == null || qValues.Length == 0)
                return 0;

            float maxQ = float.NegativeInfinity;
            List<int> bestActions = new List<int>();

            for (int i = 0; i < qValues.Length && i < ActionCount; i++)
            {
                if (qValues[i] > maxQ)
                {
                    maxQ = qValues[i];
                    bestActions.Clear();
                    bestActions.Add(i);
                }
                else if (Mathf.Approximately(qValues[i], maxQ))
                {
                    bestActions.Add(i);
                }
            }

            // Break ties randomly
            return bestActions[UnityEngine.Random.Range(0, bestActions.Count)];
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Policy Updates
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Updates the policy using temporal difference (TD) learning.
        /// Implements the Q-learning update rule:
        /// Q(s,a) += alpha * [r + gamma * max(Q(s',a')) - Q(s,a)]
        /// </summary>
        /// <param name="state">The state before the action was taken.</param>
        /// <param name="action">The action that was taken.</param>
        /// <param name="reward">The reward received.</param>
        /// <param name="nextState">The state after the action was taken.</param>
        public void UpdatePolicy(RLState state, int action, float reward, RLState nextState)
        {
            if (state == null)
            {
                Debug.LogWarning("[RLAgent] UpdatePolicy: state is null.");
                return;
            }

            string stateKey = state.GetDiscreteKey();
            float[] qValues = GetOrCreateQValues(stateKey);

            // Validate action index
            if (action < 0 || action >= ActionCount)
            {
                Debug.LogWarning($"[RLAgent] UpdatePolicy: invalid action index {action}.");
                return;
            }

            // Calculate TD target: r + gamma * max(Q(s',a'))
            float maxNextQ = 0f;
            if (nextState != null && nextState.FeatureCount > 0)
            {
                string nextStateKey = nextState.GetDiscreteKey();
                float[] nextQValues = GetOrCreateQValues(nextStateKey);
                maxNextQ = Mathf.Max(nextQValues);
            }

            float tdTarget = reward + (discountFactor * maxNextQ);
            float tdError = tdTarget - qValues[action];

            // Update Q-value
            qValues[action] += learningRate * tdError;
            _qTable[stateKey] = qValues;

            // Track episode reward
            _episodeReward += reward;

            OnPolicyUpdated?.Invoke();
        }

        /// <summary>
        /// Ends the current training episode, applying epsilon decay and resetting tracking.
        /// Call this at the end of each game or evaluation cycle.
        /// </summary>
        public void EndEpisode()
        {
            _totalEpisodes++;
            _totalCumulativeReward += _episodeReward;

            // Decay epsilon
            epsilon = Mathf.Max(epsilonMin, epsilon * epsilonDecay);

            Debug.Log($"[RLAgent] Episode {_totalEpisodes} ended. " +
                      $"Reward: {_episodeReward:F2}, Epsilon: {epsilon:F4}, " +
                      $"States learned: {StateCount}");

            _episodeReward = 0f;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Weight Export (for StrategicAI integration)
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Exports learned Q-values as policy weights for StrategicAI integration.
        /// The returned weights represent how well each strategic dimension has performed.
        /// </summary>
        /// <returns>Dictionary mapping RL key to learned weight value.</returns>
        public Dictionary<string, float> ExportPolicyWeights()
        {
            var weights = new Dictionary<string, float>
            {
                { StrategicAI.RL_KEY_ECO, 1f },
                { StrategicAI.RL_KEY_AGGRO, 1f },
                { StrategicAI.RL_KEY_DEFENSE, 1f },
                { StrategicAI.RL_KEY_DIPLOMACY, 1f },
                { StrategicAI.RL_KEY_MARKET, 1f }
            };

            if (_qTable.Count == 0)
                return weights;

            // Average the Q-values across all states for each action
            float[] avgQ = new float[ActionCount];
            int stateCount = 0;

            foreach (var qValues in _qTable.Values)
            {
                for (int i = 0; i < ActionCount && i < qValues.Length; i++)
                {
                    avgQ[i] += qValues[i];
                }
                stateCount++;
            }

            if (stateCount > 0)
            {
                for (int i = 0; i < ActionCount; i++)
                {
                    avgQ[i] /= stateCount;
                }

                // Normalize to weight range [0.5, 2.0]
                float minQ = avgQ.Min();
                float maxQ = avgQ.Max();
                float range = maxQ - minQ;

                if (range > 0.001f)
                {
                    for (int i = 0; i < ActionCount; i++)
                    {
                        float normalized = ((avgQ[i] - minQ) / range); // 0 to 1
                        float weight = 0.5f + (normalized * 1.5f); // 0.5 to 2.0
                        weights[MapIndexToRLKey(i)] = weight;
                    }
                }
            }

            return weights;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Persistence
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Saves the current Q-table policy to a JSON file.
        /// The file is written to Application.persistentDataPath.
        /// </summary>
        /// <param name="fileName">Name of the save file (without path). Defaults to "rl_policy.json".</param>
        /// <returns>True if the policy was saved successfully.</returns>
        public bool SavePolicy(string fileName = "rl_policy.json")
        {
            try
            {
                var policyData = new RLPolicyData
                {
                    version = 1,
                    actionCount = ActionCount,
                    featureCount = expectedFeatureCount,
                    qTable = new Dictionary<string, float[]>(_qTable),
                    trainingEpisodes = _totalEpisodes,
                    totalCumulativeReward = _totalCumulativeReward,
                    lastUpdated = DateTime.UtcNow.ToString("o")
                };

                string json = JsonUtility.ToJson(policyData, true);
                string filePath = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(filePath, json);

                Debug.Log($"[RLAgent] Policy saved to: {filePath} ({StateCount} states, {_totalEpisodes} episodes)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RLAgent] Failed to save policy: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a previously saved Q-table policy from a JSON file.
        /// </summary>
        /// <param name="fileName">Name of the save file (without path). Defaults to "rl_policy.json".</param>
        /// <returns>True if the policy was loaded successfully.</returns>
        public bool LoadPolicy(string fileName = "rl_policy.json")
        {
            try
            {
                string filePath = Path.Combine(Application.persistentDataPath, fileName);

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[RLAgent] Policy file not found: {filePath}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                var policyData = JsonUtility.FromJson<RLPolicyData>(json);

                if (policyData == null)
                {
                    Debug.LogError("[RLAgent] Failed to deserialize policy data.");
                    return false;
                }

                // Version check
                if (policyData.version != 1)
                {
                    Debug.LogWarning($"[RLAgent] Policy version mismatch: expected 1, got {policyData.version}. Attempting to load anyway.");
                }

                // Validate action count
                if (policyData.actionCount != ActionCount)
                {
                    Debug.LogWarning($"[RLAgent] Action count mismatch: expected {ActionCount}, got {policyData.actionCount}. Partial load.");
                }

                // Clear and load Q-table
                _qTable.Clear();

                if (policyData.qTable != null)
                {
                    foreach (var kvp in policyData.qTable)
                    {
                        // Ensure arrays are the correct length
                        float[] qValues = kvp.Value;
                        if (qValues == null || qValues.Length != ActionCount)
                        {
                            float[] resized = new float[ActionCount];
                            if (qValues != null)
                            {
                                int copyLen = Mathf.Min(qValues.Length, ActionCount);
                                Array.Copy(qValues, resized, copyLen);
                            }
                            _qTable[kvp.Key] = resized;
                        }
                        else
                        {
                            _qTable[kvp.Key] = qValues;
                        }
                    }
                }

                _totalEpisodes = policyData.trainingEpisodes;
                _totalCumulativeReward = policyData.totalCumulativeReward;
                expectedFeatureCount = policyData.featureCount;

                Debug.Log($"[RLAgent] Policy loaded from: {filePath} ({StateCount} states, {_totalEpisodes} episodes, " +
                          $"last updated: {policyData.lastUpdated})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RLAgent] Failed to load policy: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clears all learned Q-values and resets the agent to initial state.
        /// </summary>
        public void ResetPolicy()
        {
            _qTable.Clear();
            _totalEpisodes = 0;
            _totalCumulativeReward = 0f;
            _episodeReward = 0f;
            epsilon = 0.1f;

            Debug.Log("[RLAgent] Policy reset. All learned Q-values cleared.");
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Queries
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets the Q-values for a specific state.
        /// Returns a zero-filled array if the state has not been visited.
        /// </summary>
        /// <param name="state">The game state to query.</param>
        /// <returns>Array of Q-values (one per action).</returns>
        public float[] GetQValues(RLState state)
        {
            if (state == null) return new float[ActionCount];
            string stateKey = state.GetDiscreteKey();
            return GetOrCreateQValues(stateKey);
        }

        /// <summary>
        /// Gets a copy of the entire Q-table for analysis or debugging.
        /// </summary>
        /// <returns>Dictionary mapping state keys to Q-value arrays.</returns>
        public Dictionary<string, float[]> GetQTableCopy()
        {
            var copy = new Dictionary<string, float[]>();
            foreach (var kvp in _qTable)
            {
                float[] qCopy = new float[kvp.Value.Length];
                Array.Copy(kvp.Value, qCopy, kvp.Value.Length);
                copy[kvp.Key] = qCopy;
            }
            return copy;
        }

        // --------------------------------------------------------------------- //
        // Private Methods
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets Q-values for a state key, creating a new zero-filled entry if not found.
        /// </summary>
        private float[] GetOrCreateQValues(string stateKey)
        {
            if (_qTable.TryGetValue(stateKey, out float[] qValues))
                return qValues;

            // Create new Q-value array initialized to zeros
            qValues = new float[ActionCount];
            _qTable[stateKey] = qValues;
            return qValues;
        }

        /// <summary>
        /// Maps an action index to the corresponding StrategicAI RL weight key.
        /// </summary>
        private string MapIndexToRLKey(int actionIndex)
        {
            switch (actionIndex)
            {
                case 0: return StrategicAI.RL_KEY_ECO;
                case 1: return StrategicAI.RL_KEY_AGGRO;
                case 2: return StrategicAI.RL_KEY_DEFENSE;
                case 3: return StrategicAI.RL_KEY_DIPLOMACY;
                case 4: return StrategicAI.RL_KEY_MARKET;
                default: return StrategicAI.RL_KEY_ECO;
            }
        }
    }
}
