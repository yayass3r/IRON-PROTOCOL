// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: DynamicPricingEngine.cs
// Description: Market pricing engine with supply/demand dynamics, event shocks,
//              historical trend analysis, and EMA smoothing for resource prices.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.Economy
{
    /// <summary>
    /// Serializable data container representing a single tradable resource on the global market.
    /// Tracks price history, supply/demand levels, and volatility parameters.
    /// </summary>
    [System.Serializable]
    public class ResourceMarketData
    {
        /// <summary>Unique identifier for the resource (e.g. "oil", "steel", "uranium").</summary>
        public string resourceId;

        /// <summary>Human-readable display name for the resource.</summary>
        public string resourceName;

        /// <summary>Icon sprite used in UI panels.</summary>
        public Sprite icon;

        /// <summary>Base equilibrium price before any modifiers are applied.</summary>
        public float basePrice;

        /// <summary>Current market price after all calculations.</summary>
        public float currentPrice;

        /// <summary>Price from the previous turn, used for trend analysis.</summary>
        public float previousPrice;

        /// <summary>Maximum allowed price to prevent runaway inflation.</summary>
        public float priceCeiling;

        /// <summary>Minimum allowed price to prevent market collapse.</summary>
        public float priceFloor;

        /// <summary>Volatility factor from 0.0 (stable) to 1.0 (extremely volatile).</summary>
        public float volatility;

        /// <summary>Total global supply of this resource across all nations.</summary>
        public float globalSupply;

        /// <summary>Total global demand for this resource across all nations.</summary>
        public float globalDemand;

        /// <summary>Rolling price history, maintaining the last 20 turns of data.</summary>
        public List<float> priceHistory;

        /// <summary>
        /// Initializes a new ResourceMarketData with default values.
        /// </summary>
        public ResourceMarketData()
        {
            priceHistory = new List<float>();
            volatility = 0.1f;
            globalSupply = 100f;
            globalDemand = 100f;
        }
    }

    /// <summary>
    /// Represents an active economic shock event that modifies resource prices.
    /// Shocks decay over time according to their decay rate.
    /// </summary>
    [System.Serializable]
    public class MarketShock
    {
        /// <summary>Resource this shock applies to, or null/empty for global shocks.</summary>
        public string resourceId;

        /// <summary>Magnitude of the price shock. Positive = price increase, negative = decrease.</summary>
        public float magnitude;

        /// <summary>Remaining turns before the shock fully decays to zero.</summary>
        public float turnsRemaining;

        /// <summary>How many turns the shock was originally set to last.</summary>
        public float totalDuration;

        /// <summary>
        /// Calculates the current effective magnitude after decay.
        /// Decays linearly from full magnitude down to zero over the shock's lifetime.
        /// </summary>
        /// <returns>The current shock multiplier factor (1.0 + decayed magnitude).</returns>
        public float GetDecayedFactor()
        {
            if (totalDuration <= 0f) return 1f + magnitude;
            float decayRatio = Mathf.Max(0f, turnsRemaining / totalDuration);
            return 1f + (magnitude * decayRatio);
        }

        /// <summary>Reduces the shock's remaining lifetime by one turn.</summary>
        public void Decay()
        {
            turnsRemaining -= 1f;
        }

        /// <summary>Whether this shock has fully expired.</summary>
        public bool IsExpired => turnsRemaining <= 0f;
    }

    /// <summary>
    /// MonoBehaviour that manages the global resource market pricing engine.
    /// Recalculates prices each turn based on supply/demand, shocks, trends, and noise,
    /// then applies exponential moving average smoothing and price clamping.
    /// </summary>
    public class DynamicPricingEngine : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Events & Delegates
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Delegate fired whenever a resource's price changes significantly.
        /// Parameters: resourceId, oldPrice, newPrice.
        /// </summary>
        public delegate void PriceChangedHandler(string resourceId, float oldPrice, float newPrice);

        /// <summary>
        /// Event raised when any resource price changes after recalculation.
        /// Subscribe to respond to market fluctuations in real time.
        /// </summary>
        public event PriceChangedHandler OnPriceChanged;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Smoothing & Trend Settings")]
        [Tooltip("Exponential Moving Average smoothing factor. Higher = more responsive to change.")]
        [SerializeField, Range(0.05f, 0.5f)] private float emaSmoothingFactor = 0.3f;

        [Tooltip("Weight given to the 3-turn moving average trend when calculating prices.")]
        [SerializeField, Range(0f, 0.5f)] private float trendInfluence = 0.15f;

        [Header("Market Data")]
        [Tooltip("List of all tradable resources on the global market.")]
        [SerializeField] private List<ResourceMarketData> marketResources = new List<ResourceMarketData>();

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>Collection of all active market shock events.</summary>
        private readonly List<MarketShock> _activeShocks = new List<MarketShock>();

        /// <summary>Maximum number of price history entries retained per resource.</summary>
        private const int MaxHistoryLength = 20;

        // --------------------------------------------------------------------- //
        // Public Properties
        // --------------------------------------------------------------------- //

        /// <summary>Gets a read-only view of all market resource data.</summary>
        public IReadOnlyList<ResourceMarketData> MarketResources => marketResources.AsReadOnly();

        /// <summary>Gets a read-only view of all currently active market shocks.</summary>
        public IReadOnlyList<MarketShock> ActiveShocks => _activeShocks.AsReadOnly();

        // --------------------------------------------------------------------- //
        // Unity Lifecycle
        // --------------------------------------------------------------------- //

        private void Awake()
        {
            // Initialize price history for any resources that don't have one
            foreach (var resource in marketResources)
            {
                if (resource.priceHistory == null)
                    resource.priceHistory = new List<float>();

                // Seed history with base price if empty
                if (resource.priceHistory.Count == 0 && resource.currentPrice > 0f)
                    resource.priceHistory.Add(resource.currentPrice);
            }
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Turn Processing
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Recalculates all resource prices based on four weighted factors:
        /// <list type="number">
        ///   <item>Supply/Demand pressure (inverse ratio)</item>
        ///   <item>Active event shocks with temporal decay</item>
        ///   <item>Historical trend (3-turn moving average, 15% influence)</item>
        ///   <item>Random noise scaled by resource volatility</item>
        /// </list>
        /// Applies EMA smoothing (0.3 factor), clamps to floor/ceiling,
        /// and records the result to price history (last 20 turns kept).
        /// Should be called once per game turn.
        /// </summary>
        public void RecalculateAllPrices()
        {
            // First, decay and remove expired shocks
            for (int i = _activeShocks.Count - 1; i >= 0; i--)
            {
                _activeShocks[i].Decay();
                if (_activeShocks[i].IsExpired)
                    _activeShocks.RemoveAt(i);
            }

            foreach (var resource in marketResources)
            {
                float oldPrice = resource.currentPrice;

                // --- Factor 1: Supply/Demand Pressure ---
                float supplyDemandFactor = CalculateSupplyDemandFactor(resource);

                // --- Factor 2: Event Shock Factor ---
                float shockFactor = CalculateShockFactor(resource.resourceId);

                // --- Factor 3: Historical Trend Factor ---
                float trendFactor = CalculateTrendFactor(resource);

                // --- Factor 4: Random Noise ---
                float noiseFactor = CalculateNoiseFactor(resource);

                // --- Combine all factors multiplicatively ---
                float rawPrice = resource.currentPrice * supplyDemandFactor * shockFactor * (1f + trendFactor) * noiseFactor;

                // --- Apply EMA Smoothing ---
                float smoothedPrice = ApplyEMA(resource.currentPrice, rawPrice);

                // --- Clamp to Floor/Ceiling ---
                float finalPrice = Mathf.Clamp(smoothedPrice, resource.priceFloor, resource.priceCeiling);

                // --- Update resource state ---
                resource.previousPrice = resource.currentPrice;
                resource.currentPrice = Mathf.Round(finalPrice * 100f) / 100f;

                // --- Record to price history ---
                resource.priceHistory.Add(resource.currentPrice);
                if (resource.priceHistory.Count > MaxHistoryLength)
                    resource.priceHistory.RemoveAt(0);

                // --- Fire event if price changed meaningfully ---
                if (Mathf.Abs(resource.currentPrice - oldPrice) > 0.001f)
                {
                    OnPriceChanged?.Invoke(resource.resourceId, oldPrice, resource.currentPrice);
                }
            }
        }

        /// <summary>
        /// Applies an immediate price shock to a specific resource.
        /// Shocks decay linearly over the specified duration.
        /// Example: An oil refinery being destroyed would apply a negative supply shock.
        /// </summary>
        /// <param name="resourceId">The resource to affect (e.g. "oil").</param>
        /// <param name="magnitude">Shock strength. Positive = price spike, negative = price drop.</param>
        /// <param name="durationInTurns">How many turns the shock persists before fully decaying. Defaults to 3.</param>
        public void ApplyShock(string resourceId, float magnitude, int durationInTurns = 3)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                Debug.LogWarning("[DynamicPricingEngine] Cannot apply shock: resourceId is null or empty.");
                return;
            }

            var shock = new MarketShock
            {
                resourceId = resourceId,
                magnitude = magnitude,
                turnsRemaining = durationInTurns,
                totalDuration = durationInTurns
            };

            _activeShocks.Add(shock);
            Debug.Log($"[DynamicPricingEngine] Shock applied to '{resourceId}': magnitude={magnitude:F3}, duration={durationInTurns} turns.");
        }

        /// <summary>
        /// Processes a large market order and applies price impact.
        /// Large buy orders push prices up; large sell orders push prices down.
        /// </summary>
        /// <param name="resourceId">The resource being traded.</param>
        /// <param name="quantity">Amount of the resource in the order.</param>
        /// <param name="isBuy">True if buying (increases price), false if selling (decreases price).</param>
        /// <returns>The actual execution price after market impact.</returns>
        public float ProcessLargeOrder(string resourceId, float quantity, bool isBuy)
        {
            ResourceMarketData resource = GetResource(resourceId);
            if (resource == null)
            {
                Debug.LogWarning($"[DynamicPricingEngine] Cannot process order: resource '{resourceId}' not found.");
                return 0f;
            }

            if (quantity <= 0f)
            {
                Debug.LogWarning("[DynamicPricingEngine] Cannot process order: quantity must be positive.");
                return resource.currentPrice;
            }

            // Market impact scales with order size relative to global demand
            float orderImpact = (quantity / Mathf.Max(resource.globalDemand, 1f)) * 0.1f;
            float direction = isBuy ? 1f : -1f;

            // Apply immediate price shift
            float impactAmount = resource.currentPrice * orderImpact * direction;
            float oldPrice = resource.currentPrice;
            float newPrice = Mathf.Clamp(
                resource.currentPrice + impactAmount,
                resource.priceFloor,
                resource.priceCeiling
            );

            newPrice = Mathf.Round(newPrice * 100f) / 100f;

            // Update supply/demand to reflect the trade
            if (isBuy)
            {
                resource.globalDemand += quantity * 0.3f;
            }
            else
            {
                resource.globalSupply += quantity * 0.3f;
            }

            resource.previousPrice = resource.currentPrice;
            resource.currentPrice = newPrice;

            // Record to history
            resource.priceHistory.Add(newPrice);
            if (resource.priceHistory.Count > MaxHistoryLength)
                resource.priceHistory.RemoveAt(0);

            OnPriceChanged?.Invoke(resourceId, oldPrice, newPrice);
            Debug.Log($"[DynamicPricingEngine] Large {(isBuy ? "BUY" : "SELL")} order processed: {quantity}x '{resourceId}' @ {newPrice:F2}");

            return newPrice;
        }

        /// <summary>
        /// Updates the global supply and/or demand for a specific resource.
        /// Call this when nations build new production facilities, lose cities, or units are created/destroyed.
        /// </summary>
        /// <param name="resourceId">The resource to update.</param>
        /// <param name="supplyDelta">Change in global supply (positive = more supply available).</param>
        /// <param name="demandDelta">Change in global demand (positive = more demand).</param>
        public void UpdateSupplyDemand(string resourceId, float supplyDelta, float demandDelta)
        {
            ResourceMarketData resource = GetResource(resourceId);
            if (resource == null)
            {
                Debug.LogWarning($"[DynamicPricingEngine] Cannot update supply/demand: resource '{resourceId}' not found.");
                return;
            }

            resource.globalSupply = Mathf.Max(0.1f, resource.globalSupply + supplyDelta);
            resource.globalDemand = Mathf.Max(0.1f, resource.globalDemand + demandDelta);

            Debug.Log($"[DynamicPricingEngine] Supply/Demand updated for '{resourceId}': " +
                      $"supply={resource.globalSupply:F1} (+{supplyDelta:F1}), " +
                      $"demand={resource.globalDemand:F1} (+{demandDelta:F1})");
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Queries
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets the current market data for a specific resource.
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        /// <returns>The ResourceMarketData, or null if not found.</returns>
        public ResourceMarketData GetResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return null;
            return marketResources.Find(r => r.resourceId == resourceId);
        }

        /// <summary>
        /// Gets the current price of a specific resource.
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        /// <returns>Current price, or -1 if resource not found.</returns>
        public float GetCurrentPrice(string resourceId)
        {
            var resource = GetResource(resourceId);
            return resource != null ? resource.currentPrice : -1f;
        }

        /// <summary>
        /// Gets the percentage price change from the previous turn for a resource.
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        /// <returns>Percentage change (e.g. 0.05 = 5% increase), or 0 if unavailable.</returns>
        public float GetPriceChangePercent(string resourceId)
        {
            var resource = GetResource(resourceId);
            if (resource == null || resource.previousPrice <= 0f) return 0f;
            return (resource.currentPrice - resource.previousPrice) / resource.previousPrice;
        }

        /// <summary>
        /// Gets the nth percentile price from a resource's price history.
        /// Useful for AI trading decisions (e.g., buy below 30th percentile).
        /// </summary>
        /// <param name="resourceId">The resource identifier.</param>
        /// <param name="percentile">Percentile value from 0.0 to 1.0.</param>
        /// <returns>The price at the given percentile, or current price if insufficient history.</returns>
        public float GetPriceAtPercentile(string resourceId, float percentile)
        {
            var resource = GetResource(resourceId);
            if (resource == null || resource.priceHistory.Count < 3)
            {
                return GetCurrentPrice(resourceId);
            }

            List<float> sorted = new List<float>(resource.priceHistory);
            sorted.Sort();

            int index = Mathf.Clamp(
                Mathf.FloorToInt((sorted.Count - 1) * percentile),
                0, sorted.Count - 1
            );

            return sorted[index];
        }

        // --------------------------------------------------------------------- //
        // Private Methods - Factor Calculations
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Calculates the supply/demand pressure factor.
        /// When demand exceeds supply, prices rise; when supply exceeds demand, prices fall.
        /// </summary>
        private float CalculateSupplyDemandFactor(ResourceMarketData resource)
        {
            if (resource.globalSupply <= 0f || resource.globalDemand <= 0f)
                return 1f;

            float ratio = resource.globalDemand / resource.globalSupply;
            float deviation = (ratio - 1f) * 0.15f;
            return 1f + deviation;
        }

        /// <summary>
        /// Calculates the combined effect of all active shocks affecting a given resource.
        /// Multiple shocks stack multiplicatively.
        /// </summary>
        private float CalculateShockFactor(string resourceId)
        {
            float factor = 1f;

            foreach (var shock in _activeShocks)
            {
                if (string.IsNullOrEmpty(shock.resourceId) || shock.resourceId == resourceId)
                {
                    factor *= shock.GetDecayedFactor();
                }
            }

            return factor;
        }

        /// <summary>
        /// Calculates the historical trend factor based on a 3-turn moving average.
        /// Compares the recent average to the current price to detect trends.
        /// </summary>
        private float CalculateTrendFactor(ResourceMarketData resource)
        {
            if (resource.priceHistory.Count < 2)
                return 0f;

            int windowSize = Mathf.Min(3, resource.priceHistory.Count);
            float sum = 0f;
            for (int i = resource.priceHistory.Count - windowSize; i < resource.priceHistory.Count; i++)
            {
                sum += resource.priceHistory[i];
            }
            float movingAverage = sum / windowSize;

            if (resource.currentPrice <= 0f) return 0f;

            float trendDirection = (movingAverage - resource.currentPrice) / resource.currentPrice;
            return trendDirection * trendInfluence;
        }

        /// <summary>
        /// Generates random noise scaled by the resource's volatility setting.
        /// Returns a multiplier centered around 1.0.
        /// </summary>
        private float CalculateNoiseFactor(ResourceMarketData resource)
        {
            float noiseRange = resource.volatility * 0.1f;
            float noise = UnityEngine.Random.Range(-noiseRange, noiseRange);
            return 1f + noise;
        }

        /// <summary>
        /// Applies Exponential Moving Average smoothing between the current price and the raw calculated price.
        /// </summary>
        private float ApplyEMA(float currentPrice, float rawPrice)
        {
            return (emaSmoothingFactor * rawPrice) + ((1f - emaSmoothingFactor) * currentPrice);
        }
    }
}
