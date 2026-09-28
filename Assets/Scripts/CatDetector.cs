using UnityEngine;

/// <summary>
/// Tracks where the cat is in the game world, from the webcam detection mapped
/// through the projection calibration.
/// </summary>
public class CatDetector : MonoBehaviour
{
    [Header("Smoothing")]
    [Range(0f, 1f)] public float lerp = 0.6f;

    [Tooltip("Keep reporting the last position for this long when the cat is briefly lost")]
    public float dropoutHoldSeconds = 0.3f;

    private CatVision _vision;
    private CameraCalibration _calibration;

    private Camera _cam;
    private Vector3 _catWorldPos;
    private bool _hasCatPos = false;
    private float _lastSeenTime = float.NegativeInfinity;

    /// <summary>True while the cat is in view, or was a moment ago.</summary>
    public bool HasCat => _hasCatPos && Time.unscaledTime - _lastSeenTime <= dropoutHoldSeconds;

    void Awake()
    {
        _cam = Camera.main;
        Application.runInBackground = true;

        if (!TryGetComponent(out _vision))
            _vision = gameObject.AddComponent<CatVision>();
        if (!TryGetComponent(out _calibration))
            _calibration = gameObject.AddComponent<CameraCalibration>();
        if (!TryGetComponent(out CatAura _))
            gameObject.AddComponent<CatAura>();
    }

    void OnEnable() => _vision.ResultReady += OnVisionResult;
    void OnDisable() => _vision.ResultReady -= OnVisionResult;

    void OnVisionResult()
    {
        if (!_vision.HasCat || !_calibration.TryCameraToScreen(_vision.CatPosition, out var screen))
            return;

        // Convert to screen pixels, flipping Y since screen coordinates start at the bottom
        float sx = screen.x * Screen.width;
        float sy = Screen.height - screen.y * Screen.height;

        // Convert to world position
        Vector3 screenPoint = new Vector3(sx, sy, Mathf.Abs(_cam.transform.position.z));
        Vector3 newWorldPos = _cam.ScreenToWorldPoint(screenPoint);

        // Smooth while tracking, but jump straight to a cat that just reappeared
        _catWorldPos = HasCat ? Vector3.Lerp(_catWorldPos, newWorldPos, lerp) : newWorldPos;
        _hasCatPos = true;
        _lastSeenTime = Time.unscaledTime;
    }

    public Vector2 GetCatWorldPosition()
    {
        if (!_hasCatPos)
            return transform.position; // fallback

        return _catWorldPos;
    }

    private void OnDrawGizmos()
    {
        if (HasCat)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_catWorldPos, 0.1f);
        }
    }
}
