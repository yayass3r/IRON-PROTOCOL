// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: AllianceCurrency.cs
// Description: Digital alliance currency system with exchange rate calculation,
//              issuance, and inter-nation trading mechanics.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronProtocol.Economy
{
    /// <summary>
    /// Represents a digital currency issued by an alliance or dominant nation.
    /// Exchange rate is dynamically calculated based on the issuing nation's
    /// economic strength, military power, trade volume, and currency stability.
    /// </summary>
    [System.Serializable]
    public class AllianceCurrency
    {
        // --------------------------------------------------------------------- //
        // Identity & Issuer
        // --------------------------------------------------------------------- //

        /// <summary>Unique identifier for this currency (e.g. "IRON_CREDIT", "NATO_BOND").</summary>
        [SerializeField] private string currencyId;

        /// <summary>Human-readable name displayed in UI (e.g. "Iron Credit", "Pacific Dollar").</summary>
        [SerializeField] private string currencyName;

        /// <summary>Unique identifier of the nation that issues this currency.</summary>
        [SerializeField] private string issuingNationId;

        /// <summary>Sprite icon for UI display.</summary>
        [SerializeField] private Sprite icon;

        // --------------------------------------------------------------------- //
        // Economic Properties
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Current exchange rate relative to a global baseline currency.
        /// Higher values mean the currency is stronger (buys more).
        /// </summary>
        [SerializeField] private float exchangeRate = 1.0f;

        /// <summary>
        /// Total value backing this currency. Represents the aggregate economic
        /// output and reserves of the issuing nation.
        /// </summary>
        [SerializeField] private float value = 0f;

        /// <summary>
        /// Stability rating from 0 (hyperinflation / collapse) to 100 (rock solid).
        /// Influenced by the issuing nation's political stability, war status, and trade health.
        /// </summary>
        [SerializeField] private float stability = 50f;

        /// <summary>
        /// Total amount of currency currently in circulation.
        /// Managed through <see cref="IssueCurrency"/> and burning on transfer out.
        /// </summary>
        [SerializeField] private float totalSupply = 0f;

        // --------------------------------------------------------------------- //
        // Alliance Membership
        // --------------------------------------------------------------------- //

        /// <summary>
        /// List of nations currently using this currency as their trade medium.
        /// Includes the issuing nation automatically.
        /// </summary>
        [SerializeField] private List<string> memberNations = new List<string>();

        // --------------------------------------------------------------------- //
        // Weights for Exchange Rate Calculation
        // --------------------------------------------------------------------- //

        [Header("Exchange Rate Weights")]
        [Tooltip("Weight of GDP contribution to exchange rate.")]
        [SerializeField] private float gdpWeight = 0.35f;

        [Tooltip("Weight of military strength contribution to exchange rate.")]
        [SerializeField] private float militaryWeight = 0.20f;

        [Tooltip("Weight of trade volume contribution to exchange rate.")]
        [SerializeField] private float tradeVolumeWeight = 0.25f;

        [Tooltip("Weight of stability contribution to exchange rate.")]
        [SerializeField] private float stabilityWeight = 0.20f;

        // --------------------------------------------------------------------- //
        // Events
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Fired when the exchange rate changes significantly (>1%).
        /// </summary>
        public event Action<string, float, float> OnExchangeRateChanged;

        /// <summary>
        /// Fired when currency is issued to a member nation.
        /// </summary>
        public event Action<string, string, float> OnCurrencyIssued;

        /// <summary>
        /// Fired when a trade between nations occurs in this currency.
        /// </summary>
        public event Action<string, string, string, float> OnCurrencyTraded;

        // --------------------------------------------------------------------- //
        // Properties
        // --------------------------------------------------------------------- //

        /// <summary>Unique identifier for this currency.</summary>
        public string CurrencyId => currencyId;

        /// <summary>Display name for this currency.</summary>
        public string CurrencyName => currencyName;

        /// <summary>The nation that issues this currency.</summary>
        public string IssuingNationId => issuingNationId;

        /// <summary>Current exchange rate relative to baseline.</summary>
        public float ExchangeRate => exchangeRate;

        /// <summary>Total backing value of the currency.</summary>
        public float Value => value;

        /// <summary>Stability rating from 0 to 100.</summary>
        public float Stability => stability;

        /// <summary>Total currency currently in circulation.</summary>
        public float TotalSupply => totalSupply;

        /// <summary>Read-only list of member nations.</summary>
        public IReadOnlyList<string> MemberNations => memberNations.AsReadOnly();

        // --------------------------------------------------------------------- //
        // Constructor
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Creates a new AllianceCurrency instance.
        /// The issuing nation is automatically added as the first member.
        /// </summary>
        /// <param name="currencyId">Unique identifier.</param>
        /// <param name="currencyName">Display name.</param>
        /// <param name="issuingNationId">Nation that creates this currency.</param>
        public AllianceCurrency(string currencyId, string currencyName, string issuingNationId)
        {
            this.currencyId = currencyId;
            this.currencyName = currencyName;
            this.issuingNationId = issuingNationId;

            if (!string.IsNullOrEmpty(issuingNationId))
            {
                this.memberNations.Add(issuingNationId);
            }
        }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public AllianceCurrency() { }

        // --------------------------------------------------------------------- //
        // Public Methods - Exchange Rate
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Recalculates the exchange rate based on four weighted factors:
        /// <list type="number">
        ///   <item><b>GDP</b> (35% weight): Issuing nation's gross domestic product / resource value.</item>
        ///   <item><b>Military Strength</b> (20% weight): Backing power of the nation's military.</item>
        ///   <item><b>Trade Volume</b> (25% weight): How much this currency is used in trade.</item>
        ///   <item><b>Stability</b> (20% weight): Political/economic stability of the issuer.</item>
        /// </list>
        /// </summary>
        /// <param name="issuingNationGDP">GDP value of the issuing nation.</param>
        /// <param name="militaryStrength">Military strength score (normalized 0-100).</param>
        /// <param name="tradeVolume">Total trade volume using this currency this turn.</param>
        /// <param name="stabilityScore">Current stability score (0-100).</param>
        /// <returns>The newly calculated exchange rate.</returns>
        public float CalculateExchangeRate(float issuingNationGDP, float militaryStrength, float tradeVolume, float stabilityScore)
        {
            // Update stored stability
            stability = Mathf.Clamp(stabilityScore, 0f, 100f);

            // --- GDP Factor ---
            // Normalize GDP to a 0-2 range (assumes GDP is in thousands)
            float gdpFactor = Mathf.Clamp(issuingNationGDP / 50000f, 0f, 2f);

            // --- Military Factor ---
            float militaryFactor = Mathf.Clamp(militaryStrength / 100f, 0f, 2f);

            // --- Trade Volume Factor ---
            // More trade = stronger currency; normalized by supply
            float tradeFactor = totalSupply > 0f
                ? Mathf.Clamp(tradeVolume / Mathf.Max(totalSupply, 1f), 0f, 2f)
                : 0.5f;

            // --- Stability Factor ---
            // Higher stability = stronger currency
            float stabilityFactor = stability / 50f; // 1.0 at stability=50, 2.0 at stability=100

            // --- Weighted Combination ---
            float newRate = (gdpFactor * gdpWeight) +
                            (militaryFactor * militaryWeight) +
                            (tradeFactor * tradeVolumeWeight) +
                            (stabilityFactor * stabilityWeight);

            // Normalize: 1.0 is baseline, with a range of 0.1 to 5.0
            newRate = Mathf.Clamp(newRate, 0.1f, 5.0f);

            // Smooth transition to avoid violent swings
            float oldRate = exchangeRate;
            exchangeRate = Mathf.Lerp(oldRate, newRate, 0.3f);
            exchangeRate = Mathf.Round(exchangeRate * 1000f) / 1000f;

            // Fire event if rate changed meaningfully (>1%)
            if (oldRate > 0f && Mathf.Abs(exchangeRate - oldRate) / oldRate > 0.01f)
            {
                OnExchangeRateChanged?.Invoke(currencyId, oldRate, exchangeRate);
                Debug.Log($"[AllianceCurrency] '{currencyName}' exchange rate changed: {oldRate:F3} -> {exchangeRate:F3}");
            }

            return exchangeRate;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Issuance & Trading
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Issues new currency to a member nation.
        /// Increases total supply and assigns the amount to the nation's balance.
        /// Non-member nations must first join the currency union.
        /// </summary>
        /// <param name="nationId">The nation receiving the issued currency.</param>
        /// <param name="amount">Amount to issue. Must be positive.</param>
        /// <returns>True if issuance succeeded.</returns>
        public bool IssueCurrency(string nationId, float amount)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[AllianceCurrency] Cannot issue: nationId is null.");
                return false;
            }

            if (amount <= 0f)
            {
                Debug.LogWarning("[AllianceCurrency] Cannot issue: amount must be positive.");
                return false;
            }

            if (!memberNations.Contains(nationId))
            {
                Debug.LogWarning($"[AllianceCurrency] Nation '{nationId}' is not a member of '{currencyName}'.");
                return false;
            }

            totalSupply += amount;
            value += amount * exchangeRate;

            OnCurrencyIssued?.Invoke(currencyId, nationId, amount);
            Debug.Log($"[AllianceCurrency] Issued {amount:F2} '{currencyName}' to '{nationId}'. Total supply: {totalSupply:F2}");
            return true;
        }

        /// <summary>
        /// Executes a trade of this currency between two member nations.
        /// The sending nation's balance decreases and the receiving nation's increases.
        /// </summary>
        /// <param name="fromNationId">Nation sending the currency.</param>
        /// <param name="toNationId">Nation receiving the currency.</param>
        /// <param name="amount">Amount of currency to trade.</param>
        /// <returns>True if the trade succeeded.</returns>
        public bool TradeCurrency(string fromNationId, string toNationId, float amount)
        {
            if (string.IsNullOrEmpty(fromNationId) || string.IsNullOrEmpty(toNationId))
            {
                Debug.LogWarning("[AllianceCurrency] Trade failed: null nation IDs.");
                return false;
            }

            if (fromNationId == toNationId)
            {
                Debug.LogWarning("[AllianceCurrency] Trade failed: cannot trade with self.");
                return false;
            }

            if (amount <= 0f)
            {
                Debug.LogWarning("[AllianceCurrency] Trade failed: amount must be positive.");
                return false;
            }

            if (!memberNations.Contains(fromNationId))
            {
                Debug.LogWarning($"[AllianceCurrency] Trade failed: '{fromNationId}' is not a member.");
                return false;
            }

            if (!memberNations.Contains(toNationId))
            {
                Debug.LogWarning($"[AllianceCurrency] Trade failed: '{toNationId}' is not a member.");
                return false;
            }

            // In a full implementation, each nation would have a balance field.
            // Here we track the trade and update value.
            float tradeValue = amount * exchangeRate;

            OnCurrencyTraded?.Invoke(currencyId, fromNationId, toNationId, amount);

            Debug.Log($"[AllianceCurrency] Trade: {amount:F2} '{currencyName}' from '{fromNationId}' to '{toNationId}' " +
                      $"(value: {tradeValue:F2}, rate: {exchangeRate:F3})");

            return true;
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Membership
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Adds a nation as a member of this currency union.
        /// Only the issuing nation or game logic should manage membership.
        /// </summary>
        /// <param name="nationId">The nation to add.</param>
        /// <returns>True if the nation was added (was not already a member).</returns>
        public bool AddMember(string nationId)
        {
            if (string.IsNullOrEmpty(nationId))
            {
                Debug.LogWarning("[AllianceCurrency] Cannot add member: nationId is null.");
                return false;
            }

            if (memberNations.Contains(nationId))
            {
                Debug.LogWarning($"[AllianceCurrency] Nation '{nationId}' is already a member.");
                return false;
            }

            memberNations.Add(nationId);
            Debug.Log($"[AllianceCurrency] Nation '{nationId}' joined currency '{currencyName}'.");
            return true;
        }

        /// <summary>
        /// Removes a nation from this currency union.
        /// The issuing nation cannot be removed.
        /// </summary>
        /// <param name="nationId">The nation to remove.</param>
        /// <returns>True if the nation was removed.</returns>
        public bool RemoveMember(string nationId)
        {
            if (string.IsNullOrEmpty(nationId)) return false;

            if (nationId == issuingNationId)
            {
                Debug.LogWarning("[AllianceCurrency] Cannot remove the issuing nation.");
                return false;
            }

            bool removed = memberNations.Remove(nationId);
            if (removed)
            {
                Debug.Log($"[AllianceCurrency] Nation '{nationId}' left currency '{currencyName}'.");
            }
            return removed;
        }

        /// <summary>
        /// Checks whether a nation is a member of this currency union.
        /// </summary>
        /// <param name="nationId">The nation to check.</param>
        /// <returns>True if the nation is a member.</returns>
        public bool IsMember(string nationId)
        {
            return !string.IsNullOrEmpty(nationId) && memberNations.Contains(nationId);
        }

        // --------------------------------------------------------------------- //
        // Public Methods - Modifiers
        // --------------------------------------------------------------------- //

        /// <summary>
        /// Updates the stability rating for this currency.
        /// Stability is affected by war, political events, and economic health.
        /// </summary>
        /// <param name="delta">Change in stability (can be positive or negative).</param>
        public void ModifyStability(float delta)
        {
            float oldStability = stability;
            stability = Mathf.Clamp(stability + delta, 0f, 100f);

            if (Mathf.Abs(stability - oldStability) > 0.1f)
            {
                Debug.Log($"[AllianceCurrency] '{currencyName}' stability: {oldStability:F1} -> {stability:F1}");
            }
        }

        /// <summary>
        /// Burns (destroys) currency from circulation, reducing total supply.
        /// Used when nations leave the union or to combat inflation.
        /// </summary>
        /// <param name="amount">Amount to burn.</param>
        public void BurnCurrency(float amount)
        {
            if (amount <= 0f) return;
            totalSupply = Mathf.Max(0f, totalSupply - amount);
            value = Mathf.Max(0f, value - (amount * exchangeRate));
            Debug.Log($"[AllianceCurrency] Burned {amount:F2} '{currencyName}'. New supply: {totalSupply:F2}");
        }

        /// <summary>
        /// Updates the backing value directly (e.g., when the issuing nation's GDP changes).
        /// </summary>
        /// <param name="newValue">New total backing value.</param>
        public void UpdateValue(float newValue)
        {
            value = Mathf.Max(0f, newValue);
        }
    }
}
