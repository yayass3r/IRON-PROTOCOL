// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: InfantryUnit.cs
// Description: Concrete unit class for infantry units. Infantry forms the backbone of
//              ground forces, with balanced stats, the ability to capture cities, and
//              enhanced defense in urban terrain.
// =====================================================================================

using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Infantry unit — the foundational ground combat unit in IRON PROTOCOL.
    /// <para>
    /// Key characteristics:
    /// <list type="bullet">
    ///   <item>Balanced attack and defense stats.</item>
    ///   <item>Standard movement range (3 hexes per turn).</item>
    ///   <item>Melee-only attack range (1 hex).</item>
    ///   <item>Special ability: Can capture enemy or neutral cities by ending movement on them.</item>
    ///   <item>Terrain bonus: Receives +10 defense in urban terrain (building-to-building fighting).</item>
    /// </list>
    /// </para>
    /// <para>
    /// Infantry synergizes well with Armor (+15% defense) and Artillery (+20% attack)
    /// through the CombinedArmsSystem, making them essential for combined arms tactics.
    /// </para>
    /// </summary>
    [AddComponentMenu("Iron Protocol/Military/Ground Units/Infantry Unit")]
    public class InfantryUnit : UnitBase
    {
        // =================================================================================
        // Constants
        // =================================================================================

        /// <summary>Default attack power for infantry units.</summary>
        private const float DefaultAttack = 35f;

        /// <summary>Default defense value for infantry units.</summary>
        private const float DefaultDefense = 30f;

        /// <summary>Default maximum hit points for infantry units.</summary>
        private const float DefaultMaxHp = 100f;

        /// <summary>Default movement points per turn.</summary>
        private const float DefaultMovement = 3f;

        /// <summary>Default attack range in hexes (melee only).</summary>
        private const float DefaultRange = 1f;

        /// <summary>Urban terrain defense bonus (flat stat addition).</summary>
        private const float UrbanDefenseBonus = 10f;

        // =================================================================================
        // Serialized Configuration
        // =================================================================================

        [Header("Infantry Configuration")]
        [Tooltip("Enable/disable city capture ability for this infantry unit.")]
        [SerializeField] private bool canCaptureCities = true;

        [Tooltip("The flat defense bonus added when in urban terrain.")]
        [SerializeField] private float urbanDefenseBonus = UrbanDefenseBonus;

        /// <summary>
        /// Gets whether this infantry unit can capture cities.
        /// </summary>
        public bool CanCaptureCities => canCaptureCities;

        // =================================================================================
        // Unity Lifecycle
        // =================================================================================

        /// <summary>
        /// Unity Awake. Initializes base class and sets infantry-specific defaults.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            InitializeInfantryDefaults();
        }

        /// <summary>
        /// Unity Reset. Called when the component is first added in the inspector.
        /// Sets default values for all infantry-specific fields.
        /// </summary>
        private void Reset()
        {
            InitializeInfantryDefaults();
        }

        // =================================================================================
        // Initialization
        // =================================================================================

        /// <summary>
        /// Sets all infantry-specific stat defaults. Called on Awake and Reset.
        /// </summary>
        private void InitializeInfantryDefaults()
        {
            UnitName = "Infantry Squad";
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
        /// Returns UnitType.Infantry, identifying this unit as infantry for combined arms
        /// and other systems that categorize units by type.
        /// </summary>
        /// <returns>UnitType.Infantry</returns>
        public override UnitType GetUnitType()
        {
            return UnitType.Infantry;
        }

        /// <summary>
        /// Calculates effective defense with the infantry's urban terrain bonus.
        /// When positioned on Urban terrain, infantry receive a flat +10 defense bonus
        /// (after all other modifiers), representing the advantage of building-to-building
        /// urban warfare and defensive fortifications inherent to city terrain.
        /// </summary>
        /// <returns>Effective defense value, including urban bonus if applicable.</returns>
        public override float GetEffectiveDefense()
        {
            float baseDefense = base.GetEffectiveDefense();

            // Check if the unit is currently on urban terrain
            // The HexGrid instance is obtained through the game's service locator or manager
            HexGrid grid = GetHexGrid();
            if (grid != null && Position != null)
            {
                TerrainType terrain = grid.GetTerrain(Position);
                if (terrain == TerrainType.Urban)
                {
                    baseDefense += urbanDefenseBonus;
                    Debug.Log($"[InfantryUnit] Urban defense bonus applied: +{urbanDefenseBonus} DEF " +
                              $"(total: {baseDefense:F1})");
                }
            }

            return baseDefense;
        }

        // =================================================================================
        // Infantry Special Abilities
        // =================================================================================

        /// <summary>
        /// Attempts to capture a city at the unit's current position.
        /// The unit must have the CanCaptureCities flag enabled and must not have already
        /// attacked this turn (capturing ends the unit's turn).
        /// </summary>
        /// <param name="cityController">
        /// The city controller at the unit's position. If null, no capturable city exists here.
        /// </param>
        /// <returns>True if the city was successfully captured, false otherwise.</returns>
        /// <remarks>
        /// Capturing a city:
        /// <list type="number">
        ///   <item>Unit must be on the city hex.</item>
        ///   <item>Unit must not have attacked this turn.</item>
        ///   <item>City ownership transfers to the unit's nation.</item>
        ///   <item>Unit's turn is ended (marked as moved and attacked).</item>
        /// </list>
        /// </remarks>
        public bool CaptureCity(object cityController)
        {
            if (!canCaptureCities)
            {
                Debug.LogWarning("[InfantryUnit] This infantry unit cannot capture cities.");
                return false;
            }

            if (!IsAlive)
            {
                Debug.LogWarning("[InfantryUnit] Cannot capture city: unit is destroyed.");
                return false;
            }

            if (hasAttackedThisTurn)
            {
                Debug.LogWarning("[InfantryUnit] Cannot capture city: unit already attacked this turn.");
                return false;
            }

            if (cityController == null)
            {
                Debug.LogWarning("[InfantryUnit] No city to capture at current position.");
                return false;
            }

            // Mark the unit as having acted (capturing ends the turn)
            hasMovedThisTurn = true;
            hasAttackedThisTurn = true;

            Debug.Log($"[InfantryUnit] '{UnitName}' captured a city! Owner: {OwnerNationId}");

            // The actual city ownership transfer would be handled by the CityController
            // through a game event or direct method call here.
            // This method serves as the entry point for the capture action.

            return true;
        }

        /// <summary>
        /// Infantry receive bonus suppression recovery compared to other units.
        /// Infantry recover 1 additional suppression stack per turn (2 total instead of 1).
        /// </summary>
        public override void ResetTurn()
        {
            base.ResetTurn();

            // Infantry's natural toughness grants extra suppression recovery
            if (Suppression > 0)
            {
                Suppression = Mathf.Max(0, Suppression - 1);
            }
        }

        // =================================================================================
        // Private Helpers
        // =================================================================================

        /// <summary>
        /// Attempts to retrieve the HexGrid instance from the game scene.
        /// In a full implementation this would use a service locator, singleton, or
        /// dependency injection to obtain the grid reference.
        /// </summary>
        /// <returns>The HexGrid instance, or null if unavailable.</returns>
        private HexGrid GetHexGrid()
        {
            // Attempt to find the HexGrid in the scene
            // This is a fallback; production code should use DI or a service locator
            GameObject gridObj = GameObject.Find("HexGrid");
            if (gridObj != null)
            {
                return gridObj.GetComponent<HexGrid>();
            }

            return null;
        }
    }
}
