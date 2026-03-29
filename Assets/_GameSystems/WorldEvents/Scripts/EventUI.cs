// ============================================================================
// Iron Protocol - Event UI System
// UI components for displaying world events, event history, and active
// event panels to the player. Provides modal popups, scrolling lists, and
// notification management.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IronProtocol.HexMap;

namespace IronProtocol.WorldEvents
{
    // ========================================================================
    // UI DATA BINDING CLASSES
    // ========================================================================

    /// <summary>
    /// Stores runtime UI state for an event list item in the active events panel.
    /// Used for recycling/pooling list entries without recreating GameObjects.
    /// </summary>
    [Serializable]
    public class EventListItem
    {
        /// <summary>The WorldEvent this list item represents.</summary>
        public WorldEvent Event { get; set; }

        /// <summary>The UI GameObject for this list item.</summary>
        public GameObject ListItem { get; set; }

        /// <summary>Whether this list item is currently visible in the panel.</summary>
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// Stores UI state for the event history panel.
    /// </summary>
    [Serializable]
    public class HistoryListItem
    {
        /// <summary>The expired WorldEvent this history entry represents.</summary>
        public WorldEvent Event { get; set; }

        /// <summary>The UI GameObject for this history entry.</summary>
        public GameObject ListItem { get; set; }
    }

    // ========================================================================
    // EVENT UI MANAGER (MONOBEHAVIOUR)
    // ========================================================================

    /// <summary>
    /// Manages all UI components related to world events: modal popups for new events,
    /// the active events sidebar/panel, and the event history browser.
    /// 
    /// <para>Setup Requirements:</para>
    /// <list type="bullet">
    ///   <item>Assign all UI references in the Inspector (prefabs, panels, buttons).</item>
    ///   <item>Ensure a Canvas and EventSystem exist in the scene.</item>
    ///   <item>Subscribe to <see cref="WorldEventManager.OnEventTriggered"/> for automatic popups.</item>
    /// </list>
    /// 
    /// <para>UI Prefab Structure Expected:</para>
    /// <list type="bullet">
    ///   <item>Event Popup: modal dialog with title, icon, description, effect list, acknowledge button.</item>
    ///   <item>Active Event Item: compact list entry with icon, name, severity, turns remaining.</item>
    ///   <item>History Event Item: similar to active item but shows turn triggered instead of remaining.</item>
    /// </list>
    /// </summary>
    public class EventUI : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // INSPECTOR UI REFERENCES
        // --------------------------------------------------------------------

        [Header("Event Popup")]
        [Tooltip("The modal popup panel shown when a new event triggers.")]
        [SerializeField] private GameObject eventPopupPanel;

        [Tooltip("Icon image in the event popup header.")]
        [SerializeField] private Image popupEventIcon;

        [Tooltip("Event name text in the popup header.")]
        [SerializeField] private TextMeshProUGUI popupEventName;

        [Tooltip("Event type / category label.")]
        [SerializeField] private TextMeshProUGUI popupEventType;

        [Tooltip("Severity badge text.")]
        [SerializeField] private TextMeshProUGUI popupSeverity;

        [Tooltip("Event description body text.")]
        [SerializeField] private TextMeshProUGUI popupDescription;

        [Tooltip("Scrollable list of effect descriptions in the popup.")]
        [SerializeField] private TextMeshProUGUI popupEffectsList;

        [Tooltip("Turn info text (e.g., 'Duration: 3 turns').")]
        [SerializeField] private TextMeshProUGUI popupDurationInfo;

        [Tooltip("Target info text (nation or region name).")]
        [SerializeField] private TextMeshProUGUI popupTargetInfo;

        [Tooltip("Button to acknowledge and dismiss the popup.")]
        [SerializeField] private Button popupAcknowledgeButton;

        [Tooltip("Overlay/background that blocks interaction behind the popup.")]
        [SerializeField] private GameObject popupOverlay;

        [Header("Active Events Panel")]
        [Tooltip("The panel showing all currently active events.")]
        [SerializeField] private GameObject activeEventsPanel;

        [Tooltip("Toggle button to show/hide the active events panel.")]
        [SerializeField] private Button activeEventsToggle;

        [Tooltip("Content container for active event list items.")]
        [SerializeField] private Transform activeEventsContent;

