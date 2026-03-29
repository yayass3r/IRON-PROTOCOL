// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: ResourceDefinition.cs
// Namespace: IronProtocol.GameSystems.Economy
// Description: ScriptableObject for defining resources. Stores base economic
//              parameters (price, supply, demand), price bounds, volatility,
//              strategic classification, and category. Used by the
//              DynamicPricingEngine and MarketUI systems.
//              Create instances via: Right-Click → Create → Iron Protocol → Resource Definition
// ============================================================================

using UnityEngine;

namespace IronProtocol.GameSystems.Economy
{
    /// <summary>
    /// ScriptableObject that fully defines a tradeable resource.
    /// Contains economic parameters for the dynamic pricing engine,
    /// visual display data, and classification metadata.
    /// <para>
    /// Create instances via: <c>Assets → Create → Iron Protocol → Resource Definition</c>
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "NewResource", menuName = "Iron Protocol/Resource Definition")]
    public class ResourceDefinition : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Identity
        // ------------------------------------------------------------------

        [Header("Identity")]
        [Tooltip("Unique identifier for this resource (no spaces, used as key in systems).")]
        [SerializeField] private string resourceId;

        [Tooltip("Display name shown in the UI and market screen.")]
        [SerializeField] private string resourceName;

        /// <summary>Unique identifier for this resource.</summary>
        public string ResourceId => resourceId;

        /// <summary>Display name shown in the UI.</summary>
        public string ResourceName => resourceName;

        // ------------------------------------------------------------------
        // Visual
        // ------------------------------------------------------------------

        [Header("Visual")]
        [Tooltip("Icon sprite displayed in the market and resource panels.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Color tint for UI elements related to this resource.")]
        [SerializeField] private Color tintColor = Color.white;

        /// <summary>Icon sprite for UI display.</summary>
        public Sprite Icon => icon;

        /// <summary>UI tint color for this resource.</summary>
        public Color TintColor => tintColor;

        // ------------------------------------------------------------------
        // Base Economic Parameters
        // ------------------------------------------------------------------

        [Header("Economy - Base Values")]
        [Tooltip("Base market price at game start.")]
        [SerializeField] private float basePrice = 100f;

        [Tooltip("Base supply quantity available per turn.")]
        [SerializeField] private float baseSupply = 500f;

        [Tooltip("Base demand quantity per turn from all nations combined.")]
        [SerializeField] private float baseDemand = 500f;

        /// <summary>Base market price at game start.</summary>
        public float BasePrice => basePrice;

        /// <summary>Base supply quantity per turn.</summary>
        public float BaseSupply => baseSupply;

        /// <summary>Base demand quantity per turn.</summary>
        public float BaseDemand => baseDemand;

        // ------------------------------------------------------------------
        // Price Bounds
        // ------------------------------------------------------------------

        [Header("Economy - Price Bounds")]
        [Tooltip("Maximum price the resource can reach (hard ceiling).")]
        [SerializeField] private float priceCeiling = 1000f;

        [Tooltip("Minimum price the resource can drop to (hard floor).")]
        [SerializeField] private float priceFloor = 10f;

        /// <summary>Maximum possible price (hard ceiling).</summary>
        public float PriceCeiling => priceCeiling;

        /// <summary>Minimum possible price (hard floor).</summary>
        public float PriceFloor => priceFloor;

        // ------------------------------------------------------------------
        // Volatility & Dynamics
        // ------------------------------------------------------------------

        [Header("Economy - Dynamics")]
        [Tooltip("Price volatility factor (0 = stable, 1 = highly volatile). Affects random price swings each turn.")]
        [SerializeField] [Range(0f, 1f)] private float volatility = 0.1f;

        [Tooltip("Price elasticity – how quickly price responds to supply/demand imbalance (higher = faster reaction).")]
        [SerializeField] [Range(0.01f, 2f)] private float priceElasticity = 0.5f;

        [Tooltip("Historical decay – how much past prices influence current pricing (0 = no memory, 1 = strong memory).")]
        [SerializeField] [Range(0f, 1f)] private float historicalDecay = 0.3f;

        /// <summary>Price volatility factor (0 = stable, 1 = highly volatile).</summary>
        public float Volatility => volatility;

        /// <summary>Price elasticity (how fast price reacts to supply/demand).</summary>
        public float PriceElasticity => priceElasticity;

        /// <summary>Historical price decay factor (momentum/inertia).</summary>
        public float HistoricalDecay => historicalDecay;

        // ------------------------------------------------------------------
        // Classification
        // ------------------------------------------------------------------

        [Header("Classification")]
        [Tooltip("Whether this resource is strategic (uranium, silicon, etc.). Strategic resources have restricted trade and special gameplay effects.")]
        [SerializeField] private bool isStrategic = false;

        [Tooltip("Resource category for grouping in the market UI and economic calculations.")]
        [SerializeField] private string category = "basic";

        /// <summary>Whether this is a strategic resource with restricted trade.</summary>
        public bool IsStrategic => isStrategic;

        /// <summary>Resource category ("basic", "advanced", "military").</summary>
        public string Category => category;

        // ------------------------------------------------------------------
        // Market Event Modifiers
        // ------------------------------------------------------------------

        [Header("Market Events")]
        [Tooltip("Chance per turn of a market event affecting this resource (0 = never, 1 = every turn).")]
        [SerializeField] [Range(0f, 0.5f)] private float marketEventChance = 0.05f;

        [Tooltip("Maximum price impact of a market event (as a percentage of base price).")]
        [SerializeField] [Range(0f, 1f)] private float maxEventImpact = 0.25f;

        /// <summary>Per-turn probability of a market event for this resource.</summary>
        public float MarketEventChance => marketEventChance;

        /// <summary>Maximum price impact of a market event (fraction of base price).</summary>
        public float MaxEventImpact => maxEventImpact;

        // ------------------------------------------------------------------
        // Supply Chain
        // ------------------------------------------------------------------

        [Header("Supply Chain")]
        [Tooltip("Resources required to produce one unit of this resource (for advanced/military goods).")]
        [SerializeField] private ResourceCost[] productionInputs;

        [Tooltip("The per-unit production cost in treasury when built by a city.")]
        [SerializeField] private float productionCost = 0f;

        /// <summary>Input resources required for production (read-only).</summary>
        public ResourceCost[] ProductionInputs => productionInputs;

        /// <summary>Treasury cost to produce one unit of this resource.</summary>
        public float ProductionCost => productionCost;

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        private void OnValidate()
        {
            // Ensure resourceId has no spaces
            if (!string.IsNullOrEmpty(resourceId))
            {
                resourceId = resourceId.Replace(" ", "_").ToLower();
            }

            // Ensure positive values
            basePrice = Mathf.Max(0.01f, basePrice);
            baseSupply = Mathf.Max(0f, baseSupply);
            baseDemand = Mathf.Max(0f, baseDemand);
            priceCeiling = Mathf.Max(priceFloor + 1f, priceCeiling);
            priceFloor = Mathf.Max(0.01f, priceFloor);
            volatility = Mathf.Clamp01(volatility);
            priceElasticity = Mathf.Max(0.01f, priceElasticity);
            historicalDecay = Mathf.Clamp01(historicalDecay);
            marketEventChance = Mathf.Clamp(marketEventChance, 0f, 0.5f);
            maxEventImpact = Mathf.Clamp01(maxEventImpact);
            productionCost = Mathf.Max(0f, productionCost);

            // Warn about category
            if (category != "basic" && category != "advanced" && category != "military")
            {
                Debug.LogWarning($"[ResourceDefinition] Unexpected category '{category}' for '{resourceName}'. Expected: basic, advanced, or military.");
            }
        }

        /// <summary>
        /// Returns a summary string for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"Resource: {resourceName} ({resourceId}), " +
                   $"Price: ${basePrice:F0} (floor: ${priceFloor:F0}, ceiling: ${priceCeiling:F0}), " +
                   $"Supply: {baseSupply:F0}, Demand: {baseDemand:F0}, " +
                   $"Volatility: {volatility:P0}, " +
                   $"Category: {category}, Strategic: {isStrategic}";
        }

        /// <summary>
        /// Clamps a given price to the resource's defined floor and ceiling.
        /// </summary>
        /// <param name="price">The raw calculated price.</param>
        /// <returns>The price clamped within [priceFloor, priceCeiling].</returns>
        public float ClampPrice(float price)
        {
            return Mathf.Clamp(price, priceFloor, priceCeiling);
        }
    }

    // ========================================================================
    // Supporting Types
    // ========================================================================

    /// <summary>
    /// Defines a resource input cost for production chains.
    /// Used in <see cref="ResourceDefinition.ProductionInputs"/>.
    /// </summary>
    [Serializable]
    public class ResourceCost
    {
        [Tooltip("The resource identifier required as input.")]
        public string resourceId;

        [Tooltip("Quantity of the input resource consumed per unit produced.")]
        public float quantity;

        /// <summary>
        /// Creates a new ResourceCost.
        /// </summary>
        public ResourceCost(string resourceId, float quantity)
        {
            this.resourceId = resourceId;
            this.quantity = quantity;
        }
    }
}
