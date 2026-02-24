using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CameraSwitchUI : MonoBehaviour
{
    [Header("Camera Names")]
    [SerializeField] private string primaryCameraName = "Main Camera";
    [SerializeField] private string secondaryCameraName = "Main Camera_MirrorIdle1";

    [Header("Button Labels")]
    [SerializeField] private string primaryLabel = "idle";
    [SerializeField] private string secondaryLabel = "idle (1)";

    [Header("Button Layout")]
    [SerializeField] private Vector2 buttonSize = new Vector2(220f, 64f);
    [SerializeField] private Vector2 buttonAnchoredPosition = new Vector2(0f, 28f);

    private Camera primaryCamera;
    private Camera secondaryCamera;
    private Text buttonText;
    private bool usingSecondaryCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<CameraSwitchUI>() != null) return;

        var root = new GameObject("CameraSwitchUI");
        DontDestroyOnLoad(root);
        root.AddComponent<CameraSwitchUI>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        BuildUi();
        ResolveCameras();
        SyncStateFromScene();
        ApplyCameraState();
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveCameras();
        SyncStateFromScene();
        ApplyCameraState();
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        var canvasObject = new GameObject("CameraSwitchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var buttonObject = new GameObject("CameraSwitchButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = buttonSize;
        buttonRect.anchoredPosition = buttonAnchoredPosition;

        var buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0f, 0f, 0f, 0.58f);

        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(SwitchCamera);

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(buttonObject.transform, false);

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 6f);
        textRect.offsetMax = new Vector2(-8f, -6f);

        buttonText = textObject.GetComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontSize = 30;
        buttonText.color = Color.white;
        buttonText.text = "CAM: " + primaryLabel;

        var outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void SwitchCamera()
    {
        ResolveCameras();
        if (primaryCamera == null || secondaryCamera == null)
        {
            SetButtonText("CAM: missing");
            return;
        }

        usingSecondaryCamera = !usingSecondaryCamera;
        ApplyCameraState();
    }

    private void ApplyCameraState()
    {
        if (primaryCamera == null || secondaryCamera == null)
        {
            SetButtonText("CAM: missing");
            return;
        }

        SetCameraActive(primaryCamera, !usingSecondaryCamera);
        SetCameraActive(secondaryCamera, usingSecondaryCamera);
        EnsureSingleAudioListener();

        SetButtonText("CAM: " + (usingSecondaryCamera ? secondaryLabel : primaryLabel));
    }

    private void ResolveCameras()
    {
        primaryCamera = FindCameraByName(primaryCameraName);
        secondaryCamera = FindCameraByName(secondaryCameraName);
    }

    private void SyncStateFromScene()
    {
        if (secondaryCamera != null && secondaryCamera.gameObject.activeInHierarchy)
        {
            usingSecondaryCamera = true;
            return;
        }

        usingSecondaryCamera = false;
    }

    private static void SetCameraActive(Camera cam, bool active)
    {
        if (cam == null) return;

        if (cam.gameObject.activeSelf != active)
        {
            cam.gameObject.SetActive(active);
        }

        cam.enabled = active;
        var listener = cam.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = active;
    }

    private void EnsureSingleAudioListener()
    {
        var listeners = FindObjectsOfType<AudioListener>(true);
        foreach (var listener in listeners)
        {
            if (listener == null) continue;
            bool shouldEnable =
                (!usingSecondaryCamera && primaryCamera != null && listener.gameObject == primaryCamera.gameObject) ||
                (usingSecondaryCamera && secondaryCamera != null && listener.gameObject == secondaryCamera.gameObject);
            listener.enabled = shouldEnable;
        }
    }

    private void SetButtonText(string value)
    {
        if (buttonText == null) return;
        buttonText.text = value;
    }

    private Camera FindCameraByName(string cameraName)
    {
        if (string.IsNullOrWhiteSpace(cameraName)) return null;

        var sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var t = FindDeepChildByName(roots[r].transform, cameraName);
                if (t == null) continue;

                var cam = t.GetComponent<Camera>();
                if (cam != null) return cam;
            }
        }

        return null;
    }

    private static Transform FindDeepChildByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeepChildByName(root.GetChild(i), targetName);
            if (found != null) return found;
        }

        return null;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }
}
