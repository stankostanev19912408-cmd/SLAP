using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroStateSetup
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";
    private const string WindupTimeParam = "Hand_L_Move";
    private const string SlapSpeedParam = "Slap_Speed";

    [MenuItem("Tools/Akiro/Configure Windup + Slap States")]
    public static void ConfigureStates()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        var clipIndex = BuildClipIndex();
        int configured = 0;
        int missingClip = 0;
        int behavioursRemoved = 0;

        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;
            foreach (var child in sm.states)
            {
                var state = child.state;
                if (state == null) continue;

                if (state.name.EndsWith("_Windup"))
                {
                    if (!AssignMotionByName(state, state.name, clipIndex)) missingClip++;
                    ConfigureWindup(state);
                    behavioursRemoved += RemoveBehaviours(state);
                    configured++;
                }
                else if (state.name.EndsWith("_Slap"))
                {
                    if (!AssignMotionByName(state, state.name, clipIndex)) missingClip++;
                    ConfigureSlap(state);
                    behavioursRemoved += RemoveBehaviours(state);
                    configured++;
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Configured states: {configured}, missing clips: {missingClip}, behaviours removed: {behavioursRemoved}");
    }

    private static void ConfigureWindup(AnimatorState state)
    {
        state.speed = 1f;
        state.speedParameterActive = false;
        state.timeParameterActive = true;
        state.timeParameter = WindupTimeParam;
        state.writeDefaultValues = true;
    }

    private static void ConfigureSlap(AnimatorState state)
    {
        state.speed = 1f;
        state.speedParameterActive = true;
        state.speedParameter = SlapSpeedParam;
        state.timeParameterActive = false;
        state.writeDefaultValues = true;
    }

    private static bool AssignMotionByName(AnimatorState state, string clipName, Dictionary<string, AnimationClip> clipIndex)
    {
        var key = NormalizeName(clipName);
        if (clipIndex.TryGetValue(key, out var clip))
        {
            state.motion = clip;
            return true;
        }

        // fallback: contains match
        var fallback = clipIndex
            .Where(kv => kv.Key.Contains(key) || key.Contains(kv.Key))
            .Select(kv => kv.Value)
            .FirstOrDefault();
        if (fallback != null)
        {
            state.motion = fallback;
            return true;
        }

        Debug.LogWarning($"Clip not found for state {state.name}");
        return false;
    }

    private static int RemoveBehaviours(AnimatorState state)
    {
        int removed = 0;
        var behaviours = state.behaviours;
        if (behaviours == null || behaviours.Length == 0) return 0;
        foreach (var b in behaviours.ToArray())
        {
            if (b == null) continue;
            Object.DestroyImmediate(b, true);
            removed++;
        }
        return removed;
    }

    private static Dictionary<string, AnimationClip> BuildClipIndex()
    {
        var dict = new Dictionary<string, AnimationClip>();
        var guids = AssetDatabase.FindAssets("t:AnimationClip");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            var key = NormalizeName(clip.name);
            if (!dict.ContainsKey(key))
                dict.Add(key, clip);
        }
        return dict;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var chars = name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }
}
