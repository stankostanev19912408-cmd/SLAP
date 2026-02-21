using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class HandsOnlyIdleCopy : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private string sourceRootName = "idle";
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0f, 0.6f);
    [SerializeField] private bool followSource = true;

    [Header("Mesh Build")]
    [SerializeField, Range(0f, 1f)] private float handWeightThreshold = 0.45f;
    [SerializeField] private bool disableOriginalRenderers = false;

    private Transform sourceRoot;
    private SkinnedMeshRenderer sourceRenderer;
    private SkinnedMeshRenderer leftRenderer;
    private SkinnedMeshRenderer rightRenderer;

    private void OnEnable()
    {
        TryBuild(true);
    }

    private void Start()
    {
        TryBuild(false);
    }

    private void LateUpdate()
    {
        if (!followSource || sourceRoot == null) return;
        SyncTransform();
    }

    private void TryBuild(bool forceRebuild)
    {
        if (forceRebuild)
        {
            ClearGeneratedChildren();
            leftRenderer = null;
            rightRenderer = null;
        }
        if (leftRenderer != null || rightRenderer != null) return;
        ResolveSource();
        if (sourceRenderer == null) return;
        SyncTransform();
        BuildHandsOnlyMeshes();
    }

    private void ClearGeneratedChildren()
    {
        var toDelete = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (child.name == "LeftHandOnlyMesh" || child.name == "RightHandOnlyMesh")
            {
                toDelete.Add(child.gameObject);
            }
        }

        foreach (var go in toDelete)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    private void ResolveSource()
    {
        if (!string.IsNullOrEmpty(sourceRootName))
        {
            sourceRoot = GameObject.Find(sourceRootName)?.transform;
        }

        if (sourceRoot == null)
        {
            sourceRoot = GameObject.Find("idle")?.transform;
        }

        if (sourceRoot == null)
        {
            sourceRoot = GameObject.Find("idle (1)")?.transform;
        }

        if (sourceRoot == null)
        {
            var anyRenderer = FindFirstObjectByType<SkinnedMeshRenderer>();
            sourceRenderer = anyRenderer;
            sourceRoot = anyRenderer != null ? anyRenderer.transform.root : null;
            return;
        }

        sourceRenderer = sourceRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
    }

    private void SyncTransform()
    {
        transform.position = sourceRoot.position + worldOffset;
        transform.rotation = sourceRoot.rotation;
        transform.localScale = sourceRoot.lossyScale;
    }

    private void BuildHandsOnlyMeshes()
    {
        var src = sourceRenderer;
        if (src == null || src.sharedMesh == null) return;
        if (src.bones == null || src.bones.Length == 0) return;
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

        if (leftBones.Count == 0 || rightBones.Count == 0) return;

        Mesh leftMesh = BuildHandOnlyMesh(src, leftBones, "_LeftHandOnly");
        Mesh rightMesh = BuildHandOnlyMesh(src, rightBones, "_RightHandOnly");

        if (leftMesh != null)
        {
            leftRenderer = CreateHandRenderer(src, "LeftHandOnlyMesh", leftMesh);
        }
        if (rightMesh != null)
        {
            rightRenderer = CreateHandRenderer(src, "RightHandOnlyMesh", rightMesh);
        }

        if (disableOriginalRenderers && sourceRoot != null)
        {
            foreach (var r in sourceRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = false;
            }
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
        smr.bones = source.bones;
        smr.rootBone = source.rootBone;
        smr.updateWhenOffscreen = source.updateWhenOffscreen;
        if (source.sharedMaterials != null && source.sharedMaterials.Length > 0)
        {
            smr.sharedMaterial = source.sharedMaterials[0];
        }
        return smr;
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
