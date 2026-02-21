using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class PesokFloorSetup : MonoBehaviour
{
    private enum DepthHalf
    {
        Near,
        Far
    }

    [SerializeField] private Texture2D floorTexture;
    [SerializeField] private string floorName = "Pesok_Floor";
    [SerializeField] private Vector3 worldPosition = new Vector3(0f, -0.02f, 0.4f);
    [SerializeField] private Vector3 worldEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 worldScale = new Vector3(0.6f, 1f, 0.6f);
    [SerializeField] private bool tileWithoutDistortion = false;
    [SerializeField] private float tileDensityY = 1f;
    [SerializeField] private Vector2 uvTiling = new Vector2(2f, 2f);
    [SerializeField] private Vector2 uvOffset = Vector2.zero;
    [SerializeField] private Color floorTint = new Color(1f, 1f, 1f, 1f);
    [SerializeField, Range(0.5f, 2f)] private float brightness = 1f;
    [SerializeField, Range(0f, 2f)] private float saturation = 1f;

    [Header("Split Across Depth (Poperek)")]
    [SerializeField] private bool splitFloorAcrossDepth = true;
    [SerializeField] private DepthHalf referenceHalf = DepthHalf.Far;
    [SerializeField] private bool mirrorCopyOnSecondHalf = true;
    [SerializeField] private string farHalfName = "Pesok_Floor_FarHalf";
    [SerializeField] private float seamOffsetZ = 0f;
    [SerializeField] private float seamOverlapWorld = 0.01f;
    [SerializeField] private bool cleanupLegacySplitFloors = true;

    private Material runtimeMaterial;
    private Material runtimeFarHalfMaterial;
    private bool refreshQueued;

    private void OnEnable()
    {
        refreshQueued = true;
    }

    private void OnValidate()
    {
        // Avoid scene hierarchy mutations directly inside OnValidate.
        refreshQueued = true;
    }

    private void Update()
    {
        if (!refreshQueued) return;
        refreshQueued = false;
        EnsureFloor();
    }

    private void OnDisable()
    {
        DestroyRuntimeMaterial(ref runtimeMaterial);
        DestroyRuntimeMaterial(ref runtimeFarHalfMaterial);
    }

    private void DestroyRuntimeMaterial(ref Material m)
    {
        if (m == null) return;
        if (Application.isPlaying) Destroy(m);
        else DestroyImmediate(m);
        m = null;
    }

    private void EnsureFloor()
    {
        CleanupLegacySplitFloorsIfNeeded();
        if (floorTexture == null) return;

        GameObject nearGo = EnsurePlaneObject(floorName);
        if (nearGo == null) return;

        if (!splitFloorAcrossDepth)
        {
            ApplySingleFloor(nearGo);
            SetActiveIfExists(farHalfName, false, null);
            return;
        }

        GameObject farGo = EnsurePlaneObject(farHalfName);
        if (farGo == null) return;

        ApplySplitFloor(nearGo, farGo);
    }

    private void ApplySingleFloor(GameObject go)
    {
        if (go == null) return;

        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.Euler(worldEuler);
        go.transform.localScale = worldScale;

        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.enabled = true;

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateFloorMaterial();
        }
        if (runtimeMaterial == null) return;

        ApplyFloorBase(runtimeMaterial);
        runtimeMaterial.mainTextureScale = ResolveBaseTiling();
        runtimeMaterial.mainTextureOffset = uvOffset;

        r.sharedMaterial = runtimeMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
    }

    private void ApplySplitFloor(GameObject nearGo, GameObject farGo)
    {
        Quaternion rot = Quaternion.Euler(worldEuler);
        Vector3 forward = rot * Vector3.forward;

        float fullDepth = Mathf.Max(0.001f, 10f * Mathf.Abs(worldScale.z));
        float quarterDepth = fullDepth * 0.25f;
        float overlap = Mathf.Max(0f, seamOverlapWorld);
        float signedZ = worldScale.z == 0f ? 1f : Mathf.Sign(worldScale.z);
        float halfScaleZ = worldScale.z * 0.5f + signedZ * (overlap / 10f);

        Vector3 seamCenter = worldPosition + forward * seamOffsetZ;
        Vector3 nearCenter = seamCenter - forward * quarterDepth;
        Vector3 farCenter = seamCenter + forward * quarterDepth;

        ConfigurePlane(nearGo.transform, nearCenter, rot, new Vector3(worldScale.x, worldScale.y, halfScaleZ));
        ConfigurePlane(farGo.transform, farCenter, rot, new Vector3(worldScale.x, worldScale.y, halfScaleZ));

        var nearR = nearGo.GetComponent<Renderer>();
        var farR = farGo.GetComponent<Renderer>();
        if (nearR == null || farR == null) return;
        nearR.enabled = true;
        farR.enabled = true;

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateFloorMaterial();
        }
        if (runtimeFarHalfMaterial == null)
        {
            runtimeFarHalfMaterial = CreateFloorMaterial();
        }
        if (runtimeMaterial == null || runtimeFarHalfMaterial == null) return;

        ApplyFloorBase(runtimeMaterial);
        ApplyFloorBase(runtimeFarHalfMaterial);

        ApplySplitUv(runtimeMaterial, mirrored: false);
        ApplySplitUv(runtimeFarHalfMaterial, mirrored: mirrorCopyOnSecondHalf);

        nearR.sharedMaterial = runtimeMaterial;
        nearR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        nearR.receiveShadows = true;

        farR.sharedMaterial = runtimeFarHalfMaterial;
        farR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        farR.receiveShadows = true;
    }

    private static void ConfigurePlane(Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        if (t == null) return;
        t.SetParent(null, true);
        t.position = pos;
        t.rotation = rot;
        t.localScale = scale;
    }

    private void ApplySplitUv(Material mat, bool mirrored)
    {
        if (mat == null) return;

        Vector2 baseTiling = ResolveBaseTiling();
        float halfY = baseTiling.y * 0.5f;
        float baseY = uvOffset.y;

        if (referenceHalf == DepthHalf.Far)
        {
            if (mirrored)
            {
                mat.mainTextureScale = new Vector2(baseTiling.x, -halfY);
                mat.mainTextureOffset = new Vector2(uvOffset.x, baseY + baseTiling.y);
            }
            else
            {
                mat.mainTextureScale = new Vector2(baseTiling.x, halfY);
                mat.mainTextureOffset = new Vector2(uvOffset.x, baseY + halfY);
            }
            return;
        }

        if (mirrored)
        {
            mat.mainTextureScale = new Vector2(baseTiling.x, -halfY);
            mat.mainTextureOffset = new Vector2(uvOffset.x, baseY + halfY);
        }
        else
        {
            mat.mainTextureScale = new Vector2(baseTiling.x, halfY);
            mat.mainTextureOffset = new Vector2(uvOffset.x, baseY);
        }
    }

    private Vector2 ResolveBaseTiling()
    {
        Vector2 tiling = uvTiling;
        if (tileWithoutDistortion && floorTexture.height > 0)
        {
            float aspect = (float)floorTexture.width / floorTexture.height;
            float y = Mathf.Max(0.01f, tileDensityY);
            tiling = new Vector2(y * aspect, y);
        }
        return tiling;
    }

    private void ApplyFloorBase(Material mat)
    {
        if (mat == null) return;

        mat.mainTexture = floorTexture;

        Color litTint = new Color(
            floorTint.r * brightness,
            floorTint.g * brightness,
            floorTint.b * brightness,
            1f);

        float gray = litTint.r * 0.299f + litTint.g * 0.587f + litTint.b * 0.114f;
        Color finalTint = new Color(
            Mathf.Lerp(gray, litTint.r, saturation),
            Mathf.Lerp(gray, litTint.g, saturation),
            Mathf.Lerp(gray, litTint.b, saturation),
            1f);

        if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", finalTint);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", finalTint);
    }

    private Material CreateFloorMaterial()
    {
        Shader shader = Shader.Find("Unlit/ShadowReceiverUnlit");
        if (shader == null) shader = Shader.Find("Unlit/FloorTintUnlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        return new Material(shader);
    }

    private GameObject EnsurePlaneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = objectName;
        }
        else if (!go.activeSelf)
        {
            go.SetActive(true);
        }

        return go;
    }

    private void CleanupLegacySplitFloorsIfNeeded()
    {
        if (!cleanupLegacySplitFloors) return;

        TryDestroyByName("Pesok_Floor_idle");
        TryDestroyByName("Pesok_Floor_idle1");
    }

    private void TryDestroyByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return;
        if (objectName == floorName || objectName == farHalfName) return;

        GameObject go = GameObject.Find(objectName);
        if (go == null) return;

        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private static void SetActiveIfExists(string objectName, bool active, GameObject except)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return;

        GameObject go = GameObject.Find(objectName);
        if (go == null || go == except) return;

        if (go.activeSelf != active)
        {
            go.SetActive(active);
        }
    }
}
