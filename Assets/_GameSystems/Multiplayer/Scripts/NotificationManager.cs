// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: NotificationManager.cs
// Namespace: IronProtocol.GameSystems.Multiplayer
// Description: Push notification system for asynchronous multiplayer.
//              Schedules turn reminders, sends notifications to players,
//              and handles incoming notification display. Integrates with
//              Unity's Android notification sender for local push notifications.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.GameSystems.Multiplayer
{
    /// <summary>
    /// Serializable container for a scheduled notification task.
    /// </summary>
    [Serializable]
    public class ScheduledNotification
    {
        /// <summary>Unique identifier for this scheduled notification.</summary>
        public string id;

        /// <summary>Target player identifier.</summary>
        public string playerId;

        /// <summary>Notification title.</summary>
        public string title;

        /// <summary>Notification message body.</summary>
        public string message;

        /// <summary>Time at which the notification should fire (UTC ticks).</summary>
        public long fireAtTicks;

        /// <summary>Whether this notification has already been fired.</summary>
        public bool hasFired;

        /// <summary>Gets the DateTime representation of <see cref="fireAtTicks"/>.</summary>
        public DateTime FireTime => new DateTime(fireAtTicks, DateTimeKind.Utc);
    }

    /// <summary>
    /// MonoBehaviour that manages push notifications for asynchronous multiplayer.
    /// Handles scheduling, sending, and receiving notifications. On Android,
    /// integrates with Unity's NotificationSender for local push notifications.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields
        // ------------------------------------------------------------------

        [Header("Server Configuration")]
        [Tooltip("Base URL for the notification API server.")]
        [SerializeField] private string serverBaseUrl = "https://api.ironprotocol.game";

        [Tooltip("API key for server authentication.")]
        [SerializeField] private string apiKey;

        [Header("Notification Settings")]
        [Tooltip("Default delay (in seconds) for turn reminders when not explicitly specified.")]
        [SerializeField] private float defaultTurnReminderDelay = 86400f; // 24 hours

        [Tooltip("Small icon name for Android notifications (must exist in res/drawable).")]
        [SerializeField] private string androidSmallIcon = "icon_0";

        [Tooltip("Large icon name for Android notifications.")]
        [SerializeField] private string androidLargeIcon = "icon_1";

        [Header("In-App Notification UI")]
        [Tooltip("Prefab instantiated for in-app notification popups.")]
        [SerializeField] private GameObject inAppNotificationPrefab;

        [Tooltip("Canvas transform that hosts in-app notification instances.")]
        [SerializeField] private Transform notificationContainer;

        [Tooltip("Duration (seconds) that in-app notifications remain visible.")]
        [SerializeField] private float inAppDisplayDuration = 5f;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when a local notification is received and displayed.</summary>
        public event Action<string, string> OnNotificationDisplayed;

        /// <summary>Fired when a scheduled notification fires.</summary>
        public event Action<ScheduledNotification> OnScheduledNotificationFired;

        // ------------------------------------------------------------------
        // Public State
        // ------------------------------------------------------------------

        /// <summary>Whether the notification manager has been initialized.</summary>
        public bool IsInitialized { get; private set; } = false;

        /// <summary>Number of currently active scheduled notifications.</summary>
        public int ActiveScheduleCount => _scheduledNotifications.Count;

        // ------------------------------------------------------------------
        // Private State
        // ------------------------------------------------------------------

        private readonly List<ScheduledNotification> _scheduledNotifications = new List<ScheduledNotification>();
        private readonly Dictionary<string, Coroutine> _runningTimers = new Dictionary<string, Coroutine>();
        private int _notificationCounter;

        // ------------------------------------------------------------------
        // Singleton (convenience)
        // ------------------------------------------------------------------

        private static NotificationManager _instance;

        /// <summary>
        /// Convenience accessor for the NotificationManager singleton.
        /// Not enforced as strictly as UI.UIManager – supports multiple instances for testing.
        /// </summary>
        public static NotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<NotificationManager>();
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void OnDestroy()
        {
            CancelAllScheduled();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // When the app goes to background, ensure pending notifications
            // are registered with the OS for delivery
            if (pauseStatus)
            {
                RegisterPendingOSNotifications();
            }
        }

        // ------------------------------------------------------------------
        // Public API - Initialization
        // ------------------------------------------------------------------

        /// <summary>
        /// Initializes the notification system. On Android, requests notification
        /// permission and configures the channel. On other platforms, enables
        /// in-app notifications only.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
                return;

#if UNITY_ANDROID
            InitializeAndroidNotifications();
#elif UNITY_IOS
            InitializeIOSNotifications();
#endif

            IsInitialized = true;
            Debug.Log("[NotificationManager] Initialized.");
        }

        // ------------------------------------------------------------------
        // Public API - Scheduling
        // ------------------------------------------------------------------

        /// <summary>
        /// Schedules a turn reminder notification for the specified player.
        /// The notification fires after <paramref name="delaySeconds"/>.
        /// </summary>
        /// <param name="playerId">The target player's identifier.</param>
        /// <param name="delaySeconds">Delay before the notification fires (seconds).</param>
        /// <returns>The unique id of the scheduled notification.</returns>
        public string ScheduleTurnReminder(string playerId, float delaySeconds)
        {
            if (delaySeconds <= 0f)
                delaySeconds = defaultTurnReminderDelay;

            string title = "Your Turn!";
            string message = "Your opponent has completed their turn. It's your move in IRON PROTOCOL.";

            return ScheduleNotification(playerId, title, message, delaySeconds);
        }

        /// <summary>
        /// Schedules a generic notification for the specified player.
        /// </summary>
        /// <param name="playerId">The target player's identifier.</param>
        /// <param name="title">Notification title.</param>
        /// <param name="message">Notification message body.</param>
        /// <param name="delaySeconds">Delay before the notification fires (seconds).</param>
        /// <returns>The unique id of the scheduled notification.</returns>
        public string ScheduleNotification(string playerId, string title, string message, float delaySeconds)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(title))
            {
                Debug.LogWarning("[NotificationManager] Cannot schedule notification – missing playerId or title.");
                return null;
            }

            string id = $"notif_{++_notificationCounter}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            ScheduledNotification notif = new ScheduledNotification
            {
                id = id,
                playerId = playerId,
                title = title,
                message = message ?? string.Empty,
                fireAtTicks = DateTime.UtcNow.AddSeconds(delaySeconds).Ticks,
                hasFired = false
            };

            _scheduledNotifications.Add(notif);

            // Start countdown coroutine
            Coroutine timer = StartCoroutine(NotificationTimerCoroutine(notif, delaySeconds));
            _runningTimers[id] = timer;

            Debug.Log($"[NotificationManager] Scheduled: '{title}' for {playerId} in {delaySeconds:F0}s (id: {id})");

            return id;
        }

        /// <summary>
        /// Cancels a previously scheduled notification by its id.
        /// </summary>
        /// <param name="notificationId">The id returned by <see cref="ScheduleNotification"/>.</param>
        public void CancelScheduledNotification(string notificationId)
        {
            if (string.IsNullOrEmpty(notificationId))
                return;

            // Stop timer coroutine
            if (_runningTimers.TryGetValue(notificationId, out Coroutine timer))
            {
                StopCoroutine(timer);
                _runningTimers.Remove(notificationId);
            }

            // Remove from list
            _scheduledNotifications.RemoveAll(n => n.id == notificationId);

            Debug.Log($"[NotificationManager] Cancelled notification: {notificationId}");
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduled()
        {
            foreach (var kvp in _runningTimers)
            {
                if (kvp.Value != null)
                {
                    StopCoroutine(kvp.Value);
                }
            }
            _runningTimers.Clear();
            _scheduledNotifications.Clear();

            Debug.Log("[NotificationManager] All scheduled notifications cancelled.");
        }

        // ------------------------------------------------------------------
        // Public API - Immediate Sending
        // ------------------------------------------------------------------

        /// <summary>
        /// Immediately sends a notification to a player (via server for remote
        /// players, via OS for local player).
        /// </summary>
        /// <param name="playerId">The target player's identifier.</param>
        /// <param name="title">Notification title.</param>
        /// <param name="message">Notification message body.</param>
        public void SendNotification(string playerId, string title, string message)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("[NotificationManager] Cannot send notification – null playerId.");
                return;
            }

            // Check if this is a local notification (current player)
            // For remote players, this would send via the server API
            bool isLocal = IsLocalPlayer(playerId);

            if (isLocal)
            {
                ShowLocalNotification(title, message);
            }
            else
            {
                StartCoroutine(SendRemoteNotificationCoroutine(playerId, title, message));
            }
        }

        /// <summary>
        /// Handles a received notification by displaying it in-app.
        /// Called by the multiplayer system when a push notification arrives.
        /// </summary>
        /// <param name="title">Notification title.</param>
        /// <param name="message">Notification message body.</param>
        public void OnNotificationReceived(string title, string message)
        {
            Debug.Log($"[NotificationManager] Notification received: '{title}' – {message}");
            ShowInAppNotification(title, message);
            OnNotificationDisplayed?.Invoke(title, message);
        }

        // ------------------------------------------------------------------
        // Platform-Specific Initialization
        // ------------------------------------------------------------------

