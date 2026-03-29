// =============================================================================
// IRON PROTOCOL - Media System UI
// File: MediaUI.cs
// Description: User interface component for the Media & Propaganda system.
//              Provides scrollable news tickers, propaganda campaign panels,
//              public opinion displays, and campaign launch dialogs.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronProtocol.MediaPropaganda;

namespace IronProtocol.MediaPropaganda.UI
{
    // =========================================================================
    // UI DATA STRUCTURES
    // =========================================================================

    /// <summary>
    /// Configuration for styling and behavior of UI bar displays (news ticker, opinion bars, etc.).
    /// </summary>
    [Serializable]
    public class UIBarConfig
    {
        [Tooltip("Color for low values (0-33%).")]
        public Color lowColor = new Color(1f, 0.3f, 0.3f);

        [Tooltip("Color for medium values (34-66%).")]
        public Color mediumColor = new Color(1f, 0.8f, 0.2f);

        [Tooltip("Color for high values (67-100%).")]
        public Color highColor = new Color(0.2f, 0.8f, 0.3f);

        [Tooltip("Color for negative sentiment (below 40).")]
        public Color negativeColor = new Color(0.9f, 0.2f, 0.2f);

        [Tooltip("Color for positive sentiment (above 60).")]
        public Color positiveColor = new Color(0.2f, 0.7f, 0.3f);

        [Tooltip("Color for neutral sentiment (40-60).")]
        public Color neutralColor = new Color(0.7f, 0.7f, 0.7f);

        [Tooltip("Bar animation speed (seconds to fill).")]
        public float animationSpeed = 0.5f;

        [Tooltip("Default width for opinion bars.")]
        public float defaultBarWidth = 200f;
    }

    /// <summary>
    /// Configuration for the news ticker scrolling behavior.
    /// </summary>
    [Serializable]
    public class NewsTickerConfig
    {
        [Tooltip("Scroll speed in units per second.")]
        public float scrollSpeed = 50f;

        [Tooltip("Seconds between headline rotations if not scrolling.")]
        public float rotationInterval = 5f;

        [Tooltip("Whether headlines scroll continuously or rotate.")]
        public bool continuousScroll = true;

        [Tooltip("Font size for headline text.")]
        public int fontSize = 18;

        [Tooltip("Color for positive headlines.")]
        public Color positiveHeadlineColor = new Color(0.2f, 0.8f, 0.3f);

        [Tooltip("Color for negative headlines.")]
        public Color negativeHeadlineColor = new Color(0.9f, 0.2f, 0.2f);

        [Tooltip("Color for neutral headlines.")]
        public Color neutralHeadlineColor = new Color(0.9f, 0.9f, 0.9f);
    }

    /// <summary>
    /// Holds cached UI element references for a news headline entry.
    /// </summary>
    [Serializable]
    public class NewsHeadlineEntry
    {
        /// <summary>The display text for the headline.</summary>
        public string headline { get; set; }

        /// <summary>The outlet that published the headline.</summary>
        public string outletName { get; set; }

        /// <summary>Whether the headline is positive, negative, or neutral.</summary>
        public float sentiment { get; set; }

        /// <summary>Timestamp when the headline was published.</summary>
        public float timestamp { get; set; }

        /// <summary>
        /// Creates a new news headline entry.
        /// </summary>
        public NewsHeadlineEntry(string headline, string outletName, float sentiment)
        {
            this.headline = headline;
            this.outletName = outletName;
            this.sentiment = Mathf.Clamp(sentiment, -1f, 1f);
            this.timestamp = Time.time;
        }
    }

    /// <summary>
    /// Cached references to a propaganda campaign UI entry.
    /// </summary>
    [Serializable]
    public class CampaignUIEntry
    {
        /// <summary>The campaign this entry represents.</summary>
        public PropagandaCampaign campaign { get; set; }

        /// <summary>Assigned UI elements for this campaign.</summary>
        public Transform entryTransform { get; set; }

        /// <summary>Reference to the effectiveness bar image.</summary>
        public Image effectivenessBar { get; set; }

        /// <summary>Reference to the turns remaining text.</summary>
        public TextMeshProUGUI turnsText { get; set; }

        /// <summary>Reference to the campaign name text.</summary>
        public TextMeshProUGUI nameText { get; set; }

        /// <summary>Reference to the action type text.</summary>
        public TextMeshProUGUI actionText { get; set; }
    }

    // =========================================================================
    // MEDIA UI COMPONENT (MonoBehaviour)
    // =========================================================================

    /// <summary>
    /// UI component for the Media &amp; Propaganda system. Renders news tickers,
    /// propaganda campaign panels, public opinion displays, and campaign launch dialogs.
    /// <para>
    /// Attach this component to a Canvas-based GameObject. Configure UI element
    /// references in the Inspector. The component automatically subscribes to
    /// <see cref="MediaSystem"/> events for real-time updates.
    /// </para>
    /// <para>
    /// Usage:
    /// <list type="number">
    ///   <item>Place on a Canvas GameObject</item>
    ///   <item>Assign all required UI element references in the Inspector</item>
    ///   <item>Call <see cref="Initialize"/> with a reference to the <see cref="MediaSystem"/></item>
    ///   <item>Use public methods to show/hide panels programmatically</item>
    /// </list>
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MediaUI : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // SERIALIZED UI REFERENCES
        // -----------------------------------------------------------------

