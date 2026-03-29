// =====================================================================================
// Iron Protocol - Mutually Assured Destruction Deterrent System
// =====================================================================================
// Manages the MAD doctrine, deterrence calculations, nuclear posture classification,
// and global tension tracking. Provides the strategic framework that discourages
// nuclear first-use by ensuring retaliation capability.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

using IronProtocol.HexMap;

namespace IronProtocol.Nuclear
{
    // ──────────────────────────────────────────────────────────────────────────────────
    //  Enumerations
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Defines the nuclear posture of a nation, indicating its level of nuclear
    /// readiness and strategic doctrine.
    /// </summary>
    public enum NuclearPosture
    {
        /// <summary>Nation has no nuclear capability or has disarmed.</summary>
        None,

        /// <summary>Nation possesses a minimal nuclear stockpile for basic deterrence.</summary>
        Minimal,

        /// <summary>Nation maintains a limited but credible nuclear arsenal.</summary>
        Limited,

        /// <summary>Nation has a substantial nuclear arsenal with diverse delivery options.</summary>
        Arsenal,

        /// <summary>Nation maintains a full nuclear triad (ICBM + SLBM + Bomber) for maximum deterrence.</summary>
        Triad
    }

    /// <summary>
    /// Defines the alert level of a nation's nuclear forces.
    /// </summary>
    public enum AlertLevel
    {
        /// <summary>Normal peacetime readiness. Warheads are stored, not mated to delivery systems.</summary>
        Normal,

        /// <summary>Elevated readiness. Increased surveillance and faster launch preparation.</summary>
        Elevated,

        /// <summary>High alert. Forces are mobilized, launch preparations underway.</summary>
        High,

        /// <summary>Maximum readiness. Launch-on-warning posture, fingers on triggers.</summary>
        DEFCON1
    }

    /// <summary>
    /// Defines the deterrence relationship between two nuclear-armed nations.
    /// </summary>
    public enum DeterrenceStatus
    {
        /// <summary>Both nations lack nuclear capability.</summary>
        NoDeterrence,

        /// <summary>Only one side has nuclear weapons. No mutual deterrence.</summary>
        Asymmetric,

        /// <summary>Both sides have nuclear weapons but one has clear superiority.</summary>
        Unstable,

        /// <summary>Both sides have comparable arsenals. Stable mutual deterrence.</summary>
        StableMAD,

        /// <summary>Both sides have overwhelming arsenals. No rational actor would initiate.</summary>
        OverkillMAD
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Data Classes
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Contains a detailed deterrence assessment between two nations.
    /// </summary>
    [Serializable]
    public class DeterrenceAssessment
    {
        /// <summary>The overall deterrence status between the two nations.</summary>
        public DeterrenceStatus Status;

        /// <summary>Nation 1's deterrence value (combined warhead and delivery score).</summary>
        public float Nation1DeterrenceValue;

        /// <summary>Nation 2's deterrence value (combined warhead and delivery score).</summary>
        public float Nation2DeterrenceValue;

        /// <summary>Balance ratio (0-1 where 0.5 = perfect parity).</summary>
        public float BalanceRatio;

        /// <summary>Whether MAD is currently active between these two nations.</summary>
        public bool IsMADActive;

        /// <summary>Human-readable assessment summary.</summary>
        public string Summary;

        /// <summary>Returns a formatted string for logging.</summary>
        public override string ToString() =>
            $"[Deterrence] Status={Status}, MAD={IsMADActive}, Balance={BalanceRatio:F2}, {Summary}";
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  MAD Deterrent System
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Manages the Mutually Assured Destruction framework for all nuclear-armed nations.
    /// Calculates deterrence values, classifies nuclear postures, and tracks global
    /// tension levels that influence AI decision-making and diplomatic interactions.
    /// </summary>
    public class MADDeterrent : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────────
        //  Configuration
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Deterrence Thresholds")]
        [Tooltip("Minimum total warheads required for 'Minimal' posture classification.")]
        [SerializeField] private int minimalPostureThreshold = 5;

        [Tooltip("Minimum total warheads required for 'Limited' posture classification.")]
        [SerializeField] private int limitedPostureThreshold = 20;

