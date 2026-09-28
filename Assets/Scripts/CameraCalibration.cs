using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps camera positions onto the projected screen. To calibrate, the screen
/// shows a marker in each corner and the camera feed in the middle; click where
/// each marker appears in the feed. The result is saved between runs.
///
/// Keys: C calibrate, V toggle the camera preview. While calibrating:
/// Enter save, R start over, Esc cancel.
/// </summary>
[RequireComponent(typeof(CatVision))]
public class CameraCalibration : MonoBehaviour
{
    const string PrefsKey = "CameraCalibration.corners";

    // Screen corners in click order, normalized with (0, 0) at the top-left
    static readonly Vector2[] ScreenCorners = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
    static readonly string[] CornerNames = { "top-left", "top-right", "bottom-right", "bottom-left" };

    [Tooltip("Start calibrating on launch if there is no saved calibration")]
    public bool calibrateIfMissing = true;

    public bool showPreview;

    [Serializable]
    class SavedCorners
    {
        public Vector2[] corners;
    }

    CatVision _vision;
    Homography _cameraToScreen;
    Homography _screenToCamera;
    bool _calibrated;
    bool _calibrating;
    float _timeScaleBeforeCalibrating;
    readonly List<Vector2> _clicks = new();

    public bool IsCalibrated => _calibrated;

    void Awake()
    {
        _vision = GetComponent<CatVision>();
        Load();
    }

    void Start()
    {
        if (calibrateIfMissing && !_calibrated)
            BeginCalibration();
    }

    /// <summary>
    /// Converts a normalized camera position to a normalized screen position, with
    /// (0, 0) at the top-left. Returns false if it falls outside the projection.
    /// Without a calibration the whole camera image counts as the screen.
    /// </summary>
    public bool TryCameraToScreen(Vector2 camera, out Vector2 screen)
    {
        screen = _calibrated ? _cameraToScreen.Map(camera) : camera;
        return screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1;
    }

    public void BeginCalibration()
    {
        if (_calibrating)
            return;
        _calibrating = true;
        _clicks.Clear();
        _timeScaleBeforeCalibrating = Time.timeScale;
        Time.timeScale = 0;
    }

    void EndCalibration()
    {
        _calibrating = false;
        Time.timeScale = _timeScaleBeforeCalibrating;
    }

