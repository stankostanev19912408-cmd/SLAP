using UnityEngine;

public class FaceBlendshapeProbe : MonoBehaviour
{
    [SerializeField] private bool runOnStart = true;

    private void Start()
    {
        if (!runOnStart) return;
        DumpBlendshapes();
    }

    [ContextMenu("Dump Blendshapes")]
    public void DumpBlendshapes()
    {
        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0)
        {
            Debug.Log($"[FaceBlendshapeProbe] {name}: no SkinnedMeshRenderer found.");
            return;
        }

        int total = 0;
        foreach (var smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            var mesh = smr.sharedMesh;
            int count = mesh.blendShapeCount;
            total += count;
            Debug.Log($"[FaceBlendshapeProbe] {name} -> {smr.name}: blendshapeCount={count}");
            for (int i = 0; i < count; i++)
            {
                string bn = mesh.GetBlendShapeName(i);
                float w = smr.GetBlendShapeWeight(i);
                Debug.Log($"[FaceBlendshapeProbe]   [{i}] {bn} (weight={w:0.##})");
            }
        }

        if (total == 0)
        {
            Debug.LogWarning($"[FaceBlendshapeProbe] {name}: no blendshapes found on any child mesh.");
        }
    }
}
