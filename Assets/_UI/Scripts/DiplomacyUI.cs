// ============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: DiplomacyUI.cs
// Namespace: IronProtocol.UI
// Description: MonoBehaviour for the diplomacy screen. Displays nation
//              relations, war declaration options with Casus Belli, peace
//              proposals, alliance management, and occupation choices.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IronProtocol.UI
{
    /// <summary>
    /// Serializable wrapper for a diplomacy relation entry displayed in the UI.
    /// </summary>
    [Serializable]
    public class RelationDisplayEntry
    {
        [Tooltip("Target nation identifier.")]
        public string nationId;

        [Tooltip("Target nation display name.")]
        public string nationName;

        [Tooltip("Current opinion score (-100 to +100).")]
        public int opinionScore;

        [Tooltip("Current diplomatic state (Neutral, Friendly, Allied, War, etc.).")]
        public string diplomaticState;

        [Tooltip("Nation color for the UI indicator.")]
        public Color nationColor;

        [Tooltip("Whether the nation is an AI-controlled opponent.")]
        public bool isAI;
    }

    /// <summary>
    /// Component for an individual relation card in the diplomacy list.
    /// </summary>
    public class RelationCard : MonoBehaviour
    {
        [Header("Relation Display")]
        [SerializeField] private Image nationColorImage;
        [SerializeField] private Text nationNameText;
        [SerializeField] private Text opinionText;
        [SerializeField] private Text stateText;
        [SerializeField] private Image opinionBarFill;

        [Header("Action Buttons")]
        [SerializeField] private Button warButton;
        [SerializeField] private Button peaceButton;
        [SerializeField] private Button allianceButton;
        [SerializeField] private Button detailsButton;

        /// <summary>Fired when War is requested against this nation.</summary>
        public event Action<string> OnWarRequested;

        /// <summary>Fired when Peace is requested with this nation.</summary>
        public event Action<string> OnPeaceRequested;

        /// <summary>Fired when an Alliance is proposed to this nation.</summary>
        public event Action<string> OnAllianceRequested;

        /// <summary>Fired when the Details button is clicked.</summary>
        public event Action<string> OnDetailsRequested;

        private string _nationId;

        private void OnEnable()
        {
            if (warButton != null)      warButton.onClick.AddListener(() => OnWarRequested?.Invoke(_nationId));
            if (peaceButton != null)    peaceButton.onClick.AddListener(() => OnPeaceRequested?.Invoke(_nationId));
            if (allianceButton != null) allianceButton.onClick.AddListener(() => OnAllianceRequested?.Invoke(_nationId));
            if (detailsButton != null)  detailsButton.onClick.AddListener(() => OnDetailsRequested?.Invoke(_nationId));
        }

        private void OnDisable()
        {
            if (warButton != null)      warButton.onClick.RemoveAllListeners();
            if (peaceButton != null)    peaceButton.onClick.RemoveAllListeners();
            if (allianceButton != null) allianceButton.onClick.RemoveAllListeners();
            if (detailsButton != null)  detailsButton.onClick.RemoveAllListeners();
        }

        /// <summary>Populates the relation card from a display entry.</summary>
        public void Populate(RelationDisplayEntry entry)
        {
            _nationId = entry.nationId;

            if (nationColorImage != null)   nationColorImage.color = entry.nationColor;
            if (nationNameText != null)     nationNameText.text = entry.nationName;
            if (stateText != null)          stateText.text = entry.diplomaticState;

            // Opinion text and bar
            if (opinionText != null)
            {
                string sign = entry.opinionScore >= 0 ? "+" : "";
                opinionText.text = $"{sign}{entry.opinionScore}";
                opinionText.color = entry.opinionScore >= 0 ? Color.green : Color.red;
            }

            if (opinionBarFill != null)
            {
                // Map -100..+100 to 0..1
                opinionBarFill.fillAmount = (entry.opinionScore + 100f) / 200f;
                opinionBarFill.color = entry.opinionScore >= 0 ? Color.green : Color.red;
            }

            // Enable/disable buttons based on state
            bool atWar = entry.diplomaticState == "War";
            bool allied = entry.diplomaticState == "Allied";

            if (warButton != null)      warButton.interactable = !atWar && entry.isAI;
            if (peaceButton != null)    peaceButton.interactable = atWar;
            if (allianceButton != null) allianceButton.interactable = !atWar && !allied && entry.isAI;
        }
    }

    /// <summary>
    /// Component for a Casus Belli option in the war declaration panel.
    /// </summary>
    public class CasusBelliOption : MonoBehaviour
    {
        [SerializeField] private Text cbNameText;
        [SerializeField] private Text cbDescriptionText;
        [SerializeField] private Text cbCostText;
        [SerializeField] private Button selectButton;

        /// <summary>Fired when this CB is selected. Parameters: (cbType, cost).</summary>
        public event Action<IronProtocol.GameSystems.Diplomacy.CBType, float> OnSelected;

        private IronProtocol.GameSystems.Diplomacy.CBType _cbType;
        private float _stabilityCost;

        private void Awake()
        {
            if (selectButton != null)
                selectButton.onClick.AddListener(OnSelectClicked);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveAllListeners();
        }

        public void Populate(IronProtocol.GameSystems.Diplomacy.CasusBelli cb)
        {
            _cbType = cb.CBType;
            _stabilityCost = cb.StabilityCost;

            if (cbNameText != null)        cbNameText.text = cb.Name;
            if (cbDescriptionText != null) cbDescriptionText.text = cb.Description;
            if (cbCostText != null)        cbCostText.text = $"Stability Cost: {-cb.StabilityCost:F0}";
        }

        private void OnSelectClicked() => OnSelected?.Invoke(_cbType, _stabilityCost);
    }

    /// <summary>
    /// MonoBehaviour that manages the diplomacy screen UI. Displays nation
    /// relations, war options with available Casus Belli, peace proposals,
    /// alliance management, and occupation choice dialogs.
    /// </summary>
    public class DiplomacyUI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector Fields - Relations List
        // ------------------------------------------------------------------

        [Header("Relations List")]
        [Tooltip("Content transform for the scrollable relations list.")]
        [SerializeField] private Transform relationsListContent;

        [Tooltip("Prefab for each relation card entry.")]
        [SerializeField] private GameObject relationCardPrefab;

        // ------------------------------------------------------------------
        // Inspector Fields - War Declaration
        // ------------------------------------------------------------------

        [Header("War Declaration Panel")]
        [Tooltip("Overlay panel for declaring war with CB selection.")]
        [SerializeField] private GameObject warDeclarationPanel;

        [SerializeField] private Text warTargetText;
        [SerializeField] private Transform casusBelliListContent;
        [SerializeField] private GameObject casusBelliOptionPrefab;
        [SerializeField] private Button declareWarConfirmButton;
        [SerializeField] private Button declareWarCancelButton;

        // ------------------------------------------------------------------
        // Inspector Fields - Occupation Choice
        // ------------------------------------------------------------------

        [Header("Occupation Choice Panel")]
        [Tooltip("Overlay panel for choosing occupation policy.")]
        [SerializeField] private GameObject occupationPanel;

        [SerializeField] private Text occupationTerritoryText;
        [SerializeField] private Button directAnnexButton;
        [SerializeField] private Button puppetGovernmentButton;
        [SerializeField] private Button occupationCancelButton;

        // ------------------------------------------------------------------
        // Inspector Fields - Confirmation Dialog
        // ------------------------------------------------------------------

        [Header("Confirmation Dialog")]
        [Tooltip("Generic confirmation dialog for peace/alliance proposals.")]
        [SerializeField] private GameObject confirmationDialog;

        [SerializeField] private Text confirmationTitleText;
        [SerializeField] private Text confirmationMessageText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        // ------------------------------------------------------------------
        // Events
        // ------------------------------------------------------------------

        /// <summary>Fired when the player declares war. Parameters: (targetNationId, cbType).</summary>
        public event Action<string, IronProtocol.GameSystems.Diplomacy.CBType> OnDeclareWar;

        /// <summary>Fired when the player requests peace. Parameter: targetNationId.</summary>
        public event Action<string> OnMakePeace;

        /// <summary>Fired when the player proposes an alliance. Parameter: targetNationId.</summary>
        public event Action<string> OnFormAlliance;

        /// <summary>Fired when the player chooses Direct Annexation. Parameter: territoryId.</summary>
        public event Action<string> OnDirectAnnex;

        /// <summary>Fired when the player chooses Puppet Government. Parameter: territoryId.</summary>
        public event Action<string> OnPuppetGovernment;

        // ------------------------------------------------------------------
        // Private State
        // ------------------------------------------------------------------

        private readonly List<RelationCard> _spawnedCards = new List<RelationCard>();
        private string _warTargetNation;
        private IronProtocol.GameSystems.Diplomacy.CBType _selectedCB;
        private string _occupationTerritoryId;

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Hide overlay panels
            if (warDeclarationPanel != null) warDeclarationPanel.SetActive(false);
            if (occupationPanel != null) occupationPanel.SetActive(false);
            if (confirmationDialog != null) confirmationDialog.SetActive(false);

            // Wire buttons
            if (declareWarConfirmButton != null) declareWarConfirmButton.onClick.AddListener(OnDeclareWarConfirmed);
            if (declareWarCancelButton != null) declareWarCancelButton.onClick.AddListener(OnDeclareWarCancelled);
            if (directAnnexButton != null) directAnnexButton.onClick.AddListener(OnDirectAnnexClicked);
            if (puppetGovernmentButton != null) puppetGovernmentButton.onClick.AddListener(OnPuppetGovernmentClicked);
            if (occupationCancelButton != null) occupationCancelButton.onClick.AddListener(OnOccupationCancelled);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        }

        private void OnDestroy()
        {
            if (declareWarConfirmButton != null) declareWarConfirmButton.onClick.RemoveAllListeners();
            if (declareWarCancelButton != null) declareWarCancelButton.onClick.RemoveAllListeners();
            if (directAnnexButton != null) directAnnexButton.onClick.RemoveAllListeners();
            if (puppetGovernmentButton != null) puppetGovernmentButton.onClick.RemoveAllListeners();
            if (occupationCancelButton != null) occupationCancelButton.onClick.RemoveAllListeners();
            if (confirmYesButton != null) confirmYesButton.onClick.RemoveAllListeners();
            if (confirmNoButton != null) confirmNoButton.onClick.RemoveAllListeners();
        }

        // ------------------------------------------------------------------
        // Public API - Relations
        // ------------------------------------------------------------------

        /// <summary>
        /// Populates the relations list with cards for each nation relationship.
        /// Clears any previously displayed relations first.
        /// </summary>
        /// <param name="relations">List of diplomacy relations from the game model.</param>
        public void ShowRelations(List<IronProtocol.GameSystems.Diplomacy.DiplomacyManager.Relation> relations)
        {
            ClearRelationCards();

            if (relations == null || relationsListContent == null || relationCardPrefab == null)
            {
                Debug.LogWarning("[DiplomacyUI] Cannot show relations – missing references or data.");
                return;
            }

            foreach (var relation in relations)
            {
                // Convert game model relation to display entry
                RelationDisplayEntry entry = new RelationDisplayEntry
                {
                    nationId = relation.TargetNationId,
                    nationName = relation.TargetNationName,
                    opinionScore = relation.OpinionScore,
                    diplomaticState = relation.CurrentState,
                    nationColor = relation.TargetNationColor,
                    isAI = relation.IsAI
                };

                GameObject cardObj = Instantiate(relationCardPrefab, relationsListContent);
                RelationCard card = cardObj.GetComponent<RelationCard>();

                if (card == null)
                {
                    Debug.LogWarning("[DiplomacyUI] RelationCard component missing on prefab.");
                    Destroy(cardObj);
                    continue;
                }

                card.Populate(entry);

                // Wire card events
                card.OnWarRequested += ShowWarOptions;
                card.OnPeaceRequested += HandlePeaceRequest;
                card.OnAllianceRequested += HandleAllianceRequest;

                _spawnedCards.Add(card);
            }

            Debug.Log($"[DiplomacyUI] Displayed {_spawnedCards.Count} nation relations.");
        }

        // ------------------------------------------------------------------
        // Public API - War Declaration
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens the war declaration panel for the specified target nation,
        /// populated with available Casus Belli options.
        /// </summary>
        /// <param name="targetNation">The nation to declare war against.</param>
        /// <param name="availableCBs">List of valid Casus Belli for this war declaration.</param>
        public void ShowWarOptions(string targetNation, List<IronProtocol.GameSystems.Diplomacy.CasusBelli> availableCBs)
        {
            if (warDeclarationPanel == null || casusBelliListContent == null)
            {
                Debug.LogWarning("[DiplomacyUI] War declaration panel references not assigned.");
                return;
            }

            _warTargetNation = targetNation;
            _selectedCB = IronProtocol.GameSystems.Diplomacy.CBType.None;

            // Update target text
            if (warTargetText != null)
                warTargetText.text = $"Declare War on: {targetNation}";

            // Clear existing CB options
            foreach (Transform child in casusBelliListContent)
            {
                Destroy(child.gameObject);
            }

            // Populate CB options
            if (availableCBs != null && casusBelliOptionPrefab != null)
            {
                foreach (var cb in availableCBs)
                {
                    GameObject cbObj = Instantiate(casusBelliOptionPrefab, casusBelliListContent);
                    CasusBelliOption cbOption = cbObj.GetComponent<CasusBelliOption>();

                    if (cbOption != null)
                    {
                        cbOption.Populate(cb);
                        cbOption.OnSelected += OnCBSelected;
                    }
                }
            }

            // Disable confirm until a CB is selected
            if (declareWarConfirmButton != null)
                declareWarConfirmButton.interactable = false;

            warDeclarationPanel.SetActive(true);
            Debug.Log($"[DiplomacyUI] War options shown for: {targetNation} ({availableCBs?.Count ?? 0} CBs)");
        }

        /// <summary>
        /// Opens the occupation choice panel for a captured territory.
        /// The player chooses between Direct Annexation and Puppet Government.
        /// </summary>
        /// <param name="territoryId">The captured territory to decide policy for.</param>
        public void ShowOccupationChoice(string territoryId)
        {
            if (occupationPanel == null)
            {
                Debug.LogWarning("[DiplomacyUI] Occupation panel not assigned.");
                return;
            }

            _occupationTerritoryId = territoryId;

            if (occupationTerritoryText != null)
                occupationTerritoryText.text = $"Territory: {territoryId}\nChoose occupation policy:";

            occupationPanel.SetActive(true);
            Debug.Log($"[DiplomacyUI] Occupation choice shown for: {territoryId}");
        }

        // ------------------------------------------------------------------
        // Public API - Peace & Alliance
        // ------------------------------------------------------------------

        /// <summary>
        /// Programmatic entry point for declaring war (e.g., from AI or script).
        /// </summary>
        /// <param name="target">Target nation identifier.</param>
        /// <param name="cbType">The Casus Belli type used.</param>
        public void OnDeclareWar(string target, IronProtocol.GameSystems.Diplomacy.CBType cbType)
        {
            Debug.Log($"[DiplomacyUI] War declared on {target} with CB: {cbType}");
            OnDeclareWar?.Invoke(target, cbType);
        }

        /// <summary>
        /// Programmatic entry point for making peace.
        /// </summary>
        /// <param name="target">Target nation identifier.</param>
        public void OnMakePeace(string target)
        {
            Debug.Log($"[DiplomacyUI] Peace requested with {target}");
            OnMakePeace?.Invoke(target);
        }

        /// <summary>
        /// Programmatic entry point for forming an alliance.
        /// </summary>
        /// <param name="target">Target nation identifier.</param>
        public void OnFormAlliance(string target)
        {
            Debug.Log($"[DiplomacyUI] Alliance proposed to {target}");
            OnFormAlliance?.Invoke(target);
        }

        // ------------------------------------------------------------------
        // Private Handlers
        // ------------------------------------------------------------------

        private void ShowWarOptions(string targetNationId)
        {
            // This will be populated by the game system's event handler
            // which calls ShowWarOptions(string, List<CasusBelli>) with the actual CB list.
            Debug.Log($"[DiplomacyUI] Requesting war options for: {targetNationId}");
            // The game controller should listen to a request event and respond
            // with the available CBs. For now, show an empty panel as a placeholder.
        }

        private void HandlePeaceRequest(string targetNationId)
        {
            ShowConfirmationDialog(
                "Propose Peace",
                $"Send a peace proposal to {targetNationId}?",
                () => OnMakePeace?.Invoke(targetNationId)
            );
        }

        private void HandleAllianceRequest(string targetNationId)
        {
            ShowConfirmationDialog(
                "Propose Alliance",
                $"Propose a military alliance with {targetNationId}?",
                () => OnFormAlliance?.Invoke(targetNationId)
            );
        }

        private void OnCBSelected(IronProtocol.GameSystems.Diplomacy.CBType cbType, float stabilityCost)
        {
            _selectedCB = cbType;
            if (declareWarConfirmButton != null)
                declareWarConfirmButton.interactable = true;

            Debug.Log($"[DiplomacyUI] Casus Belli selected: {cbType} (Cost: {stabilityCost:F0} stability)");
        }

        private void OnDeclareWarConfirmed()
        {
            if (string.IsNullOrEmpty(_warTargetNation))
            {
                Debug.LogWarning("[DiplomacyUI] No war target set.");
                return;
            }

            Debug.Log($"[DiplomacyUI] War confirmed on {_warTargetNation} with CB: {_selectedCB}");
            OnDeclareWar?.Invoke(_warTargetNation, _selectedCB);

            warDeclarationPanel.SetActive(false);
            _warTargetNation = null;
            _selectedCB = IronProtocol.GameSystems.Diplomacy.CBType.None;
        }

        private void OnDeclareWarCancelled()
        {
            warDeclarationPanel.SetActive(false);
            _warTargetNation = null;
            _selectedCB = IronProtocol.GameSystems.Diplomacy.CBType.None;
        }

        private void OnDirectAnnexClicked()
        {
            Debug.Log($"[DiplomacyUI] Direct Annexation chosen for: {_occupationTerritoryId}");
            OnDirectAnnex?.Invoke(_occupationTerritoryId);
            occupationPanel.SetActive(false);
            _occupationTerritoryId = null;
        }

        private void OnPuppetGovernmentClicked()
        {
            Debug.Log($"[DiplomacyUI] Puppet Government chosen for: {_occupationTerritoryId}");
            OnPuppetGovernment?.Invoke(_occupationTerritoryId);
            occupationPanel.SetActive(false);
            _occupationTerritoryId = null;
        }

        private void OnOccupationCancelled()
        {
            occupationPanel.SetActive(false);
            _occupationTerritoryId = null;
        }

        // ------------------------------------------------------------------
        // Confirmation Dialog
        // ------------------------------------------------------------------

        private Action _confirmCallback;

        /// <summary>
        /// Shows a generic yes/no confirmation dialog.
        /// </summary>
        private void ShowConfirmationDialog(string title, string message, Action onConfirmed)
        {
            if (confirmationDialog == null)
            {
                // Fall back to immediate execution if no dialog
                onConfirmed?.Invoke();
                return;
            }

            if (confirmationTitleText != null) confirmationTitleText.text = title;
            if (confirmationMessageText != null) confirmationMessageText.text = message;

            _confirmCallback = onConfirmed;
            confirmationDialog.SetActive(true);
        }

        private void OnConfirmYes()
        {
            _confirmCallback?.Invoke();
            _confirmCallback = null;
            confirmationDialog.SetActive(false);
        }

        private void OnConfirmNo()
        {
            _confirmCallback = null;
            confirmationDialog.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Cleanup
        // ------------------------------------------------------------------

        private void ClearRelationCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null)
                {
                    card.OnWarRequested -= ShowWarOptions;
                    card.OnPeaceRequested -= HandlePeaceRequest;
                    card.OnAllianceRequested -= HandleAllianceRequest;

                    if (card.gameObject != null)
                        Destroy(card.gameObject);
                }
            }
            _spawnedCards.Clear();
        }
    }
}
