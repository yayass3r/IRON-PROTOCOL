// ============================================================================================
// IRON PROTOCOL - ServiceLocator.cs
// Static service locator for dependency injection without a heavy DI framework.
// Allows systems to register and retrieve services by type.
// ============================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.Core
{
    /// <summary>
    /// A lightweight, static service locator for registering and retrieving game services.
    /// <para>
    /// Provides <see cref="Register{T}"/> and <see cref="Get{T}"/> methods for any type,
    /// enabling decoupled access to managers and subsystems throughout the application.
    /// </para>
    /// <example>
    /// <code>
    /// ServiceLocator.Register&lt;IHexGrid&gt;(myGrid);
    /// var grid = ServiceLocator.Get&lt;IHexGrid&gt;();
    /// </code>
    /// </example>
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// Registers a service instance under its concrete or interface type.
        /// If a service of the same type already exists, it will be replaced and
        /// a warning will be logged.
        /// </summary>
        /// <typeparam name="T">The type (interface or concrete) to register the service as.</typeparam>
        /// <param name="service">The service instance to store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                Debug.LogError($"[ServiceLocator] Cannot register a null service of type {typeof(T).Name}.");
                throw new ArgumentNullException(nameof(service));
            }

            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Service of type {type.Name} is already registered. Overwriting.");
                _services[type] = service;
            }
            else
            {
                _services.Add(type, service);
                Debug.Log($"[ServiceLocator] Registered service: {type.Name}");
            }
        }

        /// <summary>
        /// Retrieves the registered service instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <returns>The registered service instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no service of the requested type has been registered.
        /// </exception>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            Debug.LogError($"[ServiceLocator] No service of type {type.Name} is registered.");
            throw new InvalidOperationException(
                $"[ServiceLocator] Service of type {type.Name} has not been registered.");
        }

        /// <summary>
        /// Attempts to retrieve a registered service without throwing on failure.
        /// </summary>
        /// <typeparam name="T">The type of service to retrieve.</typeparam>
        /// <param name="service">
        /// When this method returns, contains the service instance if found; otherwise, null.
        /// </param>
        /// <returns><c>true</c> if the service was found; otherwise, <c>false</c>.</returns>
        public static bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var result))
            {
                service = (T)result;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Checks whether a service of the specified type is currently registered.
        /// </summary>
        /// <typeparam name="T">The type to check.</typeparam>
        /// <returns><c>true</c> if a service of type <typeparamref name="T"/> is registered.</returns>
        public static bool HasService<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Removes all registered services. Call during scene transitions or application quit
        /// to prevent stale references.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
            Debug.Log("[ServiceLocator] All services cleared.");
        }
    }
}
