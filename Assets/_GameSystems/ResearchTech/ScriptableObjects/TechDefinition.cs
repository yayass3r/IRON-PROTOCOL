// =====================================================================================
// Iron Protocol - Tech Definition ScriptableObject
// =====================================================================================
// ScriptableObject asset for defining individual technology data outside of code.
// Create new instances via the Unity menu: Assets > Create > Iron Protocol > Tech Definition.
// Tech Definitions are loaded at runtime and converted into TechTreeNode instances
// for use by the TechTree system.
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.ResearchTech
{
    /// <summary>
    /// ScriptableObject definition for a single technology in the tech tree.
    /// Each definition contains all data needed to instantiate a <see cref="TechTreeNode"/>
    /// at runtime. Use <see cref="CreateAssetMenuAttribute"/> to create new instances
    /// from the Unity editor.
    /// </summary>
    [CreateAssetMenu(menuName = "Iron Protocol/Tech Definition", fileName = "NewTechDefinition")]
    public class TechDefinition : ScriptableObject
    {
        // ──────────────────────────────────────────────────────────────────────────────
        //  Core Identity
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Unique string identifier for this technology (e.g., 'mil_t1_advanced_rifles'). Must be unique across all techs.")]
        [SerializeField]
        private string techId;

        [Tooltip("Display name shown in the tech tree UI.")]
        [SerializeField]
        private string techName;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Classification
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Classification")]
        [Tooltip("The research branch this technology belongs to.")]
        [SerializeField]
        private TechBranch branch;

        [Tooltip("The tier/depth within the branch (Tier 1 = basic, Tier 5 = advanced).")]
        [SerializeField]
        private TechTier tier;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Research Cost
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Research Cost")]
        [Tooltip("Total research points required to complete this technology.")]
        [SerializeField]
        private int researchCost = 100;

        [Tooltip("Minimum number of game turns required to research this technology.")]
        [SerializeField]
        private int turnsToResearch = 2;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Prerequisites
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Prerequisites")]
        [Tooltip("Tech ID of the prerequisite technology. Leave empty for no prerequisite.")]
        [SerializeField]
        private string prerequisiteTechId;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Effects & Unlocks
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Effects")]
        [Tooltip("List of effects applied when this technology is completed.")]
        [SerializeField]
        private List<TechEffect> effects = new List<TechEffect>();

        [Header("Unlocks")]
        [Tooltip("List of unit IDs that this technology unlocks for production.")]
        [SerializeField]
        private List<string> unlocksUnitIds = new List<string>();

        [Tooltip("List of ability IDs that this technology grants to the nation.")]
        [SerializeField]
        private List<string> unlocksAbilityIds = new List<string>();

        // ──────────────────────────────────────────────────────────────────────────────
        //  Display
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Display")]
        [Tooltip("Lore / gameplay description shown in the tech tree tooltip.")]
        [TextArea(2, 5)]
        [SerializeField]
        private string description;

        [Tooltip("Icon displayed in the tech tree UI for this technology.")]
        [SerializeField]
        private Sprite icon;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Editor Validation
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Unity OnValidate callback. Validates the data integrity of this tech definition.
        /// Logs warnings for common configuration errors.
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(techId))
            {
                Debug.LogWarning($"[TechDefinition] Tech '{name}' has no techId assigned.", this);
            }

            if (string.IsNullOrWhiteSpace(techName))
            {
                Debug.LogWarning($"[TechDefinition] Tech '{techId ?? "UNNAMED"}' has no display name.", this);
            }

            if (researchCost < 0)
            {
                Debug.LogWarning($"[TechDefinition] Tech '{techId}' has negative research cost. Clamping to 0.", this);
                researchCost = 0;
            }

            if (turnsToResearch < 1)
            {
                Debug.LogWarning($"[TechDefinition] Tech '{techId}' has turnsToResearch < 1. Clamping to 1.", this);
                turnsToResearch = 1;
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public Properties
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Gets the unique string identifier for this technology.</summary>
        public string TechId => techId;

        /// <summary>Gets the display name.</summary>
        public string TechName => techName;

        /// <summary>Gets the research branch.</summary>
        public TechBranch Branch => branch;

        /// <summary>Gets the tier level.</summary>
        public TechTier Tier => tier;

        /// <summary>Gets the research point cost.</summary>
        public string PrerequisiteTechId => prerequisiteTechId;

        /// <summary>Gets the research point cost.</summary>
        public int ResearchCost => researchCost;

        /// <summary>Gets the number of turns to research.</summary>
        public int TurnsToResearch => turnsToResearch;

        /// <summary>Gets the list of effects.</summary>
        public IReadOnlyList<TechEffect> Effects => effects;

        /// <summary>Gets the list of unlocked unit IDs.</summary>
        public IReadOnlyList<string> UnlocksUnitIds => unlocksUnitIds;

        /// <summary>Gets the list of unlocked ability IDs.</summary>
        public IReadOnlyList<string> UnlocksAbilityIds => unlocksAbilityIds;

        /// <summary>Gets the description text.</summary>
        public string Description => description;

        /// <summary>Gets the icon sprite.</summary>
        public Sprite Icon => icon;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Conversion
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new <see cref="TechTreeNode"/> from this ScriptableObject definition.
        /// The returned node has a fresh research state (not started, not completed).
        /// </summary>
        /// <returns>A new TechTreeNode populated with this definition's data.</returns>
        public TechTreeNode ToTechTreeNode()
        {
            return new TechTreeNode
            {
                TechId = techId,
                TechName = techName,
                Branch = branch,
                Tier = tier,
                ResearchCost = researchCost,
                TurnsToResearch = turnsToResearch,
                Description = description,
                PrerequisiteTechId = prerequisiteTechId,
                Icon = icon,
                Effects = effects != null ? new List<TechEffect>(effects) : new List<TechEffect>(),
                UnlocksUnitIds = unlocksUnitIds != null ? new List<string>(unlocksUnitIds) : new List<string>(),
                UnlocksAbilityIds = unlocksAbilityIds != null ? new List<string>(unlocksAbilityIds) : new List<string>(),
                ResearchProgress = 0,
                IsResearched = false,
                IsResearching = false
            };
        }

        /// <summary>
        /// Converts a list of TechDefinition ScriptableObjects into a list of TechTreeNodes.
        /// Useful for batch-loading tech definitions from an asset folder.
        /// </summary>
        /// <param name="definitions">The definitions to convert.</param>
        /// <returns>List of TechTreeNodes with fresh research state.</returns>
        public static List<TechTreeNode> ConvertAll(List<TechDefinition> definitions)
        {
            var nodes = new List<TechTreeNode>();
            if (definitions == null) return nodes;

            foreach (var def in definitions)
            {
                if (def != null)
                    nodes.Add(def.ToTechTreeNode());
            }

            return nodes;
        }
    }
}
