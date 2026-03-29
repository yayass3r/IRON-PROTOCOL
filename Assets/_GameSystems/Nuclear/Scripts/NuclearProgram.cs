// =====================================================================================
// Iron Protocol - Nuclear Weapons Program System
// =====================================================================================
// Complete nuclear weapons management including warhead production, delivery systems,
// launch mechanics, devastation modeling, radiation tracking, MAD deterrence, and
// global diplomatic consequences. Supports tactical through strategic nuclear warfare.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

using IronProtocol.HexMap;
using IronProtocol.Military;

namespace IronProtocol.Nuclear
{
    // ──────────────────────────────────────────────────────────────────────────────────
    //  Enumerations
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Defines the type of nuclear warhead, determining yield range, primary effect,
    /// and strategic classification.
    /// </summary>
    public enum WarheadType
    {
        /// <summary>Low-yield tactical warhead (10-50 kilotons). Limited area of effect.</summary>
        Tactical,

        /// <summary>High-yield strategic warhead (100-500 kilotons). Massive area devastation.</summary>
        Strategic,

        /// <summary>Multiple Independent Reentry Vehicle (~1000 kilotons total). Targets multiple hexes.</summary>
        MIRV,

        /// <summary>Enhanced radiation warhead (~5 kilotons). Kills personnel, preserves infrastructure.</summary>
        Neutron
    }

    /// <summary>
    /// Defines the delivery system used to transport and deploy a nuclear warhead
    /// to its target. Affects range, detectability, and interception probability.
    /// </summary>
    public enum DeliverySystem
    {
        /// <summary>Intercontinental Ballistic Missile. Very long range, high detectability.</summary>
        ICBM,

        /// <summary>Submarine-Launched Ballistic Missile. Long range, stealthy launch platform.</summary>
        SLBM,

        /// <summary>Cruise Missile. Medium range, low altitude, harder to detect.</summary>
        CruiseMissile,

        /// <summary>Strategic Bomber. Requires air superiority, can be recalled, flexible targeting.</summary>
        Bomber
    }

    /// <summary>
    /// Represents the status of a nuclear development project.
    /// </summary>
    public enum DevelopmentStatus
    {
        /// <summary>Nation has not begun nuclear development.</summary>
        NotStarted,

        /// <summary>Nation is actively developing nuclear capability.</summary>
        InProgress,

        /// <summary>Nation has achieved nuclear capability and can produce warheads.</summary>
        Complete
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Data Classes
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a nation's complete nuclear arsenal including warhead stockpiles,
    /// delivery systems, uranium reserves, and development status.
    /// </summary>
    [Serializable]
    public class NuclearArsenal
    {
        [Tooltip("Number of tactical warheads in stockpile.")]
        [SerializeField] private int tacticalWarheads;

        [Tooltip("Number of strategic warheads in stockpile.")]
        [SerializeField] private int strategicWarheads;

        [Tooltip("Number of MIRV warheads in stockpile.")]
        [SerializeField] private int mirvWarheads;

        [Tooltip("Number of neutron warheads in stockpile.")]
        [SerializeField] private int neutronWarheads;

        [Tooltip("Number of ICBM delivery systems available.")]
        [SerializeField] private int icbmCount;

        [Tooltip("Number of SLBM delivery systems available.")]
        [SerializeField] private int slbmCount;

        [Tooltip("Number of cruise missile delivery systems available.")]
        [SerializeField] private int cruiseMissileCount;

        [Tooltip("Number of strategic bomber delivery systems available.")]
        [SerializeField] private int bomberCount;

        [Tooltip("Whether this nation has achieved nuclear capability.")]
        [SerializeField] private bool hasNuclearCapability;

        [Tooltip("Current uranium stockpile available for warhead production.")]
        [SerializeField] private int uraniumStockpile;

        [Tooltip("Current status of the nuclear development project.")]
        [SerializeField] private DevelopmentStatus developmentStatus;