        [Tooltip("Minimum total warheads required for 'Arsenal' posture classification.")]
        [SerializeField] private int arsenalPostureThreshold = 100;

        [Tooltip("Minimum warheads AND full triad required for 'Triad' posture classification.")]
        [SerializeField] private int triadPostureThreshold = 200;

        [Header("Deterrence Scoring")]
        [Tooltip("Deterrence value per tactical warhead.")]
        [SerializeField] private float tacticalDeterrenceValue = 1.0f;

        [Tooltip("Deterrence value per strategic warhead.")]
        [SerializeField] private float strategicDeterrenceValue = 3.0f;

        [Tooltip("Deterrence value per MIRV warhead.")]
        [SerializeField] private float mirvDeterrenceValue = 5.0f;

        [Tooltip("Deterrence value per neutron warhead.")]
        [SerializeField] private float neutronDeterrenceValue = 2.0f;

        [Tooltip("Multiplier for having ICBM delivery capability.")]
        [SerializeField] private float icbmDeterrenceMultiplier = 1.2f;

        [Tooltip("Multiplier for having SLBM delivery capability (second-strike guarantee).")]
        [SerializeField] private float slbmDeterrenceMultiplier = 1.5f;

        [Tooltip("Multiplier for having bomber delivery capability (flexibility).")]
        [SerializeField] private float bomberDeterrenceMultiplier = 1.1f;

        [Header("MAD Configuration")]
        [Tooltip("Minimum combined deterrence for MAD to be considered active.")]
        [SerializeField] private float madActivationThreshold = 50f;

        [Tooltip("Balance ratio range (0-1) around 0.5 considered 'stable MAD'.")]
        [SerializeField] private float stableBalanceRange = 0.2f;

        [Header("Global Tension")]
        [Tooltip("Initial global tension value (0-100).")]
        [SerializeField] private float initialTension = 10f;

        [Tooltip("Tension increase per nuclear test or development.")]
        [SerializeField] private float developmentTensionImpact = 5f;

        [Tooltip("Tension increase per nuclear weapon produced.")]
        [SerializeField] private float productionTensionImpact = 3f;

        [Tooltip("Tension increase per nuclear strike launched.")]
        [SerializeField] private float strikeTensionImpact = 25f;

        [Tooltip("Tension increase when MAD is triggered.")]
        [SerializeField] private float madTensionImpact = 40f;

        [Tooltip("Natural tension decay per turn (simulates diplomatic cooling).")]
        [SerializeField] private float tensionDecayPerTurn = 1f;

        [Tooltip("Minimum tension floor (tension never drops below this).")]
        [SerializeField] private float minTension = 5f;

        // ──────────────────────────────────────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Global tension meter representing worldwide nuclear anxiety (0-100).
        /// Higher values increase AI aggression and reduce diplomatic cooperation.
        /// </summary>
        private float _globalTension;

        /// <summary>Per-nation alert levels.</summary>
        private Dictionary<string, AlertLevel> _nationAlertLevels;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the global tension level changes.
        /// Parameters: (newTensionLevel).
        /// </summary>
        public event Action<float> OnTensionChanged;

        /// <summary>
        /// Fired when a nation's nuclear posture changes.
        /// Parameters: (nationId, newPosture).
        /// </summary>
        public event Action<string, NuclearPosture> OnPostureChanged;

        /// <summary>
        /// Fired when a nation's alert level changes.
        /// Parameters: (nationId, newAlertLevel).
        /// </summary>
        public event Action<string, AlertLevel> OnAlertLevelChanged;

        /// <summary>
        /// Fired when the MAD relationship between two nations changes.
        /// Parameters: (nation1Id, nation2Id, isActive).
        /// </summary>
        public event Action<string, string, bool> OnMADStatusChanged;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _globalTension = initialTension;
            _nationAlertLevels = new Dictionary<string, AlertLevel>();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public Properties
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Gets the current global tension level (0-100).</summary>
        public float GlobalTension => _globalTension;

