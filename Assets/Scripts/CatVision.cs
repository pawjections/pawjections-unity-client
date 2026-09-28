using System;
using System.Collections;
using Unity.InferenceEngine;
using UnityEngine;

/// <summary>
/// Finds the cat in the webcam feed with YOLOX-Tiny, a COCO object detector
/// (Apache-2.0, https://github.com/Megvii-BaseDetection/YOLOX).
/// Positions are normalized camera coordinates: (0, 0) is the top-left of the
/// camera image and (1, 1) the bottom-right.
/// </summary>
public class CatVision : MonoBehaviour
{
    const string ModelResource = "yolox_tiny";
    const int InputSize = 416;
    static readonly int[] Strides = { 8, 16, 32 };
    const int CocoCat = 15;
    const float PadValue = 114f;
    const string CameraPrefsKey = "CatVision.camera";

    [Range(0f, 1f)] public float confidenceThreshold = 0.4f;

    /// <summary>Raised on the main thread each time a camera frame has been analyzed.</summary>
    public event Action ResultReady;

    public WebCamTexture CameraTexture { get; private set; }
    public bool HasCat { get; private set; }
    public float Confidence { get; private set; }
    public Rect CatBox { get; private set; }
    public Vector2 CatPosition => CatBox.center;

    Worker _worker;
    Tensor<float> _input;
    Tensor<float> _pending;
    TextureTransform _transform;
    int _imageWidth, _imageHeight;

    IEnumerator Start()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("CatVision: camera permission denied.");
            yield break;
        }

        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("CatVision: no webcam found.");
            yield break;
        }

        yield return OpenCamera(PlayerPrefs.GetString(CameraPrefsKey, ""));
    }

    /// <summary>Switches to another webcam and remembers it for next time.</summary>
    public void UseCamera(string cameraName)
    {
        StopAllCoroutines();
        StartCoroutine(OpenCamera(cameraName));
    }

    IEnumerator OpenCamera(string cameraName)
    {
        CloseCamera();

        // A camera that's no longer plugged in falls back to the first one
        var devices = WebCamTexture.devices;
        if (Array.FindIndex(devices, d => d.name == cameraName) < 0)
            cameraName = devices[0].name;

        CameraTexture = new WebCamTexture(cameraName, 1280, 720);
        CameraTexture.Play();
        PlayerPrefs.SetString(CameraPrefsKey, cameraName);

        // Webcams report a placeholder 16x16 size until the first frame arrives
        yield return new WaitUntil(() => CameraTexture.width > 16);
        CreateWorker(CameraTexture.width, CameraTexture.height);
    }

    void CloseCamera()
    {
        _pending = null;
        _worker?.Dispose();
        _worker = null;
        _input?.Dispose();
        _input = null;
        HasCat = false;

        if (CameraTexture != null)
        {
            CameraTexture.Stop();
            Destroy(CameraTexture);
            CameraTexture = null;
        }
    }

    void CreateWorker(int cameraWidth, int cameraHeight)
    {
        // Letterbox the frame into the top-left of the model input, like YOLOX's own preprocessing
        float scale = Mathf.Min((float)InputSize / cameraWidth, (float)InputSize / cameraHeight);
        _imageWidth = Mathf.RoundToInt(cameraWidth * scale);
        _imageHeight = Mathf.RoundToInt(cameraHeight * scale);

        var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.CPU;
        _worker = new Worker(BuildModel(_imageWidth, _imageHeight), backend);
        _input = new Tensor<float>(new TensorShape(1, 3, _imageHeight, _imageWidth));

        // YOLOX was trained on OpenCV images: BGR, top row first
        _transform = new TextureTransform()
            .SetChannelSwizzle(ChannelSwizzle.BGRA)
            .SetCoordOrigin(CameraTexture.videoVerticallyMirrored ? CoordOrigin.BottomLeft : CoordOrigin.TopLeft);

        Debug.Log($"CatVision: {CameraTexture.deviceName} at {cameraWidth}x{cameraHeight}, running on {backend}.");
    }

    /// <summary>
    /// Wraps YOLOX so the GPU does the padding and picks the single most cat-like
    /// anchor. The output is 8 floats: box offsets (x, y, log w, log h), cat score,
    /// and that anchor's grid cell (x, y, stride).
    /// </summary>
    static Model BuildModel(int imageWidth, int imageHeight)
    {
        var yolox = ModelLoader.Load(Resources.Load<ModelAsset>(ModelResource));

        var graph = new FunctionalGraph();
        var image = graph.AddInput<float>(new TensorShape(1, 3, imageHeight, imageWidth), "image");
        var pixels = Functional.Pad(image * 255f, new[] { 0, InputSize - imageWidth, 0, InputSize - imageHeight }, PadValue);

        var anchors = Functional.Forward(yolox, pixels)[0].Select(0, 0);
        var catScores = anchors.Select(1, 4) * anchors.Select(1, 5 + CocoCat);
        var best = Functional.ArgMax(catScores, 0);
        var gridCells = AnchorGrid();
        var grid = Functional.Constant(new TensorShape(gridCells.Length / 3, 3), gridCells).Select(0, best);

        graph.AddOutput(Functional.Concat(new[]
        {
            anchors.Select(0, best).Narrow(0, 0, 4),
            catScores.Select(0, best).Unsqueeze(0),
            grid,
        }), "cat");
        return graph.Compile();
    }

    static float[] AnchorGrid()
    {
        int count = 0;
        foreach (int stride in Strides)
            count += (InputSize / stride) * (InputSize / stride);

        var grid = new float[count * 3];
        int i = 0;
        foreach (int stride in Strides)
        {
            int cells = InputSize / stride;
            for (int y = 0; y < cells; y++)
            for (int x = 0; x < cells; x++)
            {
                grid[i++] = x;
                grid[i++] = y;
                grid[i++] = stride;
            }
        }
        return grid;
    }

    void Update()
    {
        if (_worker == null)
            return;

        if (_pending != null)
        {
            if (!_pending.IsReadbackRequestDone())
                return;
            ReadResult(_pending.DownloadToArray());
            _pending = null;
        }

        if (!CameraTexture.didUpdateThisFrame)
            return;

        TextureConverter.ToTensor(CameraTexture, _input, _transform);
        _worker.Schedule(_input);
        _pending = _worker.PeekOutput() as Tensor<float>;
        _pending.ReadbackRequest();
    }

    void ReadResult(float[] result)
    {
        Confidence = result[4];
        HasCat = Confidence >= confidenceThreshold;
        CatBox = DecodeBox(result, _imageWidth, _imageHeight);
        ResultReady?.Invoke();
    }

    /// <summary>Turns the model output into a box in normalized camera coordinates.</summary>
    static Rect DecodeBox(float[] result, int imageWidth, int imageHeight)
    {
        // Box offsets are relative to the anchor's grid cell, in units of its stride
        float stride = result[7];
        float cx = (result[0] + result[5]) * stride;
        float cy = (result[1] + result[6]) * stride;
        float w = Mathf.Exp(result[2]) * stride;
        float h = Mathf.Exp(result[3]) * stride;
        return new Rect((cx - w / 2) / imageWidth, (cy - h / 2) / imageHeight, w / imageWidth, h / imageHeight);
    }

    void OnDestroy() => CloseCamera();
}
