// =====================================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: CombinedArmsSystem.cs
// Description: Static system for evaluating combined arms synergies between friendly units.
//              Different unit categories provide stacking combat bonuses when operating
//              together within a specified radius, rewarding diverse army compositions.
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Military
{
    /// <summary>
    /// Broad unit categories used for combined arms synergy calculations.
    /// Multiple UnitType values can map to the same UnitCategory.
    /// </summary>
    public enum UnitCategory
    {
        /// <summary>Foot soldiers: Infantry, SpecialForces.</summary>
        Infantry,

        /// <summary>Vehicles: Armor.</summary>
        Armor,

        /// <summary>Indirect fire: Artillery, Missile.</summary>
        Artillery,

        /// <summary>Aircraft: Fighter, Bomber, Drone.</summary>
        Air,

        /// <summary>Ships: Destroyer, Submarine, Carrier.</summary>
        Naval,

        /// <summary>Special operations: Cyber.</summary>
        Special
    }

    /// <summary>
    /// Result of a combined arms evaluation, containing all active synergy bonuses.
    /// Bonuses are represented as multipliers that stack multiplicatively.
    /// </summary>
    public struct CombinedArmsResult
    {
        /// <summary>
        /// Cumulative attack bonus multiplier. Apply as: finalAttack *= (1.0 + attackBonus).
        /// For example, 0.20 = +20% attack bonus.
        /// </summary>
        public float attackBonus;

        /// <summary>
        /// Cumulative defense bonus multiplier. Apply as: finalDefense *= (1.0 + defenseBonus).
        /// For example, 0.15 = +15% defense bonus.
        /// </summary>
        public float defenseBonus;

        /// <summary>
        /// True if at least one combined arms synergy is active.
        /// </summary>
        public bool hasSynergy;

        /// <summary>
        /// Human-readable list of all active bonuses and their sources.
        /// Used for UI tooltips and combat summary logs.
        /// </summary>
        public List<string> activeBonuses;

        /// <summary>
        /// Returns a formatted string summarizing all combined arms effects.
        /// </summary>
        public override string ToString()
        {
            string bonusList = activeBonuses != null && activeBonuses.Count > 0
                ? string.Join(", ", activeBonuses)
                : "None";

            return $"[CombinedArms: ATK+{attackBonus:P0}, DEF+{defenseBonus:P0}, " +
                   $"Synergy={hasSynergy}, Bonuses=[{bonusList}]]";
        }
    }

    /// <summary>
    /// Static class that evaluates combined arms synergies between friendly units.
    /// Scans for unit categories within a radius of a center hex and applies
    /// stacking combat bonuses based on detected synergies.
    /// </summary>
    public static class CombinedArmsSystem
    {
        // =================================================================================
        // Synergy Definitions
        // =================================================================================

        // -- Bonus: Infantry + Armor = +15% defense for armor --
        private const float InfantryArmorDefenseBonus = 0.15f;
        private const string InfantryArmorName = "Infantry Screen (+15% DEF)";

        // -- Bonus: Infantry + Artillery = +20% attack for all (suppression support) --
        private const float InfantryArtilleryAttackBonus = 0.20f;
        private const string InfantryArtilleryName = "Fire Support (+20% ATK)";

        // -- Bonus: Air + Ground = +10% attack (air superiority) --
        private const float AirGroundAttackBonus = 0.10f;
        private const string AirGroundAttackName = "Air Superiority (+10% ATK)";

        // -- Bonus: AWACS + Air = +15% vision (handled externally, but tracked here) --
        private const float AwacsAirVisionBonus = 0.15f;
        private const string AwacsAirVisionName = "AWACS Coverage (+15% Vision)";

        // -- Bonus: Armor + Artillery = +10% attack for armor (softened targets) --
        private const float ArmorArtilleryAttackBonus = 0.10f;
        private const string ArmorArtilleryName = "Prepared Barrage (+10% ATK)";

        // -- Bonus: Naval + Air = +15% attack for naval (carrier strike) --
        private const float NavalAirAttackBonus = 0.15f;
        private const string NavalAirAttackName = "Carrier Strike (+15% ATK)";

        // =================================================================================
        // Main Evaluation
        // =================================================================================

        /// <summary>
        /// Evaluates all combined arms synergies among friendly units within the given radius
        /// of a center hex coordinate.
        /// </summary>
        /// <param name="friendlyUnits">
        /// List of all friendly units on the map. Only units within the radius are considered.
        /// </param>
        /// <param name="centerHex">
        /// The center hex for the radius check. Typically the position of the unit
        /// that will receive the combined arms bonuses.
        /// </param>
        /// <param name="radius">
        /// The hex radius within which friendly units contribute to synergies.
        /// A value of 2 means units within 2 hexes are considered.
        /// </param>
        /// <returns>
        /// A CombinedArmsResult containing all cumulative attack/defense bonuses,
        /// synergy status, and a list of active bonus descriptions.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Synergy rules (bonuses stack multiplicatively):
        /// <list type="bullet">
        ///   <item>Infantry + Armor present → +15% defense</item>
        ///   <item>Infantry + Artillery present → +20% attack (suppression support)</item>
        ///   <item>Air + Ground (Infantry/Armor) present → +10% attack (air superiority)</item>
        ///   <item>AWACS + Air present → +15% vision (informational, tracked in bonuses list)</item>
        ///   <item>Armor + Artillery present → +10% attack (prepared barrage)</item>
        ///   <item>Naval + Air present → +15% attack (carrier strike)</item>
        /// </list>
        /// </para>
        /// <para>
        /// Stacking example: If Infantry + Armor + Artillery are all present:
        /// Attack bonus = (1 + 0.20) * (1 + 0.10) - 1 = 32% total
        /// Defense bonus = 15%
        /// </para>
        /// </remarks>
        public static CombinedArmsResult EvaluateCombinedArms(
            List<UnitBase> friendlyUnits,
            HexCoord centerHex,
            int radius)
        {
            CombinedArmsResult result = new CombinedArmsResult
            {
                attackBonus = 0f,
                defenseBonus = 0f,
                hasSynergy = false,
                activeBonuses = new List<string>()
            };

            if (friendlyUnits == null || friendlyUnits.Count == 0)
            {
                return result;
            }

            if (centerHex == null || radius <= 0)
            {
                return result;
            }

            // Step 1: Identify which unit categories are present within radius
            HashSet<UnitCategory> categoriesInRange = new HashSet<UnitCategory>();
            bool hasAwacs = false;

            foreach (UnitBase unit in friendlyUnits)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                // Check if unit is within radius
                int distance = HexCoord.GetDistance(unit.Position, centerHex);
                if (distance > radius || distance < 0)
                {
                    continue;
                }

                UnitCategory category = GetUnitCategory(unit.GetUnitType());
                categoriesInRange.Add(category);

                // Check for AWACS (Drone type treated as AWACS for this synergy)
                if (unit.GetUnitType() == UnitType.Drone)
                {
                    hasAwacs = true;
                }
            }

            if (categoriesInRange.Count < 2)
            {
                // Need at least 2 different categories for any synergy
                return result;
            }

            // Step 2: Evaluate each synergy rule

            // --- Infantry + Armor → +15% defense ---
            if (categoriesInRange.Contains(UnitCategory.Infantry) &&
                categoriesInRange.Contains(UnitCategory.Armor))
            {
                result.defenseBonus += InfantryArmorDefenseBonus;
                result.activeBonuses.Add(InfantryArmorName);
            }

            // --- Infantry + Artillery → +20% attack ---
            if (categoriesInRange.Contains(UnitCategory.Infantry) &&
                categoriesInRange.Contains(UnitCategory.Artillery))
            {
                result.attackBonus += InfantryArtilleryAttackBonus;
                result.activeBonuses.Add(InfantryArtilleryName);
            }

            // --- Air + Ground (Infantry or Armor) → +10% attack ---
            bool hasGround = categoriesInRange.Contains(UnitCategory.Infantry) ||
                             categoriesInRange.Contains(UnitCategory.Armor);

            if (categoriesInRange.Contains(UnitCategory.Air) && hasGround)
            {
                result.attackBonus += AirGroundAttackBonus;
                result.activeBonuses.Add(AirGroundAttackName);
            }

            // --- AWACS (Drone) + Air → +15% vision ---
            if (hasAwacs && categoriesInRange.Contains(UnitCategory.Air))
            {
                // Vision bonus is informational; it's applied by the vision system
                // but we track it here for UI display
                result.activeBonuses.Add(AwacsAirVisionName);
            }

            // --- Armor + Artillery → +10% attack ---
            if (categoriesInRange.Contains(UnitCategory.Armor) &&
                categoriesInRange.Contains(UnitCategory.Artillery))
            {
                result.attackBonus += ArmorArtilleryAttackBonus;
                result.activeBonuses.Add(ArmorArtilleryName);
            }

            // --- Naval + Air → +15% attack ---
            if (categoriesInRange.Contains(UnitCategory.Naval) &&
                categoriesInRange.Contains(UnitCategory.Air))
            {
                result.attackBonus += NavalAirAttackBonus;
                result.activeBonuses.Add(NavalAirAttackName);
            }

            // Step 3: Set synergy flag
            result.hasSynergy = result.attackBonus > 0f || result.defenseBonus > 0f ||
                                result.activeBonuses.Count > 0;

            return result;
        }

        // =================================================================================
        // Utility: Type to Category Mapping
        // =================================================================================

        /// <summary>
        /// Maps a specific UnitType to its broad UnitCategory for combined arms calculations.
        /// </summary>
        /// <param name="unitType">The specific unit type.</param>
        /// <returns>The corresponding broad category.</returns>
        public static UnitCategory GetUnitCategory(UnitType unitType)
        {
            switch (unitType)
            {
                case UnitType.Infantry:
                case UnitType.SpecialForces:
                    return UnitCategory.Infantry;

                case UnitType.Armor:
                    return UnitCategory.Armor;

                case UnitType.Artillery:
                case UnitType.Missile:
                    return UnitCategory.Artillery;

                case UnitType.Fighter:
                case UnitType.Bomber:
                case UnitType.Drone:
                    return UnitCategory.Air;

                case UnitType.Destroyer:
                case UnitType.Submarine:
                case UnitType.Carrier:
                    return UnitCategory.Naval;

                case UnitType.Cyber:
                    return UnitCategory.Special;

                default:
                    Debug.LogWarning($"[CombinedArmsSystem] Unhandled UnitType: {unitType}. " +
                                     "Defaulting to Infantry.");
                    return UnitCategory.Infantry;
            }
        }

        // =================================================================================
        // Utility: Synergy Preview
        // =================================================================================

        /// <summary>
        /// Returns all possible synergy descriptions for UI previews (e.g., in an
        /// army composition tooltip showing "Potential synergies").
        /// </summary>
        /// <returns>A list of all defined synergy descriptions.</returns>
        public static List<string> GetAllSynergyDescriptions()
        {
            return new List<string>
            {
                InfantryArmorName,
                InfantryArtilleryName,
                AirGroundAttackName,
                AwacsAirVisionName,
                ArmorArtilleryName,
                NavalAirAttackName
            };
        }

        /// <summary>
        /// Checks if a specific synergy is possible given the current set of categories.
        /// Useful for AI and UI to determine what unit types to build next.
        /// </summary>
        /// <param name="currentCategories">The categories currently present.</param>
        /// <param name="missingCategory">Output: the category needed to activate a new synergy.</param>
        /// <returns>True if adding a new category would unlock at least one new synergy.</returns>
        public static bool HasPotentialNewSynergy(
            HashSet<UnitCategory> currentCategories,
            out UnitCategory missingCategory)
        {
            missingCategory = UnitCategory.Infantry;

            // Check each synergy rule: if one category is present and the other is missing,
            // recommend the missing one
            if (currentCategories.Contains(UnitCategory.Infantry) &&
                !currentCategories.Contains(UnitCategory.Armor))
            {
                missingCategory = UnitCategory.Armor;
                return true;
            }

            if (currentCategories.Contains(UnitCategory.Infantry) &&
                !currentCategories.Contains(UnitCategory.Artillery))
            {
                missingCategory = UnitCategory.Artillery;
                return true;
            }

            if (currentCategories.Contains(UnitCategory.Armor) &&
                !currentCategories.Contains(UnitCategory.Infantry))
            {
                missingCategory = UnitCategory.Infantry;
                return true;
            }

            if (currentCategories.Contains(UnitCategory.Artillery) &&
                !currentCategories.Contains(UnitCategory.Infantry))
            {
                missingCategory = UnitCategory.Infantry;
                return true;
            }

            bool hasGround = currentCategories.Contains(UnitCategory.Infantry) ||
                             currentCategories.Contains(UnitCategory.Armor);

            if (hasGround && !currentCategories.Contains(UnitCategory.Air))
            {
                missingCategory = UnitCategory.Air;
                return true;
            }

            if (currentCategories.Contains(UnitCategory.Air) && !hasGround)
            {
                missingCategory = UnitCategory.Infantry;
                return true;
            }

            if (currentCategories.Contains(UnitCategory.Naval) &&
                !currentCategories.Contains(UnitCategory.Air))
            {
                missingCategory = UnitCategory.Air;
                return true;
            }

            return false;
        }
    }
}
