// =====================================================================================
// Iron Protocol - Radiation System
// =====================================================================================
// Manages nuclear radiation across the hex map, including application, decay,
// contamination detection, supply line poisoning, and visual overlay rendering.
// Radiation persists for multiple turns after a nuclear strike, damaging units
// and blocking supply lines in contaminated areas.
// =====================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

using IronProtocol.HexMap;

namespace IronProtocol.Nuclear
{
    /// <summary>
    /// Manages radiation levels across the hex map. Tracks contamination per hex,
    /// handles decay over time, calculates damage to units, poisons supply lines,
    /// and provides visual overlay data for the map renderer.
    /// </summary>
    public class RadiationSystem : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────────
        //  Configuration
        // ──────────────────────────────────────────────────────────────────────────────

        [Header("Radiation Thresholds")]
        [Tooltip("Radiation level above which a hex is considered contaminated (0-100).")]
        [SerializeField] private float contaminationThreshold = 10f;

        [Tooltip("Radiation level below which radiation is considered negligible and removed.")]
        [SerializeField] private float negligibleThreshold = 0.1f;

        [Header("Radiation Decay")]
        [Tooltip("Percentage of radiation removed per turn (0.0 - 1.0).")]
        [SerializeField] private float decayRate = 0.05f;

        [Tooltip("Additional flat reduction per turn applied to all contaminated hexes.")]
        [SerializeField] private float flatDecayPerTurn = 0.5f;

        [Header("Damage Configuration")]
        [Tooltip("Base HP damage per turn for units at maximum radiation (100).")]
        [SerializeField] private int maxRadiationDamagePerTurn = 50;

        [Tooltip("Minimum radiation level required to deal any damage to units.")]
        [SerializeField] private float damageThreshold = 5f;

        [Header("Supply Line Configuration")]
        [Tooltip("Whether radiation poisons supply lines passing through contaminated hexes.")]
        [SerializeField] private bool enableSupplyContamination = true;

        [Tooltip("Supply throughput reduction per point of radiation (0.0 - 1.0).")]
        [SerializeField] private float supplyPoisonRate = 0.02f;

        [Header("Visual Configuration")]
        [Tooltip("Color used for low-level radiation overlay.")]
        [SerializeField] private Color lowRadiationColor = new Color(0f, 1f, 0f, 0.15f);

        [Tooltip("Color used for medium-level radiation overlay.")]
        [SerializeField] private Color mediumRadiationColor = new Color(1f, 1f, 0f, 0.3f);

        [Tooltip("Color used for high-level radiation overlay.")]
        [SerializeField] private Color highRadiationColor = new Color(1f, 0f, 0f, 0.45f);

        [Tooltip("Radiation level threshold for medium overlay color.")]
        [SerializeField] private float mediumRadiationThreshold = 30f;

        [Tooltip("Radiation level threshold for high overlay color.")]
        [SerializeField] private float highRadiationThreshold = 60f;

        // ──────────────────────────────────────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Maps hex coordinate keys to their current radiation level (0-100).</summary>
        private Dictionary<string, float> _radiationLevels;

        /// <summary>Set of hex keys where supply lines are currently contaminated.</summary>
        private HashSet<string> _contaminatedSupplyHexes;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Fired when a hex becomes newly contaminated. Parameters: (hexCoord, radiationLevel).</summary>
        public event Action<HexCoord, float> OnHexContaminated;

        /// <summary>Fired when a hex's radiation drops below contamination threshold. Parameters: (hexCoord).</summary>
        public event Action<HexCoord> OnHexCleaned;

        /// <summary>Fired when radiation levels change during a turn. Parameters: (contaminatedCount, totalRadiationSum).</summary>
        public event Action<int, float> OnRadiationUpdated;

        // ──────────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ──────────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _radiationLevels = new Dictionary<string, float>();
            _contaminatedSupplyHexes = new HashSet<string>();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Radiation Application
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies radiation to a hex and its surrounding area. Intensity falls off
        /// with distance from the center point using exponential decay.
        /// </summary>
        /// <param name="center">The center hex coordinate of the radiation source.</param>
        /// <param name="intensity">Maximum radiation intensity at the center (0-100).</param>
        /// <param name="radius">Number of hex rings affected by radiation.</param>
        public void ApplyRadiation(HexCoord center, float intensity, int radius)
        {
            if (intensity <= 0f || radius <= 0) return;

            intensity = Mathf.Clamp(intensity, 0f, 100f);

            // Apply to center hex
            SetRadiation(center, intensity);

            // Apply to surrounding hexes with distance falloff (60% per ring)
            for (int dist = 1; dist <= radius; dist++)
            {
                var ringCoords = GetRingCoordinates(center, dist);
                float ringIntensity = intensity * Mathf.Pow(0.6f, dist);

                if (ringIntensity < negligibleThreshold) break;

                foreach (var coord in ringCoords)
                {
                    SetRadiation(coord, ringIntensity);
                }
            }

            Debug.Log($"[RadiationSystem] Applied radiation at ({center.Q},{center.R}): " +
                      $"intensity={intensity:F1}, radius={radius}");
        }