        /// <summary>Gets or sets the number of tactical warheads.</summary>
        public int TacticalWarheads { get => tacticalWarheads; set => tacticalWarheads = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of strategic warheads.</summary>
        public int StrategicWarheads { get => strategicWarheads; set => strategicWarheads = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of MIRV warheads.</summary>
        public int MirvWarheads { get => mirvWarheads; set => mirvWarheads = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of neutron warheads.</summary>
        public int NeutronWarheads { get => neutronWarheads; set => neutronWarheads = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of ICBMs.</summary>
        public int ICBMCount { get => icbmCount; set => icbmCount = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of SLBMs.</summary>
        public int SLBMCount { get => slbmCount; set => slbmCount = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of cruise missiles.</summary>
        public int CruiseMissileCount { get => cruiseMissileCount; set => cruiseMissileCount = Mathf.Max(0, value); }

        /// <summary>Gets or sets the number of bombers.</summary>
        public int BomberCount { get => bomberCount; set => bomberCount = Mathf.Max(0, value); }

        /// <summary>Gets or sets whether this nation has nuclear capability.</summary>
        public bool HasNuclearCapability { get => hasNuclearCapability; set => hasNuclearCapability = value; }

        /// <summary>Gets or sets the uranium stockpile.</summary>
        public int UraniumStockpile { get => uraniumStockpile; set => uraniumStockpile = Mathf.Max(0, value); }

        /// <summary>Gets or sets the nuclear development status.</summary>
        public DevelopmentStatus DevelopmentStatus { get => developmentStatus; set => developmentStatus = value; }

        /// <summary>Gets the total number of all warhead types combined.</summary>
        public int TotalWarheads => tacticalWarheads + strategicWarheads + mirvWarheads + neutronWarheads;

        /// <summary>Gets the total number of all delivery systems combined.</summary>
        public int TotalDeliverySystems => icbmCount + slbmCount + cruiseMissileCount + bomberCount;

        /// <summary>Gets the estimated total yield in kilotons across all warheads.</summary>
        public int EstimatedTotalYieldKilotons =>
            (tacticalWarheads * 30) + (strategicWarheads * 300) +
            (mirvWarheads * 1000) + (neutronWarheads * 5);

        /// <summary>Creates a deep copy of this arsenal.</summary>
        public NuclearArsenal Clone() => (NuclearArsenal)MemberwiseClone();
    }

    /// <summary>
    /// Represents a single nuclear weapon with its warhead type, delivery system,
    /// yield, range, accuracy, and readiness state.
    /// </summary>
    [Serializable]
    public class NuclearWeapon
    {
        [Tooltip("Unique identifier for this weapon instance.")]
        [SerializeField] private string weaponId;

        [Tooltip("The type of warhead installed on this weapon.")]
        [SerializeField] private WarheadType warhead;

        [Tooltip("The delivery system used to transport the warhead.")]
        [SerializeField] private DeliverySystem delivery;

        [Tooltip("Yield in kilotons of TNT equivalent.")]
        [SerializeField] private int yield;

        [Tooltip("Maximum range in hex cells.")]
        [SerializeField] private int range;

        [Tooltip("Accuracy percentage (0-100). Higher = more likely to hit target hex exactly.")]
        [SerializeField] private int accuracy;

        [Tooltip("Whether this weapon is armed and ready for launch.")]
        [SerializeField] private bool isReady;

        /// <summary>Gets or sets the weapon ID.</summary>
        public string WeaponId { get => weaponId; set => weaponId = value; }

        /// <summary>Gets or sets the warhead type.</summary>
        public WarheadType Warhead { get => warhead; set => warhead = value; }

        /// <summary>Gets or sets the delivery system type.</summary>
        public DeliverySystem Delivery { get => delivery; set => delivery = value; }

        /// <summary>Gets or sets the yield in kilotons.</summary>
        public int Yield { get => yield; set => yield = Mathf.Max(1, value); }

        /// <summary>Gets or sets the range in hex cells.</summary>
        public int Range { get => range; set => range = Mathf.Max(1, value); }

        /// <summary>Gets or sets the accuracy percentage (0-100).</summary>
        public int Accuracy { get => accuracy; set => accuracy = Mathf.Clamp(value, 0, 100); }

        /// <summary>Gets or sets whether the weapon is ready for launch.</summary>
        public bool IsReady { get => isReady; set => isReady = value; }

        /// <summary>
        /// Creates a pre-configured weapon based on warhead type with sensible defaults.
        /// </summary>
        /// <param name="weaponId">Unique ID for the weapon.</param>
        /// <param name="warheadType">Type of warhead to configure.</param>
        /// <param name="deliveryType">Type of delivery system.</param>
        /// <returns>A configured NuclearWeapon instance.</returns>
        public static NuclearWeapon CreateDefault(string weaponId, WarheadType warheadType, DeliverySystem deliveryType)
        {
            return warheadType switch
            {
                WarheadType.Tactical => new NuclearWeapon
                {
                    weaponId = weaponId, warhead = warheadType, delivery = deliveryType,
                    yield = UnityEngine.Random.Range(10, 51),
                    range = deliveryType == DeliverySystem.ICBM ? 12 : 4,
                    accuracy = 85, isReady = true
                },
                WarheadType.Strategic => new NuclearWeapon
                {
                    weaponId = weaponId, warhead = warheadType, delivery = deliveryType,
                    yield = UnityEngine.Random.Range(100, 501),
                    range = deliveryType == DeliverySystem.ICBM ? 20 : deliveryType == DeliverySystem.SLBM ? 15 : 8,
                    accuracy = 75, isReady = true
                },
                WarheadType.MIRV => new NuclearWeapon
                {
                    weaponId = weaponId, warhead = warheadType, delivery = deliveryType,
                    yield = 1000,
                    range = deliveryType == DeliverySystem.ICBM ? 25 : 18,
                    accuracy = 65, isReady = true
                },
                WarheadType.Neutron => new NuclearWeapon
                {
                    weaponId = weaponId, warhead = warheadType, delivery = deliveryType,
                    yield = 5,
                    range = deliveryType == DeliverySystem.ICBM ? 12 : 4,
                    accuracy = 90, isReady = true
                },
                _ => new NuclearWeapon
                {
                    weaponId = weaponId, warhead = warheadType, delivery = deliveryType,
                    yield = 50, range = 5, accuracy = 80, isReady = true
                }
            };
        }
    }

    /// <summary>
    /// Contains the complete results of a nuclear strike, including devastation metrics,
    /// casualties, after-effects, and radiation data.
    /// </summary>
    [Serializable]
    public class LaunchResult
    {
        /// <summary>Whether the launch was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Name of the target city (empty if no city at target).</summary>
        public string TargetCityName { get; set; } = string.Empty;

        /// <summary>Percentage of infrastructure devastated (0-100).</summary>
        public float DevastationPercent { get; set; }

        /// <summary>Radiation level at ground zero (0-100).</summary>
        public float RadiationLevel { get; set; }

        /// <summary>Estimated civilian casualties.</summary>
        public int CivilianCasualties { get; set; }

        /// <summary>Military units destroyed in the blast.</summary>
        public int MilitaryCasualties { get; set; }

        /// <summary>List of ongoing after-effects (radiation, fallout, EMP, etc.).</summary>
        public List<string> AfterEffects { get; set; } = new List<string>();

        /// <summary>The weapon used in the strike.</summary>
        public NuclearWeapon WeaponUsed { get; set; }

        /// <summary>Hex coordinates of the impact point.</summary>
        public HexCoord ImpactCoord { get; set; }

        /// <summary>Whether MAD was triggered by this strike.</summary>
        public bool MADTriggered { get; set; }

        /// <summary>Returns a formatted summary of the launch result for logging.</summary>
        public override string ToString()
        {
            return $"[LaunchResult] Success={Success}, Target={TargetCityName}, " +
                   $"Devastation={DevastationPercent:F1}%, Radiation={RadiationLevel:F1}, " +
                   $"CivilianCas={CivilianCasualties}, MilitaryCas={MilitaryCasualties}, MAD={MADTriggered}";
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Diplomacy Manager Stub
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal diplomacy interface required by the nuclear system.
    /// In production, this references the full DiplomacyManager from IronProtocol.Diplomacy.
    /// </summary>
    public class DiplomacyManager : MonoBehaviour
    {
        /// <summary>Adjusts opinion between two nations by the given delta.</summary>
        public virtual void AdjustOpinion(string nation1, string nation2, float delta) { }

        /// <summary>Gets the current opinion value between two nations (-100 to 100).</summary>
        public virtual float GetOpinion(string nation1, string nation2) => 0f;
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Nuclear Program - Main System
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Central management system for all nuclear weapons capabilities across all nations.
    /// Handles development, production, launch execution, devastation calculation,
    /// radiation tracking, and MAD deterrence logic.
    /// </summary>
    public class NuclearProgram : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────────
        //  Configuration
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Development Configuration")]
        [Tooltip("Cost in resources to begin nuclear development.")]
        [SerializeField] private int developmentCost = 500;

        [Tooltip("Number of turns required to develop nuclear capability.")]
        [SerializeField] private int developmentTurns = 5;

        [Header("Production Configuration")]
        [Tooltip("Uranium cost to produce a tactical warhead.")]
        [SerializeField] private int uraniumCostTactical = 10;

        [Tooltip("Uranium cost to produce a strategic warhead.")]
        [SerializeField] private int uraniumCostStrategic = 25;

        [Tooltip("Uranium cost to produce a MIRV warhead.")]
        [SerializeField] private int uraniumCostMIRV = 50;

        [Tooltip("Uranium cost to produce a neutron warhead.")]
        [SerializeField] private int uraniumCostNeutron = 15;

        [Header("Devastation Configuration")]
        [Tooltip("Blast radius in hex cells for tactical weapons.")]
        [SerializeField] private int tacticalBlastRadius = 1;

        [Tooltip("Blast radius in hex cells for strategic weapons.")]
        [SerializeField] private int strategicBlastRadius = 2;

        [Tooltip("Blast radius in hex cells for MIRV weapons.")]
        [SerializeField] private int mirvBlastRadius = 3;

        [Tooltip("Blast radius in hex cells for neutron weapons.")]
        [SerializeField] private int neutronBlastRadius = 1;

        [Tooltip("Radiation decay rate per turn (fraction of current level removed).")]
        [SerializeField] private float radiationDecayRate = 0.05f;

        [Tooltip("Number of turns radiation typically persists before decaying to negligible levels.")]
        [SerializeField] private int radiationDurationTurns = 20;

        [Header("Diplomatic Configuration")]
        [Tooltip("Global opinion penalty applied to the attacker when using nuclear weapons.")]
        [SerializeField] private float globalOpinionPenalty = -50f;

        [Tooltip("Additional opinion penalty for using strategic/MIRV weapons.")]
        [SerializeField] private float strategicWeaponOpinionPenalty = -30f;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Per-Nation State
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Per-nation nuclear arsenals.</summary>
        private Dictionary<string, NuclearArsenal> _nationArsenals;

        /// <summary>Per-nation nuclear development progress (turns completed).</summary>
        private Dictionary<string, int> _nationDevelopmentProgress;

        /// <summary>Per-nation radiation maps tracking fallout across the hex grid.</summary>
        private Dictionary<string, float> _radiationMap;

        /// <summary>Set of nations that have been nuclear-attacked (for MAD checks).</summary>
        private HashSet<string> _nukedNations;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when a nuclear strike is executed.
        /// Parameters: (attackerId, targetHexKey, launchResult).
        /// </summary>
        public event Action<string, string, LaunchResult> OnNuclearStrike;

        /// <summary>
        /// Fired when Mutually Assured Destruction is triggered.
        /// Parameters: (nationId that triggered MAD).
        /// </summary>
        public event Action<string> OnMADTriggered;

        /// <summary>
        /// Fired when a nation begins nuclear development.
        /// Parameters: (nationId).
        /// </summary>
        public event Action<string> OnNuclearDevelopmentStarted;

        /// <summary>
        /// Fired when a nation achieves nuclear capability.
        /// Parameters: (nationId).
        /// </summary>
        public event Action<string> OnNuclearCapabilityAchieved;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _nationArsenals = new Dictionary<string, NuclearArsenal>();
            _nationDevelopmentProgress = new Dictionary<string, int>();
            _radiationMap = new Dictionary<string, float>();
            _nukedNations = new HashSet<string>();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Nation Registration
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a nation for nuclear tracking.
        /// </summary>
        /// <param name="nationId">The nation to register.</param>
        public void RegisterNation(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return;

            if (!_nationArsenals.ContainsKey(nationId))
            {
                _nationArsenals[nationId] = new NuclearArsenal
                {
                    DevelopmentStatus = DevelopmentStatus.NotStarted
                };
            }

            if (!_nationDevelopmentProgress.ContainsKey(nationId))
                _nationDevelopmentProgress[nationId] = 0;
        }

        /// <summary>Gets the nuclear arsenal for a nation, or null if not registered.</summary>
        public NuclearArsenal GetArsenal(string nationId)
        {
            _nationArsenals.TryGetValue(nationId, out var arsenal);
            return arsenal;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Nuclear Development
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Begins the nuclear development program for a nation. This is a multi-turn project.
        /// The nation must not already have nuclear capability or be currently developing.
        /// </summary>
        /// <param name="nationId">The nation beginning development.</param>
        /// <returns><c>true</c> if development was successfully started.</returns>
        public bool DevelopNuclearCapability(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return false;

            var arsenal = GetOrCreateArsenal(nationId);
            if (arsenal.HasNuclearCapability)
            {
                Debug.LogWarning($"[NuclearProgram] Nation '{nationId}' already has nuclear capability.");
                return false;
            }

            if (arsenal.DevelopmentStatus == DevelopmentStatus.InProgress)
            {
                Debug.LogWarning($"[NuclearProgram] Nation '{nationId}' is already developing nuclear capability.");
                return false;
            }

            arsenal.DevelopmentStatus = DevelopmentStatus.InProgress;
            _nationDevelopmentProgress[nationId] = 0;

            Debug.Log($"[NuclearProgram] Nation '{nationId}' has begun nuclear development ({developmentTurns} turns).");
            OnNuclearDevelopmentStarted?.Invoke(nationId);
            return true;
        }

        /// <summary>
        /// Advances nuclear development for all nations currently in progress.
        /// Called once per game turn.
        /// </summary>
        public void AdvanceDevelopment()
        {
            var nations = new List<string>(_nationDevelopmentProgress.Keys);
            foreach (var nationId in nations)
            {
                var arsenal = GetArsenal(nationId);
                if (arsenal == null || arsenal.DevelopmentStatus != DevelopmentStatus.InProgress)
                    continue;

                _nationDevelopmentProgress[nationId]++;
                int progress = _nationDevelopmentProgress[nationId];

                Debug.Log($"[NuclearProgram] Nation '{nationId}' nuclear development: {progress}/{developmentTurns}");

                if (progress >= developmentTurns)
                {
                    arsenal.HasNuclearCapability = true;
                    arsenal.DevelopmentStatus = DevelopmentStatus.Complete;
                    arsenal.UraniumStockpile += 20; // Starting uranium grant

                    Debug.Log($"[NuclearProgram] *** Nation '{nationId}' has achieved NUCLEAR CAPABILITY! ***");
                    OnNuclearCapabilityAchieved?.Invoke(nationId);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Warhead Production
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a warhead of the specified type for a nation. Costs uranium.
        /// </summary>
        /// <param name="nationId">The nation producing the warhead.</param>
        /// <param name="type">The warhead type to produce.</param>
        /// <returns><c>true</c> if production succeeded.</returns>
        public bool ProduceWarhead(string nationId, WarheadType type)
        {
            if (string.IsNullOrEmpty(nationId)) return false;

            var arsenal = GetArsenal(nationId);
            if (arsenal == null || !arsenal.HasNuclearCapability)
            {
                Debug.LogWarning($"[NuclearProgram] Nation '{nationId}' lacks nuclear capability.");
                return false;
            }

            int cost = type switch
            {
                WarheadType.Tactical => uraniumCostTactical,
                WarheadType.Strategic => uraniumCostStrategic,
                WarheadType.MIRV => uraniumCostMIRV,
                WarheadType.Neutron => uraniumCostNeutron,
                _ => 10
            };

            if (arsenal.UraniumStockpile < cost)
            {
                Debug.LogWarning($"[NuclearProgram] Nation '{nationId}' has insufficient uranium ({arsenal.UraniumStockpile}/{cost}).");
                return false;
            }

            arsenal.UraniumStockpile -= cost;

            switch (type)
            {
                case WarheadType.Tactical: arsenal.TacticalWarheads++; break;
                case WarheadType.Strategic: arsenal.StrategicWarheads++; break;
                case WarheadType.MIRV: arsenal.MirvWarheads++; break;
                case WarheadType.Neutron: arsenal.NeutronWarheads++; break;
            }

            Debug.Log($"[NuclearProgram] Nation '{nationId}' produced {type} warhead. Uranium: {arsenal.UraniumStockpile}");
            return true;
        }

        /// <summary>Adds uranium to a nation's stockpile.</summary>
        public void AddUranium(string nationId, int amount)
        {
            if (string.IsNullOrEmpty(nationId) || amount <= 0) return;
            var arsenal = GetOrCreateArsenal(nationId);
            arsenal.UraniumStockpile += amount;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Nuclear Launch
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes a nuclear strike against a target hex coordinate. Calculates devastation,
        /// applies radiation, destroys units, triggers diplomatic consequences, and checks
        /// for MAD retaliation.
        /// </summary>
        /// <param name="attackerId">The attacking nation.</param>
        /// <param name="target">The hex coordinate to target.</param>
        /// <param name="weapon">The nuclear weapon to use.</param>
        /// <param name="grid">The hex grid (for neighbor calculations).</param>
        /// <param name="units">All units on the map (for destruction calculation).</param>
        /// <returns>A <see cref="LaunchResult"/> with full strike details.</returns>
        public LaunchResult LaunchNuclearStrike(
            string attackerId,
            HexCoord target,
            NuclearWeapon weapon,
            HexGrid grid,
            List<UnitBase> units)
        {
            var result = new LaunchResult
            {
                Success = true,
                WeaponUsed = weapon,
                ImpactCoord = target,
                AfterEffects = new List<string>()
            };

            if (weapon == null || !weapon.IsReady)
            {
                result.Success = false;
                result.AfterEffects.Add("Weapon not ready or invalid.");
                Debug.LogWarning("[NuclearProgram] Launch failed: weapon not ready.");
                return result;
            }

            var attackerArsenal = GetArsenal(attackerId);
            if (attackerArsenal == null)
            {
                result.Success = false;
                result.AfterEffects.Add("Attacker has no nuclear arsenal.");
                return result;
            }

            // Consume the warhead from the attacker's stockpile
            ConsumeWarhead(attackerArsenal, weapon.Warhead);

            // Calculate blast parameters
            int blastRadius = GetBlastRadius(weapon.Warhead);
            float radiationIntensity = CalculateRadiationIntensity(weapon);

            // Calculate and apply devastation
            result.DevastationPercent = CalculateDevastation(weapon, target, grid, units, blastRadius, result);

            // Apply radiation to the impact area
            ApplyRadiationToArea(target, radiationIntensity, blastRadius);
            result.RadiationLevel = radiationIntensity;
            result.AfterEffects.Add($"Radiation level {radiationLevel:F1} at ground zero.");

            // Track radiation at impact point
            string centerKey = target.ToString();
            _radiationMap.TryGetValue(centerKey, out float existingRad);
            _radiationMap[centerKey] = Mathf.Max(existingRad, radiationIntensity);

            // Check MAD condition
            string defenderId = GetNationAtHex(target, grid);
            if (!string.IsNullOrEmpty(defenderId) && defenderId != attackerId)
            {
                _nukedNations.Add(defenderId);
                bool madTriggered = CheckMAD(attackerId, defenderId);
                result.MADTriggered = madTriggered;

                if (madTriggered)
                {
                    result.AfterEffects.Add("MAD CONDITION TRIGGERED - Retaliatory launch imminent!");
                    OnMADTriggered?.Invoke(attackerId);
                }
            }

            // Apply global diplomatic consequences
            ApplyDiplomaticConsequences(attackerId, weapon);

            // Fire event
            OnNuclearStrike?.Invoke(attackerId, target.ToString(), result);

            Debug.Log($"[NuclearProgram] *** NUCLEAR STRIKE by '{attackerId}'! {result} ***");
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  MAD Deterrence
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether Mutually Assured Destruction should be triggered.
        /// MAD is triggered when a nuclear-armed nation with warheads attacks another
        /// nuclear-armed nation that also has warheads.
        /// </summary>
        /// <param name="attackerId">The attacking nation.</param>
        /// <param name="defenderId">The defending nation.</param>
        /// <returns><c>true</c> if MAD should be triggered.</returns>
        public bool CheckMAD(string attackerId, string defenderId)
        {
            if (string.IsNullOrEmpty(attackerId) || string.IsNullOrEmpty(defenderId))
                return false;

            if (attackerId == defenderId)
                return false;

            var attackerArsenal = GetArsenal(attackerId);
            var defenderArsenal = GetArsenal(defenderId);

            if (attackerArsenal == null || defenderArsenal == null)
                return false;

            // Both must have nuclear capability AND at least one warhead for MAD
            return attackerArsenal.HasNuclearCapability
                   && defenderArsenal.HasNuclearCapability
                   && defenderArsenal.TotalWarheads > 0;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Radiation Management
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the radiation level at a specific hex coordinate.
        /// </summary>
        /// <param name="coord">The hex coordinate to query.</param>
        /// <param name="radiationMap">Optional custom radiation map. If null, uses internal map.</param>
        /// <returns>Radiation level (0-100), or 0 if no radiation present.</returns>
        public float GetRadiationLevel(HexCoord coord, Dictionary<string, float> radiationMap = null)
        {
            var map = radiationMap ?? _radiationMap;
            map.TryGetValue(coord.ToString(), out float level);
            return level;
        }

        /// <summary>
        /// Updates radiation levels for all contaminated hexes, decaying by the configured rate.
        /// Called once per game turn.
        /// </summary>
        public void UpdateRadiation()
        {
            if (_radiationMap == null || _radiationMap.Count == 0) return;

            var keys = new List<string>(_radiationMap.Keys);
            int decayedCount = 0;

            foreach (var key in keys)
            {
                float current = _radiationMap[key];
                float decayed = current * (1f - radiationDecayRate);
                decayed = Mathf.Max(0f, decayed);

                if (decayed < 0.1f)
                {
                    _radiationMap.Remove(key);
                }
                else
                {
                    _radiationMap[key] = decayed;
                }

                decayedCount++;
            }

            if (decayedCount > 0)
                Debug.Log($"[NuclearProgram] Radiation updated for {decayedCount} hexes. {_radiationMap.Count} remain.");
        }

        /// <summary>Gets a read-only copy of the complete radiation map.</summary>
        public Dictionary<string, float> GetRadiationMap() => new Dictionary<string, float>(_radiationMap);

        // ──────────────────────────────────────────────────────────────────────────────
        //  Private - Blast & Devastation Calculations
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Gets blast radius in hex cells based on warhead type.</summary>
        private int GetBlastRadius(WarheadType warhead) => warhead switch
        {
            WarheadType.Tactical => tacticalBlastRadius,
            WarheadType.Strategic => strategicBlastRadius,
            WarheadType.MIRV => mirvBlastRadius,
            WarheadType.Neutron => neutronBlastRadius,
            _ => 1
        };

        /// <summary>Calculates ground zero radiation intensity from weapon parameters.</summary>
        private float CalculateRadiationIntensity(NuclearWeapon weapon)
        {
            float yieldFactor = Mathf.Log10(weapon.Yield + 1) * 10f;
            float typeMultiplier = weapon.Warhead == WarheadType.Neutron ? 0.5f : 1.0f;
            return Mathf.Clamp(yieldFactor * typeMultiplier, 5f, 100f);
        }

        /// <summary>
        /// Applies radiation to all hexes within the blast radius of the impact point,
        /// with distance-based falloff.
        /// </summary>
        private void ApplyRadiationToArea(HexCoord center, float intensity, int radius)
        {
            // Ground zero gets full intensity
            string centerKey = center.ToString();
            _radiationMap.TryGetValue(centerKey, out float existing);
            _radiationMap[centerKey] = Mathf.Max(existing, intensity);

            // Adjacent hexes get reduced radiation (60% falloff per ring)
            if (radius > 0)
            {
                var neighbors = GetNeighborCoords(center, radius);
                foreach (var (neighborCoord, distance) in neighbors)
                {
                    float falloff = 1f - ((float)distance / (radius + 1));
                    float radAtHex = intensity * falloff * 0.6f;

                    string nKey = neighborCoord.ToString();
                    _radiationMap.TryGetValue(nKey, out float nExisting);
                    _radiationMap[nKey] = Mathf.Max(nExisting, radAtHex);
                }
            }
        }

        /// <summary>
        /// Generates hex coordinates at various distances from center using cube coordinates.
        /// In production, replace with HexGrid.GetNeighbors() for accurate hex math.
        /// </summary>
        private static List<(HexCoord coord, int distance)> GetNeighborCoords(HexCoord center, int maxRadius)
        {
            var result = new List<(HexCoord, int)>();
            for (int q = -maxRadius; q <= maxRadius; q++)
            {
                for (int r = -maxRadius; r <= maxRadius; r++)
                {
                    for (int s = -maxRadius; s <= maxRadius; s++)
                    {
                        if (q + r + s != 0) continue;
                        int dist = Mathf.Max(Mathf.Abs(q), Mathf.Abs(r), Mathf.Abs(s));
                        if (dist == 0 || dist > maxRadius) continue;
                        result.Add((new HexCoord(center.Q + q, center.R + r), dist));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Calculates full devastation: infrastructure damage, unit destruction, civilian casualties.
        /// </summary>
        private float CalculateDevastation(
            NuclearWeapon weapon, HexCoord target, HexGrid grid,
            List<UnitBase> units, int blastRadius, LaunchResult result)
        {
            int militaryDestroyed = 0;

            // Infrastructure devastation based on yield and type
            float yieldDevastation = weapon.Warhead switch
            {
                WarheadType.Tactical => 30f + (weapon.Yield / 50f * 40f),      // 30-70%
                WarheadType.Strategic => 60f + (weapon.Yield / 500f * 35f),    // 60-95%
                WarheadType.MIRV => 95f,
                WarheadType.Neutron => 10f + (weapon.Yield / 5f * 20f),        // 10-30% (infrastructure)
                _ => 50f
            };

            float totalDevastation = Mathf.Clamp(yieldDevastation, 0f, 100f);

            // Destroy military units in blast radius
            if (units != null)
            {
                foreach (var unit in units)
                {
                    if (unit == null) continue;

                    float distance = HexDistance(target, unit.Position);
                    if (distance <= blastRadius)
                    {
                        float destructionChance = 1f - ((float)distance / (blastRadius + 1));
                        destructionChance = Mathf.Clamp(destructionChance * 1.2f, 0f, 1f);

                        // Neutron weapons are especially lethal to personnel
                        if (weapon.Warhead == WarheadType.Neutron)
                            destructionChance = Mathf.Min(1f, destructionChance + 0.3f);

                        if (UnityEngine.Random.value <= destructionChance)
                        {
                            militaryDestroyed++;
                            // In production: call unit.Destroy()
                        }
                    }
                }
            }

            result.MilitaryCasualties = militaryDestroyed;
            result.AfterEffects.Add($"{militaryDestroyed} military units destroyed.");

            // Civilian casualties estimate
            int civilianCasualties = EstimateCivilianCasualties(weapon, blastRadius);
            result.CivilianCasualties = civilianCasualties;
            result.AfterEffects.Add($"Estimated {civilianCasualties:N0} civilian casualties.");

            // Warhead-specific after-effects
            if (weapon.Warhead == WarheadType.Neutron)
                result.AfterEffects.Add("Neutron warhead: Enhanced radiation, preserved infrastructure.");
            if (weapon.Warhead == WarheadType.Strategic || weapon.Warhead == WarheadType.MIRV)
                result.AfterEffects.Add("EMP effect: Electronics disabled in extended radius.");

            return totalDevastation;
        }

        /// <summary>Estimates civilian casualties based on weapon yield and blast radius.</summary>
        private static int EstimateCivilianCasualties(NuclearWeapon weapon, int blastRadius)
        {
            int baseCasualties = weapon.Warhead switch
            {
                WarheadType.Tactical => weapon.Yield * 100,
                WarheadType.Strategic => weapon.Yield * 500,
                WarheadType.MIRV => weapon.Yield * 800,
                WarheadType.Neutron => weapon.Yield * 1000,
                _ => weapon.Yield * 200
            };
            return Mathf.RoundToInt(baseCasualties * blastRadius * 0.8f);
        }

        /// <summary>Applies diplomatic penalties to all nations after a nuclear strike.</summary>
        private void ApplyDiplomaticConsequences(string attackerId, NuclearWeapon weapon)
        {
            var diplomacy = FindObjectOfType<DiplomacyManager>();
            if (diplomacy == null)
            {
                Debug.LogWarning("[NuclearProgram] DiplomacyManager not found. Skipping diplomatic consequences.");
                return;
            }

            foreach (var nationId in _nationArsenals.Keys)
            {
                if (nationId == attackerId) continue;
                diplomacy.AdjustOpinion(attackerId, nationId, globalOpinionPenalty);
            }

            // Extra penalty for strategic/MIRV
            if (weapon.Warhead == WarheadType.Strategic || weapon.Warhead == WarheadType.MIRV)
            {
                foreach (var nationId in _nationArsenals.Keys)
                {
                    if (nationId == attackerId) continue;
                    diplomacy.AdjustOpinion(attackerId, nationId, strategicWeaponOpinionPenalty);
                }
            }

            Debug.Log($"[NuclearProgram] Diplomatic consequences applied for strike by '{attackerId}'.");
        }

        /// <summary>Removes one warhead of the specified type from the attacker's arsenal.</summary>
        private static void ConsumeWarhead(NuclearArsenal arsenal, WarheadType type)
        {
            switch (type)
            {
                case WarheadType.Tactical:   arsenal.TacticalWarheads   = Mathf.Max(0, arsenal.TacticalWarheads - 1);   break;
                case WarheadType.Strategic:  arsenal.StrategicWarheads  = Mathf.Max(0, arsenal.StrategicWarheads - 1);  break;
                case WarheadType.MIRV:       arsenal.MirvWarheads       = Mathf.Max(0, arsenal.MirvWarheads - 1);       break;
                case WarheadType.Neutron:    arsenal.NeutronWarheads    = Mathf.Max(0, arsenal.NeutronWarheads - 1);    break;
            }
        }

        /// <summary>Gets the owner nation of a hex (placeholder for HexGrid integration).</summary>
        private string GetNationAtHex(HexCoord coord, HexGrid grid)
        {
            // In production: grid.GetCellAt(coord)?.OwnerNationId
            return null;
        }

        /// <summary>Calculates cube-coordinate distance between two hex positions.</summary>
        private static float HexDistance(HexCoord a, HexCoord b)
        {
            return Mathf.Max(
                Mathf.Abs(a.Q - b.Q),
                Mathf.Abs(a.R - b.R),
                Mathf.Abs((a.Q + a.R) - (b.Q + b.R))
            );
        }

        /// <summary>Gets or creates an arsenal for a nation.</summary>
        private NuclearArsenal GetOrCreateArsenal(string nationId)
        {
            if (!_nationArsenals.TryGetValue(nationId, out var arsenal))
            {
                arsenal = new NuclearArsenal();
                _nationArsenals[nationId] = arsenal;
            }
            return arsenal;
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────────────────
//  HexMap Namespace - Forward Reference Types
//  NOTE: In production these would reside in IronProtocol.HexMap assembly.
// ──────────────────────────────────────────────────────────────────────────────────────

namespace IronProtocol.HexMap
{
    /// <summary>Represents a hex coordinate using the axial (q, r) system.</summary>
    [Serializable]
    public class HexCoord
    {
        /// <summary>Column coordinate (q-axis).</summary>
        public int Q;

        /// <summary>Row coordinate (r-axis).</summary>
        public int R;

        public HexCoord(int q, int r) { Q = q; R = r; }

        public override string ToString() => $"({Q},{R})";

        public override bool Equals(object obj) =>
            obj is HexCoord other && Q == other.Q && R == other.R;

        public override int GetHashCode() => HashCode.Combine(Q, R);
    }

    /// <summary>Represents the hex grid. Provides spatial queries and neighbor lookups.</summary>
    public class HexGrid : MonoBehaviour
    {
        /// <summary>Gets neighbor coordinates of a hex cell.</summary>
        public virtual HexCoord[] GetNeighbors(HexCoord coord) => Array.Empty<HexCoord>();
    }

    /// <summary>
    /// Represents a hex cell on the game map. Contains terrain, ownership, and building data.
    /// Used by ResearchManager for calculating research income from cities and universities.
    /// </summary>
    [Serializable]
    public class HexCell
    {
        /// <summary>Unique coordinate key of this hex cell.</summary>
        public string coordinateKey;

        /// <summary>The ID of the nation that owns this cell.</summary>
        public string ownerNationId;

        /// <summary>Whether this cell contains a city.</summary>
        public bool hasCity;

        /// <summary>Name of the city if present.</summary>
        public string cityName;

        /// <summary>Population of the city (or settlement) on this cell.</summary>
        public int population;

        /// <summary>Whether this cell has a university building.</summary>
        public bool hasUniversity;

        /// <summary>Level of the university (1-3), affecting research output.</summary>
        public int universityLevel;

        /// <summary>Whether this cell has a research lab.</summary>
        public bool hasResearchLab;
    }
}

// ──────────────────────────────────────────────────────────────────────────────────────
//  Military Namespace - Forward Reference Types
//  NOTE: In production these would reside in IronProtocol.Military assembly.
// ──────────────────────────────────────────────────────────────────────────────────────

namespace IronProtocol.Military
{
    /// <summary>Base class for all military units on the map.</summary>
    public class UnitBase : MonoBehaviour
    {
        /// <summary>Gets the unit's current hex grid position.</summary>
        public virtual IronProtocol.HexMap.HexCoord Position => new IronProtocol.HexMap.HexCoord(0, 0);

        /// <summary>Destroys this unit and removes it from the map.</summary>
        public virtual void DestroyUnit() { }
    }
}
