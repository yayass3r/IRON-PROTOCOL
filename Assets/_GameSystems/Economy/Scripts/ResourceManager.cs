// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: ResourceManager.cs
// Description: Per-nation resource management including income collection from
//              cities, supply consumption by units, and total value calculation.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.Economy
{
    /// <summary>
    /// Represents a single unit's supply consumption requirements.
    /// Used by ResourceManager to calculate per-turn maintenance costs.
    /// </summary>
    [System.Serializable]
    public class SupplyRequirement
    {
        /// <summary>Resource identifier consumed by the unit.</summary>
        public string resourceId;

        /// <summary>Amount consumed per turn.</summary>
        public float amountPerTurn;
    }

    /// <summary>
    /// Minimal interface for units that consume resources.
    /// The actual UnitBase implementation should implement this interface.
    /// </summary>
    public interface IUnitSupplyConsumer
    {
        /// <summary>Unique identifier of the unit.</summary>
        string UnitId { get; }

        /// <summary>Whether the unit is currently active and consumes supply.</summary>
        bool IsActive { get; }

        /// <summary>List of per-turn resource consumption requirements.</summary>
        List<SupplyRequirement> GetSupplyRequirements();
    }

    /// <summary>
    /// Minimal interface for hex cells that produce resources.
    /// The actual HexCell implementation should implement this interface.
    /// </summary>
    public interface IResourceProducer
    {
        /// <summary>Unique identifier of the hex cell / city.</summary>
        string CellId { get; }

        /// <summary>Whether this cell has an active production facility.</summary>
        bool HasProduction { get; }

        /// <summary>Resource produced by this cell.</summary>
        string ResourceProduced { get; }

        /// <summary>Amount produced per turn (base, before modifiers).</summary>
        float ProductionAmount { get; }

        /// <summary>Terrain type affecting production efficiency.</summary>
        string TerrainType { get; }

        /// <summary>Production level or upgrade tier (0-based).</summary>
        int ProductionLevel { get; }
    }

    /// <summary>
    /// Delegate for resource change notifications.
    /// </summary>
    /// <param name="nationId">The nation whose resources changed.</param>
    /// <param name="resourceId">The resource that changed.</param>
    /// <param name="oldAmount">Previous amount.</param>
    /// <param name="newAmount">New amount after the change.</param>
    public delegate void ResourceChangedHandler(string nationId, string resourceId, float oldAmount, float newAmount);

    /// <summary>
    /// MonoBehaviour managing per-nation resource storage, income collection,
    /// supply consumption, and affordability checks.
    /// Each nation maintains an independent dictionary of resource quantities.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Fired when any nation's resource amount changes.
        /// </summary>
        public event ResourceChangedHandler OnResourceChanged;

        /// <summary>
        /// Fired when a nation cannot afford a required resource consumption.
        /// </summary>
        public event Action<string, string, float> OnResourceDeficit;

        // --------------------------------------------------------------------- //
        // Configuration
        // --------------------------------------------------------------------- //

        [Header("Production Settings")]
        [Tooltip("Base multiplier applied to all city production.")]
        [SerializeField] private float baseProductionMultiplier = 1.0f;

        [Tooltip("Terrain-based production bonuses. Key = terrain type, Value = multiplier.")]
        [SerializeField] private Dictionary<string, float> terrainBonuses = new Dictionary<string, float>
        {
            { "plains", 1.0f },
            { "hills", 0.8f },
            { "mountains", 0.4f },
            { "forest", 1.1f },
            { "desert", 0.3f },
            { "coast", 0.9f },
            { "urban", 1.5f },
            { "fertile", 1.3f }
        };

        [Header("Supply Settings")]
        [Tooltip("Global supply consumption multiplier (difficulty scaling).")]
        [SerializeField] private float supplyConsumptionMultiplier = 1.0f;

        [Tooltip("Whether units are disbanded automatically when supply runs out.")]
        [SerializeField] private bool disbandOnDeficit = false;

        // --------------------------------------------------------------------- //
        // Runtime State
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Nested dictionary: nationId -> resourceId -> amount.
        /// All resource quantities are stored per-nation.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, float>> _resources =
            new Dictionary<string, Dictionary<string, float>>();

        /// <summary>Tracks which units suffered supply deficit this turn.</summary>
        private readonly Dictionary<string, List<string>> _deficitUnits =
            new Dictionary<string, List<string>>();

        // --------------------------------------------------------------------- //
        // Public Methods - Nation Registration
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Registers a new nation in the resource system with starting amounts.
        /// If the nation already exists, its resources are not overwritten.
        /// </summary>
        /// <param name="nationId">Unique identifier of the nation.</param>
        /// <param name="startingResources">Initial resource amounts. Key = resourceId, Value = starting amount.</param>
        public void RegisterNation(string nationId, Dictionary<string, float> startingResources = null)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[ResourceManager] Cannot register nation: nationId is null or empty.");
                return;
            }

            if (_resources.ContainsKey(nationId))
            {
                Debug.LogWarning($"[ResourceManager] Nation '{nationId}' is already registered.");
                return;
            }

            _resources[nationId] = new Dictionary<string, float>();

            if (startingResources != null)
            {
                foreach (var kvp in startingResources)
                {
                    _resources[nationId][kvp.Key] = Mathf.Max(0f, kvp.Value);
                }
            }

            _deficitUnits[nationId] = new List<string>();
            Debug.Log($"[ResourceManager] Nation '{nationId}' registered with {_resources[nationId].Count} resources.");
        }

        /// <summary>
        /// Removes a nation from the resource system entirely.
        /// All associated resource data is lost.
        /// </summary>
        /// <param name="nationId">The nation to unregister.</param>
        public void UnregisterNation(string nationId)
        {
            if (_resources.ContainsKey(nationId))
            {
                _resources.Remove(nationId);
                _deficitUnits.Remove(nationId);
                Debug.Log($"[ResourceManager] Nation '{nationId}' unregistered.");
            }
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Resource Operations
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Adds a specified amount of a resource to a nation's stockpile.
        /// Creates the resource entry if it does not already exist.
        /// </summary>
        /// <param name="nationId">The nation receiving the resource.</param>
        /// <param name="resourceId">The resource type to add.</param>
        /// <param name="amount">Amount to add. Must be positive.</param>
        /// <returns>True if the resource was successfully added.</returns>
        public bool AddResource(string nationId, string resourceId, float amount)
        {
            if (!ValidateInputs(nationId, resourceId, amount, false)) return false;

            float oldAmount = GetAmount(nationId, resourceId);
            _resources[nationId][resourceId] = oldAmount + amount;

            OnResourceChanged?.Invoke(nationId, resourceId, oldAmount, _resources[nationId][resourceId]);
            return true;
        }

        /// <summary>
        /// Removes a specified amount of a resource from a nation's stockpile.
        /// Fails silently if the nation does not have enough of the resource.
        /// </summary>
        /// <param name="nationId">The nation losing the resource.</param>
        /// <param name="resourceId">The resource type to remove.</param>
        /// <param name="amount">Amount to remove. Must be positive.</param>
        /// <returns>True if the resource was successfully removed.</returns>
        public bool RemoveResource(string nationId, string resourceId, float amount)
        {
            if (!ValidateInputs(nationId, resourceId, amount, false)) return false;

            float current = GetAmount(nationId, resourceId);
            if (current < amount)
            {
                Debug.LogWarning($"[ResourceManager] Nation '{nationId}' cannot afford {amount} '{resourceId}' (has {current:F1}).");
                return false;
            }

            float newAmount = current - amount;
            _resources[nationId][resourceId] = newAmount;

            OnResourceChanged?.Invoke(nationId, resourceId, current, newAmount);
            return true;
        }

        /// <summary>
        /// Gets the current amount of a specific resource for a nation.
        /// Returns 0 if the nation or resource does not exist.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <param name="resourceId">The resource type to query.</param>
        /// <returns>Current resource amount, or 0.</returns>
        public float GetAmount(string nationId, string resourceId)
        {
            if (string.IsNullOrEmpty(nationId) || string.IsNullOrEmpty(resourceId)) return 0f;
            if (!_resources.ContainsKey(nationId)) return 0f;
            if (!_resources[nationId].ContainsKey(resourceId)) return 0f;

            return _resources[nationId][resourceId];
        }

        /// <summary>
        /// Checks whether a nation can afford a given amount of a resource.
        /// </summary>
        /// <param name="nationId">The nation to check.</param>
        /// <param name="resourceId">The resource type.</param>
        /// <param name="amount">The required amount.</param>
        /// <returns>True if the nation has at least the specified amount.</returns>
        public bool CanAfford(string nationId, string resourceId, float amount)
        {
            if (amount <= 0f) return true; // Zero or negative cost is always affordable
            return GetAmount(nationId, resourceId) >= amount;
        }

        /// <summary>
        /// Checks whether a nation can afford a set of multiple resource costs.
        /// </summary>
        /// <param name="nationId">The nation to check.</param>
        /// <param name="costs">Dictionary of resourceId -> required amount.</param>
        /// <returns>True if the nation can afford all costs simultaneously.</returns>
        public bool CanAffordAll(string nationId, Dictionary<string, float> costs)
        {
            if (costs == null) return true;

            foreach (var kvp in costs)
            {
                if (!CanAfford(nationId, kvp.Key, kvp.Value))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Attempts to pay multiple resource costs at once. Rolls back all if any fail.
        /// </summary>
        /// <param name="nationId">The nation paying the costs.</param>
        /// <param name="costs">Dictionary of resourceId -> amount to pay.</param>
        /// <returns>True if all costs were paid successfully.</returns>
        public bool PayCosts(string nationId, Dictionary<string, float> costs)
        {
            if (costs == null || costs.Count == 0) return true;
            if (!CanAffordAll(nationId, costs)) return false;

            foreach (var kvp in costs)
            {
                RemoveResource(nationId, kvp.Key, kvp.Value);
            }
            return true;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Turn Processing
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Collects income for a nation from all owned cities/production facilities.
        /// Production is based on the cell's terrain type, production level, and global multiplier.
        /// </summary>
        /// <param name="nationId">The nation collecting income.</param>
        /// <param name="ownedCities">List of hex cells/cities owned by the nation that produce resources.</param>
        /// <returns>Dictionary of resourceId -> total amount collected this turn.</returns>
        public Dictionary<string, float> CollectIncome(string nationId, List<IResourceProducer> ownedCities)
        {
            var incomeReport = new Dictionary<string, float>();

            if (!_resources.ContainsKey(nationId))
            {
                Debug.LogWarning($"[ResourceManager] Nation '{nationId}' not registered.");
                return incomeReport;
            }

            if (ownedCities == null || ownedCities.Count == 0)
                return incomeReport;

            foreach (var city in ownedCities)
            {
                if (city == null || !city.HasProduction)
                    continue;

                string resourceId = city.ResourceProduced;
                if (string.IsNullOrEmpty(resourceId))
                    continue;

                // Calculate terrain bonus
                float terrainBonus = 1.0f;
                if (!string.IsNullOrEmpty(city.TerrainType) && terrainBonuses.ContainsKey(city.TerrainType))
                {
                    terrainBonus = terrainBonuses[city.TerrainType];
                }

                // Level scaling: each level provides +20% production
                float levelMultiplier = 1f + (city.ProductionLevel * 0.2f);

                // Final production = base * terrain * level * global multiplier
                float totalProduction = city.ProductionAmount * terrainBonus * levelMultiplier * baseProductionMultiplier;
                totalProduction = Mathf.Round(totalProduction * 100f) / 100f;

                AddResource(nationId, resourceId, totalProduction);

                // Accumulate in report
                if (!incomeReport.ContainsKey(resourceId))
                    incomeReport[resourceId] = 0f;
                incomeReport[resourceId] += totalProduction;
            }

            Debug.Log($"[ResourceManager] Nation '{nationId}' collected income: {FormatResourceDict(incomeReport)}");
            return incomeReport;
        }

        /// <summary>
        /// Consumes supply for all active units owned by a nation.
        /// Units that cannot be supplied are tracked and may be disbanded.
        /// </summary>
        /// <param name="nationId">The nation whose units consume supply.</param>
        /// <param name="units">List of units owned by the nation.</param>
        /// <returns>Dictionary of resourceId -> total amount consumed this turn.</returns>
        public Dictionary<string, float> ConsumeSupply(string nationId, List<IUnitSupplyConsumer> units)
        {
            var consumptionReport = new Dictionary<string, float>();

            if (!_resources.ContainsKey(nationId))
            {
                Debug.LogWarning($"[ResourceManager] Nation '{nationId}' not registered.");
                return consumptionReport;
            }

            // Clear previous deficit tracking
            _deficitUnits[nationId]?.Clear();

            if (units == null || units.Count == 0)
                return consumptionReport;

            foreach (var unit in units)
            {
                if (unit == null || !unit.IsActive)
                    continue;

                var requirements = unit.GetSupplyRequirements();
                if (requirements == null)
                    continue;

                bool unitSupplied = true;

                foreach (var req in requirements)
                {
                    if (string.IsNullOrEmpty(req.resourceId) || req.amountPerTurn <= 0f)
                        continue;

                    float consumption = req.amountPerTurn * supplyConsumptionMultiplier;
                    consumption = Mathf.Round(consumption * 100f) / 100f;

                    float current = GetAmount(nationId, req.resourceId);
                    if (current >= consumption)
                    {
                        // Sufficient supply - deduct normally
                        RemoveResource(nationId, req.resourceId, consumption);

                        if (!consumptionReport.ContainsKey(req.resourceId))
                            consumptionReport[req.resourceId] = 0f;
                        consumptionReport[req.resourceId] += consumption;
                    }
                    else
                    {
                        // Deficit! Consume what we can, flag the unit
                        if (current > 0f)
                        {
                            RemoveResource(nationId, req.resourceId, current);
                            if (!consumptionReport.ContainsKey(req.resourceId))
                                consumptionReport[req.resourceId] = 0f;
                            consumptionReport[req.resourceId] += current;
                        }

                        float deficit = consumption - current;
                        OnResourceDeficit?.Invoke(nationId, req.resourceId, deficit);
                        _deficitUnits[nationId]?.Add(unit.UnitId);
                        unitSupplied = false;
                    }
                }
            }

            if (_deficitUnits.ContainsKey(nationId) && _deficitUnits[nationId].Count > 0)
            {
                Debug.LogWarning($"[ResourceManager] Nation '{nationId}' has {_deficitUnits[nationId].Count} units in supply deficit.");
            }

            Debug.Log($"[ResourceManager] Nation '{nationId}' consumed supply: {FormatResourceDict(consumptionReport)}");
            return consumptionReport;
        }

        /// <summary>
        /// Calculates the total market value of all resources owned by a nation.
        /// Used for scoring, victory conditions, and alliance GDP calculations.
        /// </summary>
        /// <param name="nationId">The nation to evaluate.</param>
        /// <param name="priceProvider">Optional pricing function. Returns 1.0 per unit if null.</param>
        /// <returns>Total resource value in currency units.</returns>
        public float GetTotalResourceValue(string nationId, Func<string, float> priceProvider = null)
        {
            if (!_resources.ContainsKey(nationId))
                return 0f;

            float totalValue = 0f;
            foreach (var kvp in _resources[nationId])
            {
                float unitPrice = priceProvider != null ? priceProvider(kvp.Key) : 1f;
                totalValue += kvp.Value * unitPrice;
            }

            return Mathf.Round(totalValue * 100f) / 100f;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Queries
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Gets all resource amounts for a specific nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>Read-only dictionary of resourceId -> amount.</returns>
        public IReadOnlyDictionary<string, float> GetAllResources(string nationId)
        {
            if (!_resources.ContainsKey(nationId))
                return new Dictionary<string, float>();

            // Return a copy to prevent external mutation
            return new Dictionary<string, float>(_resources[nationId]);
        }

        /// <summary>
        /// Gets all nation IDs currently registered in the system.
        /// </summary>
        /// <returns>List of nation identifier strings.</returns>
        public IReadOnlyList<string> GetRegisteredNations()
        {
            return _resources.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the list of unit IDs that suffered supply deficit this turn for a nation.
        /// </summary>
        /// <param name="nationId">The nation to query.</param>
        /// <returns>List of unit IDs in deficit.</returns>
        public IReadOnlyList<string> GetDeficitUnits(string nationId)
        {
            if (_deficitUnits.ContainsKey(nationId))
                return _deficitUnits[nationId].AsReadOnly();
            return new List<string>().AsReadOnly();
        }

        /// <summary>
        /// Transfers resources from one nation to another (e.g., trade, aid).
        /// Fails if the sender cannot afford the transfer.
        /// </summary>
        /// <param name="fromNationId">The sending nation.</param>
        /// <param name="toNationId">The receiving nation.</param>
        /// <param name="resourceId">The resource to transfer.</param>
        /// <param name="amount">Amount to transfer.</param>
        /// <returns>True if the transfer succeeded.</returns>
        public bool TransferResource(string fromNationId, string toNationId, string resourceId, float amount)
        {
            if (string.IsNullOrEmpty(fromNationId) || string.IsNullOrEmpty(toNationId))
            {
                Debug.LogWarning("[ResourceManager] Transfer failed: null nation IDs.");
                return false;
            }

            if (fromNationId == toNationId)
            {
                Debug.LogWarning("[ResourceManager] Transfer failed: same nation.");
                return false;
            }

            if (!RemoveResource(fromNationId, resourceId, amount))
                return false;

            AddResource(toNationId, resourceId, amount);
            Debug.Log($"[ResourceManager] Transferred {amount} '{resourceId}' from '{fromNationId}' to '{toNationId}'.");
            return true;
        }

        // --------------------------------------------------------------------- //
        // Private Methods
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Validates common input parameters for resource operations.
        /// </summary>
        private bool ValidateInputs(string nationId, string resourceId, float amount, bool allowNegative)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[ResourceManager] nationId is null or empty.");
                return false;
            }

            if (string.IsNullOrEmpty(resourceId))
            {
                Debug.LogWarning("[ResourceManager] resourceId is null or empty.");
                return false;
            }

            if (!allowNegative && amount <= 0f)
            {
                Debug.LogWarning("[ResourceManager] Amount must be positive.");
                return false;
            }

            if (!_resources.ContainsKey(nationId))
            {
                Debug.LogWarning($"[ResourceManager] Nation '{nationId}' not registered.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Formats a resource dictionary for logging.
        /// </summary>
        private string FormatResourceDict(Dictionary<string, float> dict)
        {
            if (dict == null || dict.Count == 0) return "{}";
            return string.Join(", ", dict.Select(kvp => $"{kvp.Key}:{kvp.Value:F1}"));
        }
    }
}
