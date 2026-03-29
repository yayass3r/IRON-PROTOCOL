// =============================================================================
// IRON PROTOCOL - Media & Propaganda System
// File: MediaSystem.cs
// Description: Comprehensive media landscape, propaganda campaign management,
//              and public opinion simulation for IRON PROTOCOL grand strategy.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IronProtocol.HexMap;

namespace IronProtocol.MediaPropaganda
{
    // =========================================================================
    // ENUMERATIONS
    // =========================================================================

    /// <summary>
    /// Classification of media outlets determining their default behavior,
    /// credibility modifiers, and susceptibility to propaganda.
    /// </summary>
    public enum MediaType
    {
        /// <summary>Government-controlled media. High domestic reach, low international credibility.</summary>
        StateMedia,

        /// <summary>Privately owned but potentially biased media. Moderate reach and credibility.</summary>
        Independent,

        /// <summary>User-generated platforms. Massive reach, low credibility, highly volatile.</summary>
        SocialMedia,

        /// <summary>Globally recognized wire services and broadcasters. High credibility, moderate reach.</summary>
        International
    }

    /// <summary>
    /// Types of propaganda actions that nations can launch to influence
    /// domestic and international public opinion.
    /// </summary>
    public enum PropagandaAction
    {
        /// <summary>Rally the populace around the flag. Increases war support and approval.</summary>
        BoostNationalism,

        /// <summary>Discredit a rival nation in domestic and international media.</summary>
        SmearEnemy,

        /// <summary>Encourage military enlistment and public support for the armed forces.</summary>
        MilitaryRecruitment,

        /// <summary>Project economic strength to maintain domestic and foreign confidence.</summary>
        EconomicConfidence,

        /// <summary>Promote national culture abroad to improve soft power and reputation.</summary>
        CulturalExport,

        /// <summary>Spread false information in enemy media ecosystems to sow confusion.</summary>
        Disinformation,

        /// <summary>Suppress dissenting narratives. Raises stability at a diplomatic cost.</summary>
        Censorship,

        /// <summary>Allow independent journalism. Lowers stability but raises reputation and intel.</summary>
        FreePress
    }

    // =========================================================================
    // DATA CLASSES
    // =========================================================================

    /// <summary>
    /// Represents a news outlet in the global media landscape. Each outlet has
    /// unique reach, credibility, and bias characteristics that determine its
    /// influence on public opinion.
    /// </summary>
    [Serializable]
    public class NewsOutlet
    {
        /// <summary>Unique identifier for this news outlet (e.g., "outlet_bbc_world").</summary>
        public string outletId { get; set; }

        /// <summary>Display name of the news outlet.</summary>
        public string name { get; set; }

        /// <summary>Classification determining default behavior patterns.</summary>
        public MediaType type { get; set; }

        /// <summary>The nation where this outlet is headquartered.</summary>
        public string nationId { get; set; }

        /// <summary>
        /// Percentage of global/national population reached by this outlet.
        /// Range: 0-100. Higher means wider audience.
        /// </summary>
        public float reach { get; set; }

        /// <summary>
        /// How trustworthy the public perceives this outlet to be.
        /// Range: 0-100. Affects how much opinion shifts from stories published here.
        /// </summary>
        public float credibility { get; set; }

        /// <summary>
        /// Directional bias toward the headquarters nation.
        /// Range: -100 (strongly anti) to +100 (strongly pro).
        /// Positive values mean the outlet tends to publish favorable stories about its nation.
        /// </summary>
        public float biasTowardNation { get; set; }

        /// <summary>
        /// Creates a new news outlet with all required fields.
        /// </summary>
        /// <param name="outletId">Unique outlet identifier.</param>
        /// <param name="name">Display name.</param>
        /// <param name="type">Media type classification.</param>
        /// <param name="nationId">Headquarters nation.</param>
        /// <param name="reach">Reach percentage (0-1 normalized, stored as 0-100).</param>
        /// <param name="credibility">Credibility percentage (0-1 normalized, stored as 0-100).</param>
        /// <param name="biasTowardNation">Bias toward headquarters nation (-100 to 100).</param>
        public NewsOutlet(string outletId, string name, MediaType type, string nationId,
            float reach, float credibility, float biasTowardNation)
        {
            this.outletId = outletId;
            this.name = name;
            this.type = type;
            this.nationId = nationId;
            this.reach = Mathf.Clamp01(reach) * 100f;
            this.credibility = Mathf.Clamp01(credibility) * 100f;
            this.biasTowardNation = Mathf.Clamp(biasTowardNation, -100f, 100f);
        }
    }

    /// <summary>
    /// An active or completed propaganda campaign launched by one nation
    /// against another (or domestically). Campaigns run for a set number of
    /// turns and degrade in effectiveness over time.
    /// </summary>
    [Serializable]
    public class PropagandaCampaign
    {
        /// <summary>Unique identifier for this campaign.</summary>
        public string campaignId { get; set; }

        /// <summary>The type of propaganda action being performed.</summary>
        public PropagandaAction action { get; set; }

        /// <summary>The nation sponsoring and funding this campaign.</summary>
        public string sponsorNation { get; set; }

        /// <summary>The nation being targeted (can be the same as sponsor for domestic campaigns).</summary>
        public string targetNation { get; set; }

        /// <summary>Budget allocated to this campaign in economic units.</summary>
        public float budget { get; set; }

        /// <summary>Current effectiveness rating (0-100), computed from budget, outlet reach, and decay.</summary>
        public float effectiveness { get; set; }

        /// <summary>Turns remaining before the campaign expires.</summary>
        public int turnsRemaining { get; set; }

        /// <summary>Total duration of the campaign in turns.</summary>
        public int totalTurns { get; set; }

        /// <summary>Whether the campaign is currently active and producing effects.</summary>
        public bool isActive { get; set; }

