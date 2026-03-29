// =============================================================================
// IRON PROTOCOL - Space Weapons System
// File: SpaceWeaponsSystem.cs
// Description: Late-game orbital assets, space missions, and devastating
//              kinetic bombardment capabilities for IRON PROTOCOL grand strategy.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.SpaceWeapons
{
    // =========================================================================
    // ENUMERATIONS
    // =========================================================================

    /// <summary>
    /// Types of space-based assets that nations can deploy into orbit.
    /// Each provides different strategic advantages.
    /// </summary>
    public enum SpaceAssetType
    {
        /// <summary>Reconnaissance satellite providing persistent surveillance of enemy territory.</summary>
        SpySatellite,

        /// <summary>Communications relay boosting allied unit coordination in range.</summary>
        CommSatellite,

        /// <summary>Global positioning system improving accuracy of guided munitions and drones.</summary>
        GPSConstellation,

        /// <summary>Orbital research laboratory boosting technology and national prestige.</summary>
        SpaceStation,

        /// <summary>Defensive orbital weapon capable of intercepting incoming ICBMs.</summary>
        OrbitalLaser,

        /// <summary>Offensive weapon delivering devastating kinetic strikes (500kt equivalent, no radiation).</summary>
        KineticBombardment,

        /// <summary>Anti-satellite weapon designed to destroy enemy orbital assets.</summary>
        AntiSatellite
    }

    /// <summary>
    /// Orbital altitude classifications affecting satellite coverage, vulnerability,
    /// and maintenance requirements.
    /// </summary>
    public enum OrbitAltitude
    {
        /// <summary>Low Earth Orbit (200-2000 km). High resolution, short lifetime.</summary>
        LEO = 1,

        /// <summary>Medium Earth Orbit (2000-35786 km). Balanced coverage and lifetime.</summary>
        MEO = 2,

        /// <summary>Geostationary Orbit (~35786 km). Persistent coverage, lower resolution.</summary>
        GEO = 3
    }

    // =========================================================================
    // DATA CLASSES
    // =========================================================================

    /// <summary>
    /// Represents a space-based asset currently in orbit or being prepared for launch.
    /// Assets provide strategic advantages ranging from intelligence gathering
    /// to devastating orbital strikes.
    /// </summary>
    [Serializable]
    public class SpaceAsset
    {
        /// <summary>Unique identifier for this space asset.</summary>
        public string assetId { get; set; }

        /// <summary>Type of space asset determining its capabilities.</summary>
        public SpaceAssetType type { get; set; }

        /// <summary>Nation that owns and operates this asset.</summary>
        public string ownerNation { get; set; }

        /// <summary>
        /// Structural integrity of the asset.
        /// Range: 0 (destroyed) to 100 (fully operational).
        /// Degrades over time and from anti-satellite attacks.
        /// </summary>
        public int durability { get; set; }

        /// <summary>
        /// Operational effectiveness of the asset.
        /// Range: 0 (non-functional) to 100 (peak performance).
        /// Affected by durability and age.
        /// </summary>
        public int effectiveness { get; set; }

        /// <summary>Orbital altitude classification.</summary>
        public OrbitAltitude orbitAltitude { get; set; }

        /// <summary>Whether the asset is currently active and operational.</summary>
        public bool isActive { get; set; }

        /// <summary>Number of turns the asset has been in orbit.</summary>
        public int turnsInOrbit { get; set; }

        /// <summary>Per-turn maintenance cost in economic units.</summary>
        public float maintenanceCost { get; set; }

        /// <summary>Current ground track position for area-of-effect calculations.</summary>
        public HexCoord groundTrackPosition { get; set; }

        /// <summary>Maximum operational lifespan in turns (-1 for permanent assets).</summary>
        public int maxLifespan { get; set; }

        /// <summary>
        /// Creates a new space asset with specified parameters.
        /// </summary>
        /// <param name="assetId">Unique identifier.</param>
        /// <param name="type">Asset type.</param>
        /// <param name="ownerNation">Owning nation.</param>
        /// <param name="altitude">Orbital altitude.</param>
        /// <param name="maintenanceCost">Per-turn maintenance cost.</param>
        /// <param name="maxLifespan">Maximum operational turns (-1 for permanent).</param>
        public SpaceAsset(string assetId, SpaceAssetType type, string ownerNation,
            OrbitAltitude altitude, float maintenanceCost, int maxLifespan)
        {
            this.assetId = assetId;
            this.type = type;
            this.ownerNation = ownerNation;
            this.orbitAltitude = altitude;
            this.maintenanceCost = maintenanceCost;
            this.maxLifespan = maxLifespan;
            this.durability = 100;
            this.effectiveness = 100;
            this.isActive = true;
            this.turnsInOrbit = 0;
            this.groundTrackPosition = null;
        }
    }

    /// <summary>
    /// Represents a space launch mission in progress. Missions take multiple
    /// turns to complete before the payload is deployed to orbit.
    /// </summary>
    [Serializable]
    public class SpaceMission
    {
        /// <summary>Unique identifier for this mission.</summary>
        public string missionId { get; set; }

        /// <summary>Display name of the mission.</summary>
        public string missionName { get; set; }

        /// <summary>Type of payload being launched.</summary>
        public SpaceAssetType payload { get; set; }

        /// <summary>Nation launching the mission.</summary>
        public string launchingNation { get; set; }

        /// <summary>Total turns required to complete the mission.</summary>
        public int turnsToComplete { get; set; }

        /// <summary>Turns remaining until completion.</summary>
        public int turnsRemaining { get; set; }

        /// <summary>Total cost of the mission in economic units.</summary>
        public float cost { get; set; }

        /// <summary>Whether the mission has been completed and payload deployed.</summary>
        public bool isComplete { get; set; }

        /// <summary>Whether the mission has failed (payload lost).</summary>
        public bool failed { get; set; }

        /// <summary>
        /// Creates a new space mission.
        /// </summary>
        /// <param name="missionId">Unique mission identifier.</param>
        /// <param name="missionName">Display name.</param>
        /// <param name="payload">Payload type.</param>
        /// <param name="launchingNation">Launching nation.</param>
        /// <param name="turnsToComplete">Duration in turns.</param>
        /// <param name="cost">Mission cost.</param>
        public SpaceMission(string missionId, string missionName, SpaceAssetType payload,
            string launchingNation, int turnsToComplete, float cost)
        {
            this.missionId = missionId;
            this.missionName = missionName;
            this.payload = payload;
            this.launchingNation = launchingNation;
            this.turnsToComplete = turnsToComplete;
            this.turnsRemaining = turnsToComplete;
            this.cost = cost;
            this.isComplete = false;
            this.failed = false;
        }
    }

    /// <summary>
    /// Result of an orbital strike operation, containing damage data
    /// and after-effects information.
    /// </summary>
    [Serializable]
    public class OrbitalStrikeResult
    {
        /// <summary>Whether the orbital strike was successful.</summary>
        public bool success { get; set; }

        /// <summary>Total damage dealt (in hit points or equivalent).</summary>
        public float damage { get; set; }

        /// <summary>Human-readable description of the target and strike.</summary>
        public string targetDescription { get; set; }

        /// <summary>List of after-effects (e.g., diplomatic penalties, radiation, etc.).</summary>
        public List<string> afterEffects { get; set; } = new List<string>();

        /// <summary>Asset ID used for the strike.</summary>
        public string strikeAssetId { get; set; }

        /// <summary>Target hex coordinate.</summary>
        public HexCoord targetCoord { get; set; }

        /// <summary>
        /// Creates a successful orbital strike result.
        /// </summary>
        public static OrbitalStrikeResult CreateSuccess(float damage, string targetDescription,
            List<string> afterEffects, string assetId, HexCoord target)
        {
            return new OrbitalStrikeResult
            {
                success = true,
                damage = damage,
                targetDescription = targetDescription,
                afterEffects = afterEffects ?? new List<string>(),
                strikeAssetId = assetId,
                targetCoord = target
            };
        }

        /// <summary>
        /// Creates a failed orbital strike result.
        /// </summary>
        public static OrbitalStrikeResult CreateFailure(string reason)
        {
            return new OrbitalStrikeResult
            {
                success = false,
                damage = 0f,
                targetDescription = reason,
                afterEffects = new List<string> { reason }
            };
        }
    }

    // =========================================================================
    // SPACE WEAPONS SYSTEM (MonoBehaviour)
    // =========================================================================

    /// <summary>
    /// Core system managing space assets, launch missions, orbital strikes,
    /// and space technology progression. Late-game system for IRON PROTOCOL.
    /// <para>
    /// Space tech unlocks in three tiers:
    /// <list type="number">
    ///   <item>Tier 1 (Satellites): Spy, Comm, GPS satellites</item>
    ///   <item>Tier 2 (Stations): Space stations for research and prestige</item>
    ///   <item>Tier 3 (Weapons): Orbital lasers and kinetic bombardment</item>
    /// </list>
    /// </para>
    /// </summary>
    public class SpaceWeaponsSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // EVENTS
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired when a new space mission is launched.
        /// Arguments: (SpaceMission).
        /// </summary>
        public event Action<SpaceMission> OnMissionLaunched;

        /// <summary>
        /// Fired when a mission is completed (payload deployed).
        /// Arguments: (SpaceMission).
        /// </summary>
        public event Action<SpaceMission> OnMissionCompleted;

        /// <summary>
        /// Fired when a space asset is destroyed (by anti-satellite or random failure).
        /// Arguments: (assetId).
        /// </summary>
        public event Action<string> OnAssetDestroyed;

        /// <summary>
        /// Fired when an orbital strike is executed.
        /// Arguments: (OrbitalStrikeResult).
        /// </summary>
        public event Action<OrbitalStrikeResult> OnOrbitalStrike;

        // -----------------------------------------------------------------
        // SERIALIZED FIELDS
        // -----------------------------------------------------------------

        [Header("Mission Settings")]
        [Tooltip("Base chance of random mission failure per turn (percentage).")]
        [SerializeField] private float missionFailureChance = 0.02f;

        [Tooltip("Random asset failure chance per turn while in orbit (percentage).")]
        [SerializeField] private float randomFailureChance = 0.01f;

        [Tooltip("Durability loss per turn from orbital decay.")]
        [SerializeField] private int durabilityDecayPerTurn = 1;

        [Header("Asset Specifications")]
        [Tooltip("Kinetic bombardment damage (equivalent to 500kt conventional).")]
        [SerializeField] private float kineticBombardmentDamage = 5000f;

        [Tooltip("Orbital laser missile defense bonus (percentage).")]
        [SerializeField] private float orbitalLaserDefenseBonus = 30f;

        [Tooltip("Spy satellite vision bonus (hexes revealed).")]
        [SerializeField] private int spySatelliteVisionRange = 10;

        [Tooltip("GPS accuracy bonus for missiles and drones (percentage).")]
        [SerializeField] private float gpsAccuracyBonus = 15f;

        [Tooltip("Comm satellite coordination bonus for allied units (percentage).")]
        [SerializeField] private float commSatelliteCoordinationBonus = 20f;

        [Tooltip("Space station research speed bonus (percentage).")]
        [SerializeField] private float spaceStationResearchBonus = 30f;

        [Tooltip("Space station prestige bonus.")]
        [SerializeField] private float spaceStationPrestigeBonus = 50f;

        [Header("Diplomacy")]
        [Tooltip("Diplomatic reputation penalty for using kinetic bombardment.")]
        [SerializeField] private float kineticStrikeDiplomaticPenalty = 40f;

        [Tooltip("Diplomatic reputation penalty for anti-satellite attacks.")]
        [SerializeField] private float antiSatDiplomaticPenalty = 15f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // -----------------------------------------------------------------
        // PRIVATE STATE
        // -----------------------------------------------------------------

        private Dictionary<string, SpaceAsset> _orbitingAssets = new Dictionary<string, SpaceAsset>();
        private Dictionary<string, SpaceMission> _activeMissions = new Dictionary<string, SpaceMission>();
        private Dictionary<string, int> _spaceTechLevels = new Dictionary<string, int>();
        private int _assetIdCounter = 0;
        private int _missionIdCounter = 0;

        // -----------------------------------------------------------------
        // PUBLIC PROPERTIES
        // -----------------------------------------------------------------

        /// <summary>Number of currently orbiting active assets.</summary>
        public int OrbitingAssetCount => _orbitingAssets.Count(a => a.Value.isActive);

        /// <summary>Number of active space missions in progress.</summary>
        public int ActiveMissionCount => _activeMissions.Count;

        /// <summary>Read-only access to all orbiting assets.</summary>
        public IReadOnlyDictionary<string, SpaceAsset> OrbitingAssets => _orbitingAssets;

        /// <summary>Read-only access to all active missions.</summary>
        public IReadOnlyDictionary<string, SpaceMission> ActiveMissions => _activeMissions;

        // =================================================================
        // SPACE TECHNOLOGY
        // =================================================================

        /// <summary>
        /// Gets the space technology level for a nation.
        /// <para>
        /// Levels:
        /// <list type="bullet">
        ///   <item>0 = No space capability</item>
        ///   <item>1 = Satellites (spy, comm, GPS)</item>
        ///   <item>2 = Stations (space station research and prestige)</item>
        ///   <item>3 = Weapons (orbital lasers, kinetic bombardment)</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Space tech level (0-3).</returns>
        public int GetSpaceTechLevel(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0;

            _spaceTechLevels.TryGetValue(nationId, out int level);
            return level;
        }

        /// <summary>
        /// Sets the space technology level for a nation.
        /// Typically called by the research system when new techs are unlocked.
        /// </summary>
        /// <param name="nationId">The nation to update.</param>
        /// <param name="level">New tech level (0-3).</param>
        public void SetSpaceTechLevel(string nationId, int level)
        {
            if (string.IsNullOrEmpty(nationId)) return;
            _spaceTechLevels[nationId] = Mathf.Clamp(level, 0, 3);
        }

        /// <summary>
        /// Checks if a nation has the required tech level for a specific asset type.
        /// </summary>
        /// <param name="nationId">The nation to check.</param>
        /// <param name="assetType">The asset type to validate.</param>
        /// <returns>True if the nation has the required tech level.</returns>
        public bool HasRequiredTech(string nationId, SpaceAssetType assetType)
        {
            int techLevel = GetSpaceTechLevel(nationId);
            int requiredLevel = GetRequiredTechLevel(assetType);
            return techLevel >= requiredLevel;
        }

        /// <summary>
        /// Returns the minimum tech level required for an asset type.
        /// </summary>
        private int GetRequiredTechLevel(SpaceAssetType assetType)
        {
            switch (assetType)
            {
                case SpaceAssetType.SpySatellite:
                case SpaceAssetType.CommSatellite:
                case SpaceAssetType.GPSConstellation:
                    return 1;

                case SpaceAssetType.SpaceStation:
                    return 2;

                case SpaceAssetType.OrbitalLaser:
                case SpaceAssetType.KineticBombardment:
                case SpaceAssetType.AntiSatellite:
                    return 3;

                default:
                    return 1;
            }
        }

        // =================================================================
        // SPACE MISSIONS
        // =================================================================

        /// <summary>
        /// Launches a new space mission to deploy an asset into orbit.
        /// The mission takes multiple turns to complete before the payload is deployed.
        /// <para>
        /// Asset capabilities:
        /// <list type="table">
        ///   <listheader><term>Asset</term><description>Effect</description></listheader>
        ///   <item><term>SpySatellite</term><description>Reveals all units in 10-hex range, 10 turn orbit</description></item>
        ///   <item><term>CommSatellite</term><description>+20% coordination for allied units in range, 15 turn orbit</description></item>
        ///   <item><term>GPSConstellation</term><description>+15% accuracy for all missiles and drones, permanent</description></item>
        ///   <item><term>SpaceStation</term><description>Research +30%, prestige +50, continuous operation</description></item>
        ///   <item><term>OrbitalLaser</term><description>Defensive, shoots down ICBMs (+30% missile defense)</description></item>
        ///   <item><term>KineticBombardment</term><description>Devastating orbital strike (500kt equivalent, no radiation)</description></item>
        ///   <item><term>AntiSatellite</term><description>Destroy enemy satellites on completion</description></item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="nation">Nation launching the mission.</param>
        /// <param name="type">Type of payload to deploy.</param>
        /// <param name="cost">Mission cost in economic units.</param>
        /// <returns>The newly created <see cref="SpaceMission"/>, or null if launch failed.</returns>
        public SpaceMission LaunchMission(string nation, SpaceAssetType type, float cost)
        {
            if (string.IsNullOrEmpty(nation))
            {
                Debug.LogWarning("[SpaceSystem] LaunchMission: nation ID required.");
                return null;
            }

            if (cost <= 0f)
            {
                Debug.LogWarning("[SpaceSystem] LaunchMission: cost must be positive.");
                return null;
            }

            // Check tech requirements
            if (!HasRequiredTech(nation, type))
            {
                int requiredLevel = GetRequiredTechLevel(type);
                int currentLevel = GetSpaceTechLevel(nation);
                Debug.LogWarning($"[SpaceSystem] '{nation}' needs space tech level {requiredLevel} " +
                                $"for {type} (has level {currentLevel}).");
                return null;
            }

            // Determine mission parameters based on asset type
            var missionParams = GetMissionParameters(type);

            string missionId = $"mission_{++_missionIdCounter}_{nation}_{type}";
            string missionName = $"{nation} {type} Program";

            var mission = new SpaceMission(
                missionId, missionName, type, nation,
                missionParams.turnsToComplete, cost
            );

            _activeMissions[missionId] = mission;
            OnMissionLaunched?.Invoke(mission);

            if (enableDebugLogs)
            {
                Debug.Log($"[SpaceSystem] Mission '{missionName}' launched by '{nation}'. " +
                          $"Payload: {type}, Cost: {cost:F0}, Duration: {missionParams.turnsToComplete} turns.");
            }

            return mission;
        }

        /// <summary>
        /// Contains parameters for different mission types.
        /// </summary>
        private struct MissionParams
        {
            public int turnsToComplete;
            public OrbitAltitude altitude;
            public float maintenanceCost;
            public int maxLifespan;
        }

        /// <summary>
        /// Returns mission parameters for a given asset type.
        /// </summary>
        private MissionParams GetMissionParameters(SpaceAssetType type)
        {
            switch (type)
            {
                case SpaceAssetType.SpySatellite:
                    return new MissionParams
                    {
                        turnsToComplete = 2,
                        altitude = OrbitAltitude.LEO,
                        maintenanceCost = 15f,
                        maxLifespan = 10
                    };

                case SpaceAssetType.CommSatellite:
                    return new MissionParams
                    {
                        turnsToComplete = 2,
                        altitude = OrbitAltitude.MEO,
                        maintenanceCost = 20f,
                        maxLifespan = 15
                    };

                case SpaceAssetType.GPSConstellation:
                    return new MissionParams
                    {
                        turnsToComplete = 4,
                        altitude = OrbitAltitude.MEO,
                        maintenanceCost = 25f,
                        maxLifespan = -1 // Permanent
                    };

                case SpaceAssetType.SpaceStation:
                    return new MissionParams
                    {
                        turnsToComplete = 6,
                        altitude = OrbitAltitude.LEO,
                        maintenanceCost = 50f,
                        maxLifespan = -1 // Permanent
                    };

                case SpaceAssetType.OrbitalLaser:
                    return new MissionParams
                    {
                        turnsToComplete = 5,
                        altitude = OrbitAltitude.LEO,
                        maintenanceCost = 60f,
                        maxLifespan = 20
                    };

                case SpaceAssetType.KineticBombardment:
                    return new MissionParams
                    {
                        turnsToComplete = 5,
                        altitude = OrbitAltitude.LEO,
                        maintenanceCost = 70f,
                        maxLifespan = 15
                    };

                case SpaceAssetType.AntiSatellite:
                    return new MissionParams
                    {
                        turnsToComplete = 1,
                        altitude = OrbitAltitude.LEO,
                        maintenanceCost = 0f,
                        maxLifespan = 0 // One-use weapon
                    };

                default:
                    return new MissionParams
                    {
                        turnsToComplete = 3,
                        altitude = OrbitAltitude.LEO,
                        maintenanceCost = 20f,
                        maxLifespan = 10
                    };
            }
        }

        /// <summary>
        /// Advances all active missions by one turn. Completes missions that
        /// have finished their build time and deploys payloads to orbit.
        /// </summary>
        public void UpdateMissions()
        {
            var completedMissions = new List<string>();
            var failedMissions = new List<string>();

            foreach (var kvp in _activeMissions)
            {
                var mission = kvp.Value;
                if (mission.isComplete || mission.failed) continue;

                mission.turnsRemaining--;

                // Random failure check
                if (UnityEngine.Random.value < missionFailureChance)
                {
                    mission.failed = true;
                    failedMissions.Add(kvp.Key);

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SpaceSystem] Mission '{mission.missionName}' FAILED due to launch anomaly.");
                    }
                    continue;
                }

                // Check completion
                if (mission.turnsRemaining <= 0)
                {
                    CompleteMission(mission.missionId);
                    completedMissions.Add(kvp.Key);
                }
            }

            // Clean up completed and failed missions
            foreach (var id in completedMissions.Concat(failedMissions))
            {
                _activeMissions.Remove(id);
            }
        }

        /// <summary>
        /// Completes a space mission and deploys the payload to orbit as an active asset.
        /// </summary>
        /// <param name="missionId">The mission to complete.</param>
        public void CompleteMission(string missionId)
        {
            if (string.IsNullOrEmpty(missionId) || !_activeMissions.ContainsKey(missionId))
            {
                Debug.LogWarning("[SpaceSystem] CompleteMission: mission not found.");
                return;
            }

            var mission = _activeMissions[missionId];
            var missionParams = GetMissionParameters(mission.payload);

            // Handle AntiSatellite as a special case (one-use, no deployment)
            if (mission.payload == SpaceAssetType.AntiSatellite)
            {
                mission.isComplete = true;
                if (enableDebugLogs)
                {
                    Debug.Log($"[SpaceSystem] Anti-satellite mission '{mission.missionName}' complete. " +
                              "Use DestroyAsset() to target enemy satellites.");
                }
                OnMissionCompleted?.Invoke(mission);
                return;
            }

            // Create the orbiting asset
            string assetId = $"asset_{++_assetIdCounter}_{mission.launchingNation}_{mission.payload}";
            var asset = new SpaceAsset(
                assetId, mission.payload, mission.launchingNation,
                missionParams.altitude, missionParams.maintenanceCost, missionParams.maxLifespan
            );

            _orbitingAssets[assetId] = asset;
            mission.isComplete = true;

            OnMissionCompleted?.Invoke(mission);

            if (enableDebugLogs)
            {
                Debug.Log($"[SpaceSystem] Mission '{mission.missionName}' COMPLETE. " +
                          $"Asset '{assetId}' ({mission.payload}) deployed to {missionParams.altitude} orbit. " +
                          $"Maintenance: {missionParams.maintenanceCost:F0}/turn, " +
                          $"Lifespan: {(missionParams.maxLifespan == -1 ? "Permanent" : missionParams.maxLifespan + " turns")}.");
            }
        }

        // =================================================================
        // ASSET MANAGEMENT
        // =================================================================

        /// <summary>
        /// Destroys a space asset, removing it from orbit.
        /// Used for anti-satellite attacks or asset retirement.
        /// </summary>
        /// <param name="assetId">The asset to destroy.</param>
        /// <returns>True if the asset was successfully destroyed.</returns>
        public bool DestroyAsset(string assetId)
        {
            if (string.IsNullOrEmpty(assetId) || !_orbitingAssets.ContainsKey(assetId))
            {
                Debug.LogWarning("[SpaceSystem] DestroyAsset: asset not found in orbit.");
                return false;
            }

            var asset = _orbitingAssets[assetId];

            if (enableDebugLogs)
            {
                Debug.Log($"[SpaceSystem] Asset '{assetId}' ({asset.type}) belonging to '{asset.ownerNation}' " +
                          $"destroyed after {asset.turnsInOrbit} turns in orbit.");
            }

            _orbitingAssets.Remove(assetId);
            OnAssetDestroyed?.Invoke(assetId);

            return true;
        }

        /// <summary>
        /// Repairs a damaged space asset, restoring durability.
        /// </summary>
        /// <param name="assetId">The asset to repair.</param>
        /// <param name="repairAmount">Amount of durability to restore.</param>
        /// <returns>True if repair was successful.</returns>
        public bool RepairAsset(string assetId, int repairAmount)
        {
            if (!_orbitingAssets.ContainsKey(assetId)) return false;

            var asset = _orbitingAssets[assetId];
            asset.durability = Mathf.Min(100, asset.durability + repairAmount);
            asset.effectiveness = asset.durability; // Effectiveness tracks durability

            if (enableDebugLogs)
            {
                Debug.Log($"[SpaceSystem] Asset '{assetId}' repaired. Durability: {asset.durability}.");
            }

            return true;
        }

        /// <summary>
        /// Returns all orbiting assets owned by a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of orbiting assets owned by the nation.</returns>
        public List<SpaceAsset> GetOrbitingAssets(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return new List<SpaceAsset>();

            return _orbitingAssets.Values
                .Where(a => a.ownerNation == nationId && a.isActive)
                .ToList();
        }

        /// <summary>
        /// Returns all orbiting assets from all nations.
        /// </summary>
        /// <returns>List of all active orbiting assets.</returns>
        public List<SpaceAsset> GetAllOrbitingAssets()
        {
            return _orbitingAssets.Values.Where(a => a.isActive).ToList();
        }

        /// <summary>
        /// Gets a specific orbiting asset by ID.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <returns>The <see cref="SpaceAsset"/>, or null if not found.</returns>
        public SpaceAsset GetAsset(string assetId)
        {
            _orbitingAssets.TryGetValue(assetId, out var asset);
            return asset;
        }

        /// <summary>
        /// Gets all orbiting assets of a specific type.
        /// </summary>
        /// <param name="type">The asset type to filter by.</param>
        /// <returns>List of matching assets.</returns>
        public List<SpaceAsset> GetAssetsByType(SpaceAssetType type)
        {
            return _orbitingAssets.Values
                .Where(a => a.type == type && a.isActive)
                .ToList();
        }

        // =================================================================
        // ORBITAL STRIKES
        // =================================================================

        /// <summary>
        /// Executes an orbital strike against a target hex coordinate.
        /// <para>
        /// Kinetic bombardment delivers devastating conventional damage
        /// (500kt equivalent) with NO nuclear fallout, making it a
        /// politically damaging but strategically powerful weapon.
        /// </para>
        /// <para>
        /// Prerequisites:
        /// <list type="bullet">
        ///   <item>Space tech level 3 (Weapons)</item>
        ///   <item>At least one active KineticBombardment satellite in orbit</item>
        ///   <item>Sufficient maintenance budget</item>
        /// </list>
        /// </para>
        /// <para>
        /// Consequences:
        /// <list type="bullet">
        ///   <item>Massive damage to target hex (5000 hit points)</item>
        ///   <item>Destroys all units and structures in the hex</item>
        ///   <item>No radiation (conventional weapon)</item>
        ///   <item>Massive diplomatic penalty (-40 international reputation)</item>
        ///   <item>All nations gain casus belli against the attacker</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="assetId">The kinetic bombardment satellite to use.</param>
        /// <param name="target">Hex coordinate to strike.</param>
        /// <returns><see cref="OrbitalStrikeResult"/> with strike details.</returns>
        public OrbitalStrikeResult OrbitalStrike(string assetId, HexCoord target)
        {
            if (string.IsNullOrEmpty(assetId) || !_orbitingAssets.ContainsKey(assetId))
            {
                return OrbitalStrikeResult.CreateFailure("Kinetic bombardment asset not found in orbit.");
            }

            var asset = _orbitingAssets[assetId];

            if (!asset.isActive)
            {
                return OrbitalStrikeResult.CreateFailure("Asset is not active.");
            }

            if (asset.type != SpaceAssetType.KineticBombardment)
            {
                return OrbitalStrikeResult.CreateFailure($"Asset type is {asset.type}, not KineticBombardment.");
            }

            if (asset.effectiveness < 50)
            {
                return OrbitalStrikeResult.CreateFailure("Asset effectiveness too low for orbital strike.");
            }

            if (target == null)
            {
                return OrbitalStrikeResult.CreateFailure("Target coordinate is null.");
            }

            // Calculate strike damage based on effectiveness
            float effectivenessMultiplier = asset.effectiveness / 100f;
            float damage = kineticBombardmentDamage * effectivenessMultiplier;

            // Durability cost of firing
            asset.durability -= 15;
            asset.effectiveness = asset.durability;

            if (asset.durability <= 0)
            {
                asset.isActive = false;
                DestroyAsset(assetId);
            }

            // Build after-effects
            var afterEffects = new List<string>
            {
                $"Devastating kinetic strike dealt {damage:F0} damage to {target}.",
                $"All enemy units and structures in target hex destroyed.",
                "No radioactive fallout (conventional weapon).",
                $"Diplomatic reputation penalty: -{kineticStrikeDiplomaticPenalty:F0} for '{asset.ownerNation}'.",
                "All nations may gain casus belli against the attacker.",
                "Global condemnation: orbital weapons usage recorded."
            };

            // Apply additional durability loss
            asset.durability = Mathf.Max(0, asset.durability - 5);

            var result = OrbitalStrikeResult.CreateSuccess(
                damage,
                $"Kinetic bombardment by '{asset.ownerNation}' on {target}",
                afterEffects,
                assetId,
                target
            );

            OnOrbitalStrike?.Invoke(result);

            if (enableDebugLogs)
            {
                Debug.Log($"[SpaceSystem] ORBITAL STRIKE! '{asset.ownerNation}' used kinetic bombardment " +
                          $"on {target}. Damage: {damage:F0}. Asset durability: {asset.durability}.");
            }

            return result;
        }

        // =================================================================
        // BONUS CALCULATIONS
        // =================================================================

        /// <summary>
        /// Calculates the total missile defense bonus for a nation from
        /// all active orbital lasers in orbit.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>Cumulative missile defense bonus (percentage).</returns>
        public float GetMissileDefenseBonus(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            float totalBonus = 0f;
            int laserCount = 0;

            foreach (var asset in _orbitingAssets.Values)
            {
                if (asset.ownerNation == nationId && asset.isActive && asset.type == SpaceAssetType.OrbitalLaser)
                {
                    float assetBonus = orbitalLaserDefenseBonus * (asset.effectiveness / 100f);

                    // Stacking penalty: each additional laser adds diminishing returns
                    float stackingPenalty = 1f / (1f + laserCount * 0.3f);
                    totalBonus += assetBonus * stackingPenalty;
                    laserCount++;
                }
            }

            return Mathf.Clamp(totalBonus, 0f, 75f); // Cap at 75% total
        }

        /// <summary>
        /// Calculates the total vision bonus for a nation from
        /// all active spy satellites in orbit.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>Cumulative vision bonus in hexes.</returns>
        public float GetVisionBonus(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            float totalBonus = 0f;

            foreach (var asset in _orbitingAssets.Values)
            {
                if (asset.ownerNation == nationId && asset.isActive)
                {
                    switch (asset.type)
                    {
                        case SpaceAssetType.SpySatellite:
                            totalBonus += spySatelliteVisionRange * (asset.effectiveness / 100f);
                            break;

                        case SpaceAssetType.GPSConstellation:
                            totalBonus += 3f * (asset.effectiveness / 100f);
                            break;
                    }
                }
            }

            return totalBonus;
        }

        /// <summary>
        /// Calculates the coordination bonus for allied units from comm satellites.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>Cumulative coordination bonus (percentage).</returns>
        public float GetCoordinationBonus(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            float totalBonus = 0f;

            foreach (var asset in _orbitingAssets.Values)
            {
                if (asset.ownerNation == nationId && asset.isActive && asset.type == SpaceAssetType.CommSatellite)
                {
                    totalBonus += commSatelliteCoordinationBonus * (asset.effectiveness / 100f);
                }
            }

            return Mathf.Clamp(totalBonus, 0f, 50f);
        }

        /// <summary>
        /// Calculates the GPS accuracy bonus for guided munitions.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>GPS accuracy bonus (percentage).</returns>
        public float GetGPSAccuracyBonus(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            float totalBonus = 0f;

            foreach (var asset in _orbitingAssets.Values)
            {
                if (asset.ownerNation == nationId && asset.isActive && asset.type == SpaceAssetType.GPSConstellation)
                {
                    totalBonus += gpsAccuracyBonus * (asset.effectiveness / 100f);
                }
            }

            return Mathf.Clamp(totalBonus, 0f, 30f);
        }

        /// <summary>
        /// Calculates the research speed bonus from space stations.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>Research speed bonus (percentage).</returns>
        public float GetResearchBonus(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            float totalBonus = 0f;

            foreach (var asset in _orbitingAssets.Values)
            {
                if (asset.ownerNation == nationId && asset.isActive && asset.type == SpaceAssetType.SpaceStation)
                {
                    totalBonus += spaceStationResearchBonus * (asset.effectiveness / 100f);
                }
            }

            return Mathf.Clamp(totalBonus, 0f, 50f);
        }

        /// <summary>
        /// Calculates the total maintenance cost for all of a nation's orbiting assets.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>Total per-turn maintenance cost.</returns>
        public float GetTotalMaintenanceCost(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            return _orbitingAssets.Values
                .Where(a => a.ownerNation == nationId && a.isActive)
                .Sum(a => a.maintenanceCost);
        }

        // =================================================================
        // ORBITAL UPDATE
        // =================================================================

        /// <summary>
        /// Updates all orbiting assets each turn. Processes:
        /// <list type="number">
        ///   <item>Maintenance cost deduction</item>
        ///   <item>Random failure chance (1% per turn)</item>
        ///   <item>Durability decay from orbital environment</item>
        ///   <item>Lifespan expiration for time-limited assets</item>
        ///   <item>Effectiveness recalculation</item>
        /// </list>
        /// </summary>
        public void UpdateOrbits()
        {
            var destroyedAssets = new List<string>();

            foreach (var kvp in _orbitingAssets)
            {
                var asset = kvp.Value;
                if (!asset.isActive) continue;

                asset.turnsInOrbit++;

                // --- Durability decay ---
                asset.durability -= durabilityDecayPerTurn;
                if (asset.orbitAltitude == OrbitAltitude.LEO)
                {
                    asset.durability -= 1; // LEO degrades faster
                }

                // --- Random failure check (1% per turn) ---
                if (UnityEngine.Random.value < randomFailureChance)
                {
                    int damage = UnityEngine.Random.Range(20, 50);
                    asset.durability -= damage;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SpaceSystem] Asset '{asset.assetId}' ({asset.type}) suffered " +
                                  $"random orbital failure! Durability -{damage}.");
                    }
                }

                // --- Update effectiveness based on durability ---
                asset.effectiveness = Mathf.Clamp(asset.durability, 0, 100);

                // --- Check lifespan expiration ---
                if (asset.maxLifespan > 0 && asset.turnsInOrbit >= asset.maxLifespan)
                {
                    asset.isActive = false;
                    destroyedAssets.Add(kvp.Key);

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SpaceSystem] Asset '{asset.assetId}' ({asset.type}) reached end of life " +
                                  $"after {asset.turnsInOrbit} turns.");
                    }
                    continue;
                }

                // --- Check durability depletion ---
                if (asset.durability <= 0)
                {
                    asset.isActive = false;
                    destroyedAssets.Add(kvp.Key);

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SpaceSystem] Asset '{asset.assetId}' ({asset.type}) destroyed " +
                                  $"due to durability depletion.");
                    }
                    continue;
                }
            }

            // Remove destroyed assets and fire events
            foreach (var assetId in destroyedAssets)
            {
                _orbitingAssets.Remove(assetId);
                OnAssetDestroyed?.Invoke(assetId);
            }
        }

        // =================================================================
        // UTILITY METHODS
        // =================================================================

        /// <summary>
        /// Gets all active missions for a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of active missions.</returns>
        public List<SpaceMission> GetMissionsByNation(string nationId)
        {
            return _activeMissions.Values
                .Where(m => m.launchingNation == nationId && !m.isComplete && !m.failed)
                .ToList();
        }

        /// <summary>
        /// Gets the number of orbiting assets by type for a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <param name="type">Asset type to count.</param>
        /// <returns>Number of active assets of the specified type.</returns>
        public int GetAssetCount(string nationId, SpaceAssetType type)
        {
            return _orbitingAssets.Values
                .Count(a => a.ownerNation == nationId && a.type == type && a.isActive);
        }

        /// <summary>
        /// Gets the total number of orbiting assets for all nations.
        /// </summary>
        /// <returns>Total active asset count.</returns>
        public int GetTotalOrbitingAssetCount()
        {
            return _orbitingAssets.Values.Count(a => a.isActive);
        }

        /// <summary>
        /// Returns a summary of all space assets and their statuses for debug display.
        /// </summary>
        public string GetOrbitSummary()
        {
            var assets = GetAllOrbitingAssets();
            if (assets.Count == 0) return "No assets in orbit.";

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"=== ORBITAL ASSETS ({assets.Count}) ===");

            foreach (var asset in assets)
            {
                summary.AppendLine($"  [{asset.assetId}] {asset.type} - {asset.ownerNation} " +
                    $"(Dur: {asset.durability}, Eff: {asset.effectiveness}, " +
                    $"Alt: {asset.orbitAltitude}, Age: {asset.turnsInOrbit}t)");
            }

            return summary.ToString();
        }

        /// <summary>
        /// Unity lifecycle: called every frame. Reserved for future real-time updates.
        /// </summary>
        private void Update()
        {
            // Reserved for real-time space simulation, visual updates, etc.
        }
    }
}
