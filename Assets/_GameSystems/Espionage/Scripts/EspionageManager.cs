// ============================================================================
// Iron Protocol - Espionage Manager
// Complete intelligence/espionage system for strategic operations
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.Espionage
{
    // ========================================================================
    // ENUMERATIONS
    // ========================================================================

    /// <summary>
    /// Defines the types of covert missions a spy can undertake against a target nation.
    /// Each mission type has unique risks, rewards, and difficulty modifiers.
    /// </summary>
    public enum SpyMission
    {
        /// <summary>Reveal hidden units and structures in a region without being detected.</summary>
        Reconnaissance,

        /// <summary>Damage enemy infrastructure, reducing production and potentially destroying buildings.</summary>
        Sabotage,

        /// <summary>Attempt to eliminate an enemy leader, reducing national stability and morale.</summary>
        Assassination,

        /// <summary>Steal an unresearched technology from the target nation.</summary>
        TechTheft,

        /// <summary>Boost defensive counter-espionage capabilities and hunt enemy spies.</summary>
        CounterIntelligence,

        /// <summary>Establish a permanent covert network for ongoing intelligence gathering.</summary>
        InstallNetwork,

        /// <summary>Frame another nation for a hostile act, manipulating diplomatic relations.</summary>
        FalseFlag,

        /// <summary>Bribe an enemy military unit to defect to your side.</summary>
        Bribe
    }

    /// <summary>
    /// Represents the depth and reliability of intelligence gathered about a target.
    /// Higher levels provide more accurate and detailed information.
    /// </summary>
    public enum IntelLevel
    {
        /// <summary>No intelligence available.</summary>
        None,

        /// <summary>Basic awareness; vague or unreliable information.</summary>
        Low,

        /// <summary>Moderate intelligence; general trends and capabilities known.</summary>
        Medium,

        /// <summary>Comprehensive intelligence; detailed knowledge of assets and plans.</summary>
        High,

        /// <summary>Total situational awareness; nearly omniscient about the target.</summary>
        Complete
    }

    // ========================================================================
    // SPY CLASS
    // ========================================================================

    /// <summary>
    /// Represents an individual covert operative belonging to a nation.
    /// Spies can be assigned missions, gain experience, and may be captured or killed.
    /// </summary>
    [Serializable]
    public class Spy
    {
        /// <summary>Unique identifier for this spy agent.</summary>
        public string SpyId { get; set; }

        /// <summary>The nation that owns and controls this spy.</summary>
        public string OwnerNation { get; set; }

        /// <summary>The nation this spy is currently operating within.</summary>
        public string AssignedNation { get; set; }

        /// <summary>Current hex-grid position of the spy on the world map.</summary>
        public HexCoord Position { get; set; }

        /// <summary>The type of mission the spy is currently executing, if any.</summary>
        public SpyMission CurrentMission { get; set; }

        /// <summary>
        /// The spy's operational skill rating (1-100). Higher values increase
        /// mission success probability and effectiveness.
        /// </summary>
        public int Skill { get; set; }

        /// <summary>
        /// The spy's stealth rating (1-100). Higher values reduce the chance
        /// of detection during missions.
        /// </summary>
        public int Stealth { get; set; }

        /// <summary>
        /// The spy's loyalty rating (1-100). Lower values increase the risk
        /// of defection or being turned by the enemy.
        /// </summary>
        public int Loyalty { get; set; }

        /// <summary>Number of turns remaining before the current mission completes.</summary>
        public int TurnsRemaining { get; set; }

        /// <summary>Whether the spy is currently hidden from enemy detection.</summary>
        public bool IsHidden { get; set; }

        /// <summary>Whether the spy has been captured by enemy counter-intelligence.</summary>
        public bool IsCaptured { get; set; }

        /// <summary>Whether the spy has been killed during a mission or by enemy action.</summary>
        public bool IsKilled { get; set; }

        /// <summary>
        /// Initializes a new spy with the specified parameters.
        /// Stats are randomly generated within moderate ranges.
        /// </summary>
        /// <param name="spyId">Unique identifier for the spy.</param>
        /// <param name="ownerNation">The nation that recruits and owns this spy.</param>
        /// <param name="position">Starting hex-grid position.</param>
        public Spy(string spyId, string ownerNation, HexCoord position)
        {
            SpyId = spyId;
            OwnerNation = ownerNation;
            AssignedNation = ownerNation;
            Position = position;
            CurrentMission = SpyMission.Reconnaissance;
            Skill = UnityEngine.Random.Range(30, 60);
            Stealth = UnityEngine.Random.Range(30, 60);
            Loyalty = UnityEngine.Random.Range(50, 80);
            TurnsRemaining = 0;
            IsHidden = true;
            IsCaptured = false;
            IsKilled = false;
        }

        /// <summary>
        /// Calculates an overall effectiveness score for this spy based on core attributes.
        /// Used in mission success probability calculations.
        /// </summary>
        /// <returns>A weighted effectiveness value between 0 and 100.</returns>
        public float CalculateEffectiveness()
        {
            return (Skill * 0.4f) + (Stealth * 0.35f) + (Loyalty * 0.25f);
        }

        /// <summary>
        /// Determines if the spy is currently available for a new mission assignment.
        /// </summary>
        /// <returns>True if the spy is idle, hidden, alive, and not captured.</returns>
        public bool IsAvailable()
        {
            return !IsCaptured && !IsKilled && IsHidden && TurnsRemaining <= 0;
        }

        /// <summary>
        /// Provides a human-readable summary of the spy's status for debugging.
        /// </summary>
        /// <returns>A formatted string with key spy attributes.</returns>
        public override string ToString()
        {
            string status = IsKilled ? "KILLED"
                : IsCaptured ? "CAPTURED"
                : TurnsRemaining > 0 ? $"ON MISSION ({TurnsRemaining} turns)"
                : "IDLE";
            return $"[{SpyId}] {OwnerNation} | Skill:{Skill} Stealth:{Stealth} Loyalty:{Loyalty} | {status}";
        }
    }

    // ========================================================================
    // MISSION RESULT CLASS
    // ========================================================================

    /// <summary>
    /// Contains the outcome data of a completed spy mission, including success/failure,
    /// details of what was discovered or accomplished, and any casualties.
    /// </summary>
    [Serializable]
    public class MissionResult
    {
        /// <summary>Whether the mission achieved its primary objective.</summary>
        public bool Success { get; set; }

        /// <summary>The type of mission that was executed.</summary>
        public string MissionType { get; set; }

        /// <summary>Human-readable narrative description of the mission outcome.</summary>
        public string Details { get; set; }

        /// <summary>
        /// The amount of intelligence value gained (0.0 to 1.0).
        /// Higher values mean more useful information was extracted.
        /// </summary>
        public float IntelGained { get; set; }

        /// <summary>
        /// If a TechTheft mission succeeded, contains the ID of the stolen technology.
        /// Null or empty if no tech was stolen.
        /// </summary>
        public string StolenTechId { get; set; }

        /// <summary>
        /// A list of discoveries made during the mission (unit positions, building info, etc.).
        /// </summary>
        public List<string> Discoveries { get; set; } = new List<string>();

        /// <summary>Whether the spy was captured during the mission.</summary>
        public bool SpyCaptured { get; set; }

        /// <summary>Whether the spy was killed during the mission.</summary>
        public bool SpyKilled { get; set; }

        /// <summary>The ID of the spy that executed this mission.</summary>
        public string SpyId { get; set; }

        /// <summary>The target nation of the mission.</summary>
        public string TargetNation { get; set; }

        /// <summary>
        /// Creates a default failure result with empty metadata.
        /// </summary>
        public MissionResult()
        {
            Success = false;
            MissionType = "Unknown";
            Details = "Mission failed.";
            IntelGained = 0f;
            StolenTechId = null;
            Discoveries = new List<string>();
            SpyCaptured = false;
            SpyKilled = false;
            SpyId = string.Empty;
            TargetNation = string.Empty;
        }
    }

    // ========================================================================
    // NATION INTEL CLASS
    // ========================================================================

    /// <summary>
    /// Aggregated intelligence profile for a single nation, tracking what is known
    /// about their military, economy, technology, and diplomacy.
    /// </summary>
    [Serializable]
    public class NationIntel
    {
        /// <summary>The nation this intelligence profile describes.</summary>
        public string NationId { get; set; }

        /// <summary>Current intelligence level on the nation's military forces and deployments.</summary>
        public IntelLevel MilitaryIntel { get; set; } = IntelLevel.None;

        /// <summary>Current intelligence level on the nation's economy and resources.</summary>
        public IntelLevel EconomicIntel { get; set; } = IntelLevel.None;

        /// <summary>Current intelligence level on the nation's technological progress.</summary>
        public IntelLevel TechIntel { get; set; } = IntelLevel.None;

        /// <summary>Current intelligence level on the nation's diplomatic activities and relations.</summary>
        public IntelLevel DiplomaticIntel { get; set; } = IntelLevel.None;

        /// <summary>
        /// Map of known enemy unit positions with confidence values (0.0 to 1.0).
        /// Key is a serialized hex coordinate string, value is the detection confidence.
        /// </summary>
        public Dictionary<string, float> KnownUnitPositions { get; set; } = new Dictionary<string, float>();

        /// <summary>List of technology IDs known to be researched by this nation.</summary>
        public List<string> DiscoveredTechs { get; set; } = new List<string>();

        /// <summary>
        /// The nation's counter-espionage defensive rating (0.0 to 1.0).
        /// Higher values make it harder for enemy spies to operate successfully.
        /// </summary>
        public float CounterEspionageLevel { get; set; } = 0.2f;

        /// <summary>
        /// Initializes a new intelligence profile for the specified nation.
        /// All intel levels start at None with a baseline counter-espionage level.
        /// </summary>
        /// <param name="nationId">The nation to track intelligence for.</param>
        public NationIntel(string nationId)
        {
            NationId = nationId;
        }

        /// <summary>
        /// Calculates an overall intelligence score (0.0 to 1.0) based on all intel categories.
        /// Military carries the highest weight due to its strategic importance.
        /// </summary>
        /// <returns>Weighted average of all intelligence levels.</returns>
        public float GetOverallIntelScore()
        {
            float military = (int)MilitaryIntel / 4f;
            float economic = (int)EconomicIntel / 4f;
            float tech = (int)TechIntel / 4f;
            float diplomatic = (int)DiplomaticIntel / 4f;
            return (military * 0.35f) + (economic * 0.2f) + (tech * 0.25f) + (diplomatic * 0.2f);
        }
    }

    // ========================================================================
    // ESPIONAGE MANAGER (MONOBEHAVIOUR)
    // ========================================================================

    /// <summary>
    /// Central manager for all espionage and intelligence operations in the game.
    /// Handles spy recruitment, mission assignment, mission processing, and intel tracking.
    /// Attach this to a persistent GameObject (e.g., GameManager) as a singleton.
    /// 
    /// <para>Integration Points:</para>
    /// <list type="bullet">
    ///   <item><description>Call <see cref="OnTurnStarted"/> from your turn manager.</description></item>
    ///   <item><description>Subscribe to <see cref="OnMissionCompleted"/> for UI notifications.</description></item>
    ///   <item><description>Use <see cref="GetIntelForNation"/> for AI decision-making.</description></item>
    /// </list>
    /// </summary>
    public class EspionageManager : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // INSPECTOR CONFIGURATION
        // --------------------------------------------------------------------

        [Header("Economy")]
        [Tooltip("Treasury cost to recruit a new spy.")]
        [SerializeField] private int recruitCost = 200;

        [Tooltip("Treasury cost per turn to maintain an active spy network.")]
        [SerializeField] private int networkMaintenanceCost = 50;

        [Tooltip("Treasury cost to bribe an enemy unit to defect.")]
        [SerializeField] private int bribeCost = 500;

        [Header("Mission Durations (turns)")]
        [Tooltip("Turns required for a reconnaissance mission to complete.")]
        [SerializeField] private int reconDuration = 2;

        [Tooltip("Turns required for a sabotage mission to complete.")]
        [SerializeField] private int sabotageDuration = 3;

        [Tooltip("Turns required for an assassination mission to complete.")]
        [SerializeField] private int assassinationDuration = 4;

        [Tooltip("Turns required for a tech theft mission to complete.")]
        [SerializeField] private int techTheftDuration = 5;

        [Tooltip("Turns required for a counter-intelligence sweep to complete.")]
        [SerializeField] private int counterIntelDuration = 2;

        [Tooltip("Turns required to install a covert network.")]
        [SerializeField] private int installNetworkDuration = 4;

        [Tooltip("Turns required for a false-flag operation to complete.")]
        [SerializeField] private int falseFlagDuration = 3;

        [Tooltip("Turns required for a bribe operation to complete.")]
        [SerializeField] private int bribeDuration = 2;

        [Header("Mission Risk Factors")]
        [Tooltip("Base detection chance per mission, before stealth modifiers (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float baseDetectionChance = 0.15f;

        [Tooltip("Chance of spy death on mission failure (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float deathOnFailureChance = 0.2f;

        [Header("Intel Settings")]
        [Tooltip("Number of turns reconnaissance intel remains visible before fading.")]
        [SerializeField] private int reconIntelDuration = 5;

        [Tooltip("Radius in hexes for reconnaissance visibility reveal.")]
        [SerializeField] private int reconVisionRadius = 3;

        [Tooltip("Counter-intel passive boost per turn when no active counter-intel mission.")]
        [SerializeField] private float passiveCounterIntelGrowth = 0.01f;

        [Tooltip("Counter-intel decay per turn when above base level.")]
        [SerializeField] private float counterIntelDecayRate = 0.02f;

        // --------------------------------------------------------------------
        // PRIVATE STATE
        // --------------------------------------------------------------------

        /// <summary>All active and inactive spies in the game, keyed by spy ID.</summary>
        private readonly Dictionary<string, Spy> _spies = new Dictionary<string, Spy>();

        /// <summary>Intelligence profiles for all nations, keyed by nation ID.</summary>
        private readonly Dictionary<string, NationIntel> _nationIntels = new Dictionary<string, NationIntel>();

        /// <summary>Installed spy networks per target nation. Key=target nation, Value=list of owning nations.</summary>
        private readonly Dictionary<string, List<string>> _installedNetworks = new Dictionary<string, List<string>>();

        /// <summary>Temporary intel reveals from reconnaissance with expiry turn numbers.</summary>
        private readonly Dictionary<string, int> _tempIntelExpiry = new Dictionary<string, int>();

        /// <summary>Counter-espionage mission boosts with remaining duration, keyed by nation ID.</summary>
        private readonly Dictionary<string, int> _counterIntelBoosts = new Dictionary<string, int>();

        /// <summary>Running spy ID counter for unique generation.</summary>
        private int _spyIdCounter;

        /// <summary>The current turn number, updated each turn cycle.</summary>
        private int _currentTurn;

        // --------------------------------------------------------------------
        // EVENTS
        // --------------------------------------------------------------------

        /// <summary>
        /// Fired when any spy mission completes, providing the full result.
        /// Subscribers should check <see cref="MissionResult.Success"/> to determine outcome.
        /// </summary>
        public event Action<MissionResult> OnMissionCompleted;

        /// <summary>
        /// Fired when a spy is captured by enemy counter-intelligence.
        /// Parameters: (spyId, capturingNationId).
        /// </summary>
        public event Action<string, string> OnSpyCaptured;

        /// <summary>
        /// Fired when a spy is killed during a mission.
        /// Parameters: (spyId, responsibleNationId).
        /// </summary>
        public event Action<string, string> OnSpyKilled;

        /// <summary>
        /// Fired when intelligence levels change for any nation.
        /// Parameters: (nationId, intelCategory, newLevel).
        /// </summary>
        public event Action<string, string, IntelLevel> OnIntelChanged;

        // --------------------------------------------------------------------
        // PROPERTIES
        // --------------------------------------------------------------------

        /// <summary>Gets the total number of spies ever created (including dead/captured).</summary>
        public int TotalSpyCount => _spies.Count;

        /// <summary>Gets the number of currently active (non-dead, non-captured) spies.</summary>
        public int ActiveSpyCount => _spies.Count(s => !s.Value.IsKilled && !s.Value.IsCaptured);

        /// <summary>Gets the number of currently installed spy networks across all nations.</summary>
        public int ActiveNetworkCount => _installedNetworks.Count;

        // --------------------------------------------------------------------
        // UNITY LIFECYCLE
        // --------------------------------------------------------------------

        /// <summary>
        /// Initializes the espionage system on startup.
        /// </summary>
        private void Awake()
        {
            Debug.Log("[EspionageManager] System initialized. Awaiting covert operations directives.");
        }

        /// <summary>
        /// Called each game turn to advance all active missions and refresh intel.
        /// Should be invoked from the turn manager's turn-processing pipeline.
        /// </summary>
        /// <param name="turnNumber">The current game turn number.</param>
        public void OnTurnStarted(int turnNumber)
        {
            _currentTurn = turnNumber;
            UpdateMissions();
            UpdateIntelForAllNations();
            ExpireTemporaryIntel();
            DecayCounterIntelBoosts();
        }

        // ====================================================================
        // SPY RECRUITMENT
        // ====================================================================

        /// <summary>
        /// Recruits a new spy for the specified nation. The spy begins at the nation's
        /// capital (or a default position) and is immediately available for assignment.
        /// Deducts <see cref="recruitCost"/> from the nation's treasury.
        /// </summary>
        /// <param name="ownerNation">The nation recruiting the spy.</param>
        /// <returns>The newly created Spy, or null if recruitment failed.</returns>
        public Spy RecruitSpy(string ownerNation)
        {
            if (string.IsNullOrEmpty(ownerNation))
            {
                Debug.LogWarning("[EspionageManager] Cannot recruit spy: owner nation is null or empty.");
                return null;
            }

            if (!CanAffordRecruitment(ownerNation))
            {
                Debug.LogWarning($"[EspionageManager] {ownerNation} cannot afford spy recruitment (cost: {recruitCost}).");
                return null;
            }

            DeductTreasury(ownerNation, recruitCost);

            _spyIdCounter++;
            string spyId = $"SPY_{ownerNation}_{_spyIdCounter:D4}";
            HexCoord startPosition = GetNationCapitalHex(ownerNation);

            Spy newSpy = new Spy(spyId, ownerNation, startPosition) { IsHidden = true };

            // Randomize stats with variance based on hypothetical nation espionage investment
            int espionageBonus = GetNationEspionageInvestment(ownerNation);
            newSpy.Skill = Mathf.Clamp(UnityEngine.Random.Range(25, 65) + espionageBonus, 1, 100);
            newSpy.Stealth = Mathf.Clamp(UnityEngine.Random.Range(25, 65) + espionageBonus, 1, 100);
            newSpy.Loyalty = Mathf.Clamp(UnityEngine.Random.Range(45, 85), 1, 100);

            _spies[spyId] = newSpy;

            Debug.Log($"[EspionageManager] {ownerNation} recruited {spyId} " +
                       $"(Skill:{newSpy.Skill} Stealth:{newSpy.Stealth} Loyalty:{newSpy.Loyalty}).");
            return newSpy;
        }

        // ====================================================================
        // MISSION ASSIGNMENT
        // ====================================================================

        /// <summary>
        /// Assigns a mission to a spy targeting a specific nation at a specific hex location.
        /// The spy must be available (not captured, killed, or on another mission).
        /// </summary>
        /// <param name="spyId">The ID of the spy to assign.</param>
        /// <param name="mission">The type of mission to execute.</param>
        /// <param name="targetNation">The nation being targeted by the mission.</param>
        /// <param name="targetHex">The hex location for the mission.</param>
        /// <returns>True if the mission was successfully assigned; false otherwise.</returns>
        public bool AssignMission(string spyId, SpyMission mission, string targetNation, HexCoord targetHex)
        {
            if (!_spies.TryGetValue(spyId, out Spy spy))
            {
                Debug.LogWarning($"[EspionageManager] Cannot assign mission: spy '{spyId}' not found.");
                return false;
            }

            if (!spy.IsAvailable())
            {
                Debug.LogWarning($"[EspionageManager] Spy '{spyId}' is not available. " +
                               $"Captured={spy.IsCaptured}, Killed={spy.IsKilled}, TurnsLeft={spy.TurnsRemaining}.");
                return false;
            }

            if (string.IsNullOrEmpty(targetNation))
            {
                Debug.LogWarning("[EspionageManager] Cannot assign mission: target nation is null or empty.");
                return false;
            }

            // Spies cannot target their own nation (except CounterIntelligence)
            if (mission != SpyMission.CounterIntelligence && spy.OwnerNation == targetNation)
            {
                Debug.LogWarning($"[EspionageManager] Cannot assign {mission}: spy cannot target own nation.");
                return false;
            }

            // Validate bribe cost upfront
            if (mission == SpyMission.Bribe && !CanAffordRecruitment(spy.OwnerNation))
            {
                Debug.LogWarning($"[EspionageManager] {spy.OwnerNation} cannot afford bribe (cost: {bribeCost}).");
                return false;
            }

            spy.CurrentMission = mission;
            spy.AssignedNation = targetNation;
            spy.Position = targetHex;
            spy.TurnsRemaining = GetMissionDuration(mission);
            spy.IsHidden = true;

            if (mission == SpyMission.Bribe)
            {
                DeductTreasury(spy.OwnerNation, bribeCost);
            }

            Debug.Log($"[EspionageManager] Assigned {mission} to {spyId} targeting {targetNation}. " +
                       $"Duration: {spy.TurnsRemaining} turns.");
            return true;
        }

        // ====================================================================
        // MISSION PROCESSING
        // ====================================================================

        /// <summary>
        /// Processes a completed mission for the specified spy, rolling success/failure
        /// and generating a detailed <see cref="MissionResult"/>. Called internally when
        /// <see cref="Spy.TurnsRemaining"/> reaches zero during <see cref="UpdateMissions"/>.
        /// </summary>
        /// <param name="spyId">The ID of the spy whose mission is being processed.</param>
        /// <returns>A MissionResult containing all outcome data.</returns>
        public MissionResult ProcessMission(string spyId)
        {
            MissionResult result = new MissionResult { SpyId = spyId };

            if (!_spies.TryGetValue(spyId, out Spy spy))
            {
                result.Details = $"Spy '{spyId}' not found. Mission aborted.";
                return result;
            }

            result.MissionType = spy.CurrentMission.ToString();
            result.TargetNation = spy.AssignedNation;

            // Calculate and roll success
            float successChance = CalculateMissionSuccessChance(spy, spy.CurrentMission, spy.AssignedNation);
            bool missionSuccess = UnityEngine.Random.value < successChance;
            result.Success = missionSuccess;

            // Roll for detection/capture regardless of success
            float detectionChance = CalculateDetectionChance(spy);
            bool detected = UnityEngine.Random.value < detectionChance;

            if (detected)
            {
                float captureChance = 0.5f + (GetNationIntel(spy.AssignedNation).CounterEspionageLevel * 0.3f);
                if (UnityEngine.Random.value < captureChance)
                {
                    CaptureSpy(spyId);
                    result.SpyCaptured = true;
                }
                else
                {
                    result.IntelGained *= 0.5f;
                    result.Details += " The spy was detected but managed to escape. ";
                }
            }

            // Dispatch to mission-specific handler
            switch (spy.CurrentMission)
            {
                case SpyMission.Reconnaissance:
                    ProcessReconnaissance(spy, result, missionSuccess);
                    break;
                case SpyMission.Sabotage:
                    ProcessSabotage(spy, result, missionSuccess);
                    break;
                case SpyMission.Assassination:
                    ProcessAssassination(spy, result, missionSuccess);
                    break;
                case SpyMission.TechTheft:
                    ProcessTechTheft(spy, result, missionSuccess);
                    break;
                case SpyMission.CounterIntelligence:
                    ProcessCounterIntelligence(spy, result, missionSuccess);
                    break;
                case SpyMission.InstallNetwork:
                    ProcessInstallNetwork(spy, result, missionSuccess);
                    break;
                case SpyMission.FalseFlag:
                    ProcessFalseFlag(spy, result, missionSuccess);
                    break;
                case SpyMission.Bribe:
                    ProcessBribe(spy, result, missionSuccess);
                    break;
            }

            // Reset spy if still alive and free
            if (!result.SpyCaptured && !result.SpyKilled)
            {
                spy.TurnsRemaining = 0;
                spy.IsHidden = true;

                // Skill improvement from field experience
                if (missionSuccess)
                {
                    spy.Skill = Mathf.Min(100, spy.Skill + UnityEngine.Random.Range(1, 4));
                    spy.Stealth = Mathf.Min(100, spy.Stealth + UnityEngine.Random.Range(0, 2));
                }
            }

            Debug.Log($"[EspionageManager] Mission {spy.CurrentMission} by {spyId}: " +
                       $"{(missionSuccess ? "SUCCESS" : "FAILURE")}. {(result.SpyCaptured ? "SPY CAPTURED!" : "")}");
            OnMissionCompleted?.Invoke(result);
            return result;
        }

        // --------------------------------------------------------------------
        // MISSION-SPECIFIC PROCESSORS
        // --------------------------------------------------------------------

        /// <summary>
        /// Reconnaissance: reveal units in a radius around the spy's position.
        /// On success, reveals all units within <see cref="reconVisionRadius"/> hexes
        /// for <see cref="reconIntelDuration"/> turns and upgrades military intel.
        /// </summary>
        private void ProcessReconnaissance(Spy spy, MissionResult result, bool success)
        {
            if (success)
            {
                result.Details = $"Reconnaissance successful near {spy.Position}. " +
                               $"Enemy units detected within radius {reconVisionRadius}.";
                result.IntelGained = 0.4f + (spy.Skill / 100f * 0.4f);

                // Register temporary intel visibility with expiry
                string intelKey = $"{spy.AssignedNation}_{spy.Position.Q}_{spy.Position.R}";
                _tempIntelExpiry[intelKey] = _currentTurn + reconIntelDuration;

                // Upgrade military intel level
                NationIntel intel = GetOrCreateNationIntel(spy.AssignedNation);
                if (intel.MilitaryIntel < IntelLevel.High)
                {
                    intel.MilitaryIntel = EnumExtension.Increment(intel.MilitaryIntel);
                    OnIntelChanged?.Invoke(spy.AssignedNation, "Military", intel.MilitaryIntel);
                }

                // Simulate discovering unit positions
                int unitsFound = UnityEngine.Random.Range(2, 8);
                for (int i = 0; i < unitsFound; i++)
                {
                    int offsetQ = UnityEngine.Random.Range(-reconVisionRadius, reconVisionRadius + 1);
                    int offsetR = UnityEngine.Random.Range(-reconVisionRadius, reconVisionRadius + 1);
                    string unitKey = $"UNIT_{spy.AssignedNation}_{spy.Position.Q + offsetQ}_{spy.Position.R + offsetR}";
                    float confidence = 0.5f + (spy.Skill / 100f * 0.5f);
                    intel.KnownUnitPositions[unitKey] = confidence;
                    result.Discoveries.Add($"Unit detected at offset ({offsetQ}, {offsetR}) " +
                                          $"with {confidence:P0} confidence.");
                }

                result.Details += $" {unitsFound} unit positions logged. Intel expires in {reconIntelDuration} turns.";
            }
            else
            {
                result.Details = "Reconnaissance failed. The spy could not gather useful intelligence.";
                result.IntelGained = UnityEngine.Random.Range(0.05f, 0.15f);
            }
        }

        /// <summary>
        /// Sabotage: damage city production and potentially destroy buildings.
        /// On success, applies 30-60% production reduction for 3 turns with a
        /// chance to destroy a random building in the target city.
        /// </summary>
        private void ProcessSabotage(Spy spy, MissionResult result, bool success)
        {
            if (success)
            {
                float productionReduction = UnityEngine.Random.Range(0.3f, 0.6f);
                const int sabotageEffectDuration = 3;
                result.Details = $"Sabotage successful! Target production reduced by " +
                               $"{productionReduction:P0} for {sabotageEffectDuration} turns.";

                string cityId = GetCityAtHex(spy.Position);
                if (!string.IsNullOrEmpty(cityId))
                {
                    ApplyProductionPenalty(cityId, productionReduction, sabotageEffectDuration);
                    result.Discoveries.Add($"City {cityId} production sabotaged.");
                }

                // Chance to destroy a building (30% base + skill bonus)
                float buildingDestroyChance = 0.3f + (spy.Skill / 100f * 0.2f);
                if (UnityEngine.Random.value < buildingDestroyChance)
                {
                    string buildingName = GetRandomBuildingAtHex(spy.Position);
                    if (!string.IsNullOrEmpty(buildingName))
                    {
                        DestroyBuilding(spy.Position, buildingName);
                        result.Details += $" Critical hit: {buildingName} was destroyed!";
                        result.Discoveries.Add($"{buildingName} destroyed.");
                    }
                }

                result.IntelGained = 0.3f;

                NationIntel intel = GetOrCreateNationIntel(spy.AssignedNation);
                if (intel.EconomicIntel < IntelLevel.Medium)
                {
                    intel.EconomicIntel = EnumExtension.Increment(intel.EconomicIntel);
                    OnIntelChanged?.Invoke(spy.AssignedNation, "Economic", intel.EconomicIntel);
                }
            }
            else
            {
                result.Details = "Sabotage attempt failed. The spy could not reach the target infrastructure.";
                result.IntelGained = 0.05f;
            }
        }

        /// <summary>
        /// Assassination: attempt to eliminate the target nation's leader.
        /// Extremely high risk. On success, applies severe stability and morale penalties
        /// and triggers a succession crisis. On failure, the spy is likely captured.
        /// </summary>
        private void ProcessAssassination(Spy spy, MissionResult result, bool success)
        {
            if (success)
            {
                float stabilityLoss = UnityEngine.Random.Range(20f, 40f);
                float moraleLoss = UnityEngine.Random.Range(15f, 30f);
                result.Details = $"Assassination successful! Enemy leader eliminated. " +
                               $"Stability -{stabilityLoss:F0}, Morale -{moraleLoss:F0}.";

                ApplyStabilityPenalty(spy.AssignedNation, -stabilityLoss);
                ApplyMoralePenalty(spy.AssignedNation, -moraleLoss);

                result.Discoveries.Add($"Leader of {spy.AssignedNation} assassinated.");
                result.Discoveries.Add("Succession crisis triggered for 3 turns.");
                result.IntelGained = 0.5f;
            }
            else
            {
                result.Details = "Assassination attempt failed. The target survived; security heightened.";
                result.IntelGained = 0.1f;

                // High risk of capture on assassination failure
                result.SpyCaptured = result.SpyCaptured || (UnityEngine.Random.value < 0.4f);
                if (result.SpyCaptured)
                {
                    result.Details += " The assassin was captured and identified!";
                }

                // Target boosts counter-intelligence after assassination attempt
                NationIntel intel = GetOrCreateNationIntel(spy.AssignedNation);
                intel.CounterEspionageLevel = Mathf.Min(1f, intel.CounterEspionageLevel + 0.1f);
            }
        }

        /// <summary>
        /// TechTheft: attempt to steal technology from the target nation.
        /// Uses a specialized success calculation (10-30% base, modified by spy skill
        /// vs target counter-intelligence) separate from the general mission roll.
        /// </summary>
        private void ProcessTechTheft(Spy spy, MissionResult result, bool success)
        {
            NationIntel targetIntel = GetOrCreateNationIntel(spy.AssignedNation);

            // Specialized tech theft probability
            float techBaseChance = 0.1f + (spy.Skill / 100f * 0.2f);
            float counterIntelPenalty = targetIntel.CounterEspionageLevel * 0.3f;
            float adjustedChance = Mathf.Clamp01(techBaseChance - counterIntelPenalty);
            bool techStolen = success && (UnityEngine.Random.value < adjustedChance);

            if (techStolen)
            {
                string stolenTech = GetRandomStolenTech(spy.OwnerNation, spy.AssignedNation);
                if (!string.IsNullOrEmpty(stolenTech))
                {
                    result.StolenTechId = stolenTech;
                    GrantTechnology(spy.OwnerNation, stolenTech);
                    result.Details = $"Tech theft successful! Stole technology: {stolenTech}.";
                    result.Discoveries.Add($"Acquired tech: {stolenTech}.");
                    result.IntelGained = 0.7f;

                    if (targetIntel.TechIntel < IntelLevel.High)
                    {
                        targetIntel.TechIntel = EnumExtension.Increment(targetIntel.TechIntel);
                        OnIntelChanged?.Invoke(spy.AssignedNation, "Tech", targetIntel.TechIntel);
                    }
                }
                else
                {
                    result.Details = "Infiltration successful but no stealable technology found.";
                    result.IntelGained = 0.3f;
                }
            }
            else
            {
                result.Details = "Tech theft attempt failed. Security protocols prevented data extraction.";
                result.IntelGained = UnityEngine.Random.Range(0.05f, 0.15f);

                // Partial intel gained even on failure: catalog visible techs
                List<string> knownTechs = GetTargetNationTechs(spy.AssignedNation);
                foreach (string tech in knownTechs)
                {
                    if (!targetIntel.DiscoveredTechs.Contains(tech))
                    {
                        targetIntel.DiscoveredTechs.Add(tech);
                    }
                }
            }
        }

        /// <summary>
        /// CounterIntelligence: bolster national counter-espionage and hunt enemy spies.
        /// On success, significantly boosts counter-espionage level and may eliminate
        /// enemy operatives currently operating within the nation.
        /// </summary>
        private void ProcessCounterIntelligence(Spy spy, MissionResult result, bool success)
        {
            float boostAmount = 0.15f + (spy.Skill / 100f * 0.2f);
            const int boostDuration = 5;

            if (success)
            {
                NationIntel ownIntel = GetOrCreateNationIntel(spy.OwnerNation);
                ownIntel.CounterEspionageLevel = Mathf.Min(1f, ownIntel.CounterEspionageLevel + boostAmount);
                _counterIntelBoosts[spy.OwnerNation] = boostDuration;

                result.Details = $"Counter-intelligence successful. Counter-espionage +" +
                               $"{boostAmount:P0} for {boostDuration} turns.";

                int enemySpiesEliminated = HuntEnemySpies(spy.OwnerNation);
                if (enemySpiesEliminated > 0)
                {
                    result.Details += $" {enemySpiesEliminated} enemy operative(s) eliminated!";
                    result.Discoveries.Add($"Eliminated {enemySpiesEliminated} enemy operative(s).");
                }

                result.IntelGained = 0.2f;
            }
            else
            {
                result.Details = "Counter-intelligence sweep found no active threats. Security reviewed.";
                result.IntelGained = 0.1f;

                // Small consolation boost even on failure
                NationIntel ownIntel = GetOrCreateNationIntel(spy.OwnerNation);
                ownIntel.CounterEspionageLevel = Mathf.Min(1f, ownIntel.CounterEspionageLevel + 0.05f);
            }
        }

        /// <summary>
        /// InstallNetwork: establish a permanent covert intelligence-gathering network.
        /// On success, the owning nation receives low-level ongoing intelligence about
        /// the target nation every turn without needing to send additional spies.
        /// </summary>
        private void ProcessInstallNetwork(Spy spy, MissionResult result, bool success)
        {
            if (success)
            {
                if (!_installedNetworks.ContainsKey(spy.AssignedNation))
                {
                    _installedNetworks[spy.AssignedNation] = new List<string>();
                }
                _installedNetworks[spy.AssignedNation].Add(spy.OwnerNation);

                result.Details = $"Covert network installed in {spy.AssignedNation}. " +
                               "Low-level intelligence will be gathered each turn.";
                result.IntelGained = 0.2f;
                result.Discoveries.Add($"Network established in {spy.AssignedNation} by {spy.OwnerNation}.");

                NationIntel intel = GetOrCreateNationIntel(spy.AssignedNation);
                if (intel.DiplomaticIntel < IntelLevel.Low)
                {
                    intel.DiplomaticIntel = IntelLevel.Low;
                    OnIntelChanged?.Invoke(spy.AssignedNation, "Diplomatic", intel.DiplomaticIntel);
                }
            }
            else
            {
                result.Details = "Network installation failed. Local authorities detected the operation.";
                result.IntelGained = 0.05f;
                result.SpyCaptured = result.SpyCaptured || (UnityEngine.Random.value < 0.3f);
            }
        }

        /// <summary>
        /// FalseFlag: frame a third nation for a hostile act against the target.
        /// On success, the target nation's aggression toward the framed nation
        /// increases significantly (+50), potentially triggering conflict.
        /// </summary>
        private void ProcessFalseFlag(Spy spy, MissionResult result, bool success)
        {
            if (success)
            {
                string framedNation = GetRandomThirdNation(spy.OwnerNation, spy.AssignedNation);
                if (!string.IsNullOrEmpty(framedNation))
                {
                    const float aggressionIncrease = 50f;
                    ModifyAggression(spy.AssignedNation, framedNation, aggressionIncrease);

                    result.Details = $"False-flag successful! {spy.AssignedNation} believes " +
                                   $"{framedNation} conducted a hostile operation. Aggression +{aggressionIncrease:F0}.";
                    result.IntelGained = 0.3f;
                    result.Discoveries.Add($"Framed {framedNation} against {spy.AssignedNation}.");
                }
                else
                {
                    result.Details = "False-flag prepared but no suitable third party found to frame.";
                    result.IntelGained = 0.1f;
                    result.Success = false;
                }
            }
            else
            {
                result.Details = "False-flag operation failed. The deception was uncovered before it took effect.";
                result.IntelGained = 0.05f;
            }
        }

        /// <summary>
        /// Bribe: attempt to convert an enemy unit to the spy's owner's side.
        /// Costs treasury upfront. Base conversion chance is 30%, modified by spy skill.
        /// </summary>
        private void ProcessBribe(Spy spy, MissionResult result, bool success)
        {
            float bribeSuccessChance = 0.3f + (spy.Skill / 100f * 0.15f);
            bool unitConverted = success && (UnityEngine.Random.value < bribeSuccessChance);

            if (unitConverted)
            {
                string unitId = GetUnitAtHex(spy.Position, spy.AssignedNation);
                if (!string.IsNullOrEmpty(unitId))
                {
                    ConvertUnitToSide(unitId, spy.OwnerNation);
                    result.Details = $"Bribe successful! Enemy unit '{unitId}' at {spy.Position} " +
                                   $"defected to {spy.OwnerNation}.";
                    result.Discoveries.Add($"Unit {unitId} defected.");
                    result.IntelGained = 0.4f;
                }
                else
                {
                    result.Details = "Bribe delivered but no enemy unit found at target location. Funds lost.";
                    result.IntelGained = 0.05f;
                    result.Success = false;
                }
            }
            else
            {
                result.Details = "Bribe attempt failed. The enemy unit refused and reported the approach.";
                result.IntelGained = 0.05f;
                result.SpyCaptured = result.SpyCaptured || (UnityEngine.Random.value < 0.25f);

                NationIntel intel = GetOrCreateNationIntel(spy.AssignedNation);
                intel.CounterEspionageLevel = Mathf.Min(1f, intel.CounterEspionageLevel + 0.05f);
            }
        }

        // ====================================================================
        // TURN-BASED UPDATES
        // ====================================================================

        /// <summary>
        /// Advances all active spy missions by one turn. Missions reaching zero
        /// <see cref="Spy.TurnsRemaining"/> are automatically processed via
        /// <see cref="ProcessMission"/>.
        /// </summary>
        public void UpdateMissions()
        {
            List<string> completedSpies = new List<string>();

            foreach (var kvp in _spies)
            {
                Spy spy = kvp.Value;
                if (spy.IsKilled || spy.IsCaptured || spy.TurnsRemaining <= 0)
                    continue;

                spy.TurnsRemaining--;
                if (spy.TurnsRemaining <= 0)
                {
                    completedSpies.Add(spy.SpyId);
                }
            }

            foreach (string spyId in completedSpies)
            {
                ProcessMission(spyId);
            }
        }

        // ====================================================================
        // SUCCESS CHANCE CALCULATION
        // ====================================================================

        /// <summary>
        /// Calculates the probability of mission success based on spy attributes,
        /// target nation counter-intelligence, mission difficulty, and network bonuses.
        /// </summary>
        /// <param name="spy">The spy attempting the mission.</param>
        /// <param name="mission">The type of mission being attempted.</param>
        /// <param name="targetNation">The nation being targeted.</param>
        /// <returns>A float between 0.0 and 1.0 representing success probability.</returns>
        public float CalculateMissionSuccessChance(Spy spy, SpyMission mission, string targetNation)
        {
            // Base effectiveness from weighted spy stats
            float effectiveness = spy.CalculateEffectiveness() / 100f;

            // Mission inherent difficulty
            float missionDifficulty = GetMissionDifficultyModifier(mission);

            // Target counter-intelligence penalty
            NationIntel targetIntel = GetOrCreateNationIntel(targetNation);
            float counterIntelPenalty = targetIntel.CounterEspionageLevel * 0.4f;

            // Active counter-intelligence boost in target nation
            if (_counterIntelBoosts.ContainsKey(targetNation))
            {
                counterIntelPenalty += 0.15f;
            }

            // Installed network provides operational advantage
            float networkBonus = 0f;
            if (_installedNetworks.TryGetValue(targetNation, out List<string> networks)
                && networks.Contains(spy.OwnerNation))
            {
                networkBonus = 0.1f;
            }

            float rawChance = effectiveness * (1f - missionDifficulty) - counterIntelPenalty + networkBonus;
            float finalChance = Mathf.Clamp01(rawChance);

            Debug.Log($"[EspionageManager] Success chance [{mission}]: {finalChance:P1} " +
                       $"(Eff:{effectiveness:P1} Diff:{missionDifficulty:P1} " +
                       $"CI:{counterIntelPenalty:P1} Net:{networkBonus:P1})");

            return finalChance;
        }

        /// <summary>
        /// Returns a difficulty modifier for each mission type.
        /// Higher values indicate harder missions (lower base success rate).
        /// </summary>
        private float GetMissionDifficultyModifier(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.Reconnaissance:   return 0.15f;
                case SpyMission.CounterIntelligence: return 0.10f;
                case SpyMission.InstallNetwork:    return 0.30f;
                case SpyMission.FalseFlag:         return 0.35f;
                case SpyMission.Sabotage:          return 0.35f;
                case SpyMission.Bribe:             return 0.40f;
                case SpyMission.TechTheft:         return 0.50f;
                case SpyMission.Assassination:     return 0.60f;
                default:                           return 0.30f;
            }
        }

        /// <summary>
        /// Calculates the probability that a spy is detected during their mission.
        /// </summary>
        private float CalculateDetectionChance(Spy spy)
        {
            NationIntel targetIntel = GetOrCreateNationIntel(spy.AssignedNation);
            float stealthDefense = spy.Stealth / 100f;
            float counterIntelStrength = targetIntel.CounterEspionageLevel;
            float missionRisk = GetMissionDifficultyModifier(spy.CurrentMission);

            float detection = baseDetectionChance
                            + (counterIntelStrength * 0.3f)
                            + (missionRisk * 0.1f)
                            - (stealthDefense * 0.25f);

            if (spy.IsHidden)
            {
                detection -= 0.1f;
            }

            return Mathf.Clamp01(detection);
        }

        // ====================================================================
        // SPY CAPTURE AND KILL
        // ====================================================================

        /// <summary>
        /// Captures a spy, marking them as a prisoner of war and triggering a diplomatic incident.
        /// The owning nation's relationship with the capturing nation will deteriorate.
        /// Fires <see cref="OnSpyCaptured"/>.
        /// </summary>
        /// <param name="spyId">The ID of the spy to capture.</param>
        public void CaptureSpy(string spyId)
        {
            if (!_spies.TryGetValue(spyId, out Spy spy))
            {
                Debug.LogWarning($"[EspionageManager] Cannot capture spy: '{spyId}' not found.");
                return;
            }

            if (spy.IsCaptured || spy.IsKilled)
            {
                Debug.LogWarning($"[EspionageManager] Spy '{spyId}' is already captured or killed.");
                return;
            }

            spy.IsCaptured = true;
            spy.IsHidden = false;
            spy.TurnsRemaining = 0;

            TriggerDiplomaticIncident(spy.OwnerNation, spy.AssignedNation, "spy_capture");

            Debug.LogWarning($"[EspionageManager] SPY CAPTURED: {spyId} ({spy.OwnerNation}) " +
                           $"by {spy.AssignedNation}! Diplomatic incident triggered.");
            OnSpyCaptured?.Invoke(spyId, spy.AssignedNation);
        }

        /// <summary>
        /// Kills a spy, permanently removing them from the game.
        /// Does not trigger a diplomatic incident (plausible deniability).
        /// Fires <see cref="OnSpyKilled"/>.
        /// </summary>
        /// <param name="spyId">The ID of the spy to kill.</param>
        public void KillSpy(string spyId)
        {
            if (!_spies.TryGetValue(spyId, out Spy spy))
            {
                Debug.LogWarning($"[EspionageManager] Cannot kill spy: '{spyId}' not found.");
                return;
            }

            if (spy.IsKilled)
            {
                Debug.LogWarning($"[EspionageManager] Spy '{spyId}' is already dead.");
                return;
            }

            spy.IsKilled = true;
            spy.IsHidden = false;
            spy.TurnsRemaining = 0;
            RemoveSpyFromNetworks(spyId);

            Debug.LogWarning($"[EspionageManager] SPY KILLED: {spyId} ({spy.OwnerNation}) eliminated.");
            OnSpyKilled?.Invoke(spyId, "Unknown");
        }

        // ====================================================================
        // INTEL MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Refreshes intelligence levels for a specific nation based on all available sources:
        /// installed networks, reconnaissance data, and passive counter-intel growth.
        /// Called automatically each turn for all tracked nations.
        /// </summary>
        /// <param name="nationId">The nation whose intel profile to update.</param>
        public void UpdateIntel(string nationId)
        {
            NationIntel intel = GetOrCreateNationIntel(nationId);

            // Passive counter-intel growth
            intel.CounterEspionageLevel = Mathf.Min(1f, intel.CounterEspionageLevel + passiveCounterIntelGrowth);

            // Installed networks provide passive intel ticks
            if (_installedNetworks.TryGetValue(nationId, out List<string> networkOwners))
            {
                foreach (string owner in networkOwners)
                {
                    // Each network owner gets gradual intel improvements
                    if (UnityEngine.Random.value < 0.10f && intel.MilitaryIntel < IntelLevel.Medium)
                        intel.MilitaryIntel = EnumExtension.Increment(intel.MilitaryIntel);
                    if (UnityEngine.Random.value < 0.08f && intel.EconomicIntel < IntelLevel.Medium)
                        intel.EconomicIntel = EnumExtension.Increment(intel.EconomicIntel);
                    if (UnityEngine.Random.value < 0.06f && intel.TechIntel < IntelLevel.Low)
                        intel.TechIntel = EnumExtension.Increment(intel.TechIntel);
                    if (UnityEngine.Random.value < 0.05f && intel.DiplomaticIntel < IntelLevel.Low)
                        intel.DiplomaticIntel = EnumExtension.Increment(intel.DiplomaticIntel);
                }
            }
        }

        /// <summary>
        /// Retrieves the intelligence profile for a specific nation.
        /// Creates a default profile if none exists yet.
        /// </summary>
        /// <param name="nationId">The nation to get intel for.</param>
        /// <returns>The <see cref="NationIntel"/> profile for the specified nation.</returns>
        public NationIntel GetIntelForNation(string nationId)
        {
            return GetOrCreateNationIntel(nationId);
        }

        /// <summary>
        /// Returns all spies in the game, organized by their owner nation.
        /// Dead and captured spies are included for record-keeping purposes.
        /// </summary>
        /// <returns>Dictionary mapping nation IDs to lists of their spies.</returns>
        public Dictionary<string, List<Spy>> GetAllSpies()
        {
            var result = new Dictionary<string, List<Spy>>();
            foreach (var kvp in _spies)
            {
                Spy spy = kvp.Value;
                if (!result.ContainsKey(spy.OwnerNation))
                    result[spy.OwnerNation] = new List<Spy>();
                result[spy.OwnerNation].Add(spy);
            }
            return result;
        }

        // ====================================================================
        // PRIVATE HELPERS
        // ====================================================================

        private int GetMissionDuration(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.Reconnaissance:    return reconDuration;
                case SpyMission.Sabotage:           return sabotageDuration;
                case SpyMission.Assassination:       return assassinationDuration;
                case SpyMission.TechTheft:           return techTheftDuration;
                case SpyMission.CounterIntelligence: return counterIntelDuration;
                case SpyMission.InstallNetwork:      return installNetworkDuration;
                case SpyMission.FalseFlag:           return falseFlagDuration;
                case SpyMission.Bribe:              return bribeDuration;
                default:                             return 2;
            }
        }

        private NationIntel GetOrCreateNationIntel(string nationId)
        {
            if (!_nationIntels.TryGetValue(nationId, out NationIntel intel))
            {
                intel = new NationIntel(nationId);
                _nationIntels[nationId] = intel;
            }
            return intel;
        }

        private void UpdateIntelForAllNations()
        {
            foreach (string nationId in _nationIntels.Keys.ToList())
            {
                UpdateIntel(nationId);
            }
        }

        private void ExpireTemporaryIntel()
        {
            List<string> expiredKeys = new List<string>();
            foreach (var kvp in _tempIntelExpiry)
            {
                if (kvp.Value <= _currentTurn)
                    expiredKeys.Add(kvp.Key);
            }
            foreach (string key in expiredKeys)
            {
                _tempIntelExpiry.Remove(key);
                Debug.Log($"[EspionageManager] Temporary intel expired: {key}");
            }
        }

        private void DecayCounterIntelBoosts()
        {
            List<string> expiredBoosts = new List<string>();
            foreach (var kvp in _counterIntelBoosts)
            {
                _counterIntelBoosts[kvp.Key] = kvp.Value - 1;
                if (_counterIntelBoosts[kvp.Key] <= 0)
                    expiredBoosts.Add(kvp.Key);
            }
            foreach (string nationId in expiredBoosts)
            {
                _counterIntelBoosts.Remove(nationId);
                Debug.Log($"[EspionageManager] Counter-intel boost expired for {nationId}.");
            }
        }

        /// <summary>
        /// Hunts enemy spies operating within a defending nation.
        /// Uses counter-espionage level to determine detection probability.
        /// </summary>
        /// <returns>The number of enemy spies eliminated (killed or captured).</returns>
        private int HuntEnemySpies(string defendingNation)
        {
            int eliminated = 0;
            NationIntel defenderIntel = GetOrCreateNationIntel(defendingNation);
            float detectionPower = defenderIntel.CounterEspionageLevel;

            List<string> enemySpyIds = _spies
                .Where(kvp => kvp.Value.AssignedNation == defendingNation
                           && kvp.Value.OwnerNation != defendingNation
                           && !kvp.Value.IsKilled && !kvp.Value.IsCaptured)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (string enemySpyId in enemySpyIds)
            {
                if (_spies.TryGetValue(enemySpyId, out Spy enemySpy))
                {
                    float catchChance = detectionPower * 0.3f;
                    if (UnityEngine.Random.value < catchChance)
                    {
                        if (UnityEngine.Random.value < 0.5f)
                            KillSpy(enemySpyId);
                        else
                            CaptureSpy(enemySpyId);
                        eliminated++;
                    }
                }
            }

            return eliminated;
        }

        private void RemoveSpyFromNetworks(string spyId)
        {
            foreach (var kvp in _installedNetworks)
            {
                kvp.Value.Remove(spyId);
            }
        }

        // ====================================================================
        // EXTERNAL SYSTEM INTEGRATION STUBS
        // These methods serve as integration points for connecting to other
        // game systems (GameManager, EconomyManager, etc.).
        // Replace implementations with actual system references in production.
        // ====================================================================

        private bool CanAffordRecruitment(string nationId)
        {
            // TODO: Hook into EconomyManager.GetTreasury(nationId) >= recruitCost
            return true;
        }

        private void DeductTreasury(string nationId, int amount)
        {
            // TODO: Hook into EconomyManager.DeductTreasury(nationId, amount)
            Debug.Log($"[EspionageManager] Economy: Deducted {amount} from {nationId} treasury.");
        }

        private HexCoord GetNationCapitalHex(string nationId)
        {
            // TODO: Hook into NationManager.GetCapital(nationId)
            return new HexCoord(0, 0);
        }

        private int GetNationEspionageInvestment(string nationId)
        {
            // TODO: Hook into PolicyManager or EconomyManager for espionage budget
            return 0;
        }

        private string GetCityAtHex(HexCoord coord)
        {
            // TODO: Hook into CityManager.GetCityAt(coord)
            return $"City_{coord.Q}_{coord.R}";
        }

        private void ApplyProductionPenalty(string cityId, float reduction, int duration)
        {
            // TODO: Hook into CityManager.ApplyProductionModifier(cityId, reduction, duration)
            Debug.Log($"[EspionageManager] City: {cityId} production -{reduction:P0} for {duration} turns.");
        }

        private string GetRandomBuildingAtHex(HexCoord coord)
        {
            // TODO: Hook into CityManager.GetBuildings(coord)
            string[] buildings = { "Factory", "Barracks", "Research Lab", "Market", "Dockyard" };
            return buildings[UnityEngine.Random.Range(0, buildings.Length)];
        }

        private void DestroyBuilding(HexCoord coord, string buildingName)
        {
            // TODO: Hook into CityManager.DestroyBuilding(coord, buildingName)
            Debug.Log($"[EspionageManager] Building: Destroyed {buildingName} at {coord}.");
        }

        private void ApplyStabilityPenalty(string nationId, float amount)
        {
            // TODO: Hook into NationManager.ModifyStability(nationId, amount)
            Debug.Log($"[EspionageManager] Nation: {nationId} stability {amount:F0}.");
        }

        private void ApplyMoralePenalty(string nationId, float amount)
        {
            // TODO: Hook into NationManager.ModifyMorale(nationId, amount)
            Debug.Log($"[EspionageManager] Nation: {nationId} morale {amount:F0}.");
        }

        private string GetRandomStolenTech(string ownerNation, string targetNation)
        {
            // TODO: Hook into TechManager.GetStealableTechs(ownerNation, targetNation)
            string[] techs = { "Advanced Metallurgy", "Rocketry", "Cryptography", "Radar", "Jet Engines" };
            return techs[UnityEngine.Random.Range(0, techs.Length)];
        }

        private void GrantTechnology(string nationId, string techId)
        {
            // TODO: Hook into TechManager.GrantTech(nationId, techId)
            Debug.Log($"[EspionageManager] Tech: Granted '{techId}' to {nationId}.");
        }

        private List<string> GetTargetNationTechs(string nationId)
        {
            // TODO: Hook into TechManager.GetResearchedTechs(nationId)
            return new List<string> { "Basic Infantry", "Early Firearms" };
        }

        private string GetRandomThirdNation(string ownerNation, string targetNation)
        {
            // TODO: Hook into NationManager.GetAllNationIds() and filter
            string[] allNations = { "Nation_A", "Nation_B", "Nation_C", "Nation_D" };
            var candidates = allNations.Where(n => n != ownerNation && n != targetNation).ToList();
            return candidates.Count > 0
                ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
                : null;
        }

        private void ModifyAggression(string nation1, string nation2, float amount)
        {
            // TODO: Hook into DiplomacyManager.ModifyRelation(nation1, nation2, -amount)
            Debug.Log($"[EspionageManager] Diplomacy: {nation1} aggression toward {nation2} +{amount:F0}.");
        }

        private string GetUnitAtHex(HexCoord coord, string nationId)
        {
            // TODO: Hook into MilitaryManager.GetUnitAt(coord, nationId)
            return $"Unit_{nationId}_{coord.Q}_{coord.R}";
        }

        private void ConvertUnitToSide(string unitId, string newOwner)
        {
            // TODO: Hook into MilitaryManager.ConvertUnit(unitId, newOwner)
            Debug.Log($"[EspionageManager] Military: Unit {unitId} defected to {newOwner}.");
        }

        private void TriggerDiplomaticIncident(string nation1, string nation2, string reason)
        {
            // TODO: Hook into DiplomacyManager.TriggerIncident(nation1, nation2, reason)
            Debug.Log($"[EspionageManager] Diplomacy: Incident between {nation1} and {nation2} ({reason}).");
        }
    }

    // ========================================================================
    // ENUM EXTENSION HELPER
    // ========================================================================

    /// <summary>
    /// Provides utility extensions for enum types used in the espionage system.
    /// </summary>
    internal static class EnumExtension
    {
        /// <summary>
        /// Increments an <see cref="IntelLevel"/> by one step.
        /// Clamps to <see cref="IntelLevel.Complete"/> as the maximum value.
        /// </summary>
        /// <param name="level">The current intelligence level.</param>
        /// <returns>The next higher intelligence level, or Complete if already maxed.</returns>
        public static IntelLevel Increment(this IntelLevel level)
        {
            return level >= IntelLevel.Complete ? IntelLevel.Complete : level + 1;
        }
    }
}
