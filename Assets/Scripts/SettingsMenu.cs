using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared camera and cat-detection settings for the title and hunt scenes.</summary>
public class SettingsMenu : MonoBehaviour
{
    const string DetectionPrefsKey = "Settings.showCatDetection";
    const string CameraPrefsKey = "CatVision.camera";
    const string RequestCalibrationPrefsKey = "CameraCalibration.requestOnNextGame";

    CatVision _vision;
    CameraCalibration _calibration;
    CatAura _aura;
    bool _open;
    bool _showDetection;
    float _timeScaleBeforeOpening;
    bool _titleButton;
    static Texture2D _panelTexture;
    GameObject _inputBlocker;

    public bool ShowTitleButton
    {
        set => _titleButton = value;
    }

    void Awake()
    {
        _showDetection = PlayerPrefs.GetInt(DetectionPrefsKey, 0) != 0;
        RefreshReferences();
        ApplyDetectionVisibility();
        CreateInputBlocker();
    }

    void RefreshReferences()
    {
        _vision = FindAnyObjectByType<CatVision>();
        _calibration = FindAnyObjectByType<CameraCalibration>();
        _aura = FindAnyObjectByType<CatAura>();
    }

    void CreateInputBlocker()
    {
        _inputBlocker = new GameObject("Settings Input Blocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        var canvas = _inputBlocker.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10000;

        var rect = (RectTransform)_inputBlocker.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = _inputBlocker.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        _inputBlocker.SetActive(false);
    }

    void OnGUI()
    {
        var currentEvent = Event.current;
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape
            && (_open || (!_titleButton && (_calibration == null || !_calibration.IsCalibrating))))
        {
            if (_open)
                Close();
            else
                Open();
            currentEvent.Use();
        }

        if (_titleButton && !_open)
        {
            float buttonWidth = Mathf.Clamp(Screen.width * 0.15f, 150, 240);
            var rect = new Rect(Screen.width - buttonWidth - 32, 32, buttonWidth, 48);
            if (GUI.Button(rect, "Settings", ButtonStyle()))
                Open();
        }

        if (!_open)
            return;

        GUI.depth = -20;
        GUI.color = new Color(0, 0, 0, 0.78f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float panelWidth = Mathf.Min(620, Screen.width - 32);
        float panelHeight = Mathf.Min(440, Screen.height - 32);
        var panel = new Rect((Screen.width - panelWidth) / 2, (Screen.height - panelHeight) / 2, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none, BoxStyle());

        float pad = Mathf.Clamp(panelWidth * 0.07f, 24, 44);
        float rowHeight = Mathf.Clamp(Screen.height * 0.07f, 42, 58);
        float x = panel.x + pad;
        float width = panel.width - pad * 2;
        float y = panel.y + pad * 0.65f;
        GUI.Label(new Rect(x, y, width, rowHeight), "Settings", HeaderStyle());
        y += rowHeight * 1.25f;

        GUI.Label(new Rect(x, y, width, rowHeight * 0.6f), "Camera", SectionStyle());
        y += rowHeight * 0.65f;
        DrawCameraSelection(new Rect(x, y, width, rowHeight));
        y += rowHeight * 1.15f;

        if (GUI.Button(new Rect(x, y, width, rowHeight), "Recalibrate camera", ButtonStyle()))
        {
            RefreshReferences();
            if (_calibration != null)
            {
                Close();
                _calibration.BeginCalibration();
            }
            else if (_titleButton)
            {
                PlayerPrefs.SetInt(RequestCalibrationPrefsKey, 1);
                PlayerPrefs.Save();
                FindAnyObjectByType<MainMenu>()?.StartGame();
            }
        }
        y += rowHeight * 1.3f;

        bool showDetection = GUI.Toggle(new Rect(x, y, width, rowHeight), _showDetection, "Show cat detection", ToggleStyle());
        if (showDetection != _showDetection)
        {
            _showDetection = showDetection;
            PlayerPrefs.SetInt(DetectionPrefsKey, _showDetection ? 1 : 0);
            PlayerPrefs.Save();
            ApplyDetectionVisibility();
        }
        y += rowHeight * 1.1f;
        GUI.Label(new Rect(x + 24, y, width - 24, rowHeight), "Shows the webcam preview and the cat aura during play.", HintStyle());

        float closeWidth = Mathf.Min(180, width);
        if (!_titleButton && GUI.Button(new Rect(panel.x + pad, panel.yMax - pad - rowHeight, closeWidth, rowHeight), "Quit to Start Menu", ButtonStyle()))
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }
        if (GUI.Button(new Rect(panel.xMax - pad - closeWidth, panel.yMax - pad - rowHeight, closeWidth, rowHeight), "Done", ButtonStyle()))
            Close();
    }

