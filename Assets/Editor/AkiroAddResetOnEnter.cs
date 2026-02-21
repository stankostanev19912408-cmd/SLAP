using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroAddResetOnEnter
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Add ResetActionIdOnEnter")]
    public static void Add()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Akiro.controller not found at: " + ControllerPath);
            return;
        }

        var sm = controller.layers[0].stateMachine;
        int added = 0;

        foreach (var child in sm.states)
        {
            var state = child.state;
            if (!IsWindupOrBlock(state.name)) continue;
            if (state.behaviours.Any(b => b is ResetActionIdOnEnter)) continue;
            state.AddStateMachineBehaviour<ResetActionIdOnEnter>();
            added++;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"ResetActionIdOnEnter added to {added} states.");
    }

    private static bool IsWindupOrBlock(string name)
    {
        return name.EndsWith("_Windup") || name.Contains("_Block");
    }
}
