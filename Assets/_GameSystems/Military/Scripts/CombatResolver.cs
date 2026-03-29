// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: CombatResolver.cs
// Description: MonoBehaviour that resolves combat between units. Orchestrates flanking,
//              combined arms, weather, terrain, suppression, and morale systems to produce
//              a deterministic combat result with slight randomness for engagement variance.
// =====================================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Weather conditions that modify combat effectiveness globally.
    /// Weather is determined by the game's weather system and passed into combat resolution.
    /// </summary>
    public enum WeatherType
    {
        /// <summary>No weather effects. Full combat effectiveness.</summary>
        Clear,

        /// <summary>Light rain. -15% accuracy for all ranged attacks.</summary>
        LightRain,

        /// <summary>Heavy rain. -30% accuracy for all ranged attacks, -10% movement.</summary>
        Rain,

        /// <summary>Fog of war. -20% attack for ranged units, limits vision.</summary>
        Fog,

        /// <summary>Snow. -15% movement, -10% attack for non-cold-adapted units.</summary>
        Snow,

        /// <summary>Storm. -40% air operations, -20% naval operations.</summary>
        Storm
    }

    /// <summary>
    /// Terrain types that provide defensive bonuses or penalties.
    /// Retrieved from the HexGrid at the defender's position.
    /// </summary>
    public enum TerrainType
    {
        Plains,
        Forest,
        Mountain,
        Desert,
        Swamp,
        Urban,
        Road,
        Water,
        DeepWater,
        Jungle,
        Hill,
        Fortification
    }

    /// <summary>
    /// Comprehensive result of a single combat engagement between two units.
    /// Contains damage values, destruction flags, modifiers applied, and a
    /// human-readable summary string for combat logs and UI display.
    /// </summary>
    public struct CombatResult
    {
        /// <summary>Damage dealt by the attacker to the defender.</summary>
        public float attackerDamage;

        /// <summary>Damage dealt by the defender to the attacker (counter-attack).</summary>
        public float defenderDamage;

        /// <summary>True if the defender was destroyed (HP reached 0).</summary>
        public bool attackerDestroyed;

        /// <summary>True if the attacker was destroyed by counter-attack.</summary>
        public bool defenderDestroyed;

        /// <summary>The flanking level determined for this attack.</summary>
        public FlankingLevel flanking;

        /// <summary>True if a combined arms bonus was active for the attacker.</summary>
        public bool combinedArmsBonus;

        /// <summary>
        /// Net morale change applied to the attacker (positive = gain, negative = loss).
        /// </summary>
        public float moraleChange;

        /// <summary>
        /// Human-readable summary of the combat engagement for logs and tooltips.
        /// </summary>
        public string summary;

        /// <summary>
        /// Returns the summary string, or a default message if empty.
        /// </summary>
        public override string ToString()
        {
            return string.IsNullOrEmpty(summary) ? "[CombatResult: No summary]" : summary;
        }
    }

    /// <summary>
    /// MonoBehaviour that orchestrates combat resolution between units.
    /// Typically attached to a GameManager or CombatManager object in the scene.
    /// Uses the FlankingSystem, CombinedArmsSystem, and terrain/weather data to
    /// produce balanced and tactically deep combat outcomes.
    /// </summary>
    public class CombatResolver : MonoBehaviour
    {
        // =================================================================================
        // Configuration
        // =================================================================================

        [Header("Combat Configuration")]
        [Tooltip("Minimum random multiplier applied to final damage (variance floor).")]
        [SerializeField] private float minDamageVariance = 0.8f;

        [Tooltip("Maximum random multiplier applied to final damage (variance ceiling).")]
        [SerializeField] private float maxDamageVariance = 1.2f;

        [Tooltip("Base counter-attack damage as a fraction of the defender's attack stat.")]
        [SerializeField] private float counterAttackFraction = 0.5f;

        [Tooltip("Radius for combined arms synergy checks around the attacker.")]
        [SerializeField] private int combinedArmsRadius = 2;

        [Tooltip("Morale loss threshold — defender loses extra morale when taking >10% max HP damage.")]
        [SerializeField] private float heavyHitMoraleThreshold = 0.1f;

        [Tooltip("Extra morale loss applied when a unit takes a heavy hit (percentage of max).")]
        [SerializeField] private float heavyHitMoraleLoss = 10f;

        [Tooltip("Morale gain for the attacker when dealing a heavy hit.")]
        [SerializeField] private float heavyHitMoraleGain = 5f;

        // =================================================================================
        // Combat Resolution
        // =================================================================================

        /// <summary>
        /// Resolves combat between an attacker and defender, accounting for flanking,
        /// combined arms, weather, terrain, suppression, encirclement, and morale.
        /// </summary>
        /// <param name="attacker">The attacking unit.</param>
        /// <param name="defender">The defending unit.</param>
        /// <param name="grid">The hex grid for position and terrain lookups.</param>
        /// <param name="allUnits">
        /// All units on the map. Used for combined arms evaluation and encirclement checks.
        /// </param>
        /// <param name="weather">Current weather conditions at the combat location.</param>
        /// <param name="terrain">Terrain type at the defender's position.</param>
        /// <returns>
        /// A CombatResult struct containing damage dealt, destruction flags, all applied
        /// modifiers, and a human-readable combat summary.
        /// </returns>
        /// <remarks>
        /// <para>Combat resolution steps:</para>
        /// <list type="number">
        ///   <item>Calculate base attack power from attacker stats.</item>
        ///   <item>Apply flanking bonus based on attacker angle to defender facing.</item>
        ///   <item>Apply combined arms bonus if friendly units are nearby.</item>
        ///   <item>Apply weather modifiers (rain, fog, storm, etc.).</item>
        ///   <item>Calculate defender effective defense.</item>
        ///   <item>Apply terrain defense bonus (forests, urban, fortifications, etc.).</item>
        ///   <item>Apply encirclement penalty if defender is surrounded.</item>
        ///   <item>Apply suppression penalty on defender's defense (-30% per stack at max).</item>
        ///   <item>Calculate final damage with randomness (0.8x-1.2x variance).</item>
        ///   <item>Apply morale effects based on damage dealt/received.</item>
        ///   <item>Generate human-readable summary string.</item>
        /// </list>
        /// </remarks>
        public CombatResult ResolveCombat(
            UnitBase attacker,
            UnitBase defender,
            HexGrid grid,
            List<UnitBase> allUnits,
            WeatherType weather,
            TerrainType terrain)
        {
            CombatResult result = new CombatResult
            {
                attackerDamage = 0f,
                defenderDamage = 0f,
                attackerDestroyed = false,
                defenderDestroyed = false,
                flanking = FlankingLevel.None,
                combinedArmsBonus = false,
                moraleChange = 0f,
                summary = string.Empty
            };

            // ---- Validation ----
            if (attacker == null || defender == null)
            {
                result.summary = "[Combat aborted: null unit reference]";
                Debug.LogError("[CombatResolver] ResolveCombat called with null unit.");
                return result;
            }

            if (!attacker.IsAlive || !defender.IsAlive)
            {
                result.summary = "[Combat aborted: one or both units are destroyed]";
                Debug.LogWarning("[CombatResolver] Attempted combat with destroyed unit.");
                return result;
            }

            StringBuilder summaryBuilder = new StringBuilder();
            summaryBuilder.Append($"COMBAT: {attacker.UnitName} → {defender.UnitName}\n");

            // Collect enemy positions for encirclement check
            List<HexCoord> attackerEnemyPositions = GetEnemyPositions(attacker.OwnerNationId, allUnits);
            List<HexCoord> defenderEnemyPositions = GetEnemyPositions(defender.OwnerNationId, allUnits);

            // =========================================================================
            // STEP 1: Base Attack Power
            // =========================================================================
            float attackPower = attacker.GetEffectiveAttack();
            summaryBuilder.Append($"  Base Attack: {attackPower:F1}\n");

            // =========================================================================
            // STEP 2: Flanking Bonus
            // =========================================================================
            FlankingLevel flankLevel = FlankingSystem.CalculateFlank(
                attacker.Position, defender.Position, defender.Facing);
            result.flanking = flankLevel;

            float flankMultiplier = FlankingSystem.GetFlankingAttackMultiplier(flankLevel);
            attackPower *= flankMultiplier;

            if (flankLevel != FlankingLevel.None)
            {
                summaryBuilder.Append($"  Flanking: {FlankingSystem.GetFlankingDescription(flankLevel)} " +
                                     $"(x{flankMultiplier:F2})\n");
            }

            // =========================================================================
            // STEP 3: Combined Arms Bonus
            // =========================================================================
            CombinedArmsResult combinedArms = CombinedArmsSystem.EvaluateCombinedArms(
                GetFriendlyUnits(attacker.OwnerNationId, allUnits),
                attacker.Position,
                combinedArmsRadius);

            result.combinedArmsBonus = combinedArms.hasSynergy;

            if (combinedArms.hasSynergy)
            {
                // Attack bonus stacks additively within the result
                attackPower *= (1f + combinedArms.attackBonus);

                if (combinedArms.attackBonus > 0f)
                {
                    summaryBuilder.Append($"  Combined Arms ATK: +{combinedArms.attackBonus:P0}\n");
                }
                if (combinedArms.defenseBonus > 0f)
                {
                    summaryBuilder.Append($"  Combined Arms DEF: +{combinedArms.defenseBonus:P0}\n");
                }

                foreach (string bonus in combinedArms.activeBonuses)
                {
                    summaryBuilder.Append($"    → {bonus}\n");
                }
            }

            // =========================================================================
            // STEP 4: Weather Modifiers
            // =========================================================================
            float weatherMultiplier = GetWeatherAttackModifier(weather, attacker.Type);
            attackPower *= weatherMultiplier;

            if (!Mathf.Approximately(weatherMultiplier, 1f))
            {
                summaryBuilder.Append($"  Weather ({weather}): x{weatherMultiplier:F2} ATK\n");
            }

            // =========================================================================
            // STEP 5: Defender Effective Defense
            // =========================================================================
            float defensePower = defender.GetEffectiveDefense();

            // Apply combined arms defense bonus to the defender as well
            CombinedArmsResult defenderCombinedArms = CombinedArmsSystem.EvaluateCombinedArms(
                GetFriendlyUnits(defender.OwnerNationId, allUnits),
                defender.Position,
                combinedArmsRadius);

            if (defenderCombinedArms.hasSynergy && defenderCombinedArms.defenseBonus > 0f)
            {
                defensePower *= (1f + defenderCombinedArms.defenseBonus);
                summaryBuilder.Append($"  Defender Combined Arms DEF: +{defenderCombinedArms.defenseBonus:P0}\n");
            }

            summaryBuilder.Append($"  Base Defense: {defensePower:F1}\n");

            // =========================================================================
            // STEP 6: Terrain Defense Bonus
            // =========================================================================
            float terrainDefenseBonus = GetTerrainDefenseBonus(terrain, defender.Type);
            defensePower *= (1f + terrainDefenseBonus);

            if (terrainDefenseBonus > 0f)
            {
                summaryBuilder.Append($"  Terrain ({terrain}) DEF: +{terrainDefenseBonus:P0}\n");
            }

            // =========================================================================
            // STEP 7: Encirclement Penalty
            // =========================================================================
            bool isEncircled = FlankingSystem.IsEncircled(
                defender.Position, defenderEnemyPositions, grid);

            if (isEncircled)
            {
                float encirclementPenalty = FlankingSystem.GetEncirclementDefensePenalty();
                defensePower *= encirclementPenalty;
                summaryBuilder.Append($"  Encirclement: x{encirclementPenalty:F2} DEF\n");
            }

            // =========================================================================
            // STEP 8: Suppression Penalty
            // =========================================================================
            float suppressionPenalty = GetSuppressionDefensePenalty(defender.Suppression);
            defensePower *= suppressionPenalty;

            if (!Mathf.Approximately(suppressionPenalty, 1f))
            {
                summaryBuilder.Append($"  Suppression ({defender.Suppression}/3): " +
                                     $"x{suppressionPenalty:F2} DEF\n");
            }

            // =========================================================================
            // STEP 9: Final Damage Calculation
            // =========================================================================
            // Damage formula: Attack^2 / (Attack + Defense)
            // This ensures damage scales well across different stat ranges
            float rawDamage = (attackPower * attackPower) / (attackPower + defensePower);

            // Apply randomness for engagement variance
            float varianceMultiplier = Random.Range(minDamageVariance, maxDamageVariance);
            float finalDamage = rawDamage * varianceMultiplier;

            // Apply damage to defender
            result.defenderDestroyed = false;
            float actualDamageDealt = defender.TakeDamage(finalDamage);
            result.attackerDamage = actualDamageDealt;

            summaryBuilder.Append($"  Raw Damage: {rawDamage:F1} x {varianceMultiplier:F2} = " +
                                 $"{finalDamage:F1}\n");
            summaryBuilder.Append($"  Damage Dealt: {actualDamageDealt:F1} " +
                                 $"(HP: {defender.Hp:F1}/{defender.MaxHp:F1})\n");

            // =========================================================================
            // STEP 9b: Counter-Attack
            // =========================================================================
            if (defender.IsAlive)
            {
                float counterAttackPower = defender.GetEffectiveAttack() * counterAttackFraction;
                float counterDamage = (counterAttackPower * counterAttackPower) /
                                      (counterAttackPower + attacker.GetEffectiveDefense());
                counterDamage *= Random.Range(minDamageVariance, maxDamageVariance);

                float counterDamageDealt = attacker.TakeDamage(counterDamage);
                result.defenderDamage = counterDamageDealt;
                result.attackerDestroyed = !attacker.IsAlive;

                summaryBuilder.Append($"  Counter-Attack: {counterDamageDealt:F1} " +
                                     $"(Atk HP: {attacker.Hp:F1}/{attacker.MaxHp:F1})\n");
            }

            result.defenderDestroyed = !defender.IsAlive;

            // =========================================================================
            // STEP 10: Morale Effects
            // =========================================================================
            result.moraleChange = CalculateMoraleChange(attacker, defender, actualDamageDealt);

            attacker.Morale = Mathf.Clamp(attacker.Morale + result.moraleChange, 0f, 100f);
            defender.Morale = Mathf.Clamp(defender.Morale - result.moraleChange, 0f, 100f);

            if (result.moraleChange > 0f)
            {
                summaryBuilder.Append($"  Morale: Attacker +{result.moraleChange:F0}, " +
                                     $"Defender -{result.moraleChange:F0}\n");
            }

            // =========================================================================
            // STEP 11: Generate Summary
            // =========================================================================
            if (result.defenderDestroyed)
            {
                summaryBuilder.Append($"  ★ {defender.UnitName} DESTROYED ★\n");
            }
            if (result.attackerDestroyed)
            {
                summaryBuilder.Append($"  ★ {attacker.UnitName} DESTROYED (counter-attack) ★\n");
            }

            summaryBuilder.Append("─────────────────────────────────");
            result.summary = summaryBuilder.ToString();

            Debug.Log(result.summary);

            return result;
        }

        // =================================================================================
        // Weather Modifiers
        // =================================================================================

        /// <summary>
        /// Returns the attack multiplier for a given weather condition and unit type.
        /// </summary>
        /// <param name="weather">The current weather condition.</param>
        /// <param name="unitType">The type of the unit (some units are weather-resistant).</param>
        /// <returns>A multiplier from 0.4 to 1.0 applied to attack power.</returns>
        private float GetWeatherAttackModifier(WeatherType weather, UnitType unitType)
        {
            switch (weather)
            {
                case WeatherType.Clear:
                    return 1.0f;

                case WeatherType.LightRain:
                    // Light rain: -15% for ranged, no effect on melee
                    return IsRangedUnit(unitType) ? 0.85f : 1.0f;

                case WeatherType.Rain:
                    // Heavy rain: -30% for ranged, -10% for melee
                    return IsRangedUnit(unitType) ? 0.70f : 0.90f;

                case WeatherType.Fog:
                    // Fog: -20% for ranged units
                    return IsRangedUnit(unitType) ? 0.80f : 1.0f;

                case WeatherType.Snow:
                    // Snow: -10% for ground units, no effect on vehicles/air
                    if (unitType == UnitType.Infantry || unitType == UnitType.SpecialForces)
                    {
                        return 0.85f;
                    }
                    return 1.0f;

                case WeatherType.Storm:
                    // Storm: -40% for air, -20% for naval
                    if (unitType == UnitType.Fighter || unitType == UnitType.Bomber || unitType == UnitType.Drone)
                    {
                        return 0.60f;
                    }
                    if (unitType == UnitType.Destroyer || unitType == UnitType.Submarine || unitType == UnitType.Carrier)
                    {
                        return 0.80f;
                    }
                    return 0.90f;

                default:
                    return 1.0f;
            }
        }

        // =================================================================================
        // Terrain Defense Bonuses
        // =================================================================================

        /// <summary>
        /// Returns the defense bonus multiplier for a given terrain type.
        /// Some unit types receive additional bonuses or penalties in specific terrain.
        /// </summary>
        /// <param name="terrain">The terrain at the defender's position.</param>
        /// <param name="defenderType">The unit type of the defender.</param>
        /// <returns>A defense bonus fraction (0.0 to 0.5+).</returns>
        private float GetTerrainDefenseBonus(TerrainType terrain, UnitType defenderType)
        {
            float bonus = 0f;

            switch (terrain)
            {
                case TerrainType.Plains:
                case TerrainType.Road:
                case TerrainType.Desert:
                    bonus = 0f;
                    break;

                case TerrainType.Forest:
                    bonus = 0.15f;
                    // Armor penalty in forests (handled separately in ArmorUnit, but base terrain still helps)
                    break;

                case TerrainType.Mountain:
                    bonus = 0.25f;
                    // High ground advantage
                    break;

                case TerrainType.Hill:
                    bonus = 0.15f;
                    break;

                case TerrainType.Urban:
                    bonus = 0.20f;
                    // Urban combat favors defenders
                    break;

                case TerrainType.Fortification:
                    bonus = 0.35f;
                    // Prepared defenses are very strong
                    break;

                case TerrainType.Swamp:
                case TerrainType.Jungle:
                    bonus = 0.10f;
                    break;

                case TerrainType.Water:
                case TerrainType.DeepWater:
                    bonus = 0f;
                    break;

                default:
                    bonus = 0f;
                    break;
            }

            return bonus;
        }

        // =================================================================================
        // Suppression Penalty
        // =================================================================================

        /// <summary>
        /// Returns the defense multiplier penalty from suppression stacks.
        /// At 3 stacks (fully suppressed), defense is reduced by 30%.
        /// </summary>
        /// <param name="suppressionStacks">Current suppression level (0-3).</param>
        /// <returns>A defense multiplier (1.0 at 0 suppression, 0.7 at max).</returns>
        private float GetSuppressionDefensePenalty(int suppressionStacks)
        {
            // Each stack of suppression reduces defense by 10%
            // At max suppression (3), total penalty is -30%
            return 1f - (suppressionStacks * 0.10f);
        }

        // =================================================================================
        // Morale Calculation
        // =================================================================================

        /// <summary>
        /// Calculates morale changes based on damage dealt and unit status.
        /// The attacker gains morale when dealing heavy hits; the defender loses morale.
        /// </summary>
        /// <param name="attacker">The attacking unit.</param>
        /// <param name="defender">The defending unit.</param>
        /// <param name="damageDealt">Actual damage dealt to the defender.</param>
        /// <returns>
        /// Net morale change (positive = attacker gains, applied as gain to attacker / loss to defender).
        /// </returns>
        private float CalculateMoraleChange(UnitBase attacker, UnitBase defender, float damageDealt)
        {
            float moraleShift = 0f;

            // Heavy hit threshold: if damage > 10% of defender's max HP
            if (damageDealt > defender.MaxHp * heavyHitMoraleThreshold)
            {
                moraleShift = heavyHitMoraleGain;
            }
            else
            {
                // Small morale shift from minor damage
                moraleShift = 2f;
            }

            // Flanking causes additional morale loss for defender
            FlankingLevel flank = FlankingSystem.CalculateFlank(
                attacker.Position, defender.Position, defender.Facing);
            if (flank == FlankingLevel.Rear)
            {
                moraleShift += 5f; // Rear attacks are demoralizing
            }
            else if (flank == FlankingLevel.Encircled)
            {
                moraleShift += 10f; // Being encircled is very demoralizing
            }

            // Defender destroyed = big morale shift
            if (!defender.IsAlive)
            {
                moraleShift = 15f;
            }

            return moraleShift;
        }

        // =================================================================================
        // Utility Helpers
        // =================================================================================

        /// <summary>
        /// Determines if a unit type relies on ranged attacks (affected by weather).
        /// </summary>
        private static bool IsRangedUnit(UnitType unitType)
        {
            return unitType == UnitType.Artillery ||
                   unitType == UnitType.Fighter ||
                   unitType == UnitType.Bomber ||
                   unitType == UnitType.Missile ||
                   unitType == UnitType.Drone;
        }

        /// <summary>
        /// Filters the full unit list to only units belonging to a specific nation.
        /// </summary>
        private static List<UnitBase> GetFriendlyUnits(string nationId, List<UnitBase> allUnits)
        {
            List<UnitBase> friendly = new List<UnitBase>();
            if (allUnits == null || string.IsNullOrEmpty(nationId))
                return friendly;

            foreach (UnitBase unit in allUnits)
            {
                if (unit != null && unit.IsAlive && unit.OwnerNationId == nationId)
                {
                    friendly.Add(unit);
                }
            }
            return friendly;
        }

        /// <summary>
        /// Collects positions of all units NOT belonging to the specified nation.
        /// </summary>
        private static List<HexCoord> GetEnemyPositions(string nationId, List<UnitBase> allUnits)
        {
            List<HexCoord> enemyPositions = new List<HexCoord>();
            if (allUnits == null || string.IsNullOrEmpty(nationId))
                return enemyPositions;

            foreach (UnitBase unit in allUnits)
            {
                if (unit != null && unit.IsAlive && unit.OwnerNationId != nationId)
                {
                    enemyPositions.Add(unit.Position);
                }
            }
            return enemyPositions;
        }
    }
}
