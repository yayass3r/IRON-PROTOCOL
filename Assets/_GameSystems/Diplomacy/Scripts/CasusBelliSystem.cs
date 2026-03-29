// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: CasusBelliSystem.cs
// Description: Complete Casus Belli (just cause for war) system with fabrication,
//              incident creation, and war penalty calculation.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.Diplomacy
{
    /// <summary>
    /// Defines the type of Casus Belli available to nations.
    /// Each type carries different diplomatic costs and aggression penalties.
    /// </summary>
    public enum CBType
    {
        /// <summary>No Casus Belli. Declaring war without one incurs maximum penalties.</summary>
        None = 0,

        /// <summary>A fabricated territorial claim, created over multiple turns through espionage.</summary>
        FabricatedClaim = 1,

        /// <summary>Triggered by a border skirmish or incursion from the target nation.</summary>
        BorderIncident = 2,

        /// <summary>Justified by atrocities, famine, or oppression in the target nation.</summary>
        HumanitarianIntervention = 3,

        /// <summary>Triggered when an ally is attacked; allows coming to their defense.</summary>
        DefensivePact = 4,

        /// <summary>No justification at all. Maximum aggression penalty and diplomatic fallout.</summary>
        SurpriseAttack = 5
    }

    /// <summary>
    /// Represents a single Casus Belli instance. Contains all metadata about the
    /// justification for war, including type, target, timing, and penalties.
    /// </summary>
    [System.Serializable]
    public class CasusBelli
    {
        // --------------------------------------------------------------------- //
        // Core Data
        // --------------------------------------------------------------------- //

        /// <summary>The type of this Casus Belli.</summary>
        [SerializeField] private CBType type;

        /// <summary>Nation that created this Casus Belli.</summary>
        [SerializeField] private string creatorNationId;

        /// <summary>Nation targeted by this Casus Belli.</summary>
        [SerializeField] private string targetNationId;

        /// <summary>Human-readable description of why this CB exists.</summary>
        [SerializeField] private string description;

        /// <summary>Game turn when this Casus Belli was created.</summary>
        [SerializeField] private int turnCreated;

        /// <summary>Game turn when this Casus Belli expires and can no longer be used.</summary>
        [SerializeField] private int expiryTurn;

        // --------------------------------------------------------------------- //
        // Penalty Data
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Diplomatic opinion cost applied to all other nations when this CB is used.
        /// Higher values = more nations dislike you for using this CB.
        /// </summary>
        [SerializeField] private float diplomaticCost;

        /// <summary>
        /// Aggression penalty applied to the declaring nation's standing.
        /// Affects trade efficiency, alliance willingness, and international reputation.
        /// </summary>
        [SerializeField] private float aggressionPenalty;

        // --------------------------------------------------------------------- //
        // Fabrication State (only for FabricatedClaim)
        // --------------------------------------------------------------------- //

        /// <summary>Whether this CB is still being fabricated (not yet ready).</summary>
        [SerializeField] private bool isFabricating;

        /// <summary>Turns remaining until fabrication is complete.</summary>
        [SerializeField] private int turnsToCompleteFabrication;

        /// <summary>Total turns required for fabrication.</summary>
        [SerializeField] private int totalFabricationTurns;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>The type of this Casus Belli.</summary>
        public CBType Type => type;

        /// <summary>The nation that created this Casus Belli.</summary>
        public string CreatorNationId => creatorNationId;

        /// <summary>The nation targeted by this Casus Belli.</summary>
        public string TargetNationId => targetNationId;

        /// <summary>Human-readable description.</summary>
        public string Description => description;

        /// <summary>Game turn when this CB was created.</summary>
        public int TurnCreated => turnCreated;

        /// <summary>Game turn when this CB expires.</summary>
        public int ExpiryTurn => expiryTurn;

        /// <summary>Diplomatic opinion cost when used.</summary>
        public float DiplomaticCost => diplomaticCost;

        /// <summary>Aggression penalty when used.</summary>
        public float AggressionPenalty => aggressionPenalty;

        /// <summary>Whether this CB is still being fabricated.</summary>
        public bool IsFabricating => isFabricating;

        /// <summary>Turns remaining until fabrication completes.</summary>
        public int TurnsToCompleteFabrication => turnsToCompleteFabrication;

        /// <summary>Whether this CB has expired.</summary>
        public bool IsExpired => expiryTurn > 0 && turnCreated >= expiryTurn;

        /// <summary>Whether this CB is ready to be used for war declaration.</summary>
        public bool IsReady => !isFabricating && !IsExpired && type != CBType.None;

        // --------------------------------------------------------------------- //
        // Constructor
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Creates a new CasusBelli instance.
        /// </summary>
        /// <param name="type">Type of justification.</param>
        /// <param name="creatorNationId">Nation creating the CB.</param>
        /// <param name="targetNationId">Nation targeted by the CB.</param>
        /// <param name="description">Human-readable description.</param>
        /// <param name="currentTurn">Current game turn (for creation and expiry calculation).</param>
        /// <param name="turnsUntilExpiry">How many turns until this CB expires. 0 = never expires.</param>
        public CasusBelli(CBType type, string creatorNationId, string targetNationId,
                          string description, int currentTurn, int turnsUntilExpiry = 0)
        {
            this.type = type;
            this.creatorNationId = creatorNationId;
            this.targetNationId = targetNationId;
            this.description = description;
            this.turnCreated = currentTurn;
            this.expiryTurn = turnsUntilExpiry > 0 ? currentTurn + turnsUntilExpiry : 0;

            // Set penalties based on CB type
            (diplomaticCost, aggressionPenalty) = GetPenaltiesForType(type);

            isFabricating = false;
            turnsToCompleteFabrication = 0;
            totalFabricationTurns = 0;
        }

        /// <summary>Parameterless constructor for serialization.</summary>
        public CasusBelli() { }

        // --------------------------------------------------------------------- //
        // Internal Methods
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Sets fabrication parameters. Called by CasusBelliSystem.FabricateClaim().
        /// </summary>
        internal void SetFabrication(int turnsRequired)
        {
            isFabricating = true;
            turnsToCompleteFabrication = turnsRequired;
            totalFabricationTurns = turnsRequired;
        }

        /// <summary>
        /// Advances fabrication by one turn. Called each turn by CasusBelliSystem.
        /// </summary>
        /// <returns>True if fabrication completed this turn.</returns>
        internal bool AdvanceFabrication()
        {
            if (!isFabricating) return false;

            turnsToCompleteFabrication--;

            if (turnsToCompleteFabrication <= 0)
            {
                isFabricating = false;
                turnsToCompleteFabrication = 0;
                Debug.Log($"[CasusBelli] Fabricated claim against '{targetNationId}' is now ready.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns default penalties for each CB type.
        /// </summary>
        private static (float diplomaticCost, float aggressionPenalty) GetPenaltiesForType(CBType cbType)
        {
            switch (cbType)
            {
                case CBType.FabricatedClaim:
                    return (15f, 20f);

                case CBType.BorderIncident:
                    return (10f, 10f);

                case CBType.HumanitarianIntervention:
                    return (5f, 5f);

                case CBType.DefensivePact:
                    return (0f, 0f); // No penalty for defensive wars

                case CBType.SurpriseAttack:
                    return (40f, 50f);

                case CBType.None:
                default:
                    return (30f, 40f);
            }
        }
    }

    /// <summary>
    /// Manages the creation, tracking, and validation of all Casus Belli in the game.
    /// Provides factory methods for each CB type and validates war declarations.
    /// </summary>
    public class CasusBelliSystem : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Fired when a new Casus Belli is created.
        /// Parameters: creatorNationId, targetNationId, CBType.
        /// </summary>
        public event Action<string, string, CBType> OnCasusBelliCreated;

        /// <summary>
        /// Fired when a Casus Belli fabrication completes.
        /// Parameters: creatorNationId, targetNationId.
        /// </summary>
        public event Action<string, string> OnFabricationComplete;

        /// <summary>
        /// Fired when a Casus Belli expires.
        /// Parameters: creatorNationId, targetNationId.
        /// </summary>
        public event Action<string, string> OnCasusBelliExpired;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Fabrication Settings")]
        [Tooltip("Minimum turns required to fabricate a claim.")]
        [SerializeField] private int minFabricationTurns = 3;

        [Tooltip("Maximum turns required to fabricate a claim.")]
        [SerializeField] private int maxFabricationTurns = 6;

        [Tooltip("Base duration (in turns) that a Casus Belli remains valid after creation/fabrication.")]
        [SerializeField] private int baseCBDuration = 15;

        [Header("Border Incident Settings")]
        [Tooltip("Probability (0-1) that a border incident CB is successfully created per attempt.")]
        [SerializeField, Range(0f, 1f)] private float borderIncidentSuccessRate = 0.7f;

        [Header("Defaults")]
        [Tooltip("Current game turn counter. Should be updated by the turn manager each turn.")]
        [SerializeField] private int currentTurn = 1;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>All active Casus Belli instances in the game.</summary>
        private readonly List<CasusBelli> _activeCBs = new List<CasusBelli>();

        /// <summary>Expired CBs kept for historical reference.</summary>
        private readonly List<CasusBelli> _expiredCBs = new List<CasusBelli>();

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Gets all currently active (non-expired) Casus Belli.</summary>
        public IReadOnlyList<CasusBelli> ActiveCasusBelli => _activeCBs.AsReadOnly();

        /// <summary>Gets all expired Casus Belli for historical reference.</summary>
        public IReadOnlyList<CasusBelli> ExpiredCasusBelli => _expiredCBs.AsReadOnly();

        /// <summary>Gets or sets the current game turn.</summary>
        public int CurrentTurn
        {
            get => currentTurn;
            set => currentTurn = Mathf.Max(1, value);
        }

        // --------------------------------------------------------------------- //
        // Public Methods - CB Creation
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Begins fabricating a territorial claim against a target nation.
        /// The claim takes several turns to forge and becomes usable once complete.
        /// </summary>
        /// <param name="creatorNationId">Nation fabricating the claim.</param>
        /// <param name="targetNationId">Nation being falsely accused.</param>
        /// <param name="turnsToForge">Specific fabrication duration. If 0, a random duration is chosen.</param>
        /// <returns>The CasusBelli instance (still fabricating), or null on failure.</returns>
        public CasusBelli FabricateClaim(string creatorNationId, string targetNationId, int turnsToForge = 0)
        {
            if (!ValidateNationIds(creatorNationId, targetNationId)) return null;

            // Check for existing fabrication against same target
            var existingFab = _activeCBs.Find(cb =>
                cb.CreatorNationId == creatorNationId &&
                cb.TargetNationId == targetNationId &&
                cb.Type == CBType.FabricatedClaim &&
                cb.IsFabricating);

            if (existingFab != null)
            {
                Debug.LogWarning($"[CasusBelli] '{creatorNationId}' is already fabricating a claim against '{targetNationId}'.");
                return null;
            }

            // Randomize fabrication time if not specified
            int fabricationTurns = turnsToForge > 0
                ? Mathf.Clamp(turnsToForge, minFabricationTurns, maxFabricationTurns)
                : UnityEngine.Random.Range(minFabricationTurns, maxFabricationTurns + 1);

            var cb = new CasusBelli(
                CBType.FabricatedClaim,
                creatorNationId,
                targetNationId,
                $"Fabricated territorial claim against {targetNationId}. Forging documents and evidence.",
                currentTurn,
                baseCBDuration + fabricationTurns
            );

            cb.SetFabrication(fabricationTurns);
            _activeCBs.Add(cb);

            OnCasusBelliCreated?.Invoke(creatorNationId, targetNationId, CBType.FabricatedClaim);
            Debug.Log($"[CasusBelli] '{creatorNationId}' begins fabricating claim against '{targetNationId}' ({fabricationTurns} turns).");

            return cb;
        }

        /// <summary>
        /// Attempts to create a border incident Casus Belli.
        /// Has a configurable success rate; failure may incur a small diplomatic penalty.
        /// </summary>
        /// <param name="creatorNationId">Nation provoking the incident.</param>
        /// <param name="targetNationId">Nation blamed for the incident.</param>
        /// <returns>The CasusBelli if successful, or null if the attempt failed.</returns>
        public CasusBelli CreateBorderIncident(string creatorNationId, string targetNationId)
        {
            if (!ValidateNationIds(creatorNationId, targetNationId)) return null;

            // Roll for success
            if (UnityEngine.Random.value > borderIncidentSuccessRate)
            {
                Debug.Log($"[CasusBelli] Border incident attempt by '{creatorNationId}' against '{targetNationId}' failed (exposed as false flag).");
                return null;
            }

            var cb = new CasusBelli(
                CBType.BorderIncident,
                creatorNationId,
                targetNationId,
                $"Border skirmish along the {creatorNationId}-{targetNationId} frontier. {targetNationId} forces allegedly crossed into sovereign territory.",
                currentTurn,
                baseCBDuration
            );

            _activeCBs.Add(cb);

            OnCasusBelliCreated?.Invoke(creatorNationId, targetNationId, CBType.BorderIncident);
            Debug.Log($"[CasusBelli] Border incident created: '{creatorNationId}' vs '{targetNationId}'.");

            return cb;
        }

        /// <summary>
        /// Creates a humanitarian intervention Casus Belli based on alleged atrocities.
        /// Has the lowest diplomatic cost of offensive CBs.
        /// </summary>
        /// <param name="creatorNationId">Nation claiming humanitarian justification.</param>
        /// <param name="targetNationId">Nation accused of atrocities.</param>
        /// <returns>The created CasusBelli, or null on failure.</returns>
        public CasusBelli CreateHumanitarianIntervention(string creatorNationId, string targetNationId)
        {
            if (!ValidateNationIds(creatorNationId, targetNationId)) return null;

            var cb = new CasusBelli(
                CBType.HumanitarianIntervention,
                creatorNationId,
                targetNationId,
                $"Reports of humanitarian crisis in {targetNationId}. International intervention authorized to restore stability.",
                currentTurn,
                baseCBDuration + 5 // Slightly longer validity for humanitarian CBs
            );

            _activeCBs.Add(cb);

            OnCasusBelliCreated?.Invoke(creatorNationId, targetNationId, CBType.HumanitarianIntervention);
            Debug.Log($"[CasusBelli] Humanitarian intervention CB created: '{creatorNationId}' vs '{targetNationId}'.");

            return cb;
        }

        /// <summary>
        /// Creates a surprise attack CB (effectively no justification).
        /// This carries the maximum diplomatic and aggression penalties.
        /// </summary>
        /// <param name="attackerNationId">Nation initiating the surprise attack.</param>
        /// <param name="targetNationId">Nation being attacked without warning.</param>
        /// <returns>The created CasusBelli with SurpriseAttack type.</returns>
        public CasusBelli CreateSurpriseAttack(string attackerNationId, string targetNationId)
        {
            if (!ValidateNationIds(attackerNationId, targetNationId)) return null;

            var cb = new CasusBelli(
                CBType.SurpriseAttack,
                attackerNationId,
                targetNationId,
                $"Unprovoked surprise attack on {targetNationId}. No declaration of war issued.",
                currentTurn,
                0 // Expires immediately after use (single-use)
            );

            _activeCBs.Add(cb);

            OnCasusBelliCreated?.Invoke(attackerNationId, targetNationId, CBType.SurpriseAttack);
            Debug.LogWarning($"[CasusBelli] SURPRISE ATTACK: '{attackerNationId}' declares war on '{targetNationId}' without justification!");

            return cb;
        }

        /// <summary>
        /// Creates a defensive pact CB. Used when coming to an ally's defense.
        /// Carries no diplomatic or aggression penalties.
        /// </summary>
        /// <param name="defenderNationId">Nation honoring the defensive pact.</param>
        /// <param name="aggressorNationId">Nation that attacked the ally.</param>
        /// <param name="allyNationId">The ally being defended.</param>
        /// <returns>The created CasusBelli with DefensivePact type.</returns>
        public CasusBelli CreateDefensivePact(string defenderNationId, string aggressorNationId, string allyNationId)
        {
            if (string.IsNullOrEmpty(defenderNationId) || string.IsNullOrEmpty(aggressorNationId))
            {
                Debug.LogWarning("[CasusBelli] Cannot create defensive pact CB: null nation IDs.");
                return null;
            }

            var cb = new CasusBelli(
                CBType.DefensivePact,
                defenderNationId,
                aggressorNationId,
                $"Defensive pact obligation: '{defenderNationId}' honors alliance with '{allyNationId}' against aggression by '{aggressorNationId}'.",
                currentTurn,
                baseCBDuration
            );

            _activeCBs.Add(cb);

            OnCasusBelliCreated?.Invoke(defenderNationId, aggressorNationId, CBType.DefensivePact);
            Debug.Log($"[CasusBelli] Defensive pact activated: '{defenderNationId}' defends '{allyNationId}' against '{aggressorNationId}'.");

            return cb;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Validation & Queries
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Validates whether a Casus Belli can be used to declare war.
        /// A CB is valid if: it exists, is not fabricating, has not expired,
        /// and the creator and target match.
        /// </summary>
        /// <param name="cb">The CasusBelli to validate.</param>
        /// <returns>True if war can be declared using this CB.</returns>
        public bool CanDeclareWar(CasusBelli cb)
        {
            if (cb == null) return false;
            if (cb.Type == CBType.None) return false;
            if (cb.IsFabricating)
            {
                Debug.Log($"[CasusBelli] Cannot declare war: claim against '{cb.TargetNationId}' is still being fabricated " +
                          $"({cb.TurnsToCompleteFabrication} turns remaining).");
                return false;
            }
            if (cb.IsExpired)
            {
                Debug.Log($"[CasusBelli] Cannot declare war: CB against '{cb.TargetNationId}' has expired.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Calculates the trade efficiency penalty incurred when declaring war
        /// with the given Casus Belli. Lower-penalty CBs result in better trade
        /// relations during and after the war.
        /// </summary>
        /// <param name="cb">The CasusBelli being used.</param>
        /// <returns>
        /// Trade efficiency penalty as a multiplier (0.0 to 1.0).
        /// 1.0 = no penalty (defensive war), 0.5 = 50% trade reduction (surprise attack).
        /// </returns>
        public float GetWarPenalty(CasusBelli cb)
        {
            if (cb == null || cb.Type == CBType.None)
                return 0.5f; // Maximum penalty for no CB

            switch (cb.Type)
            {
                case CBType.DefensivePact:
                    return 1.0f;  // No trade penalty for defensive wars

                case CBType.HumanitarianIntervention:
                    return 0.9f;  // Minimal penalty (internationally accepted)

                case CBType.BorderIncident:
                    return 0.8f;  // Moderate penalty

                case CBType.FabricatedClaim:
                    return 0.7f;  // Significant penalty (fabrication is questionable)

                case CBType.SurpriseAttack:
                    return 0.5f;  // Severe penalty

                default:
                    return 0.5f;
            }
        }

        /// <summary>
        /// Finds all valid (ready to use) Casus Belli for a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of valid Casus Belli instances.</returns>
        public List<CasusBelli> GetValidCasusBelli(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return new List<CasusBelli>();

            return _activeCBs.FindAll(cb =>
                cb.CreatorNationId == nationId && CanDeclareWar(cb));
        }

        /// <summary>
        /// Finds all Casus Belli targeting a specific nation.
        /// </summary>
        /// <param name="targetNationId">The nation being targeted.</param>
        /// <returns>List of Casus Belli instances targeting this nation.</returns>
        public List<CasusBelli> GetCBsAgainstNation(string targetNationId)
        {
            if (string.IsNullOrEmpty(targetNationId)) return new List<CasusBelli>();

            return _activeCBs.FindAll(cb => cb.TargetNationId == targetNationId);
        }

        /// <summary>
        /// Consumes a Casus Belli after it has been used to declare war.
        /// Removes it from active CBs and moves it to expired history.
        /// </summary>
        /// <param name="cb">The CasusBelli to consume.</param>
        public void ConsumeCasusBelli(CasusBelli cb)
        {
            if (cb == null) return;

            _activeCBs.Remove(cb);
            _expiredCBs.Add(cb);

            Debug.Log($"[CasusBelli] CB consumed: {cb.Type} by '{cb.CreatorNationId}' against '{cb.TargetNationId}'.");
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Turn Processing
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Processes one turn for all active Casus Belli.
        /// Advances fabrication timers and expires old CBs.
        /// Call this once per game turn.
        /// </summary>
        public void ProcessTurn()
        {
            currentTurn++;

            List<CasusBelli> toExpire = new List<CasusBelli>();

            for (int i = _activeCBs.Count - 1; i >= 0; i--)
            {
                var cb = _activeCBs[i];

                // Advance fabrication if applicable
                if (cb.IsFabricating)
                {
                    if (cb.AdvanceFabrication())
                    {
                        OnFabricationComplete?.Invoke(cb.CreatorNationId, cb.TargetNationId);
                    }
                }

                // Check for expiry
                if (cb.IsExpired)
                {
                    toExpire.Add(cb);
                }
            }

            // Move expired CBs to history
            foreach (var expired in toExpire)
            {
                _activeCBs.Remove(expired);
                _expiredCBs.Add(expired);
                OnCasusBelliExpired?.Invoke(expired.CreatorNationId, expired.TargetNationId);
                Debug.Log($"[CasusBelli] Expired: {expired.Type} by '{expired.CreatorNationId}' against '{expired.TargetNationId}'.");
            }
        }

        // --------------------------------------------------------------------- //
        // Private Methods
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Validates that both nation IDs are non-null and non-empty.
        /// </summary>
        private bool ValidateNationIds(string nation1, string nation2)
        {
            if (string.IsNullOrEmpty(nation1))
            {
                Debug.LogWarning("[CasusBelli] Creator nation ID is null or empty.");
                return false;
            }

            if (string.IsNullOrEmpty(nation2))
            {
                Debug.LogWarning("[CasusBelli] Target nation ID is null or empty.");
                return false;
            }

            if (nation1 == nation2)
            {
                Debug.LogWarning("[CasusBelli] Creator and target cannot be the same nation.");
                return false;
            }

            return true;
        }
    }
}
