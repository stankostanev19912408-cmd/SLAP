using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnsureIdleHandsCopy
{
    private const string TargetName = "IdleHandsCopy";

    static EnsureIdleHandsCopy()
    {
        EditorApplication.delayCall += EnsureInOpenScene;
        EditorApplication.hierarchyChanged += EnsureInOpenScene;
    }

    private static void EnsureInOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var existing = GameObject.Find(TargetName);
        if (existing != null) return;

        var go = new GameObject(TargetName);
        var comp = go.AddComponent<HandsOnlyIdleCopy>();
        var so = new SerializedObject(comp);
        so.FindProperty("sourceRootName").stringValue = "idle";
        so.FindProperty("worldOffset").vector3Value = new Vector3(0f, 0f, 0.6f);
        so.FindProperty("followSource").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
