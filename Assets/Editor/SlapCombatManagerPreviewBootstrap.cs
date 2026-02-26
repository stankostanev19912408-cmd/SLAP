using UnityEditor;
#if UNITY_2021_1_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEngine;

[InitializeOnLoad]
public static class SlapCombatManagerPreviewBootstrap
{
    private static bool AutoCreateManagerInEditor => false;

    static SlapCombatManagerPreviewBootstrap()
    {
        EditorApplication.delayCall += EnsurePreviewManagerExists;
        EditorApplication.hierarchyChanged += EnsurePreviewManagerExists;
    }

    private static void EnsurePreviewManagerExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;

        var fighters = Object.FindObjectsOfType<SlapMechanics>(true);
        if (fighters == null || fighters.Length < 2) return;

        var manager = Object.FindObjectOfType<SlapCombatManager>(true);
        if (manager == null)
        {
            if (!AutoCreateManagerInEditor)
            {
                return;
            }
            var go = new GameObject("SlapCombatManager");
            manager = go.AddComponent<SlapCombatManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create SlapCombatManager");
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        var so = new SerializedObject(manager);
        bool changed = false;
        changed |= SetBool(so, "createHandsOutsidePlay", true);
        changed |= SetBool(so, "createHandsInPlay", true);
        changed |= SetBool(so, "autoPositionEditHands", true);
        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }

    private static bool SetBool(SerializedObject so, string fieldName, bool value)
    {
        var p = so.FindProperty(fieldName);
        if (p == null || p.propertyType != SerializedPropertyType.Boolean) return false;
        if (p.boolValue == value) return false;
        p.boolValue = value;
        return true;
    }
}
