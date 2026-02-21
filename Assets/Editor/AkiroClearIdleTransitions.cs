using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroClearIdleTransitions
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Clear Idle Transitions (Play-driven mode)")]
    public static void ClearIdleTransitions()
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
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Akiro_Idle")
                       ?? sm.states.Select(s => s.state).FirstOrDefault(s => s.name.EndsWith("_Idle"));
            if (idle == null) continue;

            // Remove all outgoing transitions from Idle
            var toRemove = idle.transitions.ToArray();
            foreach (var t in toRemove)
            {
                idle.RemoveTransition(t);
                removed++;
            }

            // Also remove Any State transitions to windup/slap/block
            var anyToRemove = sm.anyStateTransitions
                .Where(t => t.destinationState != null &&
                            (t.destinationState.name.EndsWith("_Windup") ||
                             t.destinationState.name.EndsWith("_Slap") ||
                             t.destinationState.name.StartsWith("Block")))
                .ToArray();
            foreach (var t in anyToRemove)
            {
                sm.RemoveAnyStateTransition(t);
                removed++;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cleared Idle/AnyState transitions: {removed}");
    }
}
