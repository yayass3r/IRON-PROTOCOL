// =====================================================================
// IRON PROTOCOL - Policy System
// Government policies that modify nation stats, economics, and diplomacy.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.GameSystems.Elections
{
    // =================================================================
    // ENUMERATIONS
    // =================================================================

    /// <summary>
    /// Broad category of a government policy, used for grouping and filtering.
    /// </summary>
    public enum PolicyCategory
    {
        /// <summary>Policies affecting armed forces, defense spending, and military readiness.</summary>
        Military,
        /// <summary>Policies affecting treasury, trade, taxation, and economic growth.</summary>
        Economic,
        /// <summary>Policies affecting international relations, alliances, and treaties.</summary>
        Diplomatic,
        /// <summary>Policies affecting population welfare, education, healthcare, and civil rights.</summary>
        Social,
        /// <summary>Policies affecting internal security, espionage, and law enforcement.</summary>
        Security
    }

    // =================================================================
    // DATA CLASSES
    // =================================================================

    /// <summary>
    /// Represents a single government policy that can be enacted or revoked.
    /// Each policy carries numeric modifiers that affect nation statistics when active.
    /// Policies may be mutually exclusive with other policies.
    /// </summary>
    [Serializable]
    public class Policy
    {
        /// <summary>Unique identifier (e.g. "POL_MIL_EXPANSION").</summary>
        public string policyId;

        /// <summary>Display name shown in UI menus.</summary>
        public string name;

        /// <summary>Broad category for filtering and grouping.</summary>
        public PolicyCategory category;

        /// <summary>Human-readable description of the policy's effects.</summary>
        public string description;

        /// <summary>Percentage modifier to military spending when active (e.g. 0.3 = +30%).</summary>
        public float militarySpendingMod;

        /// <summary>Percentage modifier to economic output when active (negative = penalty).</summary>
        public float economicMod;

        /// <summary>Percentage modifier to diplomacy effectiveness when active.</summary>
        public float diplomacyMod;

        /// <summary>Flat modifier to national stability when active (negative = destabilizing).</summary>
        public float stabilityMod;

        /// <summary>Flat modifier to public approval when active.</summary>
        public float approvalMod;

        /// <summary>List of policy IDs that cannot be active simultaneously with this policy.</summary>
        public List<string> mutuallyExclusiveWith = new List<string>();

        /// <summary>Whether this policy is currently active for any nation.</summary>
        public bool isActive;

        /// <summary>
        /// Creates a new policy with all fields specified.
        /// </summary>
        public Policy(
            string policyId,
            string name,
            PolicyCategory category,
            string description,
            float militarySpendingMod,
            float economicMod,
            float diplomacyMod,
            float stabilityMod,
            float approvalMod,
            List<string> mutuallyExclusiveWith = null)
        {
            this.policyId = policyId;
            this.name = name;
            this.category = category;
            this.description = description;
            this.militarySpendingMod = militarySpendingMod;
            this.economicMod = economicMod;
            this.diplomacyMod = diplomacyMod;
            this.stabilityMod = stabilityMod;
            this.approvalMod = approvalMod;
            this.mutuallyExclusiveWith = mutuallyExclusiveWith ?? new List<string>();
            this.isActive = false;
        }
    }

    // =================================================================
    // POLICY SYSTEM
    // =================================================================

    /// <summary>
    /// Manages the full lifecycle of government policies: definition, enactment,
    /// revocation, and effect application. Contains 12 predefined policies and
    /// tracks per-nation active policy sets.
    /// Attach to a persistent GameManager as a singleton.
    /// </summary>
    public class PolicySystem : MonoBehaviour
    {
        // -------------------------------------------------------------
        // STATE
        // -------------------------------------------------------------
        private readonly Dictionary<string, Policy> _allPolicies = new Dictionary<string, Policy>();
        private readonly Dictionary<string, HashSet<string>> _nationActivePolicies =
            new Dictionary<string, HashSet<string>>();

        // -------------------------------------------------------------
        // EVENTS
        // -------------------------------------------------------------
        /// <summary>Fired when a policy is enacted. Parameters: (nationId, policyId).</summary>
        public event Action<string, string> OnPolicyEnacted;

        /// <summary>Fired when a policy is revoked. Parameters: (nationId, policyId).</summary>
        public event Action<string, string> OnPolicyRevoked;

        // =============================================================
        // INITIALIZATION
        // =============================================================

        /// <summary>
        /// Unity lifecycle callback. Registers all 12 predefined policies on startup.
        /// </summary>
        private void Awake()
        {
            RegisterPredefinedPolicies();
        }

        /// <summary>
        /// Registers the 12 predefined policies with their modifiers and exclusions.
        /// Called automatically in Awake.
        /// </summary>
        private void RegisterPredefinedPolicies()
        {
            // --- 1. Military Expansion ---
            RegisterPolicy(new Policy(
                policyId: "Military Expansion",
                name: "Military Expansion",
                category: PolicyCategory.Military,
                description: "Significantly increase defense budget. +30% military spending, but -10% economy and -5 stability.",
                militarySpendingMod: 0.30f,
                economicMod: -0.10f,
                diplomacyMod: -0.05f,
                stabilityMod: -5f,
                approvalMod: -2f
            ));

            // --- 2. Economic Austerity ---
            RegisterPolicy(new Policy(
                policyId: "Economic Austerity",
                name: "Economic Austerity",
                category: PolicyCategory.Economic,
                description: "Slash public spending to rebuild the treasury. +20% treasury income, -15% approval, +10% stability.",
                militarySpendingMod: -0.05f,
                economicMod: 0.20f,
                diplomacyMod: 0f,
                stabilityMod: 10f,
                approvalMod: -15f
            ));

            // --- 3. Free Education ---
            RegisterPolicy(new Policy(
                policyId: "Free Education",
                name: "Free Education",
                category: PolicyCategory.Social,
                description: "Invest in public education. +10% research output, -5% treasury, +10% approval.",
                militarySpendingMod: 0f,
                economicMod: -0.05f,
                diplomacyMod: 0f,
                stabilityMod: 2f,
                approvalMod: 10f
            ));

            // --- 4. Universal Healthcare ---
            RegisterPolicy(new Policy(
                policyId: "Universal Healthcare",
                name: "Universal Healthcare",
                category: PolicyCategory.Social,
                description: "Provide healthcare for all citizens. +15% population growth, -10% treasury, +20% approval.",
                militarySpendingMod: 0f,
                economicMod: -0.10f,
                diplomacyMod: 0f,
                stabilityMod: 5f,
                approvalMod: 20f
            ));

            // --- 5. Open Borders ---
            RegisterPolicy(new Policy(
                policyId: "Open Borders",
                name: "Open Borders",
                category: PolicyCategory.Diplomatic,
                description: "Welcome immigrants and trade. +20% trade, -10% stability, +5% immigration rate.",
                militarySpendingMod: 0f,
                economicMod: 0.15f,
                diplomacyMod: 0.10f,
                stabilityMod: -10f,
                approvalMod: -5f,
                mutuallyExclusiveWith: new List<string> { "Closed Borders" }
            ));

            // --- 6. Closed Borders ---
            RegisterPolicy(new Policy(
                policyId: "Closed Borders",
                name: "Closed Borders",
                category: PolicyCategory.Security,
                description: "Seal the borders. -20% trade, +15% stability, -10% economy.",
                militarySpendingMod: 0f,
                economicMod: -0.10f,
                diplomacyMod: -0.10f,
                stabilityMod: 15f,
                approvalMod: -5f,
                mutuallyExclusiveWith: new List<string> { "Open Borders" }
            ));

            // --- 7. Censorship ---
            RegisterPolicy(new Policy(
                policyId: "Censorship",
                name: "Censorship",
                category: PolicyCategory.Security,
                description: "Control information flow. +10% stability, -10% approval, -5% diplomacy.",
                militarySpendingMod: 0f,
                economicMod: 0f,
                diplomacyMod: -0.05f,
                stabilityMod: 10f,
                approvalMod: -10f,
                mutuallyExclusiveWith: new List<string> { "Freedom of Press" }
            ));

            // --- 8. Freedom of Press ---
            RegisterPolicy(new Policy(
                policyId: "Freedom of Press",
                name: "Freedom of Press",
                category: PolicyCategory.Social,
                description: "Guarantee press freedom. -5% stability, +10% approval, +10% diplomacy.",
                militarySpendingMod: 0f,
                economicMod: 0f,
                diplomacyMod: 0.10f,
                stabilityMod: -5f,
                approvalMod: 10f,
                mutuallyExclusiveWith: new List<string> { "Censorship" }
            ));

            // --- 9. Nuclear Ambition ---
            RegisterPolicy(new Policy(
                policyId: "Nuclear Ambition",
                name: "Nuclear Ambition",
                category: PolicyCategory.Military,
                description: "Pursue nuclear weapons capability. Unlocks nuclear research, -20% diplomacy, +10% military.",
                militarySpendingMod: 0.10f,
                economicMod: -0.05f,
                diplomacyMod: -0.20f,
                stabilityMod: -3f,
                approvalMod: -5f
            ));

            // --- 10. Green Energy ---
            RegisterPolicy(new Policy(
                policyId: "Green Energy",
                name: "Green Energy",
                category: PolicyCategory.Economic,
                description: "Invest in renewable energy. -10% oil dependency, +5% stability, -5% economy short-term.",
                militarySpendingMod: 0f,
                economicMod: -0.05f,
                diplomacyMod: 0.05f,
                stabilityMod: 5f,
                approvalMod: 5f
            ));

            // --- 11. Secret Police ---
            RegisterPolicy(new Policy(
                policyId: "Secret Police",
                name: "Secret Police",
                category: PolicyCategory.Security,
                description: "Deploy covert internal security. +20% counter-espionage, -15% approval, +10% stability.",
                militarySpendingMod: 0.05f,
                economicMod: -0.03f,
                diplomacyMod: -0.05f,
                stabilityMod: 10f,
                approvalMod: -15f
            ));

            // --- 12. Foreign Aid ---
            RegisterPolicy(new Policy(
                policyId: "Foreign Aid",
                name: "Foreign Aid",
                category: PolicyCategory.Diplomatic,
                description: "Provide assistance to other nations. +15% diplomacy, -10% treasury, +5% international opinion.",
                militarySpendingMod: 0f,
                economicMod: -0.10f,
                diplomacyMod: 0.15f,
                stabilityMod: 0f,
                approvalMod: -3f
            ));

            Debug.Log($"[PolicySystem] Registered {_allPolicies.Count} predefined policies.");
        }

        /// <summary>
        /// Registers a policy into the master list. Overwrites if the ID already exists.
        /// </summary>
        /// <param name="policy">The <see cref="Policy"/> to register.</param>
        public void RegisterPolicy(Policy policy)
        {
            if (policy == null || string.IsNullOrEmpty(policy.policyId))
            {
                Debug.LogError("[PolicySystem] Cannot register null or ID-less policy.");
                return;
            }

            _allPolicies[policy.policyId] = policy;
        }

        // =============================================================
        // POLICY QUERIES
        // =============================================================

        /// <summary>
        /// Returns all registered policies regardless of active state.
        /// </summary>
        /// <returns>List of all <see cref="Policy"/> definitions.</returns>
        public List<Policy> GetAllPolicies()
        {
            return _allPolicies.Values.ToList();
        }

        /// <summary>
        /// Returns policies that a nation can currently enact.
        /// Filters out already-active policies and those blocked by mutual exclusions.
        /// </summary>
        /// <param name="nationId">Nation to check available policies for.</param>
        /// <returns>List of <see cref="Policy"/> objects available for enactment.</returns>
        public List<Policy> GetAvailablePolicies(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogError("[PolicySystem] GetAvailablePolicies: nationId is null or empty.");
                return new List<Policy>();
            }

            var activeSet = GetNationActivePolicySet(nationId);
            var available = new List<Policy>();

            foreach (Policy policy in _allPolicies.Values)
            {
                // Skip if already active
                if (activeSet.Contains(policy.policyId))
                    continue;

                // Skip if any mutually exclusive policy is currently active
                bool blocked = false;
                foreach (string exclusiveId in policy.mutuallyExclusiveWith)
                {
                    if (activeSet.Contains(exclusiveId))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    available.Add(policy);
            }

            return available;
        }

        /// <summary>
        /// Returns all policies currently active for a specific nation.
        /// </summary>
        /// <param name="nationId">Nation to query.</param>
        /// <returns>List of active <see cref="Policy"/> objects.</returns>
        public List<Policy> GetActivePolicies(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogError("[PolicySystem] GetActivePolicies: nationId is null or empty.");
                return new List<Policy>();
            }

            var activeSet = GetNationActivePolicySet(nationId);
            var activePolicies = new List<Policy>();

            foreach (string policyId in activeSet)
            {
                if (_allPolicies.TryGetValue(policyId, out Policy policy))
                    activePolicies.Add(policy);
            }

            return activePolicies;
        }

        /// <summary>
        /// Retrieves a single policy definition by ID.
        /// </summary>
        /// <param name="policyId">Policy identifier to look up.</param>
        /// <returns>The <see cref="Policy"/>, or null if not found.</returns>
        public Policy GetPolicy(string policyId)
        {
            if (string.IsNullOrEmpty(policyId)) return null;
            _allPolicies.TryGetValue(policyId, out Policy policy);
            return policy;
        }

        /// <summary>
        /// Checks whether a specific policy is active for a nation.
        /// </summary>
        /// <param name="nationId">Nation to check.</param>
        /// <param name="policyId">Policy identifier.</param>
        /// <returns>True if the policy is currently active.</returns>
        public bool IsPolicyActive(string nationId, string policyId)
        {
            var activeSet = GetNationActivePolicySet(nationId);
            return activeSet.Contains(policyId);
        }

        // =============================================================
        // POLICY ENACTMENT
        // =============================================================

        /// <summary>
        /// Enacts a policy for a nation, activating its stat modifiers.
        /// Automatically revokes any mutually exclusive policies that are currently active.
        /// </summary>
        /// <param name="nationId">Nation enacting the policy.</param>
        /// <param name="policyId">Policy identifier to enact.</param>
        /// <returns>True if the policy was successfully enacted.</returns>
        public bool EnactPolicy(string nationId, string policyId)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(policyId))
            {
                Debug.LogError("[PolicySystem] EnactPolicy: nationId and policyId must not be empty.");
                return false;
            }

            if (!_allPolicies.TryGetValue(policyId, out Policy policy))
            {
                Debug.LogError($"[PolicySystem] Policy '{policyId}' not found in registry.");
                return false;
            }

            var activeSet = GetNationActivePolicySet(nationId);

            // Already active
            if (activeSet.Contains(policyId))
            {
                Debug.LogWarning($"[PolicySystem] Policy '{policyId}' is already active for '{nationId}'.");
                return false;
            }

            // Revoke mutually exclusive policies first
            foreach (string exclusiveId in policy.mutuallyExclusiveWith)
            {
                if (activeSet.Contains(exclusiveId))
                {
                    Debug.Log($"[PolicySystem] Auto-revoking mutually exclusive policy '{exclusiveId}' " +
                              $"before enacting '{policyId}' for '{nationId}'.");
                    RevokePolicy(nationId, exclusiveId);
                }
            }

            // Activate
            activeSet.Add(policyId);
            policy.isActive = true;

            Debug.Log($"[PolicySystem] Policy '{policy.name}' ({policyId}) enacted for '{nationId}'.");
            OnPolicyEnacted?.Invoke(nationId, policyId);
            return true;
        }

        /// <summary>
        /// Revokes a previously active policy from a nation, removing its stat modifiers.
        /// </summary>
        /// <param name="nationId">Nation revoking the policy.</param>
        /// <param name="policyId">Policy identifier to revoke.</param>
        /// <returns>True if the policy was successfully revoked.</returns>
        public bool RevokePolicy(string nationId, string policyId)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(policyId))
            {
                Debug.LogError("[PolicySystem] RevokePolicy: nationId and policyId must not be empty.");
                return false;
            }

            var activeSet = GetNationActivePolicySet(nationId);

            if (!activeSet.Contains(policyId))
            {
                Debug.LogWarning($"[PolicySystem] Policy '{policyId}' is not active for '{nationId}'.");
                return false;
            }

            activeSet.Remove(policyId);

            if (_allPolicies.TryGetValue(policyId, out Policy policy))
                policy.isActive = false;

            Debug.Log($"[PolicySystem] Policy '{policyId}' revoked for '{nationId}'.");
            OnPolicyRevoked?.Invoke(nationId, policyId);
            return true;
        }

        // =============================================================
        // POLICY EFFECTS
        // =============================================================

        /// <summary>
        /// Applies the cumulative effects of all active policies for a nation to the
        /// provided <see cref="Government"/> object. Call this every turn or whenever
        /// the active policy set changes.
        /// </summary>
        /// <param name="nationId">Nation whose policies to apply.</param>
        public void ApplyPolicyEffects(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogError("[PolicySystem] ApplyPolicyEffects: nationId is null or empty.");
                return;
            }

            var activePolicies = GetActivePolicies(nationId);

            float totalMilitaryMod = 0f;
            float totalEconomicMod = 0f;
            float totalDiplomacyMod = 0f;
            float totalStabilityMod = 0f;
            float totalApprovalMod = 0f;

            foreach (Policy policy in activePolicies)
            {
                totalMilitaryMod += policy.militarySpendingMod;
                totalEconomicMod += policy.economicMod;
                totalDiplomacyMod += policy.diplomacyMod;
                totalStabilityMod += policy.stabilityMod;
                totalApprovalMod += policy.approvalMod;
            }

            Debug.Log($"[PolicySystem] Cumulative policy effects for '{nationId}': " +
                      $"Military={totalMilitaryMod:+0.##;-0.##;0}, " +
                      $"Economy={totalEconomicMod:+0.##;-0.##;0}, " +
                      $"Diplomacy={totalDiplomacyMod:+0.##;-0.##;0}, " +
                      $"Stability={totalStabilityMod:+0.##;-0.##;0}, " +
                      $"Approval={totalApprovalMod:+0.##;-0.##;0}");

            // The actual application to Government stats would be handled here
            // or delegated to the EconomySystem / DiplomacySystem as needed.
            // We store the computed modifiers for external consumption.
            _lastComputedEffects[nationId] = new PolicyEffectBundle
            {
                militaryMod = totalMilitaryMod,
                economicMod = totalEconomicMod,
                diplomacyMod = totalDiplomacyMod,
                stabilityMod = totalStabilityMod,
                approvalMod = totalApprovalMod,
                activeCount = activePolicies.Count
            };
        }

        /// <summary>
        /// Returns the last computed cumulative policy effect bundle for a nation.
        /// </summary>
        /// <param name="nationId">Nation to query.</param>
        /// <returns>The cumulative effect data, or default values if not computed.</returns>
        public PolicyEffectBundle GetCumulativeEffects(string nationId)
        {
            if (_lastComputedEffects.TryGetValue(nationId, out PolicyEffectBundle bundle))
                return bundle;

            return new PolicyEffectBundle();
        }

        // =============================================================
        // INTERNAL HELPERS
        // =============================================================

        /// <summary>
        /// Gets or creates the active policy set for a nation.
        /// </summary>
        private HashSet<string> GetNationActivePolicySet(string nationId)
        {
            if (!_nationActivePolicies.TryGetValue(nationId, out HashSet<string> activeSet))
            {
                activeSet = new HashSet<string>();
                _nationActivePolicies[nationId] = activeSet;
            }

            return activeSet;
        }

        /// <summary>
        /// Cached cumulative policy effect data per nation.
        /// </summary>
        private readonly Dictionary<string, PolicyEffectBundle> _lastComputedEffects =
            new Dictionary<string, PolicyEffectBundle>();

        // =============================================================
        // UTILITY
        // =============================================================

        /// <summary>
        /// Returns all policies matching a specific category.
        /// </summary>
        /// <param name="category">Category to filter by.</param>
        /// <returns>List of policies in the given category.</returns>
        public List<Policy> GetPoliciesByCategory(PolicyCategory category)
        {
            return _allPolicies.Values.Where(p => p.category == category).ToList();
        }

        /// <summary>
        /// Returns the number of active policies for a nation.
        /// </summary>
        /// <param name="nationId">Nation to query.</param>
        /// <returns>Count of active policies.</returns>
        public int GetActivePolicyCount(string nationId)
        {
            return GetNationActivePolicySet(nationId).Count;
        }

        /// <summary>
        /// Revokes all active policies for a nation. Used during government overthrows or resets.
        /// </summary>
        /// <param name="nationId">Nation to clear policies for.</param>
        public void RevokeAllPolicies(string nationId)
        {
            var activeSet = GetNationActivePolicySet(nationId);
            if (activeSet.Count == 0) return;

            // Collect IDs to avoid modifying during enumeration
            var ids = new List<string>(activeSet);
            foreach (string policyId in ids)
            {
                RevokePolicy(nationId, policyId);
            }

            Debug.Log($"[PolicySystem] All policies revoked for '{nationId}'.");
        }

        /// <summary>
        /// Clears all nation policy tracking. Use on game reset.
        /// </summary>
        public void Reset()
        {
            _nationActivePolicies.Clear();
            _lastComputedEffects.Clear();
            Debug.Log("[PolicySystem] All nation policy data cleared.");
        }
    }

    // =================================================================
    // SUPPORTING STRUCTURES
    // =================================================================

    /// <summary>
    /// Bundles the cumulative numerical effects of all active policies for a nation.
    /// Computed by <see cref="PolicySystem.ApplyPolicyEffects"/>.
    /// </summary>
    [Serializable]
    public struct PolicyEffectBundle
    {
        /// <summary>Total percentage modifier to military spending.</summary>
        public float militaryMod;
        /// <summary>Total percentage modifier to economic output.</summary>
        public float economicMod;
        /// <summary>Total percentage modifier to diplomacy effectiveness.</summary>
        public float diplomacyMod;
        /// <summary>Total flat modifier to national stability.</summary>
        public float stabilityMod;
        /// <summary>Total flat modifier to public approval.</summary>
        public float approvalMod;
        /// <summary>Number of active policies contributing to these effects.</summary>
        public int activeCount;
    }
}
