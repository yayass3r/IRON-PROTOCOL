// ============================================================================
// Iron Protocol - World Event Manager
// Dynamic world events system that creates emergent gameplay through
// natural disasters, political crises, economic shocks, and more.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.WorldEvents
{
    // ========================================================================
    // ENUMERATIONS
    // ========================================================================

    /// <summary>
    /// Categories of world events that can occur during gameplay.
    /// Each type has unique trigger conditions, effects, and resolution mechanics.
    /// </summary>
    public enum WorldEventType
    {
        /// <summary>Earthquakes, floods, volcanic eruptions, tsunamis.</summary>
        NaturalDisaster,

        /// <summary>Popular uprisings, government overthrows, civil unrest.</summary>
        Revolution,

        /// <summary>Global or regional disease outbreaks affecting population and military.</summary>
        Pandemic,

        /// <summary>Market crashes, recessions, inflation spikes.</summary>
        EconomicCrisis,

        /// <summary>New resource deposits found, altering strategic landscape.</summary>
        ResourceDiscovery,

        /// <summary>Unexpected technological advances by a nation.</summary>
        TechBreakthrough,

        /// <summary>Mass migration of civilians across borders.</summary>
        RefugeeCrisis,

        /// <summary>Trade disagreements, embargoes, tariff wars.</summary>
        TradeDispute,

        /// <summary>Targeted killing of a national leader.</summary>
        Assassination,

        /// <summary>Radiation leaks, oil spills, ecological damage.</summary>
        EnvironmentalDisaster
    }

    /// <summary>
    /// Severity levels for world events, affecting magnitude of effects
    /// and duration of impact.
    /// </summary>
    public enum DisasterSeverity
    {
        /// <summary>Minor effects; short duration; easily managed.</summary>
        Minor,

        /// <summary>Moderate effects; noticeable impact on game state.</summary>
        Moderate,

        /// <summary>Severe effects; can alter strategic calculations significantly.</summary>
        Major,

        /// <summary>Devastating effects; potential game-changing consequences.</summary>
        Catastrophic
    }

    // ========================================================================
    // EVENT EFFECT CLASS
    // ========================================================================

    /// <summary>
    /// Represents a single effect of a world event, describing what changes
    /// to apply to the game state, to what target, and for how long.
    /// </summary>
    [Serializable]
    public class EventEffect
    {
        /// <summary>
        /// The type of effect to apply (e.g., "production_modifier", "stability_change",
        /// "morale_change", "population_change", "resource_modifier", "diplomatic_change").
        /// </summary>
        public string EffectType { get; set; }

        /// <summary>
        /// The target identifier (nation ID, city ID, region ID, or hex coordinate string)
        /// that this effect applies to.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>The magnitude of the effect. Can be positive (beneficial) or negative (harmful).</summary>
        public float Value { get; set; }

        /// <summary>Duration in turns. 0 means instant/one-time effect. -1 means permanent.</summary>
        public int Duration { get; set; }

        /// <summary>Human-readable description of this specific effect.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Creates a new event effect with all parameters.
        /// </summary>
        public EventEffect(string effectType, string targetId, float value, int duration, string description)
        {
            EffectType = effectType;
            TargetId = targetId;
            Value = value;
            Duration = duration;
            Description = description;
        }

        /// <summary>
        /// Returns a formatted string describing the effect for UI display.
        /// </summary>
        public override string ToString()
        {
            string durationText = Duration == 0 ? "Instant" : Duration == -1 ? "Permanent" : $"{Duration} turns";
            string sign = Value >= 0 ? "+" : "";
            return $"{Description} ({sign}{Value}, {durationText})";
        }
    }

    // ========================================================================
    // WORLD EVENT CLASS
    // ========================================================================

    /// <summary>
    /// Represents a single world event instance with its type, severity, effects,
    /// and duration tracking. Events can be global (affecting all nations) or
    /// targeted at specific regions or nations.
    /// </summary>
    [Serializable]
    public class WorldEvent
    {
        /// <summary>Unique identifier for this event instance.</summary>
        public string EventId { get; set; }

        /// <summary>The category of world event.</summary>
        public WorldEventType Type { get; set; }

        /// <summary>Display name of the event.</summary>
        public string EventName { get; set; }

        /// <summary>Detailed narrative description of the event.</summary>
        public string Description { get; set; }

        /// <summary>Severity level affecting the magnitude of all effects.</summary>
        public DisasterSeverity Severity { get; set; }

        /// <summary>The region ID where this event is centered, if applicable.</summary>
        public string AffectedRegionId { get; set; }

        /// <summary>The nation ID primarily affected, if applicable.</summary>
        public string AffectedNationId { get; set; }

        /// <summary>Total duration of the event in turns.</summary>
        public int DurationTurns { get; set; }

        /// <summary>Remaining turns before the event expires.</summary>
        public int TurnsRemaining { get; set; }

        /// <summary>The specific effects this event applies to the game state.</summary>
        public List<EventEffect> Effects { get; set; } = new List<EventEffect>();

        /// <summary>Whether this event is currently active and applying effects.</summary>
        public bool IsActive { get; set; }

        /// <summary>Whether this event affects all nations globally.</summary>
        public bool IsGlobal { get; set; }

        /// <summary>Icon sprite for UI display. Assigned at runtime from resources.</summary>
        public Sprite EventIcon { get; set; }

        /// <summary>The turn number on which this event was triggered.</summary>
        public int TriggeredOnTurn { get; set; }

        /// <summary>
        /// Returns a severity multiplier used to scale effect values.
        /// Minor=0.5, Moderate=1.0, Major=1.5, Catastrophic=2.0.
        /// </summary>
        public float SeverityMultiplier
        {
            get
            {
                switch (Severity)
                {
                    case DisasterSeverity.Minor:       return 0.5f;
                    case DisasterSeverity.Moderate:    return 1.0f;
                    case DisasterSeverity.Major:       return 1.5f;
                    case DisasterSeverity.Catastrophic: return 2.0f;
                    default:                           return 1.0f;
                }
            }
        }

        /// <summary>
        /// Returns a formatted summary string for UI display.
        /// </summary>
        public override string ToString()
        {
            string scope = IsGlobal ? "[GLOBAL]" : $"[{AffectedNationId ?? AffectedRegionId}]";
            return $"{scope} {EventName} ({Severity}) - {TurnsRemaining}/{DurationTurns} turns remaining";
        }
    }

    // ========================================================================
    // GAME STATE SNAPSHOT (for probability calculations)
    // ========================================================================

    /// <summary>
    /// Lightweight game state snapshot used for event probability calculations.
    /// The WorldEventManager reads this to determine which events are more or less likely.
    /// </summary>
    public class GameState
    {
        /// <summary>Current turn number.</summary>
        public int CurrentTurn { get; set; }

        /// <summary>Average stability across all nations (0-100).</summary>
        public float AverageStability { get; set; } = 70f;

        /// <summary>Average global economy health (0-100).</summary>
        public float AverageEconomy { get; set; } = 70f;

        /// <summary>Global tension level (0-100). Higher increases conflict-related events.</summary>
        public float GlobalTension { get; set; } = 20f;

        /// <summary>Whether any nation is currently at war.</summary>
        public bool IsWarActive { get; set; }

        /// <summary>Number of active trade agreements between nations.</summary>
        public int ActiveTradeAgreements { get; set; }

        /// <summary>Total population across all nations.</summary>
        public float TotalPopulation { get; set; }

        /// <summary>Number of active nuclear/radiation sources (affects environmental events).</summary>
        public int NuclearFacilities { get; set; }
    }

    // ========================================================================
    // WORLD EVENT MANAGER (MONOBEHAVIOUR)
    // ========================================================================

    /// <summary>
    /// Central manager for all dynamic world events. Handles event initialization,
    /// probability-weighted random selection, effect application, and lifecycle management.
    /// 
    /// <para>Integration Points:</para>
    /// <list type="bullet">
    ///   <item>Call <see cref="ProcessTurn"/> from your turn manager each turn.</item>
    ///   <item>Subscribe to <see cref="OnEventTriggered"/> for UI popups.</item>
    ///   <item>Call specific trigger methods (e.g., <see cref="TriggerNaturalDisaster"/>) for scripted events.</item>
    /// </list>
    /// </summary>
    public class WorldEventManager : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // INSPECTOR CONFIGURATION
        // --------------------------------------------------------------------

        [Header("Event Frequency")]
        [Tooltip("Base probability per turn that any world event occurs (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float baseEventChancePerTurn = 0.25f;

        [Tooltip("Minimum turns between random events.")]
        [SerializeField] private int minTurnsBetweenEvents = 3;

        [Tooltip("Maximum number of simultaneous active events.")]
        [SerializeField] private int maxActiveEvents = 5;

        [Header("Event Effects")]
        [Tooltip("Whether events can trigger during the first N turns (grace period).")]
        [SerializeField] private int gracePeriodTurns = 5;

        [Tooltip("Global multiplier applied to all event effect values.")]
        [SerializeField] [Range(0.5f, 2f)] private float globalEffectMultiplier = 1f;

        // --------------------------------------------------------------------
        // PRIVATE STATE
        // --------------------------------------------------------------------

        /// <summary>Template definitions for all possible world events.</summary>
        private readonly List<WorldEvent> _eventTemplates = new List<WorldEvent>();

        /// <summary>Currently active events being processed each turn.</summary>
        private readonly List<WorldEvent> _activeEvents = new List<WorldEvent>();

        /// <summary>History of all expired events for reference and UI display.</summary>
        private readonly List<WorldEvent> _eventHistory = new List<WorldEvent>();

        /// <summary>Running event ID counter for unique generation.</summary>
        private int _eventIdCounter;

        /// <summary>Turn number of the last randomly triggered event.</summary>
        private int _lastEventTurn;

        /// <summary>Current game turn number.</summary>
        private int _currentTurn;

        /// <summary>Reference to the effects applier for modifying game state.</summary>
        private EventEffectsApplier _effectsApplier;

        // --------------------------------------------------------------------
        // EVENTS
        // --------------------------------------------------------------------

        /// <summary>
        /// Fired when a new world event is triggered.
        /// Subscribe to display UI popups and notifications.
        /// </summary>
        public event Action<WorldEvent> OnEventTriggered;

        /// <summary>
        /// Fired when an active event expires after its duration ends.
        /// </summary>
        public event Action<WorldEvent> OnEventExpired;

        /// <summary>
        /// Fired when a nation collapses due to event effects (e.g., revolution, economic crisis).
        /// Parameters: nationId.
        /// </summary>
        public event Action<string> OnNationCollapsed;

        // --------------------------------------------------------------------
        // PROPERTIES
        // --------------------------------------------------------------------

        /// <summary>Gets the list of currently active world events.</summary>
        public IReadOnlyList<WorldEvent> ActiveEvents => _activeEvents.AsReadOnly();

        /// <summary>Gets the full history of expired events.</summary>
        public IReadOnlyList<WorldEvent> EventHistory => _eventHistory.AsReadOnly();

        // --------------------------------------------------------------------
        // UNITY LIFECYCLE
        // --------------------------------------------------------------------

        /// <summary>
        /// Initializes the event system and loads default event templates.
        /// </summary>
        private void Awake()
        {
            _effectsApplier = GetComponent<EventEffectsApplier>();
            if (_effectsApplier == null)
            {
                _effectsApplier = gameObject.AddComponent<EventEffectsApplier>();
            }

            InitializeDefaultEvents();
            Debug.Log($"[WorldEventManager] System initialized with {_eventTemplates.Count} event templates.");
        }

        // ====================================================================
        // TURN PROCESSING
        // ====================================================================

        /// <summary>
        /// Main turn processing method. Call this once per turn to:
        /// 1. Roll for a potential new random event.
        /// 2. Tick down active event durations.
        /// 3. Remove expired events.
        /// </summary>
        /// <param name="turnNumber">The current game turn number.</param>
        /// <param name="state">Current game state snapshot for probability calculations.</param>
        public void ProcessTurn(int turnNumber, GameState state)
        {
            _currentTurn = turnNumber;

            // Update active events (tick durations)
            UpdateActiveEvents();

            // Attempt to roll a new event
            if (_currentTurn > gracePeriodTurns)
            {
                TryRollRandomEvent(state);
            }
        }

        // ====================================================================
        // DEFAULT EVENT INITIALIZATION
        // ====================================================================

        /// <summary>
        /// Initializes the 20 predefined world event templates that can occur
        /// during gameplay. Each template defines the event type, base effects,
        /// and narrative description.
        /// </summary>
        public void InitializeDefaultEvents()
        {
            _eventTemplates.Clear();

            // --- 1. EARTHQUAKE ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_EARTHQUAKE",
                Type = WorldEventType.NaturalDisaster,
                EventName = "Earthquake",
                Description = "A devastating earthquake has struck the region, destroying buildings " +
                             "and infrastructure. Production is severely disrupted and refugees are fleeing.",
                Severity = DisasterSeverity.Major,
                DurationTurns = 3,
                Effects = new List<EventEffect>
                {
                    new EventEffect("production_modifier", "", -0.20f, 3, "Production reduced by earthquake damage"),
                    new EventEffect("population_change", "", -0.05f, 0, "Casualties from earthquake"),
                    new EventEffect("morale_change", "", -15f, 2, "Morale drop from destruction"),
                    new EventEffect("building_destroy", "", -1f, 0, "Building destroyed by earthquake")
                }
            });

            // --- 2. FLOOD ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_FLOOD",
                Type = WorldEventType.NaturalDisaster,
                EventName = "Flooding",
                Description = "Heavy rainfall has caused severe flooding across the region. " +
                             "Movement is impaired and crops have been devastated.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 2,
                Effects = new List<EventEffect>
                {
                    new EventEffect("movement_penalty", "", -0.30f, 2, "Movement reduced by flooding"),
                    new EventEffect("production_modifier", "", -0.15f, 2, "Crop damage reduces production"),
                    new EventEffect("resource_modifier", "food", -0.20f, 1, "Food supplies damaged by flood"),
                    new EventEffect("morale_change", "", -10f, 1, "Morale drop from flood damage")
                }
            });

            // --- 3. VOLCANIC ERUPTION ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_VOLCANO",
                Type = WorldEventType.NaturalDisaster,
                EventName = "Volcanic Eruption",
                Description = "A volcano has erupted, spewing ash and lava across the landscape. " +
                             "Air operations are severely hampered and terrain is permanently altered.",
                Severity = DisasterSeverity.Catastrophic,
                DurationTurns = 4,
                Effects = new List<EventEffect>
                {
                    new EventEffect("production_modifier", "", -0.30f, 4, "Production halted by eruption"),
                    new EventEffect("air_operations", "", -0.50f, 3, "Ash cloud grounds aircraft"),
                    new EventEffect("population_change", "", -0.08f, 0, "Casualties from eruption"),
                    new EventEffect("terrain_change", "", -1f, -1, "Terrain permanently altered by lava"),
                    new EventEffect("morale_change", "", -20f, 3, "Devastating morale impact")
                }
            });

            // --- 4. TSUNAMI ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_TSUNAMI",
                Type = WorldEventType.NaturalDisaster,
                EventName = "Tsunami",
                Description = "A massive tsunami has struck the coastline, devastating coastal cities " +
                             "and disrupting naval operations.",
                Severity = DisasterSeverity.Major,
                DurationTurns = 3,
                Effects = new List<EventEffect>
                {
                    new EventEffect("production_modifier", "", -0.25f, 3, "Coastal production destroyed"),
                    new EventEffect("naval_disruption", "", -0.40f, 2, "Naval operations disrupted"),
                    new EventEffect("population_change", "", -0.06f, 0, "Coastal casualties"),
                    new EventEffect("building_destroy", "", -1f, 0, "Coastal buildings destroyed")
                }
            });

            // --- 5. PANDEMIC ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_PANDEMIC",
                Type = WorldEventType.Pandemic,
                EventName = "Pandemic Outbreak",
                Description = "A deadly disease is spreading through the population. " +
                             "Military readiness is dropping as soldiers fall ill.",
                Severity = DisasterSeverity.Catastrophic,
                DurationTurns = 5,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("population_change", "", -0.05f, 5, "Population declining from disease"),
                    new EventEffect("military_readiness", "", -0.20f, 5, "Military readiness reduced by illness"),
                    new EventEffect("production_modifier", "", -0.15f, 4, "Workforce reduced by pandemic"),
                    new EventEffect("morale_change", "", -25f, 3, "Fear and panic spread"),
                    new EventEffect("movement_penalty", "", -0.10f, 3, "Quarantine restrictions limit movement")
                }
            });

            // --- 6. FAMINE ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_FAMINE",
                Type = WorldEventType.NaturalDisaster,
                EventName = "Famine",
                Description = "Crop failures have led to widespread food shortages. " +
                             "Population is declining and morale is plummeting.",
                Severity = DisasterSeverity.Major,
                DurationTurns = 4,
                Effects = new List<EventEffect>
                {
                    new EventEffect("population_change", "", -0.04f, 4, "Starvation reduces population"),
                    new EventEffect("morale_change", "", -20f, 4, "Morale devastated by hunger"),
                    new EventEffect("resource_modifier", "food", -0.40f, 4, "Food supplies critically low"),
                    new EventEffect("military_readiness", "", -0.10f, 3, "Underfed troops fight poorly")
                }
            });

            // --- 7. POPULAR REVOLUTION ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_REVOLUTION",
                Type = WorldEventType.Revolution,
                EventName = "Popular Revolution",
                Description = "Massive popular uprising against the government! Cities are rebelling " +
                             "and military units may defect. Stability has collapsed.",
                Severity = DisasterSeverity.Catastrophic,
                DurationTurns = 5,
                Effects = new List<EventEffect>
                {
                    new EventEffect("stability_change", "", -50f, 5, "Government authority collapses"),
                    new EventEffect("production_modifier", "", -0.50f, 5, "Production halts during revolution"),
                    new EventEffect("morale_change", "", -30f, 3, "Civil unrest devastates morale"),
                    new EventEffect("military_defection", "", -0.15f, 1, "Military units may defect to rebels")
                }
            });

            // --- 8. MILITARY COUP ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_COUP",
                Type = WorldEventType.Revolution,
                EventName = "Military Coup",
                Description = "The military has seized control of the government in a sudden coup. " +
                             "The nation faces severe instability as the new regime consolidates power.",
                Severity = DisasterSeverity.Major,
                DurationTurns = 3,
                Effects = new List<EventEffect>
                {
                    new EventEffect("stability_change", "", -35f, 3, "Government overthrow causes instability"),
                    new EventEffect("morale_change", "", -15f, 2, "Population unsettled by coup"),
                    new EventEffect("diplomatic_change", "", -20f, 3, "International condemnation")
                }
            });

            // --- 9. REFUGEE CRISIS ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_REFUGEES",
                Type = WorldEventType.RefugeeCrisis,
                EventName = "Refugee Crisis",
                Description = "Massive numbers of refugees are fleeing from a neighboring region, " +
                             "straining resources and destabilizing host communities.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 4,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("stability_change", "", -10f, 4, "Social strain from refugee influx"),
                    new EventEffect("resource_modifier", "food", -0.10f, 3, "Food supplies strained"),
                    new EventEffect("population_change", "", 0.03f, 2, "Population increases from refugees"),
                    new EventEffect("morale_change", "", -5f, 2, "Tension from cultural displacement")
                }
            });

            // --- 10. ECONOMIC RECESSION ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_RECESSION",
                Type = WorldEventType.EconomicCrisis,
                EventName = "Global Recession",
                Description = "The global economy has entered a recession. " +
                             "Resource prices are rising and markets are volatile.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 5,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("resource_price_modifier", "", 0.20f, 5, "Global resource prices increase 20%"),
                    new EventEffect("treasury_change", "", -0.10f, 3, "Tax revenues decline"),
                    new EventEffect("production_modifier", "", -0.10f, 4, "Economic slowdown reduces output"),
                    new EventEffect("market_volatility", "", 0.30f, 5, "Market volatility increases")
                }
            });

            // --- 11. STOCK MARKET CRASH ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_MARKET_CRASH",
                Type = WorldEventType.EconomicCrisis,
                EventName = "Stock Market Crash",
                Description = "Financial markets have suffered a catastrophic crash. " +
                             "Treasury reserves are wiped out and economic panic ensues.",
                Severity = DisasterSeverity.Major,
                DurationTurns = 3,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("treasury_change", "", -0.30f, 1, "Treasury loses 30% of value"),
                    new EventEffect("market_volatility", "", 0.50f, 3, "Extreme market volatility"),
                    new EventEffect("morale_change", "", -15f, 2, "Economic panic"),
                    new EventEffect("production_modifier", "", -0.15f, 2, "Investment freeze hurts production")
                }
            });

            // --- 12. RESOURCE DISCOVERY ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_RESOURCE_DISCOVERY",
                Type = WorldEventType.ResourceDiscovery,
                EventName = "Resource Discovery",
                Description = "A significant new resource deposit has been discovered! " +
                             "This could shift the balance of power in the region.",
                Severity = DisasterSeverity.Minor,
                DurationTurns = 1,
                Effects = new List<EventEffect>
                {
                    new EventEffect("resource_modifier", "generic", 0.20f, -1, "New resource supply +20%"),
                    new EventEffect("morale_change", "", 10f, 2, "Morale boost from discovery"),
                    new EventEffect("diplomatic_tension", "", 15f, 3, "Discovery may cause territorial dispute")
                }
            });

            // --- 13. TECH BREAKTHROUGH ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_TECH_BREAKTHROUGH",
                Type = WorldEventType.TechBreakthrough,
                EventName = "Technological Breakthrough",
                Description = "A random nation has achieved a major technological breakthrough, " +
                             "gaining free research progress toward an advanced technology.",
                Severity = DisasterSeverity.Minor,
                DurationTurns = 1,
                Effects = new List<EventEffect>
                {
                    new EventEffect("research_boost", "", 0.30f, 1, "Research progress boosted by 30%"),
                    new EventEffect("morale_change", "", 15f, 2, "National pride from achievement")
                }
            });

            // --- 14. TRADE EMBARGO ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_EMBARGO",
                Type = WorldEventType.TradeDispute,
                EventName = "Trade Embargo",
                Description = "A trade embargo has been imposed between nations, " +
                             "severely disrupting commerce and supply chains.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 4,
                Effects = new List<EventEffect>
                {
                    new EventEffect("trade_income", "", -0.30f, 4, "Trade income reduced by 30%"),
                    new EventEffect("resource_modifier", "all", -0.15f, 4, "Resource shortages from embargo"),
                    new EventEffect("diplomatic_change", "", -25f, 3, "Diplomatic relations deteriorate"),
                    new EventEffect("production_modifier", "", -0.10f, 3, "Supply chain disruption")
                }
            });

            // --- 15. PIRACY INCREASE ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_PIRACY",
                Type = WorldEventType.TradeDispute,
                EventName = "Surge in Piracy",
                Description = "Pirate activity has dramatically increased, threatening " +
                             "sea trade routes and coastal settlements.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 3,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("naval_disruption", "", -0.25f, 3, "Sea trade routes disrupted"),
                    new EventEffect("trade_income", "", -0.15f, 3, "Trade losses from piracy"),
                    new EventEffect("resource_modifier", "all", -0.08f, 2, "Shipping losses")
                }
            });

            // --- 16. NUCLEAR ACCIDENT ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_NUCLEAR",
                Type = WorldEventType.EnvironmentalDisaster,
                EventName = "Nuclear Accident",
                Description = "A catastrophic nuclear accident has occurred, releasing radiation " +
                             "into the surrounding area. International condemnation follows.",
                Severity = DisasterSeverity.Catastrophic,
                DurationTurns = 6,
                Effects = new List<EventEffect>
                {
                    new EventEffect("population_change", "", -0.10f, 3, "Radiation casualties"),
                    new EventEffect("terrain_change", "radiation", -1f, 6, "Radiation contamination"),
                    new EventEffect("production_modifier", "", -0.40f, 5, "Evacuation halts production"),
                    new EventEffect("diplomatic_change", "", -30f, 4, "International condemnation"),
                    new EventEffect("morale_change", "", -35f, 4, "Nuclear panic")
                }
            });

            // --- 17. OIL SPILL ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_OIL_SPILL",
                Type = WorldEventType.EnvironmentalDisaster,
                EventName = "Oil Spill",
                Description = "A massive oil spill has devastated coastal waters, " +
                             "damaging marine ecosystems and coastal economies.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 4,
                Effects = new List<EventEffect>
                {
                    new EventEffect("terrain_change", "water", -1f, 4, "Coastal waters contaminated"),
                    new EventEffect("production_modifier", "", -0.10f, 3, "Fishing and coastal industry damaged"),
                    new EventEffect("resource_modifier", "oil", -0.15f, 2, "Oil supply disrupted"),
                    new EventEffect("diplomatic_change", "", -10f, 2, "Environmental blame")
                }
            });

            // --- 18. CYBER ATTACK ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_CYBER",
                Type = WorldEventType.EnvironmentalDisaster,
                EventName = "Cyber Attack",
                Description = "A sophisticated cyber attack has disabled critical infrastructure, " +
                             "causing widespread disruption to communications and logistics.",
                Severity = DisasterSeverity.Major,
                DurationTurns = 2,
                Effects = new List<EventEffect>
                {
                    new EventEffect("production_modifier", "", -0.20f, 2, "Infrastructure disabled by cyber attack"),
                    new EventEffect("military_readiness", "", -0.15f, 2, "Communications disrupted"),
                    new EventEffect("research_progress", "", -0.10f, 1, "Research data corrupted")
                }
            });

            // --- 19. ARMS RACE ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_ARMS_RACE",
                Type = WorldEventType.TradeDispute,
                EventName = "Arms Race Escalation",
                Description = "Nations are rapidly expanding their military capabilities, " +
                             "driving up tensions and defense spending across the globe.",
                Severity = DisasterSeverity.Moderate,
                DurationTurns = 4,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("tension_increase", "", 20f, 4, "Global tension increases"),
                    new EventEffect("treasury_change", "", -0.10f, 4, "Defense spending increases"),
                    new EventEffect("military_readiness", "", 0.15f, 3, "Military capabilities improve"),
                    new EventEffect("diplomatic_change", "", -10f, 3, "Trust between nations erodes")
                }
            });

            // --- 20. PEACE SUMMIT ---
            _eventTemplates.Add(new WorldEvent
            {
                EventId = "TEMPLATE_PEACE_SUMMIT",
                Type = WorldEventType.TradeDispute,
                EventName = "Peace Summit",
                Description = "World leaders have convened a peace summit, leading to " +
                             "improved diplomatic relations and reduced global tensions.",
                Severity = DisasterSeverity.Minor,
                DurationTurns = 3,
                IsGlobal = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("tension_decrease", "", -15f, 3, "Global tensions decrease"),
                    new EventEffect("diplomatic_change", "", 15f, 3, "Diplomatic relations improve"),
                    new EventEffect("morale_change", "", 10f, 2, "Hope for peace boosts morale"),
                    new EventEffect("trade_income", "", 0.10f, 2, "New trade opportunities emerge")
                }
            });
        }

        // ====================================================================
        // RANDOM EVENT ROLLING
        // ====================================================================

        /// <summary>
        /// Attempts to roll a random event based on probability and game state.
        /// Respects minimum turn spacing and maximum active event limits.
        /// </summary>
        /// <param name="state">Current game state snapshot.</param>
        /// <returns>The rolled WorldEvent, or null if no event was triggered.</returns>
        public WorldEvent RollRandomEvent()
        {
            GameState state = new GameState
            {
                CurrentTurn = _currentTurn,
                AverageStability = 70f,
                AverageEconomy = 70f,
                GlobalTension = 20f
            };
            return RollRandomEvent(state);
        }

        /// <summary>
        /// Attempts to roll a random event based on probability and game state.
        /// Respects minimum turn spacing and maximum active event limits.
        /// </summary>
        /// <param name="state">Current game state snapshot for probability weighting.</param>
        /// <returns>The rolled WorldEvent, or null if no event was triggered.</returns>
        public WorldEvent RollRandomEvent(GameState state)
        {
            if (_activeEvents.Count >= maxActiveEvents)
            {
                Debug.Log("[WorldEventManager] Max active events reached. No new event rolled.");
                return null;
            }

            if (_currentTurn - _lastEventTurn < minTurnsBetweenEvents)
            {
                return null;
            }

            if (UnityEngine.Random.value > baseEventChancePerTurn)
            {
                return null;
            }

            return SelectWeightedEvent(state);
        }

        /// <summary>
        /// Internal method that attempts to roll and trigger a random event.
        /// </summary>
        private void TryRollRandomEvent(GameState state)
        {
            WorldEvent evt = RollRandomEvent(state);
            if (evt != null)
            {
                ApplyEvent(evt);
            }
        }

        /// <summary>
        /// Selects an event template using probability weighting based on game state.
        /// Events whose conditions match the current game state are more likely.
        /// </summary>
        private WorldEvent SelectWeightedEvent(GameState state)
        {
            // Build weighted probability list
            List<float> weights = new List<float>();
            foreach (WorldEvent template in _eventTemplates)
            {
                float weight = CalculateEventProbability(template.Type, state);

                // Reduce weight if a similar event type is already active
                if (_activeEvents.Any(e => e.Type == template.Type))
                {
                    weight *= 0.2f; // Strongly discourage duplicate event types
                }

                weights.Add(weight);
            }

            // Weighted random selection
            float totalWeight = weights.Sum();
            if (totalWeight <= 0f)
                return null;

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < _eventTemplates.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    return CreateEventInstance(_eventTemplates[i]);
                }
            }

            // Fallback: return last template
            return CreateEventInstance(_eventTemplates[_eventTemplates.Count - 1]);
        }

        /// <summary>
        /// Creates a concrete event instance from a template, assigning unique IDs
        /// and resolving affected targets.
        /// </summary>
        private WorldEvent CreateEventInstance(WorldEvent template)
        {
            _eventIdCounter++;
            WorldEvent instance = new WorldEvent
            {
                EventId = $"EVT_{_eventIdCounter:D4}",
                Type = template.Type,
                EventName = template.EventName,
                Description = template.Description,
                Severity = template.Severity,
                AffectedRegionId = template.AffectedRegionId,
                AffectedNationId = template.AffectedNationId,
                DurationTurns = template.DurationTurns,
                TurnsRemaining = template.DurationTurns,
                IsGlobal = template.IsGlobal,
                TriggeredOnTurn = _currentTurn,
                EventIcon = template.EventIcon,
                IsActive = true
            };

            // Copy effects with severity scaling
            foreach (EventEffect templateEffect in template.Effects)
            {
                float scaledValue = templateEffect.Value * template.SeverityMultiplier * globalEffectMultiplier;
                instance.Effects.Add(new EventEffect(
                    templateEffect.EffectType,
                    templateEffect.TargetId,
                    scaledValue,
                    templateEffect.Duration,
                    templateEffect.Description
                ));
            }

            // If event needs a target nation/region but doesn't have one, assign randomly
            if (!instance.IsGlobal && string.IsNullOrEmpty(instance.AffectedNationId))
            {
                instance.AffectedNationId = GetRandomNationId();
                instance.AffectedRegionId = GetRandomRegionId();
                // Update effect targets
                foreach (EventEffect effect in instance.Effects)
                {
                    if (string.IsNullOrEmpty(effect.TargetId))
                    {
                        effect.TargetId = instance.AffectedNationId;
                    }
                }
            }

            return instance;
        }

        // ====================================================================
        // EVENT APPLICATION AND LIFECYCLE
        // ====================================================================

        /// <summary>
        /// Applies all effects of a world event to the game state and adds it
        /// to the active events list.
        /// </summary>
        /// <param name="evt">The world event to apply.</param>
        public void ApplyEvent(WorldEvent evt)
        {
            if (evt == null)
            {
                Debug.LogWarning("[WorldEventManager] Cannot apply null event.");
                return;
            }

            Debug.Log($"[WorldEventManager] Applying event: {evt.EventName} (Severity: {evt.Severity}, " +
                       $"Target: {evt.AffectedNationId ?? "Global"}, Effects: {evt.Effects.Count})");

            // Apply each effect through the effects applier
            foreach (EventEffect effect in evt.Effects)
            {
                ApplySingleEffect(effect, evt);
            }

            evt.IsActive = true;
            _activeEvents.Add(evt);
            _lastEventTurn = _currentTurn;

            Debug.Log($"[WorldEventManager] Event '{evt.EventName}' is now active. " +
                       $"({evt.TurnsRemaining} turns remaining)");
            OnEventTriggered?.Invoke(evt);
        }

        /// <summary>
        /// Applies a single effect to the game state via the EventEffectsApplier.
        /// </summary>
        private void ApplySingleEffect(EventEffect effect, WorldEvent evt)
        {
            if (_effectsApplier == null)
            {
                Debug.LogWarning("[WorldEventManager] No EventEffectsApplier available. Effect logged only.");
                Debug.Log($"  Effect: {effect}");
                return;
            }

            string targetId = string.IsNullOrEmpty(effect.TargetId) ? evt.AffectedNationId : effect.TargetId;

            switch (effect.EffectType)
            {
                case "production_modifier":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "production");
                    break;
                case "population_change":
                    _effectsApplier.ApplyPopulationEffect(targetId, effect.Value * 100f);
                    break;
                case "morale_change":
                    _effectsApplier.ApplyMoraleEffect(targetId, effect.Value);
                    break;
                case "stability_change":
                    _effectsApplier.ApplyMoraleEffect(targetId, effect.Value);
                    break;
                case "treasury_change":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "treasury");
                    break;
                case "resource_modifier":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "resource");
                    break;
                case "resource_price_modifier":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "price");
                    break;
                case "trade_income":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "trade");
                    break;
                case "diplomatic_change":
                    if (evt.IsGlobal)
                    {
                        ApplyGlobalDiplomaticEffect(effect.Value);
                    }
                    break;
                case "diplomatic_tension":
                    // Tension is tracked globally
                    break;
                case "military_readiness":
                    _effectsApplier.ApplyMilitaryEffect(new HexCoord(0, 0), effect.Value);
                    break;
                case "naval_disruption":
                    _effectsApplier.ApplyMilitaryEffect(new HexCoord(0, 0), effect.Value);
                    break;
                case "movement_penalty":
                    // Applied globally as a modifier
                    break;
                case "market_volatility":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "volatility");
                    break;
                case "terrain_change":
                    _effectsApplier.ApplyTerrainEffect(
                        string.IsNullOrEmpty(evt.AffectedRegionId) ? new HexCoord(0, 0) : ParseRegionToHex(evt.AffectedRegionId),
                        TerrainType.Wasteland,
                        effect.Duration);
                    break;
                case "air_operations":
                    _effectsApplier.ApplyMilitaryEffect(new HexCoord(0, 0), effect.Value);
                    break;
                case "research_boost":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "research");
                    break;
                case "research_progress":
                    _effectsApplier.ApplyEconomicEffect(targetId, effect.Value, "research");
                    break;
                case "tension_increase":
                case "tension_decrease":
                    // Tension affects event probability, not direct game state
                    break;
                case "building_destroy":
                    // Handled via military/city damage
                    break;
                case "military_defection":
                    _effectsApplier.ApplyMilitaryEffect(new HexCoord(0, 0), effect.Value);
                    break;
                default:
                    Debug.Log($"[WorldEventManager] Unknown effect type: {effect.EffectType}. Logged only.");
                    break;
            }
        }

        /// <summary>
        /// Applies a diplomatic effect to all nation pairs.
        /// </summary>
        private void ApplyGlobalDiplomaticEffect(float amount)
        {
            string[] nations = GetActiveNationIds();
            for (int i = 0; i < nations.Length; i++)
            {
                for (int j = i + 1; j < nations.Length; j++)
                {
                    _effectsApplier.ApplyDiplomaticEffect(nations[i], nations[j], amount);
                }
            }
        }

        /// <summary>
        /// Updates all active events, decrementing their remaining turn counters.
        /// Expired events are removed and archived.
        /// </summary>
        public void UpdateActiveEvents()
        {
            List<WorldEvent> expired = new List<WorldEvent>();

            foreach (WorldEvent evt in _activeEvents)
            {
                evt.TurnsRemaining--;
                if (evt.TurnsRemaining <= 0)
                {
                    evt.IsActive = false;
                    expired.Add(evt);
                }
            }

            foreach (WorldEvent evt in expired)
            {
                _activeEvents.Remove(evt);
                _eventHistory.Add(evt);
                Debug.Log($"[WorldEventManager] Event expired: {evt.EventName}");
                OnEventExpired?.Invoke(evt);
            }
        }

        /// <summary>
        /// Gets all currently active world events.
        /// </summary>
        /// <returns>List of active WorldEvent objects.</returns>
        public List<WorldEvent> GetActiveEvents()
        {
            return new List<WorldEvent>(_activeEvents);
        }

        // ====================================================================
        // SPECIFIC EVENT TRIGGERS
        // ====================================================================

        /// <summary>
        /// Triggers a natural disaster in the specified region with the given severity.
        /// The disaster type (earthquake, flood, tsunami) is selected based on region context.
        /// </summary>
        /// <param name="region">The region ID where the disaster occurs.</param>
        /// <param name="severity">The severity of the disaster.</param>
        public void TriggerNaturalDisaster(string region, DisasterSeverity severity)
        {
            WorldEventType[] disasterTypes = {
                WorldEventType.NaturalDisaster,
                WorldEventType.NaturalDisaster,
                WorldEventType.EnvironmentalDisaster
            };

            WorldEvent template = _eventTemplates
                .Where(t => t.Type == WorldEventType.NaturalDisaster)
                .OrderBy(_ => UnityEngine.Random.value)
                .FirstOrDefault();

            if (template == null)
            {
                Debug.LogWarning("[WorldEventManager] No natural disaster template found.");
                return;
            }

            _eventIdCounter++;
            WorldEvent evt = CreateEventInstance(template);
            evt.EventId = $"EVT_DISASTER_{_eventIdCounter:D4}";
            evt.AffectedRegionId = region;
            evt.Severity = severity;
            evt.Description = $"A {severity.ToString().ToLower()} natural disaster has struck the {region} region. " +
                             "Emergency response teams are mobilizing.";

            ApplyEvent(evt);
        }

        /// <summary>
        /// Triggers a revolution in the specified nation. Only effective if the nation's
        /// stability is below 30. Cities may rebel, military units may defect, and
        /// production is halved until the crisis is resolved.
        /// </summary>
        /// <param name="nationId">The nation experiencing the revolution.</param>
        public void TriggerRevolution(string nationId)
        {
            float stability = GetNationStability(nationId);
            if (stability > 30f)
            {
                Debug.Log($"[WorldEventManager] Revolution trigger skipped for {nationId}: " +
                         $"stability too high ({stability:F0}).");
                return;
            }

            WorldEvent template = _eventTemplates.FirstOrDefault(t => t.EventId == "TEMPLATE_REVOLUTION");
            if (template == null)
            {
                Debug.LogWarning("[WorldEventManager] Revolution template not found.");
                return;
            }

            _eventIdCounter++;
            WorldEvent evt = CreateEventInstance(template);
            evt.EventId = $"EVT_REVOLUTION_{_eventIdCounter:D4}";
            evt.AffectedNationId = nationId;
            evt.Severity = stability < 15f ? DisasterSeverity.Catastrophic : DisasterSeverity.Major;
            evt.Description = $"A violent revolution has erupted in {nationId}! " +
                             "With stability at critical levels, cities are rebelling and the government " +
                             "struggles to maintain control. Other nations may see an opportunity to intervene.";

            // Update effect targets
            foreach (EventEffect effect in evt.Effects)
            {
                if (string.IsNullOrEmpty(effect.TargetId))
                    effect.TargetId = nationId;
            }

            ApplyEvent(evt);
        }

        /// <summary>
        /// Triggers an economic crisis affecting a region. All resource prices increase
        /// by 30-50%, market volatility doubles, and nations with weak economies may collapse.
        /// </summary>
        /// <param name="region">The region affected by the crisis.</param>
        public void TriggerEconomicCrisis(string region)
        {
            WorldEvent template = _eventTemplates.FirstOrDefault(t => t.Type == WorldEventType.EconomicCrisis);
            if (template == null)
            {
                Debug.LogWarning("[WorldEventManager] Economic crisis template not found.");
                return;
            }

            _eventIdCounter++;
            WorldEvent evt = CreateEventInstance(template);
            evt.EventId = $"EVT_ECONOMY_{_eventIdCounter:D4}";
            evt.AffectedRegionId = region;
            evt.AffectedNationId = GetNationIdForRegion(region);
            evt.Description = $"A severe economic crisis has engulfed the {region} region. " +
                             "Resource prices have skyrocketed, markets are in chaos, and " +
                             "nations with weak economies face potential collapse.";

            float priceIncrease = UnityEngine.Random.Range(0.30f, 0.50f);
            foreach (EventEffect effect in evt.Effects)
            {
                if (effect.EffectType == "resource_price_modifier")
                {
                    effect.Value = priceIncrease;
                }
                if (string.IsNullOrEmpty(effect.TargetId))
                    effect.TargetId = evt.AffectedNationId;
            }

            ApplyEvent(evt);
        }

        /// <summary>
        /// Triggers a resource discovery in the specified region. A new resource deposit
        /// is found, increasing supply by 20%. This may cause territorial disputes.
        /// </summary>
        /// <param name="region">The region where resources were discovered.</param>
        /// <param name="resourceType">The type of resource discovered (e.g., "oil", "iron", "rare_earth").</param>
        public void TriggerResourceDiscovery(string region, string resourceType)
        {
            WorldEvent template = _eventTemplates.FirstOrDefault(t => t.EventId == "TEMPLATE_RESOURCE_DISCOVERY");
            if (template == null)
            {
                Debug.LogWarning("[WorldEventManager] Resource discovery template not found.");
                return;
            }

            _eventIdCounter++;
            WorldEvent evt = CreateEventInstance(template);
            evt.EventId = $"EVT_RESOURCE_{_eventIdCounter:D4}";
            evt.AffectedRegionId = region;
            evt.AffectedNationId = GetNationIdForRegion(region);
            evt.Description = $"A significant deposit of {resourceType} has been discovered in the {region} region! " +
                             "This find could dramatically shift the balance of power and may spark " +
                             "territorial disputes among neighboring nations.";

            // Update resource effect to target the specific resource type
            foreach (EventEffect effect in evt.Effects)
            {
                if (effect.EffectType == "resource_modifier")
                {
                    effect.TargetId = resourceType;
                }
                if (string.IsNullOrEmpty(effect.TargetId) || effect.TargetId == "generic")
                {
                    effect.TargetId = evt.AffectedNationId;
                }
            }

            ApplyEvent(evt);
        }

        /// <summary>
        /// Triggers a trade dispute between two nations. The trade agreement is suspended,
        /// both nations lose trade income, and tensions may escalate to war.
        /// </summary>
        /// <param name="nation1">The first nation involved in the dispute.</param>
        /// <param name="nation2">The second nation involved in the dispute.</param>
        public void TriggerTradeDispute(string nation1, string nation2)
        {
            WorldEvent template = _eventTemplates.FirstOrDefault(t => t.Type == WorldEventType.TradeDispute);
            if (template == null)
            {
                Debug.LogWarning("[WorldEventManager] Trade dispute template not found.");
                return;
            }

            _eventIdCounter++;
            WorldEvent evt = CreateEventInstance(template);
            evt.EventId = $"EVT_TRADE_{_eventIdCounter:D4}";
            evt.AffectedNationId = nation1;
            evt.IsGlobal = false;
            evt.Description = $"A severe trade dispute has erupted between {nation1} and {nation2}. " +
                             "Trade agreements have been suspended, both nations are losing income, " +
                             "and the conflict threatens to escalate into open hostilities.";

            // Apply trade income loss to both nations
            evt.Effects.Add(new EventEffect("trade_income", nation1, -0.25f, 4,
                $"Trade income loss for {nation1}"));
            evt.Effects.Add(new EventEffect("trade_income", nation2, -0.25f, 4,
                $"Trade income loss for {nation2}"));
            evt.Effects.Add(new EventEffect("diplomatic_change", $"{nation1}_{nation2}", -30f, 4,
                $"Diplomatic relations between {nation1} and {nation2} plummet"));

            ApplyEvent(evt);
        }

        /// <summary>
        /// Triggers the assassination of a nation's leader. The nation suffers severe
        /// stability loss (-40) and morale drop (-30), a succession crisis ensues for 3 turns,
        /// and there is a chance of civil war.
        /// </summary>
        /// <param name="nationId">The nation whose leader is assassinated.</param>
        public void TriggerAssassination(string nationId)
        {
            _eventIdCounter++;
            WorldEvent evt = new WorldEvent
            {
                EventId = $"EVT_ASSASSINATION_{_eventIdCounter:D4}",
                Type = WorldEventType.Assassination,
                EventName = "Assassination",
                Description = $"The leader of {nationId} has been assassinated! The nation is thrown " +
                             "into chaos as a succession crisis unfolds. Stability plummets and there " +
                             "are fears of civil war.",
                Severity = DisasterSeverity.Catastrophic,
                AffectedNationId = nationId,
                DurationTurns = 3,
                TurnsRemaining = 3,
                IsGlobal = false,
                TriggeredOnTurn = _currentTurn,
                IsActive = true,
                Effects = new List<EventEffect>
                {
                    new EventEffect("stability_change", nationId, -40f, 3, "Leader assassinated - stability collapse"),
                    new EventEffect("morale_change", nationId, -30f, 3, "Morale devastated by leader's death"),
                    new EventEffect("production_modifier", nationId, -0.20f, 2, "Government paralysis reduces production"),
                    new EventEffect("diplomatic_change", nationId, -15f, 2, "International concern over instability")
                }
            };

            ApplyEvent(evt);

            // Check for civil war possibility
            float stability = GetNationStability(nationId);
            if (stability < 20f)
            {
                Debug.LogWarning($"[WorldEventManager] {nationId} at risk of civil war! (Stability: {stability:F0})");
                // Could chain into another revolution event
            }
        }

        /// <summary>
        /// Triggers an environmental disaster in the specified region. This could be
        /// a radiation leak, oil spill, or other ecological catastrophe with long-term
        /// terrain effects and diplomatic consequences.
        /// </summary>
        /// <param name="region">The region affected by the environmental disaster.</param>
        public void TriggerEnvironmentalDisaster(string region)
        {
            WorldEvent template = _eventTemplates.FirstOrDefault(t =>
                t.Type == WorldEventType.EnvironmentalDisaster && t.Severity >= DisasterSeverity.Major);

            if (template == null)
            {
                template = _eventTemplates.FirstOrDefault(t => t.Type == WorldEventType.EnvironmentalDisaster);
            }

            if (template == null)
            {
                Debug.LogWarning("[WorldEventManager] Environmental disaster template not found.");
                return;
            }

            _eventIdCounter++;
            WorldEvent evt = CreateEventInstance(template);
            evt.EventId = $"EVT_ENVDISASTER_{_eventIdCounter:D4}";
            evt.AffectedRegionId = region;
            evt.AffectedNationId = GetNationIdForRegion(region);
            evt.Description = $"A catastrophic environmental disaster has occurred in the {region} region. " +
                             "Long-term ecological damage is expected, and neighboring nations are " +
                             "demanding accountability.";

            foreach (EventEffect effect in evt.Effects)
            {
                if (string.IsNullOrEmpty(effect.TargetId))
                    effect.TargetId = evt.AffectedNationId;
            }

            ApplyEvent(evt);
        }

        // ====================================================================
        // EVENT PROBABILITY CALCULATION
        // ====================================================================

        /// <summary>
        /// Calculates the probability weight of a specific event type occurring
        /// based on the current game state. Higher values mean more likely.
        /// </summary>
        /// <param name="type">The event type to calculate probability for.</param>
        /// <param name="state">The current game state snapshot.</param>
        /// <returns>A probability weight (0.0 to 1.0+).</returns>
        public float CalculateEventProbability(WorldEventType type, GameState state)
        {
            float baseProbability = 0.1f; // Base weight for all events

            switch (type)
            {
                case WorldEventType.NaturalDisaster:
                    // Slightly more common early game, less impactful late game
                    baseProbability = 0.15f;
                    if (state.CurrentTurn > 50) baseProbability *= 0.7f;
                    break;

                case WorldEventType.Revolution:
                    // Much more likely when stability is low
                    baseProbability = Mathf.Lerp(0.02f, 0.30f, 1f - (state.AverageStability / 100f));
                    if (state.AverageStability < 40f) baseProbability *= 2f;
                    break;

                case WorldEventType.Pandemic:
                    // More likely with higher population and later in game
                    baseProbability = 0.05f + (state.TotalPopulation > 100000 ? 0.05f : 0f);
                    if (state.CurrentTurn > 30) baseProbability *= 1.5f;
                    break;

                case WorldEventType.EconomicCrisis:
                    // More likely when economy is weak
                    baseProbability = Mathf.Lerp(0.05f, 0.20f, 1f - (state.AverageEconomy / 100f));
                    if (state.IsWarActive) baseProbability *= 1.3f;
                    break;

                case WorldEventType.ResourceDiscovery:
                    // Positive event, more common early-mid game
                    baseProbability = 0.12f;
                    if (state.CurrentTurn < 10) baseProbability *= 1.5f;
                    if (state.CurrentTurn > 60) baseProbability *= 0.5f;
                    break;

                case WorldEventType.TechBreakthrough:
                    // More likely for nations investing in research
                    baseProbability = 0.08f;
                    if (state.CurrentTurn > 20) baseProbability *= 1.3f;
                    break;

                case WorldEventType.RefugeeCrisis:
                    // More likely during wars and disasters
                    baseProbability = 0.06f;
                    if (state.IsWarActive) baseProbability *= 2.5f;
                    if (_activeEvents.Any(e => e.Type == WorldEventType.NaturalDisaster))
                        baseProbability *= 2f;
                    break;

                case WorldEventType.TradeDispute:
                    // More likely with more trade agreements (more to disrupt)
                    baseProbability = 0.10f + (state.ActiveTradeAgreements * 0.02f);
                    if (state.GlobalTension > 50) baseProbability *= 1.5f;
                    break;

                case WorldEventType.Assassination:
                    // Rare, more likely during high tension
                    baseProbability = 0.04f;
                    if (state.GlobalTension > 60) baseProbability *= 2f;
                    if (state.IsWarActive) baseProbability *= 1.5f;
                    break;

                case WorldEventType.EnvironmentalDisaster:
                    // More likely with nuclear facilities
                    baseProbability = 0.05f + (state.NuclearFacilities * 0.02f);
                    if (state.CurrentTurn > 40) baseProbability *= 1.3f;
                    break;
            }

            // Global tension multiplier affects all events
            if (state.GlobalTension > 50)
            {
                baseProbability *= 1f + ((state.GlobalTension - 50f) / 100f);
            }

            return baseProbability;
        }

        // ====================================================================
        // PRIVATE HELPERS
        // ====================================================================

        private HexCoord ParseRegionToHex(string regionId)
        {
            // TODO: Hook into RegionManager to get center hex of a region
            return new HexCoord(0, 0);
        }

        private float GetNationStability(string nationId)
        {
            // TODO: Hook into NationManager.GetStability(nationId)
            return 50f;
        }

        private string GetRandomNationId()
        {
            // TODO: Hook into NationManager.GetAllNationIds()
            string[] nations = { "Nation_A", "Nation_B", "Nation_C", "Nation_D" };
            return nations[UnityEngine.Random.Range(0, nations.Length)];
        }

        private string GetRandomRegionId()
        {
            // TODO: Hook into RegionManager.GetAllRegionIds()
            string[] regions = { "Region_North", "Region_South", "Region_East", "Region_West", "Region_Central" };
            return regions[UnityEngine.Random.Range(0, regions.Length)];
        }

        private string GetNationIdForRegion(string regionId)
        {
            // TODO: Hook into RegionManager.GetOwnerNation(regionId)
            return "Nation_A";
        }

        private string[] GetActiveNationIds()
        {
            // TODO: Hook into NationManager.GetAllNationIds()
            return new[] { "Nation_A", "Nation_B", "Nation_C", "Nation_D" };
        }
    }

    // ========================================================================
    // TERRAIN TYPE ENUM (placeholder for HexMap integration)
    // ========================================================================

    /// <summary>
    /// Terrain types used for environmental disaster effects.
    /// This should align with the IronProtocol.HexMap.TerrainType enum.
    /// </summary>
    public enum TerrainType
    {
        Plains,
        Forest,
        Mountain,
        Desert,
        Water,
        Wasteland,
        Tundra,
        Swamp,
        Hills,
        Coastal
    }
}