        /// <summary>Gets a human-readable description of the current tension level.</summary>
        public string TensionDescription => _globalTension switch
        {
            float t when t < 15f => "Peaceful",
            float t when t < 30f => "Uneasy",
            float t when t < 50f => "Tense",
            float t when t < 70f => "Crisis",
            float t when t < 90f => "Critical",
            _ => "Imminent Annihilation"
        };

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - MAD Detection
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determines whether Mutually Assured Destruction is currently active between
        /// two nations. MAD requires both nations to have nuclear capability with
        /// sufficient arsenals to guarantee unacceptable damage to the other.
        /// </summary>
        /// <param name="nation1">First nation's identifier.</param>
        /// <param name="nation2">Second nation's identifier.</param>
        /// <param name="np1">First nation's nuclear program.</param>
        /// <param name="np2">Second nation's nuclear program.</param>
        /// <returns><c>true</c> if MAD deterrence is active between the two nations.</returns>
        public bool IsMADActive(string nation1, string nation2, NuclearProgram np1, NuclearProgram np2)
        {
            if (np1 == null || np2 == null)
                return false;

            var arsenal1 = np1.GetArsenal(nation1);
            var arsenal2 = np2.GetArsenal(nation2);

            if (arsenal1 == null || arsenal2 == null)
                return false;

            // Both must have nuclear capability
            if (!arsenal1.HasNuclearCapability || !arsenal2.HasNuclearCapability)
                return false;

            // Both must have warheads to deploy
            if (arsenal1.TotalWarheads <= 0 || arsenal2.TotalWarheads <= 0)
                return false;

            // Calculate combined deterrence - both must meet minimum threshold
            float d1 = CalculateDeterrenceValue(nation1, np1);
            float d2 = CalculateDeterrenceValue(nation2, np2);

            return d1 >= madActivationThreshold && d2 >= madActivationThreshold;
        }

