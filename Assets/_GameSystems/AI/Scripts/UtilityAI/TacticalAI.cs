// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: TacticalAI.cs
// Description: AI tactical decision-making during combat. Evaluates move targets,
//              attack priorities, retreat conditions, and threat levels using
//              positional and unit-based heuristics.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.AI
{
    /// <summary>
    /// Minimal representation of a hex grid coordinate for tactical calculations.
    /// The actual HexCoord struct from the game's grid system should match this interface.
    /// </summary>
    [System.Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        /// <summary>Column index on the hex grid.</summary>
        public int q;

        /// <summary>Row index on the hex grid.</summary>
        public int r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public static readonly HexCoord zero = new HexCoord(0, 0);

        /// <summary>Hex distance between two coordinates (axial coordinate system).</summary>
        public static int Distance(HexCoord a, HexCoord b)
        {
            return (Mathf.Abs(a.q - b.q) + Mathf.Abs(a.q + a.r - b.q - b.r) + Mathf.Abs(a.r - b.r)) / 2;
        }

        /// <summary>Returns all six hex neighbors of this coordinate.</summary>
        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0), new HexCoord(-1, 0),
            new HexCoord(0, 1), new HexCoord(0, -1),
            new HexCoord(1, -1), new HexCoord(-1, 1)
        };

        /// <summary>Gets the neighboring coordinates of this hex.</summary>
        public HexCoord[] GetNeighbors()
        {
            HexCoord[] neighbors = new HexCoord[6];
            for (int i = 0; i < 6; i++)
            {
                neighbors[i] = new HexCoord(q + Directions[i].q, r + Directions[i].r);
            }
            return neighbors;
        }

        public bool Equals(HexCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => (q * 397) ^ r;
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => $"({q},{r})";
    }

    /// <summary>
    /// Minimal interface for units used by the TacticalAI.
    /// The actual UnitBase class should implement this interface.
    /// </summary>
    public interface ITacticalUnit
    {
        /// <summary>Unique identifier.</summary>
        string UnitId { get; }

        /// <summary>Nation that owns this unit.</summary>
        string OwnerId { get; }

        /// <summary>Unit type (e.g., "infantry", "armor", "artillery", "hq").</summary>
        string UnitType { get; }

        /// <summary>Current hit points (0 to max).</summary>
        int CurrentHP { get; }

        /// <summary>Maximum hit points.</summary>
        int MaxHP { get; }

        /// <summary>Current hex grid position.</summary>
        HexCoord Position { get; }

        /// <summary>Remaining movement points this turn.</summary>
        int MovementPoints { get; }

        /// <summary>Attack range in hex cells (1 = melee, 2+ = ranged).</summary>
        int AttackRange { get; }

        /// <summary>Base combat power rating.</summary>
        float CombatPower { get; }

        /// <summary>Whether this unit is alive and operational.</summary>
        bool IsAlive { get; }

        /// <summary>Whether this unit can still move this turn.</summary>
        bool CanMove { get; }

        /// <summary>Whether this unit can still attack this turn.</summary>
        bool CanAttack { get; }
    }

    /// <summary>
    /// Minimal interface for the hex grid used by TacticalAI for pathfinding
    /// and terrain queries.
    /// </summary>
    public interface ITacticalGrid
    {
        /// <summary>Checks if a hex coordinate is valid (within bounds).</summary>
        bool IsValid(HexCoord coord);

        /// <summary>Gets the movement cost to enter a hex (higher = harder terrain).</summary>
        float GetMovementCost(HexCoord coord);

        /// <summary>Gets the defense bonus of terrain at a hex (e.g., hills = 1.3).</summary>
        float GetDefenseBonus(HexCoord coord);

        /// <summary>Checks if a hex is occupied by any unit.</summary>
        bool IsOccupied(HexCoord coord);

        /// <summary>Gets the unit at a hex, or null if empty.</summary>
        ITacticalUnit GetUnitAt(HexCoord coord);

        /// <summary>
        /// Gets all hex coordinates reachable within a given movement budget.
        /// </summary>
        /// <param name="start">Starting position.</param>
        /// <param name="movementPoints">Available movement points.</param>
        /// <returns>List of reachable hex coordinates.</returns>
        List<HexCoord> GetReachableHexes(HexCoord start, int movementPoints);
    }

    /// <summary>
    /// Minimal interface for the flanking system used by TacticalAI.
    /// </summary>
    public interface IFlankingSystem
    {
        /// <summary>
        /// Checks if attacking from a position would flank the target.
        /// Flanking typically means attacking from the side or rear relative
        /// to the target's facing direction.
        /// </summary>
        bool IsFlankingPosition(HexCoord attackerPos, ITacticalUnit target);

        /// <summary>
        /// Gets the flanking damage multiplier (e.g., 1.3 = 30% bonus damage).
        /// </summary>
        float GetFlankingDamageMultiplier(HexCoord attackerPos, ITacticalUnit target);
    }

    /// <summary>
    /// Result of threat evaluation for a single unit.
    /// </summary>
    [System.Serializable]
    public struct ThreatAssessment
    {
        /// <summary>Overall threat level from 0.0 (safe) to 1.0 (critical danger).</summary>
        public float threatLevel;

        /// <summary>Number of enemy units that can attack this unit.</summary>
        public int enemyThreats;

        /// <summary>Combined combat power of threatening enemies.</summary>
        public float totalEnemyPower;

        /// <summary>Whether the unit is surrounded (threatened from 3+ directions).</summary>
        public bool isSurrounded;

        /// <summary>Recommended action based on threat assessment.</summary>
        public string recommendedAction;
    }

    /// <summary>
    /// MonoBehaviour implementing tactical AI for combat decisions.
    /// Provides methods for finding optimal move targets, attack priorities,
    /// retreat evaluation, and threat assessment.
    /// </summary>
    public class TacticalAI : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Targeting Preferences")]
        [Tooltip("Weight for targeting low-HP units (0.0 to 1.0).")]
        [SerializeField, Range(0f, 2f)] private float lowHPTargetWeight = 1.5f;

        [Tooltip("Weight for flanking bonus opportunities (0.0 to 1.0).")]
        [SerializeField, Range(0f, 2f)] private float flankTargetWeight = 1.2f;

        [Tooltip("Weight for high-value targets like HQ and artillery (0.0 to 1.0).")]
        [SerializeField, Range(0f, 2f)] private float highValueTargetWeight = 1.3f;

        [Header("Movement Preferences")]
        [Tooltip("Bonus for moving to positions that flank enemies (0.0 to 1.0).")]
        [SerializeField, Range(0f, 1f)] private float flankMoveBonus = 0.3f;

        [Tooltip("Penalty for moving into range of multiple enemies (0.0 to 1.0).")]
        [SerializeField, Range(0f, 1f)] private float dangerAvoidancePenalty = 0.4f;

        [Tooltip("Bonus for moving to positions with high terrain defense (0.0 to 1.0).")]
        [SerializeField, Range(0f, 1f)] private float terrainDefenseBonus = 0.2f;

        [Header("Retreat Settings")]
        [Tooltip("HP threshold (fraction) below which retreat is considered. Default 0.25 = 25%.")]
        [SerializeField, Range(0f, 0.5f)] private float retreatHPThreshold = 0.25f;

        [Tooltip("Minimum number of surrounding enemies to trigger surround-retreat logic.")]
        [SerializeField] private int surroundThreshold = 3;

        // --------------------------------------------------------------------- //
        // System References (set via inspector or code)
        // --------------------------------------------------------------------- //

        [Header("System References")]
        [Tooltip("Reference to the flanking system component.")]
        [SerializeField] private MonoBehaviour flankingSystemRef;

        /// <summary>Cached flanking system interface reference.</summary>
        private IFlankingSystem _flankingSystem;

        // --------------------------------------------------------------------- //
        // Unity Lifecycle
        // --------------------------------------------------------------------- //

        private void Awake()
        {
            if (flankingSystemRef != null && flankingSystemRef is IFlankingSystem fs)
            {
                _flankingSystem = fs;
            }
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Move Targeting
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Finds the best hex position for a unit to move to.
        /// Considers flanking opportunities, terrain defense, danger avoidance,
        /// and proximity to enemy weak points.
        /// </summary>
        /// <param name="unit">The unit to move.</param>
        /// <param name="enemies">All known enemy units.</param>
        /// <param name="grid">The hex grid for pathfinding and terrain queries.</param>
        /// <returns>The best HexCoord to move to, or the unit's current position if no better option exists.</returns>
        public HexCoord FindBestMoveTarget(ITacticalUnit unit, List<ITacticalUnit> enemies, ITacticalGrid grid)
        {
            if (unit == null || grid == null)
            {
                Debug.LogWarning("[TacticalAI] FindBestMoveTarget: null unit or grid.");
                return unit?.Position ?? HexCoord.zero;
            }

            if (!unit.CanMove || unit.MovementPoints <= 0)
                return unit.Position;

            if (enemies == null || enemies.Count == 0)
                return unit.Position;

            // Get all reachable hexes
            List<HexCoord> reachable = grid.GetReachableHexes(unit.Position, unit.MovementPoints);
            if (reachable == null || reachable.Count == 0)
                return unit.Position;

            // Filter out occupied hexes (can't move onto other units)
            List<HexCoord> validMoves = new List<HexCoord>();
            foreach (var hex in reachable)
            {
                if (!grid.IsOccupied(hex) || hex == unit.Position)
                    validMoves.Add(hex);
            }

            if (validMoves.Count == 0)
                return unit.Position;

            // Score each valid move
            HexCoord bestMove = unit.Position;
            float bestScore = float.NegativeInfinity;

            foreach (var moveTarget in validMoves)
            {
                float score = ScoreMovePosition(unit, moveTarget, enemies, grid);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = moveTarget;
                }
            }

            return bestMove;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Attack Targeting
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Finds the best enemy unit to attack.
        /// Prioritizes in order:
        /// <list type="number">
        ///   <item>Low HP targets (easy kills to reduce enemy numbers)</item>
        ///   <item>Flanking opportunities (bonus damage from positional advantage)</item>
        ///   <item>High value targets (HQ units, artillery behind lines)</item>
        /// </list>
        /// </summary>
        /// <param name="unit">The attacking unit.</param>
        /// <param name="enemies">All known enemy units.</param>
        /// <returns>The best target to attack, or null if no valid targets.</returns>
        public ITacticalUnit FindBestAttackTarget(ITacticalUnit unit, List<ITacticalUnit> enemies)
        {
            if (unit == null || !unit.CanAttack)
                return null;

            if (enemies == null || enemies.Count == 0)
                return null;

            // Filter enemies in range
            List<ITacticalUnit> targetsInRange = new List<ITacticalUnit>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                int dist = HexCoord.Distance(unit.Position, enemy.Position);
                if (dist <= unit.AttackRange)
                    targetsInRange.Add(enemy);
            }

            if (targetsInRange.Count == 0)
                return null;

            // Score each target
            ITacticalUnit bestTarget = null;
            float bestScore = float.NegativeInfinity;

            foreach (var target in targetsInRange)
            {
                float score = ScoreAttackTarget(unit, target);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = target;
                }
            }

            return bestTarget;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Retreat Logic
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Evaluates whether a unit should retreat rather than fight.
        /// Retreat is recommended when:
        /// <list type="bullet">
        ///   <item>Unit HP is below the retreat threshold (default 25%), AND</item>
        ///   <item>The unit is surrounded by enemies (3+ threats from adjacent hexes)</item>
        /// </list>
        /// </summary>
        /// <param name="unit">The unit to evaluate.</param>
        /// <returns>True if the unit should retreat.</returns>
        public bool ShouldRetreat(ITacticalUnit unit)
        {
            if (unit == null || !unit.IsAlive)
                return false;

            float hpFraction = unit.MaxHP > 0 ? (float)unit.CurrentHP / unit.MaxHP : 0f;

            // Primary condition: low HP
            if (hpFraction <= retreatHPThreshold)
                return true;

            return false;
        }

        /// <summary>
        /// Extended retreat evaluation that considers surrounding enemies.
        /// A unit at moderate HP that is heavily surrounded should also retreat.
        /// </summary>
        /// <param name="unit">The unit to evaluate.</param>
        /// <param name="allUnits">All visible units on the map.</param>
        /// <param name="grid">The hex grid.</param>
        /// <returns>True if the unit should retreat.</returns>
        public bool ShouldRetreat(ITacticalUnit unit, List<ITacticalUnit> allUnits, ITacticalGrid grid)
        {
            if (ShouldRetreat(unit))
                return true;

            if (unit == null || allUnits == null || grid == null)
                return false;

            // Check for surrounded condition even at moderate HP
            float hpFraction = unit.MaxHP > 0 ? (float)unit.CurrentHP / unit.MaxHP : 0f;

            if (hpFraction < retreatHPThreshold * 2f) // Within 2x threshold
            {
                // Count adjacent enemies
                int adjacentEnemies = 0;
                foreach (var other in allUnits)
                {
                    if (other == null || !other.IsAlive || other.OwnerId == unit.OwnerId) continue;
                    if (HexCoord.Distance(unit.Position, other.Position) <= 1)
                        adjacentEnemies++;
                }

                if (adjacentEnemies >= surroundThreshold)
                    return true;
            }

            return false;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Threat Assessment
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Evaluates the threat level for a unit given all visible units and the grid.
        /// Considers nearby enemies, their attack ranges, combat power, and terrain.
        /// </summary>
        /// <param name="unit">The unit to evaluate.</param>
        /// <param name="allUnits">All visible units.</param>
        /// <param name="grid">The hex grid.</param>
        /// <returns>A ThreatAssessment struct with detailed threat information.</returns>
        public ThreatAssessment EvaluateThreat(ITacticalUnit unit, List<ITacticalUnit> allUnits, ITacticalGrid grid)
        {
            var assessment = new ThreatAssessment
            {
                threatLevel = 0f,
                enemyThreats = 0,
                totalEnemyPower = 0f,
                isSurrounded = false,
                recommendedAction = "hold"
            };

            if (unit == null || !unit.IsAlive || allUnits == null)
                return assessment;

            // Evaluate each enemy
            foreach (var other in allUnits)
            {
                if (other == null || !other.IsAlive || other.OwnerId == unit.OwnerId) continue;

                int dist = HexCoord.Distance(unit.Position, other.Position);

                // Check if this enemy can attack us
                bool canAttackUs = dist <= other.AttackRange;
                if (!canAttackUs)
                {
                    // Consider enemies that could reach us next turn
                    int reachDist = dist - other.MovementPoints;
                    if (reachDist <= other.AttackRange)
                        canAttackUs = true;
                }

                if (canAttackUs)
                {
                    assessment.enemyThreats++;
                    assessment.totalEnemyPower += other.CombatPower;
                }
            }

            // Count adjacent enemies for surround detection
            int adjacentEnemies = 0;
            foreach (var other in allUnits)
            {
                if (other == null || !other.IsAlive || other.OwnerId == unit.OwnerId) continue;
                if (HexCoord.Distance(unit.Position, other.Position) <= 1)
                    adjacentEnemies++;
            }

            assessment.isSurrounded = adjacentEnemies >= surroundThreshold;

            // Calculate overall threat level (0.0 to 1.0)
            float selfPower = unit.CombatPower > 0 ? unit.CombatPower : 1f;
            float powerRatio = assessment.totalEnemyPower > 0
                ? assessment.totalEnemyPower / (selfPower + assessment.totalEnemyPower)
                : 0f;

            float hpFactor = unit.MaxHP > 0 ? 1f - ((float)unit.CurrentHP / unit.MaxHP) : 0f;
            float surroundFactor = assessment.isSurrounded ? 0.3f : 0f;

            assessment.threatLevel = Mathf.Clamp(
                (powerRatio * 0.5f) + (hpFactor * 0.2f) + (surroundFactor * 0.3f),
                0f, 1f
            );

            // Determine recommended action
            float hpFraction = unit.MaxHP > 0 ? (float)unit.CurrentHP / unit.MaxHP : 0f;
            if (hpFraction <= retreatHPThreshold && (assessment.enemyThreats > 0 || assessment.isSurrounded))
                assessment.recommendedAction = "retreat";
            else if (assessment.threatLevel > 0.7f)
                assessment.recommendedAction = "defensive";
            else if (assessment.enemyThreats == 0)
                assessment.recommendedAction = "advance";
            else
                assessment.recommendedAction = "engage";

            return assessment;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - System References
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Sets the flanking system reference at runtime.
        /// </summary>
        /// <param name="flankingSystem">The flanking system implementing IFlankingSystem.</param>
        public void SetFlankingSystem(IFlankingSystem flankingSystem)
        {
            _flankingSystem = flankingSystem;
        }

        // --------------------------------------------------------------------- //
        // Private Methods - Scoring
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Scores a potential move position for a unit.
        /// </summary>
        private float ScoreMovePosition(ITacticalUnit unit, HexCoord position, List<ITacticalUnit> enemies, ITacticalGrid grid)
        {
            float score = 0f;

            // --- Flanking bonus: prefer positions that flank enemies ---
            if (_flankingSystem != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    int dist = HexCoord.Distance(position, enemy.Position);

                    if (dist <= unit.AttackRange + 1) // Can attack from here or adjacent
                    {
                        if (_flankingSystem.IsFlankingPosition(position, enemy))
                            score += flankMoveBonus;
                    }
                }
            }

            // --- Danger avoidance: penalize positions near many enemies ---
            float dangerScore = 0f;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                int dist = HexCoord.Distance(position, enemy.Position);
                if (dist <= enemy.AttackRange + 1)
                    dangerScore += (enemy.CombatPower / 100f);
            }
            score -= dangerScore * dangerAvoidancePenalty;

            // --- Terrain defense bonus ---
            if (grid != null && grid.IsValid(position))
            {
                float defenseBonus = grid.GetDefenseBonus(position);
                score += (defenseBonus - 1f) * terrainDefenseBonus;
            }

            // --- Proximity to weak enemies: prefer moving toward low-HP enemies ---
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                float enemyHP = enemy.MaxHP > 0 ? (float)enemy.CurrentHP / enemy.MaxHP : 1f;

                if (enemyHP < 0.4f) // Focus on weak enemies
                {
                    int dist = HexCoord.Distance(position, enemy.Position);
                    float proximityBonus = Mathf.Max(0f, 1f - (dist / 5f));
                    score += proximityBonus * 0.2f * lowHPTargetWeight;
                }
            }

            // --- Slight preference for moving forward (toward enemies) ---
            float avgEnemyDist = 0f;
            int enemyCount = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                avgEnemyDist += HexCoord.Distance(position, enemy.Position);
                enemyCount++;
            }
            if (enemyCount > 0)
            {
                avgEnemyDist /= enemyCount;
                float currentAvgDist = 0f;
                int ec = 0;
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    currentAvgDist += HexCoord.Distance(unit.Position, enemy.Position);
                    ec++;
                }
                if (ec > 0)
                {
                    currentAvgDist /= ec;
                    if (avgEnemyDist < currentAvgDist)
                        score += 0.1f; // Moving closer to enemies
                }
            }

            return score;
        }

        /// <summary>
        /// Scores an enemy unit as an attack target.
        /// Prioritizes: low HP > flanking > high value.
        /// </summary>
        private float ScoreAttackTarget(ITacticalUnit attacker, ITacticalUnit target)
        {
            float score = 0f;

            // --- Priority 1: Low HP targets (easy kills) ---
            float targetHPFraction = target.MaxHP > 0 ? (float)target.CurrentHP / target.MaxHP : 1f;
            float lowHPScore = (1f - targetHPFraction) * lowHPTargetWeight;
            score += lowHPScore;

            // --- Priority 2: Flanking opportunities ---
            if (_flankingSystem != null && _flankingSystem.IsFlankingPosition(attacker.Position, target))
            {
                float flankMultiplier = _flankingSystem.GetFlankingDamageMultiplier(attacker.Position, target);
                score += (flankMultiplier - 1f) * flankTargetWeight;
            }

            // --- Priority 3: High-value targets ---
            if (IsHighValueTarget(target))
            {
                score += highValueTargetWeight;
            }

            // --- Expected damage efficiency ---
            float expectedDamage = attacker.CombatPower * (targetHPFraction * 0.5f + 0.5f);
            if (target.MaxHP > 0 && expectedDamage >= target.CurrentHP)
                score += 2f; // Can kill - highest priority

            return score;
        }

        /// <summary>
        /// Checks if a target is high-value (HQ, artillery, or other strategic units).
        /// </summary>
        private bool IsHighValueTarget(ITacticalUnit target)
        {
            if (target == null) return false;
            string type = (target.UnitType ?? string.Empty).ToLowerInvariant();
            return type == "hq" || type == "headquarters" || type == "artillery" || type == "command";
        }
    }
}
