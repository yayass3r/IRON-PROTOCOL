// =============================================================================
// IRON PROTOCOL - Advanced Alliance Manager
// File: AdvancedAllianceManager.cs
// Description: Comprehensive alliance management system supporting military pacts,
//              economic unions, research partnerships, defense coalitions, and full
//              federations with shared assets and joint operations.
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
    /// Defines the type and depth of an alliance, determining which features
    /// are available to member nations.
    /// </summary>
    public enum AllianceType
    {
        /// <summary>Mutual military defense pact. Members defend each other when attacked.</summary>
        MilitaryPact,

        /// <summary>Shared economic zone with reduced trade barriers and potential shared currency.</summary>
        EconomicUnion,

        /// <summary>Pooled research effort accelerating technology development.</summary>
        ResearchPartnership,

        /// <summary>Regional defense coalition with integrated command structure.</summary>
        DefenseCoalition,

        /// <summary>Full political federation with shared military, economy, and governance.</summary>
        FullFederation
    }

    /// <summary>
    /// Types of shared assets that alliances can establish and operate.
    /// </summary>
    public enum SharedAssetType
    {
        /// <summary>Military bases hosted by one member for use by all.</summary>
        MilitaryBases,

        /// <summary>Intelligence sharing network among member nations.</summary>
        Intelligence,

        /// <summary>Joint research laboratories accelerating tech development.</summary>
        ResearchFacilities,

        /// <summary>Shared supply depots for logistics coordination.</summary>
        SupplyDepots,

        /// <summary>Integrated missile defense network covering member territories.</summary>
        MissileDefense
    }

    // =========================================================================
    // DATA CLASSES
    // =========================================================================

    /// <summary>
    /// Represents a formal alliance between nations with shared resources,
    /// objectives, and obligations.
    /// </summary>
    [Serializable]
    public class Alliance
    {
        /// <summary>Unique identifier for this alliance.</summary>
        public string allianceId { get; set; }

        /// <summary>Display name of the alliance (e.g., "NATO", "EU").</summary>
        public string allianceName { get; set; }

        /// <summary>The type and depth of this alliance.</summary>
        public AllianceType type { get; set; }

        /// <summary>List of member nation IDs.</summary>
        public List<string> memberNations { get; set; } = new List<string>();

        /// <summary>The leading nation that initiated the alliance.</summary>
        public string leaderNation { get; set; }

        /// <summary>Currency ID for economic union members (empty if not applicable).</summary>
        public string sharedCurrencyId { get; set; }

        /// <summary>Combined alliance treasury in economic units.</summary>
        public float treasury { get; set; }

        /// <summary>Portion of treasury allocated to military operations.</summary>
        public float militaryBudget { get; set; }

        /// <summary>Whether the alliance is currently active.</summary>
        public bool isActive { get; set; }

        /// <summary>List of shared asset types available to the alliance.</summary>
        public List<SharedAssetType> sharedAssets { get; set; } = new List<SharedAssetType>();

        /// <summary>Game turn when this alliance was founded.</summary>
        public int foundingTurn { get; set; }

        /// <summary>
        /// Creates a new alliance with specified parameters.
        /// </summary>
        public Alliance(string allianceId, string allianceName, AllianceType type,
            string leaderNation, int foundingTurn)
        {
            this.allianceId = allianceId;
            this.allianceName = allianceName;
            this.type = type;
            this.leaderNation = leaderNation;
            this.foundingTurn = foundingTurn;
            this.treasury = 0f;
            this.militaryBudget = 0f;
            this.isActive = true;
            this.memberNations = new List<string> { leaderNation };
            this.sharedAssets = new List<SharedAssetType>();
        }
    }

    /// <summary>
    /// Represents an action taken within an alliance, such as shared defense,
    /// economic aid, or sanctions against an external nation.
    /// </summary>
    [Serializable]
    public class AllianceAction
    {
        /// <summary>
        /// Types of actions that can be performed within an alliance.
        /// </summary>
        public enum ActionType
        {
            /// <summary>Activate mutual defense clause in response to an attack.</summary>
            SharedDefense,

            /// <summary>Pool research resources toward a specific technology.</summary>
            JointResearch,

            /// <summary>Transfer economic aid from one member to another.</summary>
            EconomicAid,

            /// <summary>Transfer military units or resources to a member.</summary>
            MilitaryAid,

            /// <summary>Impose trade sanctions on an external nation.</summary>
            Sanctions,

            /// <summary>Remove a member nation from the alliance.</summary>
            Expulsion,

            /// <summary>Invite a non-member nation to join the alliance.</invitation>
            InviteMember
        }

        /// <summary>The type of alliance action.</summary>
        public ActionType type { get; set; }

        /// <summary>The nation initiating the action.</summary>
        public string initiatorNation { get; set; }

        /// <summary>The target nation (member or external, depending on action type).</summary>
        public string targetNation { get; set; }

        /// <summary>Numeric value associated with the action (e.g., aid amount).</summary>
        public float value { get; set; }

        /// <summary>Human-readable description of the action.</summary>
        public string description { get; set; }

        /// <summary>
        /// Creates a new alliance action.
        /// </summary>
        public AllianceAction(ActionType type, string initiatorNation, string targetNation,
            float value = 0f, string description = "")
        {
            this.type = type;
            this.initiatorNation = initiatorNation;
            this.targetNation = targetNation;
            this.value = value;
            this.description = description;
        }
    }

    /// <summary>
    /// Simple intel report structure for intelligence sharing between alliance members.
    /// </summary>
    [Serializable]
    public class IntelReport
    {
        /// <summary>Unique report identifier.</summary>
        public string reportId { get; set; }

        /// <summary>Nation that generated the intelligence.</summary>
        public string sourceNation { get; set; }

        /// <summary>Subject nation being observed.</summary>
        public string subjectNation { get; set; }

        /// <summary>Type of intelligence gathered.</summary>
        public string intelType { get; set; }

        /// <summary>Detailed intelligence content.</summary>
        public string content { get; set; }

        /// <summary>Confidence level of the intelligence (0-100).</summary>
        public float confidence { get; set; }

        /// <summary>Turn when this intelligence was gathered.</summary>
        public int turnGathered { get; set; }

        /// <summary>
        /// Creates a new intel report.
        /// </summary>
        public IntelReport(string reportId, string sourceNation, string subjectNation,
            string intelType, string content, float confidence, int turnGathered)
        {
            this.reportId = reportId;
            this.sourceNation = sourceNation;
            this.subjectNation = subjectNation;
            this.intelType = intelType;
            this.content = content;
            this.confidence = Mathf.Clamp(confidence, 0f, 100f);
            this.turnGathered = turnGathered;
        }
    }

    // =========================================================================
    // ADVANCED ALLIANCE MANAGER (MonoBehaviour)
    // =========================================================================

    /// <summary>
    /// Core system managing advanced alliance mechanics including formation,
    /// membership, shared assets, joint operations, and inter-alliance diplomacy.
    /// <para>
    /// Attach to a persistent GameObject. Works in conjunction with the base
    /// diplomacy system to provide deep alliance functionality.
    /// </para>
    /// </summary>
    public class AdvancedAllianceManager : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // EVENTS
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired when an action is performed within an alliance.
        /// Arguments: (allianceId, initiatorNation, AllianceAction).
        /// </summary>
        public event Action<string, string, AllianceAction> OnAllianceAction;

        /// <summary>
        /// Fired when a new alliance is formed.
        /// Arguments: (allianceId).
        /// </summary>
        public event Action<string> OnAllianceFormed;

        /// <summary>
        /// Fired when an alliance is dissolved.
        /// Arguments: (allianceId).
        /// </summary>
        public event Action<string> OnAllianceDissolved;

        /// <summary>
        /// Fired when a new member is invited to an alliance.
        /// Arguments: (allianceId, candidateNationId).
        /// </summary>
        public event Action<string, string> OnMemberInvited;

        /// <summary>
        /// Fired when a member is expelled from an alliance.
        /// Arguments: (allianceId, expelledNationId).
        /// </summary>
        public event Action<string, string> OnMemberExpelled;

        /// <summary>
        /// Fired when shared defense is activated.
        /// Arguments: (allianceId, attackerNationId).
        /// </summary>
        public event Action<string, string> OnSharedDefenseActivated;

        // -----------------------------------------------------------------
        // SERIALIZED FIELDS
        // -----------------------------------------------------------------

        [Header("Alliance Settings")]
        [Tooltip("Minimum number of founding members for an alliance.")]
        [SerializeField] private int minFoundingMembers = 2;

        [Tooltip("Treasury contribution rate (% of GDP) for alliance members.")]
        [SerializeField] private float treasuryContributionRate = 0.02f;

        [Tooltip("Percentage of alliance treasury allocated to military budget.")]
        [SerializeField] private float militaryBudgetPercentage = 0.4f;

        [Tooltip("Diplomatic penalty for dissolving an alliance.")]
        [SerializeField] private float dissolutionDiplomaticPenalty = 20f;

        [Tooltip("Economic bonus for trade between alliance members (%).")]
        [SerializeField] private float internalTradeBonus = 15f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // -----------------------------------------------------------------
        // PRIVATE STATE
        // -----------------------------------------------------------------

        private Dictionary<string, Alliance> _alliances = new Dictionary<string, Alliance>();
        private Dictionary<string, List<string>> _nationToAllianceMap = new Dictionary<string, List<string>>();
        private Dictionary<string, Dictionary<string, float>> _nationContributions = new Dictionary<string, Dictionary<string, float>>();
        private Dictionary<string, List<IntelReport>> _sharedIntel = new Dictionary<string, List<IntelReport>>();
        private Dictionary<string, List<string>> _sanctionedNations = new Dictionary<string, List<string>>();
        private int _allianceIdCounter = 0;
        private int _intelReportCounter = 0;
        private int _currentTurn = 0;

        // -----------------------------------------------------------------
        // PUBLIC PROPERTIES
        // -----------------------------------------------------------------

        /// <summary>Current game turn.</summary>
        public int CurrentTurn
        {
            get => _currentTurn;
            set => _currentTurn = value;
        }

        /// <summary>Number of active alliances.</summary>
        public int AllianceCount => _alliances.Count(a => a.Value.isActive);

        /// <summary>Read-only access to all alliances.</summary>
        public IReadOnlyDictionary<string, Alliance> Alliances => _alliances;

        // =================================================================
        // ALLIANCE CREATION AND MANAGEMENT
        // =================================================================

        /// <summary>
        /// Creates a new alliance with the specified parameters.
        /// The leader nation is automatically added as the first member.
        /// Additional members are validated and added if they meet requirements.
        /// </summary>
        /// <param name="name">Display name for the alliance.</param>
        /// <param name="type">Type of alliance to create.</param>
        /// <param name="leader">Nation ID of the founding/leading nation.</param>
        /// <param name="members">List of initial member nation IDs (including leader).</param>
        /// <returns>The newly created <see cref="Alliance"/>, or null if creation failed.</returns>
        public Alliance CreateAlliance(string name, AllianceType type, string leader, List<string> members)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[AllianceManager] CreateAlliance: alliance name is required.");
                return null;
            }

            if (string.IsNullOrEmpty(leader))
            {
                Debug.LogWarning("[AllianceManager] CreateAlliance: leader nation is required.");
                return null;
            }

            if (members == null || members.Count < minFoundingMembers)
            {
                Debug.LogWarning($"[AllianceManager] CreateAlliance: minimum {minFoundingMembers} founding members required " +
                                $"(got {members?.Count ?? 0}).");
                return null;
            }

            // Validate no member is already in an alliance of the same type
            foreach (var memberId in members)
            {
                var existingAlliance = GetAllianceOfNation(memberId);
                if (existingAlliance != null && existingAlliance.type == type)
                {
                    Debug.LogWarning($"[AllianceManager] Nation '{memberId}' is already in a {type} alliance.");
                    return null;
                }
            }

            string allianceId = $"alliance_{++_allianceIdCounter}_{name.ToLower().Replace(' ', '_')}";
            var alliance = new Alliance(allianceId, name, type, leader, _currentTurn);

            // Add all members
            foreach (var memberId in members)
            {
                if (!alliance.memberNations.Contains(memberId))
                {
                    alliance.memberNations.Add(memberId);
                }
            }

            // Set up default shared assets based on alliance type
            ConfigureDefaultSharedAssets(alliance);

            _alliances[allianceId] = alliance;

            // Update nation-to-alliance mapping
            foreach (var memberId in alliance.memberNations)
            {
                AddNationAllianceMapping(memberId, allianceId);
            }

            // Initialize contribution tracking
            _nationContributions[allianceId] = new Dictionary<string, float>();
            foreach (var memberId in alliance.memberNations)
            {
                _nationContributions[allianceId][memberId] = 0f;
            }

            // Initialize shared intel pool
            _sharedIntel[allianceId] = new List<IntelReport>();

            // Initialize sanctions list
            _sanctionedNations[allianceId] = new List<string>();

            OnAllianceFormed?.Invoke(allianceId);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] Created alliance '{name}' (ID: {allianceId}, " +
                          $"type={type}, leader={leader}, members={alliance.memberNations.Count}).");
            }

            return alliance;
        }

        /// <summary>
        /// Configures default shared assets based on the alliance type.
        /// </summary>
        /// <param name="alliance">The alliance to configure.</param>
        private void ConfigureDefaultSharedAssets(Alliance alliance)
        {
            alliance.sharedAssets.Clear();

            switch (alliance.type)
            {
                case AllianceType.MilitaryPact:
                    alliance.sharedAssets.Add(SharedAssetType.Intelligence);
                    alliance.sharedAssets.Add(SharedAssetType.SupplyDepots);
                    break;

                case AllianceType.EconomicUnion:
                    alliance.sharedAssets.Add(SharedAssetType.SupplyDepots);
                    break;

                case AllianceType.ResearchPartnership:
                    alliance.sharedAssets.Add(SharedAssetType.ResearchFacilities);
                    alliance.sharedAssets.Add(SharedAssetType.Intelligence);
                    break;

                case AllianceType.DefenseCoalition:
                    alliance.sharedAssets.Add(SharedAssetType.MilitaryBases);
                    alliance.sharedAssets.Add(SharedAssetType.Intelligence);
                    alliance.sharedAssets.Add(SharedAssetType.SupplyDepots);
                    alliance.sharedAssets.Add(SharedAssetType.MissileDefense);
                    break;

                case AllianceType.FullFederation:
                    alliance.sharedAssets.Add(SharedAssetType.MilitaryBases);
                    alliance.sharedAssets.Add(SharedAssetType.Intelligence);
                    alliance.sharedAssets.Add(SharedAssetType.ResearchFacilities);
                    alliance.sharedAssets.Add(SharedAssetType.SupplyDepots);
                    alliance.sharedAssets.Add(SharedAssetType.MissileDefense);
                    break;
            }
        }

        /// <summary>
        /// Invites a non-member nation to join an alliance.
        /// The candidate must not already be a member of a conflicting alliance.
        /// </summary>
        /// <param name="allianceId">The alliance to invite to.</param>
        /// <param name="inviter">The nation sending the invitation.</param>
        /// <param name="candidate">The nation being invited.</param>
        /// <returns>True if the invitation was successfully sent.</returns>
        public bool InviteMember(string allianceId, string inviter, string candidate)
        {
            if (string.IsNullOrEmpty(allianceId) || !_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] InviteMember: alliance not found.");
                return false;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] InviteMember: alliance is not active.");
                return false;
            }

            if (!alliance.memberNations.Contains(inviter))
            {
                Debug.LogWarning($"[AllianceManager] InviteMember: '{inviter}' is not a member of this alliance.");
                return false;
            }

            if (string.IsNullOrEmpty(candidate) || alliance.memberNations.Contains(candidate))
            {
                Debug.LogWarning("[AllianceManager] InviteMember: invalid or already a member.");
                return false;
            }

            // Check for conflicting alliance membership
            var existingAlliance = GetAllianceOfNation(candidate);
            if (existingAlliance != null)
            {
                Debug.LogWarning($"[AllianceManager] '{candidate}' is already in alliance '{existingAlliance.allianceName}'.");
                return false;
            }

            // Add the candidate to the alliance
            alliance.memberNations.Add(candidate);
            AddNationAllianceMapping(candidate, allianceId);
            _nationContributions[allianceId][candidate] = 0f;

            var action = new AllianceAction(
                AllianceAction.ActionType.InviteMember,
                inviter, candidate, 0f,
                $"'{candidate}' invited to join '{alliance.allianceName}'."
            );

            OnAllianceAction?.Invoke(allianceId, inviter, action);
            OnMemberInvited?.Invoke(allianceId, candidate);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{candidate}' invited to '{alliance.allianceName}' by '{inviter}'. " +
                          $"Total members: {alliance.memberNations.Count}.");
            }

            return true;
        }

        /// <summary>
        /// Removes a member nation from an alliance. Only the leader can expel members.
        /// </summary>
        /// <param name="allianceId">The alliance to modify.</param>
        /// <param name="targetNation">The nation to expel.</param>
        /// <returns>True if the expulsion was successful.</returns>
        public bool ExpelMember(string allianceId, string targetNation)
        {
            if (string.IsNullOrEmpty(allianceId) || !_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] ExpelMember: alliance not found.");
                return false;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] ExpelMember: alliance is not active.");
                return false;
            }

            if (string.IsNullOrEmpty(targetNation) || !alliance.memberNations.Contains(targetNation))
            {
                Debug.LogWarning("[AllianceManager] ExpelMember: target nation is not a member.");
                return false;
            }

            // Cannot expel the leader
            if (targetNation == alliance.leaderNation)
            {
                Debug.LogWarning("[AllianceManager] ExpelMember: cannot expel the alliance leader.");
                return false;
            }

            alliance.memberNations.Remove(targetNation);
            RemoveNationAllianceMapping(targetNation, allianceId);
            _nationContributions[allianceId].Remove(targetNation);

            var action = new AllianceAction(
                AllianceAction.ActionType.Expulsion,
                alliance.leaderNation, targetNation, 0f,
                $"'{targetNation}' expelled from '{alliance.allianceName}'."
            );

            OnAllianceAction?.Invoke(allianceId, alliance.leaderNation, action);
            OnMemberExpelled?.Invoke(allianceId, targetNation);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{targetNation}' expelled from '{alliance.allianceName}'. " +
                          $"Remaining members: {alliance.memberNations.Count}.");
            }

            return true;
        }

        /// <summary>
        /// Permanently dissolves an alliance, removing all membership associations
        /// and clearing shared resources.
        /// </summary>
        /// <param name="allianceId">The alliance to dissolve.</param>
        /// <returns>True if dissolution was successful.</returns>
        public bool DissolveAlliance(string allianceId)
        {
            if (string.IsNullOrEmpty(allianceId) || !_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] DissolveAlliance: alliance not found.");
                return false;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] DissolveAlliance: alliance is already inactive.");
                return false;
            }

            // Remove all nation mappings
            foreach (var memberId in alliance.memberNations)
            {
                RemoveNationAllianceMapping(memberId, allianceId);
            }

            alliance.isActive = false;
            alliance.memberNations.Clear();
            alliance.treasury = 0f;
            alliance.militaryBudget = 0f;

            // Clean up tracking data
            _nationContributions.Remove(allianceId);
            _sharedIntel.Remove(allianceId);
            _sanctionedNations.Remove(allianceId);

            OnAllianceDissolved?.Invoke(allianceId);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] Alliance '{alliance.allianceName}' (ID: {allianceId}) dissolved.");
            }

            return true;
        }

        // =================================================================
        // MEMBERSHIP QUERIES
        // =================================================================

        /// <summary>
        /// Checks whether a nation is a member of a specific alliance.
        /// </summary>
        /// <param name="allianceId">The alliance to check.</param>
        /// <param name="nationId">The nation to look for.</param>
        /// <returns>True if the nation is an active member.</returns>
        public bool IsMember(string allianceId, string nationId)
        {
            if (!_alliances.ContainsKey(allianceId)) return false;
            var alliance = _alliances[allianceId];
            return alliance.isActive && alliance.memberNations.Contains(nationId);
        }

        /// <summary>
        /// Returns all alliances, both active and inactive.
        /// </summary>
        /// <returns>List of all <see cref="Alliance"/> objects.</returns>
        public List<Alliance> GetAlliances()
        {
            return _alliances.Values.ToList();
        }

        /// <summary>
        /// Returns all active alliances only.
        /// </summary>
        /// <returns>List of active alliances.</returns>
        public List<Alliance> GetActiveAlliances()
        {
            return _alliances.Values.Where(a => a.isActive).ToList();
        }

        /// <summary>
        /// Returns the first active alliance that a nation belongs to.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>The <see cref="Alliance"/>, or null if the nation is in no alliance.</returns>
        public Alliance GetAllianceOfNation(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return null;

            if (_nationToAllianceMap.TryGetValue(nationId, out var allianceIds))
            {
                foreach (var allianceId in allianceIds)
                {
                    if (_alliances.ContainsKey(allianceId) && _alliances[allianceId].isActive)
                    {
                        return _alliances[allianceId];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all alliances a nation belongs to.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of alliances containing this nation.</returns>
        public List<Alliance> GetAllAlliancesOfNation(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return new List<Alliance>();

            var result = new List<Alliance>();
            if (_nationToAllianceMap.TryGetValue(nationId, out var allianceIds))
            {
                foreach (var allianceId in allianceIds)
                {
                    if (_alliances.ContainsKey(allianceId) && _alliances[allianceId].isActive)
                    {
                        result.Add(_alliances[allianceId]);
                    }
                }
            }
            return result;
        }

        // =================================================================
        // ALLIANCE STRENGTH CALCULATIONS
        // =================================================================

        /// <summary>
        /// Calculates the combined strength of an alliance, factoring in
        /// military power, economic output, number of members, and shared assets.
        /// <para>
        /// Formula: base = sum(memberMilitaryPower + memberEconomicOutput) * memberCountBonus * sharedAssetBonus
        /// </para>
        /// </summary>
        /// <param name="allianceId">The alliance to evaluate.</param>
        /// <returns>Combined strength score.</returns>
        public float CalculateAllianceStrength(string allianceId)
        {
            if (!_alliances.ContainsKey(allianceId)) return 0f;

            var alliance = _alliances[allianceId];
            if (!alliance.isActive || alliance.memberNations.Count == 0) return 0f;

            // Base strength from member contributions
            float memberContributionTotal = 0f;
            foreach (var kvp in _nationContributions[allianceId])
            {
                memberContributionTotal += kvp.Value;
            }

            // Member count multiplier (more members = stronger coalition)
            float memberCountBonus = 1f + (alliance.memberNations.Count - 1) * 0.15f;

            // Shared asset bonus
            float sharedAssetBonus = 1f + (alliance.sharedAssets.Count * 0.1f);

            // Alliance type multiplier
            float typeMultiplier = GetAllianceTypeStrengthMultiplier(alliance.type);

            // Treasury contribution
            float treasuryBonus = alliance.treasury * 0.01f;

            float totalStrength = (memberContributionTotal + treasuryBonus) * memberCountBonus * sharedAssetBonus * typeMultiplier;

            return totalStrength;
        }

        /// <summary>
        /// Returns a strength multiplier based on alliance type depth.
        /// </summary>
        private float GetAllianceTypeStrengthMultiplier(AllianceType type)
        {
            switch (type)
            {
                case AllianceType.MilitaryPact: return 1.3f;
                case AllianceType.EconomicUnion: return 1.2f;
                case AllianceType.ResearchPartnership: return 1.1f;
                case AllianceType.DefenseCoalition: return 1.5f;
                case AllianceType.FullFederation: return 2.0f;
                default: return 1.0f;
            }
        }

        // =================================================================
        // ALLIANCE OPERATIONS
        // =================================================================

        /// <summary>
        /// Activates the shared defense clause, placing all member nations at war
        /// with the attacker. Triggers when any member is attacked.
        /// </summary>
        /// <param name="allianceId">The alliance activating shared defense.</param>
        /// <param name="attackerNation">The nation that triggered the defense clause.</param>
        public void ActivateSharedDefense(string allianceId, string attackerNation)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] ActivateSharedDefense: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] ActivateSharedDefense: alliance is not active.");
                return;
            }

            if (alliance.type != AllianceType.MilitaryPact &&
                alliance.type != AllianceType.DefenseCoalition &&
                alliance.type != AllianceType.FullFederation)
            {
                Debug.LogWarning($"[AllianceManager] Alliance type '{alliance.type}' does not support shared defense.");
                return;
            }

            // All members are now at war with the attacker
            var defendingNations = new List<string>(alliance.memberNations);

            var action = new AllianceAction(
                AllianceAction.ActionType.SharedDefense,
                attackerNation, string.Join(", ", defendingNations), 0f,
                $"Shared defense activated! All {defendingNations.Count} members now at war with '{attackerNation}'."
            );

            OnAllianceAction?.Invoke(allianceId, attackerNation, action);
            OnSharedDefenseActivated?.Invoke(allianceId, attackerNation);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] SHARED DEFENSE ACTIVATED by '{alliance.allianceName}'! " +
                          $"{defendingNations.Count} nations now at war with '{attackerNation}'.");
            }
        }

        /// <summary>
        /// Initiates joint research pooling member resources to accelerate
        /// development of a specific technology.
        /// </summary>
        /// <param name="allianceId">The alliance conducting research.</param>
        /// <param name="techId">The technology being researched.</param>
        public void JointResearch(string allianceId, string techId)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] JointResearch: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] JointResearch: alliance is not active.");
                return;
            }

            if (!alliance.sharedAssets.Contains(SharedAssetType.ResearchFacilities) &&
                alliance.type != AllianceType.ResearchPartnership &&
                alliance.type != AllianceType.FullFederation)
            {
                Debug.LogWarning("[AllianceManager] This alliance does not support joint research.");
                return;
            }

            if (alliance.treasury < 100f)
            {
                Debug.LogWarning("[AllianceManager] Insufficient alliance treasury for joint research.");
                return;
            }

            // Deduct research cost from alliance treasury
            float researchCost = 100f + (alliance.memberNations.Count * 25f);
            alliance.treasury -= researchCost;

            // Research speed bonus: more members = faster research
            float speedMultiplier = 1f + (alliance.memberNations.Count * 0.25f);

            var action = new AllianceAction(
                AllianceAction.ActionType.JointResearch,
                alliance.leaderNation, techId, speedMultiplier,
                $"Joint research on '{techId}' initiated by '{alliance.allianceName}'. " +
                $"Speed: {speedMultiplier:F1}x, Cost: {researchCost:F0}."
            );

            OnAllianceAction?.Invoke(allianceId, alliance.leaderNation, action);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] Joint research on '{techId}' by '{alliance.allianceName}'. " +
                          $"Speed multiplier: {speedMultiplier:F1}x, Cost: {researchCost:F0}.");
            }
        }

        /// <summary>
        /// Transfers economic aid from one alliance member to another through
        /// the alliance treasury system.
        /// </summary>
        /// <param name="allianceId">The alliance facilitating the transfer.</param>
        /// <param name="fromNation">The nation providing aid.</param>
        /// <param name="toNation">The nation receiving aid.</param>
        /// <param name="amount">Amount of economic aid to transfer.</param>
        public void ProvideEconomicAid(string allianceId, string fromNation, string toNation, float amount)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] ProvideEconomicAid: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] ProvideEconomicAid: alliance is not active.");
                return;
            }

            if (amount <= 0f)
            {
                Debug.LogWarning("[AllianceManager] ProvideEconomicAid: amount must be positive.");
                return;
            }

            if (!alliance.memberNations.Contains(fromNation))
            {
                Debug.LogWarning($"[AllianceManager] '{fromNation}' is not a member of this alliance.");
                return;
            }

            if (!alliance.memberNations.Contains(toNation))
            {
                Debug.LogWarning($"[AllianceManager] '{toNation}' is not a member of this alliance.");
                return;
            }

            var action = new AllianceAction(
                AllianceAction.ActionType.EconomicAid,
                fromNation, toNation, amount,
                $"'{fromNation}' provides {amount:F0} economic aid to '{toNation}' via '{alliance.allianceName}'."
            );

            OnAllianceAction?.Invoke(allianceId, fromNation, action);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{fromNation}' sent {amount:F0} economic aid to '{toNation}' " +
                          $"via '{alliance.allianceName}'.");
            }
        }

        /// <summary>
        /// Transfers military units or resources from one alliance member to another.
        /// </summary>
        /// <param name="allianceId">The alliance facilitating the transfer.</param>
        /// <param name="fromNation">The nation providing military aid.</param>
        /// <param name="toNation">The nation receiving military aid.</param>
        /// <param name="unitTypes">List of unit type identifiers being transferred.</param>
        public void ProvideMilitaryAid(string allianceId, string fromNation, string toNation, List<string> unitTypes)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] ProvideMilitaryAid: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] ProvideMilitaryAid: alliance is not active.");
                return;
            }

            if (unitTypes == null || unitTypes.Count == 0)
            {
                Debug.LogWarning("[AllianceManager] ProvideMilitaryAid: unit types list is empty.");
                return;
            }

            if (!alliance.memberNations.Contains(fromNation))
            {
                Debug.LogWarning($"[AllianceManager] '{fromNation}' is not a member of this alliance.");
                return;
            }

            if (!alliance.memberNations.Contains(toNation))
            {
                Debug.LogWarning($"[AllianceManager] '{toNation}' is not a member of this alliance.");
                return;
            }

            string unitSummary = string.Join(", ", unitTypes);
            var action = new AllianceAction(
                AllianceAction.ActionType.MilitaryAid,
                fromNation, toNation, unitTypes.Count,
                $"'{fromNation}' sends military aid to '{toNation}': {unitSummary}."
            );

            OnAllianceAction?.Invoke(allianceId, fromNation, action);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{fromNation}' sent military aid ({unitSummary}) to '{toNation}' " +
                          $"via '{alliance.allianceName}'.");
            }
        }

        /// <summary>
        /// Imposes trade sanctions on an external nation, reducing their trade
        /// income with all alliance members.
        /// </summary>
        /// <param name="allianceId">The alliance imposing sanctions.</param>
        /// <param name="targetNation">The nation being sanctioned.</param>
        public void ImposeSanctions(string allianceId, string targetNation)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] ImposeSanctions: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] ImposeSanctions: alliance is not active.");
                return;
            }

            if (alliance.memberNations.Contains(targetNation))
            {
                Debug.LogWarning("[AllianceManager] Cannot sanction a member nation.");
                return;
            }

            if (_sanctionedNations[allianceId].Contains(targetNation))
            {
                Debug.LogWarning($"[AllianceManager] '{targetNation}' is already sanctioned by this alliance.");
                return;
            }

            _sanctionedNations[allianceId].Add(targetNation);

            var action = new AllianceAction(
                AllianceAction.ActionType.Sanctions,
                alliance.leaderNation, targetNation, 0f,
                $"'{alliance.allianceName}' imposes trade sanctions on '{targetNation}'."
            );

            OnAllianceAction?.Invoke(allianceId, alliance.leaderNation, action);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{alliance.allianceName}' sanctions '{targetNation}'. " +
                          $"Total sanctioned: {_sanctionedNations[allianceId].Count}.");
            }
        }

        /// <summary>
        /// Lifts trade sanctions on a nation.
        /// </summary>
        /// <param name="allianceId">The alliance lifting sanctions.</param>
        /// <param name="targetNation">The nation being un-sanctioned.</param>
        public void LiftSanctions(string allianceId, string targetNation)
        {
            if (!_sanctionedNations.ContainsKey(allianceId)) return;

            if (_sanctionedNations[allianceId].Remove(targetNation))
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[AllianceManager] '{allianceId}' lifted sanctions on '{targetNation}'.");
                }
            }
        }

        /// <summary>
        /// Checks whether a nation is currently sanctioned by an alliance.
        /// </summary>
        /// <param name="allianceId">The alliance to check.</param>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>True if the nation is sanctioned.</returns>
        public bool IsNationSanctioned(string allianceId, string nationId)
        {
            if (!_sanctionedNations.ContainsKey(allianceId)) return false;
            return _sanctionedNations[allianceId].Contains(nationId);
        }

        /// <summary>
        /// Shares an intelligence report with all alliance members who have
        /// intelligence sharing enabled.
        /// </summary>
        /// <param name="allianceId">The alliance sharing intelligence.</param>
        /// <param name="fromNation">The nation providing the intelligence.</param>
        /// <param name="report">The intelligence report to share.</param>
        public void ShareIntelligence(string allianceId, string fromNation, IntelReport report)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] ShareIntelligence: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] ShareIntelligence: alliance is not active.");
                return;
            }

            if (!alliance.sharedAssets.Contains(SharedAssetType.Intelligence))
            {
                Debug.LogWarning("[AllianceManager] This alliance does not have intelligence sharing.");
                return;
            }

            if (report == null)
            {
                Debug.LogWarning("[AllianceManager] ShareIntelligence: report is null.");
                return;
            }

            // Add to shared intel pool
            _sharedIntel[allianceId].Add(report);

            // Trim old reports (keep last 50)
            if (_sharedIntel[allianceId].Count > 50)
            {
                _sharedIntel[allianceId].RemoveAt(0);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{fromNation}' shared intel with '{alliance.allianceName}': " +
                          $"[{report.intelType}] {report.content} (confidence: {report.confidence:F0}%).");
            }
        }

        /// <summary>
        /// Retrieves shared intelligence reports for an alliance.
        /// </summary>
        /// <param name="allianceId">The alliance to query.</param>
        /// <returns>List of shared intelligence reports.</returns>
        public List<IntelReport> GetSharedIntel(string allianceId)
        {
            if (_sharedIntel.ContainsKey(allianceId))
            {
                return new List<IntelReport>(_sharedIntel[allianceId]);
            }
            return new List<IntelReport>();
        }

        /// <summary>
        /// Establishes a shared asset (base, facility, etc.) at a specific
        /// hex grid location for alliance use.
        /// </summary>
        /// <param name="allianceId">The alliance establishing the asset.</param>
        /// <param name="location">Hex coordinate for the asset placement.</param>
        /// <param name="type">Type of shared asset to establish.</param>
        public void EstablishSharedBase(string allianceId, HexCoord location, SharedAssetType type)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] EstablishSharedBase: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] EstablishSharedBase: alliance is not active.");
                return;
            }

            if (location == null)
            {
                Debug.LogWarning("[AllianceManager] EstablishSharedBase: location is null.");
                return;
            }

            if (!alliance.sharedAssets.Contains(type))
            {
                alliance.sharedAssets.Add(type);
            }

            float establishmentCost = GetSharedAssetCost(type);
            if (alliance.treasury < establishmentCost)
            {
                Debug.LogWarning($"[AllianceManager] Insufficient treasury ({alliance.treasury:F0}) " +
                                $"for {type} (cost: {establishmentCost:F0}).");
                return;
            }

            alliance.treasury -= establishmentCost;

            var action = new AllianceAction(
                AllianceAction.ActionType.JointResearch,
                alliance.leaderNation, "", establishmentCost,
                $"'{alliance.allianceName}' established {type} at {location}."
            );

            OnAllianceAction?.Invoke(allianceId, alliance.leaderNation, action);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{alliance.allianceName}' established {type} at {location}. " +
                          $"Cost: {establishmentCost:F0}, Treasury remaining: {alliance.treasury:F0}.");
            }
        }

        /// <summary>
        /// Returns the cost to establish a shared asset.
        /// </summary>
        private float GetSharedAssetCost(SharedAssetType type)
        {
            switch (type)
            {
                case SharedAssetType.MilitaryBases: return 500f;
                case SharedAssetType.Intelligence: return 300f;
                case SharedAssetType.ResearchFacilities: return 400f;
                case SharedAssetType.SupplyDepots: return 200f;
                case SharedAssetType.MissileDefense: return 800f;
                default: return 300f;
            }
        }

        /// <summary>
        /// Deploys a joint military force from alliance members to a specific location.
        /// </summary>
        /// <param name="allianceId">The alliance deploying the force.</param>
        /// <param name="location">Hex coordinate for deployment.</param>
        /// <param name="strength">Combined combat strength of the deployed force.</param>
        public void DeployJointForce(string allianceId, HexCoord location, int strength)
        {
            if (!_alliances.ContainsKey(allianceId))
            {
                Debug.LogWarning("[AllianceManager] DeployJointForce: alliance not found.");
                return;
            }

            var alliance = _alliances[allianceId];
            if (!alliance.isActive)
            {
                Debug.LogWarning("[AllianceManager] DeployJointForce: alliance is not active.");
                return;
            }

            if (strength <= 0)
            {
                Debug.LogWarning("[AllianceManager] DeployJointForce: strength must be positive.");
                return;
            }

            float deploymentCost = strength * 5f;
            if (alliance.treasury < deploymentCost)
            {
                Debug.LogWarning($"[AllianceManager] Insufficient treasury for joint force deployment " +
                                $"(need {deploymentCost:F0}, have {alliance.treasury:F0}).");
                return;
            }

            alliance.treasury -= deploymentCost;
            alliance.militaryBudget -= deploymentCost;

            var action = new AllianceAction(
                AllianceAction.ActionType.MilitaryAid,
                alliance.leaderNation, "", strength,
                $"'{alliance.allianceName}' deployed joint force (strength {strength}) to {location}."
            );

            OnAllianceAction?.Invoke(allianceId, alliance.leaderNation, action);

            if (enableDebugLogs)
            {
                Debug.Log($"[AllianceManager] '{alliance.allianceName}' deployed joint force " +
                          $"(strength: {strength}) to {location}. Cost: {deploymentCost:F0}.");
            }
        }

        // =================================================================
        // CONTRIBUTIONS & TREASURY
        // =================================================================

        /// <summary>
        /// Calculates how much a specific nation has contributed to an alliance
        /// in terms of treasury, military, and economic resources.
        /// </summary>
        /// <param name="allianceId">The alliance to query.</param>
        /// <param name="nationId">The contributing nation.</param>
        /// <returns>Total contribution value.</returns>
        public float GetContribution(string allianceId, string nationId)
        {
            if (!_nationContributions.ContainsKey(allianceId)) return 0f;

            _nationContributions[allianceId].TryGetValue(nationId, out float contribution);
            return contribution;
        }

        /// <summary>
        /// Sets a nation's contribution value for an alliance.
        /// </summary>
        /// <param name="allianceId">The alliance.</param>
        /// <param name="nationId">The contributing nation.</param>
        /// <param name="amount">Contribution amount.</param>
        public void SetContribution(string allianceId, string nationId, float amount)
        {
            if (!_nationContributions.ContainsKey(allianceId))
            {
                _nationContributions[allianceId] = new Dictionary<string, float>();
            }

            _nationContributions[allianceId][nationId] = Mathf.Max(0f, amount);
        }

        /// <summary>
        /// Adds to a nation's contribution for an alliance.
        /// </summary>
        /// <param name="allianceId">The alliance.</param>
        /// <param name="nationId">The contributing nation.</param>
        /// <param name="amount">Amount to add.</param>
        public void AddContribution(string allianceId, string nationId, float amount)
        {
            float current = GetContribution(allianceId, nationId);
            SetContribution(allianceId, nationId, current + amount);
        }

        // =================================================================
        // TURN-BASED UPDATE
        // =================================================================

        /// <summary>
        /// Updates all alliances each turn. Processes treasury management,
        /// shared project progress, membership maintenance, and sanctions effects.
        /// </summary>
        public void UpdateAlliances()
        {
            _currentTurn++;

            foreach (var kvp in _alliances)
            {
                var alliance = kvp.Value;
                if (!alliance.isActive) continue;

                // --- Treasury income from member contributions ---
                foreach (var memberId in alliance.memberNations)
                {
                    // Simulated GDP-based contribution (in a real game, this would query the economy system)
                    float baseContribution = 50f * treasuryContributionRate * 1000f;
                    float contribution = baseContribution * GetAllianceTypeContributionMultiplier(alliance.type);
                    alliance.treasury += contribution;
                    AddContribution(allianceId, memberId, contribution);
                }

                // --- Military budget allocation ---
                alliance.militaryBudget = alliance.treasury * militaryBudgetPercentage;

                // --- Check for alliance dissolution (too few members) ---
                if (alliance.memberNations.Count < 1)
                {
                    DissolveAlliance(alliance.allianceId);
                    continue;
                }

                // --- Shared asset maintenance costs ---
                float maintenanceCost = 0f;
                foreach (var asset in alliance.sharedAssets)
                {
                    maintenanceCost += GetAssetMaintenanceCost(asset);
                }

                alliance.treasury -= maintenanceCost;

                if (alliance.treasury < -500f)
                {
                    Debug.LogWarning($"[AllianceManager] '{alliance.allianceName}' treasury critically low " +
                                    $"({alliance.treasury:F0}). Members may defect.");
                }

                if (enableDebugLogs)
                {
                    Debug.Log($"[AllianceManager] Turn {_currentTurn}: '{alliance.allianceName}' " +
                              $"treasury={alliance.treasury:F0}, military={alliance.militaryBudget:F0}, " +
                              $"members={alliance.memberNations.Count}, maintenance={maintenanceCost:F0}.");
                }
            }
        }

        /// <summary>
        /// Returns a contribution multiplier based on alliance type.
        /// </summary>
        private float GetAllianceTypeContributionMultiplier(AllianceType type)
        {
            switch (type)
            {
                case AllianceType.MilitaryPact: return 0.8f;
                case AllianceType.EconomicUnion: return 1.5f;
                case AllianceType.ResearchPartnership: return 0.6f;
                case AllianceType.DefenseCoalition: return 1.0f;
                case AllianceType.FullFederation: return 2.0f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// Returns the per-turn maintenance cost for a shared asset type.
        /// </summary>
        private float GetAssetMaintenanceCost(SharedAssetType type)
        {
            switch (type)
            {
                case SharedAssetType.MilitaryBases: return 50f;
                case SharedAssetType.Intelligence: return 30f;
                case SharedAssetType.ResearchFacilities: return 40f;
                case SharedAssetType.SupplyDepots: return 20f;
                case SharedAssetType.MissileDefense: return 80f;
                default: return 25f;
            }
        }

        // =================================================================
        // PRIVATE HELPERS
        // =================================================================

        /// <summary>
        /// Maps a nation to an alliance in the bidirectional lookup.
        /// </summary>
        private void AddNationAllianceMapping(string nationId, string allianceId)
        {
            if (!_nationToAllianceMap.ContainsKey(nationId))
            {
                _nationToAllianceMap[nationId] = new List<string>();
            }

            if (!_nationToAllianceMap[nationId].Contains(allianceId))
            {
                _nationToAllianceMap[nationId].Add(allianceId);
            }
        }

        /// <summary>
        /// Removes a nation-to-alliance mapping.
        /// </summary>
        private void RemoveNationAllianceMapping(string nationId, string allianceId)
        {
            if (_nationToAllianceMap.ContainsKey(nationId))
            {
                _nationToAllianceMap[nationId].Remove(allianceId);

                if (_nationToAllianceMap[nationId].Count == 0)
                {
                    _nationToAllianceMap.Remove(nationId);
                }
            }
        }

        /// <summary>
        /// Unity lifecycle: called every frame. Reserved for future real-time updates.
        /// </summary>
        private void Update()
        {
            // Reserved for real-time alliance simulation, UI updates, etc.
        }
    }
}
