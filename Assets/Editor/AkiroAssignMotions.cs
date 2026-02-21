using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroAssignMotions
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";

    [MenuItem("Tools/Akiro/Assign Motions By State Name")]
    public static void Assign()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Akiro.controller not found at: " + ControllerPath);
            return;
        }

        var sm = controller.layers[0].stateMachine;
        int assigned = 0;
        var missing = new List<string>();

        foreach (var child in sm.states)
        {
            var state = child.state;
            var clip = FindClipByName(state.name);
            if (clip == null)
            {
                if (state.motion == null)
                {
                    missing.Add(state.name);
                }
                continue;
            }

            if (state.motion != clip)
            {
                state.motion = clip;
                assigned++;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"Assigned motions: {assigned}");
        if (missing.Count > 0)
        {
            Debug.LogWarning("Missing clips for states: " + string.Join(", ", missing));
        }
    }

    private static AnimationClip FindClipByName(string name)
    {
        var guids = AssetDatabase.FindAssets($"t:AnimationClip {name}");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            if (clip.name != name) continue;
            // Skip preview clips
            if (clip.hideFlags != HideFlags.None && clip.hideFlags != HideFlags.NotEditable)
                continue;
            return clip;
        }
        return null;
    }
}
