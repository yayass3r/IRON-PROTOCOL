// =====================================================================
// IRON PROTOCOL - Proxy War Manager
// Covert proxy wars, rebel funding, regime change, and deniable operations.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.GameSystems.ProxyWars
{
    // =================================================================
    // ENUMERATIONS
    // =================================================================

    /// <summary>
    /// Types of covert actions a sponsor nation can take in a proxy war.
    /// Each action has different costs, risks, and effects on rebel/government strength.
    /// </summary>
    public enum ProxyAction
    {
        /// <summary>Send money to rebel groups. +10 rebel strength, costs treasury per turn.</summary>
        FundRebels,
        /// <summary>Provide weapons and ammunition. +15 rebel strength, +5 effectiveness.</summary>
        ArmRebels,
        /// <summary>Train rebel fighters in guerrilla tactics. +20 rebel combat power, takes 3 turns.</summary>
        TrainMilitia,
        /// <summary>Deploy military advisors to assist rebels. +10 effectiveness, risk of capture (CB).</summary>
        DeployAdvisors,
        /// <summary>Establish a protected no-conflict zone. +5 stability in region.</summary>
        EstablishSafeZone,
        /// <summary>Covertly influence government officials toward the sponsor's agenda.</summary>
        CovertInfluence,
        /// <summary>Frame another nation for an attack to create a casus belli.</summary>
        FalseFlagOp,
        /// <summary>Attempt full government overthrow. Requires rebel strength > 70.</summary>
        RegimeChange
    }

    // =================================================================
    // DATA CLASSES
    // =================================================================

    /// <summary>
    /// Simple hex coordinate for specifying proxy war locations on the game map.
    /// </summary>
    [Serializable]
    public struct HexCoord
    {
        /// <summary>Hex column (Q axis).</summary>
        public int q;
        /// <summary>Hex row (R axis).</summary>
        public int r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public override string ToString() => $"({q}, {r})";

        public static HexCoord Zero => new HexCoord(0, 0);
    }

    /// <summary>
    /// Represents an active proxy war between a sponsor and target nation.
    /// Tracks rebel/government strength, active actions, funding, and outcome state.
    /// </summary>
    [Serializable]
    public class ProxyWar
    {
        /// <summary>Unique proxy war identifier.</summary>
        public string proxyWarId;

        /// <summary>Nation funding and supporting the rebels.</summary>
        public string sponsorNation;

        /// <summary>Nation where the proxy war is being fought.</summary>
        public string targetNation;

        /// <summary>Center hex coordinate of the conflict region.</summary>
        public HexCoord centerHex;

        /// <summary>Radius in hexes of the affected area.</summary>
        public int radius;

        /// <summary>List of currently active covert actions being undertaken.</summary>
        public List<ProxyAction> activeActions = new List<ProxyAction>();

        /// <summary>Rebel faction strength from 0 to 100.</summary>
        [Range(0f, 100f)] public float rebelStrength;

        /// <summary>Target government strength from 0 to 100.</summary>
        [Range(0f, 100f)] public float governmentStrength;

        /// <summary>Number of turns the proxy war has been active.</summary>
        public int turnsActive;

        /// <summary>Treasury cost per turn for the sponsor to maintain operations.</summary>
        public float fundingPerTurn;

        /// <summary>Whether the proxy war is currently ongoing.</summary>
        public bool isActive;

        /// <summary>Whether the sponsor achieved their objectives.</summary>
        public bool succeeded;

        /// <summary>Whether the operation completely failed.</summary>
        public bool failed;

        /// <summary>Human-readable outcome description (set when war ends).</summary>
        public string outcome;

        /// <summary>Whether the proxy war has been exposed to the international community.</summary>
        public bool isExposed;

        /// <summary>Accumulated exposure risk (0-100); higher = more likely to be discovered.</summary>
        [Range(0f, 100f)] public float exposureLevel;

        /// <summary>Number of turns advisors have been deployed (for capture risk).</summary>
        public int advisorTurnsDeployed;

        /// <summary>Turns remaining until militia training completes (0 = not training).</summary>
        public int militiaTrainingTurnsRemaining;

        /// <summary>Effectiveness multiplier for rebel forces (improved by advisors/training).</summary>
        public float rebelEffectiveness;

        /// <summary>Nation that was framed by a FalseFlagOp (if any).</summary>
        public string framedNation;

        /// <summary>
        /// Creates a new proxy war with default values.
        /// </summary>
        public ProxyWar(
            string proxyWarId, string sponsorNation, string targetNation,
            HexCoord centerHex, int radius = 3)
        {
            this.proxyWarId = proxyWarId;
            this.sponsorNation = sponsorNation;
            this.targetNation = targetNation;
            this.centerHex = centerHex;
            this.radius = Mathf.Max(1, radius);
            this.rebelStrength = 10f;
            this.governmentStrength = 80f;
            this.turnsActive = 0;
            this.fundingPerTurn = 100f;
            this.isActive = true;
            this.succeeded = false;
            this.failed = false;
            this.outcome = string.Empty;
            this.isExposed = false;
            this.exposureLevel = 0f;
            this.advisorTurnsDeployed = 0;
            this.militiaTrainingTurnsRemaining = 0;
            this.rebelEffectiveness = 1f;
            this.framedNation = string.Empty;
        }
    }

    /// <summary>
    /// Contains the result of executing a proxy action, including success/failure,
    /// effectiveness, cost, and a description of what happened.
    /// </summary>
    [Serializable]
    public class ProxyActionResult
    {
        /// <summary>The action that was executed.</summary>
        public ProxyAction action;

        /// <summary>Whether the action succeeded.</summary>
        public bool success;

        /// <summary>Effectiveness of the action (0-1 scale).</summary>
        [Range(0f, 1f)] public float effectiveness;

        /// <summary>Human-readable description of what happened.</summary>
        public string details;

        /// <summary>Treasury/military resource cost of the action.</summary>
        public float cost;

        /// <summary>
        /// Creates a proxy action result.
        /// </summary>
        public ProxyActionResult(ProxyAction action, bool success, float effectiveness,
                                  string details, float cost)
        {
            this.action = action;
            this.success = success;
            this.effectiveness = Mathf.Clamp(effectiveness, 0f, 1f);
            this.details = details;
            this.cost = Mathf.Max(0f, cost);
        }
    }

    // =================================================================
    // PROXY WAR MANAGER
    // =================================================================

    /// <summary>
    /// Core system managing proxy wars: initiation, covert action execution,
    /// turn-based progression, exposure risk, and resolution conditions.
    /// Attach to a persistent GameManager as a singleton.
    /// </summary>
    public class ProxyWarManager : MonoBehaviour
    {
        // -------------------------------------------------------------
        // CONSTANTS
        // -------------------------------------------------------------
        private const float RebelWinThreshold = 80f;
        private const float RebelLoseThreshold = 20f;
        private const float GovCollapseThreshold = 20f;
        private const float GovStrongThreshold = 70f;
        private const float RegimeChangeMinRebelStrength = 70f;
        private const float BaseExposureGrowth = 2f;
        private const float AdvisorCaptureBaseChance = 0.03f;
        private const float FalseFlagSuccessBase = 0.5f;
        private const float RegimeChangeSuccessBase = 0.4f;
        private const int MilitiaTrainingDuration = 3;

        // Action costs
        private const float FundRebelsCost = 200f;
        private const float ArmRebelsCost = 500f;
        private const float TrainMilitiaCost = 300f;
        private const float DeployAdvisorsCost = 150f;
        private const float EstablishSafeZoneCost = 400f;
        private const float CovertInfluenceCost = 250f;
        private const float FalseFlagOpCost = 800f;
        private const float RegimeChangeCost = 1500f;

        // -------------------------------------------------------------
        // STATE
        // -------------------------------------------------------------
        private readonly Dictionary<string, ProxyWar> _proxyWars = new Dictionary<string, ProxyWar>();
        private int _currentTurn = 0;
        private int _warCounter = 0;

        // -------------------------------------------------------------
        // EVENTS
        // -------------------------------------------------------------
        /// <summary>Fired when a new proxy war begins. Parameters: (proxyWar, targetNationId).</summary>
        public event Action<ProxyWar, string> OnProxyWarStarted;

        /// <summary>Fired when a proxy war ends. Parameters: (proxyWar, outcome description).</summary>
        public event Action<ProxyWar, string> OnProxyWarEnded;

        // -------------------------------------------------------------
        // PROPERTIES
        // -------------------------------------------------------------
        /// <summary>Current global turn counter.</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>Number of active proxy wars.</summary>
        public int ActiveProxyWarCount => _proxyWars.Count(w => w.Value.isActive);

        // =============================================================
        // PROXY WAR INITIATION
        // =============================================================

        /// <summary>
        /// Starts a new proxy war with the sponsor covertly supporting rebels in the target nation.
        /// </summary>
        /// <param name="sponsor">Nation funding the proxy war.</param>
        /// <param name="target">Nation where the conflict takes place.</param>
        /// <param name="region">Center hex coordinate of the conflict zone.</param>
        /// <returns>The newly created <see cref="ProxyWar"/>.</returns>
        public ProxyWar StartProxyWar(string sponsor, string target, HexCoord region)
        {
            if (string.IsNullOrEmpty(sponsor) || string.IsNullOrEmpty(target))
            {
                Debug.LogError("[ProxyWarManager] StartProxyWar: sponsor and target must not be empty.");
                return null;
            }

            if (sponsor == target)
            {
                Debug.LogError("[ProxyWarManager] Cannot start a proxy war against yourself.");
                return null;
            }

            // Check if there's already an active proxy war in this target
            var existing = GetProxyWarsInNation(target);
            if (existing.Any(w => w.isActive && w.sponsorNation == sponsor))
            {
                Debug.LogWarning($"[ProxyWarManager] '{sponsor}' already has an active proxy war in '{target}'.");
                return null;
            }

            _warCounter++;
            string warId = $"PW_{sponsor}_{target}_{_warCounter:D3}";

            var proxyWar = new ProxyWar(warId, sponsor, target, region);
            _proxyWars[warId] = proxyWar;

            Debug.Log($"[ProxyWarManager] Proxy war STARTED: {warId}. " +
                      $"Sponsor: {sponsor}, Target: {target}, Region: {region}");

            OnProxyWarStarted?.Invoke(proxyWar, target);
            return proxyWar;
        }

        // =============================================================
        // PROXY ACTION EXECUTION
        // =============================================================

        /// <summary>
        /// Executes a specific covert action within an active proxy war.
        /// Each action type has different costs, effects, success chances, and risks:
        /// <list type="bullet">
        ///   <item><see cref="ProxyAction.FundRebels"/>: +10 rebel strength, costs treasury per turn</item>
        ///   <item><see cref="ProxyAction.ArmRebels"/>: +15 rebel strength, +5 effectiveness, costs military</item>
        ///   <item><see cref="ProxyAction.TrainMilitia"/>: +20 rebel combat power, takes 3 turns to complete</item>
        ///   <item><see cref="ProxyAction.DeployAdvisors"/>: +10 effectiveness, risk of capture creating a CB</item>
        ///   <item><see cref="ProxyAction.EstablishSafeZone"/>: +5 stability in region</item>
        ///   <item><see cref="ProxyAction.CovertInfluence"/>: slowly shift government opinion toward sponsor</item>
        ///   <item><see cref="ProxyAction.FalseFlagOp"/>: frame another nation, create a casus belli</item>
        ///   <item><see cref="ProxyAction.RegimeChange"/>: full overthrow attempt (requires rebel strength > 70)</item>
        /// </list>
        /// </summary>
        /// <param name="proxyWarId">ID of the proxy war to act within.</param>
        /// <param name="action">The covert action to execute.</param>
        /// <returns>A <see cref="ProxyActionResult"/> describing what happened.</returns>
        public ProxyActionResult ExecuteProxyAction(string proxyWarId, ProxyAction action)
        {
            if (string.IsNullOrEmpty(proxyWarId))
            {
                Debug.LogError("[ProxyWarManager] ExecuteProxyAction: proxyWarId is null or empty.");
                return new ProxyActionResult(action, false, 0f, "Invalid proxy war ID.", 0f);
            }

            if (!_proxyWars.TryGetValue(proxyWarId, out ProxyWar war))
            {
                Debug.LogError($"[ProxyWarManager] Proxy war '{proxyWarId}' not found.");
                return new ProxyActionResult(action, false, 0f, "Proxy war not found.", 0f);
            }

            if (!war.isActive)
            {
                Debug.LogWarning($"[ProxyWarManager] Proxy war '{proxyWarId}' is no longer active.");
                return new ProxyActionResult(action, false, 0f, "Proxy war has ended.", 0f);
            }

            // --- Route to specific action handler ---
            switch (action)
            {
                case ProxyAction.FundRebels:
                    return ExecuteFundRebels(war);
                case ProxyAction.ArmRebels:
                    return ExecuteArmRebels(war);
                case ProxyAction.TrainMilitia:
                    return ExecuteTrainMilitia(war);
                case ProxyAction.DeployAdvisors:
                    return ExecuteDeployAdvisors(war);
                case ProxyAction.EstablishSafeZone:
                    return ExecuteEstablishSafeZone(war);
                case ProxyAction.CovertInfluence:
                    return ExecuteCovertInfluence(war);
                case ProxyAction.FalseFlagOp:
                    return ExecuteFalseFlagOp(war);
                case ProxyAction.RegimeChange:
                    return ExecuteRegimeChange(war);
                default:
                    return new ProxyActionResult(action, false, 0f, "Unknown action type.", 0f);
            }
        }

        // -------------------------------------------------------------
        // INDIVIDUAL ACTION HANDLERS
        // -------------------------------------------------------------

        /// <summary>Send money to rebel groups. +10 rebel strength, ongoing cost.</summary>
        private ProxyActionResult ExecuteFundRebels(ProxyWar war)
        {
            float effectiveness = UnityEngine.Random.Range(0.6f, 1f);
            float strengthGain = 10f * effectiveness;

            war.rebelStrength = Mathf.Clamp(war.rebelStrength + strengthGain, 0f, 100f);
            war.fundingPerTurn += 50f; // increases ongoing cost

            if (!war.activeActions.Contains(ProxyAction.FundRebels))
                war.activeActions.Add(ProxyAction.FundRebels);

            // Increase exposure slightly
            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 1f);

            string details = $"Funding rebels: +{strengthGain:F1} strength (now {war.rebelStrength:F1}). " +
                             $"Ongoing funding increased to {war.fundingPerTurn:F0}/turn.";

            Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {details}");
            return new ProxyActionResult(ProxyAction.FundRebels, true, effectiveness, details, FundRebelsCost);
        }

        /// <summary>Provide weapons. +15 rebel strength, +5 effectiveness.</summary>
        private ProxyActionResult ExecuteArmRebels(ProxyWar war)
        {
            float effectiveness = UnityEngine.Random.Range(0.5f, 1f);
            float strengthGain = 15f * effectiveness;
            float effGain = 0.05f * effectiveness;

            war.rebelStrength = Mathf.Clamp(war.rebelStrength + strengthGain, 0f, 100f);
            war.rebelEffectiveness = Mathf.Min(2f, war.rebelEffectiveness + effGain);

            if (!war.activeActions.Contains(ProxyAction.ArmRebels))
                war.activeActions.Add(ProxyAction.ArmRebels);

            // Arms shipments increase exposure risk more
            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 3f);

            string details = $"Arming rebels: +{strengthGain:F1} strength, +{effGain:F2} effectiveness. " +
                             $"Rebel strength now {war.rebelStrength:F1}.";

            Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {details}");
            return new ProxyActionResult(ProxyAction.ArmRebels, true, effectiveness, details, ArmRebelsCost);
        }

        /// <summary>Train militia. +20 rebel combat power, takes 3 turns.</summary>
        private ProxyActionResult ExecuteTrainMilitia(ProxyWar war)
        {
            if (war.militiaTrainingTurnsRemaining > 0)
            {
                string details = $"Militia training already in progress ({war.militiaTrainingTurnsRemaining} turns remaining).";
                Debug.LogWarning($"[ProxyWarManager] {war.proxyWarId}: {details}");
                return new ProxyActionResult(ProxyAction.TrainMilitia, false, 0f, details, 0f);
            }

            war.militiaTrainingTurnsRemaining = MilitiaTrainingDuration;

            if (!war.activeActions.Contains(ProxyAction.TrainMilitia))
                war.activeActions.Add(ProxyAction.TrainMilitia);

            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 2f);

            string details = $"Militia training initiated. Will complete in {MilitiaTrainingDuration} turns. " +
                             $"Expected +20 combat power upon completion.";

            Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {details}");
            return new ProxyActionResult(ProxyAction.TrainMilitia, true, 0.8f, details, TrainMilitiaCost);
        }

        /// <summary>Deploy advisors. +10 effectiveness, risk of capture (CB for target).</summary>
        private ProxyActionResult ExecuteDeployAdvisors(ProxyWar war)
        {
            float effectiveness = UnityEngine.Random.Range(0.7f, 1f);
            float effGain = 0.10f * effectiveness;

            war.rebelEffectiveness = Mathf.Min(2f, war.rebelEffectiveness + effGain);
            war.advisorTurnsDeployed++;

            if (!war.activeActions.Contains(ProxyAction.DeployAdvisors))
                war.activeActions.Add(ProxyAction.DeployAdvisors);

            // Advisors increase exposure significantly
            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 4f);

            // Check for advisor capture risk
            bool captured = false;
            float captureChance = AdvisorCaptureBaseChance + (war.advisorTurnsDeployed * 0.01f);
            captureChance *= (war.exposureLevel / 100f); // higher exposure = more likely capture

            if (UnityEngine.Random.value < captureChance)
            {
                captured = true;
                // Remove advisors on capture
                war.advisorTurnsDeployed = 0;
                war.activeActions.Remove(ProxyAction.DeployAdvisors);
                war.exposureLevel = 100f; // definitely exposed now

                string details = $"Advisors CAPTURED by {war.targetNation} government! " +
                                 $"Casus belli created against {war.sponsorNation}. " +
                                 $"Effectiveness gain negated.";
                Debug.LogWarning($"[ProxyWarManager] {war.proxyWarId}: {details}");
                return new ProxyActionResult(ProxyAction.DeployAdvisors, false, 0f, details, DeployAdvisorsCost);
            }

            string successDetails = $"Advisors deployed successfully ({war.advisorTurnsDeployed} turns). " +
                                    $"+{effGain:F2} effectiveness. Capture risk: {captureChance:P1}.";

            Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {successDetails}");
            return new ProxyActionResult(ProxyAction.DeployAdvisors, true, effectiveness, successDetails, DeployAdvisorsCost);
        }

        /// <summary>Establish safe zone. +5 stability in region.</summary>
        private ProxyActionResult ExecuteEstablishSafeZone(ProxyWar war)
        {
            float effectiveness = UnityEngine.Random.Range(0.7f, 1f);
            float stabilityGain = 5f * effectiveness;

            // Improves rebel local support and provides a base of operations
            war.rebelStrength = Mathf.Clamp(war.rebelStrength + 3f * effectiveness, 0f, 100f);

            if (!war.activeActions.Contains(ProxyAction.EstablishSafeZone))
                war.activeActions.Add(ProxyAction.EstablishSafeZone);

            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 2f);

            string details = $"Safe zone established at {war.centerHex}. " +
                             $"+{stabilityGain:F1} local stability, +{3f * effectiveness:F1} rebel strength. " +
                             $"Radius: {war.radius} hexes.";

            Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {details}");
            return new ProxyActionResult(ProxyAction.EstablishSafeZone, true, effectiveness, details, EstablishSafeZoneCost);
        }

        /// <summary>Covertly influence government officials toward sponsor.</summary>
        private ProxyActionResult ExecuteCovertInfluence(ProxyWar war)
        {
            float effectiveness = UnityEngine.Random.Range(0.4f, 0.9f);

            // Slowly erode government strength through corruption of officials
            float govLoss = 5f * effectiveness;
            war.governmentStrength = Mathf.Clamp(war.governmentStrength - govLoss, 0f, 100f);

            if (!war.activeActions.Contains(ProxyAction.CovertInfluence))
                war.activeActions.Add(ProxyAction.CovertInfluence);

            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 1.5f);

            string details = $"Covert influence applied: -{govLoss:F1} government strength " +
                             $"(now {war.governmentStrength:F1}). Officials being swayed toward {war.sponsorNation}.";

            Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {details}");
            return new ProxyActionResult(ProxyAction.CovertInfluence, true, effectiveness, details, CovertInfluenceCost);
        }

        /// <summary>Frame another nation to create a casus belli.</summary>
        private ProxyActionResult ExecuteFalseFlagOp(ProxyWar war)
        {
            float successChance = FalseFlagSuccessBase * (1f - war.exposureLevel / 200f);
            bool succeeded = UnityEngine.Random.value < successChance;

            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 5f);

            if (succeeded)
            {
                string details = $"False flag operation SUCCEEDED. Casus belli created against {war.targetNation}. " +
                                 $"International community believes the framed narrative.";
                Debug.Log($"[ProxyWarManager] {war.proxyWarId}: {details}");
                return new ProxyActionResult(ProxyAction.FalseFlagOp, true, successChance, details, FalseFlagOpCost);
            }
            else
            {
                float backlashExposure = 15f;
                war.exposureLevel = Mathf.Min(100f, war.exposureLevel + backlashExposure);

                string details = $"False flag operation FAILED. Operation exposed! " +
                                 $"+{backlashExposure} exposure. Diplomatic penalty for {war.sponsorNation}.";
                Debug.LogWarning($"[ProxyWarManager] {war.proxyWarId}: {details}");
                return new ProxyActionResult(ProxyAction.FalseFlagOp, false, 0f, details, FalseFlagOpCost);
            }
        }

        /// <summary>Attempt full government overthrow. Requires rebel strength > 70.</summary>
        private ProxyActionResult ExecuteRegimeChange(ProxyWar war)
        {
            if (war.rebelStrength < RegimeChangeMinRebelStrength)
            {
                string details = $"Regime change FAILED: Rebel strength ({war.rebelStrength:F1}) " +
                                 $"below minimum ({RegimeChangeMinRebelStrength}). Need more rebel support.";
                Debug.LogWarning($"[ProxyWarManager] {war.proxyWarId}: {details}");
                return new ProxyActionResult(ProxyAction.RegimeChange, false, 0f, details, 0f);
            }

            // Success chance based on rebel strength, effectiveness, and government weakness
            float rebelFactor = (war.rebelStrength - RegimeChangeMinRebelStrength)
                               / (100f - RegimeChangeMinRebelStrength);
            float govWeakness = 1f - (war.governmentStrength / 100f);
            float effFactor = war.rebelEffectiveness / 2f;
            float successChance = RegimeChangeSuccessBase + (rebelFactor * 0.3f) + (govWeakness * 0.2f) + (effFactor * 0.1f);
            successChance = Mathf.Clamp(successChance, 0.1f, 0.9f);

            bool succeeded = UnityEngine.Random.value < successChance;
            war.exposureLevel = Mathf.Min(100f, war.exposureLevel + 10f);

            if (succeeded)
            {
                // Regime change succeeds - end the proxy war as a victory
                war.isActive = false;
                war.succeeded = true;
                war.outcome = $"Government of {war.targetNation} overthrown by rebel forces " +
                             $"sponsored by {war.sponsorNation}. New puppet government installed.";
                war.isExposed = true;

                string details = $"REGIME CHANGE SUCCEEDED in {war.targetNation}! " +
                                 $"Rebel forces backed by {war.sponsorNation} have toppled the government.";

                Debug.Log($"[ProxyWarManager] *** {war.proxyWarId}: {details} ***");
                OnProxyWarEnded?.Invoke(war, war.outcome);
                return new ProxyActionResult(ProxyAction.RegimeChange, true, successChance, details, RegimeChangeCost);
            }
            else
            {
                // Failed attempt - severe consequences
                float rebelLoss = 20f;
                war.rebelStrength = Mathf.Clamp(war.rebelStrength - rebelLoss, 0f, 100f);
                war.governmentStrength = Mathf.Clamp(war.governmentStrength + 10f, 0f, 100f);
                war.isExposed = true;

                string details = $"Regime change FAILED. Rebel forces suffered heavy losses (-{rebelLoss} strength). " +
                                 $"Operation exposed. {war.targetNation} government strengthened. " +
                                 $"Major diplomatic incident for {war.sponsorNation}.";

                Debug.LogWarning($"[ProxyWarManager] {war.proxyWarId}: {details}");
                return new ProxyActionResult(ProxyAction.RegimeChange, false, 0f, details, RegimeChangeCost);
            }
        }

        // =============================================================
        // TURN-BASED UPDATE
        // =============================================================

        /// <summary>
        /// Updates all active proxy wars for the current turn. Each turn:
        /// <list type="bullet">
        ///   <item>Rebel strength evolves based on active actions and funding</item>
        ///   <item>Government strength slowly decreases while proxy war is active</item>
        ///   <item>Militia training progresses</item>
        ///   <item>Exposure risk accumulates</item>
        ///   <item>Win/loss conditions are checked</item>
        /// </list>
        /// </summary>
        public void UpdateProxyWars()
        {
            _currentTurn++;

            var endedWars = new List<ProxyWar>();

            foreach (ProxyWar war in _proxyWars.Values)
            {
                if (!war.isActive) continue;

                war.turnsActive++;

                // --- 1. Rebel strength evolution ---
                float rebelDelta = 0f;

                // Funded rebels grow slowly
                if (war.activeActions.Contains(ProxyAction.FundRebels))
                    rebelDelta += 2f;

                // Armed rebels maintain or grow slightly
                if (war.activeActions.Contains(ProxyAction.ArmRebels))
                    rebelDelta += 1f;

                // Safe zones provide steady recruitment
                if (war.activeActions.Contains(ProxyAction.EstablishSafeZone))
                    rebelDelta += 1.5f;

                // Covert influence weakens government
                if (war.activeActions.Contains(ProxyAction.CovertInfluence))
                    rebelDelta += 0.5f;

                // Natural rebel attrition without support
                if (war.activeActions.Count == 0)
                    rebelDelta -= 3f;

                // Apply effectiveness multiplier to gains
                rebelDelta *= war.rebelEffectiveness;

                war.rebelStrength = Mathf.Clamp(war.rebelStrength + rebelDelta, 0f, 100f);

                // --- 2. Government strength evolution ---
                float govDelta = -0.5f; // constant pressure from active conflict

                // Active proxy war actions drain government
                govDelta -= war.activeActions.Count * 0.3f;

                // Strong government can push back
                if (war.governmentStrength > 60f)
                    govDelta += 1f;

                // Weak government deteriorates faster
                if (war.governmentStrength < 30f)
                    govDelta -= 1.5f;

                war.governmentStrength = Mathf.Clamp(war.governmentStrength + govDelta, 0f, 100f);

                // --- 3. Militia training progress ---
                if (war.militiaTrainingTurnsRemaining > 0)
                {
                    war.militiaTrainingTurnsRemaining--;
                    if (war.militiaTrainingTurnsRemaining == 0)
                    {
                        // Training complete!
                        float trainingBonus = 20f;
                        war.rebelStrength = Mathf.Clamp(war.rebelStrength + trainingBonus, 0f, 100f);
                        war.rebelEffectiveness = Mathf.Min(2f, war.rebelEffectiveness + 0.15f);
                        war.activeActions.Remove(ProxyAction.TrainMilitia);

                        Debug.Log($"[ProxyWarManager] {war.proxyWarId}: Militia training COMPLETE. " +
                                  $"+{trainingBonus} rebel strength, +0.15 effectiveness.");
                    }
                }

                // --- 4. Exposure growth ---
                float exposureGrowth = BaseExposureGrowth;
                // More actions = faster exposure
                exposureGrowth += war.activeActions.Count * 0.5f;
                // Longer wars = more likely to be discovered
                exposureGrowth += war.turnsActive * 0.1f;

                war.exposureLevel = Mathf.Clamp(war.exposureLevel + exposureGrowth, 0f, 100f);

                // Check for spontaneous exposure
                if (!war.isExposed && war.exposureLevel > 85f)
                {
                    float exposureChance = (war.exposureLevel - 85f) / 15f * 0.3f;
                    if (UnityEngine.Random.value < exposureChance)
                    {
                        war.isExposed = true;
                        Debug.LogWarning($"[ProxyWarManager] {war.proxyWarId}: Proxy war EXPOSED! " +
                                         $"{war.sponsorNation}'s involvement in {war.targetNation} revealed. " +
                                         "Major diplomatic penalty incurred.");
                    }
                }

                // --- 5. Check win/loss conditions ---
                if (war.rebelStrength >= RebelWinThreshold && war.governmentStrength <= GovCollapseThreshold)
                {
                    // REBEL VICTORY
                    war.isActive = false;
                    war.succeeded = true;
                    war.outcome = $"Rebel forces in {war.targetNation} have prevailed. " +
                                 $"Government has collapsed. {war.sponsorNation}'s influence established.";
                    endedWars.Add(war);

                    Debug.Log($"[ProxyWarManager] *** {war.proxyWarId}: REBEL VICTORY *** {war.outcome}");
                }
                else if (war.governmentStrength >= GovStrongThreshold && war.rebelStrength <= RebelLoseThreshold)
                {
                    // REBELLION CRUSHED
                    war.isActive = false;
                    war.succeeded = false;
                    war.failed = true;
                    war.outcome = $"Rebellion in {war.targetNation} has been crushed. " +
                                 $"{war.sponsorNation}'s proxy operation has failed completely.";
                    endedWars.Add(war);

                    Debug.Log($"[ProxyWarManager] *** {war.proxyWarId}: REBELLION CRUSHED *** {war.outcome}");
                }

                // --- 6. Sponsor funding drain ---
                // (In a full game, this would deduct from the sponsor's treasury)
                if (war.isActive && war.fundingPerTurn > 0)
                {
                    // Log the ongoing cost
                }
            }

            // Fire events for ended wars
            foreach (ProxyWar ended in endedWars)
            {
                OnProxyWarEnded?.Invoke(ended, ended.outcome);
            }
        }

        // =============================================================
        // EXPOSURE RISK
        // =============================================================

        /// <summary>
        /// Calculates the current exposure risk (0-1) of a proxy war being discovered
        /// by the target nation or international community.
        /// </summary>
        /// <param name="war">The proxy war to evaluate.</param>
        /// <returns>Exposure risk from 0 (safe) to 1 (certain discovery).</returns>
        public float CalculateExposureRisk(ProxyWar war)
        {
            if (war == null) return 0f;

            float risk = war.exposureLevel / 100f;

            // More active actions increase risk
            risk += war.activeActions.Count * 0.03f;

            // Longer wars increase risk
            risk += Mathf.Min(war.turnsActive * 0.005f, 0.15f);

            // Advisors are the most risky
            if (war.activeActions.Contains(ProxyAction.DeployAdvisors))
                risk += 0.05f;

            return Mathf.Clamp(risk, 0f, 1f);
        }

        /// <summary>
        /// Calculates exposure risk by proxy war ID.
        /// </summary>
        /// <param name="proxyWarId">ID of the proxy war.</param>
        /// <returns>Exposure risk from 0 to 1, or 0 if not found.</returns>
        public float CalculateExposureRisk(string proxyWarId)
        {
            if (string.IsNullOrEmpty(proxyWarId) || !_proxyWars.TryGetValue(proxyWarId, out ProxyWar war))
                return 0f;

            return CalculateExposureRisk(war);
        }

        // =============================================================
        // QUERIES
        // =============================================================

        /// <summary>
        /// Returns all active (ongoing) proxy wars.
        /// </summary>
        /// <returns>List of active <see cref="ProxyWar"/> objects.</returns>
        public List<ProxyWar> GetActiveProxyWars()
        {
            return _proxyWars.Values.Where(w => w.isActive).ToList();
        }

        /// <summary>
        /// Returns all proxy wars (active and historical) affecting a specific nation,
        /// either as target or as sponsor.
        /// </summary>
        /// <param name="nationId">Nation to filter by.</param>
        /// <returns>List of proxy wars involving this nation.</returns>
        public List<ProxyWar> GetProxyWarsInNation(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return new List<ProxyWar>();

            return _proxyWars.Values
                .Where(w => w.targetNation == nationId || w.sponsorNation == nationId)
                .ToList();
        }

        /// <summary>
        /// Returns proxy wars where a nation is the target.
        /// </summary>
        /// <param name="nationId">Target nation ID.</param>
        /// <returns>List of proxy wars targeting this nation.</returns>
        public List<ProxyWar> GetProxyWarsTargeting(string nationId)
        {
            return _proxyWars.Values.Where(w => w.targetNation == nationId && w.isActive).ToList();
        }

        /// <summary>
        /// Returns proxy wars sponsored by a specific nation.
        /// </summary>
        /// <param name="nationId">Sponsor nation ID.</param>
        /// <returns>List of proxy wars sponsored by this nation.</returns>
        public List<ProxyWar> GetProxyWarsSponsoredBy(string nationId)
        {
            return _proxyWars.Values.Where(w => w.sponsorNation == nationId && w.isActive).ToList();
        }

        /// <summary>
        /// Retrieves a proxy war by its unique ID.
        /// </summary>
        /// <param name="proxyWarId">Proxy war identifier.</param>
        /// <returns>The <see cref="ProxyWar"/>, or null if not found.</returns>
        public ProxyWar GetProxyWar(string proxyWarId)
        {
            if (string.IsNullOrEmpty(proxyWarId)) return null;
            _proxyWars.TryGetValue(proxyWarId, out ProxyWar war);
            return war;
        }

        /// <summary>
        /// Returns all proxy wars including completed ones.
        /// </summary>
        public List<ProxyWar> GetAllProxyWars()
        {
            return _proxyWars.Values.ToList();
        }

        // =============================================================
        // UTILITY
        // =============================================================

        /// <summary>
        /// Clears all proxy war data. Use on game reset.
        /// </summary>
        public void Reset()
        {
            _proxyWars.Clear();
            _currentTurn = 0;
            _warCounter = 0;
            Debug.Log("[ProxyWarManager] All proxy war data cleared.");
        }
    }
}
