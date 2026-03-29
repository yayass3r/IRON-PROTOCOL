// ============================================================================================
// IRON PROTOCOL - ObjectPool.cs
// Generic Unity object pool for efficient GameObject reuse.
// Auto-expands when the pool is exhausted. Configurable initial size.
// ============================================================================================

using System.Collections.Generic;
using UnityEngine;

namespace IronProtocol.Core
{
    /// <summary>
    /// Configuration for an <see cref="ObjectPool"/> instance, defining the prefab
    /// to pool, initial capacity, and growth behavior.
    /// </summary>
    [System.Serializable]
    public class PoolConfig
    {
        /// <summary>
        /// The prefab that will be instantiated for each pooled object.
        /// </summary>
        [Tooltip("Prefab to instantiate for pooled objects.")]
        public GameObject Prefab;

        /// <summary>
        /// Number of objects created when the pool is first initialized.
        /// </summary>
        [Tooltip("Number of objects to pre-instantiate.")]
        [Min(1)]
        public int InitialSize = 10;

        /// <summary>
        /// If true, the pool will automatically create new instances when all
        /// pooled objects are in use. If false, <see cref="ObjectPool.Get"/> returns null.
        /// </summary>
        [Tooltip("Allow the pool to grow beyond InitialSize when exhausted.")]
        public bool AutoExpand = true;

        /// <summary>
        /// The maximum number of objects the pool is allowed to contain.
        /// Only enforced when <see cref="AutoExpand"/> is true. A value of 0 means unlimited.
        /// </summary>
        [Tooltip("Max pool size (0 = unlimited).")]
        [Min(0)]
        public int MaxSize = 0;
    }

    /// <summary>
    /// A generic, MonoBehaviour-based object pool that manages reuse of GameObject instances.
    /// <para>
    /// Attach this component to a manager GameObject and assign the <see cref="config"/>
    /// field in the Inspector. Call <see cref="Initialize"/> to warm the pool, then use
    /// <see cref="Get"/> and <see cref="Return"/> to acquire and release objects.
    /// </para>
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Configuration")]
        [SerializeField]
        private PoolConfig config;

        /// <summary>
        /// Gets the pool configuration. Can be set programmatically before initialization.
        /// </summary>
        public PoolConfig Config
        {
            get => config;
            set => config = value;
        }

        /// <summary>
        /// Gets the current number of inactive (available) objects in the pool.
        /// </summary>
        public int AvailableCount => _inactiveStack.Count;

        /// <summary>
        /// Gets the total number of objects ever created by this pool (active + inactive).
        /// </summary>
        public int TotalCount { get; private set; }

        private readonly Stack<GameObject> _inactiveStack = new Stack<GameObject>();
        private readonly List<GameObject> _activeObjects = new List<GameObject>();
        private bool _initialized;

        // ----------------------------------------------------------------------------------------
        // Initialization
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Warms the pool by pre-instantiating objects up to <see cref="PoolConfig.InitialSize"/>.
        /// Called automatically on <see cref="Start"/> if the config is assigned.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Debug.LogWarning("[ObjectPool] Already initialized. Call Clear() first if re-initializing.");
                return;
            }

            if (config == null || config.Prefab == null)
            {
                Debug.LogError("[ObjectPool] Cannot initialize without a valid PoolConfig and Prefab.");
                return;
            }

            for (int i = 0; i < config.InitialSize; i++)
            {
                CreatePooledObject();
            }

            _initialized = true;
            Debug.Log($"[ObjectPool] Initialized with {config.InitialSize} objects of type {config.Prefab.name}.");
        }

        private void Start()
        {
            if (!_initialized && config != null && config.Prefab != null)
            {
                Initialize();
            }
        }

        // ----------------------------------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Retrieves an available GameObject from the pool, activating it.
        /// If the pool is empty and auto-expand is enabled, a new instance is created.
        /// </summary>
        /// <param name="position">World position to place the activated object at.</param>
        /// <param name="rotation">World rotation to apply to the activated object.</param>
        /// <returns>
        /// A pooled GameObject, or <c>null</c> if the pool is exhausted and auto-expand is disabled.
        /// </returns>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance;

            if (_inactiveStack.Count > 0)
            {
                instance = _inactiveStack.Pop();
            }
            else if (config.AutoExpand)
            {
                if (config.MaxSize > 0 && TotalCount >= config.MaxSize)
                {
                    Debug.LogWarning("[ObjectPool] Max size reached. Cannot expand further.");
                    return null;
                }

                instance = CreatePooledObject();
            }
            else
            {
                Debug.LogWarning("[ObjectPool] Pool exhausted and auto-expand is disabled.");
                return null;
            }

            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);

            _activeObjects.Add(instance);
            return instance;
        }

        /// <summary>
        /// Retrieves an available GameObject from the pool using default position and rotation.
        /// </summary>
        /// <returns>A pooled GameObject, or <c>null</c> if unavailable.</returns>
        public GameObject Get()
        {
            return Get(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Returns a GameObject to the pool, deactivating it and resetting its transform.
        /// </summary>
        /// <param name="instance">The pooled GameObject to return.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
        public void Return(GameObject instance)
        {
            if (instance == null)
            {
                Debug.LogError("[ObjectPool] Cannot return a null instance to the pool.");
                throw new System.ArgumentNullException(nameof(instance));
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);
            _activeObjects.Remove(instance);
            _inactiveStack.Push(instance);
        }

        /// <summary>
        /// Returns all currently active objects to the pool.
        /// </summary>
        public void ReturnAll()
        {
            // Iterate over a copy since Return() modifies the list.
            var activeCopy = new List<GameObject>(_activeObjects);
            foreach (var obj in activeCopy)
            {
                Return(obj);
            }
        }

        /// <summary>
        /// Destroys all pooled objects (active and inactive) and resets the pool.
        /// Call this during cleanup or scene transitions.
        /// </summary>
        public void Clear()
        {
            foreach (var obj in _inactiveStack)
            {
                if (obj != null) Destroy(obj);
            }
            _inactiveStack.Clear();

            foreach (var obj in _activeObjects)
            {
                if (obj != null) Destroy(obj);
            }
            _activeObjects.Clear();

            TotalCount = 0;
            _initialized = false;

            Debug.Log("[ObjectPool] Pool cleared.");
        }

        // ----------------------------------------------------------------------------------------
        // Internal
        // ----------------------------------------------------------------------------------------

        private GameObject CreatePooledObject()
        {
            var instance = Instantiate(config.Prefab, transform);
            instance.SetActive(false);
            instance.name = $"{config.Prefab.name}_Pooled_{TotalCount}";
            _inactiveStack.Push(instance);
            TotalCount++;
            return instance;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
