// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: NationDefinition.cs
// Namespace: IronProtocol.Data
// Description: ScriptableObject for defining nations. Stores national identity,
//              visual theme, starting resources and units, AI personality
//              parameters, and cultural archetype. Create instances via the
//              Assets menu: Right-Click → Create → Iron Protocol → Nation Definition
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.Data
{
    // ========================================================================
    // Nested Value Types
    // ========================================================================

    /// <summary>
    /// Represents a quantity of a specific resource.
    /// Used for starting resources, trade payloads, and income tracking.
    /// </summary>
    [Serializable]
    public struct ResourceAmount
    {
        [Tooltip("Unique identifier of the resource (e.g. 'iron', 'oil', 'uranium').")]
        public string resourceId;

        [Tooltip("Quantity of the resource.")]
        public float amount;

        /// <summary>
        /// Creates a new ResourceAmount.
        /// </summary>
        /// <param name="resourceId">Unique resource identifier.</param>
        /// <param name="amount">Quantity.</param>
        public ResourceAmount(string resourceId, float amount)
        {
            this.resourceId = resourceId;
            this.amount = amount;
        }

        public override string ToString() => $"{resourceId}: {amount:F1}";
    }

    /// <summary>
    /// Defines the type and starting position of a unit at game start.
    /// </summary>
    [Serializable]
    public struct UnitSpawn
    {
        [Tooltip("The type of unit to spawn (e.g. 'Infantry', 'Tank', 'Artillery').")]
        public string unitType;

        [Tooltip("Q coordinate of the spawn hex (axial coordinates).")]
        public int spawnQ;

        [Tooltip("R coordinate of the spawn hex (axial coordinates).")]
        public int spawnR;

        /// <summary>
        /// Creates a new UnitSpawn.
        /// </summary>
        /// <param name="unitType">Unit type identifier.</param>
        /// <param name="q">Hex Q coordinate.</param>
        /// <param name="r">Hex R coordinate.</param>
        public UnitSpawn(string unitType, int q, int r)
        {
            this.unitType = unitType;
            this.spawnQ = q;
            this.spawnR = r;
        }

        /// <summary>Gets the spawn position as a HexCoord.</summary>
        public IronProtocol.HexMap.HexCoord SpawnPosition => new IronProtocol.HexMap.HexCoord { Q = spawnQ, R = spawnR };

        public override string ToString() => $"{unitType} @ ({spawnQ}, {spawnR})";
    }

    // ========================================================================
    // NationDefinition ScriptableObject
    // ========================================================================

    /// <summary>
    /// ScriptableObject that fully defines a playable nation.
    /// Contains national identity, visual theme, starting economy, initial
    /// unit placements, AI personality, and cultural archetype.
    /// <para>
    /// Create instances via: <c>Assets → Create → Iron Protocol → Nation Definition</c>
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "NewNation", menuName = "Iron Protocol/Nation Definition")]
    public class NationDefinition : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------------

        [Header("Identity")]
        [Tooltip("Unique identifier for this nation (no spaces, used as key).")]
        [SerializeField] private string nationId;

        [Tooltip("Display name shown in the UI.")]
        [SerializeField] private string nationName;

        [Tooltip("Color used for this nation's territory, units, and UI elements.")]
        [SerializeField] private Color nationColor = Color.blue;

        /// <summary>Unique identifier for this nation.</summary>
        public string NationId => nationId;

        /// <summary>Display name shown in the UI.</summary>
        public string NationName => nationName;

        /// <summary>Nation's thematic color for territory and unit rendering.</summary>
        public Color NationColor => nationColor;

        // ------------------------------------------------------------------
        // Player Control
        // ------------------------------------------------------------------

        [Header("Player Control")]
        [Tooltip("If true, this nation is controlled by the local player.")]
        [SerializeField] private bool isPlayerControlled = false;

        /// <summary>Whether this nation is controlled by the local player.</summary>
        public bool IsPlayerControlled => isPlayerControlled;

        // ------------------------------------------------------------------
        // Leadership
        // ------------------------------------------------------------------

        [Header("Leadership")]
        [Tooltip("Name of this nation's leader (displayed in diplomacy).")]
        [SerializeField] private string leaderName;

        /// <summary>Name of this nation's leader.</summary>
        public string LeaderName => leaderName;

        // ------------------------------------------------------------------
        // Starting Economy
        // ------------------------------------------------------------------

        [Header("Starting Economy")]
        [Tooltip("Starting treasury balance.")]
        [SerializeField] private float startingTreasury = 1000f;

        [Tooltip("Starting resource stockpiles.")]
        [SerializeField] private List<ResourceAmount> startingResources = new List<ResourceAmount>();

        /// <summary>Starting treasury balance.</summary>
        public float StartingTreasury => startingTreasury;

        /// <summary>Starting resource stockpiles (read-only copy).</summary>
        public IReadOnlyList<ResourceAmount> StartingResources => startingResources.AsReadOnly();

        // ------------------------------------------------------------------
        // Starting Military
        // ------------------------------------------------------------------

        [Header("Starting Military")]
        [Tooltip("Units to spawn at game start with their positions.")]
        [SerializeField] private List<UnitSpawn> startingUnits = new List<UnitSpawn>();

        /// <summary>Units to spawn at game start (read-only copy).</summary>
        public IReadOnlyList<UnitSpawn> StartingUnits => startingUnits.AsReadOnly();

        // ------------------------------------------------------------------
        // Starting Cities
        // ------------------------------------------------------------------

        [Header("Starting Cities")]
        [Tooltip("Names of cities this nation starts with (order defines importance).")]
        [SerializeField] private List<string> startingCityNames = new List<string>();

        [Tooltip("Name of the capital city (must be in startingCityNames).")]
        [SerializeField] private string capitalName;

        /// <summary>Names of cities this nation starts with.</summary>
        public IReadOnlyList<string> StartingCityNames => startingCityNames.AsReadOnly();

        /// <summary>Name of the capital city.</summary>
        public string CapitalName => capitalName;

        // ------------------------------------------------------------------
        // AI Personality
        // ------------------------------------------------------------------

        [Header("AI Personality")]
        [Tooltip("How aggressively the AI pursues military action (0 = pacifist, 1 = warmonger).")]
        [SerializeField] [Range(0f, 1f)] private float aggressionTendency = 0.5f;

        [Tooltip("How much the AI prioritizes economic growth over military (0 = military, 1 = economy).")]
        [SerializeField] [Range(0f, 1f)] private float economicFocus = 0.5f;

        [Tooltip("How willing the AI is to form alliances and maintain peace (0 = isolationist, 1 = diplomat).")]
        [SerializeField] [Range(0f, 1f)] private float diplomacyWillingness = 0.5f;

        /// <summary>Aggression tendency for AI (0 = pacifist, 1 = warmonger).</summary>
        public float AggressionTendency => aggressionTendency;

        /// <summary>Economic focus for AI (0 = military, 1 = economy).</summary>
        public float EconomicFocus => economicFocus;

        /// <summary>Diplomacy willingness for AI (0 = isolationist, 1 = diplomat).</summary>
        public float DiplomacyWillingness => diplomacyWillingness;

        // ------------------------------------------------------------------
        // Culture
        // ------------------------------------------------------------------

        [Header("Culture")]
        [Tooltip("Cultural archetype affecting unit names, city styles, and AI personality modifiers.")]
        [SerializeField] private string cultureType = "Generic";

        /// <summary>Cultural archetype identifier.</summary>
        public string CultureType => cultureType;

        // ------------------------------------------------------------------
        // Lookup Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the starting amount of a specific resource, or 0 if not found.
        /// </summary>
        /// <param name="resourceId">The resource identifier to look up.</param>
        /// <returns>The starting quantity, or 0 if the nation doesn't start with this resource.</returns>
        public float GetStartingResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId) || startingResources == null)
                return 0f;

            for (int i = 0; i < startingResources.Count; i++)
            {
                if (startingResources[i].resourceId == resourceId)
                    return startingResources[i].amount;
            }

            return 0f;
        }

        /// <summary>
        /// Returns all starting units of a specific type.
        /// </summary>
        /// <param name="unitType">The unit type identifier to filter by.</param>
        /// <returns>List of UnitSpawn entries matching the given type.</returns>
        public List<UnitSpawn> GetStartingUnitsOfType(string unitType)
        {
            List<UnitSpawn> result = new List<UnitSpawn>();
            if (startingUnits == null || string.IsNullOrEmpty(unitType))
                return result;

            for (int i = 0; i < startingUnits.Count; i++)
            {
                if (startingUnits[i].unitType == unitType)
                    result.Add(startingUnits[i]);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        private void OnValidate()
        {
            // Ensure nationId has no spaces
            if (!string.IsNullOrEmpty(nationId))
            {
                nationId = nationId.Replace(" ", "_");
            }

            // Ensure positive treasury
            startingTreasury = Mathf.Max(0f, startingTreasury);

            // Ensure starting resources have valid amounts
            if (startingResources != null)
            {
                for (int i = 0; i < startingResources.Count; i++)
                {
                    if (startingResources[i].amount < 0)
                    {
                        Debug.LogWarning($"[NationDefinition] Negative resource amount for '{startingResources[i].resourceId}' in {nationName}. Clamping to 0.");
                        startingResources[i] = new ResourceAmount(startingResources[i].resourceId, 0f);
                    }
                }
            }

            // Warn if capital is not in city list
            if (!string.IsNullOrEmpty(capitalName) && startingCityNames != null && !startingCityNames.Contains(capitalName))
            {
                Debug.LogWarning($"[NationDefinition] Capital '{capitalName}' not found in starting city names for '{nationName}'.");
            }
        }

        /// <summary>
        /// Returns a summary string for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"Nation: {nationName} ({nationId}), " +
                   $"Capital: {capitalName}, " +
                   $"Treasury: {startingTreasury}, " +
                   $"Units: {startingUnits?.Count ?? 0}, " +
                   $"Cities: {startingCityNames?.Count ?? 0}, " +
                   $"AI Aggression: {aggressionTendency:P0}, " +
                   $"Culture: {cultureType}, " +
                   $"Player: {isPlayerControlled}";
        }
    }
}
