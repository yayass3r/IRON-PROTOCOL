// ============================================================================================
// IRON PROTOCOL - EventBus.cs
// Singleton event bus for decoupled communication between game systems.
// Uses C# events and delegates. Subscribe, unsubscribe, and invoke from any system.
// ============================================================================================

using System;
using UnityEngine;

namespace IronProtocol.Core
{
    /// <summary>
    /// Lightweight data structures passed as arguments with game events.
    /// </summary>
    public static class EventPayloads
    {
        /// <summary>
        /// Payload for unit movement events, containing origin and destination coordinates.
        /// </summary>
        public struct UnitMovedArgs
        {
            public int UnitId;
            public Vector3Int FromCell;
            public Vector3Int ToCell;
        }

        /// <summary>
        /// Payload for unit destruction events.
        /// </summary>
        public struct UnitDestroyedArgs
        {
            public int UnitId;
            public int DestroyingNationId;
        }

        /// <summary>
        /// Payload for city capture events.
        /// </summary>
        public struct CityCapturedArgs
        {
            public string CityName;
            public int PreviousOwnerId;
            public int NewOwnerId;
        }

        /// <summary>
        /// Payload for market price change events.
        /// </summary>
        public struct MarketPriceChangedArgs
        {
            public string ResourceId;
            public float OldPrice;
            public float NewPrice;
        }

        /// <summary>
        /// Payload for war declaration events.
        /// </summary>
        public struct WarDeclaredArgs
        {
            public int AggressorNationId;
            public int DefenderNationId;
        }

        /// <summary>
        /// Payload for weather change events.
        /// </summary>
        public struct WeatherChangedArgs
        {
            public string PreviousWeather;
            public string NewWeather;
        }
    }

    /// <summary>
    /// Enumeration of all available weather conditions in IRON PROTOCOL.
    /// </summary>
    public enum WeatherCondition
    {
        Clear,
        Rain,
        Snow,
        Fog,
        Storm
    }

    /// <summary>
    /// Singleton event bus providing decoupled communication between game systems.
    /// <para>
    /// All events use the standard C# event/delegate pattern. Systems subscribe to events
    /// they care about and are notified when those events fire, without requiring direct
    /// references between systems.
    /// </para>
    /// <para>Usage: <c>EventBus.Instance.OnTurnStarted += MyHandler;</c></para>
    /// </summary>
    public class EventBus : MonoBehaviour
    {
        private static EventBus _instance;

        /// <summary>
        /// Gets the singleton instance of the EventBus. Creates a persistent
        /// GameObject if one does not already exist.
        /// </summary>
        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[EventBus]");
                    _instance = go.AddComponent<EventBus>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[EventBus] Singleton instance created automatically.");
                }
                return _instance;
            }
        }

        // ----------------------------------------------------------------------------------------
        // Events
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Fired when a new turn begins. Passes the new turn number (int).
        /// </summary>
        public event Action<int> OnTurnStarted;

        /// <summary>
        /// Fired when the current turn ends. Passes the turn number that just ended (int).
        /// </summary>
        public event Action<int> OnTurnEnded;

        /// <summary>
        /// Fired when a unit moves from one cell to another.
        /// </summary>
        public event Action<EventPayloads.UnitMovedArgs> OnUnitMoved;

        /// <summary>
        /// Fired when a unit is destroyed in combat or otherwise removed.
        /// </summary>
        public event Action<EventPayloads.UnitDestroyedArgs> OnUnitDestroyed;

        /// <summary>
        /// Fired when a city is captured by a different nation.
        /// </summary>
        public event Action<EventPayloads.CityCapturedArgs> OnCityCaptured;

        /// <summary>
        /// Fired when any market resource price changes.
        /// </summary>
        public event Action<EventPayloads.MarketPriceChangedArgs> OnMarketPriceChanged;

        /// <summary>
        /// Fired when war is declared between two nations.
        /// </summary>
        public event Action<EventPayloads.WarDeclaredArgs> OnWarDeclared;

        /// <summary>
        /// Fired when the global weather condition changes.
        /// </summary>
        public event Action<EventPayloads.WeatherChangedArgs> OnWeatherChanged;

        // ----------------------------------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[EventBus] Duplicate instance detected. Destroying this copy.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                ClearAllEvents();
                _instance = null;
            }
        }

        // ----------------------------------------------------------------------------------------
        // Invocation helpers
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Raises the <see cref="OnTurnStarted"/> event with the specified turn number.
        /// </summary>
        /// <param name="turnNumber">The turn number that is starting.</param>
        public void RaiseTurnStarted(int turnNumber)
        {
            OnTurnStarted?.Invoke(turnNumber);
        }

        /// <summary>
        /// Raises the <see cref="OnTurnEnded"/> event with the specified turn number.
        /// </summary>
        /// <param name="turnNumber">The turn number that is ending.</param>
        public void RaiseTurnEnded(int turnNumber)
        {
            OnTurnEnded?.Invoke(turnNumber);
        }

        /// <summary>
        /// Raises the <see cref="OnUnitMoved"/> event.
        /// </summary>
        /// <param name="args">Movement details including unit ID and cell coordinates.</param>
        public void RaiseUnitMoved(EventPayloads.UnitMovedArgs args)
        {
            OnUnitMoved?.Invoke(args);
        }

        /// <summary>
        /// Raises the <see cref="OnUnitDestroyed"/> event.
        /// </summary>
        /// <param name="args">Destruction details including unit ID and responsible nation.</param>
        public void RaiseUnitDestroyed(EventPayloads.UnitDestroyedArgs args)
        {
            OnUnitDestroyed?.Invoke(args);
        }

        /// <summary>
        /// Raises the <see cref="OnCityCaptured"/> event.
        /// </summary>
        /// <param name="args">Capture details including city name and nation IDs.</param>
        public void RaiseCityCaptured(EventPayloads.CityCapturedArgs args)
        {
            OnCityCaptured?.Invoke(args);
        }

        /// <summary>
        /// Raises the <see cref="OnMarketPriceChanged"/> event.
        /// </summary>
        /// <param name="args">Price change details including resource ID and old/new prices.</param>
        public void RaiseMarketPriceChanged(EventPayloads.MarketPriceChangedArgs args)
        {
            OnMarketPriceChanged?.Invoke(args);
        }

        /// <summary>
        /// Raises the <see cref="OnWarDeclared"/> event.
        /// </summary>
        /// <param name="args">War details including aggressor and defender nation IDs.</param>
        public void RaiseWarDeclared(EventPayloads.WarDeclaredArgs args)
        {
            OnWarDeclared?.Invoke(args);
        }

        /// <summary>
        /// Raises the <see cref="OnWeatherChanged"/> event.
        /// </summary>
        /// <param name="args">Weather transition details.</param>
        public void RaiseWeatherChanged(EventPayloads.WeatherChangedArgs args)
        {
            OnWeatherChanged?.Invoke(args);
        }

        // ----------------------------------------------------------------------------------------
        // Cleanup
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Removes all subscribers from every event on the bus.
        /// <para>Call this during scene transitions or when resetting game state.</para>
        /// </summary>
        public void ClearAllEvents()
        {
            OnTurnStarted = null;
            OnTurnEnded = null;
            OnUnitMoved = null;
            OnUnitDestroyed = null;
            OnCityCaptured = null;
            OnMarketPriceChanged = null;
            OnWarDeclared = null;
            OnWeatherChanged = null;
        }
    }
}
