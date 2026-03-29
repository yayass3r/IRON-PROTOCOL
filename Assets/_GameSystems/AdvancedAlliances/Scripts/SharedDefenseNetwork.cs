// =============================================================================
// IRON PROTOCOL - Shared Defense Network
// File: SharedDefenseNetwork.cs
// Description: Integrated missile defense network for alliances, providing
//              multi-layered interception capability against incoming threats.
//              Works in conjunction with AdvancedAllianceManager and
//              SpaceWeaponsSystem for comprehensive defense coverage.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.AdvancedAlliances
{
    // =========================================================================
    // ENUMERATIONS
    // =========================================================================

    /// <summary>
    /// Operational state of a defense node within the alliance network.
    /// Nodes cycle between states based on damage, repair status, and power supply.
    /// </summary>
    public enum DefenseNodeState
    {
        /// <summary>Node is fully operational and can intercept incoming threats.</summary>
        Active,

        /// <summary>Node has taken damage and operates at reduced effectiveness until repaired.</summary>
        Damaged,

        /// <summary>Node is completely non-functional and cannot intercept.</summary>
        Offline
    }

    // =========================================================================
    // DATA CLASSES
    // =========================================================================

    /// <summary>
    /// Represents a single defense node within the alliance missile defense network.
    /// Each node provides intercept capability within its operational range.
    /// <para>
    /// Defense nodes are placed at strategic locations and work together to
    /// provide overlapping coverage zones for maximum interception probability.
    /// </para>
    /// </summary>
    [Serializable]
    public class DefenseNode
    {
        /// <summary>Unique identifier for this defense node.</summary>
        public string nodeId { get; set; }

        /// <summary>The nation that owns and maintains this defense node.</summary>
        public string ownerNation { get; set; }

        /// <summary>The alliance this defense node belongs to.</summary>
        public string allianceId { get; set; }

        /// <summary>Hex grid position of the defense node.</summary>
        public HexCoord position { get; set; }

        /// <summary>
        /// Maximum interception range in hex units.
        /// Incoming threats within this range can potentially be intercepted.
        /// </summary>
        public float interceptRange { get; set; }

        /// <summary>
        /// Base probability (0-1) of successfully intercepting a threat
        /// when the target is within range. Modified by technology, damage, and upgrades.
        /// </summary>
        public float interceptChance { get; set; }

        /// <summary>
        /// Current operational state of the defense node.
        /// Affects whether interception attempts are made and their effectiveness.
        /// </summary>
        public DefenseNodeState state { get; set; }

        /// <summary>
        /// Current durability of the defense node (0-100).
        /// When durability drops below thresholds, the node's state changes.
        /// </summary>
        public int durability { get; set; }

        /// <summary>
        /// Maximum durability of this defense node.
        /// </summary>
        public int maxDurability { get; set; }

        /// <summary>
        /// Per-turn maintenance cost in economic units.
        /// </summary>
        public float maintenanceCost { get; set; }

        /// <summary>
        /// Per-turn repair rate when the node is in Damaged state.
        /// </summary>
        public int repairRate { get; set; }

        /// <summary>
        /// Technology level of the node's interceptor systems.
        /// Higher levels increase intercept chance and range.
        /// </summary>
        public int techLevel { get; set; }

        /// <summary>
        /// Number of interceptions remaining this turn (limited ammunition/charge).
        /// </summary>
        public int interceptCharges { get; set; }

        /// <summary>
        /// Maximum intercept charges per turn.
        /// </summary>
        public int maxInterceptCharges { get; set; }

        /// <summary>
        /// Creates a new defense node with specified parameters.
        /// </summary>
        /// <param name="nodeId">Unique node identifier.</param>
        /// <param name="ownerNation">Owning nation.</param>
        /// <param name="allianceId">Parent alliance.</param>
        /// <param name="position">Hex coordinate position.</param>
        /// <param name="interceptRange">Interception range in hexes.</param>
        /// <param name="interceptChance">Base intercept probability (0-1).</param>
        public DefenseNode(string nodeId, string ownerNation, string allianceId,
            HexCoord position, float interceptRange, float interceptChance)
        {
            this.nodeId = nodeId;
            this.ownerNation = ownerNation;
            this.allianceId = allianceId;
            this.position = position;
            this.interceptRange = Mathf.Max(1f, interceptRange);
            this.interceptChance = Mathf.Clamp01(interceptChance);
            this.state = DefenseNodeState.Active;
            this.durability = 100;
            this.maxDurability = 100;
            this.maintenanceCost = 30f;
            this.repairRate = 5;
            this.techLevel = 1;
            this.interceptCharges = 3;
            this.maxInterceptCharges = 3;
        }
    }

    /// <summary>
    /// Result of an interception attempt against an incoming missile or threat.
    /// Contains success/failure status, the interceptor used, and probability data.
    /// </summary>
    [Serializable]
    public class InterceptionResult
    {
        /// <summary>Whether the incoming threat was successfully intercepted.</summary>
        public bool intercepted { get; set; }

        /// <summary>
        /// The defense node ID that performed the interception.
        /// Empty if no node was available or interception was not attempted.
        /// </summary>
        public string interceptorId { get; set; }

        /// <summary>
        /// The intercept probability that was used for the roll.
        /// Useful for displaying chances to the player.
        /// </summary>
        public float interceptChance { get; set; }

        /// <summary>
        /// Hex coordinate where the interception took place.
        /// </summary>
        public HexCoord interceptLocation { get; set; }

        /// <summary>
        /// Human-readable description of the interception outcome.
        /// </summary>
        public string description { get; set; }

        /// <summary>
        /// Creates a successful interception result.
        /// </summary>
        /// <param name="interceptorId">ID of the intercepting node.</param>
        /// <param name="interceptChance">Probability used.</param>
        /// <param name="location">Interception location.</param>
        /// <returns>Successful <see cref="InterceptionResult"/>.</returns>
        public static InterceptionResult CreateSuccess(string interceptorId, float interceptChance, HexCoord location)
        {
            return new InterceptionResult
            {
                intercepted = true,
                interceptorId = interceptorId,
                interceptChance = interceptChance,
                interceptLocation = location,
                description = $"Incoming threat intercepted by '{interceptorId}' " +
                              $"(chance: {interceptChance:P0}) at {location}."
            };
        }

        /// <summary>
        /// Creates a failed interception result.
        /// </summary>
        /// <param name="interceptorId">ID of the node that attempted interception.</param>
        /// <param name="interceptChance">Probability used.</param>
        /// <param name="location">Location where the attempt was made.</param>
        /// <returns>Failed <see cref="InterceptionResult"/>.</returns>
        public static InterceptionResult CreateFailure(string interceptorId, float interceptChance, HexCoord location)
        {
            return new InterceptionResult
            {
                intercepted = false,
                interceptorId = interceptorId,
                interceptChance = interceptChance,
                interceptLocation = location,
                description = $"Interception by '{interceptorId}' FAILED " +
                              $"(chance: {interceptChance:P0}) at {location}. Threat penetrated defense."
            };
        }

        /// <summary>
        /// Creates a result indicating no interception was possible.
        /// </summary>
        /// <param name="reason">Reason why interception was not attempted.</param>
        /// <returns>Non-attempted <see cref="InterceptionResult"/>.</returns>
        public static InterceptionResult CreateNoIntercept(string reason)
        {
            return new InterceptionResult
            {
                intercepted = false,
                interceptorId = string.Empty,
                interceptChance = 0f,
                interceptLocation = null,
                description = reason
            };
        }
    }

    // =========================================================================
    // SHARED DEFENSE NETWORK (MonoBehaviour)
    // =========================================================================

    /// <summary>
    /// Integrated missile defense network system for alliances. Manages defense
    /// nodes, calculates coverage, and processes interception attempts against
    /// incoming threats.
    /// <para>
    /// The defense network operates in layers:
    /// <list type="number">
    ///   <item>Outer layer: Long-range interceptors with lower accuracy</item>
    ///   <item>Middle layer: Medium-range interceptors with balanced stats</item>
    ///   <item>Inner layer: Point-defense systems with high accuracy</item>
    ///   <item>Orbital layer: Space-based interceptors (via SpaceWeaponsSystem)</item>
    /// </list>
    /// Multiple interception attempts can be made against a single threat,
    /// with each successive attempt having reduced effectiveness.
    /// </para>
    /// </summary>
    public class SharedDefenseNetwork : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // EVENTS
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired when a defense node is added to the network.
        /// Arguments: (nodeId, allianceId).
        /// </summary>
        public event Action<string, string> OnDefenseNodeAdded;

        /// <summary>
        /// Fired when an interception attempt is made.
        /// Arguments: (InterceptionResult).
        /// </summary>
        public event Action<InterceptionResult> OnInterceptionAttempt;

        /// <summary>
        /// Fired when a defense node's state changes.
        /// Arguments: (nodeId, oldState, newState).
        /// </summary>
        public event Action<string, DefenseNodeState, DefenseNodeState> OnNodeStateChanged;

        /// <summary>
        /// Fired when a defense node is destroyed.
        /// Arguments: (nodeId, allianceId).
        /// </summary>
        public event Action<string, string> OnDefenseNodeDestroyed;

        // -----------------------------------------------------------------
        // SERIALIZED FIELDS
        // -----------------------------------------------------------------

        [Header("Network Settings")]
        [Tooltip("Default interception range for new defense nodes (hexes).")]
        [SerializeField] private float defaultInterceptRange = 5f;

        [Tooltip("Default intercept chance for new defense nodes (0-1).")]
        [SerializeField] private float defaultInterceptChance = 0.6f;

        [Tooltip("Per-turn repair rate for damaged nodes (durability points).")]
        [SerializeField] private int defaultRepairRate = 5;

        [Tooltip("Damage threshold below which a node becomes Damaged (percentage of max).")]
        [SerializeField] private float damagedThreshold = 0.5f;

        [Tooltip("Damage threshold below which a node goes Offline (percentage of max).")]
        [SerializeField] private float offlineThreshold = 0.15f;

        [Tooltip("Successive interception penalty (reduced chance per additional attempt).")]
        [SerializeField] private float successiveInterceptPenalty = 0.15f;

        [Tooltip("Maximum number of interception attempts per threat per turn.")]
        [SerializeField] private int maxInterceptAttempts = 3;

        [Tooltip("Coverage percentage threshold for 'well-defended' status.")]
        [SerializeField] private float wellDefendedThreshold = 0.7f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // -----------------------------------------------------------------
        // PRIVATE STATE
        // -----------------------------------------------------------------

        private Dictionary<string, DefenseNode> _defenseNodes = new Dictionary<string, DefenseNode>();
        private Dictionary<string, List<string>> _allianceNodeMap = new Dictionary<string, List<string>>();
        private Dictionary<string, Dictionary<string, int>> _interceptionHistory = new Dictionary<string, Dictionary<string, int>>();
        private int _nodeIdCounter = 0;

        // -----------------------------------------------------------------
        // PUBLIC PROPERTIES
        // -----------------------------------------------------------------

        /// <summary>Total number of defense nodes across all alliances.</summary>
        public int TotalNodeCount => _defenseNodes.Count;

        /// <summary>Number of active defense nodes.</summary>
        public int ActiveNodeCount => _defenseNodes.Count(n => n.Value.state == DefenseNodeState.Active);

        /// <summary>Number of damaged defense nodes.</summary>
        public int DamagedNodeCount => _defenseNodes.Count(n => n.Value.state == DefenseNodeState.Damaged);

        /// <summary>Read-only access to all defense nodes.</summary>
        public IReadOnlyDictionary<string, DefenseNode> DefenseNodes => _defenseNodes;

        // =================================================================
        // DEFENSE NODE MANAGEMENT
        // =================================================================

        /// <summary>
        /// Adds a new defense node to an alliance's missile defense network.
        /// The node is placed at the specified hex coordinate and begins
        /// operating immediately in Active state.
        /// </summary>
        /// <param name="allianceId">The alliance to add the node to.</param>
        /// <param name="nation">The nation building/owning the node.</param>
        /// <param name="pos">Hex coordinate placement position.</param>
        /// <param name="interceptRange">Override intercept range (0 for default).</param>
        /// <param name="interceptChance">Override intercept chance (0 for default).</param>
        /// <returns>The newly created <see cref="DefenseNode"/>, or null if creation failed.</returns>
        public DefenseNode AddDefenseNode(string allianceId, string nation, HexCoord pos,
            float interceptRange = 0f, float interceptChance = 0f)
        {
            if (string.IsNullOrEmpty(allianceId))
            {
                Debug.LogWarning("[DefenseNetwork] AddDefenseNode: alliance ID required.");
                return null;
            }

            if (string.IsNullOrEmpty(nation))
            {
                Debug.LogWarning("[DefenseNetwork] AddDefenseNode: nation ID required.");
                return null;
            }

            if (pos == null)
            {
                Debug.LogWarning("[DefenseNetwork] AddDefenseNode: position cannot be null.");
                return null;
            }

            string nodeId = $"defnode_{++_nodeIdCounter}_{allianceId}_{nation}";

            float range = interceptRange > 0f ? interceptRange : defaultInterceptRange;
            float chance = interceptChance > 0f ? interceptChance : defaultInterceptChance;

            var node = new DefenseNode(nodeId, nation, allianceId, pos, range, chance)
            {
                repairRate = defaultRepairRate,
                maintenanceCost = range * 8f, // Larger range = higher cost
                interceptCharges = Mathf.CeilToInt(range), // More range = more charges
                maxInterceptCharges = Mathf.CeilToInt(range)
            };

            _defenseNodes[nodeId] = node;

            // Update alliance mapping
            if (!_allianceNodeMap.ContainsKey(allianceId))
            {
                _allianceNodeMap[allianceId] = new List<string>();
            }
            _allianceNodeMap[allianceId].Add(nodeId);

            // Initialize interception history
            if (!_interceptionHistory.ContainsKey(allianceId))
            {
                _interceptionHistory[allianceId] = new Dictionary<string, int>();
            }

            OnDefenseNodeAdded?.Invoke(nodeId, allianceId);

            if (enableDebugLogs)
            {
                Debug.Log($"[DefenseNetwork] Node '{nodeId}' added to alliance '{allianceId}' by '{nation}'. " +
                          $"Position: {pos}, Range: {range:F1}, Chance: {chance:P0}.");
            }

            return node;
        }

        /// <summary>
        /// Removes a defense node from the network.
        /// </summary>
        /// <param name="nodeId">The node to remove.</param>
        /// <returns>True if removal was successful.</returns>
        public bool RemoveDefenseNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !_defenseNodes.ContainsKey(nodeId))
            {
                Debug.LogWarning("[DefenseNetwork] RemoveDefenseNode: node not found.");
                return false;
            }

            var node = _defenseNodes[nodeId];
            string allianceId = node.allianceId;

            _defenseNodes.Remove(nodeId);

            if (_allianceNodeMap.ContainsKey(allianceId))
            {
                _allianceNodeMap[allianceId].Remove(nodeId);
            }

            OnDefenseNodeDestroyed?.Invoke(nodeId, allianceId);

            if (enableDebugLogs)
            {
                Debug.Log($"[DefenseNetwork] Node '{nodeId}' removed from alliance '{allianceId}'.");
            }

            return true;
        }

        /// <summary>
        /// Retrieves a defense node by its ID.
        /// </summary>
        /// <param name="nodeId">The node identifier.</param>
        /// <returns>The <see cref="DefenseNode"/>, or null if not found.</returns>
        public DefenseNode GetDefenseNode(string nodeId)
        {
            _defenseNodes.TryGetValue(nodeId, out var node);
            return node;
        }

        /// <summary>
        /// Retrieves all defense nodes belonging to a specific alliance.
        /// </summary>
        /// <param name="allianceId">The alliance to query.</param>
        /// <returns>List of defense nodes in the alliance.</returns>
        public List<DefenseNode> GetDefenseNodes(string allianceId)
        {
            if (string.IsNullOrEmpty(allianceId)) return new List<DefenseNode>();

            if (!_allianceNodeMap.ContainsKey(allianceId)) return new List<DefenseNode>();

            return _allianceNodeMap[allianceId]
                .Where(id => _defenseNodes.ContainsKey(id))
                .Select(id => _defenseNodes[id])
                .ToList();
        }

        /// <summary>
        /// Retrieves all defense nodes belonging to a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of defense nodes owned by the nation.</returns>
        public List<DefenseNode> GetDefenseNodesByNation(string nationId)
        {
            return _defenseNodes.Values
                .Where(n => n.ownerNation == nationId)
                .ToList();
        }

        // =================================================================
        // DEFENSE COVERAGE CALCULATIONS
        // =================================================================

        /// <summary>
        /// Calculates the overall missile defense coverage for an alliance.
        /// <para>
        /// Coverage is computed by evaluating the percentage of alliance territory
        /// that falls within at least one active defense node's intercept range.
        /// </para>
        /// <para>
        /// Formula: coverage = (covered area / total alliance area) * 100
        /// A value of 0.7 (70%) or higher is considered "well-defended".
        /// </para>
        /// </summary>
        /// <param name="allianceId">The alliance to evaluate.</param>
        /// <returns>Coverage percentage (0-1).</returns>
        public float CalculateAllianceMissileDefense(string allianceId)
        {
            var nodes = GetDefenseNodes(allianceId);
            if (nodes.Count == 0) return 0f;

            // Calculate average effectiveness of active nodes
            var activeNodes = nodes.Where(n => n.state == DefenseNodeState.Active).ToList();
            if (activeNodes.Count == 0) return 0f;

            // Base coverage from node count relative to ideal (approximately 1 node per 10 hex radius)
            float nodeCoverageBonus = Mathf.Min(activeNodes.Count * 0.15f, 0.6f);

            // Average intercept chance of active nodes
            float avgInterceptChance = activeNodes.Average(n => n.interceptChance * (n.durability / 100f));

            // Range coverage (nodes with larger ranges provide more coverage)
            float avgRange = activeNodes.Average(n => n.interceptRange);
            float rangeCoverage = Mathf.Min(avgRange / 10f, 0.4f);

            // Tech level bonus
            float avgTechLevel = activeNodes.Average(n => (float)n.techLevel);
            float techBonus = (avgTechLevel - 1f) * 0.05f;

            float totalCoverage = nodeCoverageBonus + (avgInterceptChance * rangeCoverage) + techBonus;

            return Mathf.Clamp(totalCoverage, 0f, 1f);
        }

        /// <summary>
        /// Calculates the local defense strength at a specific hex coordinate.
        /// Considers all active nodes within range of the position.
        /// </summary>
        /// <param name="allianceId">The alliance to evaluate.</param>
        /// <param name="target">The target hex coordinate.</param>
        /// <returns>Local defense strength (0-1).</returns>
        public float CalculateLocalDefenseStrength(string allianceId, HexCoord target)
        {
            if (string.IsNullOrEmpty(allianceId) || target == null) return 0f;

            var nodes = GetDefenseNodes(allianceId);
            float localStrength = 0f;

            foreach (var node in nodes)
            {
                if (node.state != DefenseNodeState.Active) continue;
                if (node.position == null) continue;
                if (node.interceptCharges <= 0) continue;

                // Calculate hex distance between node and target
                float distance = HexDistance(node.position, target);

                if (distance <= node.interceptRange)
                {
                    // Effectiveness decreases with distance
                    float distanceFactor = 1f - (distance / node.interceptRange) * 0.5f;
                    float nodeEffectiveness = node.interceptChance * (node.durability / 100f) * distanceFactor;
                    localStrength = Mathf.Max(localStrength, nodeEffectiveness);
                }
            }

            return Mathf.Clamp(localStrength, 0f, 1f);
        }

        /// <summary>
        /// Calculates the number of defense nodes that can reach a given target.
        /// </summary>
        /// <param name="allianceId">The alliance to evaluate.</param>
        /// <param name="target">The target hex coordinate.</param>
        /// <returns>Number of active nodes in range.</returns>
        public int GetNodesInRange(string allianceId, HexCoord target)
        {
            if (string.IsNullOrEmpty(allianceId) || target == null) return 0;

            var nodes = GetDefenseNodes(allianceId);
            int count = 0;

            foreach (var node in nodes)
            {
                if (node.state != DefenseNodeState.Active) continue;
                if (node.position == null) continue;
                if (node.interceptCharges <= 0) continue;

                float distance = HexDistance(node.position, target);
                if (distance <= node.interceptRange)
                {
                    count++;
                }
            }

            return count;
        }

        // =================================================================
        // INTERCEPTION
        // =================================================================

        /// <summary>
        /// Attempts to intercept an incoming missile or threat targeting a specific hex.
        /// <para>
        /// The system evaluates all active defense nodes in range and selects the best
        /// one to attempt interception. Multiple attempts may be made (up to
        /// <see cref="maxInterceptAttempts"/>) with diminishing probability.
        /// </para>
        /// <para>
        /// Intercept resolution:
        /// <list type="number">
        ///   <item>Find all active nodes in range with available charges</item>
        ///   <item>Sort by intercept probability (highest first)</item>
        ///   <item>For each node up to max attempts, roll against intercept chance</item>
        ///   <item>Apply successive penalty for each additional attempt</item>
        ///   <item>Return result on first success or final failure</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="allianceId">The alliance attempting the interception.</param>
        /// <param name="target">The hex coordinate being targeted by the incoming threat.</param>
        /// <returns><see cref="InterceptionResult"/> with the outcome details.</returns>
        public InterceptionResult InterceptIncomingMissile(string allianceId, HexCoord target)
        {
            if (string.IsNullOrEmpty(allianceId))
            {
                return InterceptionResult.CreateNoIntercept("No alliance specified for interception.");
            }

            if (target == null)
            {
                return InterceptionResult.CreateNoIntercept("Target coordinate is null.");
            }

            var nodes = GetDefenseNodes(allianceId);
            if (nodes.Count == 0)
            {
                return InterceptionResult.CreateNoIntercept(
                    $"No defense nodes in alliance '{allianceId}'.");
            }

            // Find all active nodes in range with charges
            var eligibleNodes = nodes
                .Where(n => n.state == DefenseNodeState.Active && n.interceptCharges > 0 && n.position != null)
                .Where(n => HexDistance(n.position, target) <= n.interceptRange)
                .OrderByDescending(n => n.interceptChance * (n.durability / 100f))
                .ToList();

            if (eligibleNodes.Count == 0)
            {
                return InterceptionResult.CreateNoIntercept(
                    $"No active defense nodes in range of target {target}.");
            }

            // Attempt interception with up to maxInterceptAttempts nodes
            int attemptsMade = 0;

            foreach (var node in eligibleNodes)
            {
                if (attemptsMade >= maxInterceptAttempts) break;

                // Calculate effective intercept chance
                float distanceFactor = 1f - (HexDistance(node.position, target) / node.interceptRange) * 0.3f;
                float durabilityFactor = node.durability / 100f;
                float successivePenalty = 1f - (attemptsMade * successiveInterceptPenalty);
                float effectiveChance = node.interceptChance * distanceFactor * durabilityFactor * successivePenalty;
                effectiveChance = Mathf.Clamp01(effectiveChance);

                // Consume a charge
                node.interceptCharges--;
                attemptsMade++;

                // Roll for interception
                float roll = UnityEngine.Random.value;

                if (enableDebugLogs)
                {
                    Debug.Log($"[DefenseNetwork] Intercept attempt #{attemptsMade} by '{node.nodeId}': " +
                              $"chance={effectiveChance:P1}, roll={roll:P1}, " +
                              $"distance factor={distanceFactor:F2}, durability={durabilityFactor:P0}.");
                }

                InterceptionResult result;

                if (roll <= effectiveChance)
                {
                    // Successful interception
                    result = InterceptionResult.CreateSuccess(node.nodeId, effectiveChance, target);

                    // Record in history
                    RecordInterception(allianceId, node.nodeId, true);
                }
                else
                {
                    // Failed interception - may try again with next node
                    result = InterceptionResult.CreateFailure(node.nodeId, effectiveChance, target);

                    // Record in history
                    RecordInterception(allianceId, node.nodeId, false);

                    // Continue to next node if more attempts available
                    continue;
                }

                OnInterceptionAttempt?.Invoke(result);
                return result;
            }

            // All attempts failed
            var failResult = InterceptionResult.CreateNoIntercept(
                $"{attemptsMade} interception attempt(s) failed. Threat penetrated defense at {target}.");

            OnInterceptionAttempt?.Invoke(failResult);
            return failResult;
        }

        /// <summary>
        /// Records an interception attempt in the history for statistics.
        /// </summary>
        private void RecordInterception(string allianceId, string nodeId, bool success)
        {
            if (!_interceptionHistory.ContainsKey(allianceId))
            {
                _interceptionHistory[allianceId] = new Dictionary<string, int>();
            }

            string key = success ? $"{nodeId}_success" : $"{nodeId}_failure";
            if (!_interceptionHistory[allianceId].ContainsKey(key))
            {
                _interceptionHistory[allianceId][key] = 0;
            }
            _interceptionHistory[allianceId][key]++;
        }

        /// <summary>
        /// Gets interception statistics for a defense node.
        /// </summary>
        /// <param name="allianceId">The alliance.</param>
        /// <param name="nodeId">The defense node.</param>
        /// <returns>Tuple of (successes, failures).</returns>
        public (int successes, int failures) GetNodeInterceptionStats(string allianceId, string nodeId)
        {
            int successes = 0;
            int failures = 0;

            if (_interceptionHistory.ContainsKey(allianceId))
            {
                _interceptionHistory[allianceId].TryGetValue($"{nodeId}_success", out successes);
                _interceptionHistory[allianceId].TryGetValue($"{nodeId}_failure", out failures);
            }

            return (successes, failures);
        }

        // =================================================================
        // NETWORK UPDATE
        // =================================================================

        /// <summary>
        /// Updates the defense network for an alliance each turn. Processes:
        /// <list type="number">
        ///   <item>Repair damaged nodes</item>
        ///   <item>Replenish intercept charges</item>
        ///   <item>Update node states based on durability</item>
        ///   <item>Maintenance cost processing</item>
        /// </list>
        /// </summary>
        /// <param name="allianceId">The alliance to update.</param>
        public void UpdateNetwork(string allianceId)
        {
            var nodes = GetDefenseNodes(allianceId);
            if (nodes.Count == 0) return;

            foreach (var node in nodes)
            {
                DefenseNodeState oldState = node.state;

                // --- Repair damaged nodes ---
                if (node.state == DefenseNodeState.Damaged)
                {
                    node.durability = Mathf.Min(node.maxDurability, node.durability + node.repairRate);

                    if (enableDebugLogs && node.repairRate > 0)
                    {
                        Debug.Log($"[DefenseNetwork] Node '{node.nodeId}' repairing. " +
                                  $"Durability: {node.durability}/{node.maxDurability}.");
                    }
                }

                // --- Replenish intercept charges ---
                node.interceptCharges = node.maxInterceptCharges;

                // --- Update state based on durability ---
                float durabilityPercent = (float)node.durability / node.maxDurability;

                if (durabilityPercent <= offlineThreshold)
                {
                    node.state = DefenseNodeState.Offline;
                }
                else if (durabilityPercent <= damagedThreshold)
                {
                    node.state = DefenseNodeState.Damaged;
                }
                else
                {
                    node.state = DefenseNodeState.Active;
                }

                // --- Fire state change event ---
                if (oldState != node.state)
                {
                    OnNodeStateChanged?.Invoke(node.nodeId, oldState, node.state);

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[DefenseNetwork] Node '{node.nodeId}' state changed: " +
                                  $"{oldState} -> {node.state}.");
                    }
                }
            }

            if (enableDebugLogs)
            {
                int active = nodes.Count(n => n.state == DefenseNodeState.Active);
                int damaged = nodes.Count(n => n.state == DefenseNodeState.Damaged);
                int offline = nodes.Count(n => n.state == DefenseNodeState.Offline);

                Debug.Log($"[DefenseNetwork] Alliance '{allianceId}' network update: " +
                          $"Active={active}, Damaged={damaged}, Offline={offline}.");
            }
        }

        /// <summary>
        /// Updates all alliance defense networks.
        /// </summary>
        public void UpdateAllNetworks()
        {
            foreach (var allianceId in _allianceNodeMap.Keys.ToList())
            {
                UpdateNetwork(allianceId);
            }
        }

        /// <summary>
        /// Manually damages a defense node (from enemy attack or other sources).
        /// </summary>
        /// <param name="nodeId">The node to damage.</param>
        /// <param name="damageAmount">Amount of durability damage.</param>
        /// <returns>True if the node was found and damaged.</returns>
        public bool DamageNode(string nodeId, int damageAmount)
        {
            if (string.IsNullOrEmpty(nodeId) || !_defenseNodes.ContainsKey(nodeId)) return false;

            var node = _defenseNodes[nodeId];
            DefenseNodeState oldState = node.state;

            node.durability = Mathf.Max(0, node.durability - damageAmount);

            // Update state based on new durability
            float durabilityPercent = (float)node.durability / node.maxDurability;
            if (durabilityPercent <= offlineThreshold)
            {
                node.state = DefenseNodeState.Offline;
            }
            else if (durabilityPercent <= damagedThreshold)
            {
                node.state = DefenseNodeState.Damaged;
            }

            if (oldState != node.state)
            {
                OnNodeStateChanged?.Invoke(nodeId, oldState, node.state);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DefenseNetwork] Node '{nodeId}' took {damageAmount} damage. " +
                          $"Durability: {node.durability}/{node.maxDurability}, State: {node.state}.");
            }

            return true;
        }

        /// <summary>
        /// Manually repairs a defense node.
        /// </summary>
        /// <param name="nodeId">The node to repair.</param>
        /// <param name="repairAmount">Amount of durability to restore.</param>
        /// <returns>True if the node was found and repaired.</returns>
        public bool RepairNode(string nodeId, int repairAmount)
        {
            if (string.IsNullOrEmpty(nodeId) || !_defenseNodes.ContainsKey(nodeId)) return false;

            var node = _defenseNodes[nodeId];
            DefenseNodeState oldState = node.state;

            node.durability = Mathf.Min(node.maxDurability, node.durability + repairAmount);

            // Update state
            float durabilityPercent = (float)node.durability / node.maxDurability;
            if (durabilityPercent > damagedThreshold)
            {
                node.state = DefenseNodeState.Active;
            }
            else if (durabilityPercent > offlineThreshold)
            {
                node.state = DefenseNodeState.Damaged;
            }

            if (oldState != node.state)
            {
                OnNodeStateChanged?.Invoke(nodeId, oldState, node.state);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[DefenseNetwork] Node '{nodeId}' repaired for {repairAmount}. " +
                          $"Durability: {node.durability}/{node.maxDurability}, State: {node.state}.");
            }

            return true;
        }

        // =================================================================
        // UTILITY METHODS
        // =================================================================

        /// <summary>
        /// Checks if an alliance is "well-defended" (coverage above threshold).
        /// </summary>
        /// <param name="allianceId">The alliance to check.</param>
        /// <returns>True if coverage meets the well-defended threshold.</returns>
        public bool IsWellDefended(string allianceId)
        {
            return CalculateAllianceMissileDefense(allianceId) >= wellDefendedThreshold;
        }

        /// <summary>
        /// Gets the total maintenance cost for all defense nodes in an alliance.
        /// </summary>
        /// <param name="allianceId">The alliance to query.</param>
        /// <returns>Total per-turn maintenance cost.</returns>
        public float GetTotalMaintenanceCost(string allianceId)
        {
            return GetDefenseNodes(allianceId).Sum(n => n.maintenanceCost);
        }

        /// <summary>
        /// Gets a coverage summary string for an alliance (useful for UI display).
        /// </summary>
        /// <param name="allianceId">The alliance to summarize.</param>
        /// <returns>Formatted summary string.</returns>
        public string GetNetworkSummary(string allianceId)
        {
            var nodes = GetDefenseNodes(allianceId);
            if (nodes.Count == 0) return $"Alliance '{allianceId}': No defense nodes.";

            float coverage = CalculateAllianceMissileDefense(allianceId);
            int active = nodes.Count(n => n.state == DefenseNodeState.Active);
            int damaged = nodes.Count(n => n.state == DefenseNodeState.Damaged);
            int offline = nodes.Count(n => n.state == DefenseNodeState.Offline);
            float totalMaintenance = nodes.Sum(n => n.maintenanceCost);

            return $"Alliance '{allianceId}': Coverage={coverage:P0}, " +
                   $"Nodes={nodes.Count} (Active={active}, Damaged={damaged}, Offline={offline}), " +
                   $"Maintenance={totalMaintenance:F0}/turn";
        }

        // =================================================================
        // PRIVATE HELPERS
        // =================================================================

        /// <summary>
        /// Calculates hex distance between two coordinates.
        /// Uses cube coordinate distance formula for axial hex grids.
        /// </summary>
        /// <param name="a">First hex coordinate.</param>
        /// <param name="b">Second hex coordinate.</param>
        /// <returns>Distance in hex units.</returns>
        private float HexDistance(HexCoord a, HexCoord b)
        {
            if (a == null || b == null) return float.MaxValue;

            // Cube coordinate conversion and distance
            // For axial coordinates (q, r), cube coords are (q, -q-r, r)
            float dq = Mathf.Abs(a.q - b.q);
            float dr = Mathf.Abs(a.r - b.r);
            float ds = Mathf.Abs((-a.q - a.r) - (-b.q - b.r));

            return Mathf.Max(dq, dr, ds);
        }

        /// <summary>
        /// Unity lifecycle: called every frame. Reserved for future real-time updates.
        /// </summary>
        private void Update()
        {
            // Reserved for real-time defense visualization, targeting overlays, etc.
        }
    }
}