    void DrawCameraSelection(Rect area)
    {
        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            GUI.Label(area, "No webcam found", HintStyle());
            return;
        }

        string current = _vision != null && _vision.CameraTexture != null
            ? _vision.CameraTexture.deviceName
            : PlayerPrefs.GetString(CameraPrefsKey, devices[0].name);
        if (ArrayUtilityContains(devices, current) == false)
            current = devices[0].name;

        string[] names = devices.Select(device => device.name).ToArray();
        int selected = Mathf.Max(0, System.Array.IndexOf(names, current));
        if (devices.Length == 1)
        {
            GUI.Label(area, names[0], ButtonStyle());
            if (PlayerPrefs.GetString(CameraPrefsKey, "") != names[0])
            {
                PlayerPrefs.SetString(CameraPrefsKey, names[0]);
                PlayerPrefs.Save();
            }
            return;
        }

        int next = GUI.SelectionGrid(area, selected, names, names.Length, ButtonStyle());
        if (next != selected && next >= 0 && next < names.Length)
        {
            PlayerPrefs.SetString(CameraPrefsKey, names[next]);
            PlayerPrefs.Save();
            RefreshReferences();
            _vision?.UseCamera(names[next]);
        }
    }

    static bool ArrayUtilityContains(WebCamDevice[] devices, string name)
    {
        foreach (var device in devices)
            if (device.name == name)
                return true;
        return false;
    }

    void ApplyDetectionVisibility()
    {
        RefreshReferences();
        if (_calibration != null)
            _calibration.showPreview = _showDetection;
        if (_aura != null)
            _aura.SetDetectionVisible(_showDetection);
    }

    void Open()
    {
        if (_open)
            return;
        RefreshReferences();
        _timeScaleBeforeOpening = Time.timeScale;
        if (!_titleButton)
            Time.timeScale = 0;
        _open = true;
        _inputBlocker.SetActive(true);
    }

    void Close()
    {
        if (!_open)
            return;
        _open = false;
        _inputBlocker.SetActive(false);
        Time.timeScale = _timeScaleBeforeOpening;
    }

    static GUIStyle HeaderStyle() => new(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = Mathf.RoundToInt(Screen.height * 0.055f),
        fontStyle = FontStyle.Bold,
        normal = { textColor = Color.white },
    };

    static GUIStyle SectionStyle() => new(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleLeft,
        fontSize = Mathf.RoundToInt(Screen.height * 0.03f),
        fontStyle = FontStyle.Bold,
        normal = { textColor = Color.white },
    };

    static GUIStyle HintStyle() => new(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleLeft,
        fontSize = Mathf.RoundToInt(Screen.height * 0.021f),
        normal = { textColor = new Color(0.82f, 0.82f, 0.82f) },
    };

    static GUIStyle ButtonStyle() => new(GUI.skin.button)
    {
        fontSize = Mathf.RoundToInt(Screen.height * 0.026f),
        wordWrap = true,
    };

    static GUIStyle ToggleStyle() => new(GUI.skin.toggle)
    {
        fontSize = Mathf.RoundToInt(Screen.height * 0.03f),
        normal = { textColor = Color.white },
    };

    static GUIStyle BoxStyle() => new(GUI.skin.box)
    {
        normal = { background = MakePanelTexture() },
    };

    static Texture2D MakePanelTexture()
    {
        if (_panelTexture == null)
        {
            _panelTexture = new Texture2D(1, 1);
            _panelTexture.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.15f, 1));
            _panelTexture.Apply();
        }
        return _panelTexture;
    }
}
