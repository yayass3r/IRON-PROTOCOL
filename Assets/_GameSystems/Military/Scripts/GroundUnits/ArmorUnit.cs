// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: ArmorUnit.cs
// Description: Concrete unit class for armored units (tanks, IFVs). Armor units excel
//              at breakthrough attacks with high damage output, but suffer penalties
//              in difficult terrain like forests and mountains.
// =====================================================================================

using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Armor unit — heavy ground combat vehicle (tanks, armored fighting vehicles).
    /// <para>
    /// Key characteristics:
    /// <list type="bullet">
    ///   <item>High attack power (55) — the strongest ground unit in direct combat.</item>
    ///   <item>Strong base defense (45) — heavy armor plating.</item>
    ///   <item>High hit points (150) — very survivable in open terrain.</item>
    ///   <item>Good movement (4 hexes per turn) — mobile strike capability.</item>
    ///   <item>Melee range only (1 hex) — must close distance to engage.</item>
    ///   <item>Special: Breakthrough damage — bonus damage against suppressed units.</item>
    ///   <item>Penalty: -20% defense in forests and mountains (terrain restricts maneuverability).</item>
    /// </list>
    /// </para>
    /// <para>
    /// Armor units synergize with Infantry (+15% defense via CombinedArmsSystem) and
    /// Artillery (+10% attack via prepared barrage). They are most effective in open
    /// terrain (plains, deserts, roads) where their mobility and armor shine.
    /// </para>
    /// </summary>
    [AddComponentMenu("Iron Protocol/Military/Ground Units/Armor Unit")]
    public class ArmorUnit : UnitBase
    {
        // =================================================================================
        // Constants
        // =================================================================================

        /// <summary>Default attack power for armor units.</summary>
        private const float DefaultAttack = 55f;

        /// <summary>Default defense value for armor units.</summary>
        private const float DefaultDefense = 45f;

        /// <summary>Default maximum hit points for armor units.</summary>
        private const float DefaultMaxHp = 150f;

        /// <summary>Default movement points per turn.</summary>
        private const float DefaultMovement = 4f;

        /// <summary>Default attack range in hexes (melee only).</summary>
        private const float DefaultRange = 1f;

        /// <summary>Defense penalty multiplier in difficult terrain (forests, mountains).</summary>
        private const float DifficultTerrainDefensePenalty = 0.80f; // -20% defense

        /// <summary>Bonus attack multiplier when attacking suppressed units (breakthrough).</summary>
        private const float BreakthroughSuppressedMultiplier = 1.30f; // +30% attack

        // =================================================================================
        // Serialized Configuration
        // =================================================================================

        [Header("Armor Configuration")]
        [Tooltip("Defense penalty in forests and mountains (-20%).")]
        [SerializeField] private float difficultTerrainPenalty = DifficultTerrainDefensePenalty;

        [Tooltip("Bonus damage against suppressed units (+30% attack).")]
        [SerializeField] private float breakthroughMultiplier = BreakthroughSuppressedMultiplier;

        /// <summary>
        /// Gets the difficult terrain defense penalty multiplier.
        /// </summary>
        public float DifficultTerrainPenalty => difficultTerrainPenalty;

        /// <summary>
        /// Gets the breakthrough attack multiplier against suppressed targets.
        /// </summary>
        public float BreakthroughMultiplier => breakthroughMultiplier;

        // =================================================================================
        // Unity Lifecycle
        // =================================================================================

        /// <summary>
        /// Unity Awake. Initializes base class and sets armor-specific defaults.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            InitializeArmorDefaults();
        }

        /// <summary>
        /// Unity Reset. Called when the component is first added in the inspector.
        /// Sets default values for all armor-specific fields.
        /// </summary>
        private void Reset()
        {
            InitializeArmorDefaults();
        }

        // =================================================================================
        // Initialization
        // =================================================================================

        /// <summary>
        /// Sets all armor-specific stat defaults. Called on Awake and Reset.
        /// </summary>
        private void InitializeArmorDefaults()
        {
            UnitName = "Armor Unit";
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
        /// Returns UnitType.Armor, identifying this unit as an armored vehicle
        /// for combined arms and terrain system categorization.
        /// </summary>
        /// <returns>UnitType.Armor</returns>
        public override UnitType GetUnitType()
        {
            return UnitType.Armor;
        }

        /// <summary>
        /// Calculates effective defense with the armor's terrain penalty.
        /// Armor units suffer a -20% defense penalty when in forests or mountains,
        /// as the difficult terrain restricts their maneuverability and negates
        /// their speed advantage.
        /// </summary>
        /// <returns>Effective defense value, with terrain penalty if applicable.</returns>
        public override float GetEffectiveDefense()
        {
            float baseDefense = base.GetEffectiveDefense();

            // Check for difficult terrain (forests, mountains)
            HexGrid grid = GetHexGrid();
            if (grid != null && Position != null)
            {
                TerrainType terrain = grid.GetTerrain(Position);
                if (IsDifficultTerrainForArmor(terrain))
                {
                    float prePenalty = baseDefense;
                    baseDefense *= difficultTerrainPenalty;
                    Debug.Log($"[ArmorUnit] Difficult terrain ({terrain}) penalty: " +
                              $"{prePenalty:F1} → {baseDefense:F1} DEF");
                }
            }

            return baseDefense;
        }

        /// <summary>
        /// Calculates effective attack with the armor's breakthrough bonus.
        /// When attacking a suppressed target, armor units deal +30% bonus damage,
        /// simulating their ability to exploit weakened enemy positions with rapid
        /// armored thrusts.
        /// </summary>
        /// <returns>Effective attack value, with breakthrough bonus if applicable.</returns>
        public override float GetEffectiveAttack()
        {
            float baseAttack = base.GetEffectiveAttack();

            // The breakthrough bonus would ideally check the target unit's suppression,
            // but since GetEffectiveAttack doesn't receive target info, we apply a
            // flat situational awareness bonus. The actual breakthrough multiplier is
            // applied in CombatResolver when targeting a suppressed unit.
            // This method preserves the base pattern for subclasses.

            return baseAttack;
        }

        // =================================================================================
        // Armor Special Abilities
        // =================================================================================

        /// <summary>
        /// Calculates the breakthrough attack bonus for attacking a suppressed target.
        /// Call this from the CombatResolver when the armor attacks a suppressed defender.
        /// </summary>
        /// <param name="targetSuppression">The suppression level of the target (0-3).</param>
        /// <returns>
        /// A multiplier to apply to the armor's attack. Returns 1.0 if target is not suppressed,
        /// or scales up to the full breakthrough multiplier based on suppression level.
        /// </returns>
        /// <remarks>
        /// Breakthrough scaling by suppression:
        /// <list type="bullet">
        ///   <item>0 suppression: 1.0x (no bonus)</item>
        ///   <item>1 suppression: 1.10x (+10%)</item>
        ///   <item>2 suppression: 1.20x (+20%)</item>
        ///   <item>3 suppression: 1.30x (+30%, full breakthrough)</item>
        /// </list>
        /// </remarks>
        public float GetBreakthroughAttackBonus(int targetSuppression)
        {
            if (targetSuppression <= 0)
            {
                return 1.0f;
            }

            // Scale breakthrough bonus linearly with suppression stacks
            float bonusPerStack = (breakthroughMultiplier - 1.0f) / 3f;
            float totalBonus = 1.0f + (targetSuppression * bonusPerStack);

            Debug.Log($"[ArmorUnit] Breakthrough bonus vs suppressed target " +
                      $"({targetSuppression}/3): x{totalBonus:F2}");

            return totalBonus;
        }

        /// <summary>
        /// Checks whether a given terrain type is considered difficult for armor units.
        /// Difficult terrain applies the -20% defense penalty.
        /// </summary>
        /// <param name="terrain">The terrain type to check.</param>
        /// <returns>True if the terrain is difficult for armor (forest, mountain, jungle).</returns>
        public static bool IsDifficultTerrainForArmor(TerrainType terrain)
        {
            return terrain == TerrainType.Forest ||
                   terrain == TerrainType.Mountain ||
                   terrain == TerrainType.Jungle ||
                   terrain == TerrainType.Swamp;
        }

        // =================================================================================
        // Private Helpers
        // =================================================================================

        /// <summary>
        /// Attempts to retrieve the HexGrid instance from the game scene.
        /// </summary>
        /// <returns>The HexGrid instance, or null if unavailable.</returns>
        private HexGrid GetHexGrid()
        {
            GameObject gridObj = GameObject.Find("HexGrid");
            if (gridObj != null)
            {
                return gridObj.GetComponent<HexGrid>();
            }
            return null;
        }
    }
}
