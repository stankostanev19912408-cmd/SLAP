using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroAnimatorReport
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Report Transitions")]
    public static void ReportTransitions()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Akiro.controller not found at: " + ControllerPath);
            return;
        }

        var sm = controller.layers[0].stateMachine;
        Debug.Log("=== Any State Transitions ===");
        foreach (var t in sm.anyStateTransitions)
        {
            Debug.Log(FormatTransition("Any State", t));
        }

        Debug.Log("=== State Transitions ===");
        foreach (var child in sm.states)
        {
            var state = child.state;
            foreach (var t in state.transitions)
            {
                Debug.Log(FormatTransition(state.name, t));
            }
        }
    }

    private static string FormatTransition(string from, AnimatorStateTransition t)
    {
        string to = t.destinationState != null ? t.destinationState.name : "(null)";
        string cond = t.conditions != null && t.conditions.Length > 0
            ? string.Join(", ", t.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold}"))
            : "(no conditions)";
        return $"{from} -> {to} | exitTime={t.hasExitTime} dur={t.duration} | {cond}";
    }
}
