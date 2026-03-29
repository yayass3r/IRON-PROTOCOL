// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: UnitBase.cs
// Description: Abstract base class for all military units in the game.
//              Provides core unit fields, stats, turn management, and damage handling.
// =====================================================================================

using System;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Enumeration of all possible unit types in IRON PROTOCOL.
    /// </summary>
    public enum UnitType
    {
        Infantry,
        Armor,
        Artillery,
        Fighter,
        Bomber,
        Drone,
        Destroyer,
        Submarine,
        Carrier,
        Cyber,
        SpecialForces,
        Missile
    }

    /// <summary>
    /// Operational domain for a unit, determining which terrain layers it can traverse.
    /// </summary>
    public enum UnitDomain
    {
        Ground,
        Air,
        Naval,
        Special
    }

    /// <summary>
    /// Abstract base class for all military units.
    /// Inherit from this to create concrete unit implementations (InfantryUnit, ArmorUnit, etc.).
    /// Attach derived classes to GameObjects as MonoBehaviour components.
    /// </summary>
    public abstract class UnitBase : MonoBehaviour
    {
        // =================================================================================
        // Identity
        // =================================================================================

        /// <summary>
        /// Unique identifier for this unit instance, auto-generated as a GUID on Awake.
        /// </summary>
        [Header("Identity")]
        [SerializeField] private string unitId;

        /// <summary>
        /// The specific type classification of this unit (e.g., Infantry, Armor, Fighter).
        /// </summary>
        [SerializeField] private UnitType type;

        /// <summary>
        /// The operational domain of this unit (Ground, Air, Naval, Special).
        /// Determines which terrain layers the unit can traverse and which systems interact with it.
        /// </summary>
        [SerializeField] private UnitDomain domain;

        /// <summary>
        /// Human-readable display name for this unit (e.g., "M1 Abrams", "Rifle Squad").
        /// </summary>
        [SerializeField] private string unitName;

        /// <summary>
        /// The nation ID that owns and controls this unit.
        /// </summary>
        [SerializeField] private string ownerNationId;

        // =================================================================================
        // Position & Orientation
        // =================================================================================

        /// <summary>
        /// Current grid position on the hex map.
        /// </summary>
        [Header("Position & Orientation")]
        [SerializeField] private HexCoord position;

        /// <summary>
        /// Current facing direction on the hex grid (0-5, corresponding to six hex directions).
        /// 0 = East, 1 = Northeast, 2 = Northwest, 3 = West, 4 = Southwest, 5 = Southeast.
        /// </summary>
        [SerializeField] private int facing;

        // =================================================================================
        // Core Stats
        // =================================================================================

        /// <summary>
        /// Current hit points. When this reaches zero the unit is destroyed.
        /// </summary>
        [Header("Core Stats")]
        [SerializeField] private float hp = 100f;

        /// <summary>
        /// Maximum hit points. Determines the unit's survivability.
        /// </summary>
        [SerializeField] private float maxHp = 100f;

        /// <summary>
        /// Base attack power. Used as the foundation for all damage calculations.
        /// </summary>
        [SerializeField] private float attack = 30f;

        /// <summary>
        /// Base defense value. Reduces incoming damage.
        /// </summary>
        [SerializeField] private float defense = 20f;

        /// <summary>
        /// Maximum movement points available per turn.
        /// </summary>
        [SerializeField] private float movementPoints = 3f;

        /// <summary>
        /// Maximum attack range in hexes. A value of 1 means melee/adjacent only.
        /// </summary>
        [SerializeField] private float attackRange = 1f;

        // =================================================================================
        // Status & Morale
        // =================================================================================

        /// <summary>
        /// Current morale level (0-100). Low morale reduces combat effectiveness.
        /// At 0 morale the unit may rout or surrender.
        /// </summary>
        [Header("Status & Morale")]
        [SerializeField] private float morale = 100f;

        /// <summary>
        /// Current suppression stacks (0-3). Suppression reduces defense and limits actions.
        /// Applied by artillery and certain special abilities.
        /// </summary>
        [SerializeField] private int suppression;

        /// <summary>
        /// Current supply level (0-100). Low supply reduces movement and combat effectiveness.
        /// Units at 0 supply cannot attack or move.
        /// </summary>
        [SerializeField] private float supply = 100f;

        // =================================================================================
        // Turn State
        // =================================================================================

        /// <summary>
        /// Whether this unit has already moved during the current turn.
        /// </summary>
        [Header("Turn State")]
        public bool hasMovedThisTurn;

        /// <summary>
        /// Whether this unit has already attacked during the current turn.
        /// </summary>
        public bool hasAttackedThisTurn;

        // =================================================================================
        // Visuals
        // =================================================================================

        /// <summary>
        /// Icon sprite used in UI panels (selection, HUD, tooltips).
        /// </summary>
        [Header("Visuals")]
        [SerializeField] private Sprite unitIcon;

        /// <summary>
        /// The 3D model prefab instance for this unit on the game board.
        /// </summary>
        [SerializeField] private GameObject unitModel;

        // =================================================================================
        // Public Properties
        // =================================================================================

        /// <summary>Gets the unique GUID for this unit instance.</summary>
        public string UnitId => unitId;

        /// <summary>Gets or sets the unit type classification.</summary>
        public UnitType Type
        {
            get => type;
            protected set => type = value;
        }

        /// <summary>Gets or sets the operational domain.</summary>
        public UnitDomain Domain
        {
            get => domain;
            protected set => domain = value;
        }

        /// <summary>Gets or sets the display name.</summary>
        public string UnitName
        {
            get => unitName;
            set => unitName = value;
        }

        /// <summary>Gets or sets the owning nation ID.</summary>
        public string OwnerNationId
        {
            get => ownerNationId;
            set => ownerNationId = value;
        }

        /// <summary>Gets or sets the current hex grid position.</summary>
        public HexCoord Position
        {
            get => position;
            set => position = value;
        }

        /// <summary>
        /// Gets or sets the facing direction (0-5). Value is clamped to valid range.
        /// </summary>
        public int Facing
        {
            get => facing;
            set => facing = Mathf.Clamp(value, 0, 5);
        }

        /// <summary>Gets or sets current hit points, clamped to [0, maxHp].</summary>
        public float Hp
        {
            get => hp;
            set => hp = Mathf.Clamp(value, 0f, maxHp);
        }

        /// <summary>Gets or sets maximum hit points. Setting this also clamps current HP.</summary>
        public float MaxHp
        {
            get => maxHp;
            set
            {
                maxHp = Mathf.Max(1f, value);
                hp = Mathf.Clamp(hp, 0f, maxHp);
            }
        }

        /// <summary>Gets or sets base attack power.</summary>
        public float Attack
        {
            get => attack;
            protected set => attack = Mathf.Max(0f, value);
        }

        /// <summary>Gets or sets base defense value.</summary>
        public float Defense
        {
            get => defense;
            protected set => defense = Mathf.Max(0f, value);
        }

        /// <summary>Gets or sets maximum movement points per turn.</summary>
        public float MovementPoints
        {
            get => movementPoints;
            protected set => movementPoints = Mathf.Max(0f, value);
        }

        /// <summary>Gets or sets attack range in hexes.</summary>
        public float AttackRange
        {
            get => attackRange;
            protected set => attackRange = Mathf.Max(1f, value);
        }

        /// <summary>Gets or sets morale (0-100).</summary>
        public float Morale
        {
            get => morale;
            set => morale = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>Gets or sets suppression stacks (0-3).</summary>
        public int Suppression
        {
            get => suppression;
            set => suppression = Mathf.Clamp(value, 0, 3);
        }

        /// <summary>Gets or sets supply level (0-100).</summary>
        public float Supply
        {
            get => supply;
            set => supply = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>Gets or sets the UI icon sprite.</summary>
        public Sprite UnitIcon
        {
            get => unitIcon;
            set => unitIcon = value;
        }

        /// <summary>Gets or sets the 3D model GameObject.</summary>
        public GameObject UnitModel
        {
            get => unitModel;
            set => unitModel = value;
        }

        /// <summary>
        /// Returns true if the unit is currently alive (HP > 0).
        /// </summary>
        public bool IsAlive => hp > 0f;

        /// <summary>
        /// Returns true if the unit can perform actions (has supply and is alive).
        /// </summary>
        public bool CanAct => IsAlive && supply > 0f;

        /// <summary>
        /// Returns true if the unit can still move this turn.
        /// </summary>
        public bool CanMove => CanAct && !hasMovedThisTurn;

        /// <summary>
        /// Returns true if the unit can still attack this turn.
        /// </summary>
        public bool CanAttack => CanAct && !hasAttackedThisTurn;

        // =================================================================================
        // Unity Lifecycle
        // =================================================================================

        /// <summary>
        /// Unity Awake. Generates a unique GUID for this unit instance and validates required fields.
        /// </summary>
        protected virtual void Awake()
        {
            unitId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Unity Start. Logs unit initialization for debugging.
        /// </summary>
        protected virtual void Start()
        {
            Debug.Log($"[UnitBase] Initialized unit '{unitName}' (ID: {unitId}) " +
                      $"Type={type}, Domain={domain}, Owner={ownerNationId}");
        }

        // =================================================================================
        // Virtual Methods
        // =================================================================================

        /// <summary>
        /// Applies damage to this unit. Reduces HP and triggers death handling if destroyed.
        /// Override to add custom damage absorption, armor, or shield mechanics.
        /// </summary>
        /// <param name="damageAmount">Raw damage to apply before any mitigation.</param>
        /// <returns>The actual damage dealt after mitigation.</returns>
        public virtual float TakeDamage(float damageAmount)
        {
            if (damageAmount <= 0f)
                return 0f;

            float actualDamage = Mathf.Min(damageAmount, hp);
            hp -= actualDamage;
            hp = Mathf.Max(0f, hp);

            Debug.Log($"[UnitBase] '{unitName}' took {actualDamage:F1} damage. " +
                      $"HP: {hp:F1}/{maxHp:F1}");

            if (hp <= 0f)
            {
                OnUnitDestroyed();
            }

            return actualDamage;
        }

        /// <summary>
        /// Applies suppression stacks to this unit. Suppression reduces defense and combat effectiveness.
        /// Each stack reduces defense. At max suppression (3), the unit is pinned.
        /// </summary>
        /// <param name="stacks">Number of suppression stacks to apply.</param>
        public virtual void ApplySuppression(int stacks)
        {
            if (stacks <= 0)
                return;

            suppression = Mathf.Min(suppression + stacks, 3);
            Debug.Log($"[UnitBase] '{unitName}' received {stacks} suppression. " +
                      $"Total suppression: {suppression}/3");

            // Suppression impacts morale
            float moraleDrop = stacks * 5f;
            morale = Mathf.Max(0f, morale - moraleDrop);
        }

        /// <summary>
        /// Resets the unit's turn state, allowing it to move and attack again.
        /// Also naturally reduces suppression by 1 stack per turn.
        /// Override to add unit-specific turn reset logic.
        /// </summary>
        public virtual void ResetTurn()
        {
            hasMovedThisTurn = false;
            hasAttackedThisTurn = false;

            // Natural suppression recovery: lose 1 stack per turn
            if (suppression > 0)
            {
                suppression--;
            }

            // Natural morale recovery if supplied
            if (supply > 25f && morale < 100f)
            {
                morale = Mathf.Min(100f, morale + 2f);
            }
        }

        /// <summary>
        /// Calculates the effective attack power including morale, supply, and suppression modifiers.
        /// Override in derived classes to add type-specific attack modifiers.
        /// </summary>
        /// <returns>The effective attack value after all modifiers.</returns>
        public virtual float GetEffectiveAttack()
        {
            float effectiveAttack = attack;

            // Morale modifier: 50% at 0 morale, 100% at 50+ morale, up to 110% at 100 morale
            float moraleModifier = Mathf.Lerp(0.5f, 1.0f, Mathf.Clamp01(morale / 50f));
            if (morale >= 80f)
            {
                moraleModifier = Mathf.Lerp(1.0f, 1.1f, (morale - 80f) / 20f);
            }

            effectiveAttack *= moraleModifier;

            // Supply modifier: at 0 supply the unit cannot attack effectively
            if (supply <= 0f)
            {
                effectiveAttack *= 0.25f; // Desperation attacks only
            }
            else if (supply < 30f)
            {
                effectiveAttack *= 0.5f + (supply / 30f) * 0.5f; // Linear ramp from 50% to 100%
            }

            // Suppression penalty: each stack reduces attack by 10%
            effectiveAttack *= (1f - suppression * 0.1f);

            return effectiveAttack;
        }

        /// <summary>
        /// Calculates the effective defense including morale, supply, and suppression modifiers.
        /// Override in derived classes to add terrain-specific or type-specific defense bonuses.
        /// </summary>
        /// <returns>The effective defense value after all modifiers.</returns>
        public virtual float GetEffectiveDefense()
        {
            float effectiveDefense = defense;

            // Morale modifier for defense
            float moraleModifier = Mathf.Lerp(0.6f, 1.0f, Mathf.Clamp01(morale / 50f));
            effectiveDefense *= moraleModifier;

            // Supply modifier
            if (supply <= 0f)
            {
                effectiveDefense *= 0.5f;
            }
            else if (supply < 30f)
            {
                effectiveDefense *= 0.7f + (supply / 30f) * 0.3f;
            }

            // Suppression penalty: each stack reduces defense by 10%
            effectiveDefense *= (1f - suppression * 0.1f);

            return effectiveDefense;
        }

        // =================================================================================
        // Abstract Methods
        // =================================================================================

        /// <summary>
        /// Returns the specific unit type. Must be implemented by all derived classes.
        /// This is used by the CombinedArmsSystem and other systems that need type information.
        /// </summary>
        /// <returns>The UnitType enum value for this unit.</returns>
        public abstract UnitType GetUnitType();

        // =================================================================================
        // Protected Helpers
        // =================================================================================

        /// <summary>
        /// Called when the unit's HP reaches zero. Handles destruction logic.
        /// Override to add death effects, cleanup, or special destruction behavior.
        /// </summary>
        protected virtual void OnUnitDestroyed()
        {
            Debug.Log($"[UnitBase] '{unitName}' has been destroyed!");
            // Base implementation: disable the GameObject
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Heals the unit by the specified amount, up to max HP.
        /// </summary>
        /// <param name="healAmount">Amount of HP to restore.</param>
        /// <returns>The actual amount healed.</returns>
        public float Heal(float healAmount)
        {
            if (healAmount <= 0f || hp >= maxHp)
                return 0f;

            float actualHeal = Mathf.Min(healAmount, maxHp - hp);
            hp += actualHeal;
            return actualHeal;
        }

        /// <summary>
        /// Sets the unit's facing toward a target hex coordinate.
        /// </summary>
        /// <param name="targetPos">The hex to face toward.</param>
        public void FaceToward(HexCoord targetPos)
        {
            if (position == null || targetPos == null)
                return;

            int direction = position.GetDirectionTo(targetPos);
            Facing = direction;
        }
    }
}
