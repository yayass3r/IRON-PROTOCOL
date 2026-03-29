// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: UIManager.cs
// Namespace: IronProtocol.UI
// Description: Singleton MonoBehaviour managing all UI screens, notifications,
//              and HUD updates. Persists across scene loads via DontDestroyOnLoad.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IronProtocol.UI
{
    /// <summary>
    /// Enumeration of all available game screens.
    /// Each value maps to a canvas panel in the <see cref="UIManager.screenPanels"/> dictionary.
    /// </summary>
    public enum GameScreen
    {
        MainMenu,
        GlobalMap,
        BattleView,
        MarketScreen,
        DiplomacyScreen,
        ResearchScreen,
        ProductionScreen,
        Settings
    }

    /// <summary>
    /// Serializable wrapper for pairing a <see cref="GameScreen"/> key with a
    /// <see cref="GameObject"/> panel reference in the Inspector.
    /// </summary>
    [Serializable]
    public class ScreenPanelPair
    {
        [Tooltip("The game screen this panel represents.")]
        public GameScreen screen;

        [Tooltip("The root GameObject of this screen's UI panel.")]
        public GameObject panel;
    }

    /// <summary>
    /// Singleton MonoBehaviour that orchestrates all UI screens, floating toast
    /// notifications, combat report popups, and HUD refresh calls.
    /// Survives scene transitions via <see cref="Object.DontDestroyOnLoad"/>.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------

        private static UIManager _instance;

        /// <summary>
        /// Global singleton instance of the UIManager.
        /// Created automatically on first access; destroyed on application quit.
        /// </summary>
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UIManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[UIManager]");
                        _instance = go.AddComponent<UIManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------
        // Inspector Fields
        // ------------------------------------------------------------------

        [Header("Screen Panels")]
        [Tooltip("List of screen-to-panel mappings assigned in the Inspector.")]
        [SerializeField] private List<ScreenPanelPair> screenPanelList = new List<ScreenPanelPair>();

        [Header("Notification")]
        [Tooltip("Prefab instantiated for floating toast notifications.")]
        [SerializeField] private GameObject notificationPrefab;

        [Tooltip("Canvas transform that hosts notification instances.")]
        [SerializeField] private Transform notificationContainer;

        [Header("Combat Report Popup")]
        [Tooltip("Prefab instantiated to display combat result details.")]
        [SerializeField] private GameObject combatReportPrefab;

        [Header("HUD Reference")]
        [Tooltip("Reference to the HUD controller (usually on the same canvas).")]
        [SerializeField] private HUDController hudController;

        // ------------------------------------------------------------------
        // Runtime State
        // ------------------------------------------------------------------

        /// <summary>
        /// The currently active game screen.
        /// </summary>
        public GameScreen CurrentScreen { get; private set; } = GameScreen.MainMenu;

        /// <summary>
        /// Maps each <see cref="GameScreen"/> to its corresponding panel <see cref="GameObject"/>.
        /// Built from <see cref="screenPanelList"/> on Awake.
        /// </summary>
        private Dictionary<GameScreen, GameObject> screenPanels = new Dictionary<GameScreen, GameObject>();

        /// <summary>
        /// Active notification coroutines keyed by a unique id for cancellation support.
        /// </summary>
        private readonly Dictionary<int, Coroutine> _activeNotifications = new Dictionary<int, Coroutine>();

        private int _notificationIdCounter;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired whenever the active screen changes. Parameters: (oldScreen, newScreen).</summary>
        public event Action<GameScreen, GameScreen> OnScreenChanged;

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Enforce singleton pattern
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            BuildScreenDictionary();
        }

        // ------------------------------------------------------------------
        // Public API - Screen Management
        // ------------------------------------------------------------------

        /// <summary>
        /// Activates the panel for <paramref name="screen"/> and deactivates all others.
        /// Fires <see cref="OnScreenChanged"/> if the screen actually changes.
        /// </summary>
        /// <param name="screen">The screen to display.</param>
        public void ShowScreen(GameScreen screen)
        {
            if (screen == CurrentScreen)
                return;

            GameScreen previousScreen = CurrentScreen;

            foreach (var kvp in screenPanels)
            {
                bool isActive = (kvp.Key == screen);
                kvp.Value.SetActive(isActive);
            }

            CurrentScreen = screen;
            Debug.Log($"[UIManager] Screen changed: {previousScreen} → {screen}");

            OnScreenChanged?.Invoke(previousScreen, screen);
        }

        // ------------------------------------------------------------------
        // Public API - Notifications
        // ------------------------------------------------------------------

        /// <summary>
        /// Displays a floating toast notification that auto-dismisses after
        /// <paramref name="duration"/> seconds.
        /// </summary>
        /// <param name="message">The text to display in the notification.</param>
        /// <param name="duration">How long (in seconds) the notification remains visible. Default 3 s.</param>
        /// <returns>A unique identifier that can be used to dismiss the notification early.</returns>
        public int ShowNotification(string message, float duration = 3f)
        {
            if (notificationPrefab == null || notificationContainer == null)
            {
                Debug.LogWarning("[UIManager] Notification prefab or container not assigned.");
                return -1;
            }

            int id = ++_notificationIdCounter;
            Coroutine routine = StartCoroutine(NotificationRoutine(id, message, duration));
            _activeNotifications[id] = routine;

            return id;
        }

        /// <summary>
        /// Immediately dismisses a previously shown notification by its id.
        /// </summary>
        /// <param name="notificationId">The id returned by <see cref="ShowNotification"/>.</param>
        public void DismissNotification(int notificationId)
        {
            if (_activeNotifications.TryGetValue(notificationId, out Coroutine routine))
            {
                StopCoroutine(routine);
                _activeNotifications.Remove(notificationId);
            }
        }

        // ------------------------------------------------------------------
        // Public API - Combat Report
        // ------------------------------------------------------------------

        /// <summary>
        /// Displays a popup containing detailed combat results.
        /// Delegates to the <see cref="CombatReportView"/> component on the prefab.
        /// </summary>
        /// <param name="result">The combat result data to present.</param>
        public void ShowCombatReport(IronProtocol.Military.CombatResult result)
        {
            if (combatReportPrefab == null)
            {
                Debug.LogWarning("[UIManager] Combat report prefab not assigned.");
                return;
            }

            Transform parent = notificationContainer != null ? notificationContainer : transform;
            GameObject popup = Instantiate(combatReportPrefab, parent);

            CombatReportView reportView = popup.GetComponent<CombatReportView>();
            if (reportView != null)
            {
                reportView.Populate(result);
            }
            else
            {
                Debug.LogWarning("[UIManager] CombatReportView component not found on combat report prefab.");
            }
        }

        // ------------------------------------------------------------------
        // Public API - HUD
        // ------------------------------------------------------------------

        /// <summary>
        /// Refreshes the top-bar HUD (turn number, treasury, resource overview).
        /// Delegates to the <see cref="HUDController"/> reference when available.
        /// </summary>
        public void UpdateHUD()
        {
            if (hudController != null)
            {
                hudController.Refresh();
            }
        }

        /// <summary>
        /// Provides external access to the HUD controller for direct field updates.
        /// </summary>
        public HUDController HUD => hudController;

        // ------------------------------------------------------------------
        // Private Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the internal <see cref="screenPanels"/> dictionary from the
        /// serialized <see cref="screenPanelList"/>.
        /// Logs a warning for any duplicate or missing entries.
        /// </summary>
        private void BuildScreenDictionary()
        {
            screenPanels.Clear();

            foreach (var pair in screenPanelList)
            {
                if (pair.panel == null)
                {
                    Debug.LogWarning($"[UIManager] Panel GameObject is null for screen '{pair.screen}'.");
                    continue;
                }

                if (screenPanels.ContainsKey(pair.screen))
                {
                    Debug.LogWarning($"[UIManager] Duplicate screen entry: '{pair.screen}'. Overwriting.");
                }

                screenPanels[pair.screen] = pair.panel;
            }

            // Validate that all enum values are covered
            foreach (GameScreen screen in Enum.GetValues(typeof(GameScreen)))
            {
                if (!screenPanels.ContainsKey(screen))
                {
                    Debug.LogWarning($"[UIManager] No panel assigned for screen '{screen}'.");
                }
            }
        }

        /// <summary>
        /// Coroutine that spawns a notification, fades it in, holds for
        /// <paramref name="duration"/>, fades out, then destroys the instance.
        /// </summary>
        private IEnumerator NotificationRoutine(int id, string message, float duration)
        {
            GameObject notifObj = Instantiate(notificationPrefab, notificationContainer);
            notifObj.SetActive(true);

            // Attempt to set the message text
            Text textComponent = notifObj.GetComponentInChildren<Text>();
            if (textComponent != null)
            {
                textComponent.text = message;
            }

            CanvasGroup canvasGroup = notifObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = notifObj.AddComponent<CanvasGroup>();
            }

            // Fade-in
            float elapsed = 0f;
            float fadeDuration = 0.3f;
            while (elapsed < fadeDuration)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(duration);

            // Fade-out
            elapsed = 0f;
            fadeDuration = 0.5f;
            while (elapsed < fadeDuration)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            canvasGroup.alpha = 0f;

            Destroy(notifObj);
            _activeNotifications.Remove(id);
        }

        // ------------------------------------------------------------------
        // Editor Utility
        // ------------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Auto-populates <see cref="screenPanelList"/> with all enum values.
        /// Intended for use from a custom editor script.
        /// </summary>
        public void AutoPopulateScreenList()
        {
            screenPanelList.Clear();
            foreach (GameScreen screen in Enum.GetValues(typeof(GameScreen)))
            {
                screenPanelList.Add(new ScreenPanelPair { screen = screen, panel = null });
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    // ========================================================================
    // CombatReportView – lightweight component expected on the combat report
    // prefab. Populates UI text fields from a CombatResult.
    // ========================================================================

    /// <summary>
    /// Component that lives on the combat report popup prefab and displays
    /// detailed results from a <see cref="IronProtocol.Military.CombatResult"/>.
    /// </summary>
    public class CombatReportView : MonoBehaviour
    {
        [Header("Result Text Fields")]
        [SerializeField] private Text attackerLabel;
        [SerializeField] private Text defenderLabel;
        [SerializeField] private Text resultText;
        [SerializeField] private Text damageText;
        [SerializeField] private Text casualtiesText;
        [SerializeField] private Text moraleText;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnClose);
        }

        private void OnDisable()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnClose);
        }

        /// <summary>
        /// Populates all text fields from the given <paramref name="result"/>.
        /// </summary>
        public void Populate(IronProtocol.Military.CombatResult result)
        {
            if (result == null)
            {
                Debug.LogWarning("[CombatReportView] Null CombatResult passed to Populate.");
                return;
            }

            if (attackerLabel != null)  attackerLabel.text  = $"Attacker: {result.AttackerName}";
            if (defenderLabel != null)  defenderLabel.text  = $"Defender: {result.DefenderName}";
            if (resultText != null)     resultText.text     = $"Outcome: {result.Outcome}";
            if (damageText != null)     damageText.text     = $"Damage Dealt: {result.DamageDealt:F1}";
            if (casualtiesText != null) casualtiesText.text = $"Casualties: {result.AttackerLosses} / {result.DefenderLosses}";
            if (moraleText != null)     moraleText.text     = $"Morale Impact: {result.MoraleChange:F1}";
        }

        private void OnClose()
        {
            Destroy(gameObject);
        }
    }
}