        /// <summary>
        /// Creates a new propaganda campaign.
        /// </summary>
        /// <param name="campaignId">Unique campaign identifier.</param>
        /// <param name="action">Propaganda action type.</param>
        /// <param name="sponsorNation">Sponsoring nation.</param>
        /// <param name="targetNation">Target nation.</param>
        /// <param name="budget">Budget allocation.</param>
        /// <param name="duration">Duration in turns.</param>
        public PropagandaCampaign(string campaignId, PropagandaAction action,
            string sponsorNation, string targetNation, float budget, int duration)
        {
            this.campaignId = campaignId;
            this.action = action;
            this.sponsorNation = sponsorNation;
            this.targetNation = targetNation;
            this.budget = budget;
            this.effectiveness = 0f;
            this.turnsRemaining = duration;
            this.totalTurns = duration;
            this.isActive = true;
        }
    }

    /// <summary>
    /// Snapshot of a nation's public sentiment across key opinion dimensions.
    /// Updated each turn based on in-game events, propaganda, and natural drift.
    /// </summary>
    [Serializable]
    public class PublicOpinion
    {
        /// <summary>The nation this opinion snapshot belongs to.</summary>
        public string nationId { get; set; }

        /// <summary>
        /// Public willingness to support ongoing wars.
        /// Range: 0 (total pacifism) to 100 (total war fervor).
        /// </summary>
        public float warSupport { get; set; }

        /// <summary>
        /// Public approval of the current government.
        /// Range: 0 (revolution imminent) to 100 (unwavering loyalty).
        /// </summary>
        public float governmentApproval { get; set; }

        /// <summary>
        /// How the public views enemy nations in general.
        /// Range: 0 (sympathetic) to 100 (hostile).
        /// </summary>
        public float enemyView { get; set; }

        /// <summary>
        /// How the international community perceives this nation.
        /// Range: 0 (pariah state) to 100 (global beacon).
        /// </summary>
        public float internationalReputation { get; set; }

        /// <summary>
        /// Granular opinions about specific other nations.
        /// Key: other nation ID, Value: opinion (0-100, 50 = neutral).
        /// </summary>
        public Dictionary<string, float> opinionsOfOtherNations { get; set; }

        /// <summary>Current bonus to unit production speed from military recruitment campaigns (percentage).</summary>
        public float recruitmentSpeedBonus { get; set; }

        /// <summary>Current bonus to treasury income from economic confidence campaigns.</summary>
        public float tradeIncomeBonus { get; set; }

        /// <summary>Turns remaining on military recruitment bonus.</summary>
        public int recruitmentBonusTurns { get; set; }

        /// <summary>
        /// Creates a default opinion snapshot for a nation with neutral starting values.
        /// </summary>
        /// <param name="nationId">Nation identifier.</param>
        public PublicOpinion(string nationId)
        {
            this.nationId = nationId;
            warSupport = 50f;
            governmentApproval = 60f;
            enemyView = 50f;
            internationalReputation = 50f;
            opinionsOfOtherNations = new Dictionary<string, float>();
            recruitmentSpeedBonus = 0f;
            tradeIncomeBonus = 0f;
            recruitmentBonusTurns = 0;
        }

        /// <summary>
        /// Clamps all opinion values to their valid ranges (0-100).
        /// </summary>
        public void ClampValues()
        {
            warSupport = Mathf.Clamp(warSupport, 0f, 100f);
            governmentApproval = Mathf.Clamp(governmentApproval, 0f, 100f);
            enemyView = Mathf.Clamp(enemyView, 0f, 100f);
            internationalReputation = Mathf.Clamp(internationalReputation, 0f, 100f);

            if (opinionsOfOtherNations != null)
            {
                var keys = opinionsOfOtherNations.Keys.ToList();
                foreach (var key in keys)
                {
                    opinionsOfOtherNations[key] = Mathf.Clamp(opinionsOfOtherNations[key], 0f, 100f);
                }
            }
        }
    }

    // =========================================================================
    // MEDIA SYSTEM (MonoBehaviour)
    // =========================================================================

    /// <summary>
    /// Core system managing the global media landscape, propaganda campaigns,
    /// and public opinion simulation. Attach to a persistent GameObject (e.g., GameManager).
    /// <para>
    /// The system tracks news outlets, runs propaganda campaigns, publishes stories,
    /// and updates public opinion every game turn based on active events and natural decay.
    /// </para>
    /// </summary>
    public class MediaSystem : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // EVENTS
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired when a news story is published. Arguments: (outletId, headline).
        /// </summary>
        public event Action<string, string> OnNewsPublished;

        /// <summary>
        /// Fired when a nation's public opinion shifts significantly.
        /// Arguments: (nationId, shiftMagnitude).
        /// </summary>
        public event Action<string, float> OnOpinionShifted;

        /// <summary>
        /// Fired when a new propaganda campaign is launched.
        /// Arguments: (campaignId, sponsorNation).
        /// </summary>
        public event Action<string, string> OnCampaignLaunched;

        /// <summary>
        /// Fired when a propaganda campaign expires or is cancelled.
        /// Arguments: (campaignId, reason).
        /// </summary>
        public event Action<string, string> OnCampaignEnded;

        /// <summary>
        /// Fired when a social media trend goes viral.
        /// Arguments: (trendDescription, impactMagnitude).
        /// </summary>
        public event Action<string, float> OnSocialMediaTrend;

        // -----------------------------------------------------------------
        // SERIALIZED FIELDS
        // -----------------------------------------------------------------

        [Header("System Settings")]
        [Tooltip("Natural opinion decay rate per turn toward 50 (neutral).")]
        [SerializeField] private float opinionDecayRate = 2f;

        [Tooltip("War casualty opinion penalty per 1% losses.")]
        [SerializeField] private float casualtyOpinionPenalty = 0.5f;

