// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: HUDController.cs
// Namespace: IronProtocol.UI
// Description: MonoBehaviour controlling the in-game heads-up display (HUD).
//              Shows turn information, treasury, selected unit details,
//              weather, morale, and the End Turn button.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace IronProtocol.UI
{
    /// <summary>
    /// Lightweight custom UI component that visually represents a 0–1 progress bar.
    /// Wraps a Unity <see cref="Image"/> with filled type to avoid exposing the
    /// raw Image API.
    /// </summary>
    public class ProgressBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text label;

        private float _currentValue;

        /// <summary>
        /// Gets or sets the current fill amount (clamped to 0–1).
        /// </summary>
        public float Value
        {
            get => _currentValue;
            set
            {
                _currentValue = Mathf.Clamp01(value);
                if (fillImage != null)
                {
                    fillImage.fillAmount = _currentValue;
                }

                // Tint color based on value thresholds
                if (fillImage != null)
                {
                    fillImage.color = _currentValue > 0.6f ? Color.green
                                 : _currentValue > 0.3f ? Color.yellow
                                 : Color.red;
                }
            }
        }

        /// <summary>
        /// Optional text label displayed alongside the bar (e.g. "42 / 100").
        /// </summary>
        public string LabelText
        {
            get => label != null ? label.text : string.Empty;
            set { if (label != null) label.text = value; }
        }

        /// <summary>
        /// Sets the bar to a fraction of a max value and updates the label.
        /// </summary>
        /// <param name="current">Current value.</param>
        /// <param name="max">Maximum value.</param>
        public void SetValue(float current, float max)
        {
            if (max <= 0f)
            {
                Value = 0f;
                LabelText = "0 / 0";
                return;
            }
            Value = current / max;
            LabelText = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    /// <summary>
    /// MonoBehaviour that drives the in-game HUD. Binds to Text, Image,
    /// ProgressBar, and Button references via the Inspector and exposes
    /// a clean public API for the rest of the game to update the display.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields - Top Bar
        // ------------------------------------------------------------------

        [Header("Top Bar")]
        [Tooltip("Displays the current turn number (e.g. 'Turn 12').")]
        [SerializeField] private Text turnText;

        [Tooltip("Displays the player's treasury amount.")]
        [SerializeField] private Text treasuryText;

        [Tooltip("Displays a summary line of all owned resources.")]
        [SerializeField] private Text resourceOverviewText;

        // ------------------------------------------------------------------
        // Inspector Fields - Buttons
        // ------------------------------------------------------------------

        [Header("Buttons")]
        [Tooltip("Button that the player clicks to end their turn.")]
        [SerializeField] private Button endTurnButton;

        [Tooltip("Button that opens the main menu or settings overlay.")]
        [SerializeField] private Button menuButton;

        // ------------------------------------------------------------------
        // Inspector Fields - Weather
        // ------------------------------------------------------------------

        [Header("Weather")]
        [Tooltip("Image component showing the current weather icon.")]
        [SerializeField] private Image weatherIcon;

        [Tooltip("Sprite used when weather is Clear.")]
        [SerializeField] private Sprite weatherClearSprite;

        [Tooltip("Sprite used when weather is Rain.")]
        [SerializeField] private Sprite weatherRainSprite;

        [Tooltip("Sprite used when weather is Storm.")]
        [SerializeField] private Sprite weatherStormSprite;

        [Tooltip("Sprite used when weather is Snow.")]
        [SerializeField] private Sprite weatherSnowSprite;

        [Tooltip("Sprite used when weather is Fog.")]
        [SerializeField] private Sprite weatherFogSprite;

        // ------------------------------------------------------------------
        // Inspector Fields - Selected Unit Panel
        // ------------------------------------------------------------------

        [Header("Selected Unit Panel")]
        [Tooltip("Parent panel showing info about the currently selected unit.")]
        [SerializeField] private GameObject unitInfoPanel;

        [Tooltip("Displays the selected unit's name, type, and status.")]
        [SerializeField] private Text unitInfoText;

        [Tooltip("Progress bar for the selected unit's hit points.")]
        [SerializeField] private ProgressBar unitHPBar;

        [Tooltip("Progress bar for the selected unit's morale.")]
        [SerializeField] private ProgressBar moraleBar;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the player clicks the End Turn button.</summary>
        public event Action OnEndTurn;

        /// <summary>Fired when the player clicks the Menu button.</summary>
        public event Action OnMenuClicked;

        // ------------------------------------------------------------------
        // Private State
        // ------------------------------------------------------------------

        private int _currentTurn = 1;
        private float _treasury = 0f;

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Wire button listeners
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(OnMenuButtonClicked);
            }

            // Hide unit panel initially
            if (unitInfoPanel != null)
            {
                unitInfoPanel.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Updates the turn counter display.
        /// </summary>
        /// <param name="turnNumber">The new turn number (1-based).</param>
        public void UpdateTurn(int turnNumber)
        {
            _currentTurn = turnNumber;
            if (turnText != null)
            {
                turnText.text = $"Turn {turnNumber}";
            }
        }

        /// <summary>
        /// Updates the treasury amount display.
        /// </summary>
        /// <param name="amount">The player's current treasury balance.</param>
        public void UpdateTreasury(float amount)
        {
            _treasury = amount;
            if (treasuryText != null)
            {
                treasuryText.text = $"${amount:##,##0}";
            }
        }

        /// <summary>
        /// Populates the unit info panel with data from the selected unit.
        /// Shows HP bar, morale bar, unit name, and type.
        /// </summary>
        /// <param name="unit">The unit to display, or <c>null</c> to clear.</param>
        public void UpdateSelectedUnit(IronProtocol.Military.UnitBase unit)
        {
            if (unitInfoPanel == null)
                return;

            if (unit == null)
            {
                ClearSelection();
                return;
            }

            unitInfoPanel.SetActive(true);

            if (unitInfoText != null)
            {
                unitInfoText.text = $"{unit.UnitName} ({unit.UnitType})\n" +
                                    $"ATK: {unit.Attack}  DEF: {unit.Defense}\n" +
                                    $"Moves: {unit.MovementRemaining}/{unit.MovementRange}";
            }

            if (unitHPBar != null)
            {
                unitHPBar.SetValue(unit.CurrentHP, unit.MaxHP);
            }

            if (moraleBar != null)
            {
                moraleBar.SetValue(unit.Morale, 100f);
            }
        }

        /// <summary>
        /// Hides the selected unit info panel and resets bars.
        /// </summary>
        public void ClearSelection()
        {
            if (unitInfoPanel != null)
            {
                unitInfoPanel.SetActive(false);
            }

            if (unitInfoText != null)
            {
                unitInfoText.text = string.Empty;
            }

            if (unitHPBar != null)
            {
                unitHPBar.Value = 0f;
            }

            if (moraleBar != null)
            {
                moraleBar.Value = 0f;
            }
        }

        /// <summary>
        /// Updates the weather icon based on the current <see cref="IronProtocol.Weather.WeatherType"/>.
        /// </summary>
        /// <param name="weather">The current weather condition.</param>
        public void OnWeatherChanged(IronProtocol.Weather.WeatherType weather)
        {
            if (weatherIcon == null)
                return;

            Sprite sprite = weather switch
            {
                IronProtocol.Weather.WeatherType.Clear => weatherClearSprite,
                IronProtocol.Weather.WeatherType.Rain  => weatherRainSprite,
                IronProtocol.Weather.WeatherType.Storm => weatherStormSprite,
                IronProtocol.Weather.WeatherType.Snow  => weatherSnowSprite,
                IronProtocol.Weather.WeatherType.Fog   => weatherFogSprite,
                _                                       => weatherClearSprite
            };

            weatherIcon.sprite = sprite;
            weatherIcon.gameObject.SetActive(sprite != null);
        }

        /// <summary>
        /// Updates the resource overview text in the top bar.
        /// </summary>
        /// <param name="overview">A formatted string summarizing owned resources.</param>
        public void UpdateResourceOverview(string overview)
        {
            if (resourceOverviewText != null)
            {
                resourceOverviewText.text = overview;
            }
        }

        /// <summary>
        /// Convenience method to refresh all HUD fields at once.
        /// Called by <see cref="UIManager.UpdateHUD"/> and during turn transitions.
        /// </summary>
        public void Refresh()
        {
            UpdateTurn(_currentTurn);
            UpdateTreasury(_treasury);
        }

        // ------------------------------------------------------------------
        // Button Handlers
        // ------------------------------------------------------------------

        /// <summary>
        /// Internal handler for the End Turn button click.
        /// Fires the <see cref="OnEndTurn"/> event.
        /// </summary>
        private void OnEndTurnClicked()
        {
            Debug.Log("[HUDController] End Turn clicked.");
            OnEndTurn?.Invoke();
        }

        /// <summary>
        /// Internal handler for the Menu button click.
        /// Fires the <see cref="OnMenuClicked"/> event.
        /// </summary>
        private void OnMenuButtonClicked()
        {
            Debug.Log("[HUDController] Menu clicked.");
            OnMenuClicked?.Invoke();
        }
    }
}
