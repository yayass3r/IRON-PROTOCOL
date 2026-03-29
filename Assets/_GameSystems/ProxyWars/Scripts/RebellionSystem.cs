// =====================================================================
// IRON PROTOCOL - Rebellion System
// Internal rebellions, civil wars, secession, and suppression mechanics.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.GameSystems.ProxyWars
{
    // =================================================================
    // ENUMERATIONS
    // =================================================================

    /// <summary>
    /// Types of internal rebellion that can occur within a nation.
    /// Each type has different triggers, severity, and resolution conditions.
    /// </summary>
    public enum RebellionType
    {
        /// <summary>Peaceful protests and civil disobedience. Low violence, easier to resolve.</summary>
        CivilUnrest,
        /// <summary>Organized armed insurrection against the government. High threat level.</summary>
        ArmedRebellion,
        /// <summary>A region attempts to break away and form an independent state.</summary>
        Secessionist,
        /// <summary>Rebellion driven by ideological differences (political, religious, or social).</summary>
        Ideological
    }

    // =================================================================
    // DATA CLASSES
    // =================================================================

    /// <summary>
    /// Represents an active rebellion within a nation. Tracks strength, support,
    /// demands, and duration. Can be suppressed, negotiated with, or allowed to grow
    /// into a full civil war.
    /// </summary>
    [Serializable]
    public class Rebellion
    {
        /// <summary>Unique rebellion identifier.</summary>
        public string rebellionId;

        /// <summary>Nation experiencing the rebellion.</summary>
        public string nationId;

        /// <summary>Center hex coordinate of the rebellion's stronghold.</summary>
        public HexCoord centerHex;

        /// <summary>Type of rebellion affecting tactics, demands, and severity.</summary>
        public RebellionType type;

        /// <summary>Military strength of the rebellion from 0 to 100.</summary>
        [Range(0, 100)] public int strength;

        /// <summary>Public support for the rebellion from 0 to 100.</summary>
        [Range(0, 100)] public int support;

        /// <summary>List of rebel demands (human-readable strings for UI).</summary>
        public List<string> demands = new List<string>();

        /// <summary>Whether the rebellion is currently active.</summary>
        public bool isActive;

        /// <summary>Number of turns the rebellion has been active.</summary>
        public int turnsActive;

        /// <summary>Whether the rebellion was resolved peacefully through negotiation.</summary>
        public bool resolvedPeacefully;

        /// <summary>Whether the rebellion was crushed by military force.</summary>
        public bool crushed;

        /// <summary>Human-readable outcome description (set when rebellion ends).</summary>
        public string outcome;

        /// <summary>Estimated economic damage per turn caused by the rebellion.</summary>
        public float economicDamagePerTurn;

        /// <summary>Stability damage per turn caused by the rebellion.</summary>
        public float stabilityDamagePerTurn;

        /// <summary>ID of the proxy war (if any) that is fueling this rebellion.</summary>
        public string linkedProxyWarId;

        /// <summary>Turns remaining until the rebellion escalates to the next severity level (0 = at max).</summary>
        public int turnsUntilEscalation;

        /// <summary>
        /// Creates a new rebellion with the specified parameters.
        /// </summary>
        public Rebellion(
            string rebellionId, string nationId, HexCoord centerHex,
            RebellionType type, int initialStrength = 20, int initialSupport = 15)
        {
            this.rebellionId = rebellionId;
            this.nationId = nationId;
            this.centerHex = centerHex;
            this.type = type;
            this.strength = Mathf.Clamp(initialStrength, 0, 100);
            this.support = Mathf.Clamp(initialSupport, 0, 100);
            this.isActive = true;
            this.turnsActive = 0;
            this.resolvedPeacefully = false;
            this.crushed = false;
            this.outcome = string.Empty;
            this.linkedProxyWarId = string.Empty;
            this.turnsUntilEscalation = 5;

            // Set type-specific defaults
            switch (type)
            {
                case RebellionType.CivilUnrest:
                    this.economicDamagePerTurn = 10f;
                    this.stabilityDamagePerTurn = 2f;
                    this.demands = new List<string> { "End corruption", "Fair elections", "Reduce taxes" };
                    break;

                case RebellionType.ArmedRebellion:
                    this.economicDamagePerTurn = 30f;
                    this.stabilityDamagePerTurn = 5f;
                    this.demands = new List<string> { "Government resignation", "Power sharing", "Military reform" };
                    break;

                case RebellionType.Secessionist:
                    this.economicDamagePerTurn = 40f;
                    this.stabilityDamagePerTurn = 6f;
                    this.demands = new List<string> { "Regional autonomy", "Independence referendum", "Resource control" };
                    break;

                case RebellionType.Ideological:
                    this.economicDamagePerTurn = 20f;
                    this.stabilityDamagePerTurn = 4f;
                    this.demands = new List<string> { "Ideological reforms", "Religious freedom", "Social change" };
                    break;
            }
        }
    }

    /// <summary>
    /// Result of an attempt to suppress or negotiate with a rebellion.
    /// </summary>
    [Serializable]
    public class RebellionActionResult
    {
        /// <summary>Whether the action succeeded in its goal.</summary>
        public bool success;

        /// <summary>Human-readable description of what happened.</summary>
        public string details;

        /// <summary>Cost of the action (treasury, military resources, or stability).</summary>
        public float cost;

        /// <summary>Change in rebellion strength (negative = reduced).</summary>
        public int strengthChange;

        /// <summary>Change in rebellion support (negative = reduced).</summary>
        public int supportChange;

        public RebellionActionResult(bool success, string details, float cost,
                                      int strengthChange = 0, int supportChange = 0)
        {
            this.success = success;
            this.details = details;
            this.cost = Mathf.Max(0f, cost);
            this.strengthChange = strengthChange;
            this.supportChange = supportChange;
        }
    }

    // =================================================================
    // REFERENCE INTERFACE FOR ESPIONAGE
    // =================================================================

    /// <summary>
    /// Minimal interface for espionage data needed by the rebellion system.
    /// Implement this in the EspionageManager and inject via SetEspionageManager().
    /// </summary>
    public interface IEspionageDataProvider
    {
        /// <summary>Gets the current counter-espionage rating for a nation (0-100).</summary>
        float GetCounterEspionageLevel(string nationId);

        /// <summary>Gets the number of active covert operations targeting a nation.</summary>
        int GetActiveOperationsAgainst(string nationId);

        /// <summary>Gets the current spy network strength in a nation (0-100).</summary>
        float GetSpyNetworkStrength(string nationId);
    }

    // =================================================================
    // REBELLION SYSTEM
    // =================================================================

    /// <summary>
    /// Core system managing internal rebellions, civil wars, and domestic unrest.
    /// Checks for rebellion conditions, simulates growth/suppression, handles
    /// negotiation and military responses, and integrates with proxy war and
    /// government systems.
    /// Attach to a persistent GameManager as a singleton.
    /// </summary>
    public class RebellionSystem : MonoBehaviour
    {
        // -------------------------------------------------------------
        // CONSTANTS
        // -------------------------------------------------------------
        private const int MaxSimultaneousRebellions = 5;
        private const int RebellionStrengthWinThreshold = 90;
        private const int RebellionStrengthLoseThreshold = 5;
        private const float StabilityRebellionTrigger = 25f;
        private const float ApprovalRebellionTrigger = 20f;
        private const int BaseRebellionCheckInterval = 3; // check every N turns
        private const float ProxyWarRebellionChance = 0.4f;
        private const float EspionageRebellionBonus = 0.1f;

        // -------------------------------------------------------------
        // STATE
        // -------------------------------------------------------------
        private readonly Dictionary<string, Rebellion> _rebellions = new Dictionary<string, Rebellion>();
        private readonly Dictionary<string, int> _nationLastRebellionCheck = new Dictionary<string, int>();
        private int _currentTurn = 0;
        private int _rebellionCounter = 0;
        private IEspionageDataProvider _espionageProvider;

        // -------------------------------------------------------------
        // EVENTS
        // -------------------------------------------------------------
        /// <summary>Fired when a new rebellion starts. Parameters: (rebellion, nationId).</summary>
        public event Action<Rebellion, string> OnRebellionStarted;

        /// <summary>Fired when a rebellion ends. Parameters: (rebellion, outcome).</summary>
        public event Action<Rebellion, string> OnRebellionEnded;

        // -------------------------------------------------------------
        // PROPERTIES
        // -------------------------------------------------------------
        /// <summary>Current global turn counter.</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>Number of currently active rebellions across all nations.</summary>
        public int ActiveRebellionCount => _rebellions.Count(r => r.Value.isActive);

        // =============================================================
        // INITIALIZATION
        // =============================================================

        /// <summary>
        /// Sets the espionage data provider for integration with the espionage system.
        /// Call this during game initialization.
        /// </summary>
        /// <param name="provider">Implementation of <see cref="IEspionageDataProvider"/>.</param>
        public void SetEspionageManager(IEspionageDataProvider provider)
        {
            _espionageProvider = provider;
            Debug.Log("[RebellionSystem] Espionage data provider registered.");
        }

        // =============================================================
        // REBELLION CHECK
        // =============================================================

        /// <summary>
        /// Checks whether conditions are ripe for a rebellion to start in a nation.
        /// Rebellions can trigger from:
        /// <list type="bullet">
        ///   <item>Stability below 25</item>
        ///   <item>Approval below 20</item>
        ///   <item>Active proxy war in the nation (40% chance per check)</item>
        ///   <item>Foreign espionage operations reducing cohesion</item>
        /// </list>
        /// The check is performed periodically (every 3 turns) to avoid constant triggering.
        /// </summary>
        /// <param name="nationId">Nation to check for rebellion conditions.</param>
        /// <param name="government">The current government state of the nation.</param>
        /// <param name="espionage">Optional espionage manager for additional triggers.</param>
        /// <returns>The newly started <see cref="Rebellion"/>, or null if conditions aren't met.</returns>
        public Rebellion CheckForRebellion(string nationId, Elections.Government government,
                                            IEspionageDataProvider espionage = null)
        {
            if (string.IsNullOrEmpty(nationId) || government == null)
            {
                Debug.LogError("[RebellionSystem] CheckForRebellion: nationId and government required.");
                return null;
            }

            // Don't check too frequently
            if (!_nationLastRebellionCheck.ContainsKey(nationId))
                _nationLastRebellionCheck[nationId] = 0;

            int turnsSinceLastCheck = _currentTurn - _nationLastRebellionCheck[nationId];
            if (turnsSinceLastCheck < BaseRebellionCheckInterval)
                return null;

            // Don't allow too many simultaneous rebellions in one nation
            if (GetActiveRebellions(nationId).Count >= MaxSimultaneousRebellions)
                return null;

            _nationLastRebellionCheck[nationId] = _currentTurn;

            // --- Calculate rebellion probability ---
            float rebellionChance = 0f;

            // Low stability trigger
            if (government.stability < StabilityRebellionTrigger)
            {
                float stabilityFactor = (StabilityRebellionTrigger - government.stability)
                                       / StabilityRebellionTrigger;
                rebellionChance += stabilityFactor * 0.4f;
            }

            // Low approval trigger
            if (government.approval < ApprovalRebellionTrigger)
            {
                float approvalFactor = (ApprovalRebellionTrigger - government.approval)
                                      / ApprovalRebellionTrigger;
                rebellionChance += approvalFactor * 0.3f;
            }

            // Active proxy war in this nation
            IEspionageDataProvider espProvider = espionage ?? _espionageProvider;
            bool hasProxyWar = false;

            // Check proxy war influence via espionage data or directly
            if (espProvider != null)
            {
                int activeOps = espProvider.GetActiveOperationsAgainst(nationId);
                if (activeOps > 0)
                {
                    rebellionChance += ProxyWarRebellionChance;
                    hasProxyWar = true;
                }

                // Espionage bonus
                float spyStrength = espProvider.GetSpyNetworkStrength(nationId);
                if (spyStrength > 30f)
                    rebellionChance += EspionageRebellionBonus;
            }

            // High corruption exacerbates rebellion risk
            if (government.corruption > 60f)
                rebellionChance += 0.1f;

            // War fatigue
            if (government.warFatigue > 70f)
                rebellionChance += 0.15f;

            // --- Roll for rebellion ---
            rebellionChance = Mathf.Clamp(rebellionChance, 0f, 0.95f);

            if (UnityEngine.Random.value < rebellionChance)
            {
                return SpawnRebellion(nationId, government, hasProxyWar);
            }

            return null;
        }

        /// <summary>
        /// Creates a new rebellion in the nation, determining type and severity from conditions.
        /// </summary>
        private Rebellion SpawnRebellion(string nationId, Elections.Government government, bool proxyWarLinked)
        {
            _rebellionCounter++;
            string rebellionId = $"REB_{nationId}_{_rebellionCounter:D3}";

            // Determine rebellion type based on conditions
            RebellionType type;
            float roll = UnityEngine.Random.value;

            if (government.stability < 10f)
            {
                type = RebellionType.ArmedRebellion;
            }
            else if (government.corruption > 50f)
            {
                type = roll < 0.5f ? RebellionType.CivilUnrest : RebellionType.Ideological;
            }
            else if (government.warFatigue > 60f)
            {
                type = roll < 0.6f ? RebellionType.ArmedRebellion : RebellionType.Ideological;
            }
            else
            {
                type = roll < 0.4f ? RebellionType.CivilUnrest
                         : roll < 0.7f ? RebellionType.Ideological
                         : roll < 0.9f ? RebellionType.ArmedRebellion
                         : RebellionType.Secessionist;
            }

            // Initial strength and support based on government weakness
            int initialStrength = Mathf.RoundToInt(
                (100f - government.stability) * 0.3f +
                (100f - government.approval) * 0.2f +
                UnityEngine.Random.Range(5f, 15f)
            );

            int initialSupport = Mathf.RoundToInt(
                (100f - government.approval) * 0.3f +
                government.corruption * 0.15f +
                UnityEngine.Random.Range(5f, 15f)
            );

            if (proxyWarLinked)
            {
                initialStrength += 15;
                initialSupport += 10;
            }

            initialStrength = Mathf.Clamp(initialStrength, 10, 60);
            initialSupport = Mathf.Clamp(initialSupport, 5, 50);

            // Random center hex (in a real game, this would be a border or resource hex)
            HexCoord center = new HexCoord(
                UnityEngine.Random.Range(-10, 11),
                UnityEngine.Random.Range(-10, 11)
            );

            var rebellion = new Rebellion(rebellionId, nationId, center, type,
                                          initialStrength, initialSupport);

            if (proxyWarLinked)
                rebellion.linkedProxyWarId = "PROXY_LINKED"; // simplified link

            _rebellions[rebellionId] = rebellion;

            Debug.LogWarning($"[RebellionSystem] *** REBELLION STARTED: {rebellionId} in {nationId} *** " +
                             $"Type: {type}, Strength: {initialStrength}, Support: {initialSupport}");

            OnRebellionStarted?.Invoke(rebellion, nationId);
            return rebellion;
        }

        // =============================================================
        // TURN-BASED UPDATE
        // =============================================================

        /// <summary>
        /// Updates all active rebellions for the current turn. Each turn:
        /// <list type="bullet">
        ///   <item>Rebellion strength and support evolve based on government state</item>
        ///   <item>Economic and stability damage is applied</item>
        ///   <item>Escalation timers tick down</item>
        ///   <item>Win/loss conditions are checked</item>
        /// </list>
        /// </summary>
        /// <param name="governments">Dictionary of current government states keyed by nation ID.</param>
        public void UpdateRebellions(Dictionary<string, Elections.Government> governments = null)
        {
            _currentTurn++;

            var endedRebellions = new List<Rebellion>();

            foreach (Rebellion rebellion in _rebellions.Values)
            {
                if (!rebellion.isActive) continue;

                rebellion.turnsActive++;

                // --- Get government state ---
                Elections.Government gov = null;
                if (governments != null)
                    governments.TryGetValue(rebellion.nationId, out gov);

                // --- 1. Strength evolution ---
                int strengthDelta = 0;

                // Low government stability feeds rebellion
                if (gov != null)
                {
                    if (gov.stability < 30f)
                        strengthDelta += 3;
                    else if (gov.stability < 50f)
                        strengthDelta += 1;
                    else
                        strengthDelta -= 2; // strong government suppresses

                    if (gov.approval < 30f)
                        strengthDelta += 2;

                    if (gov.corruption > 50f)
                        strengthDelta += 1;

                    // Proxy war boosts rebellion
                    if (!string.IsNullOrEmpty(rebellion.linkedProxyWarId))
                        strengthDelta += 2;
                }

                // Type-specific behavior
                switch (rebellion.type)
                {
                    case RebellionType.CivilUnrest:
                        // Slow growth, can dissipate naturally
                        strengthDelta += (rebellion.support > 40) ? 1 : -1;
                        break;
                    case RebellionType.ArmedRebellion:
                        // Aggressive growth
                        strengthDelta += 1;
                        break;
                    case RebellionType.Secessionist:
                        // Grows if regional control is weak
                        strengthDelta += (rebellion.strength > 50) ? 2 : 0;
                        break;
                    case RebellionType.Ideological:
                        // Grows with low approval
                        strengthDelta += (gov != null && gov.approval < 40f) ? 2 : 0;
                        break;
                }

                // Natural attrition
                if (rebellion.turnsActive > 15 && strengthDelta <= 0)
                    strengthDelta -= 1;

                rebellion.strength = Mathf.Clamp(rebellion.strength + strengthDelta, 0, 100);

                // --- 2. Support evolution ---
                int supportDelta = 0;

                if (gov != null)
                {
                    if (gov.approval < 25f)
                        supportDelta += 3;
                    else if (gov.approval < 40f)
                        supportDelta += 1;

                    if (gov.corruption > 60f)
                        supportDelta += 2;
                    else if (gov.corruption < 20f)
                        supportDelta -= 1;
                }

                // Strength breeds support
                if (rebellion.strength > 60)
                    supportDelta += 1;

                // Economic damage reduces public support for rebellion over time
                if (rebellion.economicDamagePerTurn > 20 && rebellion.turnsActive > 10)
                    supportDelta -= 1;

                rebellion.support = Mathf.Clamp(rebellion.support + supportDelta, 0, 100);

                // --- 3. Escalation timer ---
                if (rebellion.turnsUntilEscalation > 0)
                {
                    rebellion.turnsUntilEscalation--;

                    if (rebellion.turnsUntilEscalation == 0 && rebellion.type != RebellionType.ArmedRebellion)
                    {
                        // Escalate to more severe type
                        RebellionType escalatedType = rebellion.type == RebellionType.CivilUnrest
                            ? RebellionType.ArmedRebellion
                            : rebellion.type;

                        if (escalatedType != rebellion.type)
                        {
                            Debug.LogWarning($"[RebellionSystem] {rebellion.rebellionId} ESCALATED " +
                                             $"from {rebellion.type} to {escalatedType}!");
                            rebellion.type = escalatedType;
                            rebellion.strength = Mathf.Clamp(rebellion.strength + 10, 0, 100);
                            rebellion.economicDamagePerTurn *= 1.5f;
                            rebellion.stabilityDamagePerTurn *= 1.5f;
                            rebellion.turnsUntilEscalation = 0;
                        }
                    }
                }

                // --- 4. Check win/loss conditions ---
                // Rebellion wins if strength is very high
                if (rebellion.strength >= RebellionStrengthWinThreshold)
                {
                    rebellion.isActive = false;
                    rebellion.outcome = $"Rebellion {rebellion.rebellionId} has OVERTHROWN the government " +
                                       $"of {rebellion.nationId}. Nation descends into civil war.";
                    endedRebellions.Add(rebellion);

                    Debug.LogWarning($"[RebellionSystem] *** {rebellion.rebellionId}: GOVERNMENT OVERTHROWN ***");
                }
                // Rebellion collapses if strength is very low
                else if (rebellion.strength <= RebellionStrengthLoseThreshold && rebellion.turnsActive > 3)
                {
                    rebellion.isActive = false;
                    rebellion.crushed = true;
                    rebellion.outcome = $"Rebellion {rebellion.rebellionId} in {rebellion.nationId} " +
                                       $"has collapsed. Remaining fighters disperse.";
                    endedRebellions.Add(rebellion);

                    Debug.Log($"[RebellionSystem] {rebellion.rebellionId}: Rebellion collapsed.");
                }
                // Rebellion dissolves if support drops to zero
                else if (rebellion.support <= 0 && rebellion.turnsActive > 5)
                {
                    rebellion.isActive = false;
                    rebellion.resolvedPeacefully = true;
                    rebellion.outcome = $"Rebellion {rebellion.rebellionId} in {rebellion.nationId} " +
                                       $"has lost all public support and dissolved peacefully.";
                    endedRebellions.Add(rebellion);

                    Debug.Log($"[RebellionSystem] {rebellion.rebellionId}: Dissolved from lack of support.");
                }
            }

            // Fire events for ended rebellions
            foreach (Rebellion ended in endedRebellions)
            {
                OnRebellionEnded?.Invoke(ended, ended.outcome);
            }
        }

        /// <summary>
        /// Overload that updates without explicit government data (uses default evolution).
        /// </summary>
        public void UpdateRebellions()
        {
            UpdateRebellions(null);
        }

        // =============================================================
        // SUPPRESSION
        // =============================================================

        /// <summary>
        /// Attempts to suppress a rebellion using military force.
        /// Higher military force values are more effective but risk civilian casualties
        /// and increased support for the rebellion if disproportionate force is used.
        /// </summary>
        /// <param name="rebellionId">ID of the rebellion to suppress.</param>
        /// <param name="militaryForce">Military force allocated (0-100 scale).</param>
        /// <returns>A <see cref="RebellionActionResult"/> describing the outcome.</returns>
        public RebellionActionResult SuppressRebellion(string rebellionId, int militaryForce)
        {
            if (string.IsNullOrEmpty(rebellionId))
            {
                Debug.LogError("[RebellionSystem] SuppressRebellion: rebellionId is null or empty.");
                return new RebellionActionResult(false, "Invalid rebellion ID.", 0f);
            }

            if (!_rebellions.TryGetValue(rebellionId, out Rebellion rebellion))
            {
                Debug.LogError($"[RebellionSystem] Rebellion '{rebellionId}' not found.");
                return new RebellionActionResult(false, "Rebellion not found.", 0f);
            }

            if (!rebellion.isActive)
            {
                return new RebellionActionResult(false, "Rebellion is no longer active.", 0f);
            }

            militaryForce = Mathf.Clamp(militaryForce, 1, 100);
            float cost = militaryForce * 10f; // treasury cost

            // --- Calculate suppression effectiveness ---
            // Base effectiveness: military force vs rebellion strength
            float forceRatio = (float)militaryForce / (float)Mathf.Max(1, rebellion.strength);
            float suppressionRoll = UnityEngine.Random.value * forceRatio;

            // Civil unrest is easier to suppress with less force
            if (rebellion.type == RebellionType.CivilUnrest)
                suppressionRoll *= 1.3f;

            // Armed rebellions are harder to suppress
            if (rebellion.type == RebellionType.ArmedRebellion)
                suppressionRoll *= 0.7f;

            // Secessionist rebellions in their stronghold are harder
            if (rebellion.type == RebellionType.Secessionist && rebellion.strength > 50)
                suppressionRoll *= 0.8f;

            int strengthLoss = 0;
            int supportLoss = 0;
            bool success = false;
            string details;

            if (suppressionRoll > 0.7f)
            {
                // Decisive victory
                strengthLoss = Mathf.RoundToInt(militaryForce * 0.4f);
                supportLoss = Mathf.RoundToInt(militaryForce * 0.1f);
                success = true;
                details = $"Military suppression SUCCEEDED against {rebellion.rebellionId}. " +
                          $"Rebel strength reduced by {strengthLoss}, support by {supportLoss}.";
            }
            else if (suppressionRoll > 0.4f)
            {
                // Partial success
                strengthLoss = Mathf.RoundToInt(militaryForce * 0.2f);
                supportLoss = 0;
                success = true;
                details = $"Military suppression partially effective against {rebellion.rebellionId}. " +
                          $"Rebel strength reduced by {strengthLoss}.";
            }
            else
            {
                // Failed suppression
                strengthLoss = 0;
                // Disproportionate force can INCREASE support
                if (militaryForce > rebellion.strength * 1.5f)
                {
                    supportLoss = -Mathf.RoundToInt(militaryForce * 0.15f); // negative = support increased
                    details = $"Military suppression FAILED. Heavy-handed tactics have " +
                              $"INCREASED public support for the rebellion by {-supportLoss}.";
                }
                else
                {
                    details = $"Military suppression FAILED. Insufficient force to dislodge rebels.";
                }
                success = false;
            }

            rebellion.strength = Mathf.Clamp(rebellion.strength - strengthLoss, 0, 100);
            rebellion.support = Mathf.Clamp(rebellion.support - supportLoss, 0, 100);

            // Reset escalation timer after suppression attempt
            rebellion.turnsUntilEscalation = Mathf.Max(3, rebellion.turnsUntilEscalation);

            Debug.Log($"[RebellionSystem] Suppress {rebellionId}: {details} " +
                      $"Strength: {rebellion.strength}, Support: {rebellion.support}");

            return new RebellionActionResult(success, details, cost, -strengthLoss, -supportLoss);
        }

        // =============================================================
        // NEGOTIATION
        // =============================================================

        /// <summary>
        /// Attempts to negotiate a peaceful resolution with rebels.
        /// Costs treasury but may resolve the rebellion without further bloodshed.
        /// Success depends on the government's diplomatic stance, rebel demands,
        /// and willingness (support level).
        /// </summary>
        /// <param name="rebellionId">ID of the rebellion to negotiate with.</param>
        /// <returns>A <see cref="RebellionActionResult"/> describing the outcome.</returns>
        public RebellionActionResult NegotiateWithRebels(string rebellionId)
        {
            if (string.IsNullOrEmpty(rebellionId))
            {
                Debug.LogError("[RebellionSystem] NegotiateWithRebels: rebellionId is null or empty.");
                return new RebellionActionResult(false, "Invalid rebellion ID.", 0f);
            }

            if (!_rebellions.TryGetValue(rebellionId, out Rebellion rebellion))
            {
                Debug.LogError($"[RebellionSystem] Rebellion '{rebellionId}' not found.");
                return new RebellionActionResult(false, "Rebellion not found.", 0f);
            }

            if (!rebellion.isActive)
            {
                return new RebellionActionResult(false, "Rebellion is no longer active.", 0f);
            }

            // Negotiation cost scales with rebellion severity
            float cost = 200f + (rebellion.strength * 5f) + (rebellion.turnsActive * 20f);

            // --- Calculate negotiation chance ---
            float negotiationChance = 0f;

            // Civil unrest is most amenable to negotiation
            switch (rebellion.type)
            {
                case RebellionType.CivilUnrest:
                    negotiationChance = 0.6f;
                    break;
                case RebellionType.Ideological:
                    negotiationChance = 0.4f;
                    break;
                case RebellionType.ArmedRebellion:
                    negotiationChance = 0.25f;
                    break;
                case RebellionType.Secessionist:
                    negotiationChance = 0.15f; // hardest to negotiate
                    break;
            }

            // Moderate support is best for negotiation (too low = irrelevant, too high = intransigent)
            if (rebellion.support >= 20 && rebellion.support <= 60)
                negotiationChance += 0.1f;

            // Weak rebellions are more willing to negotiate
            if (rebellion.strength < 30)
                negotiationChance += 0.15f;

            // Notorious / long rebellies are harder
            if (rebellion.turnsActive > 10)
                negotiationChance -= 0.1f;

            negotiationChance = Mathf.Clamp(negotiationChance, 0.05f, 0.85f);

            bool success = UnityEngine.Random.value < negotiationChance;

            if (success)
            {
                // Peaceful resolution
                int strengthLoss = Mathf.RoundToInt(rebellion.strength * 0.6f);
                int supportLoss = Mathf.RoundToInt(rebellion.support * 0.5f);

                rebellion.strength = Mathf.Clamp(rebellion.strength - strengthLoss, 0, 100);
                rebellion.support = Mathf.Clamp(rebellion.support - supportLoss, 0, 100);

                // Check if rebellion resolves completely
                if (rebellion.strength < 10 && rebellion.support < 10)
                {
                    rebellion.isActive = false;
                    rebellion.resolvedPeacefully = true;
                    rebellion.outcome = $"Rebellion {rebellion.rebellionId} in {rebellion.nationId} " +
                                       $"resolved through peaceful negotiation. Demands partially met.";

                    string details = $"Negotiation SUCCEEDED. Rebellion resolved peacefully. " +
                                     $"Some demands of the rebels have been addressed.";

                    Debug.Log($"[RebellionSystem] *** {rebellionId}: PEACEFULLY RESOLVED ***");
                    OnRebellionEnded?.Invoke(rebellion, rebellion.outcome);
                    return new RebellionActionResult(true, details, cost, -strengthLoss, -supportLoss);
                }
                else
                {
                    string details = $"Negotiation partially succeeded. Rebel strength reduced by {strengthLoss}, " +
                                     $"support by {supportLoss}. Further negotiation or action needed.";

                    Debug.Log($"[RebellionSystem] {rebellionId}: {details}");
                    return new RebellionActionResult(true, details, cost, -strengthLoss, -supportLoss);
                }
            }
            else
            {
                // Failed negotiation
                string details;

                if (rebellion.type == RebellionType.ArmedRebellion)
                {
                    // Failed negotiation with armed groups can escalate
                    rebellion.strength = Mathf.Clamp(rebellion.strength + 5, 0, 100);
                    rebellion.turnsUntilEscalation = 0;
                    details = $"Negotiations FAILED with armed rebels. They have escalated hostilities. " +
                              $"Rebel strength increased by 5.";
                }
                else
                {
                    details = $"Negotiations FAILED. Rebels rejected the government's terms. " +
                              $"No progress toward resolution.";
                }

                Debug.LogWarning($"[RebellionSystem] {rebellionId}: {details}");
                return new RebellionActionResult(false, details, cost, 0, 0);
            }
        }

        // =============================================================
        // QUERIES
        // =============================================================

        /// <summary>
        /// Retrieves a rebellion by its unique ID.
        /// </summary>
        /// <param name="rebellionId">Rebellion identifier.</param>
        /// <returns>The <see cref="Rebellion"/>, or null if not found.</returns>
        public Rebellion GetRebellion(string rebellionId)
        {
            if (string.IsNullOrEmpty(rebellionId)) return null;
            _rebellions.TryGetValue(rebellionId, out Rebellion rebellion);
            return rebellion;
        }

        /// <summary>
        /// Returns all active rebellions for a specific nation.
        /// </summary>
        /// <param name="nationId">Nation to query.</param>
        /// <returns>List of active <see cref="Rebellion"/> objects in the nation.</returns>
        public List<Rebellion> GetActiveRebellions(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return new List<Rebellion>();

            return _rebellions.Values
                .Where(r => r.nationId == nationId && r.isActive)
                .ToList();
        }

        /// <summary>
        /// Returns all rebellions (active and historical) for a specific nation.
        /// </summary>
        /// <param name="nationId">Nation to query.</param>
        /// <returns>List of all <see cref="Rebellion"/> objects for the nation.</returns>
        public List<Rebellion> GetAllRebellions(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return new List<Rebellion>();

            return _rebellions.Values
                .Where(r => r.nationId == nationId)
                .ToList();
        }

        /// <summary>
        /// Returns all active rebellions across all nations.
        /// </summary>
        /// <returns>List of all active rebellions.</returns>
        public List<Rebellion> GetAllActiveRebellions()
        {
            return _rebellions.Values.Where(r => r.isActive).ToList();
        }

        /// <summary>
        /// Calculates the total economic damage being caused by all active rebellions in a nation.
        /// </summary>
        /// <param name="nationId">Nation to evaluate.</param>
        /// <returns>Total economic damage per turn.</returns>
        public float GetTotalEconomicDamage(string nationId)
        {
            return GetActiveRebellions(nationId).Sum(r => r.economicDamagePerTurn);
        }

        /// <summary>
        /// Calculates the total stability damage being caused by all active rebellions in a nation.
        /// </summary>
        /// <param name="nationId">Nation to evaluate.</param>
        /// <returns>Total stability damage per turn.</returns>
        public float GetTotalStabilityDamage(string nationId)
        {
            return GetActiveRebellions(nationId).Sum(r => r.stabilityDamagePerTurn);
        }

        /// <summary>
        /// Returns whether a nation currently has any active rebellions.
        /// </summary>
        /// <param name="nationId">Nation to check.</param>
        /// <returns>True if any active rebellion exists in the nation.</returns>
        public bool HasActiveRebellions(string nationId)
        {
            return _rebellions.Values.Any(r => r.nationId == nationId && r.isActive);
        }

        // =============================================================
        // FORCIBLY END REBELLION
        // =============================================================

        /// <summary>
        /// Forcibly ends a rebellion (e.g. when the nation is conquered or government changes).
        /// </summary>
        /// <param name="rebellionId">ID of the rebellion to end.</param>
        /// <param name="reason">Reason for ending the rebellion.</param>
        public void EndRebellion(string rebellionId, string reason = "Government change")
        {
            if (string.IsNullOrEmpty(rebellionId)) return;
            if (!_rebellions.TryGetValue(rebellionId, out Rebellion rebellion)) return;
            if (!rebellion.isActive) return;

            rebellion.isActive = false;
            rebellion.outcome = $"Rebellion {rebellionId} ended: {reason}.";

            Debug.Log($"[RebellionSystem] {rebellionId} ended: {reason}");
            OnRebellionEnded?.Invoke(rebellion, rebellion.outcome);
        }

        // =============================================================
        // UTILITY
        // =============================================================

        /// <summary>
        /// Clears all rebellion data. Use on game reset.
        /// </summary>
        public void Reset()
        {
            _rebellions.Clear();
            _nationLastRebellionCheck.Clear();
            _currentTurn = 0;
            _rebellionCounter = 0;
            Debug.Log("[RebellionSystem] All rebellion data cleared.");
        }
    }
}
