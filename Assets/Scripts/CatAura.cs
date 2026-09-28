using UnityEngine;

/// <summary>
/// Makes detection easy to see: a glowing disc around the cat showing where the
/// mice run away from, and a border around the whole screen while the cat is in view.
/// </summary>
[RequireComponent(typeof(CatDetector))]
public class CatAura : MonoBehaviour
{
    public Color color = new(1f, 0.15f, 0.75f);

    [Tooltip("Disc radius in world units. Replaced by the mice's flight distance when there's a MouseSpawner")]
    public float radius = 2f;

    public bool showScreenBorder = true;

    const float FadeSpeed = 8f;
    const float PulseSpeed = 8f;

    CatDetector _detector;
    SpriteRenderer _disc;
    float _visibility;

    void Awake()
    {
        _detector = GetComponent<CatDetector>();

        _disc = new GameObject("Cat Aura").AddComponent<SpriteRenderer>();
        _disc.sprite = CreateDiscSprite();
        _disc.sortingOrder = -1; // under the mice
        _disc.color = Color.clear;
    }

    void Start()
    {
        var spawner = FindAnyObjectByType<MouseSpawner>();
        if (spawner != null && spawner.mousePrefab != null && spawner.mousePrefab.TryGetComponent(out MouseMovement mouse))
            radius = mouse.flightDistance;
    }

    void Update()
    {
        // Unscaled time so it keeps working while calibration pauses the game
        _visibility = Mathf.MoveTowards(_visibility, _detector.HasCat ? 1 : 0, Time.unscaledDeltaTime * FadeSpeed);
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * PulseSpeed);

        _disc.transform.position = _detector.GetCatWorldPosition();
        _disc.transform.localScale = Vector3.one * radius;
        _disc.color = new Color(color.r, color.g, color.b, _visibility * pulse);
    }

    void OnGUI()
    {
        if (!showScreenBorder || _visibility <= 0)
            return;

        // Draw behind the calibration screen
        GUI.depth = 10;
        var previous = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, _visibility);

        float w = Screen.width, h = Screen.height, t = Mathf.Min(w, h) * 0.04f;
        GUI.DrawTexture(new Rect(0, 0, w, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, h - t, w, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 0, t, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(w - t, 0, t, h), Texture2D.whiteTexture);

        GUI.color = previous;
    }

    void OnDestroy()
    {
        if (_disc != null)
        {
            Destroy(_disc.sprite.texture);
            Destroy(_disc.sprite);
            Destroy(_disc.gameObject);
        }
    }

    /// <summary>
    /// A white disc of radius 1 world unit: solid rim, translucent fill that
    /// brightens towards the rim, and a solid dot at the tracked point.
    /// </summary>
    static Sprite CreateDiscSprite()
    {
        const int size = 256;
        const float half = size / 2f;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // 0 at the center, 1 at the rim
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
            float alpha = d > 1 ? 0
                : d > 0.9f ? Mathf.Clamp01((1 - d) * half)
                : d < 0.06f ? 1
                : 0.2f + 0.3f * d * d;
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), half);
    }
}