        [Header("News Ticker")]
        [Tooltip("Parent transform for scrolling news headlines.")]
        [SerializeField] private RectTransform newsTickerContent;

        [Tooltip("Text component displaying the current headline in the ticker.")]
        [SerializeField] private TextMeshProUGUI newsTickerText;

        [Tooltip("Viewport rect for the scrolling news ticker.")]
        [SerializeField] private RectTransform newsTickerViewport;

        [Tooltip("Background image for the news ticker bar.")]
        [SerializeField] private Image newsTickerBackground;

        [Header("Propaganda Panel")]
        [Tooltip("Parent transform for campaign list entries.")]
        [SerializeField] private RectTransform campaignListContent;

        [Tooltip("Prefab for creating campaign list entries.")]
        [SerializeField] private GameObject campaignEntryPrefab;

        [Tooltip("Panel root for the propaganda campaign panel.")]
        [SerializeField] private GameObject propagandaPanel;

        [Tooltip("Text showing total active campaigns count.")]
        [SerializeField] private TextMeshProUGUI campaignCountText;

        [Header("Public Opinion Panel")]
        [Tooltip("Panel root for the public opinion display.")]
        [SerializeField] private GameObject opinionPanel;

        [Tooltip("Text displaying the nation name for the opinion panel.")]
        [SerializeField] private TextMeshProUGUI opinionNationText;

        [Tooltip("Bar image for war support.")]
        [SerializeField] private Image warSupportBar;

        [Tooltip("Text for war support percentage.")]
        [SerializeField] private TextMeshProUGUI warSupportText;

        [Tooltip("Bar image for government approval.")]
        [SerializeField] private Image governmentApprovalBar;

        [Tooltip("Text for government approval percentage.")]
        [SerializeField] private TextMeshProUGUI governmentApprovalText;

        [Tooltip("Bar image for enemy view.")]
        [SerializeField] private Image enemyViewBar;

        [Tooltip("Text for enemy view percentage.")]
        [SerializeField] private TextMeshProUGUI enemyViewText;

        [Tooltip("Bar image for international reputation.")]
        [SerializeField] private Image internationalReputationBar;

        [Tooltip("Text for international reputation percentage.")]
        [SerializeField] private TextMeshProUGUI internationalReputationText;

        [Tooltip("Container for opinion of other nations entries.")]
        [SerializeField] private RectTransform otherNationsContent;

        [Tooltip("Prefab for other nations opinion entries.")]
        [SerializeField] private GameObject otherNationEntryPrefab;

        [Header("Campaign Launch Dialog")]
        [Tooltip("Root panel for the campaign launch dialog.")]
        [SerializeField] private GameObject launchDialogPanel;

        [Tooltip("Dropdown for selecting propaganda action type.")]
        [SerializeField] private TMP_Dropdown actionTypeDropdown;

        [Tooltip("Dropdown for selecting target nation.")]
        [SerializeField] private TMP_Dropdown targetNationDropdown;

        [Tooltip("Input field for campaign budget.")]
        [SerializeField] private TMP_InputField budgetInputField;

        [Tooltip("Text displaying max available budget.")]
        [SerializeField] private TextMeshProUGUI maxBudgetText;

        [Tooltip("Text displaying estimated effectiveness preview.")]
        [SerializeField] private TextMeshProUGUI effectivenessPreviewText;

        [Tooltip("Button to confirm campaign launch.")]
        [SerializeField] private Button launchConfirmButton;

        [Tooltip("Button to cancel campaign launch.")]
        [SerializeField] private Button launchCancelButton;

        [Tooltip("Text showing action description/effects in the dialog.")]
        [SerializeField] private TextMeshProUGUI actionDescriptionText;

        [Header("Configuration")]
        [Tooltip("Visual configuration for opinion bars and sentiment indicators.")]
        [SerializeField] private UIBarConfig barConfig = new UIBarConfig();

        [Tooltip("Configuration for the news ticker behavior.")]
        [SerializeField] private NewsTickerConfig tickerConfig = new NewsTickerConfig();

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // -----------------------------------------------------------------
        // PRIVATE STATE
        // -----------------------------------------------------------------

        private MediaSystem _mediaSystem;
        private CanvasGroup _canvasGroup;
        private List<NewsHeadlineEntry> _currentHeadlines = new List<NewsHeadlineEntry>();
        private List<CampaignUIEntry> _campaignEntries = new List<CampaignUIEntry>();
        private List<GameObject> _otherNationEntries = new List<GameObject>();
        private float _tickerScrollPosition;
        private int _currentHeadlineIndex;
        private float _lastHeadlineRotation;
        private string _currentNationId;
        private bool _isInitialized;

        // -----------------------------------------------------------------
        // PUBLIC PROPERTIES
        // -----------------------------------------------------------------

        /// <summary>Whether the news ticker is currently visible and active.</summary>
        public bool IsTickerVisible { get; private set; }

        /// <summary>Whether the propaganda panel is currently visible.</summary>
        public bool IsPropagandaPanelVisible => propagandaPanel != null && propagandaPanel.activeSelf;

        /// <summary>Whether the opinion panel is currently visible.</summary>
        public bool IsOpinionPanelVisible => opinionPanel != null && opinionPanel.activeSelf;

        /// <summary>Whether the launch dialog is currently visible.</summary>
        public bool IsLaunchDialogVisible => launchDialogPanel != null && launchDialogPanel.activeSelf;

