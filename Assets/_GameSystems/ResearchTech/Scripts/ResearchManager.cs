// =====================================================================================
// Iron Protocol - Research Point Manager
// =====================================================================================
// Manages the generation, tracking, and spending of research points for all nations.
// Research points are calculated based on cities, universities, and population,
// and are spent on technology tree advancement each turn.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

using IronProtocol.HexMap;

namespace IronProtocol.ResearchTech
{
    /// <summary>
    /// Manages research point generation, tracking, and spending for all nations.
    /// Works in conjunction with <see cref="TechTree"/> to fund technology research.
    /// Research income is calculated each turn based on cities, universities,
    /// population, and building investments.
    /// </summary>
    public class ResearchManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────────
        //  Configuration Constants
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Research Income Configuration")]
        [Tooltip("Base research points generated per city per turn.")]
        [SerializeField]
        private int baseResearchPerCity = 10;

        [Tooltip("Research points added per 100,000 population in a city.")]
        [SerializeField]
        private int researchPerHundredKPopulation = 5;

        [Tooltip("Research points added per university level per turn.")]
        [SerializeField]
        private int researchPerUniversityLevel = 15;

        [Tooltip("Research points added per research lab per turn.")]
        [SerializeField]
        private int researchPerLab = 20;

        [Tooltip("Flat research bonus applied to all nations per turn (e.g., from global tech).")]
        [SerializeField]
        private int globalResearchBonus = 0;

        [Header("Caps & Limits")]
        [Tooltip("Maximum stored research points per nation. 0 = unlimited.")]
        [SerializeField]
        private int maxResearchPointsPerNation = 0;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Private State
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Per-nation current research point balance.</summary>
        private Dictionary<string, int> _nationResearchPoints;

        /// <summary>Per-nation custom research income modifiers (from effects, events, etc.).</summary>
        private Dictionary<string, int> _nationIncomeModifiers;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when a nation's research points change.
        /// Parameters: (nationId, newBalance, delta).
        /// </summary>
        public event Action<string, int, int> OnResearchPointsChanged;

        /// <summary>
        /// Fired when research income is calculated for a turn.
        /// Parameters: (nationId, totalIncome).
        /// </summary>
        public event Action<string, int> OnResearchIncomeCalculated;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _nationResearchPoints = new Dictionary<string, int>();
            _nationIncomeModifiers = new Dictionary<string, int>();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Point Management
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the current research point balance for a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Current research point balance, or 0 if nation not registered.</returns>
        public int GetResearchPoints(string nationId)
        {
            _nationResearchPoints.TryGetValue(nationId, out var points);
            return points;
        }

        /// <summary>
        /// Adds research points to a nation's balance, respecting the cap.
        /// </summary>
        /// <param name="nationId">The nation receiving points.</param>
        /// <param name="amount">Number of points to add (must be positive).</param>
        public void AddResearchPoints(string nationId, int amount)
        {
            if (string.IsNullOrEmpty(nationId) || amount <= 0) return;

            EnsureNationRegistered(nationId);

            var current = _nationResearchPoints[nationId];
            var newBalance = current + amount;

            // Apply cap if configured
            if (maxResearchPointsPerNation > 0)
                newBalance = Mathf.Min(newBalance, maxResearchPointsPerNation);

            var delta = newBalance - current;
            _nationResearchPoints[nationId] = newBalance;

            OnResearchPointsChanged?.Invoke(nationId, newBalance, delta);
            Debug.Log($"[ResearchManager] Added {delta} research points to '{nationId}'. Balance: {newBalance}");
        }

