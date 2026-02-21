using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroRemoveActionIdBehaviours
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Remove ActionId Behaviours From States")]
    public static void RemoveBehaviours()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        int removed = 0;

        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;
            foreach (var child in sm.states)
            {
                var state = child.state;
                if (state == null) continue;
                foreach (var b in state.behaviours.ToArray())
                {
                    if (b == null) continue;
                    var typeName = b.GetType().Name;
                    if (typeName == "ResetActionIdOnEnter" || typeName == "ResetActionIdOnExit")
                    {
                        Object.DestroyImmediate(b, true);
                        removed++;
                    }
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Removed ActionId behaviours: {removed}");
    }
}
