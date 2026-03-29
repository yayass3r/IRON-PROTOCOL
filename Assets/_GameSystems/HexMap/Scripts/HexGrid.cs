// ============================================================================================
// IRON PROTOCOL - HexGrid.cs
// MonoBehaviour that manages the hex grid data model: generation, lookup,
// neighbor queries, range queries, and terrain movement cost calculation.
// ============================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.HexMap
{
    /// <summary>
    /// Manages the logical hex grid for IRON PROTOCOL. Responsible for generating cells,
    /// performing coordinate-based lookups, neighbor enumeration, range queries, and
    /// terrain-based movement cost calculation.
    /// <para>
    /// Attach this to a GameObject in the scene and configure <see cref="width"/>,
    /// <see cref="height"/>, and <see cref="hexSize"/> in the Inspector. Call
    /// <see cref="GenerateGrid"/> to populate the grid at runtime.
    /// </para>
    /// </summary>
    public class HexGrid : MonoBehaviour
    {
        // ----------------------------------------------------------------------------------------
        // Configuration
        // ----------------------------------------------------------------------------------------

        [Header("Grid Dimensions")]
        [Tooltip("Number of columns in the hex grid.")]
        [SerializeField, Min(1)]
        private int width = 20;

        [Tooltip("Number of rows in the hex grid.")]
        [SerializeField, Min(1)]
        private int height = 15;

        [Header("Hex Properties")]
        [Tooltip("Outer radius (center to vertex) of a single hex cell.")]
        [SerializeField, Min(0.1f)]
        private float hexSize = 1f;

        // ----------------------------------------------------------------------------------------
        // Runtime State
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Dictionary mapping hex coordinates to their cell data.
        /// </summary>
        private Dictionary<HexCoord, HexCell> _cells = new Dictionary<HexCoord, HexCell>();

        /// <summary>
        /// The world-space origin of the grid (position of hex 0,0).
        /// </summary>
        private Vector3 _gridOrigin;

        /// <summary>
        /// Gets the grid width (number of columns).
        /// </summary>
        public int Width => width;

        /// <summary>
        /// Gets the grid height (number of rows).
        /// </summary>
        public int Height => height;

        /// <summary>
        /// Gets the hex outer radius (center to vertex).
        /// </summary>
        public float HexSize => hexSize;

        /// <summary>
        /// Gets the world-space origin position of the grid.
        /// </summary>
        public Vector3 GridOrigin => _gridOrigin;

        /// <summary>
        /// Gets the total number of cells currently in the grid.
        /// </summary>
        public int CellCount => _cells.Count;

        // ----------------------------------------------------------------------------------------
        // Initialization
        // ----------------------------------------------------------------------------------------

        private void Awake()
        {
            _gridOrigin = transform.position;
        }

        // ----------------------------------------------------------------------------------------
        // Grid Generation
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Generates the full hex grid, populating cells from (0, 0) to
        /// (<see cref="Width"/> - 1, <see cref="Height"/> - 1) with default Plains terrain.
        /// <para>Clears any previously generated cells before regenerating.</para>
        /// </summary>
        public void GenerateGrid()
        {
            _cells.Clear();

            for (int r = 0; r < height; r++)
            {
                for (int q = 0; q < width; q++)
                {
                    var coord = new HexCoord(q, r);
                    var cell = new HexCell(coord, TerrainType.Plains);
                    _cells[coord] = cell;
                }
            }

            Debug.Log($"[HexGrid] Generated {_cells.Count} cells ({width}x{height}).");
        }

        /// <summary>
        /// Generates the hex grid using a provided terrain data array.
        /// The array must have exactly <see cref="Width"/> * <see cref="Height"/> elements,
        /// indexed row-major (r * width + q).
        /// </summary>
        /// <param name="terrainData">Terrain type for each cell, row-major order.</param>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the terrain data length does not match the grid dimensions.
        /// </exception>
        public void GenerateGrid(TerrainType[] terrainData)
        {
            if (terrainData == null || terrainData.Length != width * height)
            {
                Debug.LogError($"[HexGrid] Terrain data length mismatch. Expected {width * height}.");
                throw new System.ArgumentException(
                    $"Terrain data must have exactly {width * height} elements.");
            }

            _cells.Clear();

            for (int r = 0; r < height; r++)
            {
                for (int q = 0; q < width; q++)
                {
                    var coord = new HexCoord(q, r);
                    TerrainType terrain = terrainData[r * width + q];
                    var cell = new HexCell(coord, terrain);
                    _cells[coord] = cell;
                }
            }

            Debug.Log($"[HexGrid] Generated {_cells.Count} cells with custom terrain data.");
        }

        // ----------------------------------------------------------------------------------------
        // Cell Lookup
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the <see cref="HexCell"/> at the specified coordinate, or
        /// <c>null</c> if the coordinate is outside the grid or unoccupied.
        /// </summary>
        /// <param name="coord">The hex coordinate to look up.</param>
        /// <returns>The cell, or null if not found.</returns>
        public HexCell GetCell(HexCoord coord)
        {
            _cells.TryGetValue(coord, out HexCell cell);
            return cell;
        }

        /// <summary>
        /// Returns all valid neighbor cells adjacent to the specified coordinate.
        /// Neighbors that fall outside the grid bounds are excluded.
        /// </summary>
        /// <param name="coord">The center coordinate.</param>
        /// <returns>A list of neighboring <see cref="HexCell"/> instances (0–6 elements).</returns>
        public List<HexCell> GetNeighbors(HexCoord coord)
        {
            var neighbors = new List<HexCell>(6);
            for (int dir = 0; dir < 6; dir++)
            {
                var neighborCoord = coord.GetNeighbor(dir);
                if (_cells.TryGetValue(neighborCoord, out HexCell cell))
                {
                    neighbors.Add(cell);
                }
            }
            return neighbors;
        }

        /// <summary>
        /// Determines whether a hex coordinate falls within the grid's rectangular bounds.
        /// </summary>
        /// <param name="coord">The coordinate to validate.</param>
        /// <returns><c>true</c> if the coordinate is within bounds; <c>false</c> otherwise.</returns>
        public bool IsValidCoord(HexCoord coord)
        {
            return coord.Q >= 0 && coord.Q < width && coord.R >= 0 && coord.R < height;
        }

        /// <summary>
        /// Finds the hex cell closest to the given world-space position.
        /// Uses <see cref="HexCoord.WorldToHex"/> for coordinate conversion.
        /// </summary>
        /// <param name="worldPos">The world-space position (typically from a raycast hit).</param>
        /// <returns>The nearest cell, or null if the coordinate is out of bounds.</returns>
        public HexCell GetCellAtWorldPos(Vector3 worldPos)
        {
            HexCoord coord = HexCoord.WorldToHex(worldPos, _gridOrigin, hexSize);
            return GetCell(coord);
        }

        /// <summary>
        /// Returns all cells currently in the grid.
        /// <para>Warning: Returns a reference to the internal collection. Do not modify.</para>
        /// </summary>
        /// <returns>A read-only collection of all <see cref="HexCell"/> instances.</returns>
        public IEnumerable<HexCell> GetAllCells()
        {
            return _cells.Values;
        }

        /// <summary>
        /// Returns all cells within the specified hex-distance range of a center coordinate.
        /// Uses breadth-first expansion for efficiency.
        /// </summary>
        /// <param name="center">The center hex coordinate.</param>
        /// <param name="range">Maximum hex distance (inclusive). Must be non-negative.</param>
        /// <returns>A list of all cells within range (excluding cells outside the grid).</returns>
        public List<HexCell> GetCellsInRange(HexCoord center, int range)
        {
            if (range < 0)
            {
                Debug.LogWarning("[HexGrid] Range must be non-negative. Returning empty list.");
                return new List<HexCell>();
            }

            var result = new List<HexCell>();

            // Cube-coordinate bounds for ring iteration.
            for (int dq = -range; dq <= range; dq++)
            {
                for (int dr = Mathf.Max(-range, -dq - range); dr <= Mathf.Min(range, -dq + range); dr++)
                {
                    var coord = new HexCoord(center.Q + dq, center.R + dr);
                    if (_cells.TryGetValue(coord, out HexCell cell))
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        // ----------------------------------------------------------------------------------------
        // Terrain Queries
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the movement cost to enter the cell at the specified coordinate.
        /// Returns <c>int.MaxValue</c> if the coordinate is out of bounds or the cell
        /// has a movement cost of 0 (impassable).
        /// </summary>
        /// <param name="coord">The hex coordinate to query.</param>
        /// <returns>The movement cost, or int.MaxValue if impassable or out of bounds.</returns>
        public int GetTerrainMoveCost(HexCoord coord)
        {
            if (!_cells.TryGetValue(coord, out HexCell cell))
            {
                Debug.LogWarning($"[HexGrid] Cannot get move cost for out-of-bounds coordinate {coord}.");
                return int.MaxValue;
            }

            return cell.MovementCost > 0 ? cell.MovementCost : int.MaxValue;
        }

        // ----------------------------------------------------------------------------------------
        // Editor & Debug
        // ----------------------------------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Draws grid boundary gizmos in the Unity Editor scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 center = _gridOrigin != Vector3.zero ? _gridOrigin : transform.position;
            float worldWidth = width * hexSize * 2f * 0.75f;
            float worldHeight = height * hexSize * 1.7320508075688772f;
            Gizmos.DrawWireCube(
                center + new Vector3(worldWidth * 0.5f, 0f, worldHeight * 0.5f),
                new Vector3(worldWidth, 0.1f, worldHeight));
        }
#endif
    }
}
