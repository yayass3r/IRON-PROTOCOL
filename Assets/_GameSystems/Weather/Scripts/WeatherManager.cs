// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: WeatherManager.cs
// Description: Dynamic weather system that affects combat modifiers each turn.
//              Supports weather forecasting, smooth transitions, and configurable
//              weather-specific combat modifiers.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.Weather
{
    /// <summary>
    /// Defines all possible weather conditions in the game.
    /// Each type has distinct effects on combat, movement, vision, and air operations.
    /// </summary>
    public enum WeatherType
    {
        /// <summary>Clear skies. No modifiers to any operations.</summary>
        Clear = 0,

        /// <summary>Rain reduces attack effectiveness and slows ground movement. Severely limits air ops.</summary>
        Rain = 1,

        /// <summary>Fog drastically reduces visibility and air operations. Slightly reduces attack power.</summary>
        Fog = 2,

        /// <summary>Snow slows ground movement significantly. Provides a slight defensive bonus.</summary>
        Snow = 3,

        /// <summary>Sandstorm grounds all air operations. Reduces vision and ground movement.</summary>
        Sandstorm = 4
    }

    /// <summary>
    /// Defines combat and operational modifiers for a specific weather type.
    /// All values are multipliers applied to base stats (1.0 = no change).
    /// Values below 1.0 represent penalties; values above 1.0 represent bonuses.
    /// </summary>
    [System.Serializable]
    public struct WeatherModifiers
    {
        /// <summary>Multiplier applied to attack power (e.g., 0.7 = 30% reduction).</summary>
        public float attackMod;

        /// <summary>Multiplier applied to defensive power (e.g., 0.9 = 10% reduction, 1.1 = 10% bonus).</summary>
        public float defenseMod;

        /// <summary>Multiplier applied to movement points (e.g., 0.8 = 20% reduction).</summary>
        public float movementMod;

        /// <summary>Multiplier applied to vision range (e.g., 0.5 = 50% reduction).</summary>
        public float visionMod;

        /// <summary>Multiplier applied to air operations effectiveness (e.g., 0.0 = grounded).</summary>
        public float airOpsMod;

        /// <summary>
        /// Creates WeatherModifiers with specified values.
        /// </summary>
        public WeatherModifiers(float attackMod, float defenseMod, float movementMod,
                                float visionMod, float airOpsMod)
        {
            this.attackMod = attackMod;
            this.defenseMod = defenseMod;
            this.movementMod = movementMod;
            this.visionMod = visionMod;
            this.airOpsMod = airOpsMod;
        }

        /// <summary>Weather with no modifiers (all 1.0).</summary>
        public static readonly WeatherModifiers Neutral = new WeatherModifiers(1f, 1f, 1f, 1f, 1f);

        /// <summary>Clamps all modifier values to [0.0, 2.0] range.</summary>
        public WeatherModifiers Clamped => new WeatherModifiers(
            Mathf.Clamp(attackMod, 0f, 2f),
            Mathf.Clamp(defenseMod, 0f, 2f),
            Mathf.Clamp(movementMod, 0f, 2f),
            Mathf.Clamp(visionMod, 0f, 2f),
            Mathf.Clamp(airOpsMod, 0f, 2f)
        );

        /// <summary>Formats modifiers as a readable string.</summary>
        public override string ToString() =>
            $"ATK:{attackMod:F1} DEF:{defenseMod:F1} MOV:{movementMod:F1} VIS:{visionMod:F1} AIR:{airOpsMod:F1}";
    }

    /// <summary>
    /// Represents a single entry in the weather forecast.
    /// </summary>
    [System.Serializable]
    public struct ForecastEntry
    {
        /// <summary>The predicted weather type.</summary>
        public WeatherType weather;

        /// <summary>How many turns from now this forecast applies.</summary>
        public int turnsAhead;

        /// <summary>Confidence of this forecast (0.0 to 1.0). Decreases for further predictions.</summary>
        public float confidence;

        public ForecastEntry(WeatherType weather, int turnsAhead, float confidence)
        {
            this.weather = weather;
            this.turnsAhead = turnsAhead;
            this.confidence = confidence;
        }
    }

    /// <summary>
    /// Delegate for weather change events.
    /// </summary>
    public delegate void WeatherChangedHandler(WeatherType oldWeather, WeatherType newWeather);

    /// <summary>
    /// MonoBehaviour managing the dynamic weather system.
    /// Weather changes every 1-3 turns with random selection weighted by configuration.
    /// Provides a 3-turn forecast for AI and player planning.
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Fired when the weather changes at the start of a new weather period.
        /// Parameters: old weather type, new weather type.
        /// </summary>
        public event WeatherChangedHandler OnWeatherChanged;

        /// <summary>Fired each turn when the weather countdown decreases (but hasn't changed yet).</summary>
        public event Action<WeatherType, int> OnWeatherCountdown;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Weather Timing")]
        [Tooltip("Minimum number of turns a weather condition lasts.")]
        [SerializeField, Range(1, 5)] private int minWeatherDuration = 1;

        [Tooltip("Maximum number of turns a weather condition lasts.")]
        [SerializeField, Range(1, 5)] private int maxWeatherDuration = 3;

        [Header("Weather Probabilities")]
        [Tooltip("Probability weights for each weather type. Higher = more likely. Must sum to 1.0.")]
        [SerializeField] private float clearProbability = 0.40f;
        [SerializeField] private float rainProbability = 0.25f;
        [SerializeField] private float fogProbability = 0.10f;
        [SerializeField] private float snowProbability = 0.10f;
        [SerializeField] private float sandstormProbability = 0.15f;

        [Header("Forecast Settings")]
        [Tooltip("Number of turns ahead to forecast.")]
        [SerializeField, Range(1, 5)] private int forecastTurns = 3;

        [Tooltip("Base confidence of forecast (1 turn ahead). Decreases for further forecasts.")]
        [SerializeField, Range(0.5f, 1f)] private float baseForecastConfidence = 0.9f;

        [Tooltip("Confidence decay rate per additional turn ahead.")]
        [SerializeField, Range(0.05f, 0.3f)] private float forecastConfidenceDecay = 0.15f;

        [Header("Initial State")]
        [Tooltip("Starting weather type for a new game.")]
        [SerializeField] private WeatherType initialWeather = WeatherType.Clear;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>Current active weather condition.</summary>
        private WeatherType _currentWeather;

        /// <summary>Weather type that will take effect when the current period ends.</summary>
        private WeatherType _forecastWeather;

        /// <summary>Turns remaining until the weather changes.</summary>
        private int _turnsUntilChange;

        /// <summary>Pre-computed forecast entries for UI and AI use.</summary>
        private ForecastEntry[] _cachedForecast;

        /// <summary>Track if forecast has been pre-generated.</summary>
        private bool _forecastGenerated;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Gets the current weather type.</summary>
        public WeatherType CurrentWeather => _currentWeather;

        /// <summary>Gets the weather type that will take effect next.</summary>
        public WeatherType ForecastWeather => _forecastWeather;

        /// <summary>Gets the number of turns until the next weather change.</summary>
        public int TurnsUntilChange => _turnsUntilChange;

        // --------------------------------------------------------------------- //
        // Unity Lifecycle
        // --------------------------------------------------------------------- //

        private void Awake()
        {
            // Initialize weather state
            _currentWeather = initialWeather;
            _forecastWeather = RollNewWeather();
            _turnsUntilChange = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration + 1);
            _cachedForecast = new ForecastEntry[forecastTurns];
            _forecastGenerated = false;

            Debug.Log($"[WeatherManager] Initialized. Current: {_currentWeather}, " +
                      $"Next: {_forecastWeather}, Changes in: {_turnsUntilChange} turns.");
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Weather Queries
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets the combat and operational modifiers for a specific weather type.
        /// Each weather type has unique effects on gameplay:
        /// <list type="table">
        ///   <listheader><term>Weather</term><description>Modifiers</description></listheader>
        ///   <item><term>Clear</term><description>All 1.0 (no effect)</description></item>
        ///   <item><term>Rain</term><description>ATK 0.7, MOV 0.8, AIR 0.5</description></item>
        ///   <item><term>Fog</term><description>VIS 0.5, ATK 0.8, AIR 0.3</description></item>
        ///   <item><term>Snow</term><description>MOV 0.6, DEF 0.9 (+10% bonus)</description></item>
        ///   <item><term>Sandstorm</term><description>AIR 0.0, MOV 0.7, VIS 0.3</description></item>
        /// </list>
        /// </summary>
        /// <param name="weather">The weather type to query.</param>
        /// <returns>A WeatherModifiers struct with all modifier values.</returns>
        public WeatherModifiers GetModifiers(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear:
                    return new WeatherModifiers(
                        attackMod: 1.0f,
                        defenseMod: 1.0f,
                        movementMod: 1.0f,
                        visionMod: 1.0f,
                        airOpsMod: 1.0f
                    );

                case WeatherType.Rain:
                    return new WeatherModifiers(
                        attackMod: 0.7f,
                        defenseMod: 1.0f,
                        movementMod: 0.8f,
                        visionMod: 0.9f,
                        airOpsMod: 0.5f
                    );

                case WeatherType.Fog:
                    return new WeatherModifiers(
                        attackMod: 0.8f,
                        defenseMod: 1.0f,
                        movementMod: 1.0f,
                        visionMod: 0.5f,
                        airOpsMod: 0.3f
                    );

                case WeatherType.Snow:
                    return new WeatherModifiers(
                        attackMod: 0.9f,
                        defenseMod: 1.1f,  // Defensive bonus from snow cover
                        movementMod: 0.6f,
                        visionMod: 0.8f,
                        airOpsMod: 0.6f
                    );

                case WeatherType.Sandstorm:
                    return new WeatherModifiers(
                        attackMod: 0.8f,
                        defenseMod: 0.9f,
                        movementMod: 0.7f,
                        visionMod: 0.3f,
                        airOpsMod: 0.0f   // Complete air operations shutdown
                    );

                default:
                    return WeatherModifiers.Neutral;
            }
        }

        /// <summary>
        /// Gets the modifiers for the current weather.
        /// Convenience method - equivalent to GetModifiers(CurrentWeather).
        /// </summary>
        /// <returns>WeatherModifiers for the current weather type.</returns>
        public WeatherModifiers GetCurrentModifiers()
        {
            return GetModifiers(_currentWeather);
        }

        /// <summary>
        /// Gets the weather forecast for the next several turns.
        /// Each entry includes the predicted weather, turns ahead, and confidence level.
        /// Confidence decreases for predictions further in the future.
        /// </summary>
        /// <returns>List of forecast entries ordered by turns ahead (1, 2, 3...).</returns>
        public List<(WeatherType weather, int turnsAhead)> GetForecast()
        {
            var result = new List<(WeatherType, int)>();

            // Turn 1: the forecast weather (already determined)
            if (_turnsUntilChange > 0)
            {
                result.Add((_currentWeather, 0)); // Current weather persists for remaining turns
            }

            // When current weather changes, the forecast weather takes over
            result.Add((_forecastWeather, _turnsUntilChange));

            // Beyond that, generate probable weather (with decreasing confidence)
            WeatherType simulatedWeather = _forecastWeather;
            for (int i = 1; i < forecastTurns - 1; i++)
            {
                // Generate random weather for extended forecast
                WeatherType nextWeather = RollNewWeather();
                int turnsAhead = _turnsUntilChange + i;
                result.Add((nextWeather, turnsAhead));
                simulatedWeather = nextWeather;
            }

            return result;
        }

        /// <summary>
        /// Gets the detailed forecast with confidence values for AI planning.
        /// </summary>
        /// <returns>Array of ForecastEntry structs with weather, turns ahead, and confidence.</returns>
        public ForecastEntry[] GetDetailedForecast()
        {
            if (!_forecastGenerated)
            {
                RegenerateForecast();
            }
            return _cachedForecast;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Turn Processing
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Advances the weather system by one turn.
        /// When the turn counter reaches zero, the forecast weather becomes current
        /// and a new forecast is generated.
        /// Call this once per game turn.
        /// </summary>
        /// <returns>True if the weather changed this turn.</returns>
        public bool AdvanceWeather()
        {
            _turnsUntilChange--;
            _forecastGenerated = false; // Invalidate cached forecast

            OnWeatherCountdown?.Invoke(_currentWeather, _turnsUntilChange);

            if (_turnsUntilChange <= 0)
            {
                // Change weather
                WeatherType oldWeather = _currentWeather;
                _currentWeather = _forecastWeather;
                _forecastWeather = RollNewWeather();
                _turnsUntilChange = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration + 1);

                OnWeatherChanged?.Invoke(oldWeather, _currentWeather);

                Debug.Log($"[WeatherManager] Weather changed: {oldWeather} -> {_currentWeather}. " +
                          $"Next change in {_turnsUntilChange} turns (forecast: {_forecastWeather}).");

                return true;
            }

            Debug.Log($"[WeatherManager] Weather: {_currentWeather}. Changes in {_turnsUntilChange} turns.");
            return false;
        }

        /// <summary>
        /// Forces an immediate weather change to a specific type.
        /// Resets the weather timer.
        /// </summary>
        /// <param name="weather">The weather type to set.</param>
        /// <param name="durationInTurns">How long this weather should last. 0 = random duration.</param>
        public void SetWeather(WeatherType weather, int durationInTurns = 0)
        {
            WeatherType oldWeather = _currentWeather;
            _currentWeather = weather;
            _forecastWeather = RollNewWeather();
            _turnsUntilChange = durationInTurns > 0
                ? durationInTurns
                : UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration + 1);

            _forecastGenerated = false;
            OnWeatherChanged?.Invoke(oldWeather, _currentWeather);

            Debug.Log($"[WeatherManager] Weather forced: {oldWeather} -> {_currentWeather} for {_turnsUntilChange} turns.");
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Utility
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets a human-readable display name for a weather type.
        /// </summary>
        /// <param name="weather">The weather type.</param>
        /// <returns>Display name string.</returns>
        public static string GetWeatherDisplayName(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear: return "Clear";
                case WeatherType.Rain: return "Rain";
                case WeatherType.Fog: return "Fog";
                case WeatherType.Snow: return "Snow";
                case WeatherType.Sandstorm: return "Sandstorm";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Gets a brief description of weather effects for UI tooltips.
        /// </summary>
        /// <param name="weather">The weather type.</param>
        /// <returns>Description string.</returns>
        public static string GetWeatherDescription(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear:
                    return "Clear skies. No combat modifiers.";
                case WeatherType.Rain:
                    return "Rain reduces attack power by 30%, ground movement by 20%, and air operations by 50%.";
                case WeatherType.Fog:
                    return "Fog halves vision range, reduces attack by 20%, and severely limits air operations (70% reduction).";
                case WeatherType.Snow:
                    return "Snow slows ground movement by 40%, but provides a 10% defensive bonus from snow cover.";
                case WeatherType.Sandstorm:
                    return "Sandstorms ground all air operations, reduce movement by 30%, and halve vision range.";
                default:
                    return "Unknown weather conditions.";
            }
        }

        // --------------------------------------------------------------------- //
        // Private Methods
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Rolls a new random weather type based on the configured probabilities.
        /// </summary>
        /// <returns>A randomly selected WeatherType.</returns>
        private WeatherType RollNewWeather()
        {
            float totalProbability = clearProbability + rainProbability + fogProbability +
                                     snowProbability + sandstormProbability;

            if (totalProbability <= 0f)
            {
                return WeatherType.Clear; // Fallback
            }

            float roll = UnityEngine.Random.value * totalProbability;
            float cumulative = 0f;

            cumulative += clearProbability;
            if (roll < cumulative) return WeatherType.Clear;

            cumulative += rainProbability;
            if (roll < cumulative) return WeatherType.Rain;

            cumulative += fogProbability;
            if (roll < cumulative) return WeatherType.Fog;

            cumulative += snowProbability;
            if (roll < cumulative) return WeatherType.Snow;

            cumulative += sandstormProbability;
            if (roll < cumulative) return WeatherType.Sandstorm;

            return WeatherType.Clear; // Default fallback
        }

        /// <summary>
        /// Regenerates the cached forecast entries.
        /// </summary>
        private void RegenerateForecast()
        {
            _cachedForecast = new ForecastEntry[forecastTurns];

            for (int i = 0; i < forecastTurns; i++)
            {
                int turnsAhead = i + 1;
                WeatherType predictedWeather;

                if (i == 0)
                {
                    // First forecast is the actual forecast weather
                    predictedWeather = _forecastWeather;
                }
                else
                {
                    // Extended forecasts are random (simulated)
                    predictedWeather = RollNewWeather();
                }

                float confidence = Mathf.Clamp(
                    baseForecastConfidence - (i * forecastConfidenceDecay),
                    0.1f, 1f
                );

                _cachedForecast[i] = new ForecastEntry(predictedWeather, turnsAhead, confidence);
            }

            _forecastGenerated = true;
        }
    }
}
