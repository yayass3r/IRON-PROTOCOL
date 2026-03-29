// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: DiplomacyManager.cs
// Description: Central diplomacy system managing all inter-nation relations,
//              war declarations, peace treaties, alliances, and trade agreements.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.Diplomacy
{
    /// <summary>
    /// Represents the bilateral diplomatic relationship between two nations.
    /// Tracks opinion, war status, alliances, and trade agreements.
    /// </summary>
    [System.Serializable]
    public class Relation
    {
        /// <summary>The nation this relation is from.</summary>
        [SerializeField] private string fromId;

        /// <summary>The nation this relation is directed toward.</summary>
        [SerializeField] private string toId;

        /// <summary>
        /// Opinion score from -100 (absolute hatred) to +100 (deep alliance).
        /// 0 = neutral. Affects willingness to trade, form alliances, and respond to demands.
        /// </summary>
        [SerializeField] private float opinion;

        /// <summary>Whether these two nations are currently at war.</summary>
        [SerializeField] private bool atWar;

        /// <summary>Whether these two nations have a mutual defense alliance.</summary>
        [SerializeField] private bool allied;

        /// <summary>Whether these two nations have an active trade agreement.</summary>
        [SerializeField] private bool tradeAgreement;

        /// <summary>The turn when war was declared (0 if not at war).</summary>
        [SerializeField] private int turnWarDeclared;

        /// <summary>The number of turns these nations have been at war continuously.</summary>
        [SerializeField] private int turnsAtWar;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>The nation this relation originates from.</summary>
        public string FromId => fromId;

        /// <summary>The nation this relation targets.</summary>
        public string ToId => toId;

        /// <summary>Opinion score from -100 to +100.</summary>
        public float Opinion => opinion;

        /// <summary>Whether the nations are at war.</summary>
        public bool AtWar => atWar;

        /// <summary>Whether the nations are allied.</summary>
        public bool Allied => allied;

        /// <summary>Whether the nations have a trade agreement.</summary>
        public bool TradeAgreement => tradeAgreement;

        /// <summary>Turn the war was declared (0 if at peace).</summary>
        public int TurnWarDeclared => turnWarDeclared;

        /// <summary>Number of continuous turns at war.</summary>
        public int TurnsAtWar => turnsAtWar;

        // --------------------------------------------------------------------- //
        // Constructor
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Creates a new bilateral Relation with initial opinion.
        /// </summary>
        /// <param name="fromId">Originating nation.</param>
        /// <param name="toId">Target nation.</param>
        /// <param name="initialOpinion">Starting opinion (-100 to 100). Defaults to 0.</param>
        /// <param name="currentTurn">Current game turn for tracking.</param>
        public Relation(string fromId, string toId, float initialOpinion = 0f, int currentTurn = 1)
        {
            this.fromId = fromId;
            this.toId = toId;
            this.opinion = Mathf.Clamp(initialOpinion, -100f, 100f);
            this.atWar = false;
            this.allied = false;
            this.tradeAgreement = false;
            this.turnWarDeclared = 0;
            this.turnsAtWar = 0;
        }

        /// <summary>Parameterless constructor for serialization.</summary>
        public Relation() { }

        // --------------------------------------------------------------------- //
        // Internal Mutators (used by DiplomacyManager)
        // --------------------------------------------------------------------- //

        /// <summary>Clamps and sets opinion value.</summary>
        internal void SetOpinion(float value) => opinion = Mathf.Clamp(value, -100f, 100f);

        /// <summary>Sets the alliance flag.</summary>
        internal void SetAllied(bool value) => allied = value;

        /// <summary>Sets the trade agreement flag.</summary>
        internal void SetTradeAgreement(bool value) => tradeAgreement = value;

        /// <summary>
        /// Sets war state and records the turn. Clears alliance and trade when entering war.
        /// </summary>
        internal void SetWarState(bool isWar, int currentTurn)
        {
            if (isWar && !atWar)
            {
                turnWarDeclared = currentTurn;
                turnsAtWar = 1;
                allied = false;
                tradeAgreement = false;
            }
            else if (!isWar && atWar)
            {
                turnsAtWar = 0;
                turnWarDeclared = 0;
            }
            atWar = isWar;
        }

        /// <summary>Increments the war duration counter if at war.</summary>
        internal void IncrementWarDuration()
        {
            if (atWar) turnsAtWar++;
        }
    }

    /// <summary>
    /// Delegate for diplomacy events involving two nations.
    /// </summary>
    public delegate void DiplomacyEventHandler(string nation1, string nation2);

    /// <summary>
    /// Delegate for opinion change events.
    /// </summary>
    public delegate void OpinionChangedHandler(string fromId, string toId, float oldOpinion, float newOpinion);

    /// <summary>
    /// MonoBehaviour that manages all bilateral diplomatic relations in the game.
    /// Handles war declarations, peace treaties, alliance formation, trade agreements,
    /// and per-turn opinion decay toward neutrality.
    /// </summary>
    public class DiplomacyManager : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>Fired when two nations go to war.</summary>
        public event DiplomacyEventHandler OnWarDeclared;

        /// <summary>Fired when two nations make peace.</summary>
        public event DiplomacyEventHandler OnPeaceMade;

        /// <summary>Fired when two nations form an alliance.</summary>
        public event DiplomacyEventHandler OnAllianceFormed;

        /// <summary>Fired when an alliance is broken.</summary>
        public event DiplomacyEventHandler OnAllianceBroken;

        /// <summary>Fired when a trade agreement is signed.</summary>
        public event DiplomacyEventHandler OnTradeAgreementSigned;

        /// <summary>Fired when a trade agreement is cancelled.</summary>
        public event DiplomacyEventHandler OnTradeAgreementCancelled;

        /// <summary>Fired when an opinion changes between any two nations.</summary>
        public event OpinionChangedHandler OnOpinionChanged;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Opinion Settings")]
        [Tooltip("Rate at which opinions decay toward 0 per turn (absolute value).")]
        [SerializeField] private float opinionDecayRate = 0.5f;

        [Tooltip("Opinion threshold above which nations consider trade agreements.")]
        [SerializeField] private float tradeThreshold = 10f;

        [Tooltip("Opinion threshold above which nations consider alliances.")]
        [SerializeField] private float allianceThreshold = 50f;

        [Header("War Penalties")]
        [Tooltip("Opinion penalty applied globally when a nation declares war without CB.")]
        [SerializeField] private float unjustWarOpinionPenalty = -20f;

        [Tooltip("Opinion penalty applied globally when a nation declares war with CB.")]
        [SerializeField] private float justifiedWarOpinionPenalty = -5f;

        [Header("State")]
        [Tooltip("Current game turn, updated each turn cycle.")]
        [SerializeField] private int currentTurn = 1;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>All bilateral relations in the game.</summary>
        private readonly List<Relation> relations = new List<Relation>();

        /// <summary>Set of registered nation IDs.</summary>
        private readonly HashSet<string> registeredNations = new HashSet<string>();

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Gets all bilateral relations.</summary>
        public IReadOnlyList<Relation> Relations => relations.AsReadOnly();

        /// <summary>Gets all registered nation IDs.</summary>
        public IReadOnlyCollection<string> RegisteredNations => registeredNations;

        /// <summary>Gets or sets the current game turn.</summary>
        public int CurrentTurn
        {
            get => currentTurn;
            set => currentTurn = Mathf.Max(1, value);
        }

        // --------------------------------------------------------------------- //
        // Unity Lifecycle
        // --------------------------------------------------------------------- //

        private void Awake()
        {
            opinionDecayRate = Mathf.Max(0f, opinionDecayRate);
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Nation Registration
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Registers a nation in the diplomacy system and creates bidirectional relations
        /// with all previously registered nations.
        /// </summary>
        /// <param name="nationId">The nation to register.</param>
        /// <param name="initialOpinions">Optional initial opinions. Key = other nation ID, Value = opinion score.</param>
        public void RegisterNation(string nationId, Dictionary<string, float> initialOpinions = null)
        {
            if (string.IsNullOrEmpty(nationId) || registeredNations.Contains(nationId))
            {
                if (registeredNations.Contains(nationId))
                    Debug.LogWarning($"[DiplomacyManager] Nation '{nationId}' is already registered.");
                return;
            }

            registeredNations.Add(nationId);

            foreach (var otherNation in registeredNations)
            {
                if (otherNation == nationId) continue;

                float initOp = 0f;
                if (initialOpinions != null && initialOpinions.TryGetValue(otherNation, out float op))
                    initOp = op;

                // nationId -> otherNation
                relations.Add(new Relation(nationId, otherNation, initOp, currentTurn));

                // otherNation -> nationId
                float reverseOp = 0f;
                if (initialOpinions != null && initialOpinions.TryGetValue(nationId, out float rop))
                    reverseOp = rop;

                relations.Add(new Relation(otherNation, nationId, reverseOp, currentTurn));
            }

            Debug.Log($"[DiplomacyManager] Nation '{nationId}' registered. Total nations: {registeredNations.Count}");
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Core Diplomatic Actions
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Declares war between two nations. Applies diplomatic penalties to all third-party nations,
        /// cancels trade agreements and alliances between the belligerents, and fires events.
        /// </summary>
        /// <param name="attacker">Nation initiating the war.</param>
        /// <param name="defender">Nation being attacked.</param>
        /// <param name="cb">CasusBelli used for justification. CBType.None means no justification.</param>
        /// <returns>True if the war was successfully declared.</returns>
        public bool DeclareWar(string attacker, string defender, CBType cb = CBType.None)
        {
            if (!ValidateNationPair(attacker, defender)) return false;

            if (AreAtWar(attacker, defender))
            {
                Debug.LogWarning($"[DiplomacyManager] '{attacker}' and '{defender}' are already at war.");
                return false;
            }

            var relAToD = GetOrCreateRelation(attacker, defender);
            var relDToA = GetOrCreateRelation(defender, attacker);

            relAToD.SetWarState(true, currentTurn);
            relDToA.SetWarState(true, currentTurn);

            // Bilateral opinion damage
            ChangeOpinion(attacker, defender, -50f);
            ChangeOpinion(defender, attacker, -60f);

            // Global opinion penalties
            float penalty = (cb == CBType.None || cb == CBType.SurpriseAttack)
                ? unjustWarOpinionPenalty
                : justifiedWarOpinionPenalty;

            foreach (var nation in registeredNations)
            {
                if (nation == attacker || nation == defender) continue;
                ChangeOpinion(nation, attacker, penalty);

                if (cb == CBType.None || cb == CBType.SurpriseAttack)
                    ChangeOpinion(nation, defender, -penalty * 0.2f);
            }

            OnWarDeclared?.Invoke(attacker, defender);
            Debug.LogWarning($"[DiplomacyManager] WAR DECLARED: '{attacker}' attacks '{defender}' (CB: {cb}).");
            return true;
        }

        /// <summary>
        /// Negotiates peace between two nations at war.
        /// Resets war state and applies a small opinion recovery.
        /// </summary>
        /// <param name="nation1">First nation.</param>
        /// <param name="nation2">Second nation.</param>
        /// <returns>True if peace was successfully negotiated.</returns>
        public bool MakePeace(string nation1, string nation2)
        {
            if (!ValidateNationPair(nation1, nation2)) return false;

            if (!AreAtWar(nation1, nation2))
            {
                Debug.LogWarning($"[DiplomacyManager] '{nation1}' and '{nation2}' are not at war.");
                return false;
            }

            var rel1 = GetOrCreateRelation(nation1, nation2);
            var rel2 = GetOrCreateRelation(nation2, nation1);

            rel1.SetWarState(false, currentTurn);
            rel2.SetWarState(false, currentTurn);

            ChangeOpinion(nation1, nation2, 10f);
            ChangeOpinion(nation2, nation1, 10f);

            OnPeaceMade?.Invoke(nation1, nation2);
            Debug.Log($"[DiplomacyManager] PEACE: '{nation1}' and '{nation2}' have made peace.");
            return true;
        }

        /// <summary>
        /// Forms a mutual defense alliance between two nations.
        /// Both nations commit to defending each other if attacked.
        /// Requires mutual positive opinion above <see cref="allianceThreshold"/> and peace between them.
        /// </summary>
        /// <param name="nation1">First alliance member.</param>
        /// <param name="nation2">Second alliance member.</param>
        /// <returns>True if the alliance was successfully formed.</returns>
        public bool FormAlliance(string nation1, string nation2)
        {
            if (!ValidateNationPair(nation1, nation2)) return false;

            if (AreAtWar(nation1, nation2))
            {
                Debug.LogWarning("[DiplomacyManager] Cannot form alliance: nations are at war.");
                return false;
            }

            var rel1 = GetOrCreateRelation(nation1, nation2);
            var rel2 = GetOrCreateRelation(nation2, nation1);

            if (rel1.Allied)
            {
                Debug.LogWarning($"[DiplomacyManager] '{nation1}' and '{nation2}' are already allied.");
                return false;
            }

            if (rel1.Opinion < allianceThreshold || rel2.Opinion < allianceThreshold)
            {
                Debug.LogWarning($"[DiplomacyManager] Alliance rejected: insufficient mutual opinion " +
                                 $"({nation1}->{nation2}: {rel1.Opinion:F0}, {nation2}->{nation1}: {rel2.Opinion:F0}).");
                return false;
            }

            rel1.SetAllied(true);
            rel2.SetAllied(true);

            // Alliance boosts mutual opinion
            ChangeOpinion(nation1, nation2, 15f);
            ChangeOpinion(nation2, nation1, 15f);

            OnAllianceFormed?.Invoke(nation1, nation2);
            Debug.Log($"[DiplomacyManager] ALLIANCE FORMED: '{nation1}' and '{nation2}'.");
            return true;
        }

        /// <summary>
        /// Breaks an existing alliance between two nations.
        /// Causes significant opinion penalties and diplomatic fallout.
        /// </summary>
        public bool BreakAlliance(string nation1, string nation2)
        {
            if (!ValidateNationPair(nation1, nation2)) return false;

            var rel1 = GetRelation(nation1, nation2);
            var rel2 = GetRelation(nation2, nation1);

            if (rel1 == null || !rel1.Allied)
            {
                Debug.LogWarning("[DiplomacyManager] No alliance to break.");
                return false;
            }

            rel1.SetAllied(false);
            rel2.SetAllied(false);

            ChangeOpinion(nation1, nation2, -30f);
            ChangeOpinion(nation2, nation1, -30f);

            OnAllianceBroken?.Invoke(nation1, nation2);
            Debug.LogWarning($"[DiplomacyManager] ALLIANCE BROKEN: '{nation1}' and '{nation2}'.");
            return true;
        }

        /// <summary>
        /// Signs a trade agreement between two nations.
        /// Requires mutual opinion above <see cref="tradeThreshold"/> and peace.
        /// </summary>
        public bool SignTradeAgreement(string nation1, string nation2)
        {
            if (!ValidateNationPair(nation1, nation2)) return false;

            if (AreAtWar(nation1, nation2))
            {
                Debug.LogWarning("[DiplomacyManager] Cannot sign trade agreement during war.");
                return false;
            }

            var rel1 = GetOrCreateRelation(nation1, nation2);
            var rel2 = GetOrCreateRelation(nation2, nation1);

            if (rel1.TradeAgreement)
            {
                Debug.LogWarning($"[DiplomacyManager] '{nation1}' and '{nation2}' already trade.");
                return false;
            }

            if (rel1.Opinion < tradeThreshold || rel2.Opinion < tradeThreshold)
            {
                Debug.LogWarning("[DiplomacyManager] Trade rejected: opinion too low.");
                return false;
            }

            rel1.SetTradeAgreement(true);
            rel2.SetTradeAgreement(true);

            ChangeOpinion(nation1, nation2, 5f);
            ChangeOpinion(nation2, nation1, 5f);

            OnTradeAgreementSigned?.Invoke(nation1, nation2);
            Debug.Log($"[DiplomacyManager] TRADE AGREEMENT: '{nation1}' and '{nation2}'.");
            return true;
        }

        /// <summary>
        /// Cancels an existing trade agreement between two nations.
        /// </summary>
        public bool CancelTradeAgreement(string nation1, string nation2)
        {
            if (!ValidateNationPair(nation1, nation2)) return false;

            var rel1 = GetRelation(nation1, nation2);
            var rel2 = GetRelation(nation2, nation1);

            if (rel1 == null || !rel1.TradeAgreement)
            {
                Debug.LogWarning("[DiplomacyManager] No trade agreement to cancel.");
                return false;
            }

            rel1.SetTradeAgreement(false);
            rel2.SetTradeAgreement(false);

            ChangeOpinion(nation1, nation2, -10f);
            ChangeOpinion(nation2, nation1, -10f);

            OnTradeAgreementCancelled?.Invoke(nation1, nation2);
            Debug.Log($"[DiplomacyManager] Trade cancelled: '{nation1}' and '{nation2}'.");
            return true;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Opinion Management
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Adjusts the opinion one nation has toward another.
        /// Opinion is clamped to [-100, +100].
        /// </summary>
        /// <param name="from">Nation whose opinion is changing.</param>
        /// <param name="to">Nation the opinion is about.</param>
        /// <param name="delta">Change amount (positive = warmer, negative = colder).</param>
        public void ChangeOpinion(string from, string to, float delta)
        {
            if (delta == 0f) return;
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

            var relation = GetOrCreateRelation(from, to);
            float oldOpinion = relation.Opinion;
            relation.SetOpinion(oldOpinion + delta);

            if (Mathf.Abs(relation.Opinion - oldOpinion) > 0.01f)
            {
                OnOpinionChanged?.Invoke(from, to, oldOpinion, relation.Opinion);
            }
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Queries
        // --------------------------------------------------------------------- //

        /// <summary>Gets the bilateral relation from one nation to another, or null.</summary>
        public Relation GetRelation(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return null;
            return relations.Find(r => r.FromId == from && r.ToId == to);
        }

        /// <summary>Checks whether two nations are currently at war.</summary>
        public bool AreAtWar(string nation1, string nation2)
        {
            var rel = GetRelation(nation1, nation2);
            return rel != null && rel.AtWar;
        }

        /// <summary>Checks whether two nations are allied.</summary>
        public bool AreAllied(string nation1, string nation2)
        {
            var rel = GetRelation(nation1, nation2);
            return rel != null && rel.Allied;
        }

        /// <summary>Checks whether two nations have a trade agreement.</summary>
        public bool HaveTradeAgreement(string nation1, string nation2)
        {
            var rel = GetRelation(nation1, nation2);
            return rel != null && rel.TradeAgreement;
        }

        /// <summary>Gets the opinion one nation has toward another (0 if no relation).</summary>
        public float GetOpinion(string from, string to)
        {
            var rel = GetRelation(from, to);
            return rel != null ? rel.Opinion : 0f;
        }

        /// <summary>Gets all nations that a given nation is at war with.</summary>
        public List<string> GetEnemies(string nationId)
        {
            return relations.Where(r => r.FromId == nationId && r.AtWar).Select(r => r.ToId).ToList();
        }

        /// <summary>Gets all nations that a given nation is allied with.</summary>
        public List<string> GetAllies(string nationId)
        {
            return relations.Where(r => r.FromId == nationId && r.Allied).Select(r => r.ToId).ToList();
        }

        /// <summary>Gets all nations that have trade agreements with a given nation.</summary>
        public List<string> GetTradingPartners(string nationId)
        {
            return relations.Where(r => r.FromId == nationId && r.TradeAgreement).Select(r => r.ToId).ToList();
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Turn Processing
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Updates all relations once per game turn.
        /// Opinions decay toward neutrality (0), and war durations are incremented.
        /// Call this once per turn after all other turn processing.
        /// </summary>
        public void UpdateRelations()
        {
            currentTurn++;

            foreach (var relation in relations)
            {
                relation.IncrementWarDuration();

                if (!relation.AtWar)
                {
                    float currentOp = relation.Opinion;
                    if (Mathf.Abs(currentOp) > 0.1f)
                    {
                        float decayDir = currentOp > 0f ? -1f : 1f;
                        float newOp = currentOp + (decayDir * opinionDecayRate);
                        newOp = decayDir < 0f ? Mathf.Max(0f, newOp) : Mathf.Min(0f, newOp);

                        float oldOp = relation.Opinion;
                        relation.SetOpinion(newOp);

                        if (Mathf.Abs(relation.Opinion - oldOp) > 0.01f)
                            OnOpinionChanged?.Invoke(relation.FromId, relation.ToId, oldOp, relation.Opinion);
                    }
                }
            }
        }

        // --------------------------------------------------------------------- //
        // Private Methods
        // --------------------------------------------------------------------- //

        private Relation GetOrCreateRelation(string from, string to)
        {
            var existing = GetRelation(from, to);
            if (existing != null) return existing;

            var newRel = new Relation(from, to, 0f, currentTurn);
            relations.Add(newRel);
            return newRel;
        }

        private bool ValidateNationPair(string n1, string n2)
        {
            if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2))
            {
                Debug.LogWarning("[DiplomacyManager] Nation IDs cannot be null or empty.");
                return false;
            }
            if (n1 == n2)
            {
                Debug.LogWarning("[DiplomacyManager] Nation IDs cannot be the same.");
                return false;
            }
            return true;
        }
    }
}
