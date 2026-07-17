using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies a centered 16:9 viewport without changing camera projection.</summary>
public sealed class AspectRatioPresentation : MonoBehaviour
{
    public const float TargetAspect = 16f / 9f;

    private Camera blackClearCamera;
    private int lastWidth;
    private int lastHeight;
    private int lastCameraCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject presentationObject = new GameObject(nameof(AspectRatioPresentation));
        DontDestroyOnLoad(presentationObject);
        presentationObject.AddComponent<AspectRatioPresentation>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        CreateBlackClearCamera();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LateUpdate()
    {
        int cameraCount = Camera.allCamerasCount;
        if (Screen.width != lastWidth || Screen.height != lastHeight || cameraCount != lastCameraCount)
            ApplyToSceneCameras();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToSceneCameras();
    }

    private void CreateBlackClearCamera()
    {
        blackClearCamera = gameObject.AddComponent<Camera>();
        blackClearCamera.name = "Black Bar Clear Camera";
        blackClearCamera.clearFlags = CameraClearFlags.SolidColor;
        blackClearCamera.backgroundColor = Color.black;
        blackClearCamera.cullingMask = 0;
        blackClearCamera.depth = -10000f;
        blackClearCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private void ApplyToSceneCameras()
    {
        Rect viewport = CalculateViewport(Screen.width, Screen.height);
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera != blackClearCamera && camera.targetTexture == null)
                camera.rect = viewport;
        }

        if (blackClearCamera != null)
            blackClearCamera.rect = new Rect(0f, 0f, 1f, 1f);

        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastCameraCount = Camera.allCamerasCount;
    }

    public static Rect CalculateViewport(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0)
            return new Rect(0f, 0f, 1f, 1f);

        float screenAspect = (float)screenWidth / screenHeight;
        if (Mathf.Approximately(screenAspect, TargetAspect))
            return new Rect(0f, 0f, 1f, 1f);

        if (screenAspect > TargetAspect)
        {
            float normalizedWidth = TargetAspect / screenAspect;
            return new Rect((1f - normalizedWidth) * 0.5f, 0f, normalizedWidth, 1f);
        }

        float normalizedHeight = screenAspect / TargetAspect;
        return new Rect(0f, (1f - normalizedHeight) * 0.5f, 1f, normalizedHeight);
    }
}