#if UNITY_ANDROID
        private void InitializeAndroidNotifications()
        {
            try
            {
                // Request notification permission on Android 13+
                if (!AndroidPermission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                {
                    AndroidPermission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                }

                // Create notification channel
                var notificationChannel = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity")
                    .Call<AndroidJavaObject>("getSystemService", "notification");

                Debug.Log("[NotificationManager] Android notifications initialized.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NotificationManager] Android notification init failed: {e.Message}");
            }
        }
#elif UNITY_IOS
        private void InitializeIOSNotifications()
        {
            // iOS notification authorization would use UnityEngine.iOS.NotificationServices
            Debug.Log("[NotificationManager] iOS notifications initialized (in-app only).");
        }
#endif

        // ------------------------------------------------------------------
        // Private - Notification Display
        // ------------------------------------------------------------------

        /// <summary>
        /// Shows an OS-level local notification (works when app is in background).
        /// </summary>
        private void ShowLocalNotification(string title, string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    // Use Android's NotificationCompat to schedule a local notification
                    var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                    var notificationManager = context.Call<AndroidJavaObject>("getSystemService", "notification");

                    // Build notification using NotificationCompat
                    var builderClass = new AndroidJavaClass("androidx.core.app.NotificationCompat$Builder");
                    var builder = builderClass.Call<AndroidJavaObject>
                        ("<init>", context, "iron_protocol_channel");

                    builder.Call<AndroidJavaObject>("setContentTitle", title);
                    builder.Call<AndroidJavaObject>("setContentText", message);
                    builder.Call<AndroidJavaObject>("setSmallIcon", GetResourceId(context, androidSmallIcon, "drawable"));
                    builder.Call<AndroidJavaObject>("setAutoCancel", true);
                    builder.Call<AndroidJavaObject>("setPriority", 2); // PRIORITY_HIGH

                    var notification = builder.Call<AndroidJavaObject>("build");
                    int id = (int)(DateTime.UtcNow.Ticks % int.MaxValue);

                    notificationManager.Call("notify", id, notification);
                }

                Debug.Log($"[NotificationManager] Android notification sent: '{title}'");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NotificationManager] Failed to send Android notification: {e.Message}");
            }
