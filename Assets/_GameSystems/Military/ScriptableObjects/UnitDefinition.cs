// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: UnitDefinition.cs
// Description: ScriptableObject defining the base stat template for a unit type.
//              Used by the production system, unit spawner, and balance tools to create
//              and configure units consistently across the game.
// =====================================================================================

using UnityEngine;

namespace IronProtocol.Military
{
    /// <summary>
    /// ScriptableObject asset that defines the base statistics and configuration for a unit type.
    /// <para>
    /// UnitDefinitions serve as data-driven templates that the unit factory uses to spawn
    /// new unit instances. Each definition contains all the fundamental stats (attack, defense,
    /// HP, movement, range), production costs, visual assets, and metadata needed to create
    /// a fully configured unit in the game world.
    /// </para>
    /// <para>
    /// <b>Usage:</b> Create new UnitDefinition assets via the Unity editor:
    /// <list type="number">
    ///   <item>Right-click in the Project window.</item>
    ///   <item>Navigate to: Create → Iron Protocol → Military → Unit Definition.</item>
    ///   <item>Configure the unit stats, costs, and visual references in the Inspector.</item>
    ///   <item>Reference the asset in your unit spawner, production queue, or faction config.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Design Notes:</b> Each unit type (Infantry, Armor, Artillery, etc.) should have a
    /// corresponding UnitDefinition asset. Variants (e.g., "Heavy Tank", "Light Tank") can
    /// be created as separate assets with different stat values.
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewUnitDefinition",
        menuName = "Iron Protocol/Military/Unit Definition",
        order = 100)]
    public class UnitDefinition : ScriptableObject
    {
        // =================================================================================
        // Identity & Classification
        // =================================================================================

        [Header("Identity")]
        [Tooltip("The specific unit type classification (Infantry, Armor, Artillery, etc.).")]
        [SerializeField] private UnitType unitType;

        [Tooltip("The operational domain (Ground, Air, Naval, Special).")]
        [SerializeField] private UnitDomain domain;

        [Tooltip("Human-readable display name for this unit (e.g., 'M1 Abrams', 'Rifle Squad').")]
        [SerializeField] private string unitName = "New Unit";

        // =================================================================================
        // Combat Statistics
        // =================================================================================

        [Header("Combat Stats")]
        [Tooltip("Base attack power. Foundation for all damage calculations.")]
        [SerializeField] private float attack = 30f;

        [Tooltip("Base defense value. Reduces incoming damage.")]
        [SerializeField] private float defense = 20f;

        [Tooltip("Maximum hit points. Determines unit survivability.")]
        [SerializeField] private float hp = 100f;

        [Tooltip("Maximum movement points available per turn.")]
        [SerializeField] private float movementPoints = 3f;

        [Tooltip("Maximum attack range in hexes. 1 = melee/adjacent only.")]
        [SerializeField] private float attackRange = 1f;

        // =================================================================================
        // Production
        // =================================================================================

        [Header("Production")]
        [Tooltip("Industrial production cost to build one unit of this type.")]
        [SerializeField] private int productionCost = 100;

        [Tooltip("Number of turns required to produce this unit.")]
        [SerializeField] private int productionTime = 1;

        // =================================================================================
        // Visual Assets
        // =================================================================================

        [Header("Visuals")]
        [Tooltip("Icon sprite used in UI panels (selection, HUD, tooltips, production queue).")]
        [SerializeField] private Sprite icon;

        [Tooltip("Prefab GameObject for the unit's 3D model on the game board.")]
        [SerializeField] private GameObject prefab;

        // =================================================================================
        // Metadata
        // =================================================================================

        [Header("Metadata")]
        [Tooltip("Description text shown in tooltips and the codex.")]
        [TextArea(2, 5)]
        [SerializeField] private string description = "A military unit.";

        [Tooltip("If true, this unit requires a research project to be completed before it can be produced.")]
        [SerializeField] private bool requiresResearch;

        [Tooltip("The research project ID required to unlock this unit (only used if requiresResearch is true).")]
        [SerializeField] private string requiredResearchId;

        // =================================================================================
        // Public Properties
        // =================================================================================

        /// <summary>Gets the unit type classification.</summary>
        public UnitType UnitType => unitType;

        /// <summary>Gets the operational domain.</summary>
        public UnitDomain Domain => domain;

        /// <summary>Gets the display name.</summary>
        public string UnitName => unitName;

        /// <summary>Gets the base attack power.</summary>
        public float Attack => attack;

        /// <summary>Gets the base defense value.</summary>
        public float Defense => defense;

        /// <summary>Gets the maximum hit points.</summary>
        public float HP => hp;

        /// <summary>Gets the maximum movement points per turn.</summary>
        public float MovementPoints => movementPoints;

        /// <summary>Gets the attack range in hexes.</summary>
        public float AttackRange => attackRange;

        /// <summary>Gets the production cost in industrial points.</summary>
        public int ProductionCost => productionCost;

        /// <summary>Gets the number of turns to produce this unit.</summary>
        public int ProductionTime => productionTime;

        /// <summary>Gets the UI icon sprite.</summary>
        public Sprite Icon => icon;

        /// <summary>Gets the 3D model prefab.</summary>
        public GameObject Prefab => prefab;

        /// <summary>Gets the description text.</summary>
        public string Description => description;

        /// <summary>
        /// Gets whether this unit requires research to unlock.
        /// </summary>
        public bool RequiresResearch => requiresResearch;

        /// <summary>
        /// Gets the research project ID required to unlock this unit.
        /// Only meaningful when RequiresResearch is true.
        /// </summary>
        public string RequiredResearchId => requiredResearchId;

        // =================================================================================
        // Validation
        // =================================================================================

        /// <summary>
        /// Unity OnValidate. Called when the ScriptableObject is modified in the inspector.
        /// Ensures all stat values are within valid ranges.
        /// </summary>
        private void OnValidate()
        {
            // Ensure attack is non-negative
            attack = Mathf.Max(0f, attack);

            // Ensure defense is non-negative
            defense = Mathf.Max(0f, defense);

            // Ensure HP is at least 1
            hp = Mathf.Max(1f, hp);

            // Ensure movement points are non-negative
            movementPoints = Mathf.Max(0f, movementPoints);

            // Ensure attack range is at least 1
            attackRange = Mathf.Max(1f, attackRange);

            // Ensure production cost is non-negative
            productionCost = Mathf.Max(0, productionCost);

            // Ensure production time is at least 1
            productionTime = Mathf.Max(1, productionTime);

            // Ensure unit name is not empty
            if (string.IsNullOrWhiteSpace(unitName))
            {
                unitName = "Unnamed Unit";
                Debug.LogWarning($"[UnitDefinition] Unit name was empty. Reset to '{unitName}'.");
            }

            // Ensure description is not empty
            if (string.IsNullOrWhiteSpace(description))
            {
                description = "A military unit.";
            }
        }

        // =================================================================================
        // Utility
        // =================================================================================

        /// <summary>
        /// Returns a formatted summary of this unit definition for debugging or UI display.
        /// </summary>
        /// <returns>A multi-line string containing all unit stats and metadata.</returns>
        public string GetStatsSummary()
        {
            return $"{unitName} ({unitType}, {domain})\n" +
                   $"  ATK: {attack} | DEF: {defense} | HP: {hp}\n" +
                   $"  MOV: {movementPoints} | RNG: {attackRange}\n" +
                   $"  Cost: {productionCost} | Time: {productionTime} turns\n" +
                   $"  Research: {(requiresResearch ? $"Required ({requiredResearchId})" : "None")}\n" +
                   $"  {description}";
        }

        /// <summary>
        /// Checks whether the given research ID satisfies this unit's research requirement.
        /// </summary>
        /// <param name="completedResearchId">The ID of a completed research project.</param>
        /// <returns>True if the research requirement is met (either not required, or the correct ID is provided).</returns>
        public bool IsResearchRequirementMet(string completedResearchId)
        {
            if (!requiresResearch)
            {
                return true;
            }

            return !string.IsNullOrEmpty(completedResearchId) &&
                   completedResearchId == requiredResearchId;
        }

        /// <summary>
        /// Checks whether all required assets (icon, prefab) are assigned.
        /// Useful for pre-flight validation before runtime unit spawning.
        /// </summary>
        /// <returns>True if all visual assets are assigned, false otherwise.</returns>
        public bool HasAllAssets()
        {
            bool hasIcon = icon != null;
            bool hasPrefab = prefab != null;

            if (!hasIcon)
            {
                Debug.LogWarning($"[UnitDefinition] '{unitName}' is missing an icon sprite.");
            }

            if (!hasPrefab)
            {
                Debug.LogWarning($"[UnitDefinition] '{unitName}' is missing a prefab reference.");
            }

            return hasIcon && hasPrefab;
        }
    }
}
