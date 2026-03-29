// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: BattleUI.cs
// Namespace: IronProtocol.UI
// Description: MonoBehaviour for the battle/tactical view UI. Shows combat
//              previews, animated results, unit action context menus, and
//              flanking direction indicators.
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
    /// Enumeration of available unit actions in the battle context menu.
    /// </summary>
    public enum UnitAction
    {
        Move,
        Attack,
        Fortify,
        Wait
    }

    /// <summary>
    /// Serializable container mapping a <see cref="UnitAction"/> to a button
    /// in the Inspector for easy wiring.
    /// </summary>
    [Serializable]
    public class ActionButtonMapping
    {
        [Tooltip("The action this button represents.")]
        public UnitAction action;

        [Tooltip("The UI Button for this action.")]
        public Button button;
    }

    /// <summary>
    /// MonoBehaviour that manages the battle/tactical view UI layer.
    /// Handles combat previews, animated result displays, unit action
    /// context menus, and flanking direction overlays.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields - Battle Preview
        // ------------------------------------------------------------------

        [Header("Battle Preview")]
        [Tooltip("Panel showing the predicted battle outcome before committing.")]
        [SerializeField] private GameObject battlePreviewPanel;

        [SerializeField] private Text previewAttackerText;
        [SerializeField] private Text previewDefenderText;
        [SerializeField] private Text previewDamageRangeText;
        [SerializeField] private Text previewFlankingText;
        [SerializeField] private Text previewMoraleEstimateText;
        [SerializeField] private Text previewCombinedArmsText;
        [SerializeField] private Image flankingIndicatorIcon;
        [SerializeField] private Sprite[] flankingLevelSprites; // Index: 0=None, 1=Partial, 2=Full, 3=Rear

        [Header("Preview Buttons")]
        [SerializeField] private Button previewConfirmButton;
        [SerializeField] private Button previewCancelButton;

        // ------------------------------------------------------------------
        // Inspector Fields - Battle Result
        // ------------------------------------------------------------------

        [Header("Battle Result")]
        [Tooltip("Panel showing the animated battle result after resolution.")]
        [SerializeField] private GameObject battleResultPanel;

        [SerializeField] private Text resultTitleText;
        [SerializeField] private Text resultDescriptionText;
        [SerializeField] private Text resultDamageText;
        [SerializeField] private Text resultCasualtiesText;
        [SerializeField] private Text resultMoraleText;
        [SerializeField] private Text resultExperienceText;
        [SerializeField] private Button resultDismissButton;

        [Header("Result Animation")]
        [Tooltip("Animator on the result panel for entry/exit animations.")]
        [SerializeField] private Animator resultAnimator;

        [Tooltip("Name of the 'Show' trigger parameter in the Animator controller.")]
        [SerializeField] private string showTriggerName = "ShowResult";

        // ------------------------------------------------------------------
        // Inspector Fields - Unit Actions
        // ------------------------------------------------------------------

        [Header("Unit Actions")]
        [Tooltip("Context menu panel for unit actions (Move, Attack, Fortify, Wait).")]
        [SerializeField] private GameObject unitActionsPanel;

        [Tooltip("List of action button mappings wired in the Inspector.")]
        [SerializeField] private List<ActionButtonMapping> actionButtons = new List<ActionButtonMapping>();

        // ------------------------------------------------------------------
        // Inspector Fields - Flanking Overlay
        // ------------------------------------------------------------------

        [Header("Flanking Indicator")]
        [Tooltip("Arrow prefab instantiated to show flanking direction on the map.")]
        [SerializeField] private GameObject flankingArrowPrefab;

        [Tooltip("Transform parent for flanking arrow instances (usually the map overlay).")]
        [SerializeField] private Transform flankingOverlayParent;

        [Tooltip("Color for flanking arrows by level.")]
        [SerializeField] private Color noFlankColor = Color.gray;
        [SerializeField] private Color partialFlankColor = Color.yellow;
        [SerializeField] private Color fullFlankColor = Color.orange;
        [SerializeField] private Color rearFlankColor = Color.red;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the player confirms an attack from the preview. Parameters: (attacker, defender).</summary>
        public event Action<IronProtocol.Military.UnitBase, IronProtocol.Military.UnitBase> OnAttackConfirmed;

        /// <summary>Fired when the player cancels the battle preview.</summary>
        public event Action OnPreviewCancelled;

        /// <summary>Fired when the player selects a unit action.</summary>
        public event Action<UnitAction> OnUnitActionSelected;

        // ------------------------------------------------------------------
        // Runtime State
        // ------------------------------------------------------------------

        /// <summary>The most recently displayed combat result.</summary>
        public IronProtocol.Military.CombatResult LastResult { get; private set; }

        /// <summary>The unit currently targeted by the battle preview.</summary>
        private IronProtocol.Military.UnitBase _previewAttacker;
        private IronProtocol.Military.UnitBase _previewDefender;

        /// <summary>Currently active flanking arrow instance.</summary>
        private GameObject _activeFlankingArrow;

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Hide panels initially
            if (battlePreviewPanel != null) battlePreviewPanel.SetActive(false);
            if (battleResultPanel != null) battleResultPanel.SetActive(false);
            if (unitActionsPanel != null) unitActionsPanel.SetActive(false);

            // Wire preview buttons
            if (previewConfirmButton != null)
                previewConfirmButton.onClick.AddListener(OnPreviewConfirmClicked);

            if (previewCancelButton != null)
                previewCancelButton.onClick.AddListener(OnPreviewCancelClicked);

            // Wire result dismiss button
            if (resultDismissButton != null)
                resultDismissButton.onClick.AddListener(OnResultDismissClicked);

            // Wire action buttons
            foreach (var mapping in actionButtons)
            {
                if (mapping.button != null)
                {
                    UnitAction capturedAction = mapping.action; // Closure capture
                    mapping.button.onClick.AddListener(() => OnActionButtonClicked(capturedAction));
                }
            }
        }

        private void OnDestroy()
        {
            if (previewConfirmButton != null) previewConfirmButton.onClick.RemoveAllListeners();
            if (previewCancelButton != null) previewCancelButton.onClick.RemoveAllListeners();
            if (resultDismissButton != null) resultDismissButton.onClick.RemoveAllListeners();
        }

        // ------------------------------------------------------------------
        // Public API - Battle Preview
        // ------------------------------------------------------------------

        /// <summary>
        /// Displays a battle preview panel with predicted damage range,
        /// flanking indicator, morale estimate, and combined arms bonus.
        /// </summary>
        /// <param name="attacker">The attacking unit.</param>
        /// <param name="defender">The defending unit.</param>
        /// <param name="flank">The calculated flanking level.</param>
        /// <param name="combinedArms">Whether a combined arms bonus applies.</param>
        public void ShowBattlePreview(
            IronProtocol.Military.UnitBase attacker,
            IronProtocol.Military.UnitBase defender,
            IronProtocol.Military.FlankingLevel flank,
            bool combinedArms)
        {
            if (battlePreviewPanel == null || attacker == null || defender == null)
            {
                Debug.LogWarning("[BattleUI] Cannot show preview – missing panel or unit references.");
                return;
            }

            _previewAttacker = attacker;
            _previewDefender = defender;

            // Calculate predicted damage range
            float minDamage = CalculateMinDamage(attacker, defender, flank);
            float maxDamage = CalculateMaxDamage(attacker, defender, flank);
            float flankMultiplier = GetFlankingMultiplier(flank);
            float moraleEstimate = CalculateMoraleEstimate(attacker, defender, flank);

            // Populate text fields
            if (previewAttackerText != null)
                previewAttackerText.text = $"{attacker.UnitName} (ATK {attacker.Attack})";

            if (previewDefenderText != null)
                previewDefenderText.text = $"{defender.UnitName} (DEF {defender.Defense})";

            if (previewDamageRangeText != null)
                previewDamageRangeText.text = $"Damage: {minDamage:F0} – {maxDamage:F0}";

            if (previewFlankingText != null)
            {
                string flankLabel = flank.ToString();
                string flankBonus = flankMultiplier > 1f ? $" (+{((flankMultiplier - 1f) * 100f):F0}%)" : "";
                previewFlankingText.text = $"Flanking: {flankLabel}{flankBonus}";
            }

            if (previewMoraleEstimateText != null)
                previewMoraleEstimateText.text = $"Morale Impact: {moraleEstimate:+0.0;-0.0}";

            if (previewCombinedArmsText != null)
            {
                previewCombinedArmsText.text = combinedArms
                    ? "<color=green>Combined Arms: Active (+15%)</color>"
                    : "<color=gray>Combined Arms: None</color>";
            }

            // Flanking indicator icon
            if (flankingIndicatorIcon != null && flankingLevelSprites != null)
            {
                int index = (int)flank;
                if (index >= 0 && index < flankingLevelSprites.Length)
                {
                    flankingIndicatorIcon.sprite = flankingLevelSprites[index];
                    flankingIndicatorIcon.gameObject.SetActive(true);
                }
            }

            battlePreviewPanel.SetActive(true);
        }

        /// <summary>
        /// Hides the battle preview panel without committing the attack.
        /// </summary>
        public void HideBattlePreview()
        {
            if (battlePreviewPanel != null)
            {
                battlePreviewPanel.SetActive(false);
            }
            _previewAttacker = null;
            _previewDefender = null;
        }

        // ------------------------------------------------------------------
        // Public API - Battle Result
        // ------------------------------------------------------------------

        /// <summary>
        /// Displays the animated battle result panel with full combat details.
        /// </summary>
        /// <param name="result">The resolved combat result.</param>
        public void ShowBattleResult(IronProtocol.Military.CombatResult result)
        {
            if (battleResultPanel == null || result == null)
            {
                Debug.LogWarning("[BattleUI] Cannot show result – missing panel or null result.");
                return;
            }

            LastResult = result;

            // Populate fields
            if (resultTitleText != null)
                resultTitleText.text = result.IsVictory ? "Victory!" : "Defeat!";

            if (resultDescriptionText != null)
                resultDescriptionText.text = $"{result.AttackerName} engaged {result.DefenderName}.\nOutcome: {result.Outcome}";

            if (resultDamageText != null)
                resultDamageText.text = $"Total Damage: {result.DamageDealt:F1}";

            if (resultCasualtiesText != null)
                resultCasualtiesText.text = $"Losses: {result.AttackerLosses} attackers / {result.DefenderLosses} defenders";

            if (resultMoraleText != null)
                resultMoraleText.text = $"Morale Change: {result.MoraleChange:+0.0;-0.0}";

            if (resultExperienceText != null)
                resultExperienceText.text = $"XP Gained: +{result.ExperienceGained}";

            // Trigger animation
            battleResultPanel.SetActive(true);

            if (resultAnimator != null && !string.IsNullOrEmpty(showTriggerName))
            {
                resultAnimator.SetTrigger(showTriggerName);
            }

            Debug.Log($"[BattleUI] Battle result displayed: {result.Outcome}");
        }

        /// <summary>
        /// Hides the battle result panel.
        /// </summary>
        public void HideBattleResult()
        {
            if (battleResultPanel != null)
            {
                battleResultPanel.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Public API - Unit Actions
        // ------------------------------------------------------------------

        /// <summary>
        /// Displays the unit action context menu for the specified unit.
        /// Enables/disables individual action buttons based on unit state.
        /// </summary>
        /// <param name="unit">The unit to show actions for.</param>
        public void ShowUnitActions(IronProtocol.Military.UnitBase unit)
        {
            if (unitActionsPanel == null || unit == null)
            {
                Debug.LogWarning("[BattleUI] Cannot show actions – missing panel or unit.");
                return;
            }

            // Configure button availability based on unit state
            foreach (var mapping in actionButtons)
            {
                if (mapping.button == null) continue;

                switch (mapping.action)
                {
                    case UnitAction.Move:
                        mapping.button.interactable = unit.MovementRemaining > 0;
                        break;

                    case UnitAction.Attack:
                        mapping.button.interactable = unit.CanAttack;
                        break;

                    case UnitAction.Fortify:
                        mapping.button.interactable = !unit.HasFortified;
                        break;

                    case UnitAction.Wait:
                        mapping.button.interactable = true;
                        break;
                }
            }

            unitActionsPanel.SetActive(true);
        }

        /// <summary>
        /// Hides the unit action context menu.
        /// </summary>
        public void HideActions()
        {
            if (unitActionsPanel != null)
            {
                unitActionsPanel.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Public API - Flanking Indicator
        // ------------------------------------------------------------------

        /// <summary>
        /// Displays a directional arrow on the map indicating the flanking
        /// angle from the attacker's hex to the defender's hex.
        /// </summary>
        /// <param name="from">The attacker's hex coordinate.</param>
        /// <param name="to">The defender's hex coordinate.</param>
        /// <param name="level">The calculated flanking level.</param>
        public void ShowFlankingIndicator(
            IronProtocol.HexMap.HexCoord from,
            IronProtocol.HexMap.HexCoord to,
            IronProtocol.Military.FlankingLevel level)
        {
            if (flankingArrowPrefab == null || flankingOverlayParent == null)
            {
                Debug.LogWarning("[BattleUI] Flanking arrow prefab or overlay parent not assigned.");
                return;
            }

            // Remove existing arrow
            ClearFlankingIndicator();

            // Instantiate arrow
            _activeFlankingArrow = Instantiate(flankingArrowPrefab, flankingOverlayParent);

            // Position arrow between the two hexes (approximate)
            Vector3 fromWorld = HexToWorldPosition(from);
            Vector3 toWorld = HexToWorldPosition(to);
            Vector3 midPoint = (fromWorld + toWorld) * 0.5f;

            _activeFlankingArrow.transform.position = midPoint;

            // Rotate arrow to point from → to
            Vector3 direction = (toWorld - fromWorld).normalized;
            if (direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                _activeFlankingArrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Color by flanking level
            Image arrowImage = _activeFlankingArrow.GetComponentInChildren<Image>();
            if (arrowImage != null)
            {
                arrowImage.color = level switch
                {
                    IronProtocol.Military.FlankingLevel.None    => noFlankColor,
                    IronProtocol.Military.FlankingLevel.Partial => partialFlankColor,
                    IronProtocol.Military.FlankingLevel.Full    => fullFlankColor,
                    IronProtocol.Military.FlankingLevel.Rear    => rearFlankColor,
                    _                                            => noFlankColor
                };
            }

            _activeFlankingArrow.SetActive(true);
            Debug.Log($"[BattleUI] Flanking indicator shown: {level} ({from} → {to})");
        }

        /// <summary>
        /// Removes the active flanking direction arrow from the map.
        /// </summary>
        public void ClearFlankingIndicator()
        {
            if (_activeFlankingArrow != null)
            {
                Destroy(_activeFlankingArrow);
                _activeFlankingArrow = null;
            }
        }

        // ------------------------------------------------------------------
        // Private Helpers - Combat Calculations
        // ------------------------------------------------------------------

        /// <summary>Calculates minimum predicted damage (no crits, defender advantage).</summary>
        private float CalculateMinDamage(
            IronProtocol.Military.UnitBase attacker,
            IronProtocol.Military.UnitBase defender,
            IronProtocol.Military.FlankingLevel flank)
        {
            float baseDamage = Mathf.Max(0f, attacker.Attack - defender.Defense * 0.5f);
            return baseDamage * 0.7f * GetFlankingMultiplier(flank);
        }

        /// <summary>Calculates maximum predicted damage (crits, flanking, no fortification).</summary>
        private float CalculateMaxDamage(
            IronProtocol.Military.UnitBase attacker,
            IronProtocol.Military.UnitBase defender,
            IronProtocol.Military.FlankingLevel flank)
        {
            float baseDamage = attacker.Attack * (1f + attacker.Attack * 0.02f);
            float defenseReduction = defender.HasFortified ? 0.7f : 1f;
            return baseDamage * 1.3f * GetFlankingMultiplier(flank) * defenseReduction;
        }

        /// <summary>Returns the damage multiplier for the given flanking level.</summary>
        private float GetFlankingMultiplier(IronProtocol.Military.FlankingLevel flank)
        {
            return flank switch
            {
                IronProtocol.Military.FlankingLevel.None    => 1.0f,
                IronProtocol.Military.FlankingLevel.Partial => 1.15f,
                IronProtocol.Military.FlankingLevel.Full    => 1.3f,
                IronProtocol.Military.FlankingLevel.Rear    => 1.5f,
                _                                            => 1.0f
            };
        }

        /// <summary>Estimates morale change from the engagement.</summary>
        private float CalculateMoraleEstimate(
            IronProtocol.Military.UnitBase attacker,
            IronProtocol.Military.UnitBase defender,
            IronProtocol.Military.FlankingLevel flank)
        {
            float baseMorale = (attacker.Attack - defender.Defense) * 0.1f;
            float flankMorale = ((int)flank) * -2f; // Higher flank = more morale loss for defender
            return -(baseMorale + flankMorale);
        }

        /// <summary>
        /// Converts a hex coordinate to an approximate world position.
        /// Uses axial hex layout calculations.
        /// </summary>
        private Vector3 HexToWorldPosition(IronProtocol.HexMap.HexCoord hex)
        {
            float x = 1.5f * hex.Q;
            float z = Mathf.Sqrt(3f) * (hex.R + hex.Q * 0.5f);
            return new Vector3(x, 0f, z);
        }

        // ------------------------------------------------------------------
        // Button Handlers
        // ------------------------------------------------------------------

        private void OnPreviewConfirmClicked()
        {
            Debug.Log("[BattleUI] Attack confirmed from preview.");
            OnAttackConfirmed?.Invoke(_previewAttacker, _previewDefender);
            HideBattlePreview();
        }

        private void OnPreviewCancelClicked()
        {
            Debug.Log("[BattleUI] Battle preview cancelled.");
            HideBattlePreview();
            OnPreviewCancelled?.Invoke();
        }

        private void OnResultDismissClicked()
        {
            Debug.Log("[BattleUI] Battle result dismissed.");
            HideBattleResult();
        }

        private void OnActionButtonClicked(UnitAction action)
        {
            Debug.Log($"[BattleUI] Unit action selected: {action}");
            HideActions();
            OnUnitActionSelected?.Invoke(action);
        }
    }
}
