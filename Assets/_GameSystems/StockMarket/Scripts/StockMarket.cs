// =====================================================================
// IRON PROTOCOL - Stock Market System
// Global stock market with 15 companies, trading, and event-driven pricing.
// =====================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.GameSystems.StockMarket
{
    // =================================================================
    // ENUMERATIONS
    // =================================================================

    /// <summary>
    /// Sector classification for publicly traded companies.
    /// Sector-wide events affect all companies in the same sector.
    /// </summary>
    public enum StockSector
    {
        /// <summary>Defense contractors, weapon manufacturers, military technology.</summary>
        Military,
        /// <summary>Software, hardware, AI, quantum computing.</summary>
        Technology,
        /// <summary>Oil, gas, renewables, nuclear, mining.</summary>
        Energy,
        /// <summary>Banks, insurance, crypto, investment firms.</summary>
        Finance,
        /// <summary>Construction, telecommunications, space infrastructure.</summary>
        Infrastructure
    }

    // =================================================================
    // DATA CLASSES
    // =================================================================

    /// <summary>
    /// Represents a publicly traded company on the global stock market.
    /// Tracks share price history, market cap, dividends, and sector classification.
    /// </summary>
    [Serializable]
    public class StockCompany
    {
        /// <summary>Unique company identifier (e.g. "COMP_TDEF").</summary>
        public string companyId;

        /// <summary>Full company name displayed in UI.</summary>
        public string companyName;

        /// <summary>Stock ticker symbol (e.g. "TDEF").</summary>
        public string ticker;

        /// <summary>Sector classification affecting how events impact the stock.</summary>
        public StockSector sector;

        /// <summary>Current share price in game currency.</summary>
        public float sharePrice;

        /// <summary>Share price at the end of the previous turn (for change calculation).</summary>
        public float previousPrice;

        /// <summary>Total market capitalization (sharePrice * totalShares).</summary>
        public float marketCap;

        /// <summary>Total number of outstanding shares.</summary>
        public long totalShares;

        /// <summary>Price-to-earnings ratio; higher suggests growth expectations.</summary>
        public float peRatio;

        /// <summary>Annual dividend yield as a percentage (e.g. 0.03 = 3%).</summary>
        public float dividendYield;

        /// <summary>Price volatility from 0 (stable) to 1 (extremely volatile).</summary>
        [Range(0f, 1f)] public float volatility;

        /// <summary>Historical share prices for charting (max 100 entries).</summary>
        public List<float> priceHistory = new List<float>();

        /// <summary>Nation ID where the company is headquartered.</summary>
        public string nationId;

        /// <summary>Whether the company is publicly tradable.</summary>
        public bool isPublic;

        /// <summary>Dividend accumulation counter; pays out every <see cref="DividendIntervalTurns"/> turns.</summary>
        [HideInInspector] public int turnsSinceLastDividend;

        /// <summary>
        /// Creates a new stock company with all required fields.
        /// </summary>
        public StockCompany(
            string companyId, string companyName, string ticker,
            StockSector sector, float sharePrice, long totalShares,
            float peRatio, float dividendYield, float volatility,
            string nationId)
        {
            this.companyId = companyId;
            this.companyName = companyName;
            this.ticker = ticker;
            this.sector = sector;
            this.sharePrice = Mathf.Max(0.01f, sharePrice);
            this.previousPrice = this.sharePrice;
            this.totalShares = Mathf.Max(1, totalShares);
            this.marketCap = this.sharePrice * this.totalShares;
            this.peRatio = Mathf.Max(0.1f, peRatio);
            this.dividendYield = Mathf.Clamp(dividendYield, 0f, 0.15f);
            this.volatility = Mathf.Clamp(volatility, 0f, 1f);
            this.nationId = nationId;
            this.isPublic = true;
            this.turnsSinceLastDividend = 0;
            this.priceHistory.Add(this.sharePrice);
        }

        /// <summary>Recalculates market cap from current share price and total shares.</summary>
        public void RecalculateMarketCap()
        {
            marketCap = sharePrice * totalShares;
        }

        /// <summary>Records the current price into history, capped at 100 entries.</summary>
        public void RecordPrice()
        {
            previousPrice = sharePrice;
            priceHistory.Add(sharePrice);
            if (priceHistory.Count > 100)
                priceHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// Represents a nation's stock portfolio containing all share holdings.
    /// </summary>
    [Serializable]
    public class Portfolio
    {
        /// <summary>Share holdings keyed by company ID (companyId -> number of shares).</summary>
        public Dictionary<string, long> holdings = new Dictionary<string, long>();

        /// <summary>Total current market value of all holdings.</summary>
        public float totalValue;

        /// <summary>Total profit/loss compared to the average purchase price.</summary>
        public float totalProfitLoss;

        /// <summary>Historical cost basis for each holding (companyId -> total cost paid).</summary>
        [NonSerialized]
        public Dictionary<string, float> costBasis = new Dictionary<string, float>();
    }

    /// <summary>
    /// Represents a pending or executed trade order on the stock market.
    /// </summary>
    [Serializable]
    public class TradeOrder
    {
        /// <summary>Unique order identifier.</summary>
        public string orderId;

        /// <summary>Company being traded.</summary>
        public string companyId;

        /// <summary>Nation placing the order.</summary>
        public string investorNationId;

        /// <summary>True for buy, false for sell.</summary>
        public bool isBuy;

        /// <summary>Number of shares to trade.</summary>
        public long quantity;

        /// <summary>Target price per share (0 = market order).</summary>
        public float targetPrice;

        /// <summary>Whether this order is still active / pending.</summary>
        public bool isActive;

        /// <summary>Timestamp (turn number) when the order was placed.</summary>
        public int turnPlaced;

        /// <summary>
        /// Creates a new trade order.
        /// </summary>
        public TradeOrder(string orderId, string companyId, string investorNationId,
                          bool isBuy, long quantity, float targetPrice, int turnPlaced)
        {
            this.orderId = orderId;
            this.companyId = companyId;
            this.investorNationId = investorNationId;
            this.isBuy = isBuy;
            this.quantity = Mathf.Max(1, quantity);
            this.targetPrice = Mathf.Max(0f, targetPrice);
            this.isActive = true;
            this.turnPlaced = turnPlaced;
        }
    }

    // =================================================================
    // STOCK MARKET SYSTEM
    // =================================================================

    /// <summary>
    /// Core system managing the global stock market: company listings, price updates,
    /// buy/sell execution, portfolio tracking, dividends, and event-driven shocks.
    /// Attach to a persistent GameManager as a singleton.
    /// </summary>
    public class StockMarket : MonoBehaviour
    {
        // -------------------------------------------------------------
        // CONSTANTS
        // -------------------------------------------------------------
        private const int DividendIntervalTurns = 10;
        private const int MaxPriceHistory = 100;
        private const float MeanReversionStrength = 0.02f;
        private const float MaxSingleTurnChange = 0.15f; // 15% max move per turn
        private const float WarMilitarySectorBoost = 0.04f;
        private const float DefeatNationPenalty = 0.08f;
        private const float OrderIdCounter = 0;
        private const long MaxPortfolioShares = 100_000_000;

        // -------------------------------------------------------------
        // STATE
        // -------------------------------------------------------------
        private readonly Dictionary<string, StockCompany> _companies = new Dictionary<string, StockCompany>();
        private readonly Dictionary<string, Portfolio> _portfolios = new Dictionary<string, Portfolio>();
        private readonly List<TradeOrder> _pendingOrders = new List<TradeOrder>();
        private int _currentTurn = 0;
        private int _orderCounter = 0;
        private float _marketSentiment = 0f; // -1 to 1 (bearish to bullish)

        // Nation war state tracking for sector modifiers
        private readonly HashSet<string> _nationsAtWar = new HashSet<string>();
        private readonly HashSet<string> _nationsDefeated = new HashSet<string>();

        // -------------------------------------------------------------
        // EVENTS
        // -------------------------------------------------------------
        /// <summary>Fired when any stock price changes. Parameters: (companyId, oldPrice, newPrice).</summary>
        public event Action<string, float, float> OnStockPriceChanged;

        /// <summary>Fired when a trade is executed. Parameters: (companyId, quantity, totalCost).</summary>
        public event Action<string, long, float> OnTradeExecuted;

        // -------------------------------------------------------------
        // PROPERTIES
        // -------------------------------------------------------------
        /// <summary>Current global turn counter.</summary>
        public int CurrentTurn => _currentTurn;

        /// <summary>Overall market sentiment from -1 (bearish) to 1 (bullish).</summary>
        public float MarketSentiment => _marketSentiment;

        /// <summary>Number of listed companies.</summary>
        public int CompanyCount => _companies.Count;

        // =============================================================
        // INITIALIZATION
        // =============================================================

        /// <summary>
        /// Unity lifecycle callback. Initializes the market with 15 default companies.
        /// </summary>
        private void Awake()
        {
            InitializeMarket();
        }

        /// <summary>
        /// Creates all 15 default publicly traded companies across 5 sectors.
        /// Called automatically on Awake.
        /// </summary>
        public void InitializeMarket()
        {
            _companies.Clear();
            _currentTurn = 0;
            _orderCounter = 0;
            _marketSentiment = 0f;

            // --- Military Sector ---
            AddCompany(new StockCompany("COMP_TDEF", "Titan Defense", "TDEF",
                StockSector.Military, 145.50f, 50_000_000, 18.5f, 0.025f, 0.15f, "USA"));
            AddCompany(new StockCompany("COMP_SKFS", "SkyForce Systems", "SKFS",
                StockSector.Military, 98.20f, 35_000_000, 22.0f, 0.015f, 0.20f, "USA"));
            AddCompany(new StockCompany("COMP_CSHC", "CyberShield Corp", "CSHC",
                StockSector.Military, 210.75f, 20_000_000, 35.0f, 0.005f, 0.30f, "GBR"));

            // --- Technology Sector ---
            AddCompany(new StockCompany("COMP_QCOR", "QuantumCore", "QCOR",
                StockSector.Technology, 520.00f, 15_000_000, 50.0f, 0f, 0.35f, "USA"));
            AddCompany(new StockCompany("COMP_NTIN", "NanoTech Industries", "NTIN",
                StockSector.Technology, 88.30f, 40_000_000, 28.0f, 0.01f, 0.25f, "JPN"));
            AddCompany(new StockCompany("COMP_SVDX", "Silicon Valley Dynamics", "SVDX",
                StockSector.Technology, 340.60f, 25_000_000, 42.0f, 0.008f, 0.28f, "USA"));

            // --- Energy Sector ---
            AddCompany(new StockCompany("COMP_PGLB", "PetroGlobal", "PGLB",
                StockSector.Energy, 72.40f, 80_000_000, 12.0f, 0.04f, 0.18f, "SAU"));
            AddCompany(new StockCompany("COMP_GPWR", "GreenPower Inc", "GPWR",
                StockSector.Energy, 55.80f, 60_000_000, 45.0f, 0f, 0.32f, "DEU"));
            AddCompany(new StockCompany("COMP_URAN", "UraniumOne", "URAN",
                StockSector.Energy, 32.10f, 100_000_000, 15.0f, 0.02f, 0.22f, "CAN"));

            // --- Finance Sector ---
            AddCompany(new StockCompany("COMP_GBK", "GlobalBank", "GBK",
                StockSector.Finance, 185.00f, 45_000_000, 14.0f, 0.035f, 0.12f, "CHN"));
            AddCompany(new StockCompany("COMP_CRVT", "CryptoVault", "CRVT",
                StockSector.Finance, 125.50f, 30_000_000, 60.0f, 0f, 0.45f, "USA"));
            AddCompany(new StockCompany("COMP_INSW", "InsuranceWorld", "INSW",
                StockSector.Finance, 95.20f, 55_000_000, 11.0f, 0.03f, 0.10f, "GBR"));

            // --- Infrastructure Sector ---
            AddCompany(new StockCompany("COMP_BRLT", "BuildRight", "BRLT",
                StockSector.Infrastructure, 42.60f, 70_000_000, 16.0f, 0.02f, 0.14f, "IND"));
            AddCompany(new StockCompany("COMP_TCGL", "TeleCom Global", "TCGL",
                StockSector.Infrastructure, 68.90f, 50_000_000, 20.0f, 0.025f, 0.16f, "KOR"));
            AddCompany(new StockCompany("COMP_SPTX", "SpaceXport", "SPTX",
                StockSector.Infrastructure, 310.00f, 10_000_000, 80.0f, 0f, 0.40f, "USA"));

            Debug.Log($"[StockMarket] Market initialized with {_companies.Count} companies.");
        }

        /// <summary>
        /// Registers a company into the market listing.
        /// </summary>
        private void AddCompany(StockCompany company)
        {
            if (company != null && !string.IsNullOrEmpty(company.companyId))
                _companies[company.companyId] = company;
        }

        // =============================================================
        // STOCK QUERIES
        // =============================================================

        /// <summary>
        /// Returns all listed companies.
        /// </summary>
        /// <returns>List of all <see cref="StockCompany"/> objects.</returns>
        public List<StockCompany> GetAllStocks()
        {
            return _companies.Values.ToList();
        }

        /// <summary>
        /// Returns a company by its unique ID.
        /// </summary>
        /// <param name="companyId">Company identifier.</param>
        /// <returns>The <see cref="StockCompany"/>, or null if not found.</returns>
        public StockCompany GetStock(string companyId)
        {
            if (string.IsNullOrEmpty(companyId)) return null;
            _companies.TryGetValue(companyId, out StockCompany company);
            return company;
        }

        /// <summary>
        /// Returns all companies in a specific sector.
        /// </summary>
        /// <param name="sector">Sector to filter by.</param>
        /// <returns>List of companies in the sector.</returns>
        public List<StockCompany> GetStocksBySector(StockSector sector)
        {
            return _companies.Values.Where(c => c.sector == sector).ToList();
        }

        /// <summary>
        /// Calculates the percentage change of a stock between the previous and current turn.
        /// </summary>
        /// <param name="companyId">Company identifier.</param>
        /// <returns>Percentage change (e.g. 0.05 = +5%), or 0 if not found.</returns>
        public float GetStockChange(string companyId)
        {
            StockCompany company = GetStock(companyId);
            if (company == null || company.previousPrice <= 0f) return 0f;
            return (company.sharePrice - company.previousPrice) / company.previousPrice;
        }

        // =============================================================
        // PRICE UPDATES
        // =============================================================

        /// <summary>
        /// Updates all stock prices for the current turn. Incorporates:
        /// <list type="bullet">
        ///   <item>Random walk with mean reversion</item>
        ///   <item>Sector-specific modifiers (military stocks up during wars)</item>
        ///   <item>Nation-specific modifiers (stocks drop if nation loses)</item>
        ///   <item>Market sentiment (bullish/bearish trend)</item>
        ///   <item>Automatic dividend payments every 10 turns</item>
        /// </list>
        /// </summary>
        public void UpdateStockPrices()
        {
            _currentTurn++;
            bool isDividendTurn = (_currentTurn % DividendIntervalTurns == 0);

            // Update market sentiment (slow random walk)
            _marketSentiment += UnityEngine.Random.Range(-0.1f, 0.1f);
            _marketSentiment = Mathf.Clamp(_marketSentiment, -1f, 1f);

            foreach (StockCompany company in _companies.Values)
            {
                if (!company.isPublic) continue;

                float oldPrice = company.sharePrice;

                // --- 1. Random walk component ---
                float randomChange = (UnityEngine.Random.value - 0.5f) * 2f * company.volatility;

                // --- 2. Mean reversion ---
                // Stocks tend to revert toward their historical average
                float historicalAvg = company.priceHistory.Count > 0
                    ? company.priceHistory.Average()
                    : company.sharePrice;
                float meanReversion = (historicalAvg - company.sharePrice) / Mathf.Max(historicalAvg, 0.01f)
                                     * MeanReversionStrength;

                // --- 3. Market sentiment ---
                float sentimentImpact = _marketSentiment * 0.01f;

                // --- 4. Sector-specific modifiers ---
                float sectorModifier = 0f;

                // Military stocks benefit from global wars
                if (company.sector == StockSector.Military && _nationsAtWar.Count > 0)
                    sectorModifier += WarMilitarySectorBoost * _nationsAtWar.Count;

                // Energy stocks benefit from instability
                if (company.sector == StockSector.Energy && _nationsAtWar.Count > 0)
                    sectorModifier += 0.02f;

                // --- 5. Nation-specific modifiers ---
                float nationModifier = 0f;

                if (!string.IsNullOrEmpty(company.nationId))
                {
                    // Company's nation at war: mixed effects
                    if (_nationsAtWar.Contains(company.nationId))
                    {
                        // Military companies benefit, others suffer
                        if (company.sector != StockSector.Military)
                            nationModifier -= 0.02f;
                    }

                    // Company's nation defeated: major penalty
                    if (_nationsDefeated.Contains(company.nationId))
                        nationModifier -= DefeatNationPenalty;
                }

                // --- Combine all factors ---
                float totalChange = randomChange + meanReversion + sentimentImpact
                                  + sectorModifier + nationModifier;

                // Clamp to maximum single-turn change
                totalChange = Mathf.Clamp(totalChange, -MaxSingleTurnChange, MaxSingleTurnChange);

                // Apply change
                company.sharePrice = Mathf.Max(0.01f, company.sharePrice * (1f + totalChange));
                company.RecalculateMarketCap();
                company.RecordPrice();

                // Update PE ratio slightly (random walk)
                company.peRatio = Mathf.Max(0.1f, company.peRatio * (1f + UnityEngine.Random.Range(-0.02f, 0.02f)));

                // --- Dividends ---
                if (isDividendTurn && company.dividendYield > 0f)
                {
                    float dividendPerShare = company.sharePrice * company.dividendYield;
                    DistributeDividends(company, dividendPerShare);
                    company.turnsSinceLastDividend = 0;

                    Debug.Log($"[StockMarket] {company.ticker} dividend: {dividendPerShare:F2}/share");
                }
                else
                {
                    company.turnsSinceLastDividend++;
                }

                // Fire price change event
                float actualChange = company.sharePrice - oldPrice;
                if (Mathf.Abs(actualChange) > 0.001f)
                {
                    OnStockPriceChanged?.Invoke(company.companyId, oldPrice, company.sharePrice);
                }
            }

            // Process pending orders
            ProcessPendingOrders();

            Debug.Log($"[StockMarket] Turn {_currentTurn} price update complete. " +
                      $"Sentiment: {_marketSentiment:F2}");
        }

        /// <summary>
        /// Distributes dividends to all nations holding shares of a company.
        /// </summary>
        /// <param name="company">Company paying dividends.</param>
        /// <param name="dividendPerShare">Amount paid per share.</param>
        private void DistributeDividends(StockCompany company, float dividendPerShare)
        {
            foreach (var kvp in _portfolios)
            {
                string nationId = kvp.Key;
                Portfolio portfolio = kvp.Value;

                if (portfolio.holdings.TryGetValue(company.companyId, out long shares) && shares > 0)
                {
                    float totalDividend = shares * dividendPerShare;
                    // In a full game, this would be added to the nation's treasury
                    Debug.Log($"[StockMarket] Dividend for '{nationId}': {shares} shares " +
                              $"of {company.ticker} = +{totalDividend:F0}");
                }
            }
        }

        /// <summary>
        /// Processes all pending limit orders against current market prices.
        /// </summary>
        private void ProcessPendingOrders()
        {
            var executed = new List<TradeOrder>();

            foreach (TradeOrder order in _pendingOrders)
            {
                if (!order.isActive) continue;

                StockCompany company = GetStock(order.companyId);
                if (company == null)
                {
                    order.isActive = false;
                    continue;
                }

                bool shouldExecute = false;

                if (order.targetPrice <= 0f)
                {
                    // Market order: always executes
                    shouldExecute = true;
                }
                else if (order.isBuy && company.sharePrice <= order.targetPrice)
                {
                    // Buy limit: execute when price drops to or below target
                    shouldExecute = true;
                }
                else if (!order.isBuy && company.sharePrice >= order.targetPrice)
                {
                    // Sell limit: execute when price rises to or above target
                    shouldExecute = true;
                }

                // Cancel stale orders (older than 20 turns)
                if (_currentTurn - order.turnPlaced > 20)
                {
                    order.isActive = false;
                    continue;
                }

                if (shouldExecute)
                {
                    ExecuteOrder(order, company);
                    order.isActive = false;
                    executed.Add(order);
                }
            }

            // Remove executed and inactive orders
            _pendingOrders.RemoveAll(o => !o.isActive);
        }

        /// <summary>
        /// Executes a trade order at the current market price.
        /// </summary>
        private void ExecuteOrder(TradeOrder order, StockCompany company)
        {
            float totalCost = company.sharePrice * order.quantity;
            Portfolio portfolio = GetOrCreatePortfolio(order.investorNationId);

            if (order.isBuy)
            {
                if (!portfolio.holdings.ContainsKey(order.companyId))
                    portfolio.holdings[order.companyId] = 0;

                portfolio.holdings[order.companyId] += order.quantity;

                if (!portfolio.costBasis.ContainsKey(order.companyId))
                    portfolio.costBasis[order.companyId] = 0f;
                portfolio.costBasis[order.companyId] += totalCost;
            }
            else
            {
                if (portfolio.holdings.ContainsKey(order.companyId))
                {
                    portfolio.holdings[order.companyId] -= order.quantity;
                    if (portfolio.holdings[order.companyId] <= 0)
                    {
                        portfolio.holdings.Remove(order.companyId);
                        portfolio.costBasis.Remove(order.companyId);
                    }
                }
            }

            RecalculatePortfolioValue(portfolio);
            OnTradeExecuted?.Invoke(order.companyId, order.quantity, totalCost);

            Debug.Log($"[StockMarket] Order {order.orderId} executed: " +
                      $"{(order.isBuy ? "BUY" : "SELL")} {order.quantity} {company.ticker} " +
                      $"@ {company.sharePrice:F2} = {totalCost:F0}");
        }

        // =============================================================
        // TRADING
        // =============================================================

        /// <summary>
        /// Buys shares of a company at the current market price for a nation.
        /// In a full game, this would deduct from the nation's treasury.
        /// </summary>
        /// <param name="nationId">Nation purchasing shares.</param>
        /// <param name="companyId">Company to buy.</param>
        /// <param name="quantity">Number of shares to purchase.</param>
        /// <returns>Total cost of the purchase, or -1 on failure.</returns>
        public float BuyShares(string nationId, string companyId, long quantity)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(companyId))
            {
                Debug.LogError("[StockMarket] BuyShares: nationId and companyId required.");
                return -1f;
            }

            if (quantity <= 0)
            {
                Debug.LogError("[StockMarket] BuyShares: quantity must be positive.");
                return -1f;
            }

            StockCompany company = GetStock(companyId);
            if (company == null)
            {
                Debug.LogError($"[StockMarket] BuyShares: Company '{companyId}' not found.");
                return -1f;
            }

            if (!company.isPublic)
            {
                Debug.LogError($"[StockMarket] BuyShares: {company.companyName} is not publicly traded.");
                return -1f;
            }

            Portfolio portfolio = GetOrCreatePortfolio(nationId);

            // Check max holding limit
            long currentHolding = 0;
            portfolio.holdings.TryGetValue(companyId, out currentHolding);
            if (currentHolding + quantity > MaxPortfolioShares)
            {
                Debug.LogWarning($"[StockMarket] BuyShares: Exceeds max holding limit for '{nationId}'.");
                return -1f;
            }

            float totalCost = company.sharePrice * quantity;

            // Execute buy
            if (!portfolio.holdings.ContainsKey(companyId))
                portfolio.holdings[companyId] = 0;
            portfolio.holdings[companyId] += quantity;

            if (!portfolio.costBasis.ContainsKey(companyId))
                portfolio.costBasis[companyId] = 0f;
            portfolio.costBasis[companyId] += totalCost;

            RecalculatePortfolioValue(portfolio);

            Debug.Log($"[StockMarket] BUY: '{nationId}' bought {quantity} shares of {company.ticker} " +
                      $"@ {company.sharePrice:F2} = {totalCost:F0}");

            OnTradeExecuted?.Invoke(companyId, quantity, totalCost);
            return totalCost;
        }

        /// <summary>
        /// Sells shares of a company at the current market price for a nation.
        /// In a full game, this would add to the nation's treasury.
        /// </summary>
        /// <param name="nationId">Nation selling shares.</param>
        /// <param name="companyId">Company to sell.</param>
        /// <param name="quantity">Number of shares to sell.</param>
        /// <returns>Total revenue from the sale, or -1 on failure.</returns>
        public float SellShares(string nationId, string companyId, long quantity)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(companyId))
            {
                Debug.LogError("[StockMarket] SellShares: nationId and companyId required.");
                return -1f;
            }

            if (quantity <= 0)
            {
                Debug.LogError("[StockMarket] SellShares: quantity must be positive.");
                return -1f;
            }

            StockCompany company = GetStock(companyId);
            if (company == null)
            {
                Debug.LogError($"[StockMarket] SellShares: Company '{companyId}' not found.");
                return -1f;
            }

            Portfolio portfolio = GetOrCreatePortfolio(nationId);

            if (!portfolio.holdings.TryGetValue(companyId, out long currentHolding) || currentHolding < quantity)
            {
                Debug.LogError($"[StockMarket] SellShares: '{nationId}' does not hold enough shares " +
                               $"of {company.ticker} (has {currentHolding}, wants to sell {quantity}).");
                return -1f;
            }

            float totalRevenue = company.sharePrice * quantity;

            // Execute sell
            portfolio.holdings[companyId] -= quantity;
            if (portfolio.holdings[companyId] <= 0)
            {
                portfolio.holdings.Remove(companyId);
                portfolio.costBasis.Remove(companyId);
            }
            else
            {
                // Reduce cost basis proportionally
                float proportion = (float)quantity / (currentHolding + quantity);
                portfolio.costBasis[companyId] *= (1f - proportion);
            }

            RecalculatePortfolioValue(portfolio);

            Debug.Log($"[StockMarket] SELL: '{nationId}' sold {quantity} shares of {company.ticker} " +
                      $"@ {company.sharePrice:F2} = {totalRevenue:F0}");

            OnTradeExecuted?.Invoke(companyId, quantity, totalRevenue);
            return totalRevenue;
        }

        // =============================================================
        // PORTFOLIO MANAGEMENT
        // =============================================================

        /// <summary>
        /// Returns the full portfolio for a nation including holdings, value, and P/L.
        /// </summary>
        /// <param name="nationId">Nation to query.</param>
        /// <returns>The <see cref="Portfolio"/> object.</returns>
        public Portfolio GetPortfolio(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return null;
            return GetOrCreatePortfolio(nationId);
        }

        /// <summary>
        /// Calculates and returns the total market value of a nation's portfolio.
        /// </summary>
        /// <param name="nationId">Nation to evaluate.</param>
        /// <returns>Total portfolio value in game currency.</returns>
        public float GetPortfolioValue(string nationId)
        {
            Portfolio portfolio = GetPortfolio(nationId);
            if (portfolio == null) return 0f;
            RecalculatePortfolioValue(portfolio);
            return portfolio.totalValue;
        }

        /// <summary>
        /// Recalculates total value and profit/loss for a portfolio based on current prices.
        /// </summary>
        private void RecalculatePortfolioValue(Portfolio portfolio)
        {
            float totalVal = 0f;
            float totalCost = 0f;

            foreach (var holding in portfolio.holdings)
            {
                string companyId = holding.Key;
                long shares = holding.Value;

                StockCompany company = GetStock(companyId);
                if (company != null && shares > 0)
                {
                    totalVal += company.sharePrice * shares;
                }
            }

            foreach (var basis in portfolio.costBasis)
            {
                totalCost += basis.Value;
            }

            portfolio.totalValue = totalVal;
            portfolio.totalProfitLoss = totalVal - totalCost;
        }

        /// <summary>
        /// Gets or creates a portfolio for a nation.
        /// </summary>
        private Portfolio GetOrCreatePortfolio(string nationId)
        {
            if (!_portfolios.TryGetValue(nationId, out Portfolio portfolio))
            {
                portfolio = new Portfolio();
                _portfolios[nationId] = portfolio;
            }
            return portfolio;
        }

        // =============================================================
        // MARKET EVENTS
        // =============================================================

        /// <summary>
        /// Applies a sector-wide price shock affecting all companies in the specified sector.
        /// </summary>
        /// <param name="eventSector">Sector to affect.</param>
        /// <param name="impact">Percentage impact (e.g. 0.1 = +10%, -0.15 = -15%).</param>
        public void ApplyMarketEvent(string eventSector, float impact)
        {
            if (!Enum.TryParse<StockSector>(eventSector, out StockSector sector))
            {
                Debug.LogError($"[StockMarket] ApplyMarketEvent: Unknown sector '{eventSector}'.");
                return;
            }

            ApplyMarketEvent(sector, impact);
        }

        /// <summary>
        /// Applies a sector-wide price shock affecting all companies in the specified sector.
        /// </summary>
        /// <param name="sector">Sector enum to affect.</param>
        /// <param name="impact">Percentage impact (e.g. 0.1 = +10%, -0.15 = -15%).</param>
        public void ApplyMarketEvent(StockSector sector, float impact)
        {
            int affected = 0;
            foreach (StockCompany company in _companies.Values)
            {
                if (company.sector == sector && company.isPublic)
                {
                    float oldPrice = company.sharePrice;
                    company.sharePrice = Mathf.Max(0.01f, company.sharePrice * (1f + impact));
                    company.RecalculateMarketCap();
                    company.RecordPrice();
                    affected++;

                    OnStockPriceChanged?.Invoke(company.companyId, oldPrice, company.sharePrice);
                }
            }

            Debug.Log($"[StockMarket] Sector event on {sector}: {impact:+0.##%;-0.##%;0%} impact, " +
                      $"{affected} companies affected.");
        }

        /// <summary>
        /// Applies a nation-specific price shock to all companies headquartered in that nation.
        /// </summary>
        /// <param name="nationId">Nation whose companies are affected.</param>
        /// <param name="impact">Percentage impact (negative for bad events).</param>
        public void ApplyNationEvent(string nationId, float impact)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogError("[StockMarket] ApplyNationEvent: nationId is null or empty.");
                return;
            }

            int affected = 0;
            foreach (StockCompany company in _companies.Values)
            {
                if (company.nationId == nationId && company.isPublic)
                {
                    float oldPrice = company.sharePrice;
                    company.sharePrice = Mathf.Max(0.01f, company.sharePrice * (1f + impact));
                    company.RecalculateMarketCap();
                    company.RecordPrice();
                    affected++;

                    OnStockPriceChanged?.Invoke(company.companyId, oldPrice, company.sharePrice);
                }
            }

            Debug.Log($"[StockMarket] Nation event on '{nationId}': {impact:+0.##%;-0.##%;0%} impact, " +
                      $"{affected} companies affected.");
        }

        // =============================================================
        // WAR STATE TRACKING
        // =============================================================

        /// <summary>
        /// Marks a nation as being at war, enabling military sector stock boosts.
        /// </summary>
        /// <param name="nationId">Nation entering a war.</param>
        public void SetNationAtWar(string nationId)
        {
            if (!string.IsNullOrEmpty(nationId))
                _nationsAtWar.Add(nationId);
        }

        /// <summary>
        /// Removes a nation from the war state.
        /// </summary>
        /// <param name="nationId">Nation leaving a war.</param>
        public void SetNationAtPeace(string nationId)
        {
            _nationsAtWar.Remove(nationId);
        }

        /// <summary>
        /// Marks a nation as defeated, causing major stock penalties for its companies.
        /// </summary>
        /// <param name="nationId">Defeated nation.</param>
        public void SetNationDefeated(string nationId)
        {
            if (!string.IsNullOrEmpty(nationId))
            {
                _nationsDefeated.Add(nationId);
                ApplyNationEvent(nationId, -DefeatNationPenalty);
            }
        }

        /// <summary>
        /// Clears the defeated status for a nation.
        /// </summary>
        /// <param name="nationId">Nation recovering from defeat.</param>
        public void ClearNationDefeated(string nationId)
        {
            _nationsDefeated.Remove(nationId);
        }

        // =============================================================
        // MARKET METRICS
        // =============================================================

        /// <summary>
        /// Calculates a composite market index (average of all stock prices normalized).
        /// </summary>
        /// <returns>Market index value.</returns>
        public float GetMarketIndex()
        {
            if (_companies.Count == 0) return 0f;
            return _companies.Values.Where(c => c.isPublic).Average(c => c.sharePrice);
        }

        /// <summary>
        /// Returns the total market capitalization of all listed companies.
        /// </summary>
        /// <returns>Total market cap in game currency.</returns>
        public float GetTotalMarketCap()
        {
            return _companies.Values.Where(c => c.isPublic).Sum(c => c.marketCap);
        }

        /// <summary>
        /// Returns the top N gainers by percentage change.
        /// </summary>
        /// <param name="count">Number of top gainers to return.</param>
        /// <returns>List of (company, change) tuples sorted descending.</returns>
        public List<(StockCompany company, float change)> GetTopGainers(int count = 5)
        {
            return _companies.Values
                .Where(c => c.isPublic && c.previousPrice > 0f)
                .Select(c => (company: c, change: GetStockChange(c.companyId)))
                .OrderByDescending(x => x.change)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Returns the top N losers by percentage change.
        /// </summary>
        /// <param name="count">Number of top losers to return.</param>
        /// <returns>List of (company, change) tuples sorted ascending.</returns>
        public List<(StockCompany company, float change)> GetTopLosers(int count = 5)
        {
            return _companies.Values
                .Where(c => c.isPublic && c.previousPrice > 0f)
                .Select(c => (company: c, change: GetStockChange(c.companyId)))
                .OrderBy(x => x.change)
                .Take(count)
                .ToList();
        }

        // =============================================================
        // UTILITY
        // =============================================================

        /// <summary>
        /// Clears all market data, portfolios, and orders. Use on game reset.
        /// </summary>
        public void Reset()
        {
            _companies.Clear();
            _portfolios.Clear();
            _pendingOrders.Clear();
            _nationsAtWar.Clear();
            _nationsDefeated.Clear();
            _currentTurn = 0;
            _orderCounter = 0;
            _marketSentiment = 0f;
            Debug.Log("[StockMarket] Market reset.");
        }
    }
}