        [Tooltip("Prefab for a single active event list item.")]
        [SerializeField] private GameObject activeEventItemPrefab;

        [Tooltip("Text showing the count of active events on the toggle button.")]
        [SerializeField] private TextMeshProUGUI activeEventsCountBadge;

        [Tooltip("Text shown when no events are active.")]
        [SerializeField] private GameObject noActiveEventsLabel;

        [Header("Event History Panel")]
        [Tooltip("The panel showing all past/expired events.")]
        [SerializeField] private GameObject eventHistoryPanel;

        [Tooltip("Toggle button to show/hide the event history panel.")]
        [SerializeField] private Button historyToggle;

        [Tooltip("Content container for history list items.")]
        [SerializeField] private Transform historyContent;

        [Tooltip("Prefab for a single history list item.")]
        [SerializeField] private GameObject historyItemPrefab;

        [Tooltip("Text shown when no history exists.")]
        [SerializeField] private GameObject noHistoryLabel;

        [Header("Event Queue")]
        [Tooltip("Whether popups queue when multiple events trigger simultaneously.")]
        [SerializeField] private bool queuePopups = true;

        [Tooltip("Maximum number of popups in the queue before auto-dismissing old ones.")]
        [SerializeField] private int maxQueueSize = 10;

        // --------------------------------------------------------------------
        // COLORS (Severity)
        // --------------------------------------------------------------------

        [Header("Severity Colors")]
        [Tooltip("Color for Minor severity events.")]
        [SerializeField] private Color minorColor = new Color(0.5f, 0.8f, 0.5f, 1f);

        [Tooltip("Color for Moderate severity events.")]
        [SerializeField] private Color moderateColor = new Color(0.9f, 0.8f, 0.3f, 1f);

        [Tooltip("Color for Major severity events.")]
        [SerializeField] private Color majorColor = new Color(0.9f, 0.5f, 0.2f, 1f);

        [Tooltip("Color for Catastrophic severity events.")]
        [SerializeField] private Color catastrophicColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        // --------------------------------------------------------------------
        // PRIVATE STATE
        // --------------------------------------------------------------------

        /// <summary>Reference to the WorldEventManager for event data.</summary>
        private WorldEventManager _eventManager;

        /// <summary>Queue of events awaiting popup display.</summary>
        private readonly Queue<WorldEvent> _popupQueue = new Queue<WorldEvent>();

        /// <summary>The event currently shown in the popup, if any.</summary>
        private WorldEvent _currentPopupEvent;

        /// <summary>Active event list items currently in the panel.</summary>
        private readonly List<EventListItem> _activeListItems = new List<EventListItem>();

        /// <summary>History list items currently in the panel.</summary>
        private readonly List<HistoryListItem> _historyListItems = new List<HistoryListItem>();

        /// <summary>Whether the active events panel is currently visible.</summary>
        private bool _isActivePanelOpen;

        /// <summary>Whether the history panel is currently visible.</summary>
        private bool _isHistoryPanelOpen;

        /// <summary>Callback invoked when the player acknowledges an event popup.</summary>
        private Action<string> _onAcknowledgedCallback;

        // --------------------------------------------------------------------
        // EVENTS
        // --------------------------------------------------------------------

        /// <summary>
        /// Fired when the player acknowledges (dismisses) an event popup.
        /// Parameters: eventId.
        /// </summary>
        public event Action<string> OnEventAcknowledged;

        /// <summary>
        /// Fired when the active events panel is toggled open or closed.
        /// Parameters: isOpen.
        /// </summary>
        public event Action<bool> OnActivePanelToggled;

        /// <summary>
        /// Fired when the event history panel is toggled open or closed.
        /// Parameters: isOpen.
        /// </summary>
        public event Action<bool> OnHistoryPanelToggled;

        // --------------------------------------------------------------------
        // PROPERTIES
        // --------------------------------------------------------------------

        /// <summary>Whether any event popup is currently being displayed.</summary>
        public bool IsPopupShowing => eventPopupPanel != null && eventPopupPanel.activeSelf;

        /// <summary>The number of events waiting in the popup queue.</summary>
        public int PopupQueueCount => _popupQueue.Count;

        // --------------------------------------------------------------------
        // UNITY LIFECYCLE
        // --------------------------------------------------------------------

