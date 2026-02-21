using UnityEngine;

[ExecuteAlways]
public class ForceLayerRecursively : MonoBehaviour
{
    [SerializeField] private int layerIndex = 8;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Apply()
    {
        int l = Mathf.Clamp(layerIndex, 0, 31);
        SetLayerRecursive(transform, l);
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursive(root.GetChild(i), layer);
        }
    }
}
