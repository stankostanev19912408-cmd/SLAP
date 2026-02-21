using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroBuildOldScheme
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";
    private const string ActionIdParam = "ActionID";

    [MenuItem("Tools/Akiro/Build Old Scheme (No Return)")]
    public static void BuildScheme()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        int addedTransitions = 0;
        int removedTransitions = 0;
        int createdStates = 0;

        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;

            // Build ActionID map from existing transitions (Any State + state transitions)
            var actionIdByDest = new Dictionary<string, int>();
            foreach (var t in sm.anyStateTransitions)
                TryCaptureActionId(t, actionIdByDest);
            foreach (var st in sm.states)
                foreach (var t in st.state.transitions)
                    TryCaptureActionId(t, actionIdByDest);

            // Remove Any State transitions that target known slap/block states
            var anyToRemove = sm.anyStateTransitions
                .Where(t => t.destinationState != null && IsSlapOrBlockState(t.destinationState.name))
                .ToArray();
            foreach (var t in anyToRemove)
            {
                sm.RemoveAnyStateTransition(t);
                removedTransitions++;
            }

            var idle = FindState(sm, "Akiro_Idle") ?? sm.states.Select(s => s.state).FirstOrDefault(s => s.name.EndsWith("_Idle"));
            if (idle == null)
            {
                Debug.LogWarning("Idle state not found. Skipping layer.");
                continue;
            }

            // Handle slaps: Idle -> Windup -> Slap -> Idle, plus Idle <-> Windup
            var windups = sm.states.Select(s => s.state).Where(s => s.name.EndsWith("_Windup")).ToArray();
            foreach (var windup in windups)
            {
                var prefix = windup.name.Substring(0, windup.name.Length - "_Windup".Length);
                var slapName = prefix + "_Slap";
                var slap = FindState(sm, slapName);
                if (slap == null)
                    continue;

                removedTransitions += RemoveTransitionsTo(sm, windup, slap, idle);

                // Idle -> Windup (ActionID == windup)
                addedTransitions += AddActionTransition(idle, windup, actionIdByDest, windup.name);

                // Windup -> Idle (ActionID == ReturnBase + id)
                var id = GetIdFromName(prefix);
                if (id > 0)
                    addedTransitions += AddActionTransitionWithId(windup, idle, ReturnBase + id);

                // Windup -> Slap (ActionID == slap)
                addedTransitions += AddActionTransition(windup, slap, actionIdByDest, slap.name);

                // Slap -> Idle (exit time)
                addedTransitions += AddExitTimeTransition(slap, idle);
            }

            // Handle blocks: Idle -> Block -> Idle, plus Block -> Idle on Return ActionID if present
            var blocks = sm.states.Select(s => s.state).Where(s => s.name.StartsWith("Block") && s.name.EndsWith("_Block")).ToArray();
            foreach (var block in blocks)
            {
                var prefix = block.name.Substring(0, block.name.Length - "_Block".Length);
                removedTransitions += RemoveTransitionsTo(sm, block, idle);

                // Idle -> Block (ActionID == block)
                addedTransitions += AddActionTransition(idle, block, actionIdByDest, block.name);

                // Block -> Idle (exit time)
                addedTransitions += AddExitTimeTransition(block, idle);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Old scheme built (no Return). Added transitions: {addedTransitions}, removed transitions: {removedTransitions}, created states: {createdStates}");
    }

    [MenuItem("Tools/Akiro/Force Add Back Arrows (Windup/Block -> Idle)")]
    public static void ForceAddBackArrows()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        int added = 0;
        int removed = 0;

        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;
            var idle = FindState(sm, "Akiro_Idle") ?? sm.states.Select(s => s.state).FirstOrDefault(s => s.name.EndsWith("_Idle"));
            if (idle == null)
            {
                Debug.LogWarning("Idle state not found. Skipping layer.");
                continue;
            }

            // Windup -> Idle with ActionID ReturnBase + id
            var windups = sm.states.Select(s => s.state).Where(s => s.name.EndsWith("_Windup")).ToArray();
            foreach (var windup in windups)
            {
                var prefix = windup.name.Substring(0, windup.name.Length - "_Windup".Length);
                removed += RemoveTransitionsFromTo(windup, idle);
                var id = GetIdFromName(prefix);
                if (id > 0)
                    added += AddActionTransitionWithId(windup, idle, ReturnBase + id);
            }

            // Block -> Idle with Exit Time
            var blocks = sm.states.Select(s => s.state).Where(s => s.name.StartsWith("Block") && s.name.EndsWith("_Block")).ToArray();
            foreach (var block in blocks)
            {
                removed += RemoveTransitionsFromTo(block, idle);
                added += AddExitTimeTransition(block, idle);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Forced back arrows. Added: {added}, removed: {removed}");
    }

    [MenuItem("Tools/Akiro/Force Slap -> Idle (Exit Time)")]
    public static void ForceSlapToIdle()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"AnimatorController not found at {ControllerPath}");
            return;
        }

        int added = 0;
        int removed = 0;

        foreach (var layer in controller.layers)
        {
            var sm = layer.stateMachine;
            var idle = FindState(sm, "Akiro_Idle") ?? sm.states.Select(s => s.state).FirstOrDefault(s => s.name.EndsWith("_Idle"));
            if (idle == null)
            {
                Debug.LogWarning("Idle state not found. Skipping layer.");
                continue;
            }

            var slaps = sm.states.Select(s => s.state).Where(s => s.name.EndsWith("_Slap")).ToArray();
            foreach (var slap in slaps)
            {
                removed += RemoveTransitionsFromTo(slap, idle);
                added += AddExitTimeTransition(slap, idle);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"Forced Slap->Idle. Added: {added}, removed: {removed}");
    }

    private static bool IsSlapOrBlockState(string name)
    {
        return name.StartsWith("Slap") || name.StartsWith("Block");
    }

    private const int ReturnBase = 300;

    private static int GetIdFromName(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("slap1") || n.Contains("block1")) return 1;
        if (n.Contains("slap3") || n.Contains("block3")) return 2;
        if (n.Contains("slap7") || n.Contains("block7")) return 3;
        if (n.Contains("slap9") || n.Contains("block9")) return 4;
        if (n.Contains("slapleft") || n.Contains("blockleft")) return 5;
        if (n.Contains("slapright") || n.Contains("blockright")) return 6;
        if (n.Contains("slapup") || n.Contains("blockup")) return 7;
        if (n.Contains("slapercut") || n.Contains("slaper") || n.Contains("blockercut") || n.Contains("blocker")) return 8;
        return 0;
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        return sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, ref int created)
    {
        var state = FindState(sm, name);
        if (state != null) return state;
        state = sm.AddState(name);
        created++;
        return state;
    }

    private static int RemoveTransitionsTo(AnimatorStateMachine sm, params AnimatorState[] targets)
    {
        int removed = 0;
        var targetSet = new HashSet<AnimatorState>(targets.Where(t => t != null));

        foreach (var child in sm.states)
        {
            var state = child.state;
            var toRemove = state.transitions.Where(t => t.destinationState != null && targetSet.Contains(t.destinationState)).ToArray();
            foreach (var t in toRemove)
            {
                state.RemoveTransition(t);
                removed++;
            }
        }
        return removed;
    }

    private static int RemoveTransitionsFromTo(AnimatorState from, AnimatorState to)
    {
        if (from == null || to == null) return 0;
        int removed = 0;
        var toRemove = from.transitions.Where(t => t.destinationState == to).ToArray();
        foreach (var t in toRemove)
        {
            from.RemoveTransition(t);
            removed++;
        }
        return removed;
    }

    private static void TryCaptureActionId(AnimatorStateTransition t, Dictionary<string, int> map)
    {
        if (t.destinationState == null) return;
        foreach (var c in t.conditions)
        {
            if (c.parameter == ActionIdParam && c.mode == AnimatorConditionMode.Equals)
            {
                map[t.destinationState.name] = (int)c.threshold;
                break;
            }
        }
    }

    private static int AddActionTransition(AnimatorState from, AnimatorState to, Dictionary<string, int> actionIdByDest, string destName)
    {
        if (from == null || to == null) return 0;
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.exitTime = 0f;
        t.duration = 0f;
        t.offset = 0f;
        if (!actionIdByDest.TryGetValue(destName, out var id))
            id = 0;
        if (id > 0)
            t.AddCondition(AnimatorConditionMode.Equals, id, ActionIdParam);
        return 1;
    }

    private static int AddActionTransitionWithId(AnimatorState from, AnimatorState to, int id)
    {
        if (from == null || to == null) return 0;
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.exitTime = 0f;
        t.duration = 0f;
        t.offset = 0f;
        if (id > 0)
            t.AddCondition(AnimatorConditionMode.Equals, id, ActionIdParam);
        return 1;
    }

    private static int AddExitTimeTransition(AnimatorState from, AnimatorState to)
    {
        if (from == null || to == null) return 0;
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.hasFixedDuration = true;
        t.exitTime = 1f;
        t.duration = 0f;
        t.offset = 0f;
        return 1;
    }
}
