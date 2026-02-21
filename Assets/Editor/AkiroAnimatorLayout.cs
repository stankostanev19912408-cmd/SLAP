using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroAnimatorLayout
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Arrange States")]
    public static void Arrange()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Akiro.controller not found at: " + ControllerPath);
            return;
        }

        var sm = controller.layers[0].stateMachine;
        var states = sm.states;
        var indexByName = new Dictionary<string, int>();
        for (int i = 0; i < states.Length; i++)
        {
            indexByName[states[i].state.name] = i;
        }

        Place(states, indexByName, "Akiro_Idle", new Vector2(0f, 0f));

        float left = -600f;
        float leftDiag = -900f;
        float right = 600f;
        float rightDiag = 900f;
        float up = 0f;
        float down = 0f;

        float top = -200f;
        float midTop = 150f;
        float low = 500f;

        PlaceColumn(states, indexByName, left, midTop,
            "BlockLeft_Block",
            "BlockLeft_Return",
            "SlapLeft_Windup",
            "SlapLeft_Slap",
            "SlapLeft_Return");

        PlaceColumn(states, indexByName, right, midTop,
            "BlockRight_Block",
            "BlockRight_Return",
            "SlapRight_Windup",
            "SlapRight_Slap",
            "SlapRight_Return");

        PlaceColumn(states, indexByName, up, top,
            "BlockUp_Block",
            "BlockUp_Return",
            "SlapUp_Windup",
            "SlapUp_Slap",
            "SlapUp_Return");

        PlaceColumn(states, indexByName, down, low,
            "BlockerCut_Block",
            "BlockerCut_Return",
            "SlaperCut_Windup",
            "SlaperCut_Slap",
            "SlaperCut_Return");

        PlaceColumn(states, indexByName, leftDiag, midTop,
            "Block1_Block",
            "Block1_Return",
            "Slap1_Windup",
            "Slap1_Slap",
            "Slap1_Return");

        PlaceColumn(states, indexByName, rightDiag, midTop,
            "Block3_Block",
            "Block3_Return",
            "Slap3_Windup",
            "Slap3_Slap",
            "Slap3_Return");

        PlaceColumn(states, indexByName, leftDiag, low,
            "Block7_Block",
            "Block7_Return",
            "Slap7_Windup",
            "Slap7_Slap",
            "Slap7_Return");

        PlaceColumn(states, indexByName, rightDiag, low,
            "Block9_Block",
            "Block9_Return",
            "Slap9_Windup",
            "Slap9_Slap",
            "Slap9_Return");

        sm.states = states;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Animator states arranged.");
    }

    private static void Place(ChildAnimatorState[] states, Dictionary<string, int> indexByName, string name, Vector2 pos)
    {
        if (!indexByName.TryGetValue(name, out var idx)) return;
        var s = states[idx];
        s.position = pos;
        states[idx] = s;
    }

    private static void PlaceColumn(ChildAnimatorState[] states, Dictionary<string, int> indexByName, float x, float startY, params string[] names)
    {
        float y = startY;
        float step = 120f;
        foreach (var n in names)
        {
            Place(states, indexByName, n, new Vector2(x, y));
            y += step;
        }
    }
}