        /// <summary>
        /// Initializes UI components and subscribes to event system notifications.
        /// </summary>
        private void Awake()
        {
            // Ensure panels start hidden
            if (eventPopupPanel != null) eventPopupPanel.SetActive(false);
            if (popupOverlay != null) popupOverlay.SetActive(false);
            if (activeEventsPanel != null) activeEventsPanel.SetActive(false);
            if (eventHistoryPanel != null) eventHistoryPanel.SetActive(false);

            Debug.Log("[EventUI] System initialized.");
        }

        /// <summary>
        /// Finds the WorldEventManager and subscribes to event notifications.
        /// </summary>
        private void Start()
        {
            _eventManager = FindAnyObjectByType<WorldEventManager>();
            if (_eventManager == null)
            {
                Debug.LogWarning("[EventUI] WorldEventManager not found. UI will not auto-update.");
                return;
            }

            // Subscribe to event lifecycle
            _eventManager.OnEventTriggered += HandleEventTriggered;
            _eventManager.OnEventExpired += HandleEventExpired;

            // Setup button listeners
            if (popupAcknowledgeButton != null)
                popupAcknowledgeButton.onClick.AddListener(OnAcknowledgeButtonClicked);

            if (activeEventsToggle != null)
                activeEventsToggle.onClick.AddListener(ToggleActiveEventsPanel);

            if (historyToggle != null)
                historyToggle.onClick.AddListener(ToggleHistoryPanel);

            // Initial panel refresh
            RefreshActiveEventsPanel();
            RefreshHistoryPanel();
        }

        /// <summary>
        /// Unsubscribes from events when destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.OnEventTriggered -= HandleEventTriggered;
                _eventManager.OnEventExpired -= HandleEventExpired;
            }

            if (popupAcknowledgeButton != null)
                popupAcknowledgeButton.onClick.RemoveAllListeners();

            if (activeEventsToggle != null)
                activeEventsToggle.onClick.RemoveAllListeners();

