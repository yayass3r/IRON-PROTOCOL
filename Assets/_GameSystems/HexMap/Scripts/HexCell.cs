// ============================================================================================
// IRON PROTOCOL - HexCell.cs
// Defines terrain types and the serializable data class for a single hex cell.
// ============================================================================================

using UnityEngine;

namespace IronProtocol.HexMap
{
    /// <summary>
    /// Enumeration of all terrain types available in IRON PROTOCOL.
    /// Each terrain type has distinct movement costs, defense bonuses, and strategic value.
    /// </summary>
    public enum TerrainType
    {
        /// <summary>Open grassland. Standard movement and no defensive bonus.</summary>
        Plains,

        /// <summary>Dense woodland. Slows movement, provides moderate defense bonus.</summary>
        Forest,

        /// <summary>Impassable high ground. Provides maximum defense; limited unit access.</summary>
        Mountain,

        /// <summary>Deep water. Only naval units may traverse.</summary>
        Water,

        /// <summary>Developed city hex. High defense, generates resources.</summary>
        Urban,

        /// <summary>Arid sand. Increases movement cost, minor defense penalty.</summary>
        Desert,

        /// <summary>Paved road. Reduced movement cost for rapid deployment.</summary>
        Road
    }

    /// <summary>
    /// Serializable data container for a single hex cell on the game map.
    /// <para>
    /// Stores position, terrain, ownership, city information, population, and combat
    /// modifiers. Designed to be easily serialized and used by both the logic and
    /// rendering layers.
    /// </para>
    /// </summary>
    [Serializable]
    public class HexCell
    {
        // ----------------------------------------------------------------------------------------
        // Position
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// The axial coordinate of this cell on the hex grid.
        /// </summary>
        [SerializeField]
        private int q;

        /// <summary>
        /// The axial coordinate of this cell on the hex grid (r component).
        /// </summary>
        [SerializeField]
        private int r;

        /// <summary>
        /// Gets the hex coordinate (position) of this cell.
        /// </summary>
        public HexCoord Coord => new HexCoord(q, r);

        // ----------------------------------------------------------------------------------------
        // Terrain & Elevation
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// The terrain type of this hex cell.
        /// </summary>
        [Tooltip("Terrain type for this hex cell.")]
        [SerializeField]
        private TerrainType terrain = TerrainType.Plains;

        /// <summary>
        /// Gets or sets the terrain type of this cell.
        /// </summary>
        public TerrainType Terrain
        {
            get => terrain;
            set => terrain = value;
        }

        /// <summary>
        /// The elevation of this cell in abstract units. Affects visual height and
        /// line-of-sight calculations. Higher elevation provides combat advantages.
        /// </summary>
        [Tooltip("Elevation height (affects visuals and line-of-sight).")]
        [SerializeField]
        [Range(0, 10)]
        private int elevation;

        /// <summary>
        /// Gets or sets the elevation of this cell.
        /// </summary>
        public int Elevation
        {
            get => elevation;
            set => elevation = Mathf.Clamp(value, 0, 10);
        }

        // ----------------------------------------------------------------------------------------
        // Ownership
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// The nation ID that currently controls this cell.
        /// A value of -1 indicates no owner (unclaimed territory).
        /// </summary>
        [Tooltip("Nation ID that owns this cell (-1 = unclaimed).")]
        [SerializeField]
        private int ownerNationId = -1;

        /// <summary>
        /// Gets or sets the nation ID that owns this cell.
        /// </summary>
        public int OwnerNationId
        {
            get => ownerNationId;
            set => ownerNationId = value;
        }

        // ----------------------------------------------------------------------------------------
        // City
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Whether this cell contains a city settlement.
        /// </summary>
        [Tooltip("Whether a city is located on this cell.")]
        [SerializeField]
        private bool hasCity;

        /// <summary>
        /// Gets or sets whether this cell has a city.
        /// </summary>
        public bool HasCity
        {
            get => hasCity;
            set => hasCity = value;
        }

        /// <summary>
        /// Whether this cell contains the capital city of a nation.
        /// Capital cities provide unique strategic bonuses.
        /// </summary>
        [Tooltip("Whether this city is a nation capital.")]
        [SerializeField]
        private bool isCapital;

