// =============================================================================
// IRON PROTOCOL - Turn-Based Strategy Game
// File: GameState.cs
// Description: Shared game state representation used by AI systems.
//              Provides a serializable snapshot of the world for strategic
//              and tactical decision-making.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.AI
{
    /// <summary>
    /// Represents a single nation's state within the game world.
    /// Used by AI to evaluate strategic options.
    /// </summary>
    [System.Serializable]
    public class NationState
    {
        /// <summary>Unique identifier of the nation.</summary>
        public string nationId;

        /// <summary>Total economic strength (GDP / resource value).</summary>
        public float economicStrength;

        /// <summary>Total military strength (unit count * average power).</summary>
        public float militaryStrength;

        /// <summary>Number of cities/territories owned.</summary>
        public int territoryCount;

        /// <summary>Current threat level from enemies (0-1).</summary>
        public float threatLevel;

        /// <summary>Diplomatic influence score.</summary>
        public float diplomaticInfluence;

        /// <summary>Resource reserves per type.</summary>
        public Dictionary<string, float> resources;

        /// <summary>Number of active military units.</summary>
        public int unitCount;

        /// <summary>Whether this nation is the AI's nation.</summary>
        public bool isSelf;

        public NationState()
        {
            resources = new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Minimal representation of a unit for tactical AI evaluation.
    /// </summary>
    [System.Serializable]
    public class UnitSnapshot
    {
        /// <summary>Unique identifier of the unit.</summary>
        public string unitId;

        /// <summary>Nation that owns this unit.</summary>
        public string ownerId;

        /// <summary>Unit type classification (e.g. "infantry", "armor", "artillery", "hq").</summary>
        public string unitType;

        /// <summary>Current hit points as a fraction (0.0 to 1.0).</summary>
        public float healthFraction;

        /// <summary>Current grid position (column, row).</summary>
        public Vector2Int gridPosition;

        /// <summary>Combat power rating.</summary>
        public float combatPower;

        /// <summary>Whether this unit can move this turn.</summary>
        public bool canMove;

        /// <summary>Whether this unit can attack this turn.</summary>
        public bool canAttack;

        /// <summary>Remaining movement points.</summary>
        public int movementPoints;

        /// <summary>Attack range in hex cells.</summary>
        public int attackRange;
    }

    /// <summary>
    /// Provides a complete snapshot of the game state for AI decision-making.
    /// Updated each turn by the game's turn manager.
    /// </summary>
    [System.Serializable]
    public class GameState
    {
        /// <summary>Current game turn number.</summary>
        public int currentTurn;

        /// <summary>Nation ID of the AI agent this state belongs to.</summary>
        public string aiNationId;

        /// <summary>States of all nations in the game.</summary>
        public List<NationState> nations;

        /// <summary>All visible units on the map (within AI's vision).</summary>
        public List<UnitSnapshot> visibleUnits;

        /// <summary>Current market prices for key resources.</summary>
        public Dictionary<string, float> resourcePrices;

        /// <summary>Weather conditions affecting the map.</summary>
        public string currentWeather;

        /// <summary>Map dimensions (width, height).</summary>
        public Vector2Int mapSize;

        public GameState()
        {
            nations = new List<NationState>();
            visibleUnits = new List<UnitSnapshot>();
            resourcePrices = new Dictionary<string, float>();
        }

        /// <summary>Gets the AI's own nation state.</summary>
        public NationState GetSelfState()
        {
            return nations.Find(n => n.isSelf);
        }

        /// <summary>Gets all enemy nation states.</summary>
        public List<NationState> GetEnemyStates()
        {
            return nations.FindAll(n => !n.isSelf);
        }

        /// <summary>Gets all visible enemy units.</summary>
        public List<UnitSnapshot> GetEnemyUnits()
        {
            string selfId = aiNationId;
            return visibleUnits.FindAll(u => u.ownerId != selfId);
        }

        /// <summary>Gets all visible friendly units.</summary>
        public List<UnitSnapshot> GetFriendlyUnits()
        {
            string selfId = aiNationId;
            return visibleUnits.FindAll(u => u.ownerId == selfId);
        }

        /// <summary>
        /// Calculates the military strength ratio: self military / sum of all enemies' military.
        /// Values above 1.0 mean the AI is stronger than its enemies combined.
        /// </summary>
        public float GetMilitaryRatio()
        {
            NationState self = GetSelfState();
            if (self == null || self.militaryStrength <= 0f) return 0f;

            float enemyMilitary = 0f;
            foreach (var nation in nations)
            {
                if (!nation.isSelf)
                    enemyMilitary += nation.militaryStrength;
            }

            return enemyMilitary > 0f ? self.militaryStrength / enemyMilitary : 999f;
        }

        /// <summary>
        /// Gets the average economic strength of all nations.
        /// </summary>
        public float GetAverageEconomicStrength()
        {
            if (nations.Count == 0) return 0f;
            float total = 0f;
            foreach (var n in nations) total += n.economicStrength;
            return total / nations.Count;
        }
    }

    /// <summary>
    /// Represents the outcome of a strategic action, used for reinforcement learning.
    /// </summary>
    [System.Serializable]
    public class GameResult
    {
        /// <summary>The action that was taken.</summary>
        public string actionId;

        /// <summary>Whether the action achieved its goal (positive outcome).</summary>
        public bool success;

        /// <summary>Numerical reward signal for RL training.</summary>
        public float reward;

        /// <summary>Value change in the AI's economic strength after this action.</summary>
        public float economicDelta;

        /// <summary>Value change in the AI's military strength after this action.</summary>
        public float militaryDelta;

        /// <summary>Value change in the AI's territory count after this action.</summary>
        public int territoryDelta;

        /// <summary>Turn number when this result was recorded.</summary>
        public int turnRecorded;

        /// <summary>
        /// Gets the composite reward for RL training.
        /// Combines success bonus with delta signals.
        /// </summary>
        public float GetCompositeReward(float economicWeight = 0.3f, float militaryWeight = 0.5f, float territoryWeight = 0.2f)
        {
            float composite = reward;
            composite += economicDelta * economicWeight * 0.01f;
            composite += militaryDelta * militaryWeight * 0.01f;
            composite += territoryDelta * territoryWeight * 0.5f;

            if (success) composite += 1f;

            return composite;
        }
    }
}