    void Save()
    {
        var corners = _clicks.ToArray();
        if (!TryApply(corners))
        {
            Debug.LogWarning("CameraCalibration: those corners don't form a shape, try again.");
            _clicks.Clear();
            return;
        }

        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new SavedCorners { corners = corners }));
        PlayerPrefs.Save();
        EndCalibration();
    }

    void Load()
    {
        var saved = JsonUtility.FromJson<SavedCorners>(PlayerPrefs.GetString(PrefsKey, "{}"));
        if (saved?.corners is { Length: 4 })
            TryApply(saved.corners);
    }

    bool TryApply(Vector2[] cameraCorners)
    {
        if (!Homography.TryFit(cameraCorners, ScreenCorners, out var cameraToScreen)
            || !Homography.TryFit(ScreenCorners, cameraCorners, out var screenToCamera))
            return false;

        _cameraToScreen = cameraToScreen;
        _screenToCamera = screenToCamera;
        _calibrated = true;
        return true;
    }

    void OnGUI()
    {
        HandleKeys(Event.current);

        if (_calibrating)
            DrawCalibration();
        else if (showPreview)
            DrawPreview();
    }

    void HandleKeys(Event e)
    {
        if (e.type != EventType.KeyDown)
            return;

        switch (e.keyCode)
        {
            case KeyCode.C:
                BeginCalibration();
                break;
            case KeyCode.V:
                showPreview = !showPreview;
                break;
            case KeyCode.R when _calibrating:
                _clicks.Clear();
                break;
            case KeyCode.Return or KeyCode.KeypadEnter when _calibrating && _clicks.Count == 4:
                Save();
                break;
            case KeyCode.Escape when _calibrating:
                EndCalibration();
                break;
            default:
                return;
        }
        e.Use();
    }

    void DrawCalibration()
    {
        float w = Screen.width, h = Screen.height;
        FillRect(new Rect(0, 0, w, h), Color.black);

        // Corner markers for the camera to see; the next one to click is yellow
        float marker = Mathf.Min(w, h) * 0.08f;
        for (int i = 0; i < 4; i++)
        {
            var corner = ScreenCorners[i];
            var rect = new Rect(corner.x * (w - marker), corner.y * (h - marker), marker, marker);
            FillRect(rect, i < _clicks.Count ? Color.green : i == _clicks.Count ? Color.yellow : Color.white);
        }

        var style = LabelStyle();
        var instructions = _clicks.Count < 4
            ? $"In the camera view, click the {CornerNames[_clicks.Count]} corner marker"
            : "Press Enter to save";
        GUI.Label(new Rect(0, marker * 1.2f, w, style.fontSize * 1.6f), instructions, style);
        GUI.Label(new Rect(0, h - marker * 1.2f - style.fontSize * 1.6f, w, style.fontSize * 1.6f),
            _calibrated ? "R start over · Esc cancel" : "R start over · Esc skip (use the whole camera image)", style);

        DrawCameraPicker(new Rect(w * 0.15f, h * 0.75f, w * 0.7f, style.fontSize * 1.5f));

        var feed = CameraRect(new Rect(w * 0.15f, h * 0.18f, w * 0.7f, h * 0.55f));
        if (feed == null)
            return;

        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && _clicks.Count < 4 && feed.Value.Contains(e.mousePosition))
        {
            _clicks.Add(Rect.PointToNormalized(feed.Value, e.mousePosition));
            e.Use();
        }

        for (int i = 0; i < _clicks.Count; i++)
        {
            var p = Rect.NormalizedToPoint(feed.Value, _clicks[i]);
            FillRect(new Rect(p.x - 5, p.y - 5, 10, 10), Color.green);
            GUI.Label(new Rect(p.x + 8, p.y - 30, 40, 30), (i + 1).ToString(), style);
        }
    }

    /// <summary>One button per webcam, for when the computer has more than one.</summary>
    void DrawCameraPicker(Rect area)
    {
        var devices = WebCamTexture.devices;
        if (devices.Length < 2)
            return;

        var current = _vision.CameraTexture != null ? _vision.CameraTexture.deviceName : null;
        var style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(area.height * 0.45f) };
        float width = area.width / devices.Length;
        for (int i = 0; i < devices.Length; i++)
        {
            var name = devices[i].name;
            var button = new Rect(area.x + i * width + 4, area.y, width - 8, area.height);
            if (GUI.Toggle(button, name == current, name, style) && name != current)
            {
                _vision.UseCamera(name);
                _clicks.Clear();
            }
        }
    }

    void DrawPreview()
    {
        float width = Screen.width * 0.25f;
        var feed = CameraRect(new Rect(Screen.width - width - 16, 16, width, width));
        if (feed == null)
            return;

        if (_calibrated)
        {
            foreach (var corner in ScreenCorners)
            {
                var p = Rect.NormalizedToPoint(feed.Value, _screenToCamera.Map(corner));
                FillRect(new Rect(p.x - 3, p.y - 3, 6, 6), Color.yellow);
            }
        }

        if (_vision.HasCat)
        {
            var box = _vision.CatBox;
            var min = Rect.NormalizedToPoint(feed.Value, box.min);
            var max = Rect.NormalizedToPoint(feed.Value, box.max);
            OutlineRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y), Color.green);
        }

        var style = LabelStyle();
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize /= 2;
        GUI.Label(new Rect(feed.Value.x, feed.Value.yMax + 4, feed.Value.width, style.fontSize * 1.6f),
            $"cat {_vision.Confidence:0.00}" + (_calibrated ? "" : " · uncalibrated, press C"), style);
    }

    /// <summary>Draws the camera feed fitted inside the area and returns where it went.</summary>
    Rect? CameraRect(Rect area)
    {
        var camera = _vision.CameraTexture;
        if (camera == null || camera.width <= 16)
        {
            GUI.Label(area, "Waiting for camera…", LabelStyle());
            return null;
        }

        float aspect = (float)camera.width / camera.height;
        var rect = area.width / area.height > aspect
            ? new Rect(area.x + (area.width - area.height * aspect) / 2, area.y, area.height * aspect, area.height)
            : new Rect(area.x, area.y + (area.height - area.width / aspect) / 2, area.width, area.width / aspect);

        if (camera.videoVerticallyMirrored)
            GUI.DrawTextureWithTexCoords(rect, camera, new Rect(0, 1, 1, -1));
        else
            GUI.DrawTexture(rect, camera);
        return rect;
    }

    static GUIStyle LabelStyle() => new(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = Mathf.RoundToInt(Screen.height * 0.03f),
        normal = { textColor = Color.white },
    };

    static void FillRect(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    static void OutlineRect(Rect rect, Color color, float thickness = 2)
    {
        FillRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        FillRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        FillRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        FillRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
