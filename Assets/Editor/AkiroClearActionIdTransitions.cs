using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroClearActionIdTransitions
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";
    private const string ActionIdParam = "ActionID";

    [MenuItem("Tools/Akiro/Clear ActionID Transitions (No ActionID mode)")]
    public static void ClearActionIdTransitions()
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

            // Remove Any State transitions that target slap/block/windup states
            var anyToRemove = sm.anyStateTransitions
                .Where(t => t.destinationState != null && IsSlapBlockOrWindup(t.destinationState.name))
                .ToArray();
            foreach (var t in anyToRemove)
            {
                sm.RemoveAnyStateTransition(t);
                removed++;
            }

            // Remove transitions that depend on ActionID
            foreach (var child in sm.states)
            {
                var state = child.state;
                var toRemove = state.transitions
                    .Where(t => t.conditions.Any(c => c.parameter == ActionIdParam))
                    .ToArray();
                foreach (var t in toRemove)
                {
                    state.RemoveTransition(t);
                    removed++;
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cleared ActionID transitions: {removed}");
    }

    private static bool IsSlapBlockOrWindup(string name)
    {
        if (name.EndsWith("_Windup")) return true;
        if (name.EndsWith("_Slap")) return true;
        if (name.StartsWith("Block")) return true;
        return false;
    }
}
