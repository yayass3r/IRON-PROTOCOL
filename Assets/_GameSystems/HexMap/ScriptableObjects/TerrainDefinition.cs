// ============================================================================================
// IRON PROTOCOL - TerrainDefinition.cs
// ScriptableObject that defines the properties and visual representation of a
// single terrain type. Create instances via the Unity asset menu.
// ============================================================================================

using UnityEngine;

namespace IronProtocol.HexMap
{
    /// <summary>
    /// A ScriptableObject that defines all gameplay and visual properties for a terrain type.
    /// <para>
    /// Create new instances via <b>Assets &gt; Create &gt; IRON PROTOCOL &gt; Terrain Definition</b>.
    /// Reference these assets from systems that need terrain data (HexMapRenderer, HexGrid,
    /// combat calculators, etc.).
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewTerrainDefinition",
        menuName = "IRON PROTOCOL/Terrain Definition",
        order = 100)]
    public class TerrainDefinition : ScriptableObject
    {
        // ----------------------------------------------------------------------------------------
        // Identity
        // ----------------------------------------------------------------------------------------

        [Header("Identity")]
        [Tooltip("The terrain type this definition corresponds to.")]
        [SerializeField]
        private TerrainType terrainType = TerrainType.Plains;

        /// <summary>
        /// Gets the terrain type this definition describes.
        /// </summary>
        public TerrainType TerrainType => terrainType;

        [Tooltip("Human-readable name displayed in the UI.")]
        [SerializeField]
        private string terrainName = "New Terrain";

        /// <summary>
        /// Gets the display name of this terrain type.
        /// </summary>
        public string TerrainName => terrainName;

        // ----------------------------------------------------------------------------------------
        // Gameplay Properties
        // ----------------------------------------------------------------------------------------

        [Header("Gameplay")]
        [Tooltip("Flat defense bonus granted to units defending on this terrain.")]
        [SerializeField, Range(-10, 20)]
        private int defenseBonus;

        /// <summary>
        /// Gets the flat defense bonus for units on this terrain.
        /// </summary>
        public int DefenseBonus => defenseBonus;

        [Tooltip("Movement point cost to enter a hex of this terrain. 0 = impassable.")]
        [SerializeField, Min(0)]
        private int movementCost = 1;

        /// <summary>
        /// Gets the movement cost to enter a hex of this terrain type.
        /// A value of 0 indicates the terrain is impassable by normal units.
        /// </summary>
        public int MovementCost => movementCost;

        [Tooltip("Whether standard ground units can traverse this terrain.")]
        [SerializeField]
        private bool isPassable = true;

        /// <summary>
        /// Gets whether standard ground units can traverse this terrain.
        /// </summary>
        public bool IsPassable => isPassable;

        [Tooltip("Whether this terrain provides cover (reduces incoming ranged damage).")]
        [SerializeField]
        private bool providesCover;

        /// <summary>
        /// Gets whether this terrain provides cover, reducing incoming ranged attack damage.
        /// </summary>
        public bool ProvidesCover => providesCover;

        // ----------------------------------------------------------------------------------------
        // Combat Modifiers
        // ----------------------------------------------------------------------------------------

        [Header("Combat Modifiers")]
        [Tooltip("Multiplier applied to the attack stat of units on this terrain (1.0 = no change).")]
        [SerializeField, Range(0.1f, 3f)]
        private float atkModifier = 1f;

        /// <summary>
        /// Gets the attack modifier multiplier for units on this terrain.
        /// A value of 1.0 means no modification. Values &gt; 1.0 increase attack power.
        /// </summary>
        public float AtkModifier => atkModifier;

        [Tooltip("Multiplier applied to the defense stat of units on this terrain (1.0 = no change).")]
        [SerializeField, Range(0.1f, 3f)]
        private float defModifier = 1f;

        /// <summary>
        /// Gets the defense modifier multiplier for units on this terrain.
        /// A value of 1.0 means no modification. Values &gt; 1.0 increase defense power.
        /// </summary>
        public float DefModifier => defModifier;

        // ----------------------------------------------------------------------------------------
        // Visual Properties
        // ----------------------------------------------------------------------------------------

        [Header("Visuals")]
        [Tooltip("Base color of hexes with this terrain on the map.")]
        [SerializeField]
        private Color color = Color.green;

        /// <summary>
        /// Gets the base color used to render hexes of this terrain type.
        /// </summary>
        public Color Color => color;

        [Tooltip("Icon displayed on the terrain info panel and minimap.")]
        [SerializeField]
        private Sprite icon;

        /// <summary>
        /// Gets the UI icon for this terrain type.
        /// </summary>
        public Sprite Icon => icon;

        // ----------------------------------------------------------------------------------------
        // Utility
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Returns a formatted summary string of this terrain definition for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"[{terrainType}] {terrainName} | Def:+{defenseBonus} Move:{movementCost} " +
                   $"Passable:{isPassable} Cover:{providesCover} " +
                   $"ATK×{atkModifier:F2} DEF×{defModifier:F2}";
        }
    }
}
