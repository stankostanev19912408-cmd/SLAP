using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroRemoveReturnStates
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Remove Return States")]
    public static void RemoveReturnStates()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        int removedStates = 0;
        int removedTransitions = 0;

        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;

            // Remove transitions that go to Return states (Any State)
            var anyToReturn = sm.anyStateTransitions
                .Where(t => t.destinationState != null && t.destinationState.name.EndsWith("_Return"))
                .ToArray();
            foreach (var t in anyToReturn)
            {
                sm.RemoveAnyStateTransition(t);
                removedTransitions++;
            }

            // Remove transitions that go to Return states (from other states)
            foreach (var child in sm.states)
            {
                var state = child.state;
                var toReturn = state.transitions
                    .Where(t => t.destinationState != null && t.destinationState.name.EndsWith("_Return"))
                    .ToArray();
                foreach (var t in toReturn)
                {
                    state.RemoveTransition(t);
                    removedTransitions++;
                }
            }

            // Remove Return states themselves
            var returnStates = sm.states
                .Where(s => s.state != null && s.state.name.EndsWith("_Return"))
                .ToArray();
            foreach (var child in returnStates)
            {
                sm.RemoveState(child.state);
                removedStates++;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Removed Return states: {removedStates}, transitions removed: {removedTransitions}");
    }
}
