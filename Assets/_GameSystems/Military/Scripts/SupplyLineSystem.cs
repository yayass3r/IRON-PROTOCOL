// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: SupplyLineSystem.cs
// Description: System for tracing supply lines using Breadth-First Search (BFS).
//              Evaluates supply status, efficiency based on distance and terrain,
//              and identifies vulnerable interdiction points along supply routes.
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Represents the supply status of a unit based on its connection to a supply source.
    /// Used by the CombatResolver and AI systems to determine supply-related penalties.
    /// </summary>
    public struct SupplyStatus
    {
        /// <summary>True if the unit has an unbroken supply line to the source.</summary>
        public bool isSupplied;

        /// <summary>
        /// Supply efficiency as a fraction (0.0 to 1.0).
        /// Decreases with distance from the supply source and difficult terrain.
        /// At 1.0 the unit is fully supplied; at 0.0 it is completely cut off.
        /// </summary>
        public float efficiency;

        /// <summary>
        /// The length of the supply path in hexes from source to unit.
        /// A shorter path means higher efficiency.
        /// </summary>
        public int pathLength;

        /// <summary>
        /// Returns a human-readable summary of the supply status.
        /// </summary>
        public override string ToString()
        {
            return $"[SupplyStatus: Supplied={isSupplied}, Efficiency={efficiency:P0}, PathLength={pathLength}]";
        }
    }

    /// <summary>
    /// BFS node used internally for pathfinding during supply evaluation.
    /// </summary>
    internal class SupplyPathNode
    {
        public HexCoord Position;
        public int Cost;           // Accumulated terrain cost to reach this node
        public int PathLength;     // Number of hexes traversed
        public HexCoord Parent;    // Previous hex on the path (for interdiction point tracing)

        public SupplyPathNode(HexCoord position, int cost, int pathLength, HexCoord parent)
        {
            Position = position;
            Cost = cost;
            PathLength = pathLength;
            Parent = parent;
        }
    }

    /// <summary>
    /// System for evaluating supply lines using BFS traversal of the hex grid.
    /// Accounts for terrain costs, enemy interdiction, and distance-based efficiency decay.
    /// </summary>
    public class SupplyLineSystem
    {
        // =================================================================================
        // Configuration Constants
        // =================================================================================

        /// <summary>Maximum path length before supply is considered cut off entirely.</summary>
        private const int MaxSupplyRange = 20;

        /// <summary>Base efficiency per hex of distance (efficiency decays with distance).</summary>
        private const float EfficiencyDecayPerHex = 0.04f;

        /// <summary>Minimum guaranteed efficiency if a supply path exists.</summary>
        private const float MinimumEfficiency = 0.1f;

        /// <summary>BFS iteration limit to prevent infinite loops on very large maps.</summary>
        private const int MaxBfsIterations = 5000;

        /// <summary>Default terrain supply cost for passable terrain.</summary>
        private const int DefaultTerrainCost = 1;

        /// <summary>Terrain supply cost for difficult terrain (mountains, swamps, etc.).</summary>
        private const int DifficultTerrainCost = 2;

        /// <summary>Terrain supply cost for easy terrain (roads, clear terrain).</summary>
        private const int EasyTerrainCost = 1;

        // =================================================================================
        // Main Evaluation
        // =================================================================================

        /// <summary>
        /// Evaluates the supply status of a unit by performing BFS from the supply source
        /// to the unit's position, accounting for terrain costs and enemy-blocking positions.
        /// </summary>
        /// <param name="unitPos">The hex coordinate of the unit requiring supply.</param>
        /// <param name="supplySource">The hex coordinate of the supply source (city, depot, etc.).</param>
        /// <param name="grid">The hex grid for neighbor traversal.</param>
        /// <param name="enemyPositions">
        /// List of hex coordinates occupied by enemy units. Any hex occupied by an enemy
        /// blocks supply flow through that hex.
        /// </param>
        /// <returns>
        /// A SupplyStatus struct indicating whether the unit is supplied, the efficiency
        /// of the supply line, and the path length.
        /// </returns>
        public SupplyStatus EvaluateSupply(
            HexCoord unitPos,
            HexCoord supplySource,
            HexGrid grid,
            List<HexCoord> enemyPositions)
        {
            SupplyStatus result = new SupplyStatus
            {
                isSupplied = false,
                efficiency = 0f,
                pathLength = 0
            };

            // Null checks
            if (unitPos == null || supplySource == null || grid == null)
            {
                Debug.LogWarning("[SupplyLineSystem] EvaluateSupply: null parameter provided.");
                return result;
            }

            // If the unit is on the supply source, it's fully supplied
            if (unitPos.Equals(supplySource))
            {
                result.isSupplied = true;
                result.efficiency = 1.0f;
                result.pathLength = 0;
                return result;
            }

            // Build enemy position lookup set
            HashSet<HexCoord> enemySet = enemyPositions != null
                ? new HashSet<HexCoord>(enemyPositions)
                : new HashSet<HexCoord>();

            // Remove the unit's own position from the enemy set (in case of stale data)
            enemySet.Remove(unitPos);

            // BFS from supply source to unit position
            Queue<SupplyPathNode> queue = new Queue<SupplyPathNode>();
            Dictionary<HexCoord, SupplyPathNode> visited = new Dictionary<HexCoord, SupplyPathNode>();

            SupplyPathNode startNode = new SupplyPathNode(supplySource, 0, 0, null);
            queue.Enqueue(startNode);
            visited[supplySource] = startNode;

            int iterations = 0;

            while (queue.Count > 0 && iterations < MaxBfsIterations)
            {
                iterations++;
                SupplyPathNode current = queue.Dequeue();

                // Check if we've reached the target unit
                if (current.Position.Equals(unitPos))
                {
                    result.isSupplied = true;
                    result.pathLength = current.PathLength;

                    // Calculate efficiency based on path length
                    // Efficiency decreases with distance: starts at 1.0 and decays
                    float distanceDecay = 1.0f - (current.PathLength * EfficiencyDecayPerHex);
                    distanceDecay = Mathf.Max(MinimumEfficiency, distanceDecay);

                    // Terrain difficulty penalty (based on accumulated cost vs path length)
                    float terrainPenalty = 1.0f;
                    if (current.PathLength > 0)
                    {
                        float avgCostPerHex = (float)current.Cost / current.PathLength;
                        terrainPenalty = 1.0f / Mathf.Max(1f, avgCostPerHex);
                    }

                    result.efficiency = Mathf.Clamp01(distanceDecay * terrainPenalty);
                    return result;
                }

                // Don't explore beyond max supply range
                if (current.PathLength >= MaxSupplyRange)
                {
                    continue;
                }

                // Explore neighbors
                HexCoord[] neighbors = grid.GetNeighbors(current.Position);
                if (neighbors == null)
                {
                    continue;
                }

                foreach (HexCoord neighbor in neighbors)
                {
                    if (neighbor == null)
                    {
                        continue;
                    }

                    // Skip already visited nodes
                    if (visited.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    // Skip hexes occupied by enemies (supply is cut through enemy positions)
                    if (enemySet.Contains(neighbor))
                    {
                        // Mark as visited so we don't retry, but don't enqueue
                        visited[neighbor] = null;
                        continue;
                    }

                    // Get terrain cost for this hex
                    int terrainCost = GetTerrainSupplyCost(neighbor, grid);
                    int newCost = current.Cost + terrainCost;
                    int newPathLength = current.PathLength + 1;

                    SupplyPathNode neighborNode = new SupplyPathNode(
                        neighbor, newCost, newPathLength, current.Position);

                    visited[neighbor] = neighborNode;
                    queue.Enqueue(neighborNode);
                }
            }

            // BFS exhausted without reaching the unit — supply is cut
            Debug.Log($"[SupplyLineSystem] Supply line cut: could not reach unit at " +
                      $"{unitPos} from source at {supplySource} after {iterations} iterations.");
            return result;
        }

        // =================================================================================
        // Interdiction Point Detection
        // =================================================================================

        /// <summary>
        /// Identifies vulnerable interdiction points along the supply path between
        /// a unit and its supply source. These are hexes where an enemy unit could
        /// be placed to cut or significantly degrade the supply line.
        /// </summary>
        /// <param name="unitPos">The hex coordinate of the unit requiring supply.</param>
        /// <param name="supplySource">The hex coordinate of the supply source.</param>
        /// <param name="grid">The hex grid for pathfinding.</param>
        /// <returns>
        /// A list of HexCoord positions that are critical supply route hexes.
        /// An enemy unit placed on any of these hexes would cut the supply line.
        /// Returns an empty list if the unit is not supplied or positions are invalid.
        /// </returns>
        /// <remarks>
        /// Interdiction points are identified by performing BFS and recording the path
        /// from source to unit. Every hex along this path is a potential interdiction point.
        /// Points closer to the source (bottlenecks) are listed first as they affect
        /// more downstream units.
        /// </remarks>
        public List<HexCoord> GetInterdictionPoints(
            HexCoord unitPos,
            HexCoord supplySource,
            HexGrid grid)
        {
            List<HexCoord> interdictionPoints = new List<HexCoord>();

            if (unitPos == null || supplySource == null || grid == null)
            {
                Debug.LogWarning("[SupplyLineSystem] GetInterdictionPoints: null parameter provided.");
                return interdictionPoints;
            }

            if (unitPos.Equals(supplySource))
            {
                // Unit is on the source — no interdiction points
                return interdictionPoints;
            }

            // BFS to find path, tracking parent nodes
            Queue<SupplyPathNode> queue = new Queue<SupplyPathNode>();
            Dictionary<HexCoord, SupplyPathNode> visited = new Dictionary<HexCoord, SupplyPathNode>();

            SupplyPathNode startNode = new SupplyPathNode(supplySource, 0, 0, null);
            queue.Enqueue(startNode);
            visited[supplySource] = startNode;

            SupplyPathNode targetNode = null;
            int iterations = 0;

            while (queue.Count > 0 && iterations < MaxBfsIterations)
            {
                iterations++;
                SupplyPathNode current = queue.Dequeue();

                if (current.Position.Equals(unitPos))
                {
                    targetNode = current;
                    break;
                }

                if (current.PathLength >= MaxSupplyRange)
                {
                    continue;
                }

                HexCoord[] neighbors = grid.GetNeighbors(current.Position);
                if (neighbors == null)
                {
                    continue;
                }

                foreach (HexCoord neighbor in neighbors)
                {
                    if (neighbor == null || visited.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    int terrainCost = GetTerrainSupplyCost(neighbor, grid);
                    SupplyPathNode neighborNode = new SupplyPathNode(
                        neighbor,
                        current.Cost + terrainCost,
                        current.PathLength + 1,
                        current.Position);

                    visited[neighbor] = neighborNode;
                    queue.Enqueue(neighborNode);
                }
            }

            // Trace back the path from target to source using parent references
            if (targetNode != null)
            {
                SupplyPathNode traceNode = targetNode;

                // Walk back to source, collecting all hexes on the path (excluding source and target)
                while (traceNode != null && traceNode.Parent != null)
                {
                    interdictionPoints.Add(traceNode.Position);
                    traceNode = visited.ContainsKey(traceNode.Parent) ? visited[traceNode.Parent] : null;
                }

                // Reverse so the list goes from source toward unit (bottlenecks first)
                interdictionPoints.Reverse();
            }

            return interdictionPoints;
        }

        // =================================================================================
        // Terrain Supply Cost
        // =================================================================================

        /// <summary>
        /// Determines the supply cost of traversing a hex based on its terrain type.
        /// Higher costs mean supply efficiency degrades faster through this terrain.
        /// </summary>
        /// <param name="hex">The hex coordinate to evaluate.</param>
        /// <param name="grid">The hex grid for terrain lookups.</param>
        /// <returns>
        /// The supply cost for traversing this hex:
        /// <list type="bullet">
        ///   <item>1 = Normal terrain (plains, grassland)</item>
        ///   <item>2 = Difficult terrain (mountains, swamps, forests)</item>
        /// </list>
        /// </returns>
        private int GetTerrainSupplyCost(HexCoord hex, HexGrid grid)
        {
            if (grid == null || hex == null)
            {
                return DefaultTerrainCost;
            }

            // Attempt to get terrain data from the grid
            TerrainType terrain = grid.GetTerrain(hex);

            switch (terrain)
            {
                // Easy terrain — supply flows freely
                case TerrainType.Road:
                case TerrainType.Plains:
                case TerrainType.Desert:
                    return EasyTerrainCost;

                // Difficult terrain — supply is harder to push through
                case TerrainType.Mountain:
                case TerrainType.Swamp:
                case TerrainType.Forest:
                case TerrainType.Jungle:
                    return DifficultTerrainCost;

                // Water terrain — supply requires naval transport
                case TerrainType.Water:
                case TerrainType.DeepWater:
                    return DifficultTerrainCost;

                // Urban terrain — supply actually flows well through cities
                case TerrainType.Urban:
                    return EasyTerrainCost;

                default:
                    return DefaultTerrainCost;
            }
        }

        // =================================================================================
        // Utility: Supply Path Length Estimation
        // =================================================================================

        /// <summary>
        /// Estimates the hex distance between two positions without performing full pathfinding.
        /// Uses the hex grid's built-in distance calculation.
        /// </summary>
        /// <param name="from">Starting hex coordinate.</param>
        /// <param name="to">Destination hex coordinate.</param>
        /// <returns>The estimated hex distance, or -1 if inputs are invalid.</returns>
        public static int EstimateSupplyDistance(HexCoord from, HexCoord to)
        {
            if (from == null || to == null)
                return -1;

            return HexCoord.GetDistance(from, to);
        }
    }
}
