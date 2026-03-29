// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: OccupationManager.cs
// Description: System for managing occupied territories with two occupation types,
//              rebellion mechanics, resource extraction, and type transitions.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.Diplomacy
{
    /// <summary>
    /// Defines how an occupying nation administers a captured territory.
    /// Each type has different resource extraction rates, rebellion risks,
    /// and strategic benefits.
    /// </summary>
    public enum OccupationType
    {
        /// <summary>Territory is not occupied.</summary>
        None = 0,

        /// <summary>
        /// Direct military annexation. High resource extraction (80%) but high rebellion risk.
        /// The occupying force bears full administrative burden.
        /// </summary>
        DirectAnnexation = 1,

        /// <summary>
        /// Puppet government installed. Moderate resource extraction (40%) with lower rebellion risk.
        /// Puppet territories may provide allied units and intelligence.
        /// </summary>
        PuppetGovernment = 2
    }

    /// <summary>
    /// Represents a single occupied territory with its administration type,
    /// rebellion dynamics, and resource extraction efficiency.
    /// </summary>
    [System.Serializable]
    public class Occupation
    {
        // --------------------------------------------------------------------- //
        // Core Identity
        // --------------------------------------------------------------------- //

        /// <summary>Unique identifier of the occupied territory (hex cell or region).</summary>
        [SerializeField] private string territoryId;

        /// <summary>Unique identifier of the occupying nation.</summary>
        [SerializeField] private string occupierId;

        /// <summary>The original owner nation before occupation.</summary>
        [SerializeField] private string originalOwnerId;

        /// <summary>Current administration type.</summary>
        [SerializeField] private OccupationType type;

        // --------------------------------------------------------------------- //
        // Temporal Tracking
        // --------------------------------------------------------------------- //

        /// <summary>The game turn when occupation began.</summary>
        [SerializeField] private int turnOccupied;

        /// <summary>The total number of turns this territory has been occupied.</summary>
        [SerializeField] private int totalTurnsOccupied;

        // --------------------------------------------------------------------- //
        // Occupation Metrics
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Current rebellion risk from 0.0 (docile) to 1.0 (imminent uprising).
        /// Drives rebellion event probability each turn.
        /// </summary>
        [SerializeField] private float rebellionRisk;

        /// <summary>
        /// Administrative efficiency from 0.0 (total chaos) to 1.0 (full control).
        /// Affects resource extraction and unit recruitment.
        /// </summary>
        [SerializeField] private float efficiency;

        /// <summary>
        /// Number of rebellion events that have occurred in this territory.
        /// Used for scaling future rebellion severity.
        /// </summary>
        [SerializeField] private int rebellionCount;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Unique identifier of the occupied territory.</summary>
        public string TerritoryId => territoryId;

        /// <summary>Nation that currently occupies this territory.</summary>
        public string OccupierId => occupierId;

        /// <summary>Nation that originally owned this territory.</summary>
        public string OriginalOwnerId => originalOwnerId;

        /// <summary>Current occupation type (Direct Annexation or Puppet Government).</summary>
        public OccupationType Type => type;

        /// <summary>Game turn when occupation began.</summary>
        public int TurnOccupied => turnOccupied;

        /// <summary>Total turns this territory has been under occupation.</summary>
        public int TotalTurnsOccupied => totalTurnsOccupied;

        /// <summary>Current rebellion risk (0.0 to 1.0).</summary>
        public float RebellionRisk => rebellionRisk;

        /// <summary>Administrative efficiency (0.0 to 1.0).</summary>
        public float Efficiency => efficiency;

        /// <summary>Number of rebellion events in this territory.</summary>
        public int RebellionCount => rebellionCount;

        /// <summary>Resource extraction rate as a fraction (0.0 to 1.0).</summary>
        public float ExtractionRate
        {
            get
            {
                switch (type)
                {
                    case OccupationType.DirectAnnexation:
                        return 0.8f * efficiency;
                    case OccupationType.PuppetGovernment:
                        return 0.4f * efficiency;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>Whether this occupation can provide allied units (puppet governments only).</summary>
        public bool CanProvideUnits => type == OccupationType.PuppetGovernment && efficiency > 0.5f;

        // --------------------------------------------------------------------- //
        // Constructor
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Creates a new Occupation instance.
        /// </summary>
        /// <param name="territoryId">Unique identifier of the territory.</param>
        /// <param name="occupierId">Nation occupying the territory.</param>
        /// <param name="originalOwnerId">Nation that originally owned the territory.</param>
        /// <param name="type">Administration type.</param>
        /// <param name="currentTurn">Current game turn.</param>
        public Occupation(string territoryId, string occupierId, string originalOwnerId,
                          OccupationType type, int currentTurn)
        {
            this.territoryId = territoryId;
            this.occupierId = occupierId;
            this.originalOwnerId = originalOwnerId;
            this.type = type;
            this.turnOccupied = currentTurn;
            this.totalTurnsOccupied = 0;
            this.rebellionCount = 0;

            // Initial metrics based on occupation type
            switch (type)
            {
                case OccupationType.DirectAnnexation:
                    rebellionRisk = 0.6f;  // High initial rebellion
                    efficiency = 0.5f;     // Starts at 50% efficiency
                    break;

                case OccupationType.PuppetGovernment:
                    rebellionRisk = 0.2f;  // Lower initial rebellion
                    efficiency = 0.3f;     // Starts lower, grows over time
                    break;

                default:
                    rebellionRisk = 0f;
                    efficiency = 0f;
                    break;
            }
        }

        /// <summary>Parameterless constructor for serialization.</summary>
        public Occupation() { }

        // --------------------------------------------------------------------- //
        // Internal Methods (called by OccupationManager)
        // --------------------------------------------------------------------- //

        /// <summary>Advances occupation by one turn, updating metrics.</summary>
        internal void AdvanceTurn(int currentTurn)
        {
            totalTurnsOccupied++;

            // Efficiency improves over time (learning curve for administration)
            float maxEfficiency = type == OccupationType.DirectAnnexation ? 0.85f : 0.7f;
            float efficiencyGrowthRate = type == OccupationType.DirectAnnexation ? 0.02f : 0.03f;
            efficiency = Mathf.Min(maxEfficiency, efficiency + efficiencyGrowthRate);

            // Rebellion risk changes based on occupation type and duration
            UpdateRebellionRisk();
        }

        /// <summary>Sets the occupation type and recalibrates metrics.</summary>
        internal void SetOccupationType(OccupationType newType)
        {
            if (type == newType) return;

            OccupationType oldType = type;
            type = newType;

            // Recalibrate metrics for the new type
            switch (newType)
            {
                case OccupationType.DirectAnnexation:
                    rebellionRisk = Mathf.Min(1f, rebellionRisk + 0.3f);
                    efficiency = Mathf.Max(0.3f, efficiency - 0.1f);
                    break;

                case OccupationType.PuppetGovernment:
                    rebellionRisk = Mathf.Max(0f, rebellionRisk - 0.3f);
                    efficiency = Mathf.Max(0.2f, efficiency - 0.15f);
                    break;
            }

            Debug.Log($"[Occupation] Territory '{territoryId}' changed from {oldType} to {newType}.");
        }

        /// <summary>Records a rebellion event and adjusts metrics.</summary>
        internal void RecordRebellion()
        {
            rebellionCount++;
            rebellionRisk = Mathf.Min(1f, rebellionRisk + 0.1f);
            efficiency = Mathf.Max(0.1f, efficiency - 0.15f);
        }

        /// <summary>
        /// Updates rebellion risk based on occupation type, duration, and history.
        /// Direct annexation: rebellion risk stays high and slowly grows.
        /// Puppet government: rebellion risk decreases over time as locals accept governance.
        /// </summary>
        private void UpdateRebellionRisk()
        {
            float baseRisk = type == OccupationType.DirectAnnexation ? 0.5f : 0.15f;

            // Duration factor: long occupations tend to either pacify or radicalize
            float durationFactor;
            if (type == OccupationType.DirectAnnexation)
            {
                // Direct rule gets harder over time
                durationFactor = Mathf.Min(0.3f, totalTurnsOccupied * 0.01f);
            }
            else
            {
                // Puppet governments stabilize over time
                durationFactor = -Mathf.Min(0.2f, totalTurnsOccupied * 0.008f);
            }

            // Rebellion history factor: each past rebellion slightly raises the baseline
            float historyFactor = Mathf.Min(0.3f, rebellionCount * 0.05f);

            // Target rebellion risk
            float targetRisk = Mathf.Clamp(baseRisk + durationFactor + historyFactor, 0f, 1f);

            // Smoothly move toward target (don't jump suddenly)
            rebellionRisk = Mathf.Lerp(rebellionRisk, targetRisk, 0.2f);
            rebellionRisk = Mathf.Clamp(rebellionRisk, 0f, 1f);
        }
    }

    /// <summary>
    /// Delegate for occupation-related events.
    /// </summary>
    public delegate void OccupationEventHandler(string territoryId, string occupierId);

    /// <summary>
    /// Delegate for rebellion events with severity.
    /// </summary>
    public delegate void RebellionEventHandler(string territoryId, string occupierId, float severity);

    /// <summary>
    /// MonoBehaviour that manages all occupied territories in the game.
    /// Handles occupation creation, type transitions, rebellion mechanics,
    /// and per-turn updates of all occupation metrics.
    /// </summary>
    public class OccupationManager : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>Fired when a territory is first occupied.</summary>
        public event OccupationEventHandler OnTerritoryOccupied;

        /// <summary>Fired when a territory is liberated (occupation ends).</summary>
        public event OccupationEventHandler OnTerritoryLiberated;

        /// <summary>Fired when an occupation type is changed.</summary>
        public event Action<string, OccupationType, OccupationType> OnOccupationTypeChanged;

        /// <summary>Fired when a rebellion occurs in an occupied territory.</summary>
        public event RebellionEventHandler OnRebellion;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Rebellion Settings")]
        [Tooltip("Base probability per turn that a rebellion triggers (scaled by rebellion risk).")]
        [SerializeField, Range(0f, 1f)] private float baseRebellionProbability = 0.15f;

        [Tooltip("Minimum rebellion risk required for a rebellion event to be possible.")]
        [SerializeField, Range(0f, 1f)] private float minimumRebellionThreshold = 0.3f;

        [Tooltip("Severity multiplier for rebellions in long-occupied territories.")]
        [SerializeField] private float longOccupationSeverityBonus = 0.1f;

        [Header("State")]
        [Tooltip("Current game turn counter.</param>")]
        [SerializeField] private int currentTurn = 1;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>All active occupations in the game.</summary>
        private readonly List<Occupation> occupations = new List<Occupation>();

        /// <summary>Tracks rebellions that occurred this turn for event aggregation.</summary>
        private readonly List<(string territoryId, string occupierId, float severity)> _pendingRebellions =
            new List<(string, string, float)>();

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Gets all active occupations.</summary>
        public IReadOnlyList<Occupation> Occupations => occupations.AsReadOnly();

        /// <summary>Gets or sets the current game turn.</summary>
        public int CurrentTurn
        {
            get => currentTurn;
            set => currentTurn = Mathf.Max(1, value);
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Occupation Management
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Occupies a territory on behalf of a nation.
        /// If the territory is already occupied, the occupier is replaced.
        /// </summary>
        /// <param name="territoryId">The territory to occupy.</param>
        /// <param name="occupierId">The nation occupying the territory.</param>
        /// <param name="type">Administration type for the occupation.</param>
        /// <param name="originalOwnerId">The nation that originally owned the territory.</param>
        /// <returns>The created or updated Occupation instance.</returns>
        public Occupation OccupyTerritory(string territoryId, string occupierId, OccupationType type, string originalOwnerId = null)
        {
            if (string.IsNullOrEmpty(territoryId) || string.IsNullOrEmpty(occupierId))
            {
                Debug.LogWarning("[OccupationManager] Cannot occupy: null territory or occupier ID.");
                return null;
            }

            if (type == OccupationType.None)
            {
                Debug.LogWarning("[OccupationManager] Cannot occupy with OccupationType.None.");
                return null;
            }

            // Check for existing occupation of this territory
            var existing = GetOccupation(territoryId);
            if (existing != null)
            {
                // Transfer to new occupier
                existing = new Occupation(territoryId, occupierId, originalOwnerId ?? existing.OriginalOwnerId, type, currentTurn);
                occupations.Remove(existing);
            }

            var occupation = existing ?? new Occupation(
                territoryId,
                occupierId,
                originalOwnerId ?? string.Empty,
                type,
                currentTurn
            );

            // If we didn't create a new one above, create it now
            if (!occupations.Contains(occupation))
            {
                occupation = new Occupation(territoryId, occupierId, originalOwnerId ?? string.Empty, type, currentTurn);
                occupations.Add(occupation);
            }

            OnTerritoryOccupied?.Invoke(territoryId, occupierId);
            Debug.Log($"[OccupationManager] Territory '{territoryId}' occupied by '{occupierId}' ({type}).");

            return occupation;
        }

        /// <summary>
        /// Liberates a territory, ending its occupation.
        /// The territory returns to its original owner or becomes neutral.
        /// </summary>
        /// <param name="territoryId">The territory to liberate.</param>
        /// <returns>True if the territory was successfully liberated.</returns>
        public bool LiberateTerritory(string territoryId)
        {
            var occupation = GetOccupation(territoryId);
            if (occupation == null)
            {
                Debug.LogWarning($"[OccupationManager] Territory '{territoryId}' is not occupied.");
                return false;
            }

            string occupierId = occupation.OccupierId;
            occupations.Remove(occupation);

            OnTerritoryLiberated?.Invoke(territoryId, occupierId);
            Debug.Log($"[OccupationManager] Territory '{territoryId}' liberated from '{occupierId}'.");
            return true;
        }

        /// <summary>
        /// Transfers an occupation from one administration type to another.
        /// Recalibrates rebellion risk and efficiency for the new type.
        /// </summary>
        /// <param name="territoryId">The occupied territory.</param>
        /// <param name="newType">The new administration type.</param>
        /// <returns>True if the transfer was successful.</returns>
        public bool TransferOccupation(string territoryId, OccupationType newType)
        {
            if (newType == OccupationType.None)
            {
                Debug.LogWarning("[OccupationManager] Cannot transfer to OccupationType.None. Use LiberateTerritory instead.");
                return false;
            }

            var occupation = GetOccupation(territoryId);
            if (occupation == null)
            {
                Debug.LogWarning($"[OccupationManager] Territory '{territoryId}' is not occupied.");
                return false;
            }

            OccupationType oldType = occupation.Type;
            occupation.SetOccupationType(newType);

            OnOccupationTypeChanged?.Invoke(territoryId, oldType, newType);
            return true;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Turn Processing
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Updates all occupations once per turn.
        /// Advances timers, recalculates rebellion risk, processes rebellion events,
        /// and updates administrative efficiency.
        /// </summary>
        /// <returns>List of territories where rebellions occurred this turn.</returns>
        public List<(string territoryId, string occupierId, float severity)> UpdateOccupations()
        {
            currentTurn++;
            _pendingRebellions.Clear();

            foreach (var occupation in occupations)
            {
                occupation.AdvanceTurn(currentTurn);

                // Check for rebellion event
                ProcessRebellionCheck(occupation);
            }

            // Fire all rebellion events
            foreach (var rebellion in _pendingRebellions)
            {
                OnRebellion?.Invoke(rebellion.territoryId, rebellion.occupierId, rebellion.severity);
            }

            return new List<(string, string, float)>(_pendingRebellions);
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Queries
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets the occupation data for a specific territory.
        /// </summary>
        /// <param name="territoryId">The territory to query.</param>
        /// <returns>The Occupation, or null if the territory is not occupied.</returns>
        public Occupation GetOccupation(string territoryId)
        {
            if (string.IsNullOrEmpty(territoryId)) return null;
            return occupations.Find(o => o.TerritoryId == territoryId);
        }

        /// <summary>
        /// Gets all territories occupied by a specific nation.
        /// </summary>
        /// <param name="occupierId">The occupying nation to query.</param>
        /// <returns>List of occupations by this nation.</returns>
        public List<Occupation> GetOccupationsByNation(string occupierId)
        {
            if (string.IsNullOrEmpty(occupierId)) return new List<Occupation>();
            return occupations.Where(o => o.OccupierId == occupierId).ToList();
        }

        /// <summary>
        /// Gets all territories originally owned by a specific nation that are now occupied.
        /// </summary>
        /// <param name="originalOwnerId">The original owner nation.</param>
        /// <returns>List of occupations of this nation's former territories.</returns>
        public List<Occupation> GetLostTerritories(string originalOwnerId)
        {
            if (string.IsNullOrEmpty(originalOwnerId)) return new List<Occupation>();
            return occupations.Where(o => o.OriginalOwnerId == originalOwnerId).ToList();
        }

        /// <summary>
        /// Calculates the total resource extraction value for an occupying nation
        /// across all their occupied territories.
        /// </summary>
        /// <param name="occupierId">The occupying nation.</param>
        /// <param name="baseProductionValues">Dictionary mapping territory IDs to their base production values.</param>
        /// <returns>Total extracted resource value.</returns>
        public float GetTotalExtraction(string occupierId, Dictionary<string, float> baseProductionValues)
        {
            var nationOccupations = GetOccupationsByNation(occupierId);
            float total = 0f;

            foreach (var occ in nationOccupations)
            {
                if (baseProductionValues != null && baseProductionValues.TryGetValue(occ.TerritoryId, out float baseVal))
                {
                    total += baseVal * occ.ExtractionRate;
                }
            }

            return Mathf.Round(total * 100f) / 100f;
        }

        /// <summary>
        /// Gets the average rebellion risk across all territories occupied by a nation.
        /// </summary>
        /// <param name="occupierId">The occupying nation.</param>
        /// <returns>Average rebellion risk (0.0 to 1.0), or 0 if no occupations.</returns>
        public float GetAverageRebellionRisk(string occupierId)
        {
            var nationOccupations = GetOccupationsByNation(occupierId);
            if (nationOccupations.Count == 0) return 0f;

            return nationOccupations.Average(o => o.RebellionRisk);
        }

        // --------------------------------------------------------------------- //
        // Private Methods
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Checks whether a rebellion should trigger in an occupied territory.
        /// Probability is based on rebellion risk, base probability, and occupation duration.
        /// </summary>
        private void ProcessRebellionCheck(Occupation occupation)
        {
            if (occupation.RebellionRisk < minimumRebellionThreshold)
                return;

            // Rebellion probability = base * risk * duration modifier
            float durationModifier = 1f + (occupation.TotalTurnsOccupied * 0.02f);
            float rebellionChance = baseRebellionProbability * occupation.RebellionRisk * durationModifier;

            // Roll for rebellion
            if (UnityEngine.Random.value < rebellionChance)
            {
                // Calculate severity (0.0 to 1.0)
                float severity = occupation.RebellionRisk;
                if (occupation.TotalTurnsOccupied > 10)
                    severity = Mathf.Min(1f, severity + longOccupationSeverityBonus);

                severity = Mathf.Clamp(severity, 0f, 1f);

                // Record the rebellion
                occupation.RecordRebellion();
                _pendingRebellions.Add((occupation.TerritoryId, occupation.OccupierId, severity));

                Debug.LogWarning($"[OccupationManager] REBELLION in '{occupation.TerritoryId}' " +
                                 $"(occupier: '{occupation.OccupierId}', severity: {severity:F2})");
            }
        }
    }
}
