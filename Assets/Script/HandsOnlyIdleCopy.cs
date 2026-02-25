using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class HandsOnlyIdleCopy : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

    [Header("Source")]
    [SerializeField] private string sourceRootName = "idle";
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0f, 0.6f);
    [SerializeField] private bool followSource = true;
    [SerializeField, Range(0.1f, 2f)] private float avatarScaleMultiplier = 0.8f;

    [Header("Mesh Build")]
    [SerializeField, Range(0f, 1f)] private float handWeightThreshold = 0.45f;
    [SerializeField] private bool disableOriginalRenderers = false;

    [Header("Avatar Visual")]
    [SerializeField] private bool useCombatGlow = true;
    [SerializeField] private bool visibleOnlyForControllingCamera = true;
    [SerializeField, Range(0f, 0.2f)] private float movementRevealThreshold01 = 0.001f;
    [SerializeField, Range(0.01f, 0.5f)] private float handFadeSeconds = 0.2f;
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField, Range(0.01f, 1f)] private float idleOpacity = 0.1f;
    [SerializeField, Range(0.01f, 1f)] private float activeColorOpacity = 0.4f;
    [SerializeField] private Color attackGlowColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color blockGlowColor = new Color(0.1f, 0.45f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float glowMinIntensity = 0f;
    [SerializeField] private float glowMaxIntensity = 2.5f;
    [SerializeField] private float glowBlendSpeed = 14f;

    private const string PoseRigContainerName = "HandsPoseRig";
    private const string LeftHandMeshName = "LeftHandOnlyMesh";
    private const string RightHandMeshName = "RightHandOnlyMesh";
    private const string PoseBoneSuffix = "__HandsPose";

    private Transform sourceRoot;
    private SkinnedMeshRenderer sourceRenderer;
    private SkinnedMeshRenderer leftRenderer;
    private SkinnedMeshRenderer rightRenderer;
    private Transform poseRigRoot;
    private Transform[] sourceBones;
    private Transform[] poseBones;
    private SlapMechanics sourceMechanics;
    private Material avatarMaterial;
    private float glowProgressSmoothed;
    private Color glowColorSmoothed;
    private bool renderersVisible;
    private float leftHandFade01;
    private float rightHandFade01;
    private MaterialPropertyBlock leftPropertyBlock;
    private MaterialPropertyBlock rightPropertyBlock;
    private Dictionary<Transform, Transform> sourceToPoseBone;
    private List<Transform> poseSyncSource;
    private List<Transform> poseSyncTarget;

    private void OnEnable()
    {
        EnsureRuntimeObjects();
        TryBuild(true);
    }

    private void Start()
    {
        TryBuild(false);
    }

    private void OnDisable()
    {
        CleanupAvatarMaterial();
    }

    private void OnDestroy()
    {
        CleanupAvatarMaterial();
    }

    private void LateUpdate()
    {
        EnsureRuntimeObjects();

        if (sourceRoot == null || sourceRenderer == null || poseRigRoot == null || (leftRenderer == null && rightRenderer == null))
        {
            TryBuild(false);
        }

        if (sourceRoot == null || sourceRenderer == null) return;

        if (followSource)
        {
            SyncTransform();
        }

        SyncPoseRig();
        EnsureAvatarMaterialAssigned();
        UpdateDrivenHandVisibility();
        UpdateAvatarVisual();
    }

    private void TryBuild(bool forceRebuild)
    {
        EnsureRuntimeObjects();

        if (forceRebuild)
        {
            ClearGeneratedChildren();
            leftRenderer = null;
            rightRenderer = null;
            poseRigRoot = null;
            sourceBones = null;
            poseBones = null;
            sourceMechanics = null;
            glowProgressSmoothed = 0f;
            glowColorSmoothed = Color.black;
            renderersVisible = false;
            leftHandFade01 = 0f;
            rightHandFade01 = 0f;
            sourceToPoseBone.Clear();
            poseSyncSource.Clear();
            poseSyncTarget.Clear();
        }

        ResolveSource();
        if (sourceRenderer == null) return;
        if (leftRenderer != null && rightRenderer != null && poseRigRoot != null) return;

        SyncTransform();
        BuildHandsOnlyMeshes();
    }

    private void ClearGeneratedChildren()
    {
        var toDelete = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            toDelete.Add(child.gameObject);
        }

        foreach (var go in toDelete)
        {
            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null && smr.sharedMesh.name.Contains("_HandOnly"))
            {
                if (Application.isPlaying) Destroy(smr.sharedMesh);
                else DestroyImmediate(smr.sharedMesh);
            }

            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    private void ResolveSource()
    {
        if (!string.IsNullOrEmpty(sourceRootName))
        {
            sourceRoot = FindExternalByName(sourceRootName);
        }

        if (sourceRoot == null)
        {
            sourceRoot = FindExternalByName("idle");
        }

        if (sourceRoot == null)
        {
            sourceRoot = FindExternalByName("idle (1)");
        }

        if (sourceRoot == null)
        {
            var allRenderers = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var r = allRenderers[i];
                if (r == null) continue;
                if (r.transform == null || r.transform.IsChildOf(transform)) continue;
                sourceRenderer = r;
                sourceRoot = r.transform.root;
                sourceMechanics = sourceRoot != null
                    ? sourceRoot.GetComponent<SlapMechanics>() ?? sourceRoot.GetComponentInChildren<SlapMechanics>(true)
                    : null;
                break;
            }
            return;
        }

        sourceRenderer = sourceRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        sourceMechanics = sourceRoot.GetComponent<SlapMechanics>() ?? sourceRoot.GetComponentInChildren<SlapMechanics>(true);
    }

    private Transform FindExternalByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var direct = GameObject.Find(name)?.transform;
        if (direct != null && !direct.IsChildOf(transform)) return direct;

        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!string.Equals(t.name, name, System.StringComparison.Ordinal)) continue;
            if (t.IsChildOf(transform)) continue;
            return t;
        }

        return null;
    }

    private void SyncTransform()
    {
        if (sourceRoot == null) return;

        // Keep avatar in front of source in source-local space.
        transform.position = sourceRoot.TransformPoint(worldOffset);
        transform.rotation = sourceRoot.rotation;
        transform.localScale = sourceRoot.lossyScale * avatarScaleMultiplier;
    }

    private void BuildHandsOnlyMeshes()
    {
        var src = sourceRenderer;
        if (src == null || src.sharedMesh == null) return;
        if (src.bones == null || src.bones.Length == 0 || sourceRoot == null) return;
        if (!src.sharedMesh.isReadable) return;

        var leftBones = new HashSet<int>();
        var rightBones = new HashSet<int>();

        for (int i = 0; i < src.bones.Length; i++)
        {
            var b = src.bones[i];
            if (b == null) continue;
            string n = b.name.ToLowerInvariant();
            if (!IsHandBoneName(n)) continue;
            if (BoneNameIsLeft(n)) leftBones.Add(i);
            else if (BoneNameIsRight(n)) rightBones.Add(i);
        }

        if (leftBones.Count == 0 && rightBones.Count == 0) return;

        BuildPoseRig(src);
        if (poseBones == null || poseBones.Length != src.bones.Length) return;

        Mesh leftMesh = leftBones.Count > 0 ? BuildHandOnlyMesh(src, leftBones, "_LeftHandOnly") : null;
        Mesh rightMesh = rightBones.Count > 0 ? BuildHandOnlyMesh(src, rightBones, "_RightHandOnly") : null;

        if (leftMesh != null)
        {
            leftRenderer = CreateHandRenderer(src, LeftHandMeshName, leftMesh);
        }
        if (rightMesh != null)
        {
            rightRenderer = CreateHandRenderer(src, RightHandMeshName, rightMesh);
        }

        if (disableOriginalRenderers && sourceRoot != null)
        {
            foreach (var r in sourceRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = false;
            }
        }

        EnsureAvatarMaterialAssigned();
    }

    private void BuildPoseRig(SkinnedMeshRenderer source)
    {
        if (source == null || source.bones == null || source.bones.Length == 0 || sourceRoot == null) return;
        if (poseRigRoot != null && poseBones != null && poseBones.Length == source.bones.Length) return;

        sourceToPoseBone.Clear();
        poseSyncSource.Clear();
        poseSyncTarget.Clear();

        var rigContainer = new GameObject(PoseRigContainerName);
        var containerTransform = rigContainer.transform;
        containerTransform.SetParent(transform, false);
        containerTransform.localPosition = Vector3.zero;
        containerTransform.localRotation = Quaternion.identity;
        containerTransform.localScale = Vector3.one;

        poseRigRoot = CloneHierarchyRecursive(sourceRoot, containerTransform, true);

        sourceBones = source.bones;
        poseBones = new Transform[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
        {
            var srcBone = sourceBones[i];
            if (srcBone == null || !sourceToPoseBone.TryGetValue(srcBone, out var mappedBone))
            {
                CleanupPoseRigContainer(rigContainer);
                poseRigRoot = null;
                poseBones = null;
                sourceBones = null;
                sourceToPoseBone.Clear();
                return;
            }

            poseBones[i] = mappedBone;
        }

        foreach (var pair in sourceToPoseBone)
        {
            poseSyncSource.Add(pair.Key);
            poseSyncTarget.Add(pair.Value);
        }

        SyncPoseRig();
    }

    private static void CleanupPoseRigContainer(GameObject rigContainer)
    {
        if (rigContainer == null) return;
        if (Application.isPlaying) Object.Destroy(rigContainer);
        else Object.DestroyImmediate(rigContainer);
    }

    private Transform CloneHierarchyRecursive(Transform source, Transform parent, bool isRigRoot)
    {
        if (source == null) return null;

        var go = new GameObject(source.name + PoseBoneSuffix);
        var clone = go.transform;
        clone.SetParent(parent, false);

        if (isRigRoot)
        {
            clone.localPosition = Vector3.zero;
            clone.localRotation = Quaternion.identity;
            clone.localScale = Vector3.one;
        }
        else
        {
            clone.localPosition = source.localPosition;
            clone.localRotation = source.localRotation;
            clone.localScale = source.localScale;
        }

        sourceToPoseBone[source] = clone;

        foreach (Transform child in source)
        {
            CloneHierarchyRecursive(child, clone, false);
        }

        return clone;
    }

    private void SyncPoseRig()
    {
        if (poseSyncSource.Count == 0 || poseSyncSource.Count != poseSyncTarget.Count) return;

        for (int i = 0; i < poseSyncSource.Count; i++)
        {
            var src = poseSyncSource[i];
            var dst = poseSyncTarget[i];
            if (src == null || dst == null) continue;

            if (src == sourceRoot)
            {
                dst.localPosition = Vector3.zero;
                dst.localRotation = Quaternion.identity;
                dst.localScale = Vector3.one;
                continue;
            }

            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
    }

    private Mesh BuildHandOnlyMesh(SkinnedMeshRenderer source, HashSet<int> allowedBones, string suffix)
    {
        Mesh src = source.sharedMesh;
        if (src == null || src.vertexCount <= 0) return null;
        if (src.boneWeights == null || src.boneWeights.Length != src.vertexCount) return null;

        var keepVertex = new bool[src.vertexCount];
        var boneWeights = src.boneWeights;
        for (int i = 0; i < boneWeights.Length; i++)
        {
            BoneWeight bw = boneWeights[i];
            float handWeight = 0f;
            if (allowedBones.Contains(bw.boneIndex0)) handWeight += bw.weight0;
            if (allowedBones.Contains(bw.boneIndex1)) handWeight += bw.weight1;
            if (allowedBones.Contains(bw.boneIndex2)) handWeight += bw.weight2;
            if (allowedBones.Contains(bw.boneIndex3)) handWeight += bw.weight3;
            keepVertex[i] = handWeight >= handWeightThreshold;
        }

        int[] tris = src.triangles;
        if (tris == null || tris.Length < 3) return null;
        var used = new HashSet<int>();
        var keptTri = new List<int>(tris.Length);
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            int a = tris[i];
            int b = tris[i + 1];
            int c = tris[i + 2];
            if (!keepVertex[a] || !keepVertex[b] || !keepVertex[c]) continue;
            keptTri.Add(a);
            keptTri.Add(b);
            keptTri.Add(c);
            used.Add(a);
            used.Add(b);
            used.Add(c);
        }
        if (keptTri.Count < 30) return null;

        var map = new Dictionary<int, int>(used.Count);
        var srcVerts = src.vertices;
        var srcNormals = src.normals;
        var srcTangents = src.tangents;
        var srcUv = src.uv;

        var newVerts = new List<Vector3>(used.Count);
        var newNormals = (srcNormals != null && srcNormals.Length == srcVerts.Length) ? new List<Vector3>(used.Count) : null;
        var newTangents = (srcTangents != null && srcTangents.Length == srcVerts.Length) ? new List<Vector4>(used.Count) : null;
        var newUv = (srcUv != null && srcUv.Length == srcVerts.Length) ? new List<Vector2>(used.Count) : null;
        var newBw = new List<BoneWeight>(used.Count);

        foreach (int oldIndex in used)
        {
            int newIndex = newVerts.Count;
            map[oldIndex] = newIndex;
            newVerts.Add(srcVerts[oldIndex]);
            if (newNormals != null) newNormals.Add(srcNormals[oldIndex]);
            if (newTangents != null) newTangents.Add(srcTangents[oldIndex]);
            if (newUv != null) newUv.Add(srcUv[oldIndex]);
            newBw.Add(boneWeights[oldIndex]);
        }

        var remapTris = new int[keptTri.Count];
        for (int i = 0; i < keptTri.Count; i++)
        {
            remapTris[i] = map[keptTri[i]];
        }

        var handMesh = new Mesh();
        handMesh.name = source.sharedMesh.name + suffix;
        handMesh.SetVertices(newVerts);
        if (newNormals != null) handMesh.SetNormals(newNormals);
        if (newTangents != null) handMesh.SetTangents(newTangents);
        if (newUv != null) handMesh.SetUVs(0, newUv);
        handMesh.boneWeights = newBw.ToArray();
        handMesh.bindposes = src.bindposes;
        handMesh.SetTriangles(remapTris, 0, true);
        if (newNormals == null) handMesh.RecalculateNormals();
        handMesh.RecalculateBounds();
        return handMesh;
    }

    private SkinnedMeshRenderer CreateHandRenderer(SkinnedMeshRenderer source, string goName, Mesh mesh)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = poseBones;
        if (source.rootBone != null && sourceToPoseBone.TryGetValue(source.rootBone, out var mappedRoot))
        {
            smr.rootBone = mappedRoot;
        }
        else
        {
            smr.rootBone = poseRigRoot;
        }
        smr.updateWhenOffscreen = source.updateWhenOffscreen;
        smr.localBounds = source.localBounds;
        smr.shadowCastingMode = ShadowCastingMode.Off;
        smr.receiveShadows = false;
        smr.lightProbeUsage = LightProbeUsage.Off;
        smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var mat = GetOrCreateAvatarMaterial(source);
        if (mat != null)
        {
            smr.sharedMaterials = new[] { mat };
        }
        return smr;
    }

    private Material GetOrCreateAvatarMaterial(SkinnedMeshRenderer source)
    {
        if (avatarMaterial != null)
        {
            string currentShaderName = avatarMaterial.shader != null ? avatarMaterial.shader.name : string.Empty;
            if (!string.Equals(currentShaderName, "Unlit/Color", System.StringComparison.Ordinal))
            {
                return avatarMaterial;
            }

            CleanupAvatarMaterial();
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null) return null;
        avatarMaterial = new Material(shader);

        avatarMaterial.name = "HandsAvatar_UntexturedGlow";
        avatarMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        ConfigureMaterialForTransparency(avatarMaterial);
        StripTexture(avatarMaterial);
        ApplyColor(avatarMaterial, GetIdleFogColor());
        ApplyEmission(avatarMaterial, Color.black);
        avatarMaterial.EnableKeyword("_EMISSION");
        return avatarMaterial;
    }

    private void EnsureAvatarMaterialAssigned()
    {
        if (leftRenderer == null)
        {
            var leftT = transform.Find(LeftHandMeshName);
            if (leftT != null) leftRenderer = leftT.GetComponent<SkinnedMeshRenderer>();
        }
        if (rightRenderer == null)
        {
            var rightT = transform.Find(RightHandMeshName);
            if (rightT != null) rightRenderer = rightT.GetComponent<SkinnedMeshRenderer>();
        }

        if (leftRenderer == null && rightRenderer == null) return;
        if (sourceRenderer == null) return;

        var mat = GetOrCreateAvatarMaterial(sourceRenderer);
        if (mat == null) return;

        ConfigureMaterialForTransparency(mat);
        StripTexture(mat);

        if (leftRenderer != null && leftRenderer.sharedMaterial != mat)
        {
            leftRenderer.sharedMaterials = new[] { mat };
        }
        if (rightRenderer != null && rightRenderer.sharedMaterial != mat)
        {
            rightRenderer.sharedMaterials = new[] { mat };
        }
    }

    private void EnsureRuntimeObjects()
    {
        if (sourceToPoseBone == null) sourceToPoseBone = new Dictionary<Transform, Transform>();
        if (poseSyncSource == null) poseSyncSource = new List<Transform>();
        if (poseSyncTarget == null) poseSyncTarget = new List<Transform>();
        if (leftPropertyBlock == null) leftPropertyBlock = new MaterialPropertyBlock();
        if (rightPropertyBlock == null) rightPropertyBlock = new MaterialPropertyBlock();
    }

    private void UpdateDrivenHandVisibility()
    {
        bool leftTarget = false;
        bool rightTarget = false;

        if (ShouldShowForCurrentViewer())
        {
            if (!Application.isPlaying)
            {
                // Keep both visible in edit mode for setup convenience.
                leftTarget = true;
                rightTarget = true;
            }
            else
            {
                if (sourceMechanics == null && sourceRoot != null)
                {
                    sourceMechanics = sourceRoot.GetComponent<SlapMechanics>() ?? sourceRoot.GetComponentInChildren<SlapMechanics>(true);
                }

                if (sourceMechanics != null && sourceMechanics.IsControlledHandMoving())
                {
                    float progress = sourceMechanics.GetControlledVisualProgress01();
                    bool reveal = progress > Mathf.Max(0f, movementRevealThreshold01);
                    if (reveal)
                    {
                        var hand = sourceMechanics.GetControlledVisualHand();
                        leftTarget = hand == SlapMechanics.VisualHand.Left;
                        rightTarget = hand == SlapMechanics.VisualHand.Right;
                    }
                }
            }
        }

        if (!Application.isPlaying)
        {
            leftHandFade01 = leftTarget ? 1f : 0f;
            rightHandFade01 = rightTarget ? 1f : 0f;
        }
        else
        {
            float dt = Mathf.Max(0f, Time.deltaTime);
            float speed = 1f / Mathf.Max(0.01f, handFadeSeconds);
            leftHandFade01 = Mathf.MoveTowards(leftHandFade01, leftTarget ? 1f : 0f, speed * dt);
            rightHandFade01 = Mathf.MoveTowards(rightHandFade01, rightTarget ? 1f : 0f, speed * dt);
        }

        bool leftVisibleNow = leftTarget || leftHandFade01 > 0.001f;
        bool rightVisibleNow = rightTarget || rightHandFade01 > 0.001f;
        if (leftRenderer != null) leftRenderer.enabled = leftVisibleNow;
        if (rightRenderer != null) rightRenderer.enabled = rightVisibleNow;
        renderersVisible = leftVisibleNow || rightVisibleNow;
    }

    private bool ShouldShowForCurrentViewer()
    {
        if (!visibleOnlyForControllingCamera) return true;
        if (!Application.isPlaying) return true;
        var fp = GetActiveFirstPersonCamera();
        if (fp == null) return true;

        var camSource = fp.GetSourceRootTransform();
        if (camSource == null) return true;
        if (sourceRoot == null) return true;

        return camSource == sourceRoot;
    }

    private static MainCameraFirstPerson GetActiveFirstPersonCamera()
    {
        var all = FindObjectsByType<MainCameraFirstPerson>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MainCameraFirstPerson best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = 0; i < all.Length; i++)
        {
            var fp = all[i];
            if (fp == null) continue;

            var cam = fp.GetComponent<Camera>();
            if (cam == null) continue;
            if (!cam.enabled) continue;
            if (!cam.gameObject.activeInHierarchy) continue;

            if (best == null || cam.depth > bestDepth)
            {
                best = fp;
                bestDepth = cam.depth;
            }
        }

        return best;
    }

    private void UpdateAvatarVisual()
    {
        if (leftRenderer == null && rightRenderer == null) return;
        if (!renderersVisible) return;
        if (avatarMaterial == null)
        {
            EnsureAvatarMaterialAssigned();
            if (avatarMaterial == null) return;
        }

        if (!useCombatGlow)
        {
            glowProgressSmoothed = 0f;
            glowColorSmoothed = Color.black;
            ApplyHandRendererVisual(leftRenderer, leftPropertyBlock, GetIdleFogColor(), Color.black, leftHandFade01);
            ApplyHandRendererVisual(rightRenderer, rightPropertyBlock, GetIdleFogColor(), Color.black, rightHandFade01);
            return;
        }

        if (sourceMechanics == null && sourceRoot != null)
        {
            sourceMechanics = sourceRoot.GetComponent<SlapMechanics>() ?? sourceRoot.GetComponentInChildren<SlapMechanics>(true);
        }

        GetGlowTarget(out Color targetColor, out float targetProgress);

        float dt = Application.isPlaying ? Time.deltaTime : (1f / 60f);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, glowBlendSpeed) * Mathf.Max(0f, dt));
        glowProgressSmoothed = Mathf.Lerp(glowProgressSmoothed, Mathf.Clamp01(targetProgress), blend);
        glowColorSmoothed = Color.Lerp(glowColorSmoothed, targetColor, blend);

        float alpha = Mathf.Lerp(Mathf.Clamp01(idleOpacity), Mathf.Clamp01(activeColorOpacity), glowProgressSmoothed);
        Color fog = GetFogColor(alpha);
        Color tinted = Color.Lerp(Color.white, glowColorSmoothed, glowProgressSmoothed);
        Color baseColor = new Color(tinted.r, tinted.g, tinted.b, fog.a);

        float intensity = glowProgressSmoothed <= 0.0001f
            ? 0f
            : Mathf.Lerp(Mathf.Max(0f, glowMinIntensity), Mathf.Max(0f, glowMaxIntensity), glowProgressSmoothed);
        Color emissionColor = targetColor * intensity * Mathf.Clamp01(alpha * 3f);

        ApplyHandRendererVisual(leftRenderer, leftPropertyBlock, baseColor, emissionColor, leftHandFade01);
        ApplyHandRendererVisual(rightRenderer, rightPropertyBlock, baseColor, emissionColor, rightHandFade01);
    }

    private void GetGlowTarget(out Color targetColor, out float targetProgress)
    {
        targetColor = Color.black;
        targetProgress = 0f;
        if (sourceMechanics == null) return;

        var role = sourceMechanics.GetRole();
        if (role == SlapMechanics.Role.Attacker)
        {
            if (!sourceMechanics.IsAttackerWindupHolding()) return;
            targetColor = attackGlowColor;
            targetProgress = Mathf.Clamp01(sourceMechanics.GetDebugWindup01());
            return;
        }

        if (role == SlapMechanics.Role.Defender)
        {
            bool active = sourceMechanics.IsDefenderBlocking() || sourceMechanics.IsDefenderBlockReleasing();
            if (!active) return;
            targetColor = blockGlowColor;
            targetProgress = Mathf.Clamp01(sourceMechanics.GetDefenderBlockHold01());
        }
    }

    private static void StripTexture(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, null);
        if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, null);
    }

    private static void ConfigureMaterialForTransparency(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty(SurfaceId)) mat.SetFloat(SurfaceId, 1f);
        if (mat.HasProperty(AlphaClipId)) mat.SetFloat(AlphaClipId, 0f);
        if (mat.HasProperty(BlendId)) mat.SetFloat(BlendId, 0f);
        if (mat.HasProperty(SrcBlendId)) mat.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        if (mat.HasProperty(DstBlendId)) mat.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty(ZWriteId)) mat.SetFloat(ZWriteId, 0f);
        if (mat.HasProperty(ModeId)) mat.SetFloat(ModeId, 3f);

        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private Color GetIdleFogColor()
    {
        return GetFogColor(idleOpacity);
    }

    private Color GetFogColor(float alpha)
    {
        Color c = idleColor;
        c.a = Mathf.Clamp01(alpha);
        return c;
    }

    private static void ApplyColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
    }

    private static void ApplyEmission(Material mat, Color color)
    {
        if (mat == null) return;
        if (!mat.HasProperty(EmissionColorId)) return;

        mat.SetColor(EmissionColorId, color);
        if (color.maxColorComponent > 0.0001f) mat.EnableKeyword("_EMISSION");
        else mat.DisableKeyword("_EMISSION");
    }

    private static void ApplyHandRendererVisual(
        SkinnedMeshRenderer renderer,
        MaterialPropertyBlock block,
        Color baseColor,
        Color emissionColor,
        float handFade01)
    {
        if (renderer == null) return;
        if (block == null) return;

        float f = Mathf.Clamp01(handFade01);
        Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * f));
        Color e = emissionColor * f;

        renderer.GetPropertyBlock(block);
        block.SetColor(ColorId, c);
        block.SetColor(BaseColorId, c);
        block.SetColor(EmissionColorId, e);
        renderer.SetPropertyBlock(block);
    }

    private void CleanupAvatarMaterial()
    {
        if (avatarMaterial == null) return;
        if (Application.isPlaying) Destroy(avatarMaterial);
        else DestroyImmediate(avatarMaterial);
        avatarMaterial = null;
    }

    private static bool IsHandBoneName(string lowerName)
    {
        bool handBone = lowerName.Contains("hand") ||
                        lowerName.Contains("forearm") ||
                        lowerName.Contains("shoulder") ||
                        lowerName.Contains("arm") ||
                        lowerName.Contains("thumb") ||
                        lowerName.Contains("index") ||
                        lowerName.Contains("middle") ||
                        lowerName.Contains("ring") ||
                        lowerName.Contains("pinky");
        bool bodyBone = lowerName.Contains("spine") ||
                        lowerName.Contains("hips") ||
                        lowerName.Contains("head") ||
                        lowerName.Contains("neck") ||
                        lowerName.Contains("leg") ||
                        lowerName.Contains("foot") ||
                        lowerName.Contains("toe");
        return handBone && !bodyBone;
    }

    private static bool BoneNameIsLeft(string lowerName)
    {
        return lowerName.Contains("left") || lowerName.Contains("_l") || lowerName.Contains(" l ");
    }

    private static bool BoneNameIsRight(string lowerName)
    {
        return lowerName.Contains("right") || lowerName.Contains("_r") || lowerName.Contains(" r ");
    }
}
