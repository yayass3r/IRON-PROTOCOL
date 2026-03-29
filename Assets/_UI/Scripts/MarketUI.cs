// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: MarketUI.cs
// Namespace: IronProtocol.UI
// Description: MonoBehaviour for the dynamic market screen. Displays resource
//              cards with live pricing, purchase/sell controls, price history
//              charts, and market event log entries.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace IronProtocol.UI
{
    /// <summary>
    /// Lightweight data structure used to populate an individual resource
    /// card in the market UI.
    /// </summary>
    [Serializable]
    public class ResourceCardData
    {
        [Tooltip("Unique resource identifier.")]
        public string resourceId;

        [Tooltip("Display name of the resource.")]
        public string resourceName;

        [Tooltip("Icon sprite for the resource.")]
        public Sprite icon;

        [Tooltip("Current market price.")]
        public float currentPrice;

        [Tooltip("Percentage change from last turn (negative = drop).")]
        public float priceChangePercent;

        [Tooltip("Quantity currently owned by the player.")]
        public float ownedQuantity;

        [Tooltip("Is the resource available for trading this turn?")]
        public bool isAvailable;
    }

    /// <summary>
    /// Component that lives on each resource card prefab instance.
    /// Binds to the card's Text, Image, and Button children.
    /// </summary>
    public class ResourceCard : MonoBehaviour
    {
        [Header("Display Fields")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text priceText;
        [SerializeField] private Text changeText;
        [SerializeField] private Text ownedText;
        [SerializeField] private Image changeIndicator;

        [Header("Buttons")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button historyButton;

        /// <summary>Fired when the Buy button is clicked. Parameter: resourceId.</summary>
        public event Action<string> OnBuyClicked;

        /// <summary>Fired when the Sell button is clicked. Parameter: resourceId.</summary>
        public event Action<string> OnSellClicked;

        /// <summary>Fired when the History button is clicked. Parameter: resourceId.</summary>
        public event Action<string> OnHistoryClicked;

        private string _resourceId;

        private void OnEnable()
        {
            if (buyButton != null)     buyButton.onClick.AddListener(() => OnBuyClicked?.Invoke(_resourceId));
            if (sellButton != null)    sellButton.onClick.AddListener(() => OnSellClicked?.Invoke(_resourceId));
            if (historyButton != null) historyButton.onClick.AddListener(() => OnHistoryClicked?.Invoke(_resourceId));
        }

        private void OnDisable()
        {
            if (buyButton != null)     buyButton.onClick.RemoveAllListeners();
            if (sellButton != null)    sellButton.onClick.RemoveAllListeners();
            if (historyButton != null) historyButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Populates the card's visual elements from a <see cref="ResourceCardData"/>.
        /// </summary>
        public void Populate(ResourceCardData data)
        {
            _resourceId = data.resourceId;

            if (iconImage != null) iconImage.sprite = data.icon;
            if (nameText != null)  nameText.text = data.resourceName;
            if (priceText != null) priceText.text = $"${data.currentPrice:##,##0.00}";
            if (ownedText != null) ownedText.text = $"Owned: {data.ownedQuantity:F0}";

            if (changeText != null)
            {
                string sign = data.priceChangePercent >= 0 ? "+" : "";
                changeText.text = $"{sign}{data.priceChangePercent:F1}%";
                changeText.color = data.priceChangePercent >= 0 ? Color.green : Color.red;
            }

            // Interactable state
            bool canInteract = data.isAvailable;
            if (buyButton != null)  buyButton.interactable = canInteract;
            if (sellButton != null) sellButton.interactable = canInteract && data.ownedQuantity > 0;
        }

        /// <summary>Updates only the price-related fields for efficiency.</summary>
        public void UpdatePrice(float currentPrice, float changePercent)
        {
            if (priceText != null)
            {
                priceText.text = $"${currentPrice:##,##0.00}";
            }

            if (changeText != null)
            {
                string sign = changePercent >= 0 ? "+" : "";
                changeText.text = $"{sign}{changePercent:F1}%";
                changeText.color = changePercent >= 0 ? Color.green : Color.red;
            }
        }
    }

    /// <summary>
    /// MonoBehaviour that manages the full market screen UI.
    /// Populates a scrollable list of resource cards, handles buy/sell
    /// interactions, displays price history charts, and logs market events.
    /// </summary>
    public class MarketUI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields
        // ------------------------------------------------------------------

        [Header("Market Data")]
        [Tooltip("Reference to the DynamicPricingEngine for live price data.")]
        [SerializeField] private MonoBehaviour pricingEngineReference;

        [Header("UI References")]
        [Tooltip("Text displaying the player's current treasury.")]
        [SerializeField] private Text treasuryText;

        [Tooltip("Prefab instantiated for each resource in the list.")]
        [SerializeField] private GameObject resourceCardPrefab;

        [Tooltip("Scroll content transform that parents resource cards.")]
        [SerializeField] private Transform resourceListContent;

        [Header("Price History Chart")]
        [Tooltip("Parent object for the price history overlay.")]
        [SerializeField] private GameObject priceHistoryPanel;

        [Tooltip("Text showing which resource's history is displayed.")]
        [SerializeField] private Text historyTitleText;

        [Tooltip("UI Image used as a simple bar chart for price history.")]
        [SerializeField] private Transform historyChartContainer;

        [Tooltip("Button to close the price history overlay.")]
        [SerializeField] private Button closeHistoryButton;

        [Header("Market Event Log")]
        [Tooltip("Scrollable content transform for market event entries.")]
        [SerializeField] private Transform eventLogContent;

        [Tooltip("Prefab instantiated for each market event log entry.")]
        [SerializeField] private GameObject eventLogEntryPrefab;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the player initiates a buy order. Parameter: resourceId.</summary>
        public event Action<string> OnBuyOrder;

        /// <summary>Fired when the player initiates a sell order. Parameter: resourceId.</summary>
        public event Action<string> OnSellOrder;

        // ------------------------------------------------------------------
        // Private State
        // ------------------------------------------------------------------

        /// <summary>Maps resourceId → card component for fast price updates.</summary>
        private readonly Dictionary<string, ResourceCard> _spawnedCards = new Dictionary<string, ResourceCard>();

        /// <summary>Cached resource data list for re-population.</summary>
        private List<IronProtocol.GameSystems.Economy.ResourceMarketData> _currentResources = new List<IronProtocol.GameSystems.Economy.ResourceMarketData>();

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (priceHistoryPanel != null)
            {
                priceHistoryPanel.SetActive(false);
            }

            if (closeHistoryButton != null)
            {
                closeHistoryButton.onClick.AddListener(ClosePriceHistory);
            }
        }

        private void OnDestroy()
        {
            if (closeHistoryButton != null)
            {
                closeHistoryButton.onClick.RemoveListener(ClosePriceHistory);
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Clears existing cards and creates new ones for each resource
        /// in the provided list. Wires buy/sell/history button events.
        /// </summary>
        /// <param name="resources">List of current market resource data.</param>
        public void PopulateMarket(List<IronProtocol.GameSystems.Economy.ResourceMarketData> resources)
        {
            if (resources == null || resourceListContent == null || resourceCardPrefab == null)
            {
                Debug.LogWarning("[MarketUI] Cannot populate – missing references or null data.");
                return;
            }

            _currentResources = new List<IronProtocol.GameSystems.Economy.ResourceMarketData>(resources);

            // Clear existing cards
            ClearSpawnedCards();

            foreach (var marketData in resources)
            {
                GameObject cardObj = Instantiate(resourceCardPrefab, resourceListContent);
                ResourceCard card = cardObj.GetComponent<ResourceCard>();

                if (card == null)
                {
                    Debug.LogWarning($"[MarketUI] ResourceCard component missing on prefab instance for '{marketData.ResourceId}'.");
                    Destroy(cardObj);
                    continue;
                }

                // Build card display data
                ResourceCardData cardData = new ResourceCardData
                {
                    resourceId = marketData.ResourceId,
                    resourceName = marketData.ResourceName,
                    icon = marketData.Icon,
                    currentPrice = marketData.CurrentPrice,
                    priceChangePercent = marketData.PriceChangePercent,
                    ownedQuantity = marketData.PlayerOwned,
                    isAvailable = marketData.IsTradeable
                };

                card.Populate(cardData);

                // Wire events
                card.OnBuyClicked += HandleBuyClicked;
                card.OnSellClicked += HandleSellClicked;
                card.OnHistoryClicked += ShowPriceHistory;

                _spawnedCards[marketData.ResourceId] = card;
            }

            Debug.Log($"[MarketUI] Populated {_spawnedCards.Count} resource cards.");
        }

        /// <summary>
        /// Iterates through all spawned cards and refreshes their price
        /// displays from the cached resource data.
        /// </summary>
        public void UpdatePrices()
        {
            foreach (var marketData in _currentResources)
            {
                if (_spawnedCards.TryGetValue(marketData.ResourceId, out ResourceCard card))
                {
                    card.UpdatePrice(marketData.CurrentPrice, marketData.PriceChangePercent);
                }
            }
        }

        /// <summary>
        /// Updates the treasury display on the market screen.
        /// </summary>
        /// <param name="amount">Current treasury balance.</param>
        public void UpdateTreasury(float amount)
        {
            if (treasuryText != null)
            {
                treasuryText.text = $"Treasury: ${amount:##,##0}";
            }
        }

        /// <summary>
        /// Shows a price history overlay for the specified resource.
        /// Renders a simple bar chart using instantiated Image elements.
        /// </summary>
        /// <param name="resourceId">The resource to display history for.</param>
        public void ShowPriceHistory(string resourceId)
        {
            if (priceHistoryPanel == null || historyChartContainer == null)
            {
                Debug.LogWarning("[MarketUI] Price history panel or chart container not assigned.");
                return;
            }

            // Find resource data
            IronProtocol.GameSystems.Economy.ResourceMarketData data = _currentResources.Find(r => r.ResourceId == resourceId);
            if (data == null)
            {
                Debug.LogWarning($"[MarketUI] No market data found for resource '{resourceId}'.");
                return;
            }

            // Update title
            if (historyTitleText != null)
            {
                historyTitleText.text = $"Price History: {data.ResourceName}";
            }

            // Clear existing chart bars
            foreach (Transform child in historyChartContainer)
            {
                Destroy(child.gameObject);
            }

            // Render bar chart from price history
            List<float> history = data.PriceHistory;
            if (history == null || history.Count == 0)
            {
                Debug.Log("[MarketUI] No price history data available.");
                priceHistoryPanel.SetActive(true);
                return;
            }

            float maxPrice = 0f;
            foreach (float price in history)
            {
                if (price > maxPrice) maxPrice = price;
            }
            maxPrice = Mathf.Max(maxPrice, 1f); // Prevent division by zero

            // Instantiate a simple bar for each data point
            GameObject barPrefab = new GameObject("ChartBar", typeof(RectTransform), typeof(Image));
            barPrefab.SetActive(false);

            for (int i = 0; i < history.Count; i++)
            {
                GameObject bar = Instantiate(barPrefab, historyChartContainer);
                bar.SetActive(true);

                float normalizedHeight = history[i] / maxPrice;
                RectTransform rt = bar.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);

                float chartWidth = ((RectTransform)historyChartContainer).rect.width;
                float barWidth = chartWidth / history.Count;
                float barHeight = ((RectTransform)historyChartContainer).rect.height * normalizedHeight;

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(barWidth - 2f, 1f));
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(barHeight, 1f));
                rt.anchoredPosition = new Vector2(i * barWidth + barWidth * 0.5f, 0f);

                Image barImage = bar.GetComponent<Image>();
                if (barImage != null)
                {
                    barImage.color = (i > 0 && history[i] >= history[i - 1]) ? Color.green : Color.red;
                }
            }

            Destroy(barPrefab);
            priceHistoryPanel.SetActive(true);
        }

        /// <summary>
        /// Adds a market event entry to the event log.
        /// </summary>
        /// <param name="eventText">Descriptive text for the event.</param>
        public void ShowMarketEvent(string eventText)
        {
            if (eventLogContent == null || eventLogEntryPrefab == null)
            {
                Debug.LogWarning("[MarketUI] Event log content or prefab not assigned.");
                return;
            }

            GameObject entry = Instantiate(eventLogEntryPrefab, eventLogContent);
            Text textComponent = entry.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = $"[Turn {_currentTurn}] {eventText}";
            }
        }

        /// <summary>
        /// Updates the owned quantity display for a specific resource card.
        /// </summary>
        /// <param name="resourceId">The resource to update.</param>
        /// <param name="newQuantity">The player's new owned quantity.</param>
        public void UpdateOwnedQuantity(string resourceId, float newQuantity)
        {
            if (_spawnedCards.TryGetValue(resourceId, out ResourceCard card))
            {
                IronProtocol.GameSystems.Economy.ResourceMarketData data = _currentResources.Find(r => r.ResourceId == resourceId);
                if (data != null)
                {
                    data.PlayerOwned = newQuantity;
                    card.Populate(new ResourceCardData
                    {
                        resourceId = data.ResourceId,
                        resourceName = data.ResourceName,
                        icon = data.Icon,
                        currentPrice = data.CurrentPrice,
                        priceChangePercent = data.PriceChangePercent,
                        ownedQuantity = data.PlayerOwned,
                        isAvailable = data.IsTradeable
                    });
                }
            }
        }

        // ------------------------------------------------------------------
        // Private Methods
        // ------------------------------------------------------------------

        private void HandleBuyClicked(string resourceId)
        {
            Debug.Log($"[MarketUI] Buy clicked for: {resourceId}");
            OnBuyOrder?.Invoke(resourceId);
        }

        private void HandleSellClicked(string resourceId)
        {
            Debug.Log($"[MarketUI] Sell clicked for: {resourceId}");
            OnSellOrder?.Invoke(resourceId);
        }

        private void ClosePriceHistory()
        {
            if (priceHistoryPanel != null)
            {
                priceHistoryPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Destroys all spawned card instances and clears the lookup dictionary.
        /// </summary>
        private void ClearSpawnedCards()
        {
            foreach (var kvp in _spawnedCards)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.OnBuyClicked -= HandleBuyClicked;
                    kvp.Value.OnSellClicked -= HandleSellClicked;
                    kvp.Value.OnHistoryClicked -= ShowPriceHistory;

                    if (kvp.Value.gameObject != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }
                }
            }
            _spawnedCards.Clear();
        }

        /// <summary>Tracks the current turn for event log timestamps.</summary>
        private int _currentTurn = 1;

        /// <summary>Sets the current turn for event log display.</summary>
        public void SetTurn(int turn) => _currentTurn = turn;
    }
}
