// ============================================================================
// Iron Protocol - Event Effects Applier
// Bridges world events to other game systems, applying concrete effects
// to economy, military, diplomacy, terrain, population, and morale.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.WorldEvents
{
    // ========================================================================
    // EFFECT TRACKING
    // ========================================================================

    /// <summary>
    /// Represents a timed modifier applied to a game entity by a world event.
    /// Tracks the effect type, remaining duration, and the originating event.
    /// </summary>
    [Serializable]
    public class ActiveEffect
    {
        /// <summary>Unique ID for this active effect instance.</summary>
        public string EffectInstanceId { get; set; }

        /// <summary>The type of effect (e.g., "production_modifier", "stability_change").</summary>
        public string EffectType { get; set; }

        /// <summary>The target entity ID (nation, city, hex, etc.).</summary>
        public string TargetId { get; set; }

        /// <summary>The magnitude of the effect (positive or negative).</summary>
        public float Value { get; set; }

        /// <summary>Remaining turns before this effect expires.</summary>
        public int TurnsRemaining { get; set; }

        /// <summary>Total duration in turns (for reference).</summary>
        public int TotalDuration { get; set; }

        /// <summary>The world event ID that created this effect.</summary>
        public string SourceEventId { get; set; }

        /// <summary>Whether this effect has already been applied and is now ticking down.</summary>
        public bool IsApplied { get; set; }

        /// <summary>Whether this effect repeats each turn (true) or is one-time (false).</summary>
        public bool IsRecurring { get; set; }

        /// <summary>
        /// Creates a new active effect with full metadata.
        /// </summary>
        public ActiveEffect(string effectType, string targetId, float value,
                           int duration, string sourceEventId, bool isRecurring = false)
        {
            EffectInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            EffectType = effectType;
            TargetId = targetId;
            Value = value;
            TurnsRemaining = duration;
            TotalDuration = duration;
            SourceEventId = sourceEventId;
            IsApplied = false;
            IsRecurring = isRecurring;
        }

        /// <summary>
        /// Returns a formatted summary for debugging and UI display.
        /// </summary>
        public override string ToString()
        {
            string sign = Value >= 0 ? "+" : "";
            string recurStr = IsRecurring ? " [RECURRING]" : "";
            return $"{EffectType} on {TargetId}: {sign}{Value}{recurStr} ({TurnsRemaining} turns left)";
        }
    }

    // ========================================================================
    // EVENT EFFECTS APPLIER (MONOBEHAVIOUR)
    // ========================================================================

    /// <summary>
    /// Applies world event effects to the various game systems. Acts as a bridge
    /// between the <see cref="WorldEventManager"/> and systems like EconomyManager,
    /// MilitaryManager, DiplomacyManager, CityManager, and NationManager.
    /// 
    /// <para>This component tracks all active timed effects and processes their
    /// expiry each turn. Instant effects are applied immediately.</para>
    /// 
    /// <para>Integration: The WorldEventManager automatically creates this component
    /// if not already present. All effect application methods contain TODO hooks
    /// for connecting to actual game system implementations.</para>
    /// </summary>
    public class EventEffectsApplier : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // INSPECTOR CONFIGURATION
        // --------------------------------------------------------------------

        [Header("Effect Modifiers")]
        [Tooltip("Global multiplier for all economic effects.")]
        [SerializeField] [Range(0.5f, 2f)] private float economicEffectMultiplier = 1f;

        [Tooltip("Global multiplier for all military effects.")]
        [SerializeField] [Range(0.5f, 2f)] private float militaryEffectMultiplier = 1f;

        [Tooltip("Global multiplier for all diplomatic effects.")]
        [SerializeField] [Range(0.5f, 2f)] private float diplomaticEffectMultiplier = 1f;

        [Tooltip("Global multiplier for all population effects.")]
        [SerializeField] [Range(0.5f, 2f)] private float populationEffectMultiplier = 1f;

        [Tooltip("Global multiplier for all morale/stability effects.")]
        [SerializeField] [Range(0.5f, 2f)] private float moraleEffectMultiplier = 1f;

        [Header("Limits")]
        [Tooltip("Maximum number of simultaneous active tracked effects.")]
        [SerializeField] private int maxTrackedEffects = 200;

        // --------------------------------------------------------------------
        // PRIVATE STATE
        // --------------------------------------------------------------------

        /// <summary>All currently active timed effects being tracked.</summary>
        private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

        /// <summary>One-time effects pending application this turn.</summary>
        private readonly List<ActiveEffect> _pendingInstantEffects = new List<ActiveEffect>();

        /// <summary>Effects that expired this turn (for event notification). </summary>
        private readonly List<ActiveEffect> _expiredEffects = new List<ActiveEffect>();

        // --------------------------------------------------------------------
        // EVENTS
        // --------------------------------------------------------------------

        /// <summary>
        /// Fired when an effect is applied to the game state.
        /// </summary>
        public event Action<ActiveEffect> OnEffectApplied;

        /// <summary>
        /// Fired when a timed effect expires and is removed.
        /// </summary>
        public event Action<ActiveEffect> OnEffectExpired;

        // --------------------------------------------------------------------
        // PROPERTIES
        // --------------------------------------------------------------------

        /// <summary>Gets the current number of active tracked effects.</summary>
        public int ActiveEffectCount => _activeEffects.Count;

        /// <summary>Gets a read-only view of all active effects.</summary>
        public IReadOnlyList<ActiveEffect> ActiveEffects => _activeEffects.AsReadOnly();

        // --------------------------------------------------------------------
        // UNITY LIFECYCLE
        // --------------------------------------------------------------------

        /// <summary>
        /// Initializes the effects applier.
        /// </summary>
        private void Awake()
        {
            Debug.Log("[EventEffectsApplier] System initialized. Ready to apply event effects.");
        }

        // ====================================================================
        // TURN PROCESSING
        // ====================================================================

        /// <summary>
        /// Processes one turn of effect lifecycle:
        /// 1. Applies all pending instant effects.
        /// 2. Ticks down all active recurring effects.
        /// 3. Removes expired effects.
        /// Call this once per turn from the turn manager.
        /// </summary>
        public void ProcessTurn()
        {
            // Apply pending instant effects
            foreach (ActiveEffect instant in _pendingInstantEffects)
            {
                ApplyEffectToSystem(instant);
            }
            _pendingInstantEffects.Clear();

            // Process recurring effects (apply them this turn) and tick down
            List<ActiveEffect> toRemove = new List<ActiveEffect>();

            foreach (ActiveEffect effect in _activeEffects)
            {
                if (effect.IsRecurring && effect.TurnsRemaining > 0)
                {
                    ApplyEffectToSystem(effect);
                }

                effect.TurnsRemaining--;

                if (effect.TurnsRemaining <= 0)
                {
                    toRemove.Add(effect);
                }
            }

            // Remove expired effects
            foreach (ActiveEffect expired in toRemove)
            {
                _activeEffects.Remove(expired);
                _expiredEffects.Add(expired);
                OnEffectExpired?.Invoke(expired);
                Debug.Log($"[EventEffectsApplier] Effect expired: {expired}");
            }

            _expiredEffects.Clear();
        }

        // ====================================================================
        // ECONOMIC EFFECTS
        // ====================================================================

        /// <summary>
        /// Applies an economic effect to a nation's economy.
        /// Handles production modifiers, treasury changes, resource modifications,
        /// trade income changes, market volatility, and research boosts.
        /// </summary>
        /// <param name="nationId">The nation affected by the economic effect.</param>
        /// <param name="amount">The magnitude of the effect (e.g., -0.20 for 20% reduction).</param>
        /// <param name="type">
        /// The specific economic sub-type:
        /// <list type="bullet">
        ///   <item><c>"production"</c> - Modifies city production output.</item>
        ///   <item><c>"treasury"</c> - Adds/removes treasury funds (percentage-based).</item>
        ///   <item><c>"resource"</c> - Modifies resource stockpiles.</item>
        ///   <item><c>"price"</c> - Modifies global resource prices.</item>
        ///   <item><c>"trade"</c> - Modifies trade income.</item>
        ///   <item><c>"volatility"</c> - Modifies market volatility.</item>
        ///   <item><c>"research"</c> - Modifies research progress.</item>
        /// </list>
        /// </param>
        public void ApplyEconomicEffect(string nationId, float amount, string type)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[EventEffectsApplier] Cannot apply economic effect: nationId is null.");
                return;
            }

            float scaledAmount = amount * economicEffectMultiplier;

            switch (type.ToLowerInvariant())
            {
                case "production":
                    ApplyProductionEffect(nationId, scaledAmount);
                    break;

                case "treasury":
                    ApplyTreasuryEffect(nationId, scaledAmount);
                    break;

                case "resource":
                    ApplyResourceEffect(nationId, scaledAmount);
                    break;

                case "price":
                    ApplyResourcePriceEffect(nationId, scaledAmount);
                    break;

                case "trade":
                    ApplyTradeEffect(nationId, scaledAmount);
                    break;

                case "volatility":
                    ApplyVolatilityEffect(nationId, scaledAmount);
                    break;

                case "research":
                    ApplyResearchEffect(nationId, scaledAmount);
                    break;

                default:
                    Debug.LogWarning($"[EventEffectsApplier] Unknown economic effect type: {type}");
                    break;
            }
        }

        // --------------------------------------------------------------------
        // ECONOMIC SUB-EFFECT IMPLEMENTATIONS
        // --------------------------------------------------------------------

        /// <summary>
        /// Applies a production modifier to all cities in a nation.
        /// Positive values increase output, negative values decrease it.
        /// </summary>
        private void ApplyProductionEffect(string nationId, float amount)
        {
            // TODO: Hook into CityManager.GetCities(nationId) and apply production modifier
            // Example: foreach (city in cities) city.ProductionModifier += amount;
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: Production {amount:+P0} " +
                     $"(scaled: {amount * economicEffectMultiplier:+P0})");

            TrackEffect("production_modifier", nationId, amount, 0, null, false);
        }

        /// <summary>
        /// Modifies a nation's treasury by a percentage of its current value.
        /// </summary>
        private void ApplyTreasuryEffect(string nationId, float amount)
        {
            // TODO: Hook into EconomyManager.ModifyTreasury(nationId, amount)
            // Example: float current = EconomyManager.GetTreasury(nationId);
            //          EconomyManager.SetTreasury(nationId, current * (1 + amount));
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: Treasury {amount:+P0}");

            TrackEffect("treasury_change", nationId, amount, 0, null, false);
        }

        /// <summary>
        /// Modifies resource stockpiles for a nation across all resource types.
        /// </summary>
        private void ApplyResourceEffect(string nationId, float amount)
        {
            // TODO: Hook into ResourceManager.ModifyAllResources(nationId, amount)
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: All resources {amount:+P0}");

            TrackEffect("resource_modifier", nationId, amount, 0, null, false);
        }

        /// <summary>
        /// Modifies the global price of resources (affects trade value).
        /// </summary>
        private void ApplyResourcePriceEffect(string nationId, float amount)
        {
            // TODO: Hook into MarketManager.ModifyPriceMultiplier(nationId, amount)
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: Resource prices {amount:+P0}");

            TrackEffect("resource_price", nationId, amount, 0, null, true);
        }

        /// <summary>
        /// Modifies trade income for a nation.
        /// </summary>
        private void ApplyTradeEffect(string nationId, float amount)
        {
            // TODO: Hook into TradeManager.ModifyTradeIncome(nationId, amount)
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: Trade income {amount:+P0}");

            TrackEffect("trade_income", nationId, amount, 0, null, true);
        }

        /// <summary>
        /// Modifies market volatility (affects price fluctuation magnitude).
        /// </summary>
        private void ApplyVolatilityEffect(string nationId, float amount)
        {
            // TODO: Hook into MarketManager.ModifyVolatility(nationId, amount)
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: Market volatility {amount:+P0}");

            TrackEffect("market_volatility", nationId, amount, 0, null, true);
        }

        /// <summary>
        /// Modifies research progress for a nation.
        /// </summary>
        private void ApplyResearchEffect(string nationId, float amount)
        {
            // TODO: Hook into TechManager.ModifyResearchProgress(nationId, amount)
            Debug.Log($"[EventEffectsApplier] ECONOMIC [{nationId}]: Research progress {amount:+P0}");

            TrackEffect("research_boost", nationId, amount, 0, null, false);
        }

        // ====================================================================
        // MILITARY EFFECTS
        // ====================================================================

        /// <summary>
        /// Applies a military effect at or near the specified hex coordinate.
        /// Handles damage to units, naval disruption, air operations, movement penalties,
        /// and military readiness changes.
        /// </summary>
        /// <param name="coord">The hex coordinate where the military effect is centered.</param>
        /// <param name="damage">
        /// The magnitude of the military effect:
        /// <list type="bullet">
        ///   <item>Negative values reduce military capabilities.</item>
        ///   <item>Positive values boost military readiness.</item>
        /// </list>
        /// </param>
        public void ApplyMilitaryEffect(HexCoord coord, float damage)
        {
            float scaledDamage = damage * militaryEffectMultiplier;

            // TODO: Hook into MilitaryManager to find and affect units near coord
            // Example: var nearbyUnits = MilitaryManager.GetUnitsInRadius(coord, effectRadius);
            //          foreach (unit in nearbyUnits) unit.ApplyDamage(scaledDamage * 100);

            Debug.Log($"[EventEffectsApplier] MILITARY at {coord}: Effect {scaledDamage:+P0} " +
                     $"(raw: {damage:+P0}, multiplier: {militaryEffectMultiplier})");

            TrackEffect("military_effect", $"HEX_{coord.Q}_{coord.R}", damage, 0, null, false);
        }

        /// <summary>
        /// Applies a military readiness change to a specific nation.
        /// Affects unit combat effectiveness, reinforcement speed, and supply efficiency.
        /// </summary>
        /// <param name="nationId">The nation whose military readiness is affected.</param>
        /// <param name="amount">Readiness change as a fraction (e.g., -0.20 for 20% reduction).</param>
        public void ApplyMilitaryReadinessEffect(string nationId, float amount)
        {
            float scaledAmount = amount * militaryEffectMultiplier;

            // TODO: Hook into MilitaryManager.SetReadinessModifier(nationId, scaledAmount)
            // Example: MilitaryManager.ModifyReadiness(nationId, scaledAmount);

            Debug.Log($"[EventEffectsApplier] MILITARY [{nationId}]: Readiness {scaledAmount:+P0}");
            TrackEffect("military_readiness", nationId, amount, 0, null, true);
        }

        /// <summary>
        /// Applies a unit defection effect. A percentage of the target nation's
        /// military units may switch allegiance.
        /// </summary>
        /// <param name="nationId">The nation whose units may defect.</param>
        /// <param name="defectionChance">Probability of each unit defecting (0-1).</param>
        public void ApplyDefectionEffect(string nationId, float defectionChance)
        {
            // TODO: Hook into MilitaryManager to roll defection for each unit
            // Example: var units = MilitaryManager.GetUnits(nationId);
            //          foreach (unit in units) { if (Random.value < defectionChance) unit.Defect(); }
            int estimatedDefections = Mathf.RoundToInt(defectionChance * 10); // Simulated
            Debug.Log($"[EventEffectsApplier] MILITARY [{nationId}]: Unit defection chance {defectionChance:P0} " +
                     $"(est. {estimatedDefections} units may defect)");
            TrackEffect("military_defection", nationId, defectionChance, 0, null, false);
        }

        // ====================================================================
        // DIPLOMATIC EFFECTS
        // ====================================================================

        /// <summary>
        /// Applies a diplomatic effect between two nations. Modifies their
        /// bilateral relationship score, which affects trade, alliances, and war likelihood.
        /// </summary>
        /// <param name="nation1">The first nation in the diplomatic relationship.</param>
        /// <param name="nation2">The second nation in the diplomatic relationship.</param>
        /// <param name="opinionChange">
        /// The change in diplomatic opinion:
        /// <list type="bullet">
        ///   <item>Positive values improve relations (friendly).</item>
        ///   <item>Negative values worsen relations (hostile).</item>
        /// </list>
        /// Typical range: -100 to +100.
        /// </param>
        public void ApplyDiplomaticEffect(string nation1, string nation2, float opinionChange)
        {
            if (string.IsNullOrEmpty(nation1) || string.IsNullOrEmpty(nation2))
            {
                Debug.LogWarning("[EventEffectsApplier] Cannot apply diplomatic effect: nation ID is null.");
                return;
            }

            float scaledChange = opinionChange * diplomaticEffectMultiplier;

            // TODO: Hook into DiplomacyManager.ModifyRelation(nation1, nation2, scaledChange)
            // Example: DiplomacyManager.ModifyOpinion(nation1, nation2, scaledChange);

            string direction = scaledChange >= 0 ? "improved" : "worsened";
            Debug.Log($"[EventEffectsApplier] DIPLOMACY [{nation1} <-> {nation2}]: " +
                     $"Relations {direction} by {Mathf.Abs(scaledChange):F0} points " +
                     $"(raw: {opinionChange:F0})");

            TrackEffect("diplomatic_change", $"{nation1}_{nation2}", opinionChange, 0, null, false);
        }

        /// <summary>
        /// Applies a global tension modifier. Affects event probability calculations
        /// and can influence all nations' diplomatic postures.
        /// </summary>
        /// <param name="amount">Tension change (-100 to +100).</param>
        public void ApplyGlobalTensionEffect(float amount)
        {
            // TODO: Hook into GameStateManager.ModifyGlobalTension(amount)
            Debug.Log($"[EventEffectsApplier] DIPLOMACY [GLOBAL]: Tension {amount:+F0}");
            TrackEffect("global_tension", "GLOBAL", amount, 0, null, false);
        }

        // ====================================================================
        // TERRAIN EFFECTS
        // ====================================================================

        /// <summary>
        /// Applies a terrain change effect at the specified hex coordinate.
        /// Temporarily or permanently changes the terrain type, affecting movement,
        /// production, and combat in the affected area.
        /// </summary>
        /// <param name="coord">The hex coordinate where terrain is affected.</param>
        /// <param name="newTerrain">The new terrain type to apply.</param>
        /// <param name="duration">
        /// Duration in turns for the terrain change:
        /// <list type="bullet">
        ///   <item><c>-1</c> = permanent change.</item>
        ///   <item><c>0</c> = instant (one-time visual effect only).</item>
        ///   <item><c>&gt; 0</c> = temporary change that reverts after duration expires.</item>
        /// </list>
        /// </param>
        public void ApplyTerrainEffect(HexCoord coord, TerrainType newTerrain, int duration)
        {
            // TODO: Hook into HexGrid.SetTerrainOverride(coord, newTerrain, duration)
            // Example: hexGrid.SetTemporaryTerrain(coord, newTerrain, duration);

            string durationStr = duration == -1 ? "PERMANENT"
                              : duration == 0 ? "INSTANT"
                              : $"{duration} turns";

            Debug.Log($"[EventEffectsApplier] TERRAIN at {coord}: Changed to {newTerrain} ({durationStr})");

            if (duration > 0)
            {
                TrackEffect("terrain_change", $"HEX_{coord.Q}_{coord.R}", 0f, duration, null, false);
            }
        }

        /// <summary>
        /// Applies a terrain change to a region (multiple hexes).
        /// </summary>
        /// <param name="regionId">The region to affect.</param>
        /// <param name="newTerrain">The new terrain type.</param>
        /// <param name="duration">Duration in turns (-1 for permanent).</param>
        public void ApplyRegionalTerrainEffect(string regionId, TerrainType newTerrain, int duration)
        {
            // TODO: Hook into RegionManager.GetHexes(regionId) and apply terrain change to each
            Debug.Log($"[EventEffectsApplier] TERRAIN [{regionId}]: Regional change to {newTerrain} " +
                     $"(duration: {duration})");

            if (duration > 0)
            {
                TrackEffect("terrain_change", regionId, 0f, duration, null, false);
            }
        }

        // ====================================================================
        // POPULATION EFFECTS
        // ====================================================================

        /// <summary>
        /// Applies a population change to a city. Positive values represent growth,
        /// negative values represent losses (from disasters, war, disease, etc.).
        /// </summary>
        /// <param name="cityId">The city whose population is affected.</param>
        /// <param name="percentChange">
        /// Population change as a percentage:
        /// <list type="bullet">
        ///   <item>Positive values (e.g., 5.0f) increase population by 5%.</item>
        ///   <item>Negative values (e.g., -10.0f) decrease population by 10%.</item>
        /// </list>
        /// </param>
        public void ApplyPopulationEffect(string cityId, float percentChange)
        {
            if (string.IsNullOrEmpty(cityId))
            {
                Debug.LogWarning("[EventEffectsApplier] Cannot apply population effect: cityId is null.");
                return;
            }

            float scaledChange = percentChange * populationEffectMultiplier;

            // TODO: Hook into CityManager.ModifyPopulation(cityId, scaledChange)
            // Example: float current = CityManager.GetPopulation(cityId);
            //          CityManager.SetPopulation(cityId, current * (1 + scaledChange / 100f));

            string changeStr = scaledChange >= 0 ? "grew" : "declined";
            Debug.Log($"[EventEffectsApplier] POPULATION [{cityId}]: Population {changeStr} by " +
                     $"{Mathf.Abs(scaledChange):F1}% (raw: {percentChange:F1}%)");

            TrackEffect("population_change", cityId, percentChange, 0, null, false);
        }

        /// <summary>
        /// Applies a population change to an entire nation across all its cities.
        /// </summary>
        /// <param name="nationId">The nation whose population is affected.</param>
        /// <param name="percentChange">Population change percentage applied to each city.</param>
        public void ApplyNationPopulationEffect(string nationId, float percentChange)
        {
            // TODO: Hook into CityManager.GetCities(nationId) and apply to each
            Debug.Log($"[EventEffectsApplier] POPULATION [{nationId}]: National population " +
                     $"change {percentChange:+F1}% across all cities");
            TrackEffect("population_change", nationId, percentChange, 0, null, false);
        }

        // ====================================================================
        // MORALE EFFECTS
        // ====================================================================

        /// <summary>
        /// Applies a morale change to a nation. Affects unit combat performance,
        /// city productivity, and population happiness.
        /// </summary>
        /// <param name="nationId">The nation whose morale is affected.</param>
        /// <param name="amount">
        /// Morale change amount:
        /// <list type="bullet">
        ///   <item>Positive values boost morale (victories, discoveries, peace).</item>
        ///   <item>Negative values reduce morale (defeats, disasters, crises).</item>
        /// </list>
        /// Typical range: -50 to +50.
        /// </param>
        public void ApplyMoraleEffect(string nationId, float amount)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[EventEffectsApplier] Cannot apply morale effect: nationId is null.");
                return;
            }

            float scaledAmount = amount * moraleEffectMultiplier;

            // TODO: Hook into NationManager.ModifyMorale(nationId, scaledAmount)
            // Example: NationManager.SetMorale(nationId, NationManager.GetMorale(nationId) + scaledAmount);

            string direction = scaledAmount >= 0 ? "boost" : "penalty";
            Debug.Log($"[EventEffectsApplier] MORALE [{nationId}]: Morale {direction} of " +
                     $"{Mathf.Abs(scaledAmount):F0} points (raw: {amount:F0})");

            TrackEffect("morale_change", nationId, amount, 0, null, false);
        }

        /// <summary>
        /// Applies a stability change to a nation. Low stability can trigger
        /// revolutions and other negative events.
        /// </summary>
        /// <param name="nationId">The nation whose stability is affected.</param>
        /// <param name="amount">
        /// Stability change amount. Negative values reduce stability:
        /// <list type="bullet">
        ///   <item>Stability below 30: revolution risk increases.</item>
        ///   <item>Stability below 15: civil war risk.</item>
        /// </list>
        /// Typical range: -50 to +50.
        /// </param>
        public void ApplyStabilityEffect(string nationId, float amount)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[EventEffectsApplier] Cannot apply stability effect: nationId is null.");
                return;
            }

            float scaledAmount = amount * moraleEffectMultiplier;

            // TODO: Hook into NationManager.ModifyStability(nationId, scaledAmount)
            // Example: NationManager.SetStability(nationId, Mathf.Clamp(
            //          NationManager.GetStability(nationId) + scaledAmount, 0, 100));

            string direction = scaledAmount >= 0 ? "improved" : "reduced";
            Debug.Log($"[EventEffectsApplier] STABILITY [{nationId}]: Stability {direction} by " +
                     $"{Mathf.Abs(scaledAmount):F0} points (raw: {amount:F0})");

            TrackEffect("stability_change", nationId, amount, 0, null, false);

            // Warning for critically low stability
            float currentStability = GetCurrentStability(nationId);
            if (currentStability < 30f)
            {
                Debug.LogWarning($"[EventEffectsApplier] WARNING: {nationId} stability critically low " +
                               $"({currentStability:F0}). Revolution risk elevated!");
            }
        }

        // ====================================================================
        // EFFECT TRACKING
        // ====================================================================

        /// <summary>
        /// Tracks an effect for lifecycle management (duration, expiry, cleanup).
        /// Effects with duration > 0 are added to the active effects list.
        /// Effects with duration 0 are treated as instant (applied once, not tracked).
        /// </summary>
        /// <param name="effectType">The type of effect.</param>
        /// <param name="targetId">The target entity.</param>
        /// <param name="value">The effect magnitude.</param>
        /// <param name="duration">Duration in turns (0=instant, -1=permanent).</param>
        /// <param name="sourceEventId">The originating event ID, if applicable.</param>
        /// <param name="isRecurring">Whether the effect repeats each turn.</param>
        private void TrackEffect(string effectType, string targetId, float value,
                                int duration, string sourceEventId, bool isRecurring)
        {
            if (duration == 0)
            {
                // Instant effect - apply immediately and don't track
                return;
            }

            if (_activeEffects.Count >= maxTrackedEffects)
            {
                Debug.LogWarning("[EventEffectsApplier] Max tracked effects reached. Oldest effect removed.");
                _activeEffects.RemoveAt(0);
            }

            ActiveEffect effect = new ActiveEffect(effectType, targetId, value, duration, sourceEventId, isRecurring);
            _activeEffects.Add(effect);

            OnEffectApplied?.Invoke(effect);
        }

        /// <summary>
        /// Applies a tracked effect to the actual game system.
        /// Called during turn processing for recurring effects.
        /// </summary>
        private void ApplyEffectToSystem(ActiveEffect effect)
        {
            // Route back to the appropriate system based on effect type
            switch (effect.EffectType)
            {
                case "production_modifier":
                    ApplyProductionEffect(effect.TargetId, effect.Value);
                    break;
                case "treasury_change":
                    ApplyTreasuryEffect(effect.TargetId, effect.Value);
                    break;
                case "resource_modifier":
                case "resource_price":
                    ApplyEconomicEffect(effect.TargetId, effect.Value, "resource");
                    break;
                case "trade_income":
                    ApplyTradeEffect(effect.TargetId, effect.Value);
                    break;
                case "market_volatility":
                    ApplyVolatilityEffect(effect.TargetId, effect.Value);
                    break;
                case "research_boost":
                    ApplyResearchEffect(effect.TargetId, effect.Value);
                    break;
                case "military_readiness":
                    ApplyMilitaryReadinessEffect(effect.TargetId, effect.Value);
                    break;
                case "diplomatic_change":
                    // Re-apply diplomatic opinion change (cumulative)
                    if (effect.TargetId.Contains("_"))
                    {
                        string[] nations = effect.TargetId.Split('_');
                        if (nations.Length >= 2)
                        {
                            ApplyDiplomaticEffect(nations[0], nations[1], effect.Value);
                        }
                    }
                    break;
                case "morale_change":
                    ApplyMoraleEffect(effect.TargetId, effect.Value);
                    break;
                case "stability_change":
                    ApplyStabilityEffect(effect.TargetId, effect.Value);
                    break;
                case "terrain_change":
                    // Terrain is already changed; just tracking duration for reversion
                    break;
                default:
                    Debug.Log($"[EventEffectsApplier] Recurring effect tick: {effect}");
                    break;
            }
        }

        // ====================================================================
        // EFFECT REMOVAL AND CLEANUP
        // ====================================================================

        /// <summary>
        /// Removes all effects originating from a specific event.
        /// Called when an event is manually cancelled or overridden.
        /// </summary>
        /// <param name="sourceEventId">The event ID whose effects should be removed.</param>
        public void RemoveEffectsBySource(string sourceEventId)
        {
            if (string.IsNullOrEmpty(sourceEventId))
                return;

            int removed = _activeEffects.RemoveAll(e => e.SourceEventId == sourceEventId);
            if (removed > 0)
            {
                Debug.Log($"[EventEffectsApplier] Removed {removed} effects from event {sourceEventId}.");
            }
        }

        /// <summary>
        /// Removes all effects targeting a specific entity (nation, city, etc.).
        /// </summary>
        /// <param name="targetId">The target entity ID.</param>
        public void RemoveEffectsByTarget(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return;

            int removed = _activeEffects.RemoveAll(e => e.TargetId == targetId);
            if (removed > 0)
            {
                Debug.Log($"[EventEffectsApplier] Removed {removed} effects targeting {targetId}.");
            }
        }

        /// <summary>
        /// Removes all effects of a specific type.
        /// </summary>
        /// <param name="effectType">The effect type to remove.</param>
        public void RemoveEffectsByType(string effectType)
        {
            if (string.IsNullOrEmpty(effectType))
                return;

            int removed = _activeEffects.RemoveAll(e => e.EffectType == effectType);
            if (removed > 0)
            {
                Debug.Log($"[EventEffectsApplier] Removed {removed} effects of type {effectType}.");
            }
        }

        /// <summary>
        /// Removes all active effects. Use with caution.
        /// </summary>
        public void ClearAllEffects()
        {
            int count = _activeEffects.Count;
            _activeEffects.Clear();
            _pendingInstantEffects.Clear();
            Debug.Log($"[EventEffectsApplier] Cleared all {count} active effects.");
        }

        /// <summary>
        /// Gets all active effects for a specific target entity.
        /// </summary>
        /// <param name="targetId">The target entity ID.</param>
        /// <returns>List of active effects targeting the entity.</returns>
        public List<ActiveEffect> GetEffectsForTarget(string targetId)
        {
            return _activeEffects.Where(e => e.TargetId == targetId).ToList();
        }

        /// <summary>
        /// Gets all active effects of a specific type.
        /// </summary>
        /// <param name="effectType">The effect type to filter by.</param>
        /// <returns>List of active effects matching the type.</returns>
        public List<ActiveEffect> GetEffectsByType(string effectType)
        {
            return _activeEffects.Where(e => e.EffectType == effectType).ToList();
        }

        /// <summary>
        /// Calculates the net effect value for a target and effect type.
        /// Useful for aggregating multiple modifiers (e.g., total production bonus).
        /// </summary>
        /// <param name="targetId">The target entity.</param>
        /// <param name="effectType">The effect type.</param>
        /// <returns>The sum of all matching effect values.</returns>
        public float GetNetEffectValue(string targetId, string effectType)
        {
            return _activeEffects
                .Where(e => e.TargetId == targetId && e.EffectType == effectType)
                .Sum(e => e.Value);
        }

        // ====================================================================
        // PRIVATE HELPERS
        // ====================================================================

        /// <summary>
        /// Gets the current stability of a nation for warning checks.
        /// </summary>
        private float GetCurrentStability(string nationId)
        {
            // TODO: Hook into NationManager.GetStability(nationId)
            return 50f;
        }
    }
}
