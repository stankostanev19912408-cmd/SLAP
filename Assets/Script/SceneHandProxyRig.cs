using UnityEngine;

[ExecuteAlways]
public class SceneHandProxyRig : MonoBehaviour
{
    [SerializeField] private string playerName = "idle";
    [SerializeField] private string opponentName = "idle (1)";
    [SerializeField] private float proxyScale = 0.22f;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [Header("Camera Offset")]
    [SerializeField] private bool adjustOpponentToCamera = true;
    [SerializeField] private bool adjustPlayerToCamera = true;
    [SerializeField] private Vector3 opponentCameraOffset = new Vector3(0.18f, -0.05f, 0.15f);
    [SerializeField, Range(0f, 0.45f)] private float opponentViewMargin = 0.12f;
    [SerializeField] private Vector3 playerLeftRotationOffset = new Vector3(0f, 90f, 0f);
    [SerializeField] private Vector3 playerRightRotationOffset = new Vector3(0f, 270f, 0f);
    [SerializeField] private Vector3 opponentLeftRotationOffset = new Vector3(0f, 270f, 0f);
    [SerializeField] private Vector3 opponentRightRotationOffset = new Vector3(0f, 90f, 0f);
    [SerializeField] private Vector3 playerLeftPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 playerRightPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 opponentLeftPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 opponentRightPositionOffset = Vector3.zero;
    [Header("Screen Centering")]
    [SerializeField] private bool centerHandsOnScreen = true;
    [SerializeField] private Vector2 playerScreenCenter = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 opponentScreenCenter = new Vector2(0.5f, 0.5f);
    [SerializeField, Range(0f, 1f)] private float centerStrength = 1f;
    [SerializeField] private bool matchOpponentToPlayerPlacement = true;
    [Header("Simple Copy Mode")]
    [SerializeField] private bool simpleCopyMode = true;
    [SerializeField] private bool useOwnerSpaceOffsetInSimpleMode = true;
    [SerializeField] private Vector3 playerOwnerSpaceOffset = new Vector3(0f, 0.18f, 0.35f);
    [SerializeField] private Vector3 opponentOwnerSpaceOffset = new Vector3(0f, 0.18f, 0.35f);
    [SerializeField] private bool lockEditPlacementInPlay = false;
    [Header("Facing")]
    [SerializeField] private bool faceOpponent = false;
    [Header("Hand Color From Texture")]
    [SerializeField] private bool useTextureHandColor = true;
    [SerializeField] private Texture2D handColorTexture = null;
    [SerializeField] private Rect handColorUvRect = new Rect(0f, 0f, 1f, 1f);
    [SerializeField, Range(2, 64)] private int handColorSamples = 16;
    private bool warnedNonReadable;
    [Header("Mirror")]
    [SerializeField] private bool mirrorHandsVertically = true;
    [Header("Swap Visuals")]
    [SerializeField] private bool swapLeftRightMeshes = true;
    private bool visualsSwapped = false;
    [Header("Owner Arm Mesh")]
    [SerializeField] private bool useOwnerArmMeshes = true;
    [SerializeField, Range(0f, 1f)] private float armBoneWeightThreshold = 0.15f;
    private Transform armsRoot;
    private SkinnedMeshRenderer playerLeftArmRenderer;
    private SkinnedMeshRenderer playerRightArmRenderer;
    private SkinnedMeshRenderer opponentLeftArmRenderer;
    private SkinnedMeshRenderer opponentRightArmRenderer;
    [SerializeField] private bool manualOverrideAll = false;
    [SerializeField] private bool manualPlayerLeft = false;
    [SerializeField] private bool manualPlayerRight = false;
    [SerializeField] private bool manualOpponentLeft = false;
    [SerializeField] private bool manualOpponentRight = false;
    [SerializeField] private bool useManualOffsets = true;

    private Transform root;
    private Transform playerLeft;
    private Transform playerRight;
    private Transform opponentLeft;
    private Transform opponentRight;

    private void OnEnable()
    {
        EnsureHierarchy();
    }