        [Tooltip("Economic growth opinion bonus per 1% GDP growth.")]
        [SerializeField] private float growthApprovalBonus = 0.8f;

        [Tooltip("Maximum number of concurrent propaganda campaigns per nation.")]
        [SerializeField] private int maxCampaignsPerNation = 3;

        [Tooltip("Base campaign effectiveness multiplier from budget.")]
        [SerializeField] private float budgetEffectivenessScale = 0.1f;

        [Tooltip("Effectiveness decay per turn as a campaign runs.")]
        [SerializeField] private float campaignDecayPerTurn = 5f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // -----------------------------------------------------------------
        // PRIVATE STATE
        // -----------------------------------------------------------------

        private Dictionary<string, NewsOutlet> _newsOutlets = new Dictionary<string, NewsOutlet>();
        private Dictionary<string, PropagandaCampaign> _activeCampaigns = new Dictionary<string, PropagandaCampaign>();
        private Dictionary<string, PublicOpinion> _publicOpinions = new Dictionary<string, PublicOpinion>();
        private Dictionary<string, int> _campaignCountByNation = new Dictionary<string, int>();
        private List<string> _newsHistory = new List<string>();
        private int _campaignIdCounter = 0;

        // -----------------------------------------------------------------
        // PUBLIC PROPERTIES
        // -----------------------------------------------------------------

        /// <summary>Number of currently active propaganda campaigns.</summary>
        public int ActiveCampaignCount => _activeCampaigns.Count;

        /// <summary>Number of registered news outlets.</summary>
        public int OutletCount => _newsOutlets.Count;

        /// <summary>Read-only access to all registered news outlets.</summary>
        public IReadOnlyDictionary<string, NewsOutlet> NewsOutlets => _newsOutlets;

        /// <summary>Read-only access to all active propaganda campaigns.</summary>
        public IReadOnlyDictionary<string, PropagandaCampaign> ActiveCampaigns => _activeCampaigns;

        /// <summary>Read-only access to all public opinion snapshots.</summary>
        public IReadOnlyDictionary<string, PublicOpinion> PublicOpinions => _publicOpinions;

        // =================================================================
        // INITIALIZATION
        // =================================================================

        /// <summary>
        /// Unity Awake. Initializes the default media landscape.
        /// </summary>
        private void Awake()
        {
            InitializeMedia();
        }

