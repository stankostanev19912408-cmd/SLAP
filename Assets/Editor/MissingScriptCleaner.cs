using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptCleaner
{
    [MenuItem("Tools/Slap/Clean Missing Scripts In Open Scene")]
    private static void CleanOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[MissingScriptCleaner] Active scene is not valid.");
            return;
        }

        int removedTotal = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            removedTotal += CleanRecursive(roots[i]);
        }

        if (removedTotal > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[MissingScriptCleaner] Removed missing scripts: {removedTotal}");
    }

    private static int CleanRecursive(GameObject go)
    {
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            removed += CleanRecursive(t.GetChild(i).gameObject);
        }

        return removed;
    }
}
