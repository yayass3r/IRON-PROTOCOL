// ============================================================================================
// IRON PROTOCOL - SaveManager.cs
// Save/Load manager using JSON serialization. Persists game state to the
// Application.persistentDataPath with automatic file management.
// ============================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace IronProtocol.Core
{
    // ----------------------------------------------------------------------------------------
    // Serializable Data Classes
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Lightweight serializable representation of a single unit's state.
    /// </summary>
    [Serializable]
    public class UnitSaveData
    {
        public int UnitId;
        public string UnitType;
        public int OwnerNationId;
        public int CurrentHealth;
        public int MaxHealth;
        public int PositionQ;
        public int PositionR;
        public bool HasMovedThisTurn;
        public bool HasAttackedThisTurn;
    }

    /// <summary>
    /// Lightweight serializable representation of a nation's state.
    /// </summary>
    [Serializable]
    public class NationSaveData
    {
        public int NationId;
        public string NationName;
        public int Treasury;
        public int TotalPopulation;
        public int MilitaryScore;
        public List<int> AlliedNationIds = new List<int>();
        public List<int> EnemyNationIds = new List<int>();
    }

    /// <summary>
    /// Lightweight serializable representation of market data.
    /// </summary>
    [Serializable]
    public class MarketSaveData
    {
        public List<ResourcePriceEntry> ResourcePrices = new List<ResourcePriceEntry>();
    }

    /// <summary>
    /// A single resource price entry in the market save data.
    /// </summary>
    [Serializable]
    public class ResourcePriceEntry
    {
        public string ResourceId;
        public float BasePrice;
        public float CurrentPrice;
    }

    /// <summary>
    /// The root save data structure containing all game state needed to
    /// fully restore a session of IRON PROTOCOL.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        /// <summary>The current turn number.</summary>
        public int TurnNumber;

        /// <summary>The current game phase (e.g., "Planning", "Combat", "Economy").</summary>
        public string Phase;

        /// <summary>All unit states on the map.</summary>
        public List<UnitSaveData> Units = new List<UnitSaveData>();

        /// <summary>All nation states.</summary>
        public List<NationSaveData> Nations = new List<NationSaveData>();

        /// <summary>Market data including resource prices.</summary>
        public MarketSaveData Market = new MarketSaveData();

        /// <summary>The procedural map seed used to regenerate the hex grid.</summary>
        public int MapSeed;

        /// <summary>Timestamp when this save was created (ISO 8601 format).</summary>
        public string Timestamp;

        /// <summary>A readable label for the save slot.</summary>
        public string SaveLabel;
    }

    // ----------------------------------------------------------------------------------------
    // SaveManager
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Manages saving and loading game state as JSON files.
    /// <para>
    /// Save files are stored under <see cref="Application.persistentDataPath"/>
    /// with a <c>.json</c> extension. The manager supports multiple save slots
    /// identified by an integer index.
    /// </para>
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        private const string SaveDirectory = "Saves";
        private const string FileExtension = ".json";

        /// <summary>
        /// The default number of save slots available.
        /// </summary>
        public const int MaxSaveSlots = 10;

        [Header("Settings")]
        [SerializeField, Tooltip("If true, save files are written with pretty-printed JSON for readability.")]
        private bool prettyPrint = true;

        /// <summary>
        /// Gets the full directory path where save files are stored.
        /// </summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveDirectory);

        // ----------------------------------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------------------------------

        private void Awake()
        {
            EnsureSaveDirectory();
        }

        // ----------------------------------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Serializes the provided game state to a JSON file at the specified save slot.
        /// </summary>
        /// <param name="data">The <see cref="GameSaveData"/> to persist.</param>
        /// <param name="slot">Save slot index (0 to <see cref="MaxSaveSlots"/> - 1).</param>
        /// <returns><c>true</c> if the save succeeded; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public bool SaveGameState(GameSaveData data, int slot = 0)
        {
            if (data == null)
            {
                Debug.LogError("[SaveManager] Cannot save null GameSaveData.");
                throw new ArgumentNullException(nameof(data));
            }

            if (slot < 0 || slot >= MaxSaveSlots)
            {
                Debug.LogError($"[SaveManager] Save slot {slot} is out of range (0-{MaxSaveSlots - 1}).");
                return false;
            }

            try
            {
                EnsureSaveDirectory();

                data.Timestamp = DateTime.Now.ToString("o");
                string json = prettyPrint
                    ? JsonUtility.ToJson(data, true)
                    : JsonUtility.ToJson(data);

                string filePath = GetFilePath(slot);
                File.WriteAllText(filePath, json);

                Debug.Log($"[SaveManager] Game state saved to slot {slot}: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save game state (slot {slot}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads and deserializes game state from the JSON file at the specified save slot.
        /// </summary>
        /// <param name="slot">Save slot index (0 to <see cref="MaxSaveSlots"/> - 1).</param>
        /// <returns>
        /// The deserialized <see cref="GameSaveData"/>, or <c>null</c> if the file does not
        /// exist or deserialization fails.
        /// </returns>
        public GameSaveData LoadGameState(int slot = 0)
        {
            if (slot < 0 || slot >= MaxSaveSlots)
            {
                Debug.LogError($"[SaveManager] Save slot {slot} is out of range (0-{MaxSaveSlots - 1}).");
                return null;
            }

            string filePath = GetFilePath(slot);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SaveManager] No save file found at slot {slot}: {filePath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<GameSaveData>(json);

                Debug.Log($"[SaveManager] Game state loaded from slot {slot}. Turn: {data.TurnNumber}, Phase: {data.Phase}");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to load game state (slot {slot}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes the save file at the specified slot if it exists.
        /// </summary>
        /// <param name="slot">Save slot index.</param>
        /// <returns><c>true</c> if the file was deleted; <c>false</c> if it didn't exist or deletion failed.</returns>
        public bool DeleteSave(int slot = 0)
        {
            string filePath = GetFilePath(slot);

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SaveManager] No save file to delete at slot {slot}.");
                return false;
            }

            try
            {
                File.Delete(filePath);
                Debug.Log($"[SaveManager] Save file deleted at slot {slot}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to delete save (slot {slot}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks whether a save file exists at the specified slot.
        /// </summary>
        /// <param name="slot">Save slot index.</param>
        /// <returns><c>true</c> if a save file exists at the slot.</returns>
        public bool SaveExists(int slot = 0)
        {
            return File.Exists(GetFilePath(slot));
        }

        /// <summary>
        /// Returns information about all existing save slots.
        /// </summary>
        /// <returns>
        /// A list of tuples containing (slot index, timestamp, save label) for each existing save.
        /// </returns>
        public List<(int slot, string timestamp, string label)> GetAllSaves()
        {
            var saves = new List<(int, string, string)>();

            for (int i = 0; i < MaxSaveSlots; i++)
            {
                string filePath = GetFilePath(i);
                if (File.Exists(filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var data = JsonUtility.FromJson<GameSaveData>(json);
                        saves.Add((i, data.Timestamp, data.SaveLabel));
                    }
                    catch
                    {
                        // File exists but is corrupt or unreadable; skip it.
                        saves.Add((i, "CORRUPT", "Corrupted Save"));
                    }
                }
            }

            return saves;
        }

        // ----------------------------------------------------------------------------------------
        // Internal
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Generates the full file path for a given save slot.
        /// </summary>
        private static string GetFilePath(int slot)
        {
            return Path.Combine(SavePath, $"save_slot_{slot}{FileExtension}");
        }

        /// <summary>
        /// Ensures the save directory exists, creating it if necessary.
        /// </summary>
        private static void EnsureSaveDirectory()
        {
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
                Debug.Log($"[SaveManager] Created save directory: {SavePath}");
            }
        }
    }
}
