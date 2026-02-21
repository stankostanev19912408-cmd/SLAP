using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AkiroAnimatorAutoSetup
{
    private const string ControllerPath = "Assets/Animation/Akiro.controller";
    private const string ActionIdParam = "ActionID";

    private const int WindupBase = 100;
    private const int SlapBase = 200;
    private const int ReturnBase = 300;
    private const int BlockBase = 400;

    private struct SlapDef
    {
        public int Id;
        public string Windup;
        public string Slap;
        public string Return;
    }

    private struct BlockDef
    {
        public int Id;
        public string Block;
        public string Return;
    }

    [MenuItem("Tools/Akiro/Auto Setup Transitions")]
    public static void AutoSetup()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Akiro.controller not found at: " + ControllerPath);
            return;
        }

        EnsureIntParameter(controller, ActionIdParam);

        var layer = controller.layers[0];
        var sm = layer.stateMachine;

        var slaps = new SlapDef[]
        {
            new SlapDef { Id = 1, Windup = "Slap1_Windup", Slap = "Slap1_Slap", Return = "Slap1_Return" },
            new SlapDef { Id = 2, Windup = "Slap3_Windup", Slap = "Slap3_Slap", Return = "Slap3_Return" },
            new SlapDef { Id = 3, Windup = "Slap7_Windup", Slap = "Slap7_Slap", Return = "Slap7_Return" },
            new SlapDef { Id = 4, Windup = "Slap9_Windup", Slap = "Slap9_Slap", Return = "Slap9_Return" },
            new SlapDef { Id = 5, Windup = "SlapLeft_Windup", Slap = "SlapLeft_Slap", Return = "SlapLeft_Return" },
            new SlapDef { Id = 6, Windup = "SlapRight_Windup", Slap = "SlapRight_Slap", Return = "SlapRight_Return" },
            new SlapDef { Id = 7, Windup = "SlapUp_Windup", Slap = "SlapUp_Slap", Return = "SlapUp_Return" },
            new SlapDef { Id = 8, Windup = "SlaperCut_Windup", Slap = "SlaperCut_Slap", Return = "SlaperCut_Return" }
        };

        var blocks = new BlockDef[]
        {
            new BlockDef { Id = 1, Block = "Block1_Block", Return = "Block1_Return" },
            new BlockDef { Id = 2, Block = "Block3_Block", Return = "Block3_Return" },
            new BlockDef { Id = 3, Block = "Block7_Block", Return = "Block7_Return" },
            new BlockDef { Id = 4, Block = "Block9_Block", Return = "Block9_Return" },
            new BlockDef { Id = 5, Block = "BlockLeft_Block", Return = "BlockLeft_Return" },
            new BlockDef { Id = 6, Block = "BlockRight_Block", Return = "BlockRight_Return" },
            new BlockDef { Id = 7, Block = "BlockUp_Block", Return = "BlockUp_Return" },
            new BlockDef { Id = 8, Block = "BlockerCut_Block", Return = "BlockerCut_Return" }
        };

        var idleState = FindOrCreateState(sm, "Akiro_Idle");

        // Clear Any State transitions we will manage.
        ClearAnyStateTransitions(sm, slaps.Select(s => s.Windup));
        ClearAnyStateTransitions(sm, blocks.Select(b => b.Block));

        foreach (var slap in slaps)
        {
            var windupState = FindOrCreateState(sm, slap.Windup);
            var slapState = FindOrCreateState(sm, slap.Slap);
            var returnState = FindOrCreateState(sm, slap.Return);

            ClearTransitions(windupState);
            ClearTransitions(slapState);
            ClearTransitions(returnState);

            // Any State -> Windup
            var toWindup = sm.AddAnyStateTransition(windupState);
            ConfigureTransition(toWindup, false, 0f);
            AddIntCondition(toWindup, ActionIdParam, WindupBase + slap.Id);

            // Windup -> Slap (on second swipe)
            var windupToSlap = windupState.AddTransition(slapState);
            ConfigureTransition(windupToSlap, false, 0f);
            AddIntCondition(windupToSlap, ActionIdParam, SlapBase + slap.Id);

            // Windup -> Return (timeout)
            var windupToReturn = windupState.AddTransition(returnState);
            ConfigureTransition(windupToReturn, false, 0f);
            AddIntCondition(windupToReturn, ActionIdParam, ReturnBase + slap.Id);

            // Slap -> Return (exit time)
            var slapToReturn = slapState.AddTransition(returnState);
            ConfigureTransition(slapToReturn, true, 0f);

            // Return -> Idle (exit time)
            var returnToIdle = returnState.AddTransition(idleState);
            ConfigureTransition(returnToIdle, true, 0f);

            EnsureResetBehaviour(returnState);
        }

        foreach (var block in blocks)
        {
            var blockState = FindOrCreateState(sm, block.Block);
            var returnState = FindOrCreateState(sm, block.Return);

            ClearTransitions(blockState);
            ClearTransitions(returnState);

            // Any State -> Block
            var toBlock = sm.AddAnyStateTransition(blockState);
            ConfigureTransition(toBlock, false, 0f);
            AddIntCondition(toBlock, ActionIdParam, BlockBase + block.Id);

            // Block -> Return (exit time)
            var blockToReturn = blockState.AddTransition(returnState);
            ConfigureTransition(blockToReturn, true, 0f);

            // Return -> Idle (exit time)
            var returnToIdle = returnState.AddTransition(idleState);
            ConfigureTransition(returnToIdle, true, 0f);

            EnsureResetBehaviour(returnState);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Akiro transitions auto-setup complete.");
    }

    private static void EnsureIntParameter(AnimatorController controller, string name)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(name, AnimatorControllerParameterType.Int);
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name)
    {
        var state = sm.states.FirstOrDefault(s => s.state.name == name).state;
        if (state != null) return state;
        return sm.AddState(name);
    }

    private static void ClearTransitions(AnimatorState state)
    {
        if (state == null) return;
        foreach (var t in state.transitions.ToArray())
        {
            state.RemoveTransition(t);
        }
    }

    private static void ClearAnyStateTransitions(AnimatorStateMachine sm, System.Collections.Generic.IEnumerable<string> targetNames)
    {
        var targets = targetNames.ToHashSet();
        foreach (var t in sm.anyStateTransitions.ToArray())
        {
            if (t.destinationState != null && targets.Contains(t.destinationState.name))
            {
                sm.RemoveAnyStateTransition(t);
            }
        }
    }

    private static void ConfigureTransition(AnimatorStateTransition t, bool hasExitTime, float duration)
    {
        t.hasExitTime = hasExitTime;
        t.hasFixedDuration = true;
        t.exitTime = hasExitTime ? 1f : 0f;
        t.duration = duration;
        t.offset = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        t.orderedInterruption = true;
        t.canTransitionToSelf = false;
    }

    private static void AddIntCondition(AnimatorStateTransition t, string param, int value)
    {
        t.AddCondition(AnimatorConditionMode.Equals, value, param);
    }

    private static void EnsureResetBehaviour(AnimatorState state)
    {
        var exists = state.behaviours.Any(b => b is ResetActionIdOnExit);
        if (exists) return;
        state.AddStateMachineBehaviour<ResetActionIdOnExit>();
    }
}