        /// <summary>
        /// Performs a full deterrence assessment between two nations, returning
        /// detailed information about the balance of power and MAD status.
        /// </summary>
        /// <param name="nation1">First nation's identifier.</param>
        /// <param name="nation2">Second nation's identifier.</param>
        /// <param name="np1">First nation's nuclear program.</param>
        /// <param name="np2">Second nation's nuclear program.</param>
        /// <returns>A comprehensive deterrence assessment.</returns>
        public DeterrenceAssessment AssessDeterrence(string nation1, string nation2, NuclearProgram np1, NuclearProgram np2)
        {
            var assessment = new DeterrenceAssessment();

            if (np1 == null || np2 == null)
            {
                assessment.Status = DeterrenceStatus.NoDeterrence;
                assessment.Summary = "One or both nuclear programs unavailable.";
                return assessment;
            }

            var a1 = np1.GetArsenal(nation1);
            var a2 = np2.GetArsenal(nation2);

            if (a1 == null || a2 == null)
            {
                assessment.Status = DeterrenceStatus.NoDeterrence;
                assessment.Summary = "One or both arsenals unavailable.";
                return assessment;
            }

            assessment.Nation1DeterrenceValue = CalculateDeterrenceValue(nation1, np1);
            assessment.Nation2DeterrenceValue = CalculateDeterrenceValue(nation2, np2);

            float totalDeterrence = assessment.Nation1DeterrenceValue + assessment.Nation2DeterrenceValue;
            assessment.BalanceRatio = totalDeterrence > 0
                ? assessment.Nation1DeterrenceValue / totalDeterrence
                : 0.5f;

            assessment.IsMADActive = IsMADActive(nation1, nation2, np1, np2);

            // Classify deterrence status
            if (!a1.HasNuclearCapability || !a2.HasNuclearCapability)
            {
                assessment.Status = a1.HasNuclearCapability || a2.HasNuclearCapability
                    ? DeterrenceStatus.Asymmetric
                    : DeterrenceStatus.NoDeterrence;
                assessment.Summary = assessment.Status == DeterrenceStatus.Asymmetric
                    ? "Asymmetric nuclear balance. One side has nuclear monopoly."
                    : "No nuclear capability on either side.";
            }
            else if (!assessment.IsMADActive)
            {
                assessment.Status = DeterrenceStatus.Unstable;
                assessment.Summary = "Unstable balance. One side lacks credible deterrence.";
            }
            else if (Math.Abs(assessment.BalanceRatio - 0.5f) <= stableBalanceRange)
            {
                assessment.Status = DeterrenceStatus.StableMAD;
                assessment.Summary = "Stable mutual deterrence. Both sides face unacceptable damage.";
            }
            else
            {
                assessment.Status = DeterrenceStatus.OverkillMAD;
                assessment.Summary = "Overkill MAD. Both arsenals vastly exceed deterrence requirements.";
            }

            return assessment;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Deterrence Calculation
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates the total deterrence value for a nation based on its arsenal composition
        /// and delivery system diversity. Higher values indicate greater strategic deterrent.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <param name="np">The nation's nuclear program.</param>
        /// <returns>Total deterrence value (arbitrary units, higher = stronger deterrent).</returns>
        public float CalculateDeterrenceValue(string nationId, NuclearProgram np)
        {
            if (np == null) return 0f;

            var arsenal = np.GetArsenal(nationId);
            if (arsenal == null || !arsenal.HasNuclearCapability)
                return 0f;

            // Base deterrence from warhead stockpile
            float warheadDeterrence =
                (arsenal.TacticalWarheads * tacticalDeterrenceValue) +
                (arsenal.StrategicWarheads * strategicDeterrenceValue) +
                (arsenal.MirvWarheads * mirvDeterrenceValue) +
                (arsenal.NeutronWarheads * neutronDeterrenceValue);

            // Delivery system multipliers (diversity increases deterrence)
            float deliveryMultiplier = 1.0f;
            if (arsenal.ICBMCount > 0) deliveryMultiplier *= icbmDeterrenceMultiplier;
            if (arsenal.SLBMCount > 0) deliveryMultiplier *= slbmDeterrenceMultiplier;
            if (arsenal.BomberCount > 0) deliveryMultiplier *= bomberDeterrenceMultiplier;

            // Second-strike capability bonus (SLBMs are nearly undetectable)
            float secondStrikeBonus = arsenal.SLBMCount > 0 ? warheadDeterrence * 0.25f : 0f;

            return (warheadDeterrence * deliveryMultiplier) + secondStrikeBonus;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Nuclear Posture
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Classifies a nation's nuclear posture based on its arsenal composition.
        /// </summary>
        /// <param name="nationId">The nation to classify.</param>
        /// <param name="np">The nation's nuclear program.</param>
        /// <returns>The nuclear posture classification.</returns>
        public NuclearPosture GetNuclearPosture(string nationId, NuclearProgram np)
        {
            if (np == null) return NuclearPosture.None;

            var arsenal = np.GetArsenal(nationId);
            if (arsenal == null || !arsenal.HasNuclearCapability)
                return NuclearPosture.None;

            int totalWarheads = arsenal.TotalWarheads;
            bool hasTriad = arsenal.ICBMCount > 0 && arsenal.SLBMCount > 0 && arsenal.BomberCount > 0;

            if (hasTriad && totalWarheads >= triadPostureThreshold)
                return NuclearPosture.Triad;

            if (totalWarheads >= arsenalPostureThreshold)
                return NuclearPosture.Arsenal;

            if (totalWarheads >= limitedPostureThreshold)
                return NuclearPosture.Limited;

            if (totalWarheads >= minimalPostureThreshold)
                return NuclearPosture.Minimal;

            return NuclearPosture.Minimal; // Has capability but very few warheads
        }

        /// <summary>
        /// Gets a human-readable string describing a nation's nuclear posture.
        /// </summary>
        /// <param name="nationId">The nation to describe.</param>
        /// <param name="np">The nation's nuclear program.</param>
        /// <returns>A descriptive posture string.</returns>
        public string GetNuclearPostureDescription(string nationId, NuclearProgram np)
        {
            var posture = GetNuclearPosture(nationId, np);
            var arsenal = np?.GetArsenal(nationId);

            return posture switch
            {
                NuclearPosture.None => "No nuclear capability",
                NuclearPosture.Minimal => $"Minimal deterrent ({arsenal?.TotalWarheads ?? 0} warheads)",
                NuclearPosture.Limited => $"Limited arsenal ({arsenal?.TotalWarheads ?? 0} warheads)",
                NuclearPosture.Arsenal => $"Major arsenal ({arsenal?.TotalWarheads ?? 0} warheads)",
                NuclearPosture.Triad => $"Full nuclear triad ({arsenal?.TotalWarheads ?? 0} warheads, ICBM+SLBM+Bomber)",
                _ => "Unknown posture"
            };
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Global Tension
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the global tension meter by adding an impact value.
        /// The tension is clamped between <see cref="minTension"/> and 100.
        /// </summary>
        /// <param name="nuclearEventImpact">The tension impact of the event (positive = more tension).</param>
        public void UpdateGlobalTension(float nuclearEventImpact)
        {
            _globalTension = Mathf.Clamp(_globalTension + nuclearEventImpact, minTension, 100f);

            Debug.Log($"[MADDeterrent] Global tension: {_globalTension:F1} ({TensionDescription})");
            OnTensionChanged?.Invoke(_globalTension);
        }

        /// <summary>
        /// Called each game turn to naturally decay global tension.
        /// Simulates diplomatic cooling and reduced public anxiety over time.
        /// </summary>
        public void DecayTension()
        {
            float oldTension = _globalTension;

            if (_globalTension > minTension)
            {
                _globalTension = Mathf.Max(minTension, _globalTension - tensionDecayPerTurn);
            }

            if (Math.Abs(oldTension - _globalTension) > 0.01f)
            {
                Debug.Log($"[MADDeterrent] Tension decayed: {oldTension:F1} -> {_globalTension:F1}");
                OnTensionChanged?.Invoke(_globalTension);
            }
        }

        /// <summary>
        /// Convenience method to record a nuclear development event and increase tension.
        /// </summary>
        /// <param name="nationId">The nation that began development.</param>
        public void OnDevelopmentStarted(string nationId)
        {
            UpdateGlobalTension(developmentTensionImpact);
            Debug.Log($"[MADDeterrent] Nation '{nationId}' began nuclear development. Tension +{developmentTensionImpact}.");
        }

        /// <summary>
        /// Convenience method to record a warhead production event and increase tension.
        /// </summary>
        /// <param name="nationId">The nation producing warheads.</param>
        /// <param name="count">Number of warheads produced.</param>
        public void OnWarheadsProduced(string nationId, int count)
        {
            float impact = productionTensionImpact * Mathf.Max(1, count);
            UpdateGlobalTension(impact);
            Debug.Log($"[MADDeterrent] Nation '{nationId}' produced {count} warhead(s). Tension +{impact:F0}.");
        }

        /// <summary>
        /// Convenience method to record a nuclear strike event and dramatically increase tension.
        /// </summary>
        /// <param name="attackerId">The attacking nation.</param>
        /// <param name="targetId">The target nation (if known).</param>
        /// <param name="madTriggered">Whether MAD was triggered by this strike.</param>
        public void OnStrikeLaunched(string attackerId, string targetId, bool madTriggered)
        {
            UpdateGlobalTension(strikeTensionImpact);

            if (madTriggered)
            {
                UpdateGlobalTension(madTensionImpact);
                Debug.Log($"[MADDeterrent] *** MAD TRIGGERED *** between '{attackerId}' and '{targetId}'! " +
                          $"Tension +{strikeTensionImpact + madTensionImpact}.");
            }
            else
            {
                Debug.Log($"[MADDeterrent] Nuclear strike by '{attackerId}' on '{targetId}'. Tension +{strikeTensionImpact}.");
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Alert Levels
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the current alert level for a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Current alert level, or Normal if not tracked.</returns>
        public AlertLevel GetAlertLevel(string nationId)
        {
            _nationAlertLevels.TryGetValue(nationId, out var level);
            return level;
        }

        /// <summary>
        /// Sets the alert level for a nation. Fires the alert change event if the level changes.
        /// </summary>
        /// <param name="nationId">The nation to update.</param>
        /// <param name="level">The new alert level.</param>
        public void SetAlertLevel(string nationId, AlertLevel level)
        {
            if (string.IsNullOrEmpty(nationId)) return;

            if (_nationAlertLevels.TryGetValue(nationId, out var oldLevel) && oldLevel == level)
                return;

            _nationAlertLevels[nationId] = level;
            Debug.Log($"[MADDeterrent] Nation '{nationId}' alert level: {oldLevel} -> {level}");
            OnAlertLevelChanged?.Invoke(nationId, level);
        }

        /// <summary>
        /// Automatically adjusts alert levels based on global tension.
        /// Higher tension pushes all nuclear nations to higher alert levels.
        /// </summary>
        /// <param name="np">The nuclear program for determining which nations to affect.</param>
        public void AutoAdjustAlertLevels(NuclearProgram np)
        {
            if (np == null) return;

            AlertLevel targetLevel = _globalTension switch
            {
                float t when t < 20f => AlertLevel.Normal,
                float t when t < 40f => AlertLevel.Elevated,
                float t when t < 65f => AlertLevel.High,
                _ => AlertLevel.DEFCON1
            };

            // Only raise alert levels, never auto-lower (manual de-escalation required)
            foreach (var kvp in _nationAlertLevels)
            {
                if (kvp.Value < targetLevel)
                {
                    _nationAlertLevels[kvp.Key] = targetLevel;
                    OnAlertLevelChanged?.Invoke(kvp.Key, targetLevel);
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Strategic Assessment
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determines whether a nuclear first-strike would be "rational" for an attacker.
        /// Returns false if MAD is active (attacker would face unacceptable retaliation).
        /// Used by AI decision-making systems.
        /// </summary>
        /// <param name="attackerId">The potential attacking nation.</param>
        /// <param name="defenderId">The potential target nation.</param>
        /// <param name="np">The nuclear program for both nations.</param>
        /// <returns><c>true</c> if a first strike might be rational (no MAD), <c>false</c> otherwise.</returns>
        public bool IsFirstStrikeRational(string attackerId, string defenderId, NuclearProgram np)
        {
            if (np == null) return false;

            // MAD makes first strike irrational
            if (IsMADActive(attackerId, defenderId, np, np))
                return false;

            var defenderArsenal = np.GetArsenal(defenderId);
            if (defenderArsenal == null) return true;

            // If defender has no nuclear capability, first strike has no nuclear retaliation risk
            if (!defenderArsenal.HasNuclearCapability || defenderArsenal.TotalWarheads == 0)
                return true;

            // If defender has some weapons but below MAD threshold, first strike is risky but possible
            return CalculateDeterrenceValue(defenderId, np) < madActivationThreshold;
        }

        /// <summary>
        /// Estimates the percentage of an attacker's arsenal needed to disarm the defender
        /// in a first strike (counterforce targeting).
        /// </summary>
        /// <param name="attackerId">The attacking nation.</param>
        /// <param name="defenderId">The target nation.</param>
        /// <param name="np">The nuclear program.</param>
        /// <returns>Percentage of attacker's arsenal needed (0-100), or -1 if impossible.</returns>
        public float EstimateCounterforceRequirement(string attackerId, string defenderId, NuclearProgram np)
        {
            if (np == null) return -1f;

            var attackerArsenal = np.GetArsenal(attackerId);
            var defenderArsenal = np.GetArsenal(defenderId);

            if (attackerArsenal == null || defenderArsenal == null) return -1f;
            if (attackerArsenal.TotalWarheads <= 0) return -1f;

            // Simplified: assume 2:1 attack ratio needed to destroy defender's arsenal
            // (accounting for attrition, misses, and hardened targets)
            float requiredStrikes = defenderArsenal.TotalWarheads * 2f;
            float attackerCapacity = attackerArsenal.TotalWarheads * CalculateDeterrenceValue(attackerId, np) / 10f;

            if (attackerCapacity <= 0) return -1f;

            return Mathf.Clamp((requiredStrikes / attackerCapacity) * 100f, 0f, 100f);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Reset
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Resets all state to initial values. Called when starting a new game.</summary>
        public void Reset()
        {
            _globalTension = initialTension;
            _nationAlertLevels.Clear();
            OnTensionChanged?.Invoke(_globalTension);
            Debug.Log("[MADDeterrent] System reset to initial state.");
        }
    }
}
