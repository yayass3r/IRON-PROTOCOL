// =====================================================================================
// Iron Protocol - Tech Tree Node Data Definitions
// =====================================================================================
// Defines all data structures, enumerations, and serializable classes used by the
// research and technology tree system. TechTreeNode represents a single unlockable
// technology with prerequisites, effects, and unlock lists.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.ResearchTech
{
    /// <summary>
    /// Represents the primary research branch a technology belongs to.
    /// Each branch emphasizes a different strategic axis of the game.
    /// </summary>
    public enum TechBranch
    {
        /// <summary>Technologies focused on combat power, unit stats, and weapons systems.</summary>
        Military,

        /// <summary>Technologies focused on resource generation, trade, and economic efficiency.</summary>
        Economic,

        /// <summary>Technologies focused on hacking, electronic warfare, and AI systems.</summary>
        Cyber
    }

    /// <summary>
    /// Represents the research tier (depth) of a technology within its branch.
    /// Higher tiers require more research points and typically have stronger prerequisites.
    /// </summary>
    public enum TechTier
    {
        /// <summary>Entry-level technology, inexpensive and broadly applicable.</summary>
        Tier1,

        /// <summary>Mid-early technology requiring at least one Tier 1 prerequisite.</summary>
        Tier2,

        /// <summary>Mid-level technology representing significant advancement.</summary>
        Tier3,

        /// <summary>Late-game technology with substantial prerequisites.</summary>
        Tier4,

        /// <summary>End-game technology representing the pinnacle of research in a branch.</summary>
        Tier5
    }

    /// <summary>
    /// Represents a single effect produced by researching a technology.
    /// Each <see cref="TechTreeNode"/> may have zero or more effects that are applied
    /// when the technology is fully researched.
    /// </summary>
    [Serializable]
    public class TechEffect
    {
        [Tooltip("The category of effect this technology provides.")]
        [SerializeField]
        private string effectType;

        [Tooltip("The ID of the target (unit, building, etc.) this effect applies to. Empty for global effects.")]
        [SerializeField]
        private string targetId;

        [Tooltip("The numerical value of the effect (e.g., 0.1 for +10%, or 1 for +1 range).")]
        [SerializeField]
        private float value;

        [Tooltip("Human-readable description of this specific effect.")]
        [SerializeField]
        private string description;

        /// <summary>
        /// Gets or sets the type of effect (e.g., "unitStatBoost", "unlockBuilding").
        /// </summary>
        public string EffectType
        {
            get => effectType;
            set => effectType = value;
        }

        /// <summary>
        /// Gets or sets the ID of the target entity this effect applies to.
        /// May be <c>null</c> or empty for global effects.
        /// </summary>
        public string TargetId
        {
            get => targetId;
            set => targetId = value;
        }

        /// <summary>
        /// Gets or sets the numerical magnitude of the effect.
        /// Interpretation depends on <see cref="EffectType"/> (e.g., 0.10 = +10% bonus).
        /// </summary>
        public float Value
        {
            get => value;
            set => this.value = value;
        }

        /// <summary>
        /// Gets or sets a human-readable description of what this effect does.
        /// </summary>
        public string Description
        {
            get => description;
            set => this.description = value;
        }

        /// <summary>
        /// Returns a formatted string summarizing this effect for display purposes.
        /// </summary>
        public override string ToString()
        {
            return $"[{effectType}] {targetId ?? "Global"}: +{value:P0} - {description}";
        }
    }

    /// <summary>
    /// Represents a single node in the technology tree. Each node is a researchable
    /// technology with an associated cost, prerequisite chain, and list of effects
    /// and unlocks.
    /// </summary>
    [Serializable]
    public class TechTreeNode
    {
        [Tooltip("Unique string identifier for this technology (e.g., 'mil_t1_advanced_rifles').")]
        [SerializeField]
        private string techId;

        [Tooltip("Display name shown in the tech tree UI.")]
        [SerializeField]
        private string techName;

        [Tooltip("The branch this technology belongs to (Military, Economic, or Cyber).")]
        [SerializeField]
        private TechBranch branch;

        [Tooltip("The tier/depth of this technology within its branch.")]
        [SerializeField]
        private TechTier tier;

        [Tooltip("Total research points required to complete this technology.")]
        [SerializeField]
        private int researchCost;

        [Tooltip("Current accumulated research progress toward completion.")]
        [SerializeField]
        private int researchProgress;

        [Tooltip("Whether this technology has been fully researched.")]
        [SerializeField]
        private bool isResearched;

        [Tooltip("Whether this technology is currently being actively researched.")]
        [SerializeField]
        private bool isResearching;

        [Tooltip("Lore / gameplay description shown in the tech tree tooltip.")]
        [SerializeField]
        private string description;

        [Tooltip("Tech ID of the prerequisite technology. Leave null or empty for no prerequisite.")]
        [SerializeField]
        private string prerequisiteTechId;

        [Tooltip("List of unit IDs that this technology unlocks for production.")]
        [SerializeField]
        private List<string> unlocksUnitIds;

        [Tooltip("List of ability IDs that this technology grants.")]
        [SerializeField]
        private List<string> unlocksAbilityIds;

        [Tooltip("List of effects applied when this technology is completed.")]
        [SerializeField]
        private List<TechEffect> effects;

        [Tooltip("Icon displayed in the tech tree UI for this technology.")]
        [SerializeField]
        private Sprite icon;

        [Tooltip("Number of game turns required to research this technology.")]
        [SerializeField]
        private int turnsToResearch;

        /// <summary>
        /// Gets or sets the unique string identifier for this technology.
        /// </summary>
        public string TechId
        {
            get => techId;
            set => techId = value;
        }

        /// <summary>
        /// Gets or sets the human-readable display name.
        /// </summary>
        public string TechName
        {
            get => techName;
            set => techName = value;
        }

        /// <summary>
        /// Gets or sets the research branch this technology belongs to.
        /// </summary>
        public TechBranch Branch
        {
            get => branch;
            set => branch = value;
        }

        /// <summary>
        /// Gets or sets the tier level of this technology within its branch.
        /// </summary>
        public TechTier Tier
        {
            get => tier;
            set => this.tier = value;
        }

        /// <summary>
        /// Gets or sets the total research points required to complete this technology.
        /// </summary>
        public int ResearchCost
        {
            get => researchCost;
            set => researchCost = Mathf.Max(0, value);
        }

        /// <summary>
        /// Gets or sets the current research progress (0 to <see cref="ResearchCost"/>).
        /// </summary>
        public int ResearchProgress
        {
            get => researchProgress;
            set => researchProgress = Mathf.Clamp(value, 0, researchCost);
        }

        /// <summary>
        /// Gets or sets whether this technology has been fully researched.
        /// </summary>
        public bool IsResearched
        {
            get => isResearched;
            set => isResearched = value;
        }

        /// <summary>
        /// Gets or sets whether this technology is currently being researched.
        /// </summary>
        public bool IsResearching
        {
            get => isResearching;
            set => isResearching = value;
        }

        /// <summary>
        /// Gets or sets the lore / gameplay description.
        /// </summary>
        public string Description
        {
            get => description;
            set => this.description = value;
        }

        /// <summary>
        /// Gets or sets the prerequisite technology ID.
        /// A value of <c>null</c> or empty means this tech has no prerequisite.
        /// </summary>
        public string PrerequisiteTechId
        {
            get => prerequisiteTechId;
            set => prerequisiteTechId = value;
        }

        /// <summary>
        /// Gets or sets the list of unit IDs unlocked by this technology.
        /// </summary>
        public List<string> UnlocksUnitIds
        {
            get => unlocksUnitIds ??= new List<string>();
            set => unlocksUnitIds = value;
        }

        /// <summary>
        /// Gets or sets the list of ability IDs unlocked by this technology.
        /// </summary>
        public List<string> UnlocksAbilityIds
        {
            get => unlocksAbilityIds ??= new List<string>();
            set => unlocksAbilityIds = value;
        }

        /// <summary>
        /// Gets or sets the list of effects applied when this technology completes.
        /// </summary>
        public List<TechEffect> Effects
        {
            get => effects ??= new List<TechEffect>();
            set => effects = value;
        }

        /// <summary>
        /// Gets or sets the icon sprite displayed in the tech tree UI.
        /// </summary>
        public Sprite Icon
        {
            get => icon;
            set => icon = value;
        }

        /// <summary>
        /// Gets or sets the number of game turns needed to research this technology.
        /// </summary>
        public int TurnsToResearch
        {
            get => turnsToResearch;
            set => turnsToResearch = Mathf.Max(1, value);
        }

        /// <summary>
        /// Returns <c>true</c> if this technology has no prerequisite or its prerequisite
        /// is in the list of already-researched tech IDs.
        /// </summary>
        /// <param name="researchedTechIds">Set of tech IDs the nation has already researched.</param>
        /// <returns><c>true</c> if the prerequisite condition is satisfied.</returns>
        public bool IsPrerequisiteMet(HashSet<string> researchedTechIds)
        {
            if (string.IsNullOrEmpty(prerequisiteTechId))
                return true;

            return researchedTechIds != null && researchedTechIds.Contains(prerequisiteTechId);
        }

        /// <summary>
        /// Calculates research completion as a percentage (0-100).
        /// </summary>
        /// <returns>Integer percentage of research completion.</returns>
        public int GetProgressPercent()
        {
            if (researchCost <= 0) return 100;
            return Mathf.RoundToInt((float)researchProgress / researchCost * 100f);
        }

        /// <summary>
        /// Creates a deep copy of this tech node for per-nation tracking.
        /// Research state fields are reset to initial values.
        /// </summary>
        /// <returns>A new <see cref="TechTreeNode"/> with the same definition but fresh research state.</returns>
        public TechTreeNode CloneDefinition()
        {
            return new TechTreeNode
            {
                techId = techId,
                techName = techName,
                branch = branch,
                tier = tier,
                researchCost = researchCost,
                researchProgress = 0,
                isResearched = false,
                isResearching = false,
                description = description,
                prerequisiteTechId = prerequisiteTechId,
                unlocksUnitIds = unlocksUnitIds != null ? new List<string>(unlocksUnitIds) : new List<string>(),
                unlocksAbilityIds = unlocksAbilityIds != null ? new List<string>(unlocksAbilityIds) : new List<string>(),
                effects = effects != null ? new List<TechEffect>(effects) : new List<TechEffect>(),
                icon = icon,
                turnsToResearch = turnsToResearch
            };
        }

        /// <summary>
        /// Returns a formatted summary string for debugging and logging.
        /// </summary>
        public override string ToString()
        {
            return $"[{branch}/{tier}] {techName} ({techId}) - Cost: {researchCost}, Progress: {GetProgressPercent()}%";
        }
    }

    /// <summary>
    /// Event arguments raised when a technology research is completed.
    /// Contains the completed tech node and the nation that completed it.
    /// </summary>
    public class ResearchCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the ID of the nation that completed the research.
        /// </summary>
        public string NationId { get; }

        /// <summary>
        /// Gets the technology node that was completed.
        /// </summary>
        public TechTreeNode CompletedTech { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResearchCompleteEventArgs"/> class.
        /// </summary>
        /// <param name="nationId">The ID of the researching nation.</param>
        /// <param name="completedTech">The completed tech node.</param>
        public ResearchCompleteEventArgs(string nationId, TechTreeNode completedTech)
        {
            NationId = nationId;
            CompletedTech = completedTech;
        }
    }

    /// <summary>
    /// Event arguments raised when a new research project is started.
    /// </summary>
    public class ResearchStartedEventArgs : EventArgs
    {
        /// <summary>Gets the ID of the nation that started the research.</summary>
        public string NationId { get; }

        /// <summary>Gets the technology node being researched.</summary>
        public TechTreeNode TechNode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResearchStartedEventArgs"/> class.
        /// </summary>
        /// <param name="nationId">The nation starting the research.</param>
        /// <param name="techNode">The tech being researched.</param>
        public ResearchStartedEventArgs(string nationId, TechTreeNode techNode)
        {
            NationId = nationId;
            TechNode = techNode;
        }
    }
}
