// ============================================================================================
// IRON PROTOCOL - MapCameraController.cs
// MonoBehaviour providing touch-based camera controls for mobile:
// pinch-to-zoom, one-finger drag-to-pan, double-tap-to-focus.
// Smooth interpolation via Mathf.Lerp in Update.
// ============================================================================================

using UnityEngine;

namespace IronProtocol.HexMap
{
    /// <summary>
    /// Touch-based camera controller for the hex map, optimized for mobile devices.
    /// <para>
    /// Supports: pinch-to-zoom, single-finger drag-to-pan, and double-tap-to-focus.
    /// All movements are smoothly interpolated. Camera bounds are automatically
    /// calculated from the <see cref="HexGrid"/> extents.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MapCameraController : MonoBehaviour
    {
        // ----------------------------------------------------------------------------------------
        // Configuration
        // ----------------------------------------------------------------------------------------

        [Header("Zoom Settings")]
        [Tooltip("Minimum orthographic size (zoomed in).")]
        [SerializeField, Range(0.1f, 5f)]
        private float minZoom = 0.3f;

        [Tooltip("Maximum orthographic size (zoomed out).")]
        [SerializeField, Range(1f, 20f)]
        private float maxZoom = 3.0f;

        [Tooltip("Zoom speed multiplier for pinch gestures.")]
        [SerializeField, Range(0.5f, 5f)]
        private float zoomSpeed = 1.0f;

        [Header("Pan Settings")]
        [Tooltip("Pan speed multiplier for drag gestures.")]
        [SerializeField, Range(0.1f, 5f)]
        private float panSpeed = 1.0f;

        [Header("Smoothing")]
        [Tooltip("Smoothing factor for camera position (0 = instant, higher = smoother).")]
        [SerializeField, Range(1f, 30f)]
        private float positionSmoothTime = 8f;

        [Tooltip("Smoothing factor for camera zoom (0 = instant, higher = smoother).")]
        [SerializeField, Range(1f, 30f)]
        private float zoomSmoothTime = 8f;

        [Header("Double-Tap Focus")]
        [Tooltip("Maximum time between two taps to register a double-tap (seconds).")]
        [SerializeField, Range(0.1f, 0.6f)]
        private float doubleTapThreshold = 0.3f;

        [Tooltip("Zoom level to snap to when double-tapping to focus.")]
        [SerializeField, Range(0.3f, 3f)]
        private float focusZoomLevel = 1.0f;

        [Header("Bounds")]
        [Tooltip("Extra padding around the grid bounds (world units).")]
        [SerializeField, Range(0f, 20f)]
        private float boundsPadding = 2f;

        [Header("References")]
        [Tooltip("The HexGrid used to calculate camera bounds. Auto-found if null.")]
        [SerializeField]
        private HexGrid hexGrid;

        // ----------------------------------------------------------------------------------------
        // Runtime State
        // ----------------------------------------------------------------------------------------

        private Camera _camera;
        private float _currentOrthoSize;
        private float _targetOrthoSize;

        private Vector3 _targetPosition;
        private Vector3 _currentVelocity; // for smoothing

        // Touch tracking.
        private bool _isDragging;
        private bool _isPinching;
        private Vector2 _lastSingleTouchPos;
        private float _lastPinchDistance;

        // Double-tap detection.
        private float _lastTapTime;
        private int _lastTapFingerId = -1;

        // Bounds.
        private Bounds _gridBounds;
        private bool _boundsInitialized;

        /// <summary>
        /// Gets whether the camera is currently being dragged by the user.
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Gets whether the camera is currently being pinched (zoomed) by the user.
        /// </summary>
        public bool IsPinching => _isPinching;

        // ----------------------------------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------------------------------

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _currentOrthoSize = _camera.orthographicSize;
            _targetOrthoSize = _currentOrthoSize;
            _targetPosition = transform.position;
        }

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindAnyObjectByType<HexGrid>();
            }

            if (hexGrid != null)
            {
                CalculateBounds();
            }
        }

        private void Update()
        {
            HandleTouchInput();
            SmoothUpdate();
            EnforceBounds();
        }

        // ----------------------------------------------------------------------------------------
        // Touch Input
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Processes touch input each frame, detecting drag, pinch, and double-tap gestures.
        /// </summary>
        private void HandleTouchInput()
        {
            if (Input.touchCount == 0)
            {
                _isDragging = false;
                _isPinching = false;
                return;
            }

            switch (Input.touchCount)
            {
                case 1:
                    HandleSingleTouch(Input.GetTouch(0));
                    break;

                case 2:
                    _isDragging = false;
                    HandlePinch(Input.GetTouch(0), Input.GetTouch(1));
                    break;

                default:
                    // 3+ fingers: ignore.
                    _isDragging = false;
                    _isPinching = false;
                    break;
            }
        }

        /// <summary>
        /// Handles single-finger touch: drag to pan, detect double-tap to focus.
        /// </summary>
        private void HandleSingleTouch(Touch touch)
        {
            _isPinching = false;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // Double-tap detection.
                    if (touch.fingerId == _lastTapFingerId &&
                        (Time.time - _lastTapTime) < doubleTapThreshold)
                    {
                        FocusOnPosition(touch.position);
                        _lastTapTime = 0f; // Reset to prevent triple-tap.
                    }
                    else
                    {
                        _lastTapTime = Time.time;
                        _lastTapFingerId = touch.fingerId;
                    }

                    _lastSingleTouchPos = touch.position;
                    _isDragging = true;
                    break;

                case TouchPhase.Moved:
                    if (_isDragging)
                    {
                        Vector2 delta = touch.position - _lastSingleTouchPos;
                        PanCamera(delta);
                        _lastSingleTouchPos = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    _isDragging = false;
                    break;
            }
        }

        /// <summary>
        /// Handles two-finger pinch gesture for zooming.
        /// </summary>
        private void HandlePinch(Touch touch1, Touch touch2)
        {
            _isPinching = true;

            float currentDistance = Vector2.Distance(touch1.position, touch2.position);

            if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
            {
                _lastPinchDistance = currentDistance;
                return;
            }

            if (currentDistance <= 0f) return;

            float pinchDelta = _lastPinchDistance - currentDistance;
            float zoomDelta = pinchDelta * zoomSpeed * 0.01f;

            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + zoomDelta, minZoom, maxZoom);
            _lastPinchDistance = currentDistance;
        }

        // ----------------------------------------------------------------------------------------
        // Camera Actions
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Pans the camera by the given screen-space pixel delta.
        /// Converts to world space using the camera's orthographic size.
        /// </summary>
        /// <param name="screenDelta">Screen-space movement delta in pixels.</param>
        private void PanCamera(Vector2 screenDelta)
        {
            float worldUnitsPerPixel = (_camera.orthographicSize * 2f) / Screen.height;

            Vector3 panOffset = new Vector3(
                -screenDelta.x * worldUnitsPerPixel * panSpeed,
                0f,
                -screenDelta.y * worldUnitsPerPixel * panSpeed
            );

            _targetPosition += panOffset;
        }

        /// <summary>
        /// Smoothly moves the camera to focus on the world position under the
        /// given screen-space point (double-tap location).
        /// </summary>
        /// <param name="screenPos">Screen position to focus on.</param>
        private void FocusOnPosition(Vector2 screenPos)
        {
            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                _targetPosition = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            }
            else
            {
                // Fallback: convert screen position to world at y=0.
                Vector3 worldPos = _camera.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, -_camera.transform.position.y));
                _targetPosition = new Vector3(worldPos.x, transform.position.y, worldPos.z);
            }

            _targetOrthoSize = focusZoomLevel;
            Debug.Log($"[MapCameraController] Focusing on position: {_targetPosition}");
        }

        /// <summary>
        /// Programmatically centers the camera on a world-space position.
        /// </summary>
        /// <param name="worldPosition">The world position to center on.</param>
        /// <param name="zoom">Optional zoom level. If 0, uses current zoom.</param>
        public void CenterOnPosition(Vector3 worldPosition, float zoom = 0f)
        {
            _targetPosition = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
            if (zoom > 0f)
            {
                _targetOrthoSize = Mathf.Clamp(zoom, minZoom, maxZoom);
            }
        }

        // ----------------------------------------------------------------------------------------
        // Smoothing & Bounds
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Applies smooth interpolation to the camera's position and orthographic size.
        /// Called every frame in <see cref="Update"/>.
        /// </summary>
        private void SmoothUpdate()
        {
            float t = Time.deltaTime;

            // Smooth position.
            transform.position = Vector3.Lerp(
                transform.position, _targetPosition, t * positionSmoothTime);

            // Smooth zoom.
            _currentOrthoSize = Mathf.Lerp(
                _currentOrthoSize, _targetOrthoSize, t * zoomSmoothTime);
            _camera.orthographicSize = Mathf.Clamp(_currentOrthoSize, minZoom, maxZoom);
        }

        /// <summary>
        /// Clamps the camera's target position so the viewport does not scroll
        /// beyond the calculated grid bounds.
        /// </summary>
        private void EnforceBounds()
        {
            if (!_boundsInitialized) return;

            float vertExtent = _camera.orthographicSize;
            float horizExtent = vertExtent * _camera.aspect;

            float minX = _gridBounds.min.x + horizExtent - boundsPadding;
            float maxX = _gridBounds.max.x - horizExtent + boundsPadding;
            float minZ = _gridBounds.min.z + vertExtent - boundsPadding;
            float maxZ = _gridBounds.max.z + vertExtent + boundsPadding;

            _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);
            _targetPosition.z = Mathf.Clamp(_targetPosition.z, minZ, maxZ);
        }

        /// <summary>
        /// Calculates the grid bounds from the referenced <see cref="HexGrid"/>.
        /// Must be called after the grid has been generated.
        /// </summary>
        public void CalculateBounds()
        {
            if (hexGrid == null) return;

            float hexSize = hexGrid.HexSize;
            Vector3 origin = hexGrid.GridOrigin;

            // Estimate the world-space extents of the grid.
            float halfWidth = (hexGrid.Width - 1) * hexSize * 1.5f + hexSize;
            float halfHeight = (hexGrid.Height - 1) * hexSize * 0.8660254037844386f + hexSize * 0.8660254037844386f;

            Vector3 center = new Vector3(
                origin.x + (hexGrid.Width - 1) * hexSize * 0.75f,
                0f,
                origin.z + (hexGrid.Height - 1) * hexSize * 0.8660254037844386f * 0.5f
            );

            _gridBounds = new Bounds(center, new Vector3(halfWidth * 2f, 0f, halfHeight * 2f));
            _boundsInitialized = true;

            Debug.Log($"[MapCameraController] Grid bounds calculated: {_gridBounds}");
        }

        // ----------------------------------------------------------------------------------------
        // Editor Support
        // ----------------------------------------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_boundsInitialized)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
                Gizmos.DrawWireCube(_gridBounds.center, new Vector3(_gridBounds.size.x, 1f, _gridBounds.size.z));
            }
        }
#endif
    }
}
