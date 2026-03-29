// ============================================================================================
// IRON PROTOCOL - HexMapRenderer.cs
// MonoBehaviour that renders the hex grid as a single combined Unity Mesh for
// optimal draw-call performance. Supports per-cell coloring and highlight overlays.
// Uses flat-topped hexagon geometry.
// ============================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.HexMap
{
    /// <summary>
    /// Renders a <see cref="HexGrid"/> as a single combined mesh using flat-topped hexagons.
    /// <para>
    /// For performance, all hex cells are merged into one <see cref="Mesh"/> so the grid
    /// is rendered with a single draw call. Per-cell color changes rebuild only the color
    /// buffer; the geometry remains unchanged unless the grid is regenerated.
    /// </para>
    /// <para>
    /// Attach this component to a GameObject that also has a <see cref="HexGrid"/>
    /// component on the same or parent object.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HexMapRenderer : MonoBehaviour
    {
        // ----------------------------------------------------------------------------------------
        // Configuration
        // ----------------------------------------------------------------------------------------

        [Header("Materials")]
        [Tooltip("Material used for the base hex grid mesh. Must support vertex colors.")]
        [SerializeField]
        private Material baseMaterial;

        [Header("Visual Settings")]
        [Tooltip("Default color for hex cells when no terrain color is specified.")]
        [SerializeField]
        private Color defaultCellColor = new Color(0.45f, 0.65f, 0.35f);

        [Tooltip("Height offset between cells of different elevation levels.")]
        [SerializeField]
        private float elevationStep = 0.2f;

        [Header("References")]
        [Tooltip("The HexGrid component that provides cell data. Auto-found if null.")]
        [SerializeField]
        private HexGrid grid;

        // ----------------------------------------------------------------------------------------
        // Runtime State
        // ----------------------------------------------------------------------------------------

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _gridMesh;
        private Dictionary<HexCoord, int> _coordToTriangleIndex; // coord -> first vertex index of cell
        private List<Color> _vertexColors;
        private List<Color> _highlightColors;
        private bool _hasHighlights;

        // Flat-topped hex vertex angles (6 corners).
        private static readonly float[] HexVertexAngles =
        {
            0f, 60f, 120f, 180f, 240f, 300f
        };

        /// <summary>
        /// Gets the base material assigned to the grid renderer.
        /// </summary>
        public Material BaseMaterial => baseMaterial;

        // ----------------------------------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------------------------------

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Start()
        {
            if (grid == null)
            {
                grid = GetComponent<HexGrid>() ?? GetComponentInParent<HexGrid>();
            }

            if (grid != null)
            {
                RenderGrid(grid);
            }
            else
            {
                Debug.LogWarning("[HexMapRenderer] No HexGrid reference found. Assign one in the Inspector.");
            }
        }

        // ----------------------------------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Builds the visual mesh from the given hex grid data.
        /// <para>
        /// Destroys any previously generated mesh. All hex cells are tessellated as
        /// flat-topped hexagons and combined into a single <see cref="Mesh"/>.
        /// </para>
        /// </summary>
        /// <param name="hexGrid">The <see cref="HexGrid"/> to render.</param>
        public void RenderGrid(HexGrid hexGrid)
        {
            if (hexGrid == null)
            {
                Debug.LogError("[HexMapRenderer] Cannot render a null HexGrid.");
                return;
            }

            grid = hexGrid;
            _coordToTriangleIndex = new Dictionary<HexCoord, int>();

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            _vertexColors = new List<Color>();
            _highlightColors = new List<Color>();
            _hasHighlights = false;

            float hexSize = grid.HexSize;
            Vector3 origin = grid.GridOrigin;

            // Inner radius for flat-topped hex (apothem).
            float innerRadius = hexSize * 0.8660254037844386f; // Mathf.Sqrt(3) / 2

            foreach (HexCell cell in grid.GetAllCells())
            {
                int firstVertexIndex = vertices.Count;
                _coordToTriangleIndex[cell.Coord] = firstVertexIndex;

                // Compute world center for this cell.
                Vector3 center = cell.Coord.HexToWorld(origin, hexSize);
                center.y += cell.Elevation * elevationStep;

                // Create 6 outer vertices.
                for (int i = 0; i < 6; i++)
                {
                    float angleRad = HexVertexAngles[i] * Mathf.Deg2Rad;
                    float vx = center.x + hexSize * Mathf.Cos(angleRad);
                    float vz = center.z + hexSize * Mathf.Sin(angleRad);
                    vertices.Add(new Vector3(vx, center.y, vz));
                }

                // Create center vertex.
                Vector3 centerVertex = new Vector3(center.x, center.y, center.z);
                int centerIndex = vertices.Count;
                vertices.Add(centerVertex);

                // Create 6 triangles (fan triangulation).
                for (int i = 0; i < 6; i++)
                {
                    int next = (i + 1) % 6;
                    triangles.Add(firstVertexIndex + i);   // outer i
                    triangles.Add(firstVertexIndex + next);  // outer i+1
                    triangles.Add(centerIndex);              // center
                }

                // Set default color for all 7 vertices of this cell.
                Color cellColor = GetTerrainColor(cell.Terrain);
                for (int i = 0; i < 7; i++)
                {
                    _vertexColors.Add(cellColor);
                    _highlightColors.Add(Color.clear);
                }
            }

            // Build the mesh.
            if (_gridMesh != null)
            {
                Destroy(_gridMesh);
            }

            _gridMesh = new Mesh
            {
                name = "HexGridMesh",
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray()
            };
            _gridMesh.RecalculateNormals();
            _gridMesh.RecalculateBounds();
            _gridMesh.colors = _vertexColors.ToArray();

            _meshFilter.sharedMesh = _gridMesh;

            if (baseMaterial != null)
            {
                _meshRenderer.sharedMaterial = baseMaterial;
            }

            Debug.Log($"[HexMapRenderer] Rendered grid mesh: {vertices.Count} vertices, {triangles.Count / 3} triangles.");
        }

        /// <summary>
        /// Changes the base color of a single hex cell. Rebuilds only the color buffer.
        /// </summary>
        /// <param name="coord">The hex coordinate to recolor.</param>
        /// <param name="color">The new base color.</param>
        public void ColorCell(HexCoord coord, Color color)
        {
            if (_gridMesh == null || _coordToTriangleIndex == null || !_coordToTriangleIndex.TryGetValue(coord, out int vi))
            {
                return;
            }

            for (int i = 0; i < 7; i++)
            {
                _vertexColors[vi + i] = color;
            }

            _gridMesh.colors = _vertexColors.ToArray();
        }

        /// <summary>
        /// Applies a translucent highlight overlay to a single hex cell.
        /// The highlight is blended with the base cell color in the shader.
        /// </summary>
        /// <param name="coord">The hex coordinate to highlight.</param>
        /// <param name="color">The highlight color.</param>
        /// <param name="alpha">Opacity of the highlight (0 = invisible, 1 = fully opaque).</param>
        public void HighlightCell(HexCoord coord, Color color, float alpha)
        {
            if (_gridMesh == null || _coordToTriangleIndex == null || !_coordToTriangleIndex.TryGetValue(coord, out int vi))
            {
                return;
            }

            Color highlight = new Color(color.r, color.g, color.b, alpha);
            for (int i = 0; i < 7; i++)
            {
                _highlightColors[vi + i] = highlight;
            }

            _hasHighlights = true;
            ApplyHighlightColors();
        }

        /// <summary>
        /// Removes all highlight overlays from every cell, restoring original colors.
        /// </summary>
        public void ClearHighlights()
        {
            if (_highlightColors == null || _gridMesh == null)
            {
                return;
            }

            for (int i = 0; i < _highlightColors.Count; i++)
            {
                _highlightColors[i] = Color.clear;
            }

            _hasHighlights = false;
            _gridMesh.colors = _vertexColors.ToArray();
        }

        /// <summary>
        /// Colors all cells of a specific terrain type with the given color.
        /// </summary>
        /// <param name="terrainType">The terrain type to target.</param>
        /// <param name="color">The color to apply.</param>
        public void ColorAllOfTerrain(TerrainType terrainType, Color color)
        {
            if (grid == null) return;

            foreach (HexCell cell in grid.GetAllCells())
            {
                if (cell.Terrain == terrainType)
                {
                    ColorCell(cell.Coord, color);
                }
            }
        }

        // ----------------------------------------------------------------------------------------
        // Internal
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Returns a default color for a given terrain type, used when no custom
        /// terrain definition is provided.
        /// </summary>
        private Color GetTerrainColor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plains:   return new Color(0.55f, 0.78f, 0.38f);
                case TerrainType.Forest:   return new Color(0.22f, 0.55f, 0.22f);
                case TerrainType.Mountain: return new Color(0.55f, 0.50f, 0.48f);
                case TerrainType.Water:    return new Color(0.25f, 0.50f, 0.80f);
                case TerrainType.Urban:    return new Color(0.70f, 0.68f, 0.62f);
                case TerrainType.Desert:   return new Color(0.85f, 0.78f, 0.50f);
                case TerrainType.Road:     return new Color(0.60f, 0.58f, 0.55f);
                default:                   return defaultCellColor;
            }
        }

        /// <summary>
        /// Blends highlight colors with base vertex colors and applies them to the mesh.
        /// </summary>
        private void ApplyHighlightColors()
        {
            if (_gridMesh == null || _vertexColors == null || _highlightColors == null) return;

            var blendedColors = new Color[_vertexColors.Count];
            for (int i = 0; i < blendedColors.Length; i++)
            {
                blendedColors[i] = Color.Lerp(_vertexColors[i], _highlightColors[i], _highlightColors[i].a);
            }

            _gridMesh.colors = blendedColors;
        }

        // ----------------------------------------------------------------------------------------
        // Cleanup
        // ----------------------------------------------------------------------------------------

        private void OnDestroy()
        {
            if (_gridMesh != null)
            {
                Destroy(_gridMesh);
                _gridMesh = null;
            }
        }
    }
}
