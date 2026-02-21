using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroWindupToIdleByHandMove
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";
    private const string HandMoveParam = "Hand_L_Move";

    [MenuItem("Tools/Akiro/Windup -> Idle by Hand_L_Move (<0.02)")]
    public static void Apply()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        int updated = 0;
        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Akiro_Idle")
                       ?? sm.states.Select(s => s.state).FirstOrDefault(s => s.name.EndsWith("_Idle"));
            if (idle == null) continue;

            var windups = sm.states.Select(s => s.state).Where(s => s.name.EndsWith("_Windup")).ToArray();
            foreach (var windup in windups)
            {
                // Find or create transition from windup to idle
                var t = windup.transitions.FirstOrDefault(tr => tr.destinationState == idle);
                if (t == null)
                    t = windup.AddTransition(idle);

                t.hasExitTime = false;
                t.hasFixedDuration = true;
                t.exitTime = 0f;
                t.duration = 0f;
                t.offset = 0f;

                t.conditions = new AnimatorCondition[0];
                t.AddCondition(AnimatorConditionMode.Less, 0.02f, HandMoveParam);
                updated++;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Updated Windup->Idle transitions: {updated}");
    }
}