        /// <summary>
        /// Spends research points from a nation's balance. Fails silently if insufficient funds.
        /// </summary>
        /// <param name="nationId">The nation spending points.</param>
        /// <param name="amount">Number of points to spend (must be positive).</param>
        /// <returns><c>true</c> if the spend was successful (sufficient balance).</returns>
        public bool SpendResearchPoints(string nationId, int amount)
        {
            if (string.IsNullOrEmpty(nationId) || amount <= 0) return false;

            if (!_nationResearchPoints.TryGetValue(nationId, out var current))
            {
                Debug.LogWarning($"[ResearchManager] Nation '{nationId}' is not registered.");
                return false;
            }

            if (current < amount)
            {
                Debug.LogWarning($"[ResearchManager] Nation '{nationId}' has insufficient research points ({current}/{amount}).");
                return false;
            }

            var newBalance = current - amount;
            _nationResearchPoints[nationId] = newBalance;

            OnResearchPointsChanged?.Invoke(nationId, newBalance, -amount);
            Debug.Log($"[ResearchManager] Spent {amount} research points from '{nationId}'. Balance: {newBalance}");
            return true;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Income Calculation
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates the total research income for a nation based on its owned cities,
        /// universities, population, and any custom modifiers.
        /// </summary>
        /// <param name="nationId">The nation to calculate income for.</param>
        /// <param name="cities">List of all hex cells that may contain cities owned by this nation.</param>
        /// <returns>Total research points generated per turn for this nation.</returns>
        public int CalculateResearchIncome(string nationId, List<HexCell> cities)
        {
            if (string.IsNullOrEmpty(nationId) || cities == null)
                return 0;

            int totalIncome = 0;

            foreach (var cell in cities)
            {
                // Only count cells owned by this nation
                if (cell.ownerNationId != nationId) continue;

                // Base income from cities
                if (cell.hasCity)
                {
                    totalIncome += baseResearchPerCity;

                    // Population bonus: every 100k population adds research
                    if (cell.population > 0)
                    {
                        int populationBonus = (cell.population / 100000) * researchPerHundredKPopulation;
                        totalIncome += Mathf.Max(populationBonus, 0);
                    }
                }

                // University bonus
                if (cell.hasUniversity && cell.universityLevel > 0)
                {
                    totalIncome += cell.universityLevel * researchPerUniversityLevel;
                }

                // Research lab bonus
                if (cell.hasResearchLab)
                {
                    totalIncome += researchPerLab;
                }
            }

            // Apply nation-specific income modifiers
            if (_nationIncomeModifiers.TryGetValue(nationId, out var modifier))
            {
                totalIncome += modifier;
            }

            // Apply global bonus
            totalIncome += globalResearchBonus;

            // Ensure non-negative
            totalIncome = Mathf.Max(0, totalIncome);

            OnResearchIncomeCalculated?.Invoke(nationId, totalIncome);
            return totalIncome;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Cost Tracking
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the remaining research costs for all technologies currently being
        /// researched by a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Dictionary mapping techId to remaining research cost (0 if complete).</returns>
        public Dictionary<string, int> GetResearchCosts(string nationId)
        {
            var result = new Dictionary<string, int>();

            // Try to get the TechTree from the scene
            var techTree = FindObjectOfType<TechTree>();
            if (techTree == null)
            {
                Debug.LogWarning("[ResearchManager] TechTree not found in scene. Cannot calculate remaining costs.");
                return result;
            }

            var researched = techTree.GetResearchedTechs(nationId);
            var researchedSet = new HashSet<string>(researched.Keys);

            foreach (var tech in techTree.AllTechs)
            {
                if (tech == null) continue;
                if (researchedSet.Contains(tech.TechId)) continue;
                if (!tech.IsResearching) continue;

                int remaining = tech.ResearchCost - tech.ResearchProgress;
                result[tech.TechId] = Mathf.Max(0, remaining);
            }

            return result;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Income Modifiers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets a custom research income modifier for a nation.
        /// This can represent tech bonuses, wonder effects, or policy decisions.
        /// </summary>
        /// <param name="nationId">The nation to modify.</param>
        /// <param name="modifier">Flat research point modifier per turn (can be negative).</param>
        public void SetIncomeModifier(string nationId, int modifier)
        {
            EnsureNationRegistered(nationId);
            _nationIncomeModifiers[nationId] = modifier;
            Debug.Log($"[ResearchManager] Income modifier for '{nationId}' set to {modifier:+#;-#;0}.");
        }

        /// <summary>
        /// Gets the current research income modifier for a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Current income modifier, or 0 if not set.</returns>
        public int GetIncomeModifier(string nationId)
        {
            _nationIncomeModifiers.TryGetValue(nationId, out var modifier);
            return modifier;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Nation Registration
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a nation for research point tracking. Called automatically when
        /// points are added to an unknown nation.
        /// </summary>
        /// <param name="nationId">The nation to register.</param>
        public void RegisterNation(string nationId)
        {
            EnsureNationRegistered(nationId);
        }

        /// <summary>
        /// Resets all research points for all nations to zero.
        /// </summary>
        public void ResetAllPoints()
        {
            var keys = new List<string>(_nationResearchPoints.Keys);
            foreach (var nationId in keys)
            {
                _nationResearchPoints[nationId] = 0;
            }

            Debug.Log("[ResearchManager] All research points reset to zero.");
        }

        /// <summary>
        /// Gets the research point balance for all registered nations.
        /// </summary>
        /// <returns>Dictionary mapping nationId to current research point balance.</returns>
        public Dictionary<string, int> GetAllBalances()
        {
            return new Dictionary<string, int>(_nationResearchPoints);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Turn Processing
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Processes research income for all nations for a single turn.
        /// Calculates income from cities, adds to balances, and advances active research.
        /// </summary>
        /// <param name="allCities">List of all hex cells in the game.</param>
        /// <param name="techTree">Reference to the tech tree for advancing research.</param>
        public void ProcessTurn(List<HexCell> allCities, TechTree techTree)
        {
            if (techTree == null)
            {
                Debug.LogError("[ResearchManager] TechTree reference is null. Cannot process turn.");
                return;
            }

            foreach (var nationId in new List<string>(_nationResearchPoints.Keys))
            {
                // Calculate income
                int income = CalculateResearchIncome(nationId, allCities);
                AddResearchPoints(nationId, income);

                // Advance active research
                int balance = GetResearchPoints(nationId);
                if (balance > 0)
                {
                    // Get current research costs
                    var costs = GetResearchCosts(nationId);
                    int totalNeeded = 0;
                    foreach (var cost in costs.Values)
                        totalNeeded += cost;

                    // Spend what we can afford
                    int toSpend = Mathf.Min(balance, totalNeeded);
                    if (toSpend > 0)
                    {
                        techTree.AdvanceResearch(nationId, toSpend);
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Private Helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures a nation is registered in all tracking dictionaries.
        /// </summary>
        private void EnsureNationRegistered(string nationId)
        {
            if (!_nationResearchPoints.ContainsKey(nationId))
                _nationResearchPoints[nationId] = 0;

            if (!_nationIncomeModifiers.ContainsKey(nationId))
                _nationIncomeModifiers[nationId] = 0;
        }
    }
}