        /// <summary>
        /// Creates 8 default news outlets representing major global media
        /// organizations across different types and nations.
        /// <para>
        /// Outlets created:
        /// <list type="bullet">
        ///   <item>Global Times (StateMedia, China-like)</item>
        ///   <item>Al Jazeera (Independent)</item>
        ///   <item>BBC World (International)</item>
        ///   <item>CNN (Independent)</item>
        ///   <item>Twitter/X (SocialMedia)</item>
        ///   <item>Facebook (SocialMedia)</item>
        ///   <item>Reuters (International)</item>
        ///   <item>RT News (StateMedia)</item>
        /// </list>
        /// </para>
        /// </summary>
        public void InitializeMedia()
        {
            _newsOutlets.Clear();

            // 1. Global Times - State media, high reach domestically, strong pro-government bias
            RegisterOutlet(new NewsOutlet(
                outletId: "global_times",
                name: "Global Times",
                type: MediaType.StateMedia,
                nationId: "china",
                reach: 0.75f,
                credibility: 0.35f,
                biasTowardNation: 85f
            ));

            // 2. Al Jazeera - Independent, strong regional reach, moderate credibility
            RegisterOutlet(new NewsOutlet(
                outletId: "al_jazeera",
                name: "Al Jazeera",
                type: MediaType.Independent,
                nationId: "qatar",
                reach: 0.55f,
                credibility: 0.60f,
                biasTowardNation: 30f
            ));

            // 3. BBC World - International, high credibility, wide global reach
            RegisterOutlet(new NewsOutlet(
                outletId: "bbc_world",
                name: "BBC World",
                type: MediaType.International,
                nationId: "uk",
                reach: 0.70f,
                credibility: 0.85f,
                biasTowardNation: 20f
            ));

            // 4. CNN - Independent, strong US and international presence
            RegisterOutlet(new NewsOutlet(
                outletId: "cnn",
                name: "CNN",
                type: MediaType.Independent,
                nationId: "usa",
                reach: 0.65f,
                credibility: 0.65f,
                biasTowardNation: 40f
            ));

            // 5. Twitter/X - Social media, massive reach, very low credibility
            RegisterOutlet(new NewsOutlet(
                outletId: "twitter_x",
                name: "Twitter/X",
                type: MediaType.SocialMedia,
                nationId: "usa",
                reach: 0.95f,
                credibility: 0.20f,
                biasTowardNation: 0f
            ));

            // 6. Facebook - Social media, very high reach, low credibility
            RegisterOutlet(new NewsOutlet(
                outletId: "facebook",
                name: "Facebook",
                type: MediaType.SocialMedia,
                nationId: "usa",
                reach: 0.90f,
                credibility: 0.25f,
                biasTowardNation: 0f
            ));

            // 7. Reuters - International wire service, highest credibility
            RegisterOutlet(new NewsOutlet(
                outletId: "reuters",
                name: "Reuters",
                type: MediaType.International,
                nationId: "uk",
                reach: 0.60f,
                credibility: 0.95f,
                biasTowardNation: 5f
            ));

            // 8. RT News - State media, international reach but low credibility
            RegisterOutlet(new NewsOutlet(
                outletId: "rt_news",
                name: "RT News",
                type: MediaType.StateMedia,
                nationId: "russia",
                reach: 0.50f,
                credibility: 0.25f,
                biasTowardNation: 90f
            ));

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaSystem] Initialized {_newsOutlets.Count} news outlets.");
            }
        }

        /// <summary>
        /// Registers a news outlet into the system.
        /// </summary>
        /// <param name="outlet">The news outlet to register.</param>
        private void RegisterOutlet(NewsOutlet outlet)
        {
            if (outlet == null || string.IsNullOrEmpty(outlet.outletId))
            {
                Debug.LogWarning("[MediaSystem] Cannot register outlet with null or empty ID.");
                return;
            }

            _newsOutlets[outlet.outletId] = outlet;
        }

        // =================================================================
        // PUBLIC OPINION MANAGEMENT
        // =================================================================

        /// <summary>
        /// Retrieves the current public opinion snapshot for a nation.
        /// If no opinion exists, creates a new neutral one.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>The current <see cref="PublicOpinion"/> for the specified nation.</returns>
        public PublicOpinion GetPublicOpinion(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[MediaSystem] GetPublicOpinion called with null/empty nationId.");
                return null;
            }

            if (!_publicOpinions.ContainsKey(nationId))
            {
                _publicOpinions[nationId] = new PublicOpinion(nationId);
                if (enableDebugLogs)
                {
                    Debug.Log($"[MediaSystem] Created new PublicOpinion for nation '{nationId}'.");
                }
            }

            return _publicOpinions[nationId];
        }

        /// <summary>
        /// Updates public opinion for a specific nation each turn.
        /// <para>
        /// Processing order:
        /// <list type="number">
        ///   <item>War casualties reduce war support proportionally.</item>
        ///   <item>Economic growth improves government approval.</item>
        ///   <item>Active propaganda campaigns shift relevant opinion values.</item>
        ///   <item>Natural decay pulls all opinions toward 50 (neutral).</item>
        ///   <item>Recruitment and trade bonuses tick down.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="nationId">The nation whose opinion to update.</param>
        /// <param name="warCasualtiesPercent">Optional casualty rate (0-100) for this turn.</param>
        /// <param name="economicGrowthPercent">Optional GDP growth rate for this turn.</param>
        public void UpdatePublicOpinion(string nationId, float warCasualtiesPercent = 0f, float economicGrowthPercent = 0f)
        {
            var opinion = GetPublicOpinion(nationId);
            if (opinion == null) return;

            float totalShift = 0f;

            // --- War casualties reduce war support ---
            if (warCasualtiesPercent > 0f)
            {
                float casualtyPenalty = warCasualtiesPercent * casualtyOpinionPenalty;
                opinion.warSupport -= casualtyPenalty;
                opinion.governmentApproval -= casualtyPenalty * 0.5f;
                totalShift -= casualtyPenalty;

                if (enableDebugLogs)
                {
                    Debug.Log($"[MediaSystem] '{nationId}' casualties ({warCasualtiesPercent:F1}%) " +
                              $"reduce war support by {casualtyPenalty:F1}.");
                }
            }

            // --- Economic growth improves approval ---
            if (economicGrowthPercent > 0f)
            {
                float growthBonus = economicGrowthPercent * growthApprovalBonus;
                opinion.governmentApproval += growthBonus;
                opinion.warSupport += growthBonus * 0.3f;
                totalShift += growthBonus;

                if (enableDebugLogs)
                {
                    Debug.Log($"[MediaSystem] '{nationId}' growth ({economicGrowthPercent:F1}%) " +
                              $"improves approval by {growthBonus:F1}.");
                }
            }
            else if (economicGrowthPercent < 0f)
            {
                // Negative growth also hurts
                float growthPenalty = Mathf.Abs(economicGrowthPercent) * growthApprovalBonus * 1.5f;
                opinion.governmentApproval -= growthPenalty;
                totalShift -= growthPenalty;
            }

            // --- Propaganda campaigns shift opinion ---
            foreach (var kvp in _activeCampaigns)
            {
                var campaign = kvp.Value;
                if (!campaign.isActive) continue;

                float effect = campaign.effectiveness * 0.1f;

                if (campaign.action == PropagandaAction.BoostNationalism && campaign.targetNation == nationId)
                {
                    opinion.warSupport += effect;
                    opinion.governmentApproval += effect * 0.7f;
                }
                else if (campaign.action == PropagandaAction.SmearEnemy && campaign.sponsorNation == nationId)
                {
                    if (opinion.opinionsOfOtherNations.ContainsKey(campaign.targetNation))
                    {
                        opinion.opinionsOfOtherNations[campaign.targetNation] -= effect;
                    }
                }
                else if (campaign.action == PropagandaAction.CulturalExport && campaign.sponsorNation == nationId)
                {
                    opinion.internationalReputation += effect;
                }
                else if (campaign.action == PropagandaAction.Censorship && campaign.targetNation == nationId)
                {
                    opinion.internationalReputation -= effect;
                }
                else if (campaign.action == PropagandaAction.FreePress && campaign.targetNation == nationId)
                {
                    opinion.internationalReputation += effect;
                }
                else if (campaign.action == PropagandaAction.EconomicConfidence && campaign.sponsorNation == nationId)
                {
                    opinion.governmentApproval += effect * 0.5f;
                }
                else if (campaign.action == PropagandaAction.Disinformation && campaign.targetNation == nationId)
                {
                    opinion.governmentApproval -= effect * 0.3f;
                }
            }

            // --- Natural decay toward 50 (neutral) ---
            opinion.warSupport = Mathf.MoveTowards(opinion.warSupport, 50f, opinionDecayRate);
            opinion.governmentApproval = Mathf.MoveTowards(opinion.governmentApproval, 50f, opinionDecayRate);
            opinion.enemyView = Mathf.MoveTowards(opinion.enemyView, 50f, opinionDecayRate);
            opinion.internationalReputation = Mathf.MoveTowards(opinion.internationalReputation, 50f, opinionDecayRate * 0.5f);

            // --- Tick down temporary bonuses ---
            if (opinion.recruitmentBonusTurns > 0)
            {
                opinion.recruitmentBonusTurns--;
                if (opinion.recruitmentBonusTurns <= 0)
                {
                    opinion.recruitmentSpeedBonus = 0f;
                }
            }

            // --- Clamp all values ---
            opinion.ClampValues();

            // --- Fire opinion shift event ---
            if (Mathf.Abs(totalShift) > 1f)
            {
                OnOpinionShifted?.Invoke(nationId, totalShift);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaSystem] Updated opinion for '{nationId}': " +
                          $"WarSupport={opinion.warSupport:F1}, Approval={opinion.governmentApproval:F1}, " +
                          $"Rep={opinion.internationalReputation:F1}");
            }
        }

        /// <summary>
        /// Updates public opinion for ALL tracked nations.
        /// </summary>
        /// <param name="warCasualtiesPercent">Optional casualty rate for all nations.</param>
        /// <param name="economicGrowthPercent">Optional GDP growth rate for all nations.</param>
        public void UpdateAllPublicOpinions(float warCasualtiesPercent = 0f, float economicGrowthPercent = 0f)
        {
            foreach (var nationId in _publicOpinions.Keys.ToList())
            {
                UpdatePublicOpinion(nationId, warCasualtiesPercent, economicGrowthPercent);
            }
        }

        // =================================================================
        // PROPAGANDA CAMPAIGNS
        // =================================================================

        /// <summary>
        /// Launches a new propaganda campaign. The campaign runs for a duration
        /// determined by the action type, with effectiveness based on the budget
        /// and the sponsoring nation's media reach.
        /// <para>
        /// Action effects:
        /// <list type="table">
        ///   <listheader><term>Action</term><description>Effect</description></listheader>
        ///   <item><term>BoostNationalism</term><description>+15 war support, +10 approval</description></item>
        ///   <item><term>SmearEnemy</term><description>-20 opinion of target in domestic media</description></item>
        ///   <item><term>MilitaryRecruitment</term><description>+20% unit production for 3 turns</description></item>
        ///   <item><term>EconomicConfidence</term><description>+10 treasury income from trade</description></item>
        ///   <item><term>CulturalExport</term><description>+5 international reputation per turn</description></item>
        ///   <item><term>Disinformation</term><description>-30 credibility of independent media in target</description></item>
        ///   <item><term>Censorship</term><description>+10 stability, -15 international reputation</description></item>
        ///   <item><term>FreePress</term><description>-5 stability, +15 international reputation, better intel</description></item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="sponsor">Nation ID launching the campaign.</param>
        /// <param name="action">Type of propaganda action.</param>
        /// <param name="target">Nation ID being targeted (use sponsor ID for domestic campaigns).</param>
        /// <param name="budget">Budget allocated in economic units.</param>
        /// <returns>The newly created <see cref="PropagandaCampaign"/>, or null if launch failed.</returns>
        public PropagandaCampaign LaunchCampaign(string sponsor, PropagandaAction action, string target, float budget)
        {
            if (string.IsNullOrEmpty(sponsor) || string.IsNullOrEmpty(target))
            {
                Debug.LogWarning("[MediaSystem] LaunchCampaign: sponsor and target nation IDs required.");
                return null;
            }

            if (budget <= 0f)
            {
                Debug.LogWarning("[MediaSystem] LaunchCampaign: budget must be positive.");
                return null;
            }

            // Enforce max campaigns per nation
            int currentCount = GetCampaignCountForNation(sponsor);
            if (currentCount >= maxCampaignsPerNation)
            {
                Debug.LogWarning($"[MediaSystem] '{sponsor}' already has {currentCount} active campaigns " +
                                $"(max {maxCampaignsPerNation}). Cannot launch new campaign.");
                return null;
            }

            // Determine campaign duration based on action type
            int duration = GetCampaignDuration(action);

            string campaignId = $"camp_{++_campaignIdCounter}_{sponsor}_{action}";
            var campaign = new PropagandaCampaign(campaignId, action, sponsor, target, budget, duration);

            // Calculate initial effectiveness
            campaign.effectiveness = CalculateCampaignEffectiveness(campaign);

            _activeCampaigns[campaignId] = campaign;
            IncrementCampaignCount(sponsor);

            // Apply immediate effects based on action type
            ApplyCampaignImmediateEffects(campaign);

            OnCampaignLaunched?.Invoke(campaignId, sponsor);

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaSystem] Campaign '{campaignId}' launched by '{sponsor}' " +
                          $"(action={action}, target={target}, budget={budget:F0}, " +
                          $"effectiveness={campaign.effectiveness:F1}, duration={duration} turns).");
            }

            return campaign;
        }

        /// <summary>
        /// Returns the appropriate duration in turns for a given propaganda action.
        /// </summary>
        /// <param name="action">The propaganda action type.</param>
        /// <returns>Duration in turns.</returns>
        private int GetCampaignDuration(PropagandaAction action)
        {
            switch (action)
            {
                case PropagandaAction.BoostNationalism: return 5;
                case PropagandaAction.SmearEnemy: return 4;
                case PropagandaAction.MilitaryRecruitment: return 3;
                case PropagandaAction.EconomicConfidence: return 6;
                case PropagandaAction.CulturalExport: return 8;
                case PropagandaAction.Disinformation: return 5;
                case PropagandaAction.Censorship: return 10;
                case PropagandaAction.FreePress: return 10;
                default: return 5;
            }
        }

        /// <summary>
        /// Applies the immediate (first-turn) effects of a campaign based on its action type.
        /// </summary>
        /// <param name="campaign">The campaign to apply effects for.</param>
        private void ApplyCampaignImmediateEffects(PropagandaCampaign campaign)
        {
            var sponsorOpinion = GetPublicOpinion(campaign.sponsorNation);
            var targetOpinion = GetPublicOpinion(campaign.targetNation);

            switch (campaign.action)
            {
                case PropagandaAction.BoostNationalism:
                    sponsorOpinion.warSupport += 15f;
                    sponsorOpinion.governmentApproval += 10f;
                    break;

                case PropagandaAction.SmearEnemy:
                    if (campaign.targetNation != campaign.sponsorNation)
                    {
                        sponsorOpinion.opinionsOfOtherNations.TryGetValue(campaign.targetNation, out float currentOpinion);
                        sponsorOpinion.opinionsOfOtherNations[campaign.targetNation] = currentOpinion - 20f;

                        // Also reduce target's reputation
                        targetOpinion.internationalReputation -= 5f;
                    }
                    break;

                case PropagandaAction.MilitaryRecruitment:
                    sponsorOpinion.recruitmentSpeedBonus = 20f;
                    sponsorOpinion.recruitmentBonusTurns = 3;
                    sponsorOpinion.warSupport += 5f;
                    break;

                case PropagandaAction.EconomicConfidence:
                    sponsorOpinion.tradeIncomeBonus += 10f;
                    sponsorOpinion.governmentApproval += 5f;
                    break;

                case PropagandaAction.CulturalExport:
                    sponsorOpinion.internationalReputation += 5f;
                    break;

                case PropagandaAction.Disinformation:
                    // Reduce credibility of independent and international media in target nation
                    foreach (var outlet in _newsOutlets.Values)
                    {
                        if (outlet.nationId == campaign.targetNation &&
                            (outlet.type == MediaType.Independent || outlet.type == MediaType.International))
                        {
                            outlet.credibility = Mathf.Max(0f, outlet.credibility - 30f);
                        }
                    }
                    targetOpinion.governmentApproval -= 3f;
                    break;

                case PropagandaAction.Censorship:
                    sponsorOpinion.governmentApproval += 5f;
                    sponsorOpinion.internationalReputation -= 15f;
                    break;

                case PropagandaAction.FreePress:
                    sponsorOpinion.internationalReputation += 15f;
                    sponsorOpinion.governmentApproval -= 5f;
                    break;
            }

            // Clamp all affected opinions
            sponsorOpinion.ClampValues();
            if (targetOpinion != sponsorOpinion)
            {
                targetOpinion.ClampValues();
            }
        }

        /// <summary>
        /// Calculates the effectiveness rating of a propaganda campaign based on
        /// budget, sponsoring nation's media reach, outlet credibility, and decay.
        /// <para>
        /// Formula: effectiveness = (budget * budgetEffectivenessScale) * (mediaReach / 100) * (avgCredibility / 100) * decayFactor
        /// </para>
        /// </summary>
        /// <param name="campaign">The campaign to evaluate.</param>
        /// <returns>Effectiveness value from 0 to 100.</returns>
        public float CalculateCampaignEffectiveness(PropagandaCampaign campaign)
        {
            if (campaign == null) return 0f;

            // Budget contribution (capped)
            float budgetFactor = Mathf.Min(campaign.budget * budgetEffectivenessScale, 50f);

            // Media reach of sponsoring nation's outlets
            float sponsorReach = GetMediaInfluence(campaign.sponsorNation);
            float reachFactor = sponsorReach / 100f;

            // Average credibility of relevant outlets
            float avgCredibility = GetAverageOutletCredibility(campaign.sponsorNation);
            float credibilityFactor = avgCredibility / 100f;

            // Decay based on turns elapsed
            float turnsElapsed = campaign.totalTurns - campaign.turnsRemaining;
            float decayFactor = Mathf.Max(0f, 1f - (turnsElapsed * campaignDecayPerTurn / 100f));

            float effectiveness = budgetFactor * reachFactor * credibilityFactor * decayFactor;
            return Mathf.Clamp(effectiveness, 0f, 100f);
        }

        /// <summary>
        /// Returns the average credibility of all outlets headquartered in a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Average credibility (0-100).</returns>
        private float GetAverageOutletCredibility(string nationId)
        {
            var nationOutlets = _newsOutlets.Values
                .Where(o => o.nationId == nationId)
                .ToList();

            if (nationOutlets.Count == 0) return 50f;

            return nationOutlets.Average(o => o.credibility);
        }

        /// <summary>
        /// Updates all active campaigns each turn. Reduces remaining turns,
        /// recalculates effectiveness, and removes expired campaigns.
        /// </summary>
        public void UpdateCampaigns()
        {
            var expiredCampaigns = new List<string>();

            foreach (var kvp in _activeCampaigns)
            {
                var campaign = kvp.Value;
                if (!campaign.isActive) continue;

                // Decrement turns
                campaign.turnsRemaining--;

                // Recalculate effectiveness with decay
                campaign.effectiveness = CalculateCampaignEffectiveness(campaign);

                // Apply per-turn effects for ongoing campaigns
                ApplyCampaignPerTurnEffects(campaign);

                // Check expiration
                if (campaign.turnsRemaining <= 0)
                {
                    campaign.isActive = false;
                    expiredCampaigns.Add(kvp.Key);

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[MediaSystem] Campaign '{kvp.Key}' expired.");
                    }
                }
            }

            // Remove expired campaigns
            foreach (var campaignId in expiredCampaigns)
            {
                var campaign = _activeCampaigns[campaignId];
                DecrementCampaignCount(campaign.sponsorNation);
                _activeCampaigns.Remove(campaignId);
                OnCampaignEnded?.Invoke(campaignId, "expired");
            }
        }

        /// <summary>
        /// Applies per-turn ongoing effects for active campaigns.
        /// </summary>
        /// <param name="campaign">The campaign to process.</param>
        private void ApplyCampaignPerTurnEffects(PropagandaCampaign campaign)
        {
            float scaledEffect = campaign.effectiveness * 0.05f;

            switch (campaign.action)
            {
                case PropagandaAction.CulturalExport:
                    var sponsorOpinion = GetPublicOpinion(campaign.sponsorNation);
                    sponsorOpinion.internationalReputation += 5f;
                    sponsorOpinion.ClampValues();
                    break;

                case PropagandaAction.EconomicConfidence:
                    var econOpinion = GetPublicOpinion(campaign.sponsorNation);
                    econOpinion.tradeIncomeBonus = 10f; // Maintain the bonus
                    break;

                case PropagandaAction.Censorship:
                    var censorOpinion = GetPublicOpinion(campaign.sponsorNation);
                    censorOpinion.internationalReputation -= scaledEffect;
                    censorOpinion.ClampValues();
                    break;

                case PropagandaAction.FreePress:
                    var freeOpinion = GetPublicOpinion(campaign.sponsorNation);
                    freeOpinion.internationalReputation += scaledEffect;
                    freeOpinion.ClampValues();
                    break;
            }
        }

        /// <summary>
        /// Cancels an active campaign before its natural expiration.
        /// </summary>
        /// <param name="campaignId">The campaign to cancel.</param>
        public void CancelCampaign(string campaignId)
        {
            if (string.IsNullOrEmpty(campaignId) || !_activeCampaigns.ContainsKey(campaignId))
            {
                Debug.LogWarning("[MediaSystem] CancelCampaign: campaign not found.");
                return;
            }

            var campaign = _activeCampaigns[campaignId];
            campaign.isActive = false;
            DecrementCampaignCount(campaign.sponsorNation);
            _activeCampaigns.Remove(campaignId);
            OnCampaignEnded?.Invoke(campaignId, "cancelled");

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaSystem] Campaign '{campaignId}' cancelled.");
            }
        }

        // =================================================================
        // NEWS PUBLISHING
        // =================================================================

        /// <summary>
        /// Publishes a news story through a specific outlet. The story affects
        /// public opinion based on its sentiment and the outlet's reach/credibility.
        /// <para>
        /// Positive story: +5 approval and reputation for the subject nation.
        /// Negative story: -5 approval for the subject, potential casus belli generation.
        /// </para>
        /// </summary>
        /// <param name="outletId">The outlet publishing the story.</param>
        /// <param name="headline">The story headline.</param>
        /// <param name="content">The story content/body.</param>
        /// <param name="aboutNation">The nation the story is about.</param>
        /// <param name="isPositive">Whether the story is positive (true) or negative (false). Defaults to true.</param>
        public void PublishNewsStory(string outletId, string headline, string content, string aboutNation, bool isPositive = true)
        {
            if (string.IsNullOrEmpty(outletId) || !_newsOutlets.ContainsKey(outletId))
            {
                Debug.LogWarning($"[MediaSystem] PublishNewsStory: outlet '{outletId}' not found.");
                return;
            }

            if (string.IsNullOrEmpty(aboutNation))
            {
                Debug.LogWarning("[MediaSystem] PublishNewsStory: aboutNation is required.");
                return;
            }

            var outlet = _newsOutlets[outletId];
            var subjectOpinion = GetPublicOpinion(aboutNation);

            // Calculate impact based on outlet reach and credibility
            float reachWeight = outlet.reach / 100f;
            float credibilityWeight = outlet.credibility / 100f;
            float impactMultiplier = reachWeight * credibilityWeight;

            float approvalShift = 0f;
            float reputationShift = 0f;

            if (isPositive)
            {
                approvalShift = 5f * impactMultiplier;
                reputationShift = 5f * impactMultiplier;

                subjectOpinion.governmentApproval += approvalShift;
                subjectOpinion.internationalReputation += reputationShift;

                // Improve opinion of subject in outlet's home nation
                var outletNationOpinion = GetPublicOpinion(outlet.nationId);
                if (outletNationOpinion.opinionsOfOtherNations.ContainsKey(aboutNation))
                {
                    outletNationOpinion.opinionsOfOtherNations[aboutNation] += 3f * impactMultiplier;
                }
                else if (outlet.nationId != aboutNation)
                {
                    outletNationOpinion.opinionsOfOtherNations[aboutNation] = 53f;
                }
            }
            else
            {
                approvalShift = -5f * impactMultiplier;
                reputationShift = -3f * impactMultiplier;

                subjectOpinion.governmentApproval += approvalShift;
                subjectOpinion.internationalReputation += reputationShift;

                // Worsen opinion of subject in outlet's home nation
                var outletNationOpinion = GetPublicOpinion(outlet.nationId);
                if (outletNationOpinion.opinionsOfOtherNations.ContainsKey(aboutNation))
                {
                    outletNationOpinion.opinionsOfOtherNations[aboutNation] -= 5f * impactMultiplier;
                }
            }

            // Clamp values
            subjectOpinion.ClampValues();

            // Store in news history
            string storyEntry = $"[{outlet.name}] {headline}";
            _newsHistory.Add(storyEntry);
            if (_newsHistory.Count > 100)
            {
                _newsHistory.RemoveAt(0);
            }

            // Fire events
            OnNewsPublished?.Invoke(outletId, headline);
            OnOpinionShifted?.Invoke(aboutNation, approvalShift);

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaSystem] Published '{headline}' via '{outlet.name}' " +
                          $"(positive={isPositive}, impact={impactMultiplier:F2}, " +
                          $"approval shift={approvalShift:F1}, rep shift={reputationShift:F1}).");
            }
        }

        // =================================================================
        // MEDIA INFLUENCE CALCULATIONS
        // =================================================================

        /// <summary>
        /// Calculates the total media influence a nation holds, expressed as
        /// the combined reach of all outlets headquartered in that nation.
        /// <para>
        /// Also factors in any active propaganda campaigns boosting reach.
        /// </para>
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <returns>Total media influence score (0-100+).</returns>
        public float GetMediaInfluence(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return 0f;

            float totalReach = 0f;

            foreach (var outlet in _newsOutlets.Values)
            {
                if (outlet.nationId == nationId)
                {
                    totalReach += outlet.reach;
                }
            }

            // Bonus from active propaganda campaigns
            foreach (var campaign in _activeCampaigns.Values)
            {
                if (campaign.sponsorNation == nationId && campaign.isActive)
                {
                    if (campaign.action == PropagandaAction.BoostNationalism ||
                        campaign.action == PropagandaAction.CulturalExport ||
                        campaign.action == PropagandaAction.Disinformation)
                    {
                        totalReach += campaign.effectiveness * 0.1f;
                    }
                }
            }

            return Mathf.Clamp(totalReach, 0f, 100f);
        }

        /// <summary>
        /// Applies a social media trend that affects public opinion across nations.
        /// <para>
        /// Viral trends propagate through social media outlets, affecting
        /// war support, government approval, or international reputation
        /// depending on the trend's nature.
        /// </para>
        /// </summary>
        /// <param name="trend">Description of the viral trend.</param>
        /// <param name="impact">Magnitude of the trend's effect (-30 to +30).</param>
        public void ApplySocialMediaTrend(string trend, float impact)
        {
            if (string.IsNullOrEmpty(trend))
            {
                Debug.LogWarning("[MediaSystem] ApplySocialMediaTrend: trend description required.");
                return;
            }

            impact = Mathf.Clamp(impact, -30f, 30f);

            // Find social media outlets and calculate their combined reach
            float socialMediaReach = _newsOutlets.Values
                .Where(o => o.type == MediaType.SocialMedia)
                .Sum(o => o.reach);

            float scaledImpact = impact * (socialMediaReach / 100f);

            // Apply to all tracked nations proportionally
            foreach (var kvp in _publicOpinions)
            {
                var opinion = kvp.Value;

                // Trends generally affect government approval and international reputation
                opinion.governmentApproval += scaledImpact * 0.6f;
                opinion.internationalReputation += scaledImpact * 0.4f;

                opinion.ClampValues();
            }

            OnSocialMediaTrend?.Invoke(trend, scaledImpact);

            if (enableDebugLogs)
            {
                Debug.Log($"[MediaSystem] Social media trend: '{trend}' " +
                          $"(raw impact={impact:F1}, scaled={scaledImpact:F1}).");
            }
        }

        // =================================================================
        // UTILITY METHODS
        // =================================================================

        /// <summary>
        /// Returns all campaigns sponsored by a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of active campaigns.</returns>
        public List<PropagandaCampaign> GetCampaignsByNation(string nationId)
        {
            return _activeCampaigns.Values
                .Where(c => c.sponsorNation == nationId && c.isActive)
                .ToList();
        }

        /// <summary>
        /// Returns all campaigns targeting a specific nation.
        /// </summary>
        /// <param name="nationId">The target nation.</param>
        /// <returns>List of active campaigns targeting this nation.</returns>
        public List<PropagandaCampaign> GetCampaignsTargetingNation(string nationId)
        {
            return _activeCampaigns.Values
                .Where(c => c.targetNation == nationId && c.isActive)
                .ToList();
        }

        /// <summary>
        /// Returns the news history (last 100 stories published).
        /// </summary>
        /// <returns>List of formatted news story strings.</returns>
        public List<string> GetNewsHistory()
        {
            return new List<string>(_newsHistory);
        }

        /// <summary>
        /// Gets a specific news outlet by ID.
        /// </summary>
        /// <param name="outletId">The outlet identifier.</param>
        /// <returns>The <see cref="NewsOutlet"/>, or null if not found.</returns>
        public NewsOutlet GetOutlet(string outletId)
        {
            _newsOutlets.TryGetValue(outletId, out var outlet);
            return outlet;
        }

        /// <summary>
        /// Gets all news outlets of a specific type.
        /// </summary>
        /// <param name="type">The media type to filter by.</param>
        /// <returns>List of matching outlets.</returns>
        public List<NewsOutlet> GetOutletsByType(MediaType type)
        {
            return _newsOutlets.Values.Where(o => o.type == type).ToList();
        }

        /// <summary>
        /// Gets all news outlets headquartered in a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to filter by.</param>
        /// <returns>List of matching outlets.</returns>
        public List<NewsOutlet> GetOutletsByNation(string nationId)
        {
            return _newsOutlets.Values.Where(o => o.nationId == nationId).ToList();
        }

        /// <summary>
        /// Resets all public opinions to neutral baseline (50 for all values).
        /// </summary>
        public void ResetAllOpinions()
        {
            foreach (var opinion in _publicOpinions.Values)
            {
                opinion.warSupport = 50f;
                opinion.governmentApproval = 50f;
                opinion.enemyView = 50f;
                opinion.internationalReputation = 50f;
                opinion.recruitmentSpeedBonus = 0f;
                opinion.tradeIncomeBonus = 0f;
                opinion.recruitmentBonusTurns = 0;
            }
        }

        // -----------------------------------------------------------------
        // PRIVATE HELPERS
        // -----------------------------------------------------------------

        /// <summary>
        /// Gets the number of active campaigns for a nation from the tracking dictionary.
        /// </summary>
        private int GetCampaignCountForNation(string nationId)
        {
            _campaignCountByNation.TryGetValue(nationId, out int count);
            return count;
        }

        /// <summary>
        /// Increments the campaign count for a nation.
        /// </summary>
        private void IncrementCampaignCount(string nationId)
        {
            _campaignCountByNation.TryGetValue(nationId, out int count);
            _campaignCountByNation[nationId] = count + 1;
        }

        /// <summary>
        /// Decrements the campaign count for a nation.
        /// </summary>
        private void DecrementCampaignCount(string nationId)
        {
            _campaignCountByNation.TryGetValue(nationId, out int count);
            _campaignCountByNation[nationId] = Mathf.Max(0, count - 1);
        }

        /// <summary>
        /// Unity lifecycle: called every frame. Reserved for future real-time updates.
        /// </summary>
        private void Update()
        {
            // Reserved for real-time media simulation, UI updates, etc.
        }
    }
}
