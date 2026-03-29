// =====================================================================
// IRON PROTOCOL - Election System
// Democratic elections, government management, and policy stances.
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.GameSystems.Elections
{
    // =================================================================
    // ENUMERATIONS
    // =================================================================

    /// <summary>
    /// Represents the form of government a nation currently employs.
    /// Each type affects election mechanics, policy freedom, and stability behavior.
    /// </summary>
    public enum GovernmentType
    {
        /// <summary>Full democratic elections with regular voting cycles.</summary>
        Democracy,
        /// <summary>Single ruler with absolute authority; no elections.</summary>
        Autocracy,
        /// <summary>Government guided by religious law and clerical authority.</summary>
        Theocracy,
        /// <summary>Military leadership in control; elections may be suspended.</summary>
        MilitaryJunta,
        /// <summary>Monarch with constitutionally limited powers alongside elected parliament.</summary>
        ConstitutionalMonarchy
    }

    /// <summary>
    /// Describes a nation's broad ideological stance on foreign relations and defense.
    /// Affects military spending, diplomacy costs, alliance availability, and war declarations.
    /// </summary>
    public enum PolicyStance
    {
        /// <summary>Aggressive defense posture; +20% military, easier war, harder diplomacy.</summary>
        Hawkish,
        /// <summary>Balanced center-ground approach with no major modifiers.</summary>
        Moderate,
        /// <summary>Peace-oriented; +20% diplomacy, harder war, cheaper alliances.</summary>
        Diplomatic,
        /// <summary>Non-interventionist; -50% trade, +30% defense, no alliances allowed.</summary>
        Isolationist
    }

    // =================================================================
    // DATA CLASSES
    // =================================================================

    /// <summary>
    /// Represents an individual candidate running in an election.
    /// Combines popularity, funding, and policy positions to determine electability.
    /// </summary>
    [Serializable]
    public class Candidate
    {
        /// <summary>Unique identifier for this candidate (e.g. "CAND_USA_001").</summary>
        public string candidateId;

        /// <summary>Display name shown in UI and election results.</summary>
        public string name;

        /// <summary>Political party or coalition affiliation.</summary>
        public string party;

        /// <summary>Stance on foreign policy and international relations.</summary>
        public PolicyStance foreignPolicy;

        /// <summary>Stance on domestic issues and internal governance.</summary>
        public PolicyStance domesticPolicy;

        /// <summary>Public popularity rating from 0 to 100.</summary>
        [Range(0f, 100f)] public float popularity;

        /// <summary>Campaign funding level that influences vote share (arbitrary units).</summary>
        public float funding;

        /// <summary>Portrait sprite rendered in election UI panels.</summary>
        public Sprite portrait;

        /// <summary>
        /// Creates a new candidate with all required fields.
        /// </summary>
        /// <param name="candidateId">Unique candidate identifier.</param>
        /// <param name="name">Candidate display name.</param>
        /// <param name="party">Political party name.</param>
        /// <param name="foreignPolicy">Foreign policy stance.</param>
        /// <param name="domesticPolicy">Domestic policy stance.</param>
        /// <param name="popularity">Initial popularity 0-100.</param>
        /// <param name="funding">Campaign funding amount.</param>
        /// <param name="portrait">Optional portrait sprite.</param>
        public Candidate(
            string candidateId,
            string name,
            string party,
            PolicyStance foreignPolicy,
            PolicyStance domesticPolicy,
            float popularity,
            float funding,
            Sprite portrait = null)
        {
            this.candidateId = candidateId;
            this.name = name;
            this.party = party;
            this.foreignPolicy = foreignPolicy;
            this.domesticPolicy = domesticPolicy;
            this.popularity = Mathf.Clamp(popularity, 0f, 100f);
            this.funding = Mathf.Max(0f, funding);
            this.portrait = portrait;
        }
    }

    /// <summary>
    /// Contains the outcome of a completed election, including winner info,
    /// policy shifts, voter turnout, and a human-readable summary.
    /// </summary>
    [Serializable]
    public class ElectionResult
    {
        /// <summary>Candidate ID of the election winner.</summary>
        public string winnerId;

        /// <summary>Party name of the winning candidate.</summary>
        public string winningParty;

        /// <summary>Foreign policy stance that will be adopted by the new government.</summary>
        public PolicyStance newForeignPolicy;

        /// <summary>Domestic policy stance that will be adopted by the new government.</summary>
        public PolicyStance newDomesticPolicy;

        /// <summary>Overall approval rating of the new government immediately after the election.</summary>
        [Range(0f, 100f)] public float approvalRating;

        /// <summary>Percentage of eligible voters who participated (0-100).</summary>
        [Range(0, 100)] public int voterTurnout;

        /// <summary>Human-readable summary of the election outcome for event logs and UI.</summary>
        public string summary;

        /// <summary>Whether the election was legitimate or affected by fraud/cancellation.</summary>
        public bool wasLegitimate;

        /// <summary>
        /// Creates a complete election result.
        /// </summary>
        public ElectionResult(
            string winnerId, string winningParty,
            PolicyStance newForeignPolicy, PolicyStance newDomesticPolicy,
            float approvalRating, int voterTurnout,
            string summary, bool wasLegitimate = true)
        {
            this.winnerId = winnerId;
            this.winningParty = winningParty;
            this.newForeignPolicy = newForeignPolicy;
            this.newDomesticPolicy = newDomesticPolicy;
            this.approvalRating = Mathf.Clamp(approvalRating, 0f, 100f);
            this.voterTurnout = Mathf.Clamp(voterTurnout, 0, 100);
            this.summary = summary;
            this.wasLegitimate = wasLegitimate;
        }
    }

    /// <summary>
    /// Represents the current state of a nation's government, including leadership,
    /// policy stances, stability metrics, and active legislation.
    /// </summary>
    [Serializable]
    public class Government
    {
        /// <summary>The current form of government.</summary>
        public GovernmentType type;

        /// <summary>Name or ID of the current head of state / leader.</summary>
        public string currentLeader;

        /// <summary>Name of the currently ruling political party or faction.</summary>
        public string rulingParty;

        /// <summary>Current foreign policy stance affecting diplomacy and warfare.</summary>
        public PolicyStance foreignPolicy;

        /// <summary>Current domestic policy stance affecting internal affairs.</summary>
        public PolicyStance domesticPolicy;

        /// <summary>National stability rating from 0 (collapse) to 100 (rock-solid).</summary>
        [Range(0f, 100f)] public float stability;

        /// <summary>Public approval of the government from 0 to 100.</summary>
        [Range(0f, 100f)] public float approval;

        /// <summary>Corruption level from 0 (clean) to 100 (rampant).</summary>
        [Range(0f, 100f)] public float corruption;

        /// <summary>Number of consecutive terms the current party has been in power.</summary>
        public int termsInOffice;

        /// <summary>Turns remaining until the next scheduled election.</summary>
        public int nextElectionInTurns;

        /// <summary>List of currently active policy IDs that modify nation stats.</summary>
        public List<string> activePolicies = new List<string>();

        /// <summary>Economy health factor (0-100) used in stability calculations.</summary>
        [Range(0f, 100f)] public float economyHealth;

        /// <summary>War fatigue metric (0-100); high values destabilize the government.</summary>
        [Range(0f, 100f)] public float warFatigue;

        /// <summary>Total military casualties suffered (used in stability calculations).</summary>
        public int totalCasualties;

        /// <summary>Nation identifier this government belongs to.</summary>
        [HideInInspector] public string nationId;

        /// <summary>
        /// Creates a default government with the specified type.
        /// </summary>
        public Government(string nationId, GovernmentType type = GovernmentType.Democracy)
        {
            this.nationId = nationId;
            this.type = type;
            this.stability = 70f;
            this.approval = 60f;
            this.corruption = 15f;
            this.termsInOffice = 1;
            this.nextElectionInTurns = 10;
            this.economyHealth = 60f;
            this.warFatigue = 0f;
            this.totalCasualties = 0;
            this.foreignPolicy = PolicyStance.Moderate;
            this.domesticPolicy = PolicyStance.Moderate;
            this.currentLeader = string.Empty;
            this.rulingParty = string.Empty;
        }
    }

    // =================================================================
    // ELECTION SYSTEM
    // =================================================================

    /// <summary>
    /// Core system managing democratic elections, government state, policy stances,
    /// coups, propaganda, and turn-based government evolution.
    /// Attach to a persistent GameObject (e.g. GameManager) as a singleton.
    /// </summary>
    public class ElectionSystem : MonoBehaviour
    {
        // -------------------------------------------------------------
        // CONSTANTS
        // -------------------------------------------------------------
        private const float MinStabilityForFairElection = 20f;
        private const int ElectionIntervalTurns = 10;
        private const float PopularityWeight = 0.4f;
        private const float FundingWeight = 0.3f;
        private const float WarApprovalWeight = 0.2f;
        private const float RandomWeight = 0.1f;
        private const float HawkishMilitaryModifier = 0.2f;
        private const float DiplomaticDiplomacyModifier = 0.2f;
        private const float IsolationistTradePenalty = 0.5f;
        private const float IsolationistDefenseBonus = 0.3f;
        private const float PropagandaMinBoost = 5f;
        private const float PropagandaMaxBoost = 15f;
        private const float OverthrowStabilityThreshold = 15f;

        // -------------------------------------------------------------
        // STATE
        // -------------------------------------------------------------
        private readonly Dictionary<string, Government> _governments = new Dictionary<string, Government>();
        private int _currentTurn = 0;

        // -------------------------------------------------------------
        // EVENTS
        // -------------------------------------------------------------
        /// <summary>Fired when an election concludes. Parameters: (nationId, result).</summary>
        public event Action<string, ElectionResult> OnElectionHeld;

        /// <summary>Fired when a government type changes (including overthrows). Parameters: (nationId, newType).</summary>
        public event Action<string, GovernmentType> OnGovernmentChanged;

        // -------------------------------------------------------------
        // PROPERTIES
        // -------------------------------------------------------------
        /// <summary>Current global turn counter.</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>Number of governments currently tracked.</summary>
        public int GovernmentCount => _governments.Count;

        // =============================================================
        // INITIALIZATION
        // =============================================================

        /// <summary>
        /// Creates and registers a new government for the specified nation.
        /// </summary>
        /// <param name="nationId">Unique nation identifier.</param>
        /// <param name="type">Initial government type.</param>
        public void InitializeGovernment(string nationId, GovernmentType type)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogError("[ElectionSystem] InitializeGovernment: nationId is null or empty.");
                return;
            }

            if (_governments.ContainsKey(nationId))
            {
                Debug.LogWarning($"[ElectionSystem] Government already exists for nation '{nationId}'. Overwriting.");
                _governments.Remove(nationId);
            }

            var gov = new Government(nationId, type)
            {
                nextElectionInTurns = CanHoldElectionsByType(type) ? ElectionIntervalTurns : -1
            };

            _governments[nationId] = gov;
            Debug.Log($"[ElectionSystem] Government initialized for '{nationId}' as {type}. " +
                      $"Next election in {gov.nextElectionInTurns} turns.");
        }

        // =============================================================
        // GOVERNMENT ACCESSORS
        // =============================================================

        /// <summary>
        /// Retrieves the government data for a nation.
        /// </summary>
        /// <param name="nationId">Nation identifier to look up.</param>
        /// <returns>The <see cref="Government"/> for the nation, or null if not found.</returns>
        public Government GetGovernment(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogError("[ElectionSystem] GetGovernment: nationId is null or empty.");
                return null;
            }

            _governments.TryGetValue(nationId, out Government gov);
            if (gov == null)
                Debug.LogWarning($"[ElectionSystem] No government found for nation '{nationId}'.");

            return gov;
        }

        /// <summary>
        /// Returns all currently tracked governments.
        /// </summary>
        /// <returns>Read-only collection of governments keyed by nation ID.</returns>
        public Dictionary<string, Government>.ValueCollection GetAllGovernments()
        {
            return _governments.Values;
        }

        // =============================================================
        // GOVERNMENT TYPE MANAGEMENT
        // =============================================================

        /// <summary>
        /// Changes the government type for a nation. Sudden changes (especially from Democracy)
        /// cause stability and approval penalties proportional to the disruption.
        /// </summary>
        /// <param name="nationId">Nation to modify.</param>
        /// <param name="type">New government type.</param>
        public void SetGovernmentType(string nationId, GovernmentType type)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return;

            GovernmentType oldType = gov.type;
            if (oldType == type)
            {
                Debug.Log($"[ElectionSystem] Nation '{nationId}' already has type {type}. No change.");
                return;
            }

            // --- Calculate unrest from sudden change ---
            float unrestPenalty = CalculateUnrestPenalty(oldType, type);

            gov.type = type;
            gov.stability = Mathf.Clamp(gov.stability - unrestPenalty, 0f, 100f);
            gov.approval = Mathf.Clamp(gov.approval - (unrestPenalty * 0.6f), 0f, 100f);
            gov.nextElectionInTurns = CanHoldElectionsByType(type) ? ElectionIntervalTurns : -1;

            Debug.Log($"[ElectionSystem] Nation '{nationId}' changed from {oldType} to {type}. " +
                      $"Stability penalty: -{unrestPenalty:F1}, Approval penalty: -{unrestPenalty * 0.6f:F1}.");

            OnGovernmentChanged?.Invoke(nationId, type);
        }

        /// <summary>
        /// Calculates the stability penalty from changing government types.
        /// Moving away from Democracy or toward Autocracy is more destabilizing.
        /// </summary>
        private static float CalculateUnrestPenalty(GovernmentType oldType, GovernmentType newType)
        {
            float penalty = 5f; // base disruption

            if (oldType == GovernmentType.Democracy && newType != GovernmentType.Democracy)
                penalty += 20f;

            if (newType == GovernmentType.Autocracy || newType == GovernmentType.MilitaryJunta)
                penalty += 15f;

            if (oldType == GovernmentType.Autocracy && newType == GovernmentType.ConstitutionalMonarchy)
                penalty -= 5f;

            return Mathf.Max(penalty, 0f);
        }

        /// <summary>
        /// Determines whether a given government type permits elections.
        /// </summary>
        private static bool CanHoldElectionsByType(GovernmentType type)
        {
            return type == GovernmentType.Democracy || type == GovernmentType.ConstitutionalMonarchy;
        }

        // =============================================================
        // TURN ADVANCEMENT
        // =============================================================

        /// <summary>
        /// Advances the government simulation by one turn. Updates stability, approval,
        /// corruption, and checks if an election should trigger.
        /// </summary>
        /// <param name="nationId">Nation to advance.</param>
        public void AdvanceTurn(string nationId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return;

            _currentTurn++;

            // --- Stability evolution ---
            float stabilityDelta = 0f;

            if (gov.approval > 60f)
                stabilityDelta += 1.5f;
            else if (gov.approval < 30f)
                stabilityDelta -= 3f;

            if (gov.economyHealth > 70f)
                stabilityDelta += 1f;
            else if (gov.economyHealth < 30f)
                stabilityDelta -= 2f;

            if (gov.warFatigue > 50f)
                stabilityDelta -= (gov.warFatigue - 50f) * 0.05f;

            stabilityDelta -= gov.corruption * 0.02f;

            // Natural reversion toward mean stability (50)
            if (gov.stability > 60f)
                stabilityDelta -= 0.5f;
            else if (gov.stability < 40f)
                stabilityDelta += 0.5f;

            gov.stability = Mathf.Clamp(gov.stability + stabilityDelta, 0f, 100f);

            // --- Approval evolution ---
            float approvalDelta = 0f;

            if (gov.stability > 70f)
                approvalDelta += 1f;
            else if (gov.stability < 30f)
                approvalDelta -= 2f;

            if (gov.economyHealth > 60f)
                approvalDelta += 0.5f;
            else if (gov.economyHealth < 40f)
                approvalDelta -= 1.5f;

            approvalDelta -= gov.corruption * 0.03f;
            approvalDelta -= gov.warFatigue * 0.04f;

            gov.approval = Mathf.Clamp(gov.approval + approvalDelta, 0f, 100f);

            // --- Corruption evolution ---
            float corruptionDelta = 0f;
            if (gov.stability < 40f)
                corruptionDelta += 0.5f;
            else if (gov.stability > 80f && gov.approval > 70f)
                corruptionDelta -= 0.2f;

            gov.corruption = Mathf.Clamp(gov.corruption + corruptionDelta, 0f, 100f);

            // --- Check for auto-overthrow if stability critically low ---
            if (gov.stability < OverthrowStabilityThreshold)
            {
                float overthrowChance = (OverthrowStabilityThreshold - gov.stability) * 0.02f;
                if (UnityEngine.Random.value < overthrowChance)
                {
                    GovernmentOverthrow(nationId);
                    return;
                }
            }

            // --- Election countdown ---
            if (gov.nextElectionInTurns > 0)
            {
                gov.nextElectionInTurns--;
                if (gov.nextElectionInTurns <= 0)
                {
                    if (CanHoldElectionsByType(gov.type))
                    {
                        var candidates = GenerateDefaultCandidates(nationId, gov);
                        HoldElection(nationId, candidates);
                    }
                    else
                    {
                        gov.nextElectionInTurns = -1;
                    }
                }
            }

            Debug.Log($"[ElectionSystem] Turn {_currentTurn} for '{nationId}': " +
                      $"Stability={gov.stability:F1}, Approval={gov.approval:F1}, Corruption={gov.corruption:F1}");
        }

        // =============================================================
        // STABILITY CALCULATION
        // =============================================================

        /// <summary>
        /// Calculates a comprehensive stability score for a nation based on multiple factors:
        /// approval, economy, war fatigue, and casualties.
        /// </summary>
        /// <param name="nationId">Nation to evaluate.</param>
        /// <returns>Stability score from 0 to 100.</returns>
        public float CalculateStability(string nationId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return 0f;

            float approvalFactor = gov.approval * 0.3f;
            float economyFactor = gov.economyHealth * 0.25f;
            float warFatiguePenalty = gov.warFatigue * 0.2f;
            float corruptionPenalty = gov.corruption * 0.1f;
            float casualtyPenalty = Mathf.Min(Mathf.Log10(Mathf.Max(1, gov.totalCasualties)) * 5f, 25f);

            float stability = approvalFactor + economyFactor - warFatiguePenalty
                              - corruptionPenalty - casualtyPenalty;

            if (gov.corruption < 20f)
                stability += 5f;

            foreach (string policyId in gov.activePolicies)
            {
                if (policyId.Contains("Censorship") || policyId.Contains("Secret"))
                    stability -= 2f;
            }

            return Mathf.Clamp(stability, 0f, 100f);
        }

        // =============================================================
        // ELECTION LOGIC
        // =============================================================

        /// <summary>
        /// Holds a democratic election for a nation with the given candidates.
        /// Each candidate's vote share is determined by:
        /// <list type="bullet">
        ///   <item>Popularity (40%)</item>
        ///   <item>Funding (30%)</item>
        ///   <item>War approval alignment (20%)</item>
        ///   <item>Random factor (10%)</item>
        /// </list>
        /// If stability is below 20, there is a risk of election fraud or cancellation.
        /// The winner's policy stances become the new government's stances.
        /// </summary>
        /// <param name="nationId">Nation holding the election.</param>
        /// <param name="candidates">List of candidates running. Must have at least 2.</param>
        /// <returns>The <see cref="ElectionResult"/> with full outcome data, or null on failure.</returns>
        public ElectionResult HoldElection(string nationId, List<Candidate> candidates)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null)
            {
                Debug.LogError($"[ElectionSystem] Cannot hold election: no government for '{nationId}'.");
                return null;
            }

            if (!CanHoldElectionsByType(gov.type))
            {
                Debug.LogWarning($"[ElectionSystem] Nation '{nationId}' has type {gov.type}; elections not permitted.");
                return null;
            }

            if (candidates == null || candidates.Count < 2)
            {
                Debug.LogError("[ElectionSystem] At least 2 candidates required for an election.");
                return null;
            }

            // --- Check stability for election integrity ---
            bool wasLegitimate = true;

            if (gov.stability < MinStabilityForFairElection)
            {
                float fraudChance = (MinStabilityForFairElection - gov.stability) / MinStabilityForFairElection;
                float roll = UnityEngine.Random.value;

                if (roll < fraudChance * 0.5f)
                {
                    // Election cancelled
                    var cancelResult = new ElectionResult(
                        string.Empty, gov.rulingParty,
                        gov.foreignPolicy, gov.domesticPolicy,
                        gov.approval, 0,
                        "Election cancelled due to government instability. Current regime retains power.",
                        false
                    );

                    gov.nextElectionInTurns = ElectionIntervalTurns;
                    OnElectionHeld?.Invoke(nationId, cancelResult);
                    return cancelResult;
                }

                if (roll < fraudChance)
                {
                    wasLegitimate = false;
                    Debug.LogWarning($"[ElectionSystem] Election FRAUD detected for '{nationId}'.");
                }
            }

            // --- Calculate war approval factor ---
            float warApprovalFactor = gov.warFatigue > 50f ? 0.3f : (gov.warFatigue < 20f ? 0.8f : 0.5f);

            // --- Score each candidate ---
            var scoredCandidates = new List<(Candidate candidate, float score)>();
            float totalScore = 0f;

            foreach (Candidate candidate in candidates)
            {
                float popularityScore = candidate.popularity * PopularityWeight;
                float fundingScore = Mathf.Min(candidate.funding, 100f) * FundingWeight;

                float warScore = 0f;
                if (candidate.foreignPolicy == PolicyStance.Hawkish)
                    warScore = warApprovalFactor * WarApprovalWeight * 100f;
                else if (candidate.foreignPolicy == PolicyStance.Diplomatic)
                    warScore = (1f - warApprovalFactor) * WarApprovalWeight * 100f;
                else
                    warScore = 50f * WarApprovalWeight;

                float randomScore = UnityEngine.Random.Range(0f, 100f) * RandomWeight;
                float totalCandidateScore = popularityScore + fundingScore + warScore + randomScore;

                // If fraud, give a bias to the ruling party candidate
                if (!wasLegitimate && !string.IsNullOrEmpty(gov.rulingParty) &&
                    candidate.party == gov.rulingParty)
                {
                    totalCandidateScore *= 1.3f;
                }

                scoredCandidates.Add((candidate, totalCandidateScore));
                totalScore += totalCandidateScore;
            }

            // --- Determine winner ---
            scoredCandidates.Sort((a, b) => b.score.CompareTo(a.score));

            Candidate winner = scoredCandidates[0].candidate;
            float winnerVoteShare = (scoredCandidates[0].score / totalScore) * 100f;

            // --- Voter turnout based on stability and engagement ---
            int baseTurnout = 65;
            if (gov.stability > 60f) baseTurnout += 10;
            if (gov.stability < 30f) baseTurnout -= 20;
            if (gov.approval > 70f) baseTurnout += 5;
            if (gov.approval < 30f) baseTurnout -= 10;
            baseTurnout += UnityEngine.Random.Range(-5, 5);
            int voterTurnout = Mathf.Clamp(baseTurnout, 10, 95);

            // --- Apply results ---
            gov.currentLeader = winner.name;
            gov.rulingParty = winner.party;
            gov.foreignPolicy = winner.foreignPolicy;
            gov.domesticPolicy = winner.domesticPolicy;
            gov.approval = Mathf.Clamp(50f + (winnerVoteShare - 50f) * 0.5f, 30f, 95f);
            gov.termsInOffice++;
            gov.nextElectionInTurns = ElectionIntervalTurns;

            ApplyPolicyStanceEffects(gov, winner.foreignPolicy);

            gov.stability = Mathf.Clamp(
                gov.stability + (wasLegitimate ? 5f : -10f), 0f, 100f);

            // --- Build result ---
            string fraudNote = wasLegitimate ? "" : " [IRREGULARITIES REPORTED]";
            string summary = $"{winner.name} ({winner.party}) wins with {winnerVoteShare:F1}% of the vote. " +
                             $"Turnout: {voterTurnout}%.{fraudNote} " +
                             $"New foreign policy: {winner.foreignPolicy}. " +
                             $"New domestic policy: {winner.domesticPolicy}.";

            var result = new ElectionResult(
                winner.candidateId,
                winner.party,
                winner.foreignPolicy,
                winner.domesticPolicy,
                gov.approval,
                voterTurnout,
                summary,
                wasLegitimate
            );

            Debug.Log($"[ElectionSystem] Election held for '{nationId}': {summary}");
            OnElectionHeld?.Invoke(nationId, result);
            return result;
        }

        /// <summary>
        /// Applies stat modifiers based on the new foreign policy stance.
        /// </summary>
        private void ApplyPolicyStanceEffects(Government gov, PolicyStance stance)
        {
            switch (stance)
            {
                case PolicyStance.Hawkish:
                    gov.economyHealth = Mathf.Clamp(gov.economyHealth - 5f, 0f, 100f);
                    gov.stability = Mathf.Clamp(gov.stability + 3f, 0f, 100f);
                    Debug.Log($"[ElectionSystem] Hawkish: Military +{HawkishMilitaryModifier * 100f}%, diplomacy harder.");
                    break;

                case PolicyStance.Diplomatic:
                    gov.stability = Mathf.Clamp(gov.stability + 2f, 0f, 100f);
                    gov.approval = Mathf.Clamp(gov.approval + 5f, 0f, 100f);
                    Debug.Log($"[ElectionSystem] Diplomatic: Diplomacy +{DiplomaticDiplomacyModifier * 100f}%, war harder.");
                    break;

                case PolicyStance.Isolationist:
                    gov.economyHealth = Mathf.Clamp(gov.economyHealth - 10f, 0f, 100f);
                    gov.stability = Mathf.Clamp(gov.stability + 5f, 0f, 100f);
                    Debug.Log($"[ElectionSystem] Isolationist: Trade -{IsolationistTradePenalty * 100f}%, " +
                              $"Defense +{IsolationistDefenseBonus * 100f}%, no alliances.");
                    break;

                case PolicyStance.Moderate:
                    Debug.Log("[ElectionSystem] Moderate: Balanced approach, no major modifiers.");
                    break;
            }
        }

        // =============================================================
        // POLICY MANAGEMENT
        // =============================================================

        /// <summary>
        /// Enacts a policy for a nation, adding it to the government's active policy list.
        /// </summary>
        /// <param name="nationId">Nation to apply the policy to.</param>
        /// <param name="policyId">Unique policy identifier.</param>
        public void ApplyPolicy(string nationId, string policyId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return;

            if (string.IsNullOrEmpty(policyId))
            {
                Debug.LogError("[ElectionSystem] ApplyPolicy: policyId is null or empty.");
                return;
            }

            if (gov.activePolicies.Contains(policyId))
            {
                Debug.LogWarning($"[ElectionSystem] Policy '{policyId}' already active for '{nationId}'.");
                return;
            }

            gov.activePolicies.Add(policyId);
            Debug.Log($"[ElectionSystem] Policy '{policyId}' enacted for '{nationId}'. " +
                      $"Active policies: {gov.activePolicies.Count}");
        }

        /// <summary>
        /// Revokes an active policy from a nation's government.
        /// </summary>
        /// <param name="nationId">Nation to revoke from.</param>
        /// <param name="policyId">Policy identifier to remove.</param>
        public void RevokePolicy(string nationId, string policyId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return;

            if (gov.activePolicies.Remove(policyId))
            {
                Debug.Log($"[ElectionSystem] Policy '{policyId}' revoked for '{nationId}'. " +
                          $"Active policies: {gov.activePolicies.Count}");
            }
            else
            {
                Debug.LogWarning($"[ElectionSystem] Policy '{policyId}' was not active for '{nationId}'.");
            }
        }

        // =============================================================
        // GOVERNMENT OVERTHROW
        // =============================================================

        /// <summary>
        /// Triggers a government overthrow (revolution or coup) for a nation.
        /// Dramatically changes government type, resets leadership, and heavily impacts stability.
        /// </summary>
        /// <param name="nationId">Nation experiencing the overthrow.</param>
        public void GovernmentOverthrow(string nationId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return;

            // Determine new government type based on conditions
            GovernmentType newType;
            if (gov.corruption > 60f)
                newType = GovernmentType.Autocracy;
            else if (gov.warFatigue > 70f || gov.stability < 10f)
                newType = GovernmentType.MilitaryJunta;
            else
            {
                float roll = UnityEngine.Random.value;
                newType = roll < 0.5f ? GovernmentType.MilitaryJunta
                         : roll < 0.8f ? GovernmentType.Autocracy
                         : GovernmentType.Theocracy;
            }

            GovernmentType oldType = gov.type;
            gov.type = newType;
            gov.currentLeader = "[Overthrown]";
            gov.rulingParty = "[Interim Government]";
            gov.stability = Mathf.Clamp(gov.stability - 30f, 5f, 50f);
            gov.approval = Mathf.Clamp(gov.approval - 20f, 10f, 50f);
            gov.corruption = Mathf.Clamp(gov.corruption + 10f, 0f, 100f);
            gov.termsInOffice = 1;
            gov.nextElectionInTurns = CanHoldElectionsByType(newType) ? ElectionIntervalTurns : -1;
            gov.activePolicies.Clear();

            Debug.LogWarning($"[ElectionSystem] *** GOVERNMENT OVERTHROW in '{nationId}' *** " +
                             $"Changed from {oldType} to {newType}. " +
                             $"Stability={gov.stability:F1}, Approval={gov.approval:F1}");

            OnGovernmentChanged?.Invoke(nationId, newType);
        }

        // =============================================================
        // PROPAGANDA
        // =============================================================

        /// <summary>
        /// Launches a propaganda campaign to boost government approval.
        /// More expensive campaigns are more effective but risk backlash if corruption is high.
        /// </summary>
        /// <param name="nationId">Nation running the campaign.</param>
        /// <param name="cost">Treasury cost of the campaign (higher = more effective).</param>
        /// <returns>Actual approval boost achieved (5-15 points).</returns>
        public float PropagandaCampaign(string nationId, float cost)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return 0f;

            if (cost <= 0f)
            {
                Debug.LogError("[ElectionSystem] PropagandaCampaign: cost must be positive.");
                return 0f;
            }

            float effectiveness = Mathf.Clamp(cost / 1000f, 0f, 1f);
            float boost = UnityEngine.Random.Range(PropagandaMinBoost, PropagandaMaxBoost) * effectiveness;

            // Corruption reduces effectiveness
            if (gov.corruption > 50f)
                boost *= (1f - (gov.corruption - 50f) / 100f);

            float oldApproval = gov.approval;
            gov.approval = Mathf.Clamp(gov.approval + boost, 0f, 100f);
            float actualBoost = gov.approval - oldApproval;

            gov.corruption = Mathf.Clamp(gov.corruption + 1f * effectiveness, 0f, 100f);

            Debug.Log($"[ElectionSystem] Propaganda for '{nationId}' cost {cost:F0}. " +
                      $"Approval: {oldApproval:F1} -> {gov.approval:F1} (+{actualBoost:F1})");

            return actualBoost;
        }

        // =============================================================
        // ELECTION ELIGIBILITY
        // =============================================================

        /// <summary>
        /// Checks whether a nation is eligible to hold democratic elections.
        /// Democracies hold elections every 10 turns; non-democratic systems cannot hold elections.
        /// </summary>
        /// <param name="nationId">Nation to check.</param>
        /// <returns>True if the nation can hold elections now.</returns>
        public bool CanHoldElections(string nationId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null) return false;
            if (!CanHoldElectionsByType(gov.type)) return false;
            return gov.nextElectionInTurns <= 0;
        }

        /// <summary>
        /// Returns the number of turns until the next election, or -1 if not scheduled.
        /// </summary>
        public int TurnsUntilElection(string nationId)
        {
            Government gov = GetGovernment(nationId);
            if (gov == null || !CanHoldElectionsByType(gov.type)) return -1;
            return gov.nextElectionInTurns;
        }

        // =============================================================
        // CANDIDATE GENERATION
        // =============================================================

        /// <summary>
        /// Generates a set of default candidates for a nation when none are provided.
        /// Creates 2-4 candidates with diverse policy stances.
        /// </summary>
        private List<Candidate> GenerateDefaultCandidates(string nationId, Government gov)
        {
            var candidates = new List<Candidate>();
            var stances = new[] { PolicyStance.Hawkish, PolicyStance.Diplomatic,
                                  PolicyStance.Moderate, PolicyStance.Isolationist };
            var partyNames = new[] { "Patriot Party", "Liberty Alliance",
                                      "Unity Coalition", "Sovereignty Front" };

            int count = UnityEngine.Random.Range(2, 5);
            for (int i = 0; i < count; i++)
            {
                PolicyStance stance = stances[i % stances.Length];
                string candidateId = $"CAND_{nationId}_{i:D3}";
                string partyName = partyNames[i % partyNames.Length];

                float popularity = UnityEngine.Random.Range(20f, 80f);
                float funding = UnityEngine.Random.Range(10f, 100f);

                // Incumbent ruling party gets a bonus
                if (!string.IsNullOrEmpty(gov.rulingParty) && partyName == gov.rulingParty)
                {
                    popularity += 15f;
                    funding += 20f;
                }

                // War fatigue affects hawkish/diplomatic candidate popularity
                if (stance == PolicyStance.Hawkish && gov.warFatigue > 40f)
                    popularity -= 20f;
                if (stance == PolicyStance.Diplomatic && gov.warFatigue > 40f)
                    popularity += 15f;

                popularity = Mathf.Clamp(popularity, 5f, 95f);
                funding = Mathf.Clamp(funding, 5f, 150f);

                var candidate = new Candidate(
                    candidateId,
                    GenerateCandidateName(),
                    partyName,
                    stance,
                    (PolicyStance)UnityEngine.Random.Range(0, 4),
                    popularity,
                    funding
                );

                candidates.Add(candidate);
            }

            return candidates;
        }

        /// <summary>
        /// Generates a random candidate name from predefined first/last name pools.
        /// </summary>
        private static string GenerateCandidateName()
        {
            string[] firstNames = { "James", "Maria", "Alexander", "Sofia", "Chen",
                                     "Ahmed", "Elena", "William", "Yuki", "Olga" };
            string[] lastNames = { "Harrison", "Petrov", "Nakamura", "Al-Rashid", "Mueller",
                                    "Santos", "Kim", "Okafor", "Thompson", "Singh" };

            string first = firstNames[UnityEngine.Random.Range(0, firstNames.Length)];
            string last = lastNames[UnityEngine.Random.Range(0, lastNames.Length)];
            return $"{first} {last}";
        }

        // =============================================================
        // UTILITY
        // =============================================================

        /// <summary>
        /// Removes all tracked governments. Use when resetting the game state.
        /// </summary>
        public void Reset()
        {
            _governments.Clear();
            _currentTurn = 0;
            Debug.Log("[ElectionSystem] All government data cleared.");
        }

        /// <summary>
        /// Returns whether a government exists for the given nation.
        /// </summary>
        public bool HasGovernment(string nationId)
        {
            return !string.IsNullOrEmpty(nationId) && _governments.ContainsKey(nationId);
        }
    }
}