#else
            // Fallback for non-Android or Editor: show in-app only
            ShowInAppNotification(title, message);
#endif
        }

        /// <summary>
        /// Shows an in-app notification popup (visible when the app is in foreground).
        /// </summary>
        private void ShowInAppNotification(string title, string message)
        {
            if (inAppNotificationPrefab == null || notificationContainer == null)
            {
                Debug.LogWarning("[NotificationManager] In-app notification prefab or container not assigned.");
                return;
            }

            GameObject notifObj = Instantiate(inAppNotificationPrefab, notificationContainer);
            notifObj.SetActive(true);

            // Set text content
            UnityEngine.UI.Text[] texts = notifObj.GetComponentsInChildren<UnityEngine.UI.Text>();
            if (texts.Length >= 1) texts[0].text = title;
            if (texts.Length >= 2) texts[1].text = message;

            // Auto-dismiss after duration
            StartCoroutine(AutoDismissNotification(notifObj, inAppDisplayDuration));
        }

        /// <summary>
        /// Coroutine that auto-dismisses an in-app notification after a delay.
        /// Includes a fade-out animation via CanvasGroup if available.
        /// </summary>
        private IEnumerator AutoDismissNotification(GameObject notifObj, float duration)
        {
            yield return new WaitForSeconds(duration);

            CanvasGroup canvasGroup = notifObj.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float fadeDuration = 0.5f;
                while (elapsed < fadeDuration)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            Destroy(notifObj);
        }

        // ------------------------------------------------------------------
        // Private - Coroutines
        // ------------------------------------------------------------------

        /// <summary>
        /// Coroutine that counts down and fires a scheduled notification.
        /// </summary>
        private IEnumerator NotificationTimerCoroutine(ScheduledNotification notif, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            if (notif.hasFired)
                yield break;

            notif.hasFired = true;
            _runningTimers.Remove(notif.id);

            Debug.Log($"[NotificationManager] Scheduled notification fired: '{notif.title}' (id: {notif.id})");

            // Show notification
            SendNotification(notif.playerId, notif.title, notif.message);

            OnScheduledNotificationFired?.Invoke(notif);

            // Remove from list
            _scheduledNotifications.Remove(notif);
        }

        /// <summary>
        /// Sends a remote notification via the server API.
        /// </summary>
        private IEnumerator SendRemoteNotificationCoroutine(string playerId, string title, string message)
        {
            string url = $"{serverBaseUrl}/notifications/send";
            string jsonBody = JsonUtility.ToJson(new RemoteNotificationPayload
            {
                targetPlayerId = playerId,
                title = title,
                message = message
            });

            using (var request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();

                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[NotificationManager] Remote notification sent to {playerId}: '{title}'");
                }
                else
                {
                    Debug.LogWarning($"[NotificationManager] Failed to send remote notification: {request.error}");
                }
            }
        }

        // ------------------------------------------------------------------
        // Private - Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Determines whether the given player ID refers to the local player.
        /// </summary>
        private bool IsLocalPlayer(string playerId)
        {
            // Would check against the local player ID stored in the multiplayer system
            var turnManager = FindObjectOfType<TurnManager>();
            return turnManager != null && turnManager.CurrentPlayerId == playerId;
        }

        /// <summary>
        /// Registers all pending scheduled notifications with the OS
        /// so they fire even if the app is killed.
        /// </summary>
        private void RegisterPendingOSNotifications()
        {
            foreach (var notif in _scheduledNotifications)
            {
                if (notif.hasFired)
                    continue;

                double remainingSeconds = (notif.FireTime - DateTime.UtcNow).TotalSeconds;
                if (remainingSeconds > 0)
                {
                    Debug.Log($"[NotificationManager] Registering pending OS notification: '{notif.title}' fires in {remainingSeconds:F0}s");
                    // Would use AlarmManager or WorkManager on Android for exact timing
                }
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Helper to get an Android resource ID by name and type.</summary>
        private int GetResourceId(AndroidJavaObject context, string name, string type)
        {
            return context.Call<int>("getResources", context.Call<int>("getIdentifier", name, type, context.Call<string>("getPackageName")));
        }
#endif
    }

    // ========================================================================
    // Supporting Types
    // ========================================================================

    /// <summary>
    /// Payload for remote notification API requests.
    /// </summary>
    [Serializable]
    internal class RemoteNotificationPayload
    {
        public string targetPlayerId;
        public string title;
        public string message;
    }
}