        // -----------------------------------------------------------------
        // EVENTS
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired when the user confirms a campaign launch from the dialog.
        /// Arguments: (PropagandaAction, targetNationId, budget).
        /// </summary>
        public event Action<PropagandaAction, string, float> OnCampaignLaunchRequested;

        // =================================================================
        // INITIALIZATION
        // =================================================================

        /// <summary>
        /// Unity Awake. Caches component references.
        /// </summary>
        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Subscribe to button events if assigned
            if (launchConfirmButton != null)
            {
                launchConfirmButton.onClick.AddListener(OnLaunchConfirmClicked);
            }
            if (launchCancelButton != null)
            {
                launchCancelButton.onClick.AddListener(OnLaunchCancelClicked);
            }
            if (actionTypeDropdown != null)
            {
                actionTypeDropdown.onValueChanged.AddListener(OnActionTypeChanged);
            }
            if (budgetInputField != null)
            {
                budgetInputField.onValueChanged.AddListener(OnBudgetChanged);
            }
        }

        /// <summary>
        /// Unity OnDestroy. Unsubscribes from events to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            if (_mediaSystem != null)
            {
                _mediaSystem.OnNewsPublished -= HandleNewsPublished;
                _mediaSystem.OnOpinionShifted -= HandleOpinionShifted;
                _mediaSystem.OnCampaignLaunched -= HandleCampaignLaunched;
                _mediaSystem.OnCampaignEnded -= HandleCampaignEnded;
            }

            if (launchConfirmButton != null) launchConfirmButton.onClick.RemoveAllListeners();
            if (launchCancelButton != null) launchCancelButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Initializes the UI component with a reference to the MediaSystem.
        /// Subscribes to system events for real-time UI updates.
        /// </summary>
        /// <param name="mediaSystem">The active MediaSystem instance.</param>
        public void Initialize(MediaSystem mediaSystem)
        {
            if (mediaSystem == null)
            {
                Debug.LogWarning("[MediaUI] Initialize: MediaSystem reference is null.");
                return;
            }

            _mediaSystem = mediaSystem;

            // Subscribe to media system events
            _mediaSystem.OnNewsPublished += HandleNewsPublished;
            _mediaSystem.OnOpinionShifted += HandleOpinionShifted;
            _mediaSystem.OnCampaignLaunched += HandleCampaignLaunched;
            _mediaSystem.OnCampaignEnded += HandleCampaignEnded;

            // Initialize action type dropdown options
            if (actionTypeDropdown != null)
            {
                actionTypeDropdown.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>();
                foreach (PropagandaAction action in Enum.GetValues(typeof(PropagandaAction)))
                {
                    options.Add(new TMP_Dropdown.OptionData(action.ToString()));
                }
                actionTypeDropdown.AddOptions(options);
            }

            _isInitialized = true;

            if (enableDebugLogs)
            {
                Debug.Log("[MediaUI] Initialized and subscribed to MediaSystem events.");
            }
        }

        // =================================================================
        // NEWS TICKER
        // =================================================================

        /// <summary>
        /// Displays a scrolling news ticker with the provided headlines.
        /// <para>
        /// If <see cref="NewsTickerConfig.continuousScroll"/> is enabled,
        /// headlines scroll smoothly from right to left. Otherwise, they
        /// rotate at the configured interval.
        /// </para>
        /// </summary>
        /// <param name="headlines">List of headline strings to display.</param>
        public void ShowNewsTicker(List<string> headlines)
        {
            if (headlines == null || headlines.Count == 0)
            {
                HideNewsTicker();
                return;
            }

            // Convert string headlines to entries
            _currentHeadlines.Clear();
            foreach (var headline in headlines)
            {
                _currentHeadlines.Add(new NewsHeadlineEntry(headline, "", 0f));
            }

            // Show ticker elements
            if (newsTickerBackground != null)
            {
                newsTickerBackground.gameObject.SetActive(true);
            }
            if (newsTickerText != null)
            {
                newsTickerText.gameObject.SetActive(true);
            }

            _tickerScrollPosition = 0f;
            _currentHeadlineIndex = 0;
            _lastHeadlineRotation = Time.time;
            IsTickerVisible = true;

            // Display first headline immediately
            UpdateTickerDisplay();

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaUI] News ticker showing {headlines.Count} headlines.");
            }
        }

        /// <summary>
        /// Displays a scrolling news ticker with rich headline entries including
        /// outlet information and sentiment data.
        /// </summary>
        /// <param name="headlines">List of rich headline entries.</param>
        public void ShowNewsTicker(List<NewsHeadlineEntry> headlines)
        {
            if (headlines == null || headlines.Count == 0)
            {
                HideNewsTicker();
                return;
            }

            _currentHeadlines = new List<NewsHeadlineEntry>(headlines);

            if (newsTickerBackground != null)
            {
                newsTickerBackground.gameObject.SetActive(true);
            }
            if (newsTickerText != null)
            {
                newsTickerText.gameObject.SetActive(true);
            }

            _tickerScrollPosition = 0f;
            _currentHeadlineIndex = 0;
            _lastHeadlineRotation = Time.time;
            IsTickerVisible = true;

            UpdateTickerDisplay();

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaUI] News ticker showing {_currentHeadlines.Count} rich headlines.");
            }
        }

        /// <summary>
        /// Hides the news ticker and clears headline data.
        /// </summary>
        public void HideNewsTicker()
        {
            IsTickerVisible = false;

            if (newsTickerBackground != null)
            {
                newsTickerBackground.gameObject.SetActive(false);
            }
            if (newsTickerText != null)
            {
                newsTickerText.gameObject.SetActive(false);
            }

            _currentHeadlines.Clear();
        }

        /// <summary>
        /// Adds a single headline to the news ticker.
        /// </summary>
        /// <param name="headline">The headline text to add.</param>
        /// <param name="outletName">Name of the publishing outlet.</param>
        /// <param name="sentiment">Sentiment value (-1 negative, 0 neutral, 1 positive).</param>
        public void AddHeadline(string headline, string outletName = "", float sentiment = 0f)
        {
            var entry = new NewsHeadlineEntry(headline, outletName, sentiment);
            _currentHeadlines.Add(entry);

            // Keep max 50 headlines
            if (_currentHeadlines.Count > 50)
            {
                _currentHeadlines.RemoveAt(0);
            }

            if (!IsTickerVisible && _currentHeadlines.Count > 0)
            {
                ShowNewsTicker(_currentHeadlines);
            }
        }

        /// <summary>
        /// Updates the ticker display each frame. Handles scrolling and rotation.
        /// </summary>
        private void UpdateTickerDisplay()
        {
            if (!IsTickerVisible || _currentHeadlines.Count == 0 || newsTickerText == null) return;

            if (tickerConfig.continuousScroll && newsTickerContent != null)
            {
                // Continuous scrolling mode
                _tickerScrollPosition += tickerConfig.scrollSpeed * Time.deltaTime;

                float totalWidth = newsTickerContent.rect.width;
                if (_tickerScrollPosition > totalWidth)
                {
                    _tickerScrollPosition = -newsTickerViewport.rect.width;
                }

                newsTickerContent.anchoredPosition = new Vector2(-_tickerScrollPosition, 0f);
            }
            else
            {
                // Rotation mode - switch headlines at intervals
                if (Time.time - _lastHeadlineRotation >= tickerConfig.rotationInterval)
                {
                    _currentHeadlineIndex = (_currentHeadlineIndex + 1) % _currentHeadlines.Count;
                    _lastHeadlineRotation = Time.time;
                }

                var currentEntry = _currentHeadlines[_currentHeadlineIndex];
                newsTickerText.text = FormatHeadlineText(currentEntry);
                newsTickerText.color = GetSentimentColor(currentEntry.sentiment);
                newsTickerText.fontSize = tickerConfig.fontSize;
            }
        }

        /// <summary>
        /// Formats a headline entry into display text.
        /// </summary>
        /// <param name="entry">The headline entry to format.</param>
        /// <returns>Formatted display string.</returns>
        private string FormatHeadlineText(NewsHeadlineEntry entry)
        {
            if (string.IsNullOrEmpty(entry.outletName))
            {
                return entry.headline;
            }

            return $"<b>[{entry.outletName}]</b> {entry.headline}";
        }

        /// <summary>
        /// Returns a color based on sentiment value.
        /// </summary>
        /// <param name="sentiment">Sentiment value (-1 to 1).</param>
        /// <returns>Appropriate color.</returns>
        private Color GetSentimentColor(float sentiment)
        {
            if (sentiment > 0.1f) return tickerConfig.positiveHeadlineColor;
            if (sentiment < -0.1f) return tickerConfig.negativeHeadlineColor;
            return tickerConfig.neutralHeadlineColor;
        }

        // =================================================================
        // PROPAGANDA CAMPAIGN PANEL
        // =================================================================

        /// <summary>
        /// Displays the propaganda campaign panel with all active campaigns
        /// for the current context (nation or global).
        /// <para>
        /// Each campaign entry shows:
        /// <list type="bullet">
        ///   <item>Campaign name and ID</item>
        ///   <item>Action type with icon</item>
        ///   <item>Effectiveness bar (animated)</item>
        ///   <item>Turns remaining</item>
        ///   <item>Sponsor and target nations</item>
        ///   <item>Budget allocated</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="campaigns">List of active campaigns to display.</param>
        public void ShowPropagandaPanel(List<PropagandaCampaign> campaigns)
        {
            if (propagandaPanel == null)
            {
                Debug.LogWarning("[MediaUI] ShowPropagandaPanel: propagandaPanel not assigned in Inspector.");
                return;
            }

            // Clear existing entries
            ClearCampaignEntries();

            propagandaPanel.SetActive(true);

            // Update campaign count
            if (campaignCountText != null)
            {
                campaignCountText.text = $"Active Campaigns: {campaigns?.Count ?? 0}";
            }

            if (campaigns == null || campaigns.Count == 0)
            {
                if (enableDebugLogs) Debug.Log("[MediaUI] No active campaigns to display.");
                return;
            }

            // Create UI entries for each campaign
            foreach (var campaign in campaigns)
            {
                if (!campaign.isActive) continue;
                CreateCampaignEntry(campaign);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaUI] Propaganda panel showing {_campaignEntries.Count} campaigns.");
            }
        }

        /// <summary>
        /// Hides the propaganda campaign panel.
        /// </summary>
        public void HidePropagandaPanel()
        {
            if (propagandaPanel != null)
            {
                propagandaPanel.SetActive(false);
            }
            ClearCampaignEntries();
        }

        /// <summary>
        /// Creates a UI entry for a single campaign.
        /// </summary>
        /// <param name="campaign">The campaign to create an entry for.</param>
        private void CreateCampaignEntry(PropagandaCampaign campaign)
        {
            if (campaignListContent == null)
            {
                Debug.LogWarning("[MediaUI] campaignListContent not assigned.");
                return;
            }

            GameObject entryObj;
            if (campaignEntryPrefab != null)
            {
                entryObj = Instantiate(campaignEntryPrefab, campaignListContent);
            }
            else
            {
                entryObj = CreateDefaultCampaignEntry(campaign);
            }

            if (entryObj == null) return;

            entryObj.SetActive(true);

            var entry = new CampaignUIEntry
            {
                campaign = campaign,
                entryTransform = entryObj.transform,
                effectivenessBar = entryObj.GetComponentInChildren<Image>(),
                turnsText = entryObj.GetComponentInChildren<TextMeshProUGUI>()
            };

            // Search for named text components
            var texts = entryObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 1) entry.nameText = texts[0];
            if (texts.Length >= 2) entry.actionText = texts[1];
            if (texts.Length >= 3) entry.turnsText = texts[2];

            // Update entry content
            UpdateCampaignEntryContent(entry);

            _campaignEntries.Add(entry);
        }

        /// <summary>
        /// Updates the content of a campaign entry to reflect current data.
        /// </summary>
        /// <param name="entry">The campaign UI entry to update.</param>
        private void UpdateCampaignEntryContent(CampaignUIEntry entry)
        {
            if (entry == null || entry.campaign == null) return;

            var campaign = entry.campaign;

            if (entry.nameText != null)
            {
                entry.nameText.text = $"{campaign.action}: {campaign.sponsorNation} -> {campaign.targetNation}";
            }

            if (entry.actionText != null)
            {
                entry.actionText.text = $"Budget: {campaign.budget:F0} | Effect: {campaign.effectiveness:F1}";
            }

            if (entry.turnsText != null)
            {
                entry.turnsText.text = $"{campaign.turnsRemaining} turns remaining";
            }

            // Update effectiveness bar
            if (entry.effectivenessBar != null)
            {
                float fill = campaign.effectiveness / 100f;
                entry.effectivenessBar.fillAmount = fill;
                entry.effectivenessBar.color = GetBarColor(campaign.effectiveness);
            }
        }

        /// <summary>
        /// Creates a default campaign entry when no prefab is assigned.
        /// </summary>
        private GameObject CreateDefaultCampaignEntry(PropagandaCampaign campaign)
        {
            var entryObj = new GameObject($"CampaignEntry_{campaign.campaignId}");
            entryObj.transform.SetParent(campaignListContent, false);

            // Create background
            var bg = new GameObject("Background");
            bg.transform.SetParent(entryObj.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            // Create text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(entryObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.1f);
            textRect.anchorMax = new Vector2(0.95f, 0.9f);
            textRect.sizeDelta = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = $"{campaign.action}: {campaign.sponsorNation} -> {campaign.targetNation} ({campaign.turnsRemaining} turns)";
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            // Create effectiveness bar
            var barObj = new GameObject("Bar");
            barObj.transform.SetParent(entryObj.transform, false);
            var barRect = barObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.75f, 0.2f);
            barRect.anchorMax = new Vector2(0.95f, 0.8f);
            barRect.sizeDelta = Vector2.zero;
            var barImage = barObj.AddComponent<Image>();
            barImage.color = GetBarColor(campaign.effectiveness);
            barImage.type = Image.Type.Filled;
            barImage.fillMethod = Image.FillMethod.Horizontal;
            barImage.fillAmount = campaign.effectiveness / 100f;

            // Set entry height
            var entryRect = entryObj.GetComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(0f, 40f);

            return entryObj;
        }

        /// <summary>
        /// Clears all campaign entry UI objects.
        /// </summary>
        private void ClearCampaignEntries()
        {
            foreach (var entry in _campaignEntries)
            {
                if (entry.entryTransform != null)
                {
                    Destroy(entry.entryTransform.gameObject);
                }
            }
            _campaignEntries.Clear();
        }

        /// <summary>
        /// Refreshes all displayed campaign entries with current data.
        /// </summary>
        public void RefreshCampaignPanel()
        {
            foreach (var entry in _campaignEntries)
            {
                UpdateCampaignEntryContent(entry);
            }
        }

        // =================================================================
        // PUBLIC OPINION PANEL
        // =================================================================

        /// <summary>
        /// Displays the public opinion panel for a specific nation.
        /// <para>
        /// Shows opinion bars for:
        /// <list type="bullet">
        ///   <item>War Support (0-100)</item>
        ///   <item>Government Approval (0-100)</item>
        ///   <item>Enemy View (0-100)</item>
        ///   <item>International Reputation (0-100)</item>
        ///   <item>Opinions of other nations (0-100 each)</item>
        /// </list>
        /// Each bar is color-coded based on value ranges.
        /// </para>
        /// </summary>
        /// <param name="opinion">The public opinion snapshot to display.</param>
        public void ShowPublicOpinionPanel(PublicOpinion opinion)
        {
            if (opinion == null)
            {
                Debug.LogWarning("[MediaUI] ShowPublicOpinionPanel: opinion is null.");
                return;
            }

            if (opinionPanel == null)
            {
                Debug.LogWarning("[MediaUI] ShowPublicOpinionPanel: opinionPanel not assigned in Inspector.");
                return;
            }

            _currentNationId = opinion.nationId;
            opinionPanel.SetActive(true);

            // Update nation name
            if (opinionNationText != null)
            {
                opinionNationText.text = $"Public Opinion: {opinion.nationId}";
            }

            // Update opinion bars
            UpdateOpinionBar(warSupportBar, warSupportText, "War Support", opinion.warSupport);
            UpdateOpinionBar(governmentApprovalBar, governmentApprovalText, "Gov. Approval", opinion.governmentApproval);
            UpdateOpinionBar(enemyViewBar, enemyViewText, "Enemy View", opinion.enemyView);
            UpdateOpinionBar(internationalReputationBar, internationalReputationText, "Int'l Reputation", opinion.internationalReputation);

            // Update other nations opinions
            UpdateOtherNationsOpinions(opinion);

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaUI] Opinion panel displayed for '{opinion.nationId}'. " +
                          $"WarSupport={opinion.warSupport:F0}, Approval={opinion.governmentApproval:F0}, " +
                          $"Rep={opinion.internationalReputation:F0}.");
            }
        }

        /// <summary>
        /// Hides the public opinion panel.
        /// </summary>
        public void HideOpinionPanel()
        {
            if (opinionPanel != null)
            {
                opinionPanel.SetActive(false);
            }
            ClearOtherNationsEntries();
        }

        /// <summary>
        /// Updates a single opinion bar and its associated text.
        /// </summary>
        /// <param name="bar">The bar image to update.</param>
        /// <param name="text">The text component to update.</param>
        /// <param name="label">The label for the opinion dimension.</param>
        /// <param name="value">The current opinion value (0-100).</param>
        private void UpdateOpinionBar(Image bar, TextMeshProUGUI text, string label, float value)
        {
            if (bar != null)
            {
                bar.fillAmount = value / 100f;
                bar.color = GetBarColor(value);
            }

            if (text != null)
            {
                text.text = $"{label}: {value:F0}%";
            }
        }

        /// <summary>
        /// Updates the "opinions of other nations" section.
        /// </summary>
        /// <param name="opinion">The opinion data containing other nation opinions.</param>
        private void UpdateOtherNationsOpinions(PublicOpinion opinion)
        {
            ClearOtherNationsEntries();

            if (otherNationsContent == null || opinion.opinionsOfOtherNations == null) return;

            foreach (var kvp in opinion.opinionsOfOtherNations)
            {
                string otherNation = kvp.Key;
                float otherOpinion = kvp.Value;

                GameObject entryObj;
                if (otherNationEntryPrefab != null)
                {
                    entryObj = Instantiate(otherNationEntryPrefab, otherNationsContent);
                }
                else
                {
                    entryObj = CreateDefaultOtherNationEntry(otherNation, otherOpinion);
                }

                if (entryObj == null) continue;

                entryObj.SetActive(true);

                // Set text content
                var text = entryObj.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{otherNation}: {otherOpinion:F0}%";
                    text.color = GetBarColor(otherOpinion);
                }

                // Set bar fill
                var bar = entryObj.GetComponentInChildren<Image>();
                if (bar != null && bar.type == Image.Type.Filled)
                {
                    bar.fillAmount = otherOpinion / 100f;
                    bar.color = GetBarColor(otherOpinion);
                }

                _otherNationEntries.Add(entryObj);
            }
        }

        /// <summary>
        /// Creates a default other-nation opinion entry when no prefab is assigned.
        /// </summary>
        private GameObject CreateDefaultOtherNationEntry(string nation, float opinion)
        {
            var entryObj = new GameObject($"OpinionEntry_{nation}");
            entryObj.transform.SetParent(otherNationsContent, false);

            var entryRect = entryObj.AddComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(0f, 30f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(entryObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = new Vector2(0.6f, Vector2.one.y);
            textRect.sizeDelta = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = $"{nation}: {opinion:F0}%";
            text.fontSize = 12;
            text.color = Color.white;

            var barObj = new GameObject("Bar");
            barObj.transform.SetParent(entryObj.transform, false);
            var barRect = barObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.65f, 0.2f);
            barRect.anchorMax = new Vector2(0.95f, 0.8f);
            barRect.sizeDelta = Vector2.zero;
            var barImage = barObj.AddComponent<Image>();
            barImage.color = GetBarColor(opinion);
            barImage.type = Image.Type.Filled;
            barImage.fillMethod = Image.FillMethod.Horizontal;
            barImage.fillAmount = opinion / 100f;

            return entryObj;
        }

        /// <summary>
        /// Clears all other-nation opinion entry UI objects.
        /// </summary>
        private void ClearOtherNationsEntries()
        {
            foreach (var entry in _otherNationEntries)
            {
                if (entry != null)
                {
                    Destroy(entry);
                }
            }
            _otherNationEntries.Clear();
        }

        // =================================================================
        // CAMPAIGN LAUNCH DIALOG
        // =================================================================

        /// <summary>
        /// Shows the campaign launch dialog for a specific nation.
        /// <para>
        /// The dialog allows the player to:
        /// <list type="number">
        ///   <item>Select a propaganda action type from a dropdown</item>
        ///   <item>Choose a target nation</item>
        ///   <item>Set a budget amount (clamped to available funds)</item>
        ///   <item>Preview estimated effectiveness</item>
        ///   <item>Confirm or cancel the launch</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="nationId">The nation launching the campaign.</param>
        /// <param name="maxBudget">Maximum budget available for the campaign.</param>
        /// <param name="availableTargets">List of valid target nations.</param>
        public void ShowLaunchCampaignDialog(string nationId, float maxBudget, List<string> availableTargets = null)
        {
            if (launchDialogPanel == null)
            {
                Debug.LogWarning("[MediaUI] ShowLaunchCampaignDialog: launchDialogPanel not assigned.");
                return;
            }

            _currentNationId = nationId;
            launchDialogPanel.SetActive(true);

            // Set max budget display
            if (maxBudgetText != null)
            {
                maxBudgetText.text = $"Available Budget: {maxBudget:F0}";
            }

            // Configure budget input
            if (budgetInputField != null)
            {
                budgetInputField.text = maxBudget.ToString("F0");
                budgetInputField.characterValidation = TMP_InputField.CharacterValidation.Decimal;
            }

            // Configure target nation dropdown
            if (targetNationDropdown != null && availableTargets != null)
            {
                targetNationDropdown.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>();
                foreach (var target in availableTargets)
                {
                    options.Add(new TMP_Dropdown.OptionData(target));
                }
                targetNationDropdown.AddOptions(options);
            }

            // Update effectiveness preview
            UpdateEffectivenessPreview();

            // Update action description
            UpdateActionDescription();

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaUI] Campaign launch dialog shown for '{nationId}' (max budget: {maxBudget:F0}).");
            }
        }

        /// <summary>
        /// Hides the campaign launch dialog.
        /// </summary>
        public void HideLaunchCampaignDialog()
        {
            if (launchDialogPanel != null)
            {
                launchDialogPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Updates the effectiveness preview text based on current dialog settings.
        /// </summary>
        private void UpdateEffectivenessPreview()
        {
            if (effectivenessPreviewText == null || _mediaSystem == null) return;

            if (float.TryParse(budgetInputField?.text, out float budget) && budget > 0)
            {
                // Create a temporary campaign to estimate effectiveness
                var tempCampaign = new PropagandaCampaign(
                    "preview", GetSelectedAction(), _currentNationId,
                    _currentNationId, budget, 5
                );

                float effectiveness = _mediaSystem.CalculateCampaignEffectiveness(tempCampaign);
                effectivenessPreviewText.text = $"Est. Effectiveness: {effectiveness:F1}/100";
                effectivenessPreviewText.color = GetBarColor(effectiveness);
            }
            else
            {
                effectivenessPreviewText.text = "Est. Effectiveness: N/A";
                effectivenessPreviewText.color = Color.gray;
            }
        }

        /// <summary>
        /// Updates the action description text based on the selected action type.
        /// </summary>
        private void UpdateActionDescription()
        {
            if (actionDescriptionText == null) return;

            PropagandaAction selectedAction = GetSelectedAction();
            actionDescriptionText.text = GetActionDescription(selectedAction);
        }

        /// <summary>
        /// Returns a description of the effects of a propaganda action.
        /// </summary>
        /// <param name="action">The action to describe.</param>
        /// <returns>Human-readable description string.</returns>
        private string GetActionDescription(PropagandaAction action)
        {
            switch (action)
            {
                case PropagandaAction.BoostNationalism:
                    return "Boosts national morale. +15 War Support, +10 Government Approval.";

                case PropagandaAction.SmearEnemy:
                    return "Undermine a rival nation's reputation. -20 opinion of target in domestic media.";

                case PropagandaAction.MilitaryRecruitment:
                    return "Encourage military service. +20% unit production speed for 3 turns.";

                case PropagandaAction.EconomicConfidence:
                    return "Project economic strength. +10 treasury income from trade.";

                case PropagandaAction.CulturalExport:
                    return "Promote national culture abroad. +5 international reputation per turn.";

                case PropagandaAction.Disinformation:
                    return "Spread false information. -30 credibility of independent media in target nation.";

                case PropagandaAction.Censorship:
                    return "Suppress dissenting voices. +10 stability, -15 international reputation.";

                case PropagandaAction.FreePress:
                    return "Allow independent journalism. -5 stability, +15 international reputation, better intel.";

                default:
                    return "Select an action to see its effects.";
            }
        }

        /// <summary>
        /// Gets the currently selected propaganda action from the dropdown.
        /// </summary>
        /// <returns>The selected <see cref="PropagandaAction"/>.</returns>
        private PropagandaAction GetSelectedAction()
        {
            if (actionTypeDropdown == null) return PropagandaAction.BoostNationalism;

            int index = actionTypeDropdown.value;
            if (index >= 0 && index < Enum.GetValues(typeof(PropagandaAction)).Length)
            {
                return (PropagandaAction)Enum.GetValues(typeof(PropagandaAction)).GetValue(index);
            }

            return PropagandaAction.BoostNationalism;
        }

        // -----------------------------------------------------------------
        // UI EVENT HANDLERS
        // -----------------------------------------------------------------

        /// <summary>
        /// Handles the confirm button click on the campaign launch dialog.
        /// </summary>
        private void OnLaunchConfirmClicked()
        {
            PropagandaAction action = GetSelectedAction();
            string target = targetNationDropdown?.options.Count > 0
                ? targetNationDropdown.options[targetNationDropdown.value].text
                : _currentNationId;

            float budget = 0f;
            if (!float.TryParse(budgetInputField?.text, out budget) || budget <= 0f)
            {
                Debug.LogWarning("[MediaUI] Invalid budget value in launch dialog.");
                return;
            }

            OnCampaignLaunchRequested?.Invoke(action, target, budget);
            HideLaunchCampaignDialog();

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaUI] Campaign launch requested: {action} -> {target}, budget={budget:F0}.");
            }
        }

        /// <summary>
        /// Handles the cancel button click on the campaign launch dialog.
        /// </summary>
        private void OnLaunchCancelClicked()
        {
            HideLaunchCampaignDialog();

            if (enableDebugLogs)
            {
                Debug.Log("[MediaUI] Campaign launch dialog cancelled.");
            }
        }

        /// <summary>
        /// Handles action type dropdown value change.
        /// </summary>
        /// <param name="index">New dropdown index.</param>
        private void OnActionTypeChanged(int index)
        {
            UpdateActionDescription();
            UpdateEffectivenessPreview();
        }

        /// <summary>
        /// Handles budget input field value change.
        /// </summary>
        /// <param name="value">New budget text.</param>
        private void OnBudgetChanged(string value)
        {
            UpdateEffectivenessPreview();
        }

        // -----------------------------------------------------------------
        // MEDIA SYSTEM EVENT HANDLERS
        // -----------------------------------------------------------------

        /// <summary>
        /// Handles the OnNewsPublished event from MediaSystem.
        /// Adds the new headline to the ticker.
        /// </summary>
        private void HandleNewsPublished(string outletId, string headline)
        {
            if (_mediaSystem == null) return;

            var outlet = _mediaSystem.GetOutlet(outletId);
            string outletName = outlet?.name ?? outletId;

            AddHeadline(headline, outletName, 0f);
        }

        /// <summary>
        /// Handles the OnOpinionShifted event from MediaSystem.
        /// Refreshes the opinion panel if visible.
        /// </summary>
        private void HandleOpinionShifted(string nationId, float shift)
        {
            if (_currentNationId == nationId && IsOpinionPanelVisible && _mediaSystem != null)
            {
                var opinion = _mediaSystem.GetPublicOpinion(nationId);
                if (opinion != null)
                {
                    ShowPublicOpinionPanel(opinion);
                }
            }
        }

        /// <summary>
        /// Handles the OnCampaignLaunched event from MediaSystem.
        /// Refreshes the campaign panel if visible.
        /// </summary>
        private void HandleCampaignLaunched(string campaignId, string sponsorNation)
        {
            if (IsPropagandaPanelVisible && _mediaSystem != null)
            {
                var campaigns = _mediaSystem.GetCampaignsByNation(sponsorNation);
                if (campaigns != null)
                {
                    ShowPropagandaPanel(campaigns);
                }
            }
        }

        /// <summary>
        /// Handles the OnCampaignEnded event from MediaSystem.
        /// Refreshes the campaign panel if visible.
        /// </summary>
        private void HandleCampaignEnded(string campaignId, string reason)
        {
            if (IsPropagandaPanelVisible && _mediaSystem != null)
            {
                var allCampaigns = _mediaSystem.ActiveCampaigns.Values
                    .Where(c => c.isActive)
                    .ToList();

                ShowPropagandaPanel(allCampaigns);
            }
        }

        // =================================================================
        // UTILITY METHODS
        // =================================================================

        /// <summary>
        /// Returns a color based on a value's position in the 0-100 range.
        /// </summary>
        /// <param name="value">The value to evaluate (0-100).</param>
        /// <returns>Appropriate color from the bar configuration.</returns>
        private Color GetBarColor(float value)
        {
            float normalized = Mathf.Clamp01(value / 100f);

            if (normalized < 0.33f)
            {
                return Color.Lerp(barConfig.lowColor, barConfig.mediumColor, normalized / 0.33f);
            }
            else if (normalized < 0.67f)
            {
                return Color.Lerp(barConfig.mediumColor, barConfig.highColor, (normalized - 0.33f) / 0.34f);
            }
            else
            {
                return barConfig.highColor;
            }
        }

        /// <summary>
        /// Toggles panel visibility with optional animation.
        /// </summary>
        /// <param name="panel">The panel GameObject to toggle.</param>
        /// <param name="show">Whether to show (true) or hide (false).</param>
        /// <param name="animate">Whether to animate the transition.</param>
        private void TogglePanel(GameObject panel, bool show, bool animate = true)
        {
            if (panel == null) return;

            if (animate && _canvasGroup != null)
            {
                StartCoroutine(AnimatePanelToggle(panel, show));
            }
            else
            {
                panel.SetActive(show);
            }
        }

        /// <summary>
        /// Coroutine for animating panel toggle with fade effect.
        /// </summary>
        private System.Collections.IEnumerator AnimatePanelToggle(GameObject panel, bool show)
        {
            if (show)
            {
                panel.SetActive(true);
                _canvasGroup.alpha = 0f;

                while (_canvasGroup.alpha < 1f)
                {
                    _canvasGroup.alpha = Mathf.Min(1f, _canvasGroup.alpha + Time.deltaTime * 3f);
                    yield return null;
                }
            }
            else
            {
                _canvasGroup.alpha = 1f;

                while (_canvasGroup.alpha > 0f)
                {
                    _canvasGroup.alpha = Mathf.Max(0f, _canvasGroup.alpha - Time.deltaTime * 3f);
                    yield return null;
                }

                panel.SetActive(false);
            }
        }

        /// <summary>
        /// Unity Update. Processes news ticker animation each frame.
        /// </summary>
        private void Update()
        {
            if (IsTickerVisible)
            {
                UpdateTickerDisplay();
            }
        }
    }
}
