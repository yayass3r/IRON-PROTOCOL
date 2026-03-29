// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: ArtilleryUnit.cs
// Description: Concrete unit class for artillery units. Artillery provides powerful
//              indirect fire support with a minimum range restriction, the ability
//              to apply suppression, and vulnerability to direct combat due to low
//              defense and hit points.
// =====================================================================================

using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Artillery unit — indirect fire support platform (howitzers, MLRS, mortars).
    /// <para>
    /// Key characteristics:
    /// <list type="bullet">
    ///   <item>Very high attack power (70) — the strongest attack of any ground unit.</item>
    ///   <item>Very low defense (15) — vulnerable to direct engagement.</item>
    ///   <item>Low hit points (60) — easily destroyed if caught in the open.</item>
    ///   <item>Slow movement (2 hexes per turn) — heavy equipment is hard to reposition.</item>
    ///   <item>Long range (3 hexes) — can fire over friendly units at distant targets.</item>
    ///   <item>Special: Applies 2 suppression stacks on hit, pinning enemy units.</item>
    ///   <item>Restriction: Cannot attack adjacent units (minimum range of 2 hexes).</item>
    /// </list>
    /// </para>
    /// <para>
    /// Artillery is the backbone of fire support and pairs extremely well with Infantry
    /// (+20% attack to all friendly units via CombinedArmsSystem) and Armor (+10% attack
    /// via prepared barrage). Keep artillery behind the front line and protect it with
    /// Infantry screens for maximum effectiveness.
    /// </para>
    /// </summary>
    [AddComponentMenu("Iron Protocol/Military/Ground Units/Artillery Unit")]
    public class ArtilleryUnit : UnitBase
    {
        // =================================================================================
        // Constants
        // =================================================================================

        /// <summary>Default attack power for artillery units.</summary>
        private const float DefaultAttack = 70f;

        /// <summary>Default defense value for artillery units.</summary>
        private const float DefaultDefense = 15f;

        /// <summary>Default maximum hit points for artillery units.</summary>
        private const float DefaultMaxHp = 60f;

        /// <summary>Default movement points per turn.</summary>
        private const float DefaultMovement = 2f;

        /// <summary>Default maximum attack range in hexes.</summary>
        private const float DefaultRange = 3f;

        /// <summary>Minimum attack range — cannot fire at targets closer than this.</summary>
        private const int MinimumRange = 2;

        /// <summary>Default number of suppression stacks applied per attack.</summary>
        private const int DefaultSuppressionStacks = 2;

        /// <summary>Damage penalty when attacking at minimum range (if allowed via override).</summary>
        private const float MinimumRangeDamagePenalty = 0.5f;

        // =================================================================================
        // Serialized Configuration
        // =================================================================================

        [Header("Artillery Configuration")]
        [Tooltip("Minimum range in hexes. Artillery cannot fire at targets closer than this.")]
        [SerializeField] private int minimumRange = MinimumRange;

        [Tooltip("Number of suppression stacks applied to the target on a successful hit.")]
        [SerializeField] private int suppressionStacksApplied = DefaultSuppressionStacks;

        [Tooltip("Damage multiplier when forced to fire at targets within minimum range.")]
        [SerializeField] private float minRangeDamagePenalty = MinimumRangeDamagePenalty;

        /// <summary>
        /// Gets the minimum firing range in hexes.
        /// </summary>
        public int MinimumRangeValue => minimumRange;

        /// <summary>
        /// Gets the number of suppression stacks this artillery unit applies per hit.
        /// </summary>
        public int SuppressionStacksApplied => suppressionStacksApplied;

        // =================================================================================
        // Unity Lifecycle
        // =================================================================================

        /// <summary>
        /// Unity Awake. Initializes base class and sets artillery-specific defaults.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            InitializeArtilleryDefaults();
        }

        /// <summary>
        /// Unity Reset. Called when the component is first added in the inspector.
        /// Sets default values for all artillery-specific fields.
        /// </summary>
        private void Reset()
        {
            InitializeArtilleryDefaults();
        }

        // =================================================================================
        // Initialization
        // =================================================================================

        /// <summary>
        /// Sets all artillery-specific stat defaults. Called on Awake and Reset.
        /// </summary>
        private void InitializeArtilleryDefaults()
        {
            UnitName = "Artillery Battery";
            Attack = DefaultAttack;
            Defense = DefaultDefense;
            MaxHp = DefaultMaxHp;
            Hp = DefaultMaxHp;
            MovementPoints = DefaultMovement;
            AttackRange = DefaultRange;
        }

        // =================================================================================
        // UnitBase Implementation
        // =================================================================================

        /// <summary>
        /// Returns UnitType.Artillery, identifying this unit as artillery for combined arms
        /// categorization, range checks, and combat resolution.
        /// </summary>
        /// <returns>UnitType.Artillery</returns>
        public override UnitType GetUnitType()
        {
            return UnitType.Artillery;
        }

        /// <summary>
        /// Artillery units receive reduced defense effectiveness when attacked directly,
        /// reflecting their vulnerability to close-quarters combat.
        /// </summary>
        /// <returns>Effective defense value, potentially reduced by direct combat penalty.</returns>
        public override float GetEffectiveDefense()
        {
            float baseDefense = base.GetEffectiveDefense();

            // Artillery has inherently poor close-quarters defense
            // The base defense of 15 already accounts for this, but we apply an
            // additional 10% reduction to emphasize their vulnerability
            baseDefense *= 0.90f;

            return baseDefense;
        }

        // =================================================================================
        // Artillery Special Abilities
        // =================================================================================

        /// <summary>
        /// Validates whether this artillery unit can attack a target at the given position.
        /// Artillery has a minimum range restriction and cannot fire at adjacent units.
        /// </summary>
        /// <param name="targetPosition">The hex coordinate of the potential target.</param>
        /// <returns>True if the target is within valid range (between min and max range).</returns>
        /// <remarks>
        /// Valid range for artillery: [minimumRange, attackRange] inclusive.
        /// For default values: target must be 2 to 3 hexes away.
        /// </remarks>
        public bool CanTargetPosition(HexCoord targetPosition)
        {
            if (targetPosition == null || Position == null)
            {
                return false;
            }

            int distance = HexCoord.GetDistance(Position, targetPosition);

            // Target must be within maximum range
            if (distance > AttackRange)
            {
                Debug.Log($"[ArtilleryUnit] Target out of range: {distance} hexes " +
                          $"(max: {AttackRange})");
                return false;
            }

            // Target must be beyond minimum range (cannot fire at adjacent units)
            if (distance < minimumRange)
            {
                Debug.Log($"[ArtilleryUnit] Target too close: {distance} hexes " +
                          $"(min: {minimumRange})");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Fires artillery at a target unit, dealing damage and applying suppression.
        /// This method should be called by the CombatResolver as part of the attack sequence.
        /// </summary>
        /// <param name="target">The target unit to bombard.</param>
        /// <param name="damageAmount">Pre-calculated damage to deal.</param>
        /// <returns>
        /// True if the bombardment was successful (target was valid and in range).
        /// </returns>
        public bool BombardTarget(UnitBase target, float damageAmount)
        {
            if (!CanAct)
            {
                Debug.LogWarning("[ArtilleryUnit] Cannot bombard: unit cannot act.");
                return false;
            }

            if (hasAttackedThisTurn)
            {
                Debug.LogWarning("[ArtilleryUnit] Cannot bombard: already attacked this turn.");
                return false;
            }

            if (target == null || !target.IsAlive)
            {
                Debug.LogWarning("[ArtilleryUnit] Cannot bombard: invalid target.");
                return false;
            }

            if (!CanTargetPosition(target.Position))
            {
                Debug.LogWarning("[ArtilleryUnit] Cannot bombard: target out of artillery range.");
                return false;
            }

            // Apply damage to target
            float actualDamage = target.TakeDamage(damageAmount);

            // Apply suppression stacks
            target.ApplySuppression(suppressionStacksApplied);

            // Mark as having attacked this turn
            hasAttackedThisTurn = true;

            Debug.Log($"[ArtilleryUnit] '{UnitName}' bombarded '{target.UnitName}': " +
                      $"{actualDamage:F1} damage, +{suppressionStacksApplied} suppression " +
                      $"(target now at {target.Suppression}/3 suppression)");

            return true;
        }

        /// <summary>
        /// Gets the range status of a potential target for UI display purposes.
        /// </summary>
        /// <param name="targetPosition">The hex coordinate of the potential target.</param>
        /// <returns>
        /// A descriptive string indicating the range status:
        /// "InRange", "TooClose", "TooFar", or "InvalidPosition".
        /// </returns>
        public string GetTargetRangeStatus(HexCoord targetPosition)
        {
            if (targetPosition == null || Position == null)
            {
                return "InvalidPosition";
            }

            int distance = HexCoord.GetDistance(Position, targetPosition);

            if (distance < minimumRange)
            {
                return "TooClose";
            }

            if (distance > AttackRange)
            {
                return "TooFar";
            }

            return "InRange";
        }

        /// <summary>
        /// Returns the effective attack with a slight penalty for targets at minimum range.
        /// When artillery fires at its minimum range, accuracy and effect are reduced.
        /// </summary>
        /// <param name="targetPosition">The position of the target being fired upon.</param>
        /// <returns>
        /// Effective attack value, potentially reduced if the target is at minimum range.
        /// </returns>
        public float GetEffectiveAttackAtRange(HexCoord targetPosition)
        {
            float baseAttack = GetEffectiveAttack();

            if (targetPosition != null && Position != null)
            {
                int distance = HexCoord.GetDistance(Position, targetPosition);

                // At minimum range, artillery is less effective (shell trajectory issues)
                if (distance == minimumRange)
                {
                    baseAttack *= minRangeDamagePenalty;
                    Debug.Log($"[ArtilleryUnit] Minimum range penalty applied: " +
                              $"x{minRangeDamagePenalty:F2} attack");
                }
            }

            return baseAttack;
        }
    }
}