        /// <summary>
        /// Directly sets the radiation level at a specific hex coordinate.
        /// Radiation accumulates — if the new level is higher than the existing level, it overwrites.
        /// </summary>
        /// <param name="coord">The hex coordinate.</param>
        /// <param name="level">The radiation level to apply (0-100).</param>
        public void SetRadiation(HexCoord coord, float level)
        {
            string key = coord.ToString();
            level = Mathf.Clamp(level, 0f, 100f);

            bool wasContaminated = IsContaminated(coord);

            if (level < negligibleThreshold)
            {
                if (_radiationLevels.ContainsKey(key))
                    _radiationLevels.Remove(key);
            }
            else
            {
                // Radiation accumulates — keep the maximum
                if (_radiationLevels.TryGetValue(key, out float existing))
                    _radiationLevels[key] = Mathf.Max(existing, level);
                else
                    _radiationLevels[key] = level;
            }

            bool isNowContaminated = IsContaminated(coord);

            // Fire state-change events
            if (!wasContaminated && isNowContaminated)
                OnHexContaminated?.Invoke(coord, GetRadiationAt(coord));
            else if (wasContaminated && !isNowContaminated)
            {
                OnHexCleaned?.Invoke(coord);
                _contaminatedSupplyHexes.Remove(key);
            }
        }

