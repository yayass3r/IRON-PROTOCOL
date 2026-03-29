// =====================================================================================
// Iron Protocol - Tech Tree Management System
// =====================================================================================
// Manages the complete technology tree including initialization, per-nation research
// tracking, prerequisite validation, and event dispatching. Contains all 15 predefined
// technologies across Military, Economic, and Cyber branches.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.ResearchTech
{
    /// <summary>
    /// Central management system for the technology tree. Handles per-nation research
    /// tracking, prerequisite validation, research progression, and event dispatching.
    /// Initialize by calling <see cref="InitializeDefaultTechTree"/> on first use.
    /// </summary>
    public class TechTree : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────────
        //  Inspector Fields
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Tech Tree Data")]
        [Tooltip("Master list of all technology definitions in the game.")]
        [SerializeField]
        private List<TechTreeNode> allTechs = new List<TechTreeNode>();

        [Tooltip("Maximum number of concurrent research projects per nation.")]
        [SerializeField]
        private int maxConcurrentResearch = 1;

        [Tooltip("Global research speed multiplier applied to all nations.")]
        [SerializeField]
        private float globalResearchSpeedMultiplier = 1.0f;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Private State
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Fast lookup dictionary mapping techId to tech node definition.</summary>
        private Dictionary<string, TechTreeNode> _techLookup;

        /// <summary>Per-nation set of completed tech IDs.</summary>
        private Dictionary<string, HashSet<string>> _nationResearchedTechs;

        /// <summary>Per-nation set of tech IDs currently being researched.</summary>
        private Dictionary<string, HashSet<string>> _nationCurrentResearch;

        /// <summary>Per-nation dictionary of research progress (techId -> accumulated points).</summary>
        private Dictionary<string, Dictionary<string, int>> _nationResearchProgress;

        /// <summary>Whether the tech tree has been initialized.</summary>
        private bool _isInitialized;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when a nation completes researching a technology.
        /// Subscribers receive the nation ID and the completed tech node.
        /// </summary>
        public event EventHandler<ResearchCompleteEventArgs> OnResearchComplete;

        /// <summary>
        /// Fired when a nation begins researching a technology.
        /// </summary>
        public event EventHandler<ResearchStartedEventArgs> OnResearchStarted;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Unity Awake. Initializes internal data structures and the default tech tree.
        /// </summary>
        private void Awake()
        {
            InitializeInternalDictionaries();
            InitializeDefaultTechTree();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public Properties
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Gets the total number of technologies defined in the tree.</summary>
        public int TechCount => allTechs?.Count ?? 0;

        /// <summary>Gets all technology definitions as a read-only list.</summary>
        public IReadOnlyList<TechTreeNode> AllTechs => allTechs.AsReadOnly();

        // ──────────────────────────────────────────────────────────────────────────────
        //  Initialization
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes internal tracking dictionaries for nations.
        /// Called automatically on Awake.
        /// </summary>
        private void InitializeInternalDictionaries()
        {
            _techLookup = new Dictionary<string, TechTreeNode>();
            _nationResearchedTechs = new Dictionary<string, HashSet<string>>();
            _nationCurrentResearch = new Dictionary<string, HashSet<string>>();
            _nationResearchProgress = new Dictionary<string, Dictionary<string, int>>();
        }

        /// <summary>
        /// Builds the lookup dictionary from the current allTechs list.
        /// </summary>
        private void RebuildLookup()
        {
            _techLookup.Clear();
            if (allTechs == null) return;

            foreach (var tech in allTechs)
            {
                if (tech != null && !string.IsNullOrEmpty(tech.TechId))
                {
                    _techLookup[tech.TechId] = tech;
                }
            }
        }

        /// <summary>
        /// Registers a new nation for research tracking. Must be called for each nation
        /// at game start before any research operations.
        /// </summary>
        /// <param name="nationId">The unique identifier of the nation to register.</param>
        public void RegisterNation(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[TechTree] Cannot register nation with null or empty ID.");
                return;
            }

            if (!_nationResearchedTechs.ContainsKey(nationId))
                _nationResearchedTechs[nationId] = new HashSet<string>();

            if (!_nationCurrentResearch.ContainsKey(nationId))
                _nationCurrentResearch[nationId] = new HashSet<string>();

            if (!_nationResearchProgress.ContainsKey(nationId))
                _nationResearchProgress[nationId] = new Dictionary<string, int>();

            Debug.Log($"[TechTree] Registered nation: {nationId}");
        }

        /// <summary>
        /// Initializes the tech tree with all 15 predefined technologies across the three branches.
        /// Clears any existing tech definitions before populating.
        /// </summary>
        public void InitializeDefaultTechTree()
        {
            allTechs?.Clear();
            allTechs = new List<TechTreeNode>();

            // ══════════════════════════════════════════════════════════════════════════
            //  MILITARY BRANCH (5 tiers, 9 technologies)
            // ══════════════════════════════════════════════════════════════════════════

            // ── Tier 1 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "mil_t1_advanced_rifles",
                techName: "Advanced Rifles",
                branch: TechBranch.Military,
                tier: TechTier.Tier1,
                cost: 100,
                turns: 2,
                description: "Improved firearms technology boosts infantry combat effectiveness.",
                prereq: null,
                effects: new List<TechEffect>
                {
                    CreateEffect("unitStatBoost", "infantry", 0.10f, "Infantry ATK +10%")
                });

            AddTech(
                techId: "mil_t1_improved_armor",
                techName: "Improved Armor",
                branch: TechBranch.Military,
                tier: TechTier.Tier1,
                cost: 120,
                turns: 2,
                description: "Composite armor plating significantly increases vehicle survivability.",
                prereq: null,
                effects: new List<TechEffect>
                {
                    CreateEffect("defenseBonus", "armor", 0.20f, "Armor HP +20%")
                });

            // ── Tier 2 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "mil_t2_precision_artillery",
                techName: "Precision Artillery",
                branch: TechBranch.Military,
                tier: TechTier.Tier2,
                cost: 250,
                turns: 3,
                description: "GPS-guided munitions extend artillery engagement range.",
                prereq: "mil_t1_advanced_rifles",
                effects: new List<TechEffect>
                {
                    CreateEffect("unitStatBoost", "artillery", 1.0f, "Artillery Range +1")
                });

            AddTech(
                techId: "mil_t2_sam_defense",
                techName: "SAM Defense",
                branch: TechBranch.Military,
                tier: TechTier.Tier2,
                cost: 280,
                turns: 3,
                description: "Surface-to-Air Missile systems provide critical air defense coverage.",
                prereq: "mil_t1_improved_armor",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockBuilding", "sam_site", 0f, "Unlock Anti-Air Building")
                });

            // ── Tier 3 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "mil_t3_stealth",
                techName: "Stealth Technology",
                branch: TechBranch.Military,
                tier: TechTier.Tier3,
                cost: 500,
                turns: 4,
                description: "Radar-absorbent materials and low-observable design dramatically increase evasion.",
                prereq: "mil_t2_precision_artillery",
                effects: new List<TechEffect>
                {
                    CreateEffect("unitStatBoost", "stealth", 0.50f, "Stealth Units +50% Evasion")
                });

            AddTech(
                techId: "mil_t3_railgun",
                techName: "Railgun Research",
                branch: TechBranch.Military,
                tier: TechTier.Tier3,
                cost: 550,
                turns: 4,
                description: "Electromagnetic projectile acceleration enables devastating direct-fire weapons.",
                prereq: "mil_t2_sam_defense",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockUnit", "railgun", 0f, "Unlock Railgun Unit")
                });

            // ── Tier 4 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "mil_t4_emp_shield",
                techName: "EMP Shield",
                branch: TechBranch.Military,
                tier: TechTier.Tier4,
                cost: 800,
                turns: 5,
                description: "Faraday cage technology renders military assets immune to electromagnetic pulse attacks.",
                prereq: "mil_t3_stealth",
                effects: new List<TechEffect>
                {
                    CreateEffect("cyberBonus", "emp_shield", 1.0f, "Immune to Cyber EMP Attacks")
                });

            AddTech(
                techId: "mil_t4_drone_swarm",
                techName: "Drone Swarm",
                branch: TechBranch.Military,
                tier: TechTier.Tier4,
                cost: 850,
                turns: 5,
                description: "Autonomous drone swarms overwhelm enemy defenses through sheer numbers.",
                prereq: "mil_t3_railgun",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockUnit", "advanced_drone", 0f, "Unlock Advanced Drone Swarm")
                });

            // ── Tier 5 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "mil_t5_laser",
                techName: "Directed Energy Weapon",
                branch: TechBranch.Military,
                tier: TechTier.Tier5,
                cost: 1500,
                turns: 8,
                description: "High-energy laser systems provide instantaneous precision strikes.",
                prereq: "mil_t4_emp_shield",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockBuilding", "laser_defense", 0f, "Unlock Laser Defense System")
                });

            // ══════════════════════════════════════════════════════════════════════════
            //  ECONOMIC BRANCH (5 tiers, 9 technologies)
            // ══════════════════════════════════════════════════════════════════════════

            // ── Tier 1 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "eco_t1_mining",
                techName: "Advanced Mining",
                branch: TechBranch.Economic,
                tier: TechTier.Tier1,
                cost: 80,
                turns: 2,
                description: "Automated extraction techniques increase resource yields from mines.",
                prereq: null,
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "resource_production", 0.25f, "Resource Production +25%")
                });

            AddTech(
                techId: "eco_t1_trade",
                techName: "Free Trade",
                branch: TechBranch.Economic,
                tier: TechTier.Tier1,
                cost: 90,
                turns: 2,
                description: "Reduced trade barriers and optimized logistics lower market transaction costs.",
                prereq: null,
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "market_fees", 0.50f, "Market Fees -50%")
                });

            // ── Tier 2 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "eco_t2_automation",
                techName: "Industrial Automation",
                branch: TechBranch.Economic,
                tier: TechTier.Tier2,
                cost: 200,
                turns: 3,
                description: "Robotic assembly lines dramatically increase factory output.",
                prereq: "eco_t1_mining",
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "production_speed", 0.40f, "Production Speed +40%")
                });

            AddTech(
                techId: "eco_t2_currency",
                techName: "Currency Markets",
                branch: TechBranch.Economic,
                tier: TechTier.Tier2,
                cost: 220,
                turns: 3,
                description: "A unified alliance currency stabilizes trade and enables shared treasuries.",
                prereq: "eco_t1_trade",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockAbility", "alliance_currency", 0f, "Unlock Alliance Currency System")
                });

            // ── Tier 3 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "eco_t3_rare_earth",
                techName: "Rare Earth Processing",
                branch: TechBranch.Economic,
                tier: TechTier.Tier3,
                cost: 450,
                turns: 4,
                description: "Advanced refinement techniques extract maximum value from rare earth deposits.",
                prereq: "eco_t2_automation",
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "rare_earth_yield", 0.50f, "Rare Earth Yield +50%")
                });

            AddTech(
                techId: "eco_t3_quantum_comp",
                techName: "Quantum Computing",
                branch: TechBranch.Economic,
                tier: TechTier.Tier3,
                cost: 480,
                turns: 4,
                description: "Quantum processors revolutionize code-breaking and cryptographic research.",
                prereq: "eco_t2_currency",
                effects: new List<TechEffect>
                {
                    CreateEffect("cyberBonus", "cyber_attack", 0.30f, "Cyber Attack +30%")
                });

            // ── Tier 4 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "eco_t4_space",
                techName: "Space Economy",
                branch: TechBranch.Economic,
                tier: TechTier.Tier4,
                cost: 750,
                turns: 5,
                description: "Orbital mining and satellite infrastructure open new revenue streams.",
                prereq: "eco_t3_rare_earth",
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "space_income", 0f, "Unlock Space Income")
                });

            AddTech(
                techId: "eco_t4_trade_net",
                techName: "Global Trade Network",
                branch: TechBranch.Economic,
                tier: TechTier.Tier4,
                cost: 780,
                turns: 5,
                description: "A worldwide logistics network boosts all trade income across the board.",
                prereq: "eco_t3_quantum_comp",
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "all_trade", 0.30f, "All Trade +30%")
                });

            // ── Tier 5 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "eco_t5_post_scarcity",
                techName: "Post-Scarcity Economy",
                branch: TechBranch.Economic,
                tier: TechTier.Tier5,
                cost: 1400,
                turns: 8,
                description: "Near-infinite production capabilities eliminate resource scarcity constraints.",
                prereq: "eco_t4_space",
                effects: new List<TechEffect>
                {
                    CreateEffect("economicBonus", "all_costs", 0.25f, "All Resource Costs -25%")
                });

            // ══════════════════════════════════════════════════════════════════════════
            //  CYBER BRANCH (5 tiers, 9 technologies)
            // ══════════════════════════════════════════════════════════════════════════

            // ── Tier 1 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "cyb_t1_security",
                techName: "Network Security",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier1,
                cost: 90,
                turns: 2,
                description: "Firewall hardening and intrusion detection improve cyber defense capabilities.",
                prereq: null,
                effects: new List<TechEffect>
                {
                    CreateEffect("cyberBonus", "cyber_defense", 0.20f, "Cyber Defense +20%")
                });

            AddTech(
                techId: "cyb_t1_hacking",
                techName: "Basic Hacking",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier1,
                cost: 100,
                turns: 2,
                description: "Offensive cyber tools enable digital infiltration of enemy systems.",
                prereq: null,
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockUnit", "cyber_unit", 0f, "Unlock Cyber Unit")
                });

            // ── Tier 2 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "cyb_t2_ewarfare",
                techName: "Electronic Warfare",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier2,
                cost: 230,
                turns: 3,
                description: "Broad-spectrum jamming disrupts enemy radar and communications.",
                prereq: "cyb_t1_security",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockAbility", "disable_radar", 0f, "Disable Enemy Radar")
                });

            AddTech(
                techId: "cyb_t2_ai_command",
                techName: "AI-Assisted Command",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier2,
                cost: 250,
                turns: 3,
                description: "Machine-learning algorithms optimize tactical decision-making in real time.",
                prereq: "cyb_t1_hacking",
                effects: new List<TechEffect>
                {
                    CreateEffect("unitStatBoost", "all_units", 0.05f, "All Units +5% Combat")
                });

            // ── Tier 3 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "cyb_t3_encryption",
                techName: "Advanced Encryption",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier3,
                cost: 460,
                turns: 4,
                description: "Military-grade encryption renders networks impervious to basic cyber attacks.",
                prereq: "cyb_t2_ewarfare",
                effects: new List<TechEffect>
                {
                    CreateEffect("cyberBonus", "basic_cyber_immune", 1.0f, "Immune to Basic Cyber Attacks")
                });

            AddTech(
                techId: "cyb_t3_cyber_off2",
                techName: "Cyber Offense II",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier3,
                cost: 500,
                turns: 4,
                description: "Next-generation exploit frameworks multiply offensive cyber capabilities.",
                prereq: "cyb_t2_ai_command",
                effects: new List<TechEffect>
                {
                    CreateEffect("cyberBonus", "cyber_attack", 0.40f, "Cyber Attack +40%")
                });

            // ── Tier 4 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "cyb_t4_quantum_enc",
                techName: "Quantum Encryption",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier4,
                cost: 780,
                turns: 5,
                description: "Quantum key distribution provides theoretically unbreakable communications security.",
                prereq: "cyb_t3_encryption",
                effects: new List<TechEffect>
                {
                    CreateEffect("cyberBonus", "all_cyber_immune", 1.0f, "Immune to All Cyber Attacks")
                });

            AddTech(
                techId: "cyb_t4_autonomous",
                techName: "Autonomous Weapons",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier4,
                cost: 820,
                turns: 5,
                description: "AI-driven fire control systems enable autonomous target acquisition and engagement.",
                prereq: "cyb_t3_cyber_off2",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockAbility", "auto_target", 0f, "Units Auto-Target Enemies")
                });

            // ── Tier 5 ─────────────────────────────────────────────────────────────
            AddTech(
                techId: "cyb_t5_singularity",
                techName: "Singularity Protocol",
                branch: TechBranch.Cyber,
                tier: TechTier.Tier5,
                cost: 1600,
                turns: 8,
                description: "Artificial superintelligence unlocks unparalleled strategic and operational bonuses.",
                prereq: "cyb_t4_quantum_enc",
                effects: new List<TechEffect>
                {
                    CreateEffect("unlockAbility", "super_ai", 0f, "Unlock Super AI Bonuses")
                });

            RebuildLookup();
            _isInitialized = true;

            Debug.Log($"[TechTree] Initialized with {allTechs.Count} technologies across 3 branches.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Query Methods
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all technologies that a nation is eligible to research. A tech is available
        /// if: (1) its prerequisite is met, (2) it has not been researched, and (3) it is not
        /// already being researched.
        /// </summary>
        /// <param name="nationId">The ID of the nation to query.</param>
        /// <param name="researchedTechs">Optional explicit list of researched tech IDs. If null, uses internal tracking.</param>
        /// <returns>List of tech nodes available for research.</returns>
        public List<TechTreeNode> GetAvailableTechs(string nationId, List<string> researchedTechs = null)
        {
            var result = new List<TechTreeNode>();
            var completedSet = GetResearchedSet(nationId, researchedTechs);
            var currentSet = GetCurrentResearchSet(nationId);

            foreach (var tech in allTechs)
            {
                if (tech == null) continue;
                if (completedSet.Contains(tech.TechId)) continue;
                if (currentSet.Contains(tech.TechId)) continue;
                if (!tech.IsPrerequisiteMet(completedSet)) continue;

                result.Add(tech);
            }

            return result;
        }

        /// <summary>
        /// Determines whether a specific technology can be researched by a given nation.
        /// </summary>
        /// <param name="nationId">The ID of the nation attempting to research.</param>
        /// <param name="techId">The ID of the technology to check.</param>
        /// <returns><c>true</c> if the tech exists, prerequisites are met, and it's not already done.</returns>
        public bool CanResearch(string nationId, string techId)
        {
            if (!_techLookup.TryGetValue(techId, out var tech))
            {
                Debug.LogWarning($"[TechTree] Unknown tech ID: {techId}");
                return false;
            }

            var completedSet = GetResearchedSet(nationId);
            var currentSet = GetCurrentResearchSet(nationId);

            if (completedSet.Contains(techId)) return false;
            if (currentSet.Contains(techId)) return false;
            if (!tech.IsPrerequisiteMet(completedSet)) return false;

            return true;
        }

        /// <summary>
        /// Retrieves a technology definition by its unique ID.
        /// </summary>
        /// <param name="techId">The technology ID to look up.</param>
        /// <returns>The matching <see cref="TechTreeNode"/>, or <c>null</c> if not found.</returns>
        public TechTreeNode GetTech(string techId)
        {
            _techLookup.TryGetValue(techId, out var tech);
            return tech;
        }

        /// <summary>
        /// Gets the research progress percentage (0-100) for a specific tech and nation.
        /// </summary>
        /// <param name="nationId">The nation ID.</param>
        /// <param name="techId">The technology ID.</param>
        /// <returns>Integer percentage of completion, or 0 if not being researched.</returns>
        public int GetResearchProgressPercent(string nationId, string techId)
        {
            if (!_nationResearchProgress.TryGetValue(nationId, out var progressDict))
                return 0;

            if (!progressDict.TryGetValue(techId, out var progress))
                return 0;

            var tech = GetTech(techId);
            if (tech == null) return 0;

            return Mathf.RoundToInt((float)progress / tech.ResearchCost * 100f);
        }

        /// <summary>
        /// Gets all completed tech IDs for a specific nation.
        /// </summary>
        /// <param name="nationId">The nation ID.</param>
        /// <returns>Dictionary mapping tech ID to completion status (always 1 for completed).</returns>
        public Dictionary<string, int> GetResearchedTechs(string nationId)
        {
            var result = new Dictionary<string, int>();
            if (_nationResearchedTechs.TryGetValue(nationId, out var set))
            {
                foreach (var techId in set)
                    result[techId] = 1;
            }
            return result;
        }

        /// <summary>
        /// Gets all techs in a specific branch.
        /// </summary>
        /// <param name="branch">The branch to filter by.</param>
        /// <returns>List of tech nodes in the specified branch.</returns>
        public List<TechTreeNode> GetTechsByBranch(TechBranch branch)
        {
            return allTechs.Where(t => t != null && t.Branch == branch).ToList();
        }

        /// <summary>
        /// Gets all techs at a specific tier.
        /// </summary>
        /// <param name="tier">The tier to filter by.</param>
        /// <returns>List of tech nodes at the specified tier.</returns>
        public List<TechTreeNode> GetTechsByTier(TechTier tier)
        {
            return allTechs.Where(t => t != null && t.Tier == tier).ToList();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Research Actions
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Begins researching a technology for a specified nation.
        /// The nation must have the prerequisite tech and not already be researching this tech.
        /// </summary>
        /// <param name="nationId">The nation starting the research.</param>
        /// <param name="techId">The technology ID to research.</param>
        /// <param name="researchPointsPerTurn">Initial research points to allocate (can be 0).</param>
        /// <returns><c>true</c> if research was successfully started.</returns>
        public bool StartResearch(string nationId, string techId, int researchPointsPerTurn = 0)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(techId))
            {
                Debug.LogWarning("[TechTree] Nation ID and Tech ID must not be null or empty.");
                return false;
            }

            if (!CanResearch(nationId, techId))
            {
                Debug.LogWarning($"[TechTree] Nation '{nationId}' cannot research '{techId}' (prerequisite not met, already done, or already researching).");
                return false;
            }

            var currentSet = GetCurrentResearchSet(nationId);
            if (currentSet.Count >= maxConcurrentResearch)
            {
                Debug.LogWarning($"[TechTree] Nation '{nationId}' already at max concurrent research ({maxConcurrentResearch}).");
                return false;
            }

            // Begin research
            currentSet.Add(techId);
            var progressDict = _nationResearchProgress[nationId];
            progressDict[techId] = 0;

            var tech = GetTech(techId);
            tech.IsResearching = true;

            Debug.Log($"[TechTree] Nation '{nationId}' started researching '{tech?.TechName ?? techId}'.");

            // Apply any initial research points
            if (researchPointsPerTurn > 0)
            {
                AdvanceResearch(nationId, researchPointsPerTurn);
            }

            // Fire event
            OnResearchStarted?.Invoke(this, new ResearchStartedEventArgs(nationId, tech));
            return true;
        }

        /// <summary>
        /// Advances all currently-researching technologies for a nation by the given amount
        /// of research points. Technologies that reach their cost are automatically completed.
        /// </summary>
        /// <param name="nationId">The nation whose research is advancing.</param>
        /// <param name="researchPoints">Number of research points to distribute.</param>
        public void AdvanceResearch(string nationId, int researchPoints)
        {
            if (string.IsNullOrEmpty(nationId) || researchPoints <= 0) return;

            if (!_nationCurrentResearch.TryGetValue(nationId, out var currentSet))
            {
                Debug.LogWarning($"[TechTree] Nation '{nationId}' is not registered.");
                return;
            }

            var progressDict = _nationResearchProgress[nationId];
            var adjustedPoints = Mathf.RoundToInt(researchPoints * globalResearchSpeedMultiplier);

            // Create a copy since CompleteResearch modifies the set
            var researchingIds = new List<string>(currentSet);

            foreach (var techId in researchingIds)
            {
                var tech = GetTech(techId);
                if (tech == null) continue;

                if (!progressDict.ContainsKey(techId))
                    progressDict[techId] = 0;

                progressDict[techId] += adjustedPoints;
                tech.ResearchProgress = progressDict[techId];

                Debug.Log($"[TechTree] '{nationId}' research on '{tech.TechName}': " +
                          $"{progressDict[techId]}/{tech.ResearchCost} ({tech.GetProgressPercent()}%)");

                // Check for completion
                if (progressDict[techId] >= tech.ResearchCost)
                {
                    CompleteResearch(nationId, techId);
                }
            }
        }

        /// <summary>
        /// Marks a technology as fully researched for a nation, applies its effects,
        /// removes it from the active research list, and fires the completion event.
        /// </summary>
        /// <param name="nationId">The nation completing the research.</param>
        /// <param name="techId">The technology ID being completed.</param>
        public void CompleteResearch(string nationId, string techId)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(techId)) return;

            var tech = GetTech(techId);
            if (tech == null)
            {
                Debug.LogError($"[TechTree] Cannot complete unknown tech: {techId}");
                return;
            }

            // Update tracking state
            var completedSet = _nationResearchedTechs[nationId];
            var currentSet = _nationCurrentResearch[nationId];
            var progressDict = _nationResearchProgress[nationId];

            completedSet.Add(techId);
            currentSet.Remove(techId);
            progressDict.Remove(techId);

            tech.IsResearched = true;
            tech.IsResearching = false;
            tech.ResearchProgress = tech.ResearchCost;

            // Apply effects
            ApplyTechEffects(nationId, tech);

            Debug.Log($"[TechTree] Nation '{nationId}' completed research: '{tech.TechName}'!");

            // Fire completion event
            OnResearchComplete?.Invoke(this, new ResearchCompleteEventArgs(nationId, tech));
        }

        /// <summary>
        /// Cancels an in-progress research project, refunding no points.
        /// </summary>
        /// <param name="nationId">The nation cancelling research.</param>
        /// <param name="techId">The technology ID to cancel.</param>
        /// <returns><c>true</c> if research was successfully cancelled.</returns>
        public bool CancelResearch(string nationId, string techId)
        {
            if (!_nationCurrentResearch.TryGetValue(nationId, out var currentSet))
                return false;

            if (!currentSet.Contains(techId))
                return false;

            currentSet.Remove(techId);
            _nationResearchProgress[nationId].Remove(techId);

            var tech = GetTech(techId);
            if (tech != null)
            {
                tech.IsResearching = false;
                tech.ResearchProgress = 0;
            }

            Debug.Log($"[TechTree] Nation '{nationId}' cancelled research on '{tech?.TechName ?? techId}'.");
            return true;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Effect Application
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies all effects of a completed technology to the nation.
        /// Logs each applied effect. Actual stat modification should be integrated with
        /// the stat/buff system via the event system.
        /// </summary>
        /// <param name="nationId">The nation receiving the effects.</param>
        /// <param name="tech">The completed technology node.</param>
        private void ApplyTechEffects(string nationId, TechTreeNode tech)
        {
            if (tech.Effects == null) return;

            foreach (var effect in tech.Effects)
            {
                switch (effect.EffectType?.ToLowerInvariant())
                {
                    case "unitstatboost":
                        Debug.Log($"[TechTree] Applied stat boost: {effect.TargetId} +{effect.Value:P0} for nation '{nationId}'.");
                        break;

                    case "unlockbuilding":
                        Debug.Log($"[TechTree] Unlocked building '{effect.TargetId}' for nation '{nationId}'.");
                        break;

                    case "economicbonus":
                        Debug.Log($"[TechTree] Applied economic bonus: {effect.TargetId} = {effect.Value:P0} for nation '{nationId}'.");
                        break;

                    case "defensebonus":
                        Debug.Log($"[TechTree] Applied defense bonus: {effect.TargetId} +{effect.Value:P0} for nation '{nationId}'.");
                        break;

                    case "unlockability":
                        Debug.Log($"[TechTree] Unlocked ability '{effect.TargetId}' for nation '{nationId}'.");
                        break;

                    case "unlockunit":
                        Debug.Log($"[TechTree] Unlocked unit '{effect.TargetId}' via tech '{tech.TechId}'.");
                        break;

                    case "cyberbonus":
                        Debug.Log($"[TechTree] Applied cyber bonus: {effect.TargetId} = {effect.Value} for nation '{nationId}'.");
                        break;

                    default:
                        Debug.LogWarning($"[TechTree] Unknown effect type '{effect.EffectType}' for tech '{tech.TechId}'.");
                        break;
                }
            }

            // Log unlocked units and abilities
            if (tech.UnlocksUnitIds != null && tech.UnlocksUnitIds.Count > 0)
            {
                foreach (var unitId in tech.UnlocksUnitIds)
                    Debug.Log($"[TechTree] Unlocked unit '{unitId}' via tech '{tech.TechId}'.");
            }

            if (tech.UnlocksAbilityIds != null && tech.UnlocksAbilityIds.Count > 0)
            {
                foreach (var abilityId in tech.UnlocksAbilityIds)
                    Debug.Log($"[TechTree] Unlocked ability '{abilityId}' via tech '{tech.TechId}'.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Reset / Serialization
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resets all per-nation research state. Does not modify the tech definitions.
        /// Useful when starting a new game.
        /// </summary>
        public void ResetAllResearch()
        {
            _nationResearchedTechs.Clear();
            _nationCurrentResearch.Clear();
            _nationResearchProgress.Clear();

            foreach (var tech in allTechs)
            {
                if (tech != null)
                {
                    tech.IsResearched = false;
                    tech.IsResearching = false;
                    tech.ResearchProgress = 0;
                }
            }

            Debug.Log("[TechTree] All research state has been reset.");
        }

        /// <summary>
        /// Resets research state for a single nation.
        /// </summary>
        /// <param name="nationId">The nation to reset.</param>
        public void ResetNationResearch(string nationId)
        {
            if (_nationResearchedTechs.ContainsKey(nationId))
                _nationResearchedTechs[nationId].Clear();

            if (_nationCurrentResearch.ContainsKey(nationId))
            {
                foreach (var techId in _nationCurrentResearch[nationId])
                {
                    var tech = GetTech(techId);
                    if (tech != null)
                    {
                        tech.IsResearching = false;
                        tech.ResearchProgress = 0;
                    }
                }
                _nationCurrentResearch[nationId].Clear();
            }

            if (_nationResearchProgress.ContainsKey(nationId))
                _nationResearchProgress[nationId].Clear();

            Debug.Log($"[TechTree] Research state reset for nation '{nationId}'.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Private Helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a <see cref="TechEffect"/> with the specified parameters.
        /// </summary>
        private static TechEffect CreateEffect(string effectType, string targetId, float value, string description)
        {
            return new TechEffect
            {
                EffectType = effectType,
                TargetId = targetId,
                Value = value,
                Description = description
            };
        }

        /// <summary>
        /// Adds a technology to the master list and the lookup dictionary.
        /// </summary>
        private void AddTech(string techId, string techName, TechBranch branch, TechTier tier,
            int cost, int turns, string description, string prereq,
            List<string> unitIds = null, List<string> abilityIds = null, List<TechEffect> effects = null)
        {
            var node = new TechTreeNode
            {
                TechId = techId,
                TechName = techName,
                Branch = branch,
                Tier = tier,
                ResearchCost = cost,
                TurnsToResearch = turns,
                Description = description,
                PrerequisiteTechId = prereq,
                UnlocksUnitIds = unitIds ?? new List<string>(),
                UnlocksAbilityIds = abilityIds ?? new List<string>(),
                Effects = effects ?? new List<TechEffect>()
            };

            allTechs.Add(node);
        }

        /// <summary>
        /// Gets the completed tech set for a nation, using the explicit list if provided.
        /// </summary>
        private HashSet<string> GetResearchedSet(string nationId, List<string> explicitList = null)
        {
            if (explicitList != null)
                return new HashSet<string>(explicitList);

            if (_nationResearchedTechs.TryGetValue(nationId, out var set))
                return set;

            return new HashSet<string>();
        }

        /// <summary>
        /// Gets the current research set for a nation, initializing if needed.
        /// </summary>
        private HashSet<string> GetCurrentResearchSet(string nationId)
        {
            if (_nationCurrentResearch.TryGetValue(nationId, out var set))
                return set;

            var newSet = new HashSet<string>();
            _nationCurrentResearch[nationId] = newSet;
            return newSet;
        }
    }
}
