using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class StadionBackgroundSetup : MonoBehaviour
{
    private const string CanonicalSourceFighter = "idle";
    private const string CanonicalTargetFighter = "idle (1)";
    private const string CanonicalMirrorQuad = "Stadion_Background_Idle1";
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private string quadName = "Stadion_Background";
    [SerializeField] private bool duplicateBehindIdle = true;
    [SerializeField] private string sourceFighterName = "idle";
    [SerializeField] private string targetFighterName = "idle (1)";
    [SerializeField] private string mirrorQuadName = "Stadion_Background_Idle1";
    [SerializeField] private Vector3 localPosition = new Vector3(0f, 0.47f, 3.9f);
    [SerializeField] private Vector3 localEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private Vector3 localScale = new Vector3(4.8f, 3.2f, 1f);
    [SerializeField] private float backgroundScaleMultiplier = 3.0888f;
    [Header("Mirror Camera")]
    [SerializeField] private bool enableMirrorCamera = true;
    [SerializeField] private string cameraSourceFighterName = "idle";
    [SerializeField] private string cameraTargetFighterName = "idle (1)";
    [SerializeField] private string mirrorCameraName = "Main Camera_MirrorIdle1";
    [SerializeField] private bool useHeadCameraForTarget = true;
    [SerializeField] private bool allowCameraSwitchInPlay = true;
    [SerializeField] private KeyCode switchCameraKey = KeyCode.C;
    [Header("Floor Mirror")]
    [SerializeField] private bool mirrorFloorByCamera = true;
    [SerializeField] private string floorName = "Pesok_Floor";
    [SerializeField] private float mirroredFloorOffsetX = 1f;

    private Material runtimeMaterial;
    private bool refreshQueued;
    private Camera mirrorCamera;
    private bool useMirrorCameraView;
    private GUIStyle cameraButtonStyle;
    private SlapMechanics cachedIdle;
    private SlapMechanics cachedIdle1;
    private Renderer cachedFloorRenderer;
    private bool floorUvCached;
    private Vector2 floorUvBaseScale;
    private Vector2 floorUvBaseOffset;
    private Transform cachedSourceHead;
    private Transform cachedTargetHead;

    private void OnEnable()
    {
        // Keep mirror background mapping deterministic regardless of old scene values.
        sourceFighterName = CanonicalSourceFighter;
        targetFighterName = CanonicalTargetFighter;
        mirrorQuadName = CanonicalMirrorQuad;
        refreshQueued = true;
    }

    private void OnValidate()
    {
        // Do not create/destroy components directly in OnValidate.
        refreshQueued = true;
    }

    private void Update()
    {
        if (refreshQueued)
        {
            refreshQueued = false;
            EnsureBackground();
        }
        EnsureMirrorCamera();
        UpdateCharacterViewMode();
        UpdateFloorMirrorByCamera();

        if (Application.isPlaying && allowCameraSwitchInPlay)
        {
            HandleCameraSwitchInput();
        }
    }

    private void LateUpdate()
    {
        if (!enableMirrorCamera) return;
        EnsureMirrorCamera();
    }

    private void OnDisable()
    {
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(runtimeMaterial);
            else DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    private void EnsureBackground()
    {
        if (backgroundTexture == null) return;

        GameObject go = GameObject.Find(quadName);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = quadName;
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }
        }
        else if (!go.activeSelf)
        {
            go.SetActive(true);
        }
        // Keep background independent from camera transform scale.
        go.transform.SetParent(null, true);
        go.transform.position = transform.TransformPoint(localPosition);
        go.transform.rotation = Quaternion.LookRotation((transform.position - go.transform.position).normalized, Vector3.up);
        go.transform.Rotate(localEuler);
        go.transform.localScale = localScale * Mathf.Max(0.01f, backgroundScaleMultiplier);
        go.layer = gameObject.layer;

        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Standard");
            runtimeMaterial = new Material(shader);
        }

        runtimeMaterial.mainTexture = backgroundTexture;
        runtimeMaterial.mainTextureScale = Vector2.one;
        runtimeMaterial.mainTextureOffset = Vector2.zero;
        // Make it visible from both sides regardless of quad winding/orientation.
        TryDisableCulling(runtimeMaterial);
        r.sharedMaterial = runtimeMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        if (duplicateBehindIdle)
        {
            EnsureMirroredBackground(go);
        }
    }

    private static void TryDisableCulling(Material m)
    {
        if (m == null) return;
        if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        if (m.HasProperty("_CullMode")) m.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);
    }

    private void EnsureMirroredBackground(GameObject sourceBackground)
    {
        if (sourceBackground == null) return;

        // Cleanup legacy mirror object name to avoid seeing two different mirrored backgrounds.
        if (!string.Equals(mirrorQuadName, "Stadion_Background_Idle", System.StringComparison.Ordinal))
        {
            var legacy = GameObject.Find("Stadion_Background_Idle");
            if (legacy != null && legacy.name != mirrorQuadName)
            {
                if (Application.isPlaying) Destroy(legacy);
                else DestroyImmediate(legacy);
            }
        }

        var sourceFighter = FindByName(sourceFighterName);
        var targetFighter = FindByName(targetFighterName);
        if (sourceFighter == null || targetFighter == null) return;

        GameObject mirror = GameObject.Find(mirrorQuadName);
        if (mirror == null)
        {
            mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = mirrorQuadName;
            RemoveCollider(mirror);
        }
        else if (!mirror.activeSelf)
        {
            mirror.SetActive(true);
        }

        Vector3 localOffset = sourceFighter.InverseTransformPoint(sourceBackground.transform.position);
        Quaternion localRotation = Quaternion.Inverse(sourceFighter.rotation) * sourceBackground.transform.rotation;

        mirror.transform.SetParent(null, true);
        mirror.transform.position = targetFighter.TransformPoint(localOffset);
        mirror.transform.rotation = targetFighter.rotation * localRotation;

        // Exact same size as source background.
        mirror.transform.localScale = sourceBackground.transform.localScale;
        mirror.layer = gameObject.layer;

        var renderer = mirror.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = runtimeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }


    private static Transform FindByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;
        var go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
    }

    private static void RemoveCollider(GameObject go)
    {
        if (go == null) return;
        var c = go.GetComponent<Collider>();
        if (c == null) return;
        c.enabled = false;
    }

    private void EnsureMirrorCamera()
    {
        if (!enableMirrorCamera)
        {
            if (mirrorCamera != null) mirrorCamera.enabled = false;
            return;
        }

        var mainCam = GetComponent<Camera>();
        if (mainCam == null) return;

        var sourceFighter = FindByName(cameraSourceFighterName);
        var targetFighter = FindByName(cameraTargetFighterName);
        if (sourceFighter == null || targetFighter == null) return;

        if (mirrorCamera == null)
        {
            var existing = GameObject.Find(mirrorCameraName);
            if (existing != null)
            {
                mirrorCamera = existing.GetComponent<Camera>();
            }
            if (mirrorCamera == null)
            {
                var go = new GameObject(mirrorCameraName);
                mirrorCamera = go.AddComponent<Camera>();
                var l = go.AddComponent<AudioListener>();
                l.enabled = false;
            }
        }

        if (mirrorCamera == null) return;

        mirrorCamera.CopyFrom(mainCam);
        ForceMirrorCameraSettings(mainCam, mirrorCamera);

        if (useHeadCameraForTarget && TryApplyTargetHeadCamera(sourceFighter, targetFighter, mainCam))
        {
            // Keep exact same optical parameters as source camera.
            mirrorCamera.fieldOfView = mainCam.fieldOfView;
        }
        else
        {
            Vector3 camLocalPos = sourceFighter.InverseTransformPoint(transform.position);
            Quaternion camLocalRot = Quaternion.Inverse(sourceFighter.rotation) * transform.rotation;
            mirrorCamera.transform.position = targetFighter.TransformPoint(camLocalPos);
            mirrorCamera.transform.rotation = targetFighter.rotation * camLocalRot;
        }

        ApplyCameraActivation(mainCam);
    }

    private static void ForceMirrorCameraSettings(Camera source, Camera target)
    {
        if (source == null || target == null) return;

        // Hard sync key camera settings every frame to keep both cameras identical.
        target.clearFlags = source.clearFlags;
        target.backgroundColor = source.backgroundColor;
        target.cullingMask = source.cullingMask;
        target.orthographic = source.orthographic;
        target.orthographicSize = source.orthographicSize;
        target.fieldOfView = source.fieldOfView;
        target.nearClipPlane = source.nearClipPlane;
        target.farClipPlane = source.farClipPlane;
        target.rect = source.rect;
        target.depth = source.depth;
        target.renderingPath = source.renderingPath;
        target.allowHDR = source.allowHDR;
        target.allowMSAA = source.allowMSAA;
        target.useOcclusionCulling = source.useOcclusionCulling;
        target.targetDisplay = source.targetDisplay;
    }

    private bool TryApplyTargetHeadCamera(Transform sourceFighter, Transform targetFighter, Camera sourceCamera)
    {
        if (mirrorCamera == null || sourceFighter == null || targetFighter == null || sourceCamera == null) return false;
        if (cachedSourceHead == null || !cachedSourceHead.IsChildOf(sourceFighter))
        {
            cachedSourceHead = FindHeadTransform(sourceFighter);
        }
        if (cachedTargetHead == null || !cachedTargetHead.IsChildOf(targetFighter))
        {
            cachedTargetHead = FindHeadTransform(targetFighter);
        }
        if (cachedSourceHead == null || cachedTargetHead == null) return false;

        Vector3 localPosToHead = cachedSourceHead.InverseTransformPoint(sourceCamera.transform.position);
        Quaternion localRotToSourceFighter = Quaternion.Inverse(sourceFighter.rotation) * sourceCamera.transform.rotation;
        mirrorCamera.transform.position = cachedTargetHead.TransformPoint(localPosToHead);
        mirrorCamera.transform.rotation = targetFighter.rotation * localRotToSourceFighter;
        return true;
    }

    private static Transform FindHeadTransform(Transform root)
    {
        if (root == null) return null;
        var animator = root.GetComponent<Animator>();
        if (animator != null)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) return head;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name.ToLowerInvariant().Contains("head")) return t;
        }
        return null;
    }

    private void HandleCameraSwitchInput()
    {
        if (Input.GetKeyDown(switchCameraKey))
        {
            useMirrorCameraView = !useMirrorCameraView;
        }
    }

    private void ApplyCameraActivation(Camera mainCam)
    {
        if (mainCam != null)
        {
            mainCam.enabled = !useMirrorCameraView;
        }
        if (mirrorCamera != null)
        {
            mirrorCamera.enabled = useMirrorCameraView;
        }
    }

    private void UpdateCharacterViewMode()
    {
        ResolveCharactersIfNeeded();
        if (cachedIdle == null || cachedIdle1 == null) return;
        Camera activeCam = GetActiveViewCamera();
        if (activeCam == null) return;

        bool idleIsFront = IsMoreCentered(activeCam, cachedIdle.transform, cachedIdle1.transform);
        if (idleIsFront)
        {
            cachedIdle.SetHandsOnlyVisualRuntime(false);
            cachedIdle1.SetHandsOnlyVisualRuntime(true);
        }
        else
        {
            cachedIdle.SetHandsOnlyVisualRuntime(true);
            cachedIdle1.SetHandsOnlyVisualRuntime(false);
        }
    }

    private void ResolveCharactersIfNeeded()
    {
        if (cachedIdle != null && cachedIdle1 != null) return;

        var idleGo = GameObject.Find("idle");
        if (idleGo != null) cachedIdle = idleGo.GetComponent<SlapMechanics>();

        var idle1Go = GameObject.Find("idle (1)");
        if (idle1Go != null) cachedIdle1 = idle1Go.GetComponent<SlapMechanics>();
    }

    private void UpdateFloorMirrorByCamera()
    {
        if (!mirrorFloorByCamera) return;
        if (!TryGetFloorMaterial(out Material m)) return;

        if (!floorUvCached)
        {
            floorUvBaseScale = m.mainTextureScale;
            floorUvBaseOffset = m.mainTextureOffset;
            floorUvCached = true;
        }

        Vector2 scale = floorUvBaseScale;
        Vector2 offset = floorUvBaseOffset;
        if (!useMirrorCameraView)
        {
            // Mirror from center for CAM: idle so it matches CAM: idle (1) look.
            scale.x = -Mathf.Abs(floorUvBaseScale.x);
            offset.x = mirroredFloorOffsetX;
        }
        else
        {
            scale.x = Mathf.Abs(floorUvBaseScale.x);
        }

        if (m.mainTextureScale != scale) m.mainTextureScale = scale;
        if (m.mainTextureOffset != offset) m.mainTextureOffset = offset;
    }

    private bool TryGetFloorMaterial(out Material mat)
    {
        mat = null;
        if (cachedFloorRenderer == null)
        {
            var go = GameObject.Find(floorName);
            if (go != null) cachedFloorRenderer = go.GetComponent<Renderer>();
        }
        if (cachedFloorRenderer == null) return false;
        mat = cachedFloorRenderer.sharedMaterial;
        return mat != null;
    }

    private Camera GetActiveViewCamera()
    {
        var mainCam = GetComponent<Camera>();
        if (useMirrorCameraView && mirrorCamera != null && mirrorCamera.enabled) return mirrorCamera;
        return mainCam;
    }

    private static bool IsMoreCentered(Camera cam, Transform a, Transform b)
    {
        if (cam == null || a == null || b == null) return true;
        float sa = CenterScore(cam, a.position);
        float sb = CenterScore(cam, b.position);
        return sa >= sb;
    }

    private static float CenterScore(Camera cam, Vector3 worldPos)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f) return -9999f;
        Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        // Higher score = closer to center and in front of camera.
        return 1f - d.sqrMagnitude * 4f;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (!allowCameraSwitchInPlay || !enableMirrorCamera) return;
        float w = Mathf.Clamp(Screen.width * 0.46f, 220f, 420f);
        float h = Mathf.Clamp(Screen.height * 0.08f, 52f, 86f);
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - h - Mathf.Max(12f, Screen.height * 0.03f);
        Rect r = new Rect(x, y, w, h);
        string label = useMirrorCameraView ? "CAM: idle (1)" : "CAM: idle";
        if (mirrorCamera == null)
        {
            label += " (loading)";
        }

        if (GUI.Button(r, label, GetCameraButtonStyle()))
        {
            useMirrorCameraView = !useMirrorCameraView;
        }
    }

    private GUIStyle GetCameraButtonStyle()
    {
        if (cameraButtonStyle != null) return cameraButtonStyle;

        cameraButtonStyle = new GUIStyle(GUI.skin.button);
        cameraButtonStyle.alignment = TextAnchor.MiddleCenter;
        cameraButtonStyle.fontStyle = FontStyle.Bold;
        cameraButtonStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.0756f), 39, 74);
        return cameraButtonStyle;
    }
}