        /// <summary>
        /// Removes radiation from a specific hex (e.g., via cleanup ability or decontamination unit).
        /// </summary>
        /// <param name="coord">The hex coordinate to clean.</param>
        /// <param name="reduction">Amount of radiation to remove.</param>
        public void CleanRadiation(HexCoord coord, float reduction)
        {
            if (reduction <= 0) return;

            string key = coord.ToString();
            if (_radiationLevels.TryGetValue(key, out float current))
            {
                float newLevel = Mathf.Max(0f, current - reduction);
                if (newLevel < negligibleThreshold)
                    _radiationLevels.Remove(key);
                else
                    _radiationLevels[key] = newLevel;
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Decay
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Decays all radiation on the map by the configured rate. Called once per game turn.
        /// Uses both percentage decay and flat reduction for realistic falloff.
        /// </summary>
        /// <param name="decayRateOverride">Optional custom decay rate. If 0 or negative, uses configured rate.</param>
        public void DecayRadiation(float decayRateOverride = 0f)
        {
            float rate = decayRateOverride > 0f ? decayRateOverride : decayRate;
            int decayedCount = 0;
            float totalRadiation = 0f;

            var keys = new List<string>(_radiationLevels.Keys);

            foreach (var key in keys)
            {
                if (!_radiationLevels.TryGetValue(key, out float current)) continue;

                // Apply percentage decay then flat decay
                float newLevel = Mathf.Max(0f, current * (1f - rate) - flatDecayPerTurn);

                if (newLevel < negligibleThreshold)
                {
                    _radiationLevels.Remove(key);
                    _contaminatedSupplyHexes.Remove(key);

                    if (TryParseCoord(key, out HexCoord coord))
                        OnHexCleaned?.Invoke(coord);
                }
                else
                {
                    _radiationLevels[key] = newLevel;
                    totalRadiation += newLevel;
                }

                decayedCount++;
            }

            OnRadiationUpdated?.Invoke(_radiationLevels.Count, totalRadiation);

            if (decayedCount > 0)
                Debug.Log($"[RadiationSystem] Decay: {decayedCount} processed, {_radiationLevels.Count} remain.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Queries
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the current radiation level at a specific hex coordinate.
        /// </summary>
        /// <param name="coord">The hex coordinate to query.</param>
        /// <returns>Radiation level (0-100), or 0 if the hex is not contaminated.</returns>
        public float GetRadiationAt(HexCoord coord)
        {
            _radiationLevels.TryGetValue(coord.ToString(), out float level);
            return level;
        }

        /// <summary>
        /// Determines whether a hex is contaminated (radiation above the contamination threshold).
        /// </summary>
        /// <param name="coord">The hex coordinate to check.</param>
        /// <returns><c>true</c> if radiation at this hex exceeds the contamination threshold.</returns>
        public bool IsContaminated(HexCoord coord)
        {
            return GetRadiationAt(coord) >= contaminationThreshold;
        }

        /// <summary>
        /// Calculates the HP damage per turn that a unit would take from radiation at a given level.
        /// Damage scales linearly from 0 at <see cref="damageThreshold"/> to
        /// <see cref="maxRadiationDamagePerTurn"/> at radiation level 100.
        /// </summary>
        /// <param name="radiationLevel">The radiation level (0-100).</param>
        /// <returns>Integer HP damage per turn. Returns 0 if below damage threshold.</returns>
        public int GetRadiationDamage(float radiationLevel)
        {
            if (radiationLevel < damageThreshold) return 0;

            float normalized = (radiationLevel - damageThreshold) / (100f - damageThreshold);
            return Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxRadiationDamagePerTurn);
        }

        /// <summary>
        /// Gets all hex coordinates where radiation exceeds the contamination threshold.
        /// </summary>
        /// <returns>List of contaminated <see cref="HexCoord"/> instances.</returns>
        public List<HexCoord> GetContaminatedHexes()
        {
            return GetContaminatedHexes(contaminationThreshold);
        }

        /// <summary>
        /// Gets all hex coordinates where radiation exceeds a custom threshold.
        /// </summary>
        /// <param name="minRadiation">Minimum radiation level to include.</param>
        /// <returns>List of hex coordinates meeting the threshold.</returns>
        public List<HexCoord> GetContaminatedHexes(float minRadiation)
        {
            var result = new List<HexCoord>();
            foreach (var kvp in _radiationLevels)
            {
                if (kvp.Value >= minRadiation && TryParseCoord(kvp.Key, out HexCoord coord))
                    result.Add(coord);
            }
            return result;
        }

        /// <summary>Gets the total number of contaminated hexes on the map.</summary>
        public int GetContaminatedHexCount() =>
            _radiationLevels.Count(kvp => kvp.Value >= contaminationThreshold);

        /// <summary>Gets a read-only copy of the complete radiation level dictionary.</summary>
        public Dictionary<string, float> GetAllRadiationLevels() =>
            new Dictionary<string, float>(_radiationLevels);

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Supply Line Contamination
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Contaminates supply lines passing through a hex. Supply throughput through
        /// this hex will be reduced based on radiation level until the hex is cleaned.
        /// </summary>
        /// <param name="coord">The hex coordinate where supply lines are contaminated.</param>
        /// <returns><c>true</c> if the hex is contaminated and supply was poisoned.</returns>
        public bool ContaminateSupplyLine(HexCoord coord)
        {
            if (!enableSupplyContamination) return false;

            float radiation = GetRadiationAt(coord);
            if (radiation < contaminationThreshold) return false;

            string key = coord.ToString();
            bool wasAlready = _contaminatedSupplyHexes.Contains(key);
            _contaminatedSupplyHexes.Add(key);

            if (!wasAlready)
            {
                float reduction = radiation * supplyPoisonRate;
                Debug.Log($"[RadiationSystem] Supply contaminated at ({coord.Q},{coord.R}). " +
                          $"Throughput reduced by {reduction:P0}.");
            }

            return true;
        }

        /// <summary>
        /// Gets the supply throughput multiplier for a hex (1.0 = full, 0.0 = blocked).
        /// </summary>
        /// <param name="coord">The hex coordinate to check.</param>
        /// <returns>Throughput multiplier from 0.0 to 1.0.</returns>
        public float GetSupplyThroughput(HexCoord coord)
        {
            if (!enableSupplyContamination) return 1f;

            string key = coord.ToString();
            if (!_contaminatedSupplyHexes.Contains(key)) return 1f;

            float radiation = GetRadiationAt(coord);
            return Mathf.Max(0f, 1f - (radiation * supplyPoisonRate));
        }

        /// <summary>Gets all hexes with contaminated supply lines.</summary>
        public List<HexCoord> GetContaminatedSupplyHexes()
        {
            var result = new List<HexCoord>();
            foreach (var key in _contaminatedSupplyHexes)
            {
                if (TryParseCoord(key, out HexCoord coord))
                    result.Add(coord);
            }
            return result;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Visual Overlay
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the radiation overlay color for a specific hex coordinate.
        /// Color intensity scales with radiation level.
        /// </summary>
        /// <param name="coord">The hex coordinate to query.</param>
        /// <returns>The overlay color with appropriate alpha, or transparent if no radiation.</returns>
        public Color GetOverlayColor(HexCoord coord)
        {
            float radiation = GetRadiationAt(coord);
            if (radiation < negligibleThreshold) return Color.clear;

            Color baseColor = radiation switch
            {
                float r when r >= highRadiationThreshold => highRadiationColor,
                float r when r >= mediumRadiationThreshold => mediumRadiationColor,
                _ => lowRadiationColor
            };

            // Scale alpha by radiation level
            Color result = baseColor;
            result.a *= Mathf.Clamp01(radiation / 100f);
            return result;
        }

        /// <summary>
        /// Generates overlay data for all contaminated hexes for rendering.
        /// Returns a list of (coord, color) tuples for the map renderer.
        /// </summary>
        /// <returns>List of overlay entries for rendering.</returns>
        public List<(HexCoord coord, Color color)> GetOverlayData()
        {
            return GetOverlayData(negligibleThreshold);
        }

        /// <summary>
        /// Generates overlay data with a custom minimum radiation threshold.
        /// </summary>
        /// <param name="minRadiation">Minimum radiation level to render.</param>
        /// <returns>List of overlay entries for rendering.</returns>
        public List<(HexCoord coord, Color color)> GetOverlayData(float minRadiation)
        {
            var data = new List<(HexCoord, Color)>();

            foreach (var kvp in _radiationLevels)
            {
                if (kvp.Value < minRadiation) continue;
                if (!TryParseCoord(kvp.Key, out HexCoord coord)) continue;

                Color color = GetOverlayColor(coord);
                if (color.a > 0.01f)
                    data.Add((coord, color));
            }

            return data;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Public API - Reset & Serialization
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Clears all radiation from the map and resets all state.</summary>
        public void ClearAllRadiation()
        {
            int count = _radiationLevels.Count;
            _radiationLevels.Clear();
            _contaminatedSupplyHexes.Clear();
            Debug.Log($"[RadiationSystem] Cleared all radiation from {count} hexes.");
        }

        /// <summary>
        /// Loads radiation data from an external dictionary (e.g., from save data or NuclearProgram).
        /// Replaces all existing radiation data.
        /// </summary>
        /// <param name="radiationData">Dictionary mapping hex keys to radiation levels.</param>
        public void LoadRadiationData(Dictionary<string, float> radiationData)
        {
            _radiationLevels.Clear();
            _contaminatedSupplyHexes.Clear();

            if (radiationData != null)
            {
                foreach (var kvp in radiationData)
                {
                    if (kvp.Value >= negligibleThreshold)
                    {
                        _radiationLevels[kvp.Key] = Mathf.Clamp(kvp.Value, 0f, 100f);

                        if (kvp.Value >= contaminationThreshold)
                            _contaminatedSupplyHexes.Add(kvp.Key);
                    }
                }
            }

            Debug.Log($"[RadiationSystem] Loaded radiation for {_radiationLevels.Count} hexes.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        //  Private Helpers
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates hex coordinates in a ring at the specified distance from center
        /// using axial coordinate ring-walking algorithm.
        /// </summary>
        private static List<HexCoord> GetRingCoordinates(HexCoord center, int distance)
        {
            var coords = new List<HexCoord>();

            // Six axial directions
            var directions = new (int dq, int dr)[]
            {
                (-1, +1), (-1, 0), (0, -1),
                (+1, -1), (+1, 0), (0, +1)
            };

            // Start at the "top" of the ring
            HexCoord current = new HexCoord(center.Q + distance, center.R - distance);

            for (int side = 0; side < 6; side++)
            {
                for (int step = 0; step < distance; step++)
                {
                    coords.Add(new HexCoord(current.Q, current.R));
                    current = new HexCoord(
                        current.Q + directions[side].dq,
                        current.R + directions[side].dr
                    );
                }
            }

            return coords;
        }

        /// <summary>
        /// Attempts to parse a hex coordinate from a string key. Expects format "(q,r)".
        /// </summary>
        private static bool TryParseCoord(string key, out HexCoord coord)
        {
            coord = default;
            if (string.IsNullOrEmpty(key)) return false;

            string cleaned = key.Trim('(', ')');
            string[] parts = cleaned.Split(',');

            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0].Trim(), out int q)) return false;
            if (!int.TryParse(parts[1].Trim(), out int r)) return false;

            coord = new HexCoord(q, r);
            return true;
        }
    }
}