            if (historyToggle != null)
                historyToggle.onClick.RemoveAllListeners();
        }

        // ====================================================================
        // EVENT POPUP
        // ====================================================================

        /// <summary>
        /// Shows a modal popup dialog displaying the details of a world event.
        /// The popup includes the event name, description, severity, effects list,
        /// and duration. The player must acknowledge to dismiss.
        /// </summary>
        /// <param name="evt">The world event to display in the popup.</param>
        public void ShowEventPopup(WorldEvent evt)
        {
            if (evt == null)
            {
                Debug.LogWarning("[EventUI] Cannot show popup for null event.");
                return;
            }

            if (queuePopups && IsPopupShowing)
            {
                // Queue the event for later display
                if (_popupQueue.Count >= maxQueueSize)
                {
                    Debug.Log("[EventUI] Popup queue full. Auto-dismissing oldest event.");
                    _popupQueue.Dequeue();
                }
                _popupQueue.Enqueue(evt);
                UpdateQueueBadge();
                return;
            }

            _currentPopupEvent = evt;
            PopulatePopupContent(evt);

            // Show popup and overlay
            if (popupOverlay != null) popupOverlay.SetActive(true);
            if (eventPopupPanel != null) eventPopupPanel.SetActive(true);

            Debug.Log($"[EventUI] Showing popup: {evt.EventName}");
        }

        /// <summary>
        /// Populates all UI fields in the popup with data from the event.
        /// </summary>
        private void PopulatePopupContent(WorldEvent evt)
        {
            // Event icon
            if (popupEventIcon != null)
            {
                popupEventIcon.sprite = evt.EventIcon;
                popupEventIcon.gameObject.SetActive(evt.EventIcon != null);
            }

            // Event name
            if (popupEventName != null)
            {
                popupEventName.text = evt.EventName;
            }

            // Event type
            if (popupEventType != null)
            {
                popupEventType.text = FormatEventType(evt.Type);
            }

            // Severity with color
            if (popupSeverity != null)
            {
                popupSeverity.text = evt.Severity.ToString().ToUpper();
                popupSeverity.color = GetSeverityColor(evt.Severity);
            }

            // Description
            if (popupDescription != null)
            {
                popupDescription.text = evt.Description;
            }

            // Effects list
            if (popupEffectsList != null)
            {
                popupEffectsList.text = FormatEffectsList(evt.Effects);
            }

            // Duration info
            if (popupDurationInfo != null)
            {
                string durationText = evt.DurationTurns <= 1
                    ? "Instant effect"
                    : $"{evt.DurationTurns} turns";
                popupDurationInfo.text = $"Duration: {durationText}";
            }

            // Target info
            if (popupTargetInfo != null)
            {
                string targetText = evt.IsGlobal
                    ? "GLOBAL - All nations affected"
                    : $"Target: {evt.AffectedNationId ?? evt.AffectedRegionId ?? "Unknown"}";
                popupTargetInfo.text = targetText;
            }
        }

        /// <summary>
        /// Handles the player clicking the acknowledge/dismiss button on the popup.
        /// </summary>
        private void OnAcknowledgeButtonClicked()
        {
            if (_currentPopupEvent != null)
            {
                OnEventAcknowledged?.Invoke(_currentPopupEvent.EventId);
                Debug.Log($"[EventUI] Event acknowledged: {_currentPopupEvent.EventId}");
                _currentPopupEvent = null;
            }

            // Hide popup
            if (eventPopupPanel != null) eventPopupPanel.SetActive(false);
            if (popupOverlay != null) popupOverlay.SetActive(false);

            // Show next queued popup if available
            ProcessNextQueuedPopup();
        }

        /// <summary>
        /// Called externally to acknowledge a specific event by ID.
        /// If the event is currently shown in the popup, dismisses it.
        /// </summary>
        /// <param name="eventId">The ID of the event to acknowledge.</param>
        public void OnEventAcknowledged(string eventId)
        {
            if (_currentPopupEvent != null && _currentPopupEvent.EventId == eventId)
            {
                OnAcknowledgeButtonClicked();
            }
            else
            {
                // Remove from queue if queued
                Queue<WorldEvent> tempQueue = new Queue<WorldEvent>();
                while (_popupQueue.Count > 0)
                {
                    WorldEvent queued = _popupQueue.Dequeue();
                    if (queued.EventId != eventId)
                    {
                        tempQueue.Enqueue(queued);
                    }
                }
                _popupQueue = tempQueue;
                UpdateQueueBadge();

                OnEventAcknowledged?.Invoke(eventId);
            }
        }

        // ====================================================================
        // EVENT HISTORY
        // ====================================================================

        /// <summary>
        /// Shows the event history panel populated with all past events.
        /// Events are displayed in reverse chronological order (newest first).
        /// </summary>
        /// <param name="pastEvents">The list of expired world events to display.</param>
        public void ShowEventHistory(List<WorldEvent> pastEvents)
        {
            if (eventHistoryPanel == null)
            {
                Debug.LogWarning("[EventUI] Event history panel not assigned in Inspector.");
                return;
            }

            ClearHistoryItems();

            if (pastEvents == null || pastEvents.Count == 0)
            {
                if (noHistoryLabel != null) noHistoryLabel.SetActive(true);
                return;
            }

            if (noHistoryLabel != null) noHistoryLabel.SetActive(false);

            // Sort newest first
            var sorted = pastEvents.OrderByDescending(e => e.TriggeredOnTurn).ToList();

            foreach (WorldEvent evt in sorted)
            {
                CreateHistoryItem(evt);
            }

            eventHistoryPanel.SetActive(true);
            _isHistoryPanelOpen = true;
            OnHistoryPanelToggled?.Invoke(true);

            Debug.Log($"[EventUI] History panel opened with {sorted.Count} entries.");
        }

        /// <summary>
        /// Refreshes the history panel with current data from the event manager.
        /// </summary>
        public void RefreshHistoryPanel()
        {
            if (_eventManager == null) return;
            ShowEventHistory(_eventManager.EventHistory.ToList());
        }

        /// <summary>
        /// Creates a single history list item UI element for an expired event.
        /// </summary>
        private void CreateHistoryItem(WorldEvent evt)
        {
            if (historyContent == null || historyItemPrefab == null)
                return;

            GameObject itemObj = Instantiate(historyItemPrefab, historyContent);
            HistoryListItem item = new HistoryListItem
            {
                Event = evt,
                ListItem = itemObj,
                IsVisible = true
            };

            // Populate child text components
            PopulateEventListItem(itemObj, evt, isHistory: true);

            _historyListItems.Add(item);
        }

        /// <summary>
        /// Clears all history list items from the panel.
        /// </summary>
        private void ClearHistoryItems()
        {
            foreach (HistoryListItem item in _historyListItems)
            {
                if (item.ListItem != null)
                    Destroy(item.ListItem);
            }
            _historyListItems.Clear();

            if (historyContent != null)
            {
                foreach (Transform child in historyContent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // ====================================================================
        // ACTIVE EVENTS PANEL
        // ====================================================================

        /// <summary>
        /// Shows the active events panel with all currently running world events.
        /// Each event displays its name, severity, and remaining turns.
        /// </summary>
        /// <param name="activeEvents">The list of currently active world events.</param>
        public void ShowActiveEventsPanel(List<WorldEvent> activeEvents)
        {
            if (activeEventsPanel == null)
            {
                Debug.LogWarning("[EventUI] Active events panel not assigned in Inspector.");
                return;
            }

            ClearActiveListItems();

            if (activeEvents == null || activeEvents.Count == 0)
            {
                if (noActiveEventsLabel != null) noActiveEventsLabel.SetActive(true);
                UpdateActiveCountBadge(0);
                return;
            }

            if (noActiveEventsLabel != null) noActiveEventsLabel.SetActive(false);

            foreach (WorldEvent evt in activeEvents)
            {
                CreateActiveEventItem(evt);
            }

            UpdateActiveCountBadge(activeEvents.Count);
            activeEventsPanel.SetActive(true);
            _isActivePanelOpen = true;
            OnActivePanelToggled?.Invoke(true);

            Debug.Log($"[EventUI] Active events panel opened with {activeEvents.Count} events.");
        }

        /// <summary>
        /// Refreshes the active events panel with current data from the event manager.
        /// </summary>
        public void RefreshActiveEventsPanel()
        {
            if (_eventManager == null) return;
            ShowActiveEventsPanel(_eventManager.GetActiveEvents());
        }

        /// <summary>
        /// Creates a single active event list item UI element.
        /// </summary>
        private void CreateActiveEventItem(WorldEvent evt)
        {
            if (activeEventsContent == null || activeEventItemPrefab == null)
                return;

            GameObject itemObj = Instantiate(activeEventItemPrefab, activeEventsContent);
            EventListItem item = new EventListItem
            {
                Event = evt,
                ListItem = itemObj,
                IsVisible = true
            };

            // Populate child text components
            PopulateEventListItem(itemObj, evt, isHistory: false);

            _activeListItems.Add(item);
        }

        /// <summary>
        /// Clears all active event list items from the panel.
        /// </summary>
        private void ClearActiveListItems()
        {
            foreach (EventListItem item in _activeListItems)
            {
                if (item.ListItem != null)
                    Destroy(item.ListItem);
            }
            _activeListItems.Clear();

            if (activeEventsContent != null)
            {
                foreach (Transform child in activeEventsContent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Updates the content of an existing active event list item to reflect
        /// the current state (e.g., turns remaining has decreased).
        /// </summary>
        /// <param name="item">The list item to update.</param>
        public void UpdateActiveListItem(EventListItem item)
        {
            if (item == null || item.ListItem == null || item.Event == null)
                return;

            PopulateEventListItem(item.ListItem, item.Event, isHistory: false);
        }

        /// <summary>
        /// Updates all active list items (e.g., after a turn tick).
        /// </summary>
        public void RefreshActiveListItems()
        {
            foreach (EventListItem item in _activeListItems)
            {
                UpdateActiveListItem(item);
            }

            if (_eventManager != null)
            {
                UpdateActiveCountBadge(_eventManager.GetActiveEvents().Count);
            }
        }

        // ====================================================================
        // PANEL TOGGLING
        // ====================================================================

        /// <summary>
        /// Toggles the active events panel open/closed.
        /// </summary>
        public void ToggleActiveEventsPanel()
        {
            _isActivePanelOpen = !_isActivePanelOpen;

            if (activeEventsPanel != null)
            {
                if (_isActivePanelOpen)
                {
                    RefreshActiveEventsPanel();
                }
                else
                {
                    activeEventsPanel.SetActive(false);
                }
            }

            OnActivePanelToggled?.Invoke(_isActivePanelOpen);
        }

        /// <summary>
        /// Toggles the event history panel open/closed.
        /// </summary>
        public void ToggleHistoryPanel()
        {
            _isHistoryPanelOpen = !_isHistoryPanelOpen;

            if (eventHistoryPanel != null)
            {
                if (_isHistoryPanelOpen)
                {
                    RefreshHistoryPanel();
                }
                else
                {
                    eventHistoryPanel.SetActive(false);
                }
            }

            OnHistoryPanelToggled?.Invoke(_isHistoryPanelOpen);
        }

        /// <summary>
        /// Forces all panels closed. Useful for scene transitions or menu screens.
        /// </summary>
        public void CloseAllPanels()
        {
            if (eventPopupPanel != null) eventPopupPanel.SetActive(false);
            if (popupOverlay != null) popupOverlay.SetActive(false);
            if (activeEventsPanel != null) activeEventsPanel.SetActive(false);
            if (eventHistoryPanel != null) eventHistoryPanel.SetActive(false);

            _isActivePanelOpen = false;
            _isHistoryPanelOpen = false;
            _currentPopupEvent = null;
            _popupQueue.Clear();
            UpdateQueueBadge();

            OnActivePanelToggled?.Invoke(false);
            OnHistoryPanelToggled?.Invoke(false);
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        /// <summary>
        /// Handles the WorldEventManager's OnEventTriggered event.
        /// Shows a popup and refreshes the active events panel.
        /// </summary>
        private void HandleEventTriggered(WorldEvent evt)
        {
            ShowEventPopup(evt);
            RefreshActiveEventsPanel();
        }

        /// <summary>
        /// Handles the WorldEventManager's OnEventExpired event.
        /// Refreshes both panels when an event expires.
        /// </summary>
        private void HandleEventExpired(WorldEvent evt)
        {
            // Refresh both panels on next frame (to allow the event manager to finish its update)
            StartCoroutine(DeferredRefresh());

            Debug.Log($"[EventUI] Event expired: {evt.EventName}. Panels will refresh.");
        }

        /// <summary>
        /// Deferred refresh coroutine to avoid modifying UI during event manager's update cycle.
        /// </summary>
        private System.Collections.IEnumerator DeferredRefresh()
        {
            yield return null; // Wait one frame
            RefreshActiveEventsPanel();
            RefreshHistoryPanel();
        }

        // ====================================================================
        // POPUP QUEUE MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Processes the next event in the popup queue.
        /// </summary>
        private void ProcessNextQueuedPopup()
        {
            if (_popupQueue.Count > 0)
            {
                WorldEvent next = _popupQueue.Dequeue();
                UpdateQueueBadge();
                ShowEventPopup(next);
            }
        }

        /// <summary>
        /// Updates the queue count badge on the toggle button.
        /// </summary>
        private void UpdateQueueBadge()
        {
            // Update a queue badge if one exists
            // This would be implemented with an additional TextMeshProUGUI reference
            // if the design requires showing queued event count
        }

        // ====================================================================
        // UI HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Populates a list item's child UI components with event data.
        /// Uses naming conventions to find TextMeshProUGUI components by name.
        /// 
        /// Expected child names in prefab:
        /// <list type="bullet">
        ///   <item><c>"Icon"</c> - Image component for event type icon.</item>
        ///   <item><c>"NameText"</c> - Event name.</item>
        ///   <item><c>"TypeText"</c> - Event type/category.</item>
        ///   <item><c>"SeverityText"</c> - Severity badge.</item>
        ///   <item><c>"DetailText"</c> - Detail info (turns remaining or turn triggered).</item>
        ///   <item><c>"TargetText"</c> - Affected nation/region.</item>
        ///   <item><c>"Background"</c> - Image for severity-colored background.</item>
        /// </list>
        /// </summary>
        /// <param name="itemObj">The root GameObject of the list item.</param>
        /// <param name="evt">The event data to display.</param>
        /// <param name="isHistory">Whether this is a history item (vs active item).</param>
        private void PopulateEventListItem(GameObject itemObj, WorldEvent evt, bool isHistory)
        {
            // Find and populate name
            TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = evt.EventName;
            }

            // Find and populate type
            TextMeshProUGUI typeText = itemObj.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = FormatEventType(evt.Type);
            }

            // Find and populate severity with color
            TextMeshProUGUI severityText = itemObj.transform.Find("SeverityText")?.GetComponent<TextMeshProUGUI>();
            if (severityText != null)
            {
                severityText.text = evt.Severity.ToString().ToUpper();
                severityText.color = GetSeverityColor(evt.Severity);
            }

            // Find and populate detail text
            TextMeshProUGUI detailText = itemObj.transform.Find("DetailText")?.GetComponent<TextMeshProUGUI>();
            if (detailText != null)
            {
                if (isHistory)
                {
                    detailText.text = $"Turn {evt.TriggeredOnTurn} | Duration: {evt.DurationTurns} turns";
                }
                else
                {
                    string remaining = evt.TurnsRemaining == 1
                        ? "1 turn remaining"
                        : $"{evt.TurnsRemaining} turns remaining";
                    detailText.text = remaining;
                }
            }

            // Find and populate target
            TextMeshProUGUI targetText = itemObj.transform.Find("TargetText")?.GetComponent<TextMeshProUGUI>();
            if (targetText != null)
            {
                string target = evt.IsGlobal
                    ? "GLOBAL"
                    : evt.AffectedNationId ?? evt.AffectedRegionId ?? "Unknown";
                targetText.text = target;
            }

            // Apply severity color to background
            Image background = itemObj.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                Color bgColor = GetSeverityColor(evt.Severity);
                bgColor.a = 0.15f; // Semi-transparent background
                background.color = bgColor;
            }

            // Set icon
            Image icon = itemObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = evt.EventIcon;
                icon.gameObject.SetActive(evt.EventIcon != null);
            }
        }

        /// <summary>
        /// Updates the active events count badge on the toggle button.
        /// </summary>
        /// <param name="count">The number of active events.</param>
        private void UpdateActiveCountBadge(int count)
        {
            if (activeEventsCountBadge != null)
            {
                activeEventsCountBadge.text = count > 0 ? count.ToString() : string.Empty;
                activeEventsCountBadge.gameObject.SetActive(count > 0);
            }
        }

        /// <summary>
        /// Formats a WorldEventType into a human-readable string.
        /// </summary>
        /// <param name="type">The event type to format.</param>
        /// <returns>A formatted type string.</returns>
        private string FormatEventType(WorldEventType type)
        {
            switch (type)
            {
                case WorldEventType.NaturalDisaster:      return "Natural Disaster";
                case WorldEventType.Revolution:           return "Revolution";
                case WorldEventType.Pandemic:             return "Pandemic";
                case WorldEventType.EconomicCrisis:       return "Economic Crisis";
                case WorldEventType.ResourceDiscovery:    return "Discovery";
                case WorldEventType.TechBreakthrough:     return "Tech Breakthrough";
                case WorldEventType.RefugeeCrisis:        return "Refugee Crisis";
                case WorldEventType.TradeDispute:         return "Trade Dispute";
                case WorldEventType.Assassination:        return "Assassination";
                case WorldEventType.EnvironmentalDisaster: return "Environmental";
                default:                                  return type.ToString();
            }
        }

        /// <summary>
        /// Formats a list of event effects into a bulleted text string for display.
        /// </summary>
        /// <param name="effects">The list of effects to format.</param>
        /// <returns>A formatted multi-line string with bullet points.</returns>
        private string FormatEffectsList(List<EventEffect> effects)
        {
            if (effects == null || effects.Count == 0)
                return "No specific effects.";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < effects.Count; i++)
            {
                EventEffect effect = effects[i];
                string sign = effect.Value >= 0 ? "+" : "";
                string valueStr = effect.EffectType.Contains("change") || effect.EffectType.Contains("morale")
                    ? $"{sign}{effect.Value:F0}"
                    : $"{sign}{effect.Value:P0}";

                sb.AppendLine($"  \u2022 {effect.Description} ({valueStr})");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets the display color associated with a disaster severity level.
        /// </summary>
        /// <param name="severity">The severity level.</param>
        /// <returns>The associated color.</returns>
        private Color GetSeverityColor(DisasterSeverity severity)
        {
            switch (severity)
            {
                case DisasterSeverity.Minor:       return minorColor;
                case DisasterSeverity.Moderate:    return moderateColor;
                case DisasterSeverity.Major:       return majorColor;
                case DisasterSeverity.Catastrophic: return catastrophicColor;
                default:                           return Color.white;
            }
        }
    }
}
