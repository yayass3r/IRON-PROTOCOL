// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: FlankingSystem.cs
// Description: Static utility class for calculating flanking levels, encirclement status,
//              and associated combat modifiers based on hex-grid positioning and facing.
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Represents the level of tactical advantage gained by attacking from a particular angle
    /// relative to the defender's facing direction on the hex grid.
    /// </summary>
    public enum FlankingLevel
    {
        /// <summary>Attacker is in the defender's frontal arc. No bonus.</summary>
        None,

        /// <summary>Attacker is on the defender's side arc. +25% attack bonus.</summary>
        Side,

        /// <summary>Attacker is behind the defender's rear arc. +50% attack bonus.</summary>
        Rear,

        /// <summary>Defender is surrounded by 3+ enemies. Special encirclement handling applies.</summary>
        Encircled
    }

    /// <summary>
    /// Static class providing all flanking and encirclement calculations for combat.
    /// All methods are pure functions with no side effects.
    /// </summary>
    public static class FlankingSystem
    {
        // =================================================================================
        // Constants
        // =================================================================================

        /// <summary>Number of hex directions in the hex grid.</summary>
        private const int HexDirectionCount = 6;

        /// <summary>Number of adjacent hex neighbors to check for encirclement.</summary>
        private const int EncirclementThreshold = 3;

        // =================================================================================
        // Flanking Level Calculation
        // =================================================================================

        /// <summary>
        /// Calculates the flanking level based on the attacker's position relative to
        /// the defender's position and facing direction.
        /// </summary>
        /// <param name="attackerPos">The hex coordinate of the attacking unit.</param>
        /// <param name="defenderPos">The hex coordinate of the defending unit.</param>
        /// <param name="defenderFacing">The defender's facing direction (0-5).</param>
        /// <returns>
        /// The FlankingLevel indicating the angle-based advantage.
        /// None (frontal), Side, or Rear. Encirclement is checked separately via IsEncircled.
        /// </returns>
        /// <remarks>
        /// The algorithm works by computing the hex direction from defender to attacker,
        /// then measuring the angular difference between that direction and the defender's facing.
        /// The hex grid has 6 directions, so the maximum angular difference is 3 (directly behind).
        /// <para>
        /// Angle mapping:
        /// <list type="bullet">
        ///   <item>0 or 1 hex steps from facing = Frontal (None)</item>
        ///   <item>2 hex steps from facing = Side flank</item>
        ///   <item>3 hex steps from facing = Rear flank</item>
        /// </list>
        /// </para>
        /// </remarks>
        public static FlankingLevel CalculateFlank(HexCoord attackerPos, HexCoord defenderPos, int defenderFacing)
        {
            if (attackerPos == null || defenderPos == null)
            {
                Debug.LogWarning("[FlankingSystem] CalculateFlank: null position provided. Returning None.");
                return FlankingLevel.None;
            }

            if (attackerPos.Equals(defenderPos))
            {
                // Units on the same hex — no flanking
                return FlankingLevel.None;
            }

            // Get the direction FROM defender TO attacker
            int attackDirection = defenderPos.GetDirectionTo(attackerPos);

            // Calculate the angular difference, wrapping around the hex ring
            int angleDiff = Mathf.Abs(attackDirection - defenderFacing);
            if (angleDiff > HexDirectionCount / 2)
            {
                angleDiff = HexDirectionCount - angleDiff;
            }

            // Determine flanking level based on angular difference
            if (angleDiff <= 1)
            {
                // Frontal arc: attacker is in front or slightly to the side
                return FlankingLevel.None;
            }
            else if (angleDiff == 2)
            {
                // Side arc: attacker is on the defender's flank
                return FlankingLevel.Side;
            }
            else
            {
                // Rear arc (angleDiff == 3): attacker is directly behind
                return FlankingLevel.Rear;
            }
        }

        // =================================================================================
        // Encirclement Detection
        // =================================================================================

        /// <summary>
        /// Determines whether a unit at the target position is encircled by enemy forces.
        /// A unit is considered encircled when 3 or more of its 6 adjacent hex neighbors
        /// are occupied by enemy units.
        /// </summary>
        /// <param name="targetPos">The hex coordinate of the unit being checked.</param>
        /// <param name="allEnemyPositions">List of all enemy unit positions on the map.</param>
        /// <param name="grid">The hex grid reference for neighbor lookups.</param>
        /// <returns>True if the unit is encircled (3+ enemy neighbors), false otherwise.</returns>
        public static bool IsEncircled(HexCoord targetPos, List<HexCoord> allEnemyPositions, HexGrid grid)
        {
            if (targetPos == null || grid == null)
            {
                Debug.LogWarning("[FlankingSystem] IsEncircled: null target or grid provided.");
                return false;
            }

            if (allEnemyPositions == null || allEnemyPositions.Count == 0)
            {
                return false;
            }

            // Get all 6 neighbors of the target hex
            HexCoord[] neighbors = grid.GetNeighbors(targetPos);
            if (neighbors == null || neighbors.Length == 0)
            {
                return false;
            }

            // Count how many neighbors are occupied by enemies
            int enemyNeighborCount = 0;
            HashSet<HexCoord> enemySet = new HashSet<HexCoord>(allEnemyPositions);

            foreach (HexCoord neighbor in neighbors)
            {
                if (neighbor != null && enemySet.Contains(neighbor))
                {
                    enemyNeighborCount++;

                    // Early exit: already encircled
                    if (enemyNeighborCount >= EncirclementThreshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // =================================================================================
        // Combat Modifiers
        // =================================================================================

        /// <summary>
        /// Returns the attack multiplier for the given flanking level.
        /// Encirclement has its own separate handling and should not be passed directly here.
        /// Use GetEncirclementDefensePenalty for encirclement effects on the defender.
        /// </summary>
        /// <param name="level">The FlankingLevel (None, Side, or Rear).</param>
        /// <returns>
        /// A multiplier to apply to the attacker's damage:
        /// <list type="table">
        ///   <listheader><term>Level</term><description>Multiplier</description></listheader>
        ///   <item><term>None</term><description>1.0x (no bonus)</description></item>
        ///   <item><term>Side</term><description>1.25x (+25%)</description></item>
        ///   <item><term>Rear</term><description>1.50x (+50%)</description></item>
        ///   <item><term>Encircled</term><description>1.0x (handled separately)</description></item>
        /// </list>
        /// </returns>
        public static float GetFlankingAttackMultiplier(FlankingLevel level)
        {
            switch (level)
            {
                case FlankingLevel.Side:
                    return 1.25f;

                case FlankingLevel.Rear:
                    return 1.50f;

                case FlankingLevel.Encircled:
                    // Encirclement is handled through defense penalty, not attack bonus
                    return 1.0f;

                case FlankingLevel.None:
                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Returns the defense multiplier applied to an encircled unit.
        /// Encircled units suffer a -40% defense penalty (multiplier of 0.6).
        /// </summary>
        /// <returns>A defense multiplier of 0.6 (-40% penalty).</returns>
        public static float GetEncirclementDefensePenalty()
        {
            return 0.6f;
        }

        // =================================================================================
        // Utility: Detailed Flanking Info
        // =================================================================================

        /// <summary>
        /// Returns a human-readable description of the flanking level.
        /// </summary>
        /// <param name="level">The FlankingLevel to describe.</param>
        /// <returns>A descriptive string for UI display.</returns>
        public static string GetFlankingDescription(FlankingLevel level)
        {
            switch (level)
            {
                case FlankingLevel.None:
                    return "Frontal Assault";
                case FlankingLevel.Side:
                    return "Flanking (+25% Attack)";
                case FlankingLevel.Rear:
                    return "Rear Attack (+50% Attack)";
                case FlankingLevel.Encircled:
                    return "Encircled (-40% Defense)";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Counts how many of a target's neighbors are occupied by enemy units.
        /// Useful for UI display of encirclement pressure.
        /// </summary>
        /// <param name="targetPos">The hex coordinate of the target unit.</param>
        /// <param name="allEnemyPositions">List of all enemy unit positions.</param>
        /// <param name="grid">The hex grid reference for neighbor lookups.</param>
        /// <returns>The number of adjacent hexes occupied by enemies (0-6).</returns>
        public static int CountEnemyNeighbors(HexCoord targetPos, List<HexCoord> allEnemyPositions, HexGrid grid)
        {
            if (targetPos == null || grid == null || allEnemyPositions == null)
                return 0;

            HexCoord[] neighbors = grid.GetNeighbors(targetPos);
            if (neighbors == null || neighbors.Length == 0)
                return 0;

            HashSet<HexCoord> enemySet = new HashSet<HexCoord>(allEnemyPositions);
            int count = 0;

            foreach (HexCoord neighbor in neighbors)
            {
                if (neighbor != null && enemySet.Contains(neighbor))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