        /// <summary>
        /// Gets or sets whether this cell is a capital city.
        /// </summary>
        public bool IsCapital
        {
            get => isCapital;
            set => isCapital = value;
        }

        /// <summary>
        /// The name of the city located on this cell. Empty if no city is present.
        /// </summary>
        [Tooltip("Name of the city on this cell.")]
        [SerializeField]
        private string cityName = string.Empty;

        /// <summary>
        /// Gets or sets the city name.
        /// </summary>
        public string CityName
        {
            get => cityName;
            set => cityName = value;
        }

        // ----------------------------------------------------------------------------------------
        // Population & Combat Modifiers
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// The population of the city on this cell. Relevant only when
        /// <see cref="HasCity"/> is true.
        /// </summary>
        [Tooltip("Population count (relevant for city cells).")]
        [SerializeField]
        private int population;

        /// <summary>
        /// Gets or sets the population of this cell's city.
        /// </summary>
        public int Population
        {
            get => population;
            set => population = Mathf.Max(0, value);
        }

        /// <summary>
        /// Flat defense bonus granted to units defending on this cell.
        /// Added directly to the defender's combat stat.
        /// </summary>
        [Tooltip("Flat defense bonus for units on this cell.")]
        [SerializeField]
        private int defenseBonus;

        /// <summary>
        /// Gets or sets the flat defense bonus of this cell.
        /// </summary>
        public int DefenseBonus
        {
            get => defenseBonus;
            set => defenseBonus = value;
        }

        /// <summary>
        /// Movement point cost to enter this cell. Higher values slow units.
        /// A value of 0 means impassable (or requires special traversal ability).
        /// </summary>
        [Tooltip("Movement point cost to enter this cell.")]
        [SerializeField]
        private int movementCost = 1;

        /// <summary>
        /// Gets or sets the movement cost to enter this cell.
        /// </summary>
        public int MovementCost
        {
            get => movementCost;
            set => movementCost = Mathf.Max(0, value);
        }

        // ----------------------------------------------------------------------------------------
        // Constructors
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Default constructor. Creates a Plains cell at coordinate (0, 0).
        /// </summary>
        public HexCell() { }

        /// <summary>
        /// Creates a new hex cell at the specified coordinate with the given terrain type.
        /// </summary>
        /// <param name="coord">The axial coordinate for this cell.</param>
        /// <param name="terrainType">The terrain type.</param>
        public HexCell(HexCoord coord, TerrainType terrainType)
        {
            q = coord.Q;
            r = coord.R;
            terrain = terrainType;
        }

        /// <summary>
        /// Creates a new hex cell with fully specified properties.
        /// </summary>
        /// <param name="coord">The axial coordinate.</param>
        /// <param name="terrainType">The terrain type.</param>
        /// <param name="elev">Elevation value (0–10).</param>
        /// <param name="ownerId">Owning nation ID (-1 for unclaimed).</param>
        /// <param name="city">Whether a city is present.</param>
        /// <param name="capital">Whether the city is a capital.</param>
        /// <param name="cityNameStr">City name (empty if no city).</param>
        /// <param name="pop">City population.</param>
        /// <param name="defBonus">Defense bonus.</param>
        /// <param name="moveCost">Movement cost to enter.</param>
        public HexCell(HexCoord coord, TerrainType terrainType, int elev, int ownerId,
            bool city, bool capital, string cityNameStr, int pop, int defBonus, int moveCost)
        {
            q = coord.Q;
            r = coord.R;
            terrain = terrainType;
            elevation = Mathf.Clamp(elev, 0, 10);
            ownerNationId = ownerId;
            hasCity = city;
            isCapital = capital;
            cityName = cityNameStr ?? string.Empty;
            population = Mathf.Max(0, pop);
            defenseBonus = defBonus;
            movementCost = Mathf.Max(0, moveCost);
        }

        // ----------------------------------------------------------------------------------------
        // Utility
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Returns a string summary of this cell for debugging and logging.
        /// </summary>
        public override string ToString()
        {
            string cityInfo = hasCity ? $" [{cityName}{(isCapital ? " (Capital)" : "")} Pop:{population}]" : "";
            return $"HexCell {Coord} | {terrain} | Elev:{elevation} | Owner:{ownerNationId} | Def:+{defenseBonus} | Move:{movementCost}{cityInfo}";
        }
    }
}