    private void Update()
    {
        EnsureHierarchy();
        if (!Application.isPlaying)
        {
            ApplyProxies();
        }
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            ApplyProxies();
        }
    }

    private void ApplyProxies()
    {
        Transform player = FindByExactName(playerName);
        Transform opponent = FindByExactName(opponentName);
        if (player == null || opponent == null)
        {
            ResolveOwnersFallback(out player, out opponent);
        }
        if (player == null || opponent == null) return;

        ResolveHands(player, out var pLeftHand, out var pRightHand);
        ResolveHands(opponent, out var oLeftHand, out var oRightHand);

        if (useOwnerArmMeshes)
        {
            EnsureArmMeshes(player, opponent);
            DisableProxyRenderers();
        }

        Vector3 playerGroupDelta = Vector3.zero;
        Vector3 opponentGroupDelta = Vector3.zero;
        if (!simpleCopyMode && centerHandsOnScreen && !(lockEditPlacementInPlay && Application.isPlaying))
        {
            playerGroupDelta = ComputeGroupDelta(pLeftHand, pRightHand, playerScreenCenter, centerStrength);
            opponentGroupDelta = ComputeGroupDelta(oLeftHand, oRightHand, opponentScreenCenter, centerStrength);
        }
        if (!simpleCopyMode && matchOpponentToPlayerPlacement && !(lockEditPlacementInPlay && Application.isPlaying))
        {
            opponentGroupDelta = playerGroupDelta;
        }

        if (!manualOverrideAll && !manualPlayerLeft)
            PlaceProxy(playerLeft, pLeftHand, player, opponent, true, false, playerLeftPositionOffset, playerLeftRotationOffset, playerGroupDelta);
        if (!manualOverrideAll && !manualPlayerRight)
            PlaceProxy(playerRight, pRightHand, player, opponent, false, false, playerRightPositionOffset, playerRightRotationOffset, playerGroupDelta);
        if (!manualOverrideAll && !manualOpponentLeft)
            PlaceProxy(opponentLeft, oLeftHand, opponent, player, true, true, opponentLeftPositionOffset, opponentLeftRotationOffset, opponentGroupDelta);
        if (!manualOverrideAll && !manualOpponentRight)
            PlaceProxy(opponentRight, oRightHand, opponent, player, false, true, opponentRightPositionOffset, opponentRightRotationOffset, opponentGroupDelta);
    }

    private void EnsureHierarchy()
    {
        if (root == null)
        {
            var existing = transform.Find("VisibleHandProxies");
            if (existing != null) root = existing;
            else
            {
                var go = new GameObject("VisibleHandProxies");
                root = go.transform;
                root.SetParent(transform, false);
            }
        }

        var playerColor = ResolveOwnerColor(FindByExactName(playerName));
        var opponentColor = ResolveOwnerColor(FindByExactName(opponentName));
        if (useTextureHandColor)
        {
            var texColor = SampleTextureColor(handColorTexture, handColorUvRect, handColorSamples);
            if (texColor.a > 0f)
            {
                playerColor = texColor;
                opponentColor = texColor;
            }
        }
        playerLeft = EnsureSphere(root, "Player_Left", playerColor);
        playerRight = EnsureSphere(root, "Player_Right", playerColor);
        opponentLeft = EnsureSphere(root, "Opponent_Left", opponentColor);
        opponentRight = EnsureSphere(root, "Opponent_Right", opponentColor);

        if (swapLeftRightMeshes && !visualsSwapped)
        {
            SwapProxyVisuals(playerLeft, playerRight);
            SwapProxyVisuals(opponentLeft, opponentRight);
            visualsSwapped = true;
        }
        else if (!swapLeftRightMeshes && visualsSwapped)
        {
            SwapProxyVisuals(playerLeft, playerRight);
            SwapProxyVisuals(opponentLeft, opponentRight);
            visualsSwapped = false;
        }
    }

    private Transform EnsureSphere(Transform parent, string name, Color color)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.name = name;
            t = go.transform;
            t.SetParent(parent, false);
            t.localScale = Vector3.one * Mathf.Max(0.06f, proxyScale);
        }

        EnsureOverrideComponent(t);

        // If user put custom FBX/renderer under this proxy, do not overwrite mesh/scale.
        if (HasCustomVisual(t))
        {
            return t;
        }

        // Ensure root proxy itself is a sphere renderer.
        var mf = t.GetComponent<MeshFilter>();
        var mr = t.GetComponent<MeshRenderer>();
        if (mf == null || mr == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var srcMf = temp.GetComponent<MeshFilter>();
            if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = srcMf != null ? srcMf.sharedMesh : mf.sharedMesh;
            if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
            var col = temp.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            DestroyImmediate(temp);
        }

        if (mr != null)
        {
            if (mr.sharedMaterial == null || mr.sharedMaterial.shader == null || mr.sharedMaterial.shader.name != "Unlit/Color")
            {
                var mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = color;
                mr.sharedMaterial = mat;
            }
            else
            {
                mr.sharedMaterial.color = color;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = true;
        }
        return t;
    }

    private static bool HasCustomVisual(Transform proxy)
    {
        if (proxy == null) return false;

        var rootSkinned = proxy.GetComponent<SkinnedMeshRenderer>();
        if (rootSkinned != null) return true;
        var rootMf = proxy.GetComponent<MeshFilter>();
        var rootMr = proxy.GetComponent<MeshRenderer>();
        if (rootMf != null && rootMr != null && rootMf.sharedMesh != null)
        {
            string meshName = rootMf.sharedMesh.name;
            if (string.IsNullOrEmpty(meshName) || !meshName.ToLowerInvariant().Contains("sphere"))
            {
                return true;
            }
        }

        for (int i = 0; i < proxy.childCount; i++)
        {
            var ch = proxy.GetChild(i);
            if (ch == null) continue;
            if (ch.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return true;
            var childRenderer = ch.GetComponentInChildren<Renderer>(true);
            if (childRenderer != null)
            {
                return true;
            }
        }
        return false;
    }

    private static Transform FindByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var all = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t != null && string.Equals(t.name, exactName, System.StringComparison.Ordinal))
            {
                return t;
            }
        }
        return null;
    }

    private static Color ResolveOwnerColor(Transform owner)
    {
        if (owner == null) return Color.white;
        var renderer = owner.GetComponentInChildren<Renderer>(true);
        if (renderer == null || renderer.sharedMaterial == null) return Color.white;
        if (renderer.sharedMaterial.HasProperty("_Color"))
        {
            return renderer.sharedMaterial.color;
        }
        return Color.white;
    }

    private void EnsureArmMeshes(Transform player, Transform opponent)
    {
        if (armsRoot == null)
        {
            var existing = transform.Find("VisibleHandProxies/VisibleArmMeshes");
            if (existing != null) armsRoot = existing;
            else
            {
                var go = new GameObject("VisibleArmMeshes");
                armsRoot = go.transform;
                armsRoot.SetParent(root != null ? root : transform, false);
            }
        }

        if (player != null)
        {
            EnsureArmMeshForOwner(player, ref playerLeftArmRenderer, ref playerRightArmRenderer, "Player", false);
        }
        if (opponent != null)
        {
            EnsureArmMeshForOwner(opponent, ref opponentLeftArmRenderer, ref opponentRightArmRenderer, "Opponent", true);
        }
    }

    private void EnsureArmMeshForOwner(
        Transform owner,
        ref SkinnedMeshRenderer leftRenderer,
        ref SkinnedMeshRenderer rightRenderer,
        string prefix,
        bool isOpponent)
    {
        if (owner == null || armsRoot == null) return;
        var src = owner.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (src == null || src.sharedMesh == null) return;

        leftRenderer = EnsureArmRenderer(leftRenderer, $"{prefix}_LeftArm", owner, src);
        rightRenderer = EnsureArmRenderer(rightRenderer, $"{prefix}_RightArm", owner, src);

        var leftBones = GetArmBoneIndices(src, true);
        var rightBones = GetArmBoneIndices(src, false);
        leftRenderer.sharedMesh = BuildArmMesh(src.sharedMesh, leftBones, armBoneWeightThreshold);
        rightRenderer.sharedMesh = BuildArmMesh(src.sharedMesh, rightBones, armBoneWeightThreshold);

        leftRenderer.sharedMaterials = src.sharedMaterials;
        rightRenderer.sharedMaterials = src.sharedMaterials;
        leftRenderer.bones = src.bones;
        rightRenderer.bones = src.bones;
        leftRenderer.rootBone = src.rootBone;
        rightRenderer.rootBone = src.rootBone;
        leftRenderer.updateWhenOffscreen = true;
        rightRenderer.updateWhenOffscreen = true;
    }

    private SkinnedMeshRenderer EnsureArmRenderer(
        SkinnedMeshRenderer current,
        string name,
        Transform owner,
        SkinnedMeshRenderer src)
    {
        if (current != null) return current;
        var go = new GameObject(name);
        go.transform.SetParent(armsRoot, false);
        var r = go.AddComponent<SkinnedMeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    private static int[] GetArmBoneIndices(SkinnedMeshRenderer src, bool left)
    {
        var animator = src.GetComponentInParent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return new int[0];
        }
        var bones = src.bones;
        Transform upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var list = new System.Collections.Generic.List<int>(3);
        if (upper != null) list.Add(System.Array.IndexOf(bones, upper));
        if (lower != null) list.Add(System.Array.IndexOf(bones, lower));
        if (hand != null) list.Add(System.Array.IndexOf(bones, hand));
        list.RemoveAll(i => i < 0);
        return list.ToArray();
    }

    private static Mesh BuildArmMesh(Mesh src, int[] armBoneIndices, float threshold)
    {
        if (src == null) return null;
        if (armBoneIndices == null || armBoneIndices.Length == 0)
        {
            return src;
        }

        var boneWeights = src.boneWeights;
        if (boneWeights == null || boneWeights.Length == 0)
        {
            return src;
        }

        bool[] useVertex = new bool[boneWeights.Length];
        for (int i = 0; i < boneWeights.Length; i++)
        {
            BoneWeight bw = boneWeights[i];
            if (IsArmWeight(bw, armBoneIndices, threshold))
            {
                useVertex[i] = true;
            }
        }

        var mesh = new Mesh();
        mesh.name = $"{src.name}_Arms";
        mesh.vertices = src.vertices;
        if (src.normals != null && src.normals.Length == src.vertexCount) mesh.normals = src.normals;
        if (src.tangents != null && src.tangents.Length == src.vertexCount) mesh.tangents = src.tangents;
        if (src.uv != null && src.uv.Length == src.vertexCount) mesh.uv = src.uv;
        if (src.uv2 != null && src.uv2.Length == src.vertexCount) mesh.uv2 = src.uv2;
        if (src.colors != null && src.colors.Length == src.vertexCount) mesh.colors = src.colors;
        mesh.boneWeights = src.boneWeights;
        mesh.bindposes = src.bindposes;
        mesh.subMeshCount = src.subMeshCount;

        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tris = src.GetTriangles(s);
            var filtered = new System.Collections.Generic.List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i];
                int b = tris[i + 1];
                int c = tris[i + 2];
                if (useVertex[a] || useVertex[b] || useVertex[c])
                {
                    filtered.Add(a);
                    filtered.Add(b);
                    filtered.Add(c);
                }
            }
            mesh.SetTriangles(filtered, s);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    private static bool IsArmWeight(BoneWeight bw, int[] armBoneIndices, float threshold)
    {
        if (bw.weight0 >= threshold && System.Array.IndexOf(armBoneIndices, bw.boneIndex0) >= 0) return true;
        if (bw.weight1 >= threshold && System.Array.IndexOf(armBoneIndices, bw.boneIndex1) >= 0) return true;
        if (bw.weight2 >= threshold && System.Array.IndexOf(armBoneIndices, bw.boneIndex2) >= 0) return true;
        if (bw.weight3 >= threshold && System.Array.IndexOf(armBoneIndices, bw.boneIndex3) >= 0) return true;
        return false;
    }

    private void DisableProxyRenderers()
    {
        DisableProxyRenderer(playerLeft);
        DisableProxyRenderer(playerRight);
        DisableProxyRenderer(opponentLeft);
        DisableProxyRenderer(opponentRight);
    }

    private static void DisableProxyRenderer(Transform proxy)
    {
        if (proxy == null) return;
        var mr = proxy.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
    }

    private Color SampleTextureColor(Texture2D tex, Rect uvRect, int samples)
    {
        if (tex == null) return new Color(0f, 0f, 0f, 0f);
        if (!tex.isReadable)
        {
            if (!warnedNonReadable)
            {
                warnedNonReadable = true;
                Debug.LogWarning($"[SceneHandProxyRig] Texture '{tex.name}' is not readable. Enable Read/Write in import settings.");
            }
            return new Color(0f, 0f, 0f, 0f);
        }

        float u0 = Mathf.Clamp01(uvRect.x);
        float v0 = Mathf.Clamp01(uvRect.y);
        float u1 = Mathf.Clamp01(uvRect.x + uvRect.width);
        float v1 = Mathf.Clamp01(uvRect.y + uvRect.height);
        if (u1 <= u0 || v1 <= v0) return new Color(0f, 0f, 0f, 0f);

        int n = Mathf.Max(2, samples);
        Color sum = Color.black;
        int count = 0;
        for (int y = 0; y < n; y++)
        {
            float v = Mathf.Lerp(v0, v1, (y + 0.5f) / n);
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Lerp(u0, u1, (x + 0.5f) / n);
                Color c = tex.GetPixelBilinear(u, v);
                sum += c;
                count++;
            }
        }
        if (count == 0) return new Color(0f, 0f, 0f, 0f);
        Color avg = sum / count;
        avg.a = 1f;
        return avg;
    }

    private void ApplyHorizontalMirror(Transform proxy)
    {
        if (!mirrorHandsVertically || proxy == null || !Application.isPlaying) return;
        Vector3 s = proxy.localScale;
        s.y = -Mathf.Abs(s.y);
        proxy.localScale = s;
    }

    private static void SwapProxyVisuals(Transform left, Transform right)
    {
        if (left == null || right == null) return;

        var leftSkin = left.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var rightSkin = right.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (leftSkin != null && rightSkin != null)
        {
            var mesh = leftSkin.sharedMesh;
            var mats = leftSkin.sharedMaterials;
            leftSkin.sharedMesh = rightSkin.sharedMesh;
            leftSkin.sharedMaterials = rightSkin.sharedMaterials;
            rightSkin.sharedMesh = mesh;
            rightSkin.sharedMaterials = mats;
            return;
        }

        var leftMf = left.GetComponentInChildren<MeshFilter>(true);
        var rightMf = right.GetComponentInChildren<MeshFilter>(true);
        if (leftMf != null && rightMf != null)
        {
            var mesh = leftMf.sharedMesh;
            leftMf.sharedMesh = rightMf.sharedMesh;
            rightMf.sharedMesh = mesh;
        }

        var leftMr = left.GetComponentInChildren<MeshRenderer>(true);
        var rightMr = right.GetComponentInChildren<MeshRenderer>(true);
        if (leftMr != null && rightMr != null)
        {
            var mats = leftMr.sharedMaterials;
            leftMr.sharedMaterials = rightMr.sharedMaterials;
            rightMr.sharedMaterials = mats;
        }
    }

    private static void ResolveOwnersFallback(out Transform player, out Transform opponent)
    {
        player = null;
        opponent = null;
        var all = FindObjectsOfType<SlapMechanics>(true);
        if (all == null || all.Length == 0) return;
        if (all[0] != null) player = all[0].transform;
        for (int i = 1; i < all.Length; i++)
        {
            if (all[i] != null && all[i].transform != player)
            {
                opponent = all[i].transform;
                break;
            }
        }
    }

    private static void ResolveHands(Transform owner, out Transform leftHand, out Transform rightHand)
    {
        leftHand = null;
        rightHand = null;
        if (owner == null) return;

        var animator = owner.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (leftHand != null && rightHand != null) return;

        var all = owner.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (leftHand == null && (n.Contains("lefthand") || n.Contains("hand_l") || n.Contains("l_hand")))
            {
                leftHand = t;
            }
            if (rightHand == null && (n.Contains("righthand") || n.Contains("hand_r") || n.Contains("r_hand")))
            {
                rightHand = t;
            }
        }
    }

    private void PlaceProxy(
        Transform proxy,
        Transform hand,
        Transform owner,
        Transform otherOwner,
        bool isLeft,
        bool isOpponent,
        Vector3 perProxyPosOffset,
        Vector3 perProxyRotOffset,
        Vector3 groupWorldOffset)
    {
        if (proxy == null || owner == null) return;

        Vector3 pos;
        Quaternion rot;
        if (hand != null)
        {
            pos = hand.position;
            rot = hand.rotation * Quaternion.Euler(rotationOffsetEuler) * Quaternion.Euler(perProxyRotOffset);
            if (faceOpponent)
            {
                rot = ApplyFacing(rot, owner, otherOwner);
            }
            pos = pos + positionOffset + perProxyPosOffset + groupWorldOffset;
            if (!simpleCopyMode && !(lockEditPlacementInPlay && Application.isPlaying))
            {
                if ((isOpponent && adjustOpponentToCamera) || (!isOpponent && adjustPlayerToCamera))
                {
                    pos = ApplyCameraOffset(pos, opponentCameraOffset);
                    pos = ClampToView(pos, opponentViewMargin);
                }
            }
            else if (!simpleCopyMode)
            {
                // no-op, kept for clarity
            }
            else if (useOwnerSpaceOffsetInSimpleMode && !(lockEditPlacementInPlay && Application.isPlaying))
            {
                pos += OwnerSpaceOffset(owner, isOpponent);
            }
            proxy.position = pos;
            proxy.rotation = rot;
            proxy.localScale = GetLocalScaleForLossy(proxy.parent, hand.lossyScale);
            if (useManualOffsets)
            {
                ApplyManualOffsets(proxy, ref pos, ref rot);
            }
            ApplyHorizontalMirror(proxy);
            return;
        }
        else
        {
            float side = isLeft ? -0.25f : 0.25f;
            pos = owner.position + owner.up * 1.2f + owner.forward * 0.28f + owner.right * side;
            rot = owner.rotation * Quaternion.Euler(rotationOffsetEuler) * Quaternion.Euler(perProxyRotOffset);
            if (faceOpponent)
            {
                rot = ApplyFacing(rot, owner, otherOwner);
            }
            pos += positionOffset + perProxyPosOffset + groupWorldOffset;
            if (!simpleCopyMode && !(lockEditPlacementInPlay && Application.isPlaying))
            {
                if ((isOpponent && adjustOpponentToCamera) || (!isOpponent && adjustPlayerToCamera))
                {
                    pos = ApplyCameraOffset(pos, opponentCameraOffset);
                    pos = ClampToView(pos, opponentViewMargin);
                }
            }
            else if (!simpleCopyMode)
            {
                // no-op, kept for clarity
            }
            else if (useOwnerSpaceOffsetInSimpleMode && !(lockEditPlacementInPlay && Application.isPlaying))
            {
                pos += OwnerSpaceOffset(owner, isOpponent);
            }
        }

        proxy.position = pos;
        proxy.rotation = rot;
        if (useManualOffsets)
        {
            ApplyManualOffsets(proxy, ref pos, ref rot);
        }
        ApplyHorizontalMirror(proxy);
    }

    private static Vector3 GetLocalScaleForLossy(Transform parent, Vector3 targetLossy)
    {
        if (parent == null) return targetLossy;
        Vector3 p = parent.lossyScale;
        return new Vector3(
            p.x == 0f ? targetLossy.x : targetLossy.x / p.x,
            p.y == 0f ? targetLossy.y : targetLossy.y / p.y,
            p.z == 0f ? targetLossy.z : targetLossy.z / p.z
        );
    }

    private Vector3 ApplyCameraOffset(Vector3 worldPos, Vector3 cameraSpaceOffset)
    {
        var cam = Camera.main;
        if (cam == null) return worldPos;
        Transform ct = cam.transform;
        return worldPos
               + ct.right * cameraSpaceOffset.x
               + ct.up * cameraSpaceOffset.y
               + ct.forward * cameraSpaceOffset.z;
    }

    private Vector3 ComputeGroupDelta(Transform leftHand, Transform rightHand, Vector2 screenTarget01, float strength)
    {
        if (leftHand == null || rightHand == null) return Vector3.zero;
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 mid = (leftHand.position + rightHand.position) * 0.5f;
        Vector3 sp = cam.WorldToScreenPoint(mid);
        if (sp.z <= 0.001f) return Vector3.zero;

        float w = Mathf.Max(1f, cam.pixelWidth);
        float h = Mathf.Max(1f, cam.pixelHeight);
        Vector3 targetSp = new Vector3(w * screenTarget01.x, h * screenTarget01.y, sp.z);
        Vector3 targetWorld = cam.ScreenToWorldPoint(targetSp);
        Vector3 delta = targetWorld - mid;
        return Vector3.Lerp(Vector3.zero, delta, Mathf.Clamp01(strength));
    }

    private Vector3 OwnerSpaceOffset(Transform owner, bool isOpponent)
    {
        if (owner == null) return Vector3.zero;
        Vector3 local = isOpponent ? opponentOwnerSpaceOffset : playerOwnerSpaceOffset;
        return owner.right * local.x + owner.up * local.y + owner.forward * local.z;
    }

    private Quaternion ApplyFacing(Quaternion baseRot, Transform owner, Transform otherOwner)
    {
        if (owner == null || otherOwner == null) return baseRot;
        Vector3 dir = otherOwner.position - owner.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return baseRot;
        Quaternion yaw = Quaternion.FromToRotation(owner.forward, dir.normalized);
        return yaw * baseRot;
    }

    private Vector3 ClampToView(Vector3 worldPos, float margin)
    {
        var cam = Camera.main;
        if (cam == null) return worldPos;

        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0.001f) return worldPos;

        float w = Mathf.Max(1f, cam.pixelWidth);
        float h = Mathf.Max(1f, cam.pixelHeight);
        float minX = w * margin;
        float maxX = w * (1f - margin);
        float minY = h * margin;
        float maxY = h * (1f - margin);

        float clampedX = Mathf.Clamp(sp.x, minX, maxX);
        float clampedY = Mathf.Clamp(sp.y, minY, maxY);
        if (Mathf.Approximately(clampedX, sp.x) && Mathf.Approximately(clampedY, sp.y)) return worldPos;

        Vector3 spClamped = new Vector3(clampedX, clampedY, sp.z);
        Vector3 w0 = cam.ScreenToWorldPoint(sp);
        Vector3 w1 = cam.ScreenToWorldPoint(spClamped);
        return worldPos + (w1 - w0);
    }

    private void ApplyManualOffsets(Transform proxy, ref Vector3 pos, ref Quaternion rot)
    {
        if (proxy == null) return;
        var cache = proxy.GetComponent<ProxyTransformOverride>();
        if (cache == null) return;

        Transform parent = proxy.parent;
        if (parent == null)
        {
            proxy.position = pos;
            proxy.rotation = rot;
            return;
        }

        Vector3 baseLocalPos = parent.InverseTransformPoint(pos);
        Quaternion baseLocalRot = Quaternion.Inverse(parent.rotation) * rot;

        cache.UpdateBasePose(baseLocalPos, baseLocalRot);

        if (!Application.isPlaying && cache.TryConsumeManualEditLocal(proxy, baseLocalPos, baseLocalRot))
        {
            // Offsets were updated from the user's Transform edits in the editor.
        }

        Vector3 finalLocalPos = baseLocalPos + cache.PositionOffset;
        Quaternion finalLocalRot = baseLocalRot * Quaternion.Euler(cache.RotationOffset);

        proxy.localPosition = finalLocalPos;
        proxy.localRotation = finalLocalRot;
        if (cache.ScaleMultiplier != Vector3.one)
        {
            proxy.localScale = Vector3.Scale(cache.BaseScale, cache.ScaleMultiplier);
        }
        cache.LastAppliedLocalPosition = finalLocalPos;
        cache.LastAppliedLocalRotation = finalLocalRot;
        cache.LastAppliedScale = proxy.localScale;
    }

    private void EnsureOverrideComponent(Transform proxy)
    {
        if (proxy == null) return;
        var cache = proxy.GetComponent<ProxyTransformOverride>();
        if (cache == null) cache = proxy.gameObject.AddComponent<ProxyTransformOverride>();
        if (cache.BaseScale == Vector3.zero)
        {
            cache.BaseScale = proxy.localScale == Vector3.zero ? Vector3.one : proxy.localScale;
        }
        if (cache.LastAppliedScale == Vector3.zero)
        {
            cache.LastAppliedScale = proxy.localScale == Vector3.zero ? Vector3.one : proxy.localScale;
        }
    }
}
