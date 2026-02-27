using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SlapSceneAutoFix
{
    private const string DefaultControllerPath = "Assets/Animation/Akiro.controller";
    private const string DefaultAvatarPath = "Assets/Animation/idle.fbx";
    private const string AttackerIntroClipPath = "Assets/Animation/Animations+/AKIROmodel@Arm Stretching.fbx";
    private const string DefenderIntroClipPath = "Assets/Animation/Animations+/AKIROmodel@Neck Stretching.fbx";
    private const string MissedRightReactionClipPath = "Assets/Animation/Animations+/AKIROmodel@Falling Forward Death.fbx";
    private const string MissedLeftReactionClipPath = "Assets/Animation/Animations+/AKIROmodel@Falling Forward Death (1).fbx";
    private const string MissedUpReactionClipPath = "Assets/Animation/Animations+/AKIROmodel@Dying 1.fbx";
    private const string MissedDownReactionClipPath = "Assets/Animation/Animations+/AKIROmodel@Dyingslapercut.fbx";
    private const string PlayerName = "idle";
    private const string OpponentName = "idle (1)";

    [MenuItem("Tools/Slap/Repair Build Scene Combat Setup")]
    public static void RepairBuildSceneCombatSetupMenu()
    {
        RepairBuildSceneCombatSetup();
    }

    public static void RepairBuildSceneCombatSetup()
    {
        string scenePath = GetFirstEnabledScenePath();
        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogError("[SlapAutoFix] No enabled scene in Build Settings.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DefaultControllerPath);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(DefaultAvatarPath).OfType<Avatar>().FirstOrDefault();

        bool okPlayer = EnsureFighter(PlayerName, false, controller, avatar);
        bool okOpponent = EnsureFighter(OpponentName, true, controller, avatar);
        EnsureCombatManager();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[SlapAutoFix] Scene repaired: '{scene.path}'. playerOk={okPlayer}, opponentOk={okOpponent}, controller={(controller != null ? controller.name : "<null>")}");
    }

    private static string GetFirstEnabledScenePath()
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes == null) return null;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].enabled) return scenes[i].path;
        }
        return null;
    }

    private static bool EnsureFighter(string exactName, bool ensureAIController, RuntimeAnimatorController controller, Avatar avatar)
    {
        var fighter = FindByExactName(exactName);
        if (fighter == null)
        {
            Debug.LogError($"[SlapAutoFix] Fighter '{exactName}' not found.");
            return false;
        }

        var animator = fighter.GetComponent<Animator>();
        if (animator == null)
        {
            animator = fighter.GetComponentInChildren<Animator>(true);
        }
        if (animator == null)
        {
            Debug.LogError($"[SlapAutoFix] Animator not found for '{exactName}'.");
            return false;
        }

        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }
        if (avatar != null)
        {
            animator.avatar = avatar;
        }

        var slap = fighter.GetComponent<SlapMechanics>();
        if (slap == null) slap = fighter.AddComponent<SlapMechanics>();

        var so = new SerializedObject(slap);
        var animatorProp = so.FindProperty("animator");
        if (animatorProp != null)
        {
            animatorProp.objectReferenceValue = animator;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        if (fighter.GetComponent<CombatantStats>() == null)
        {
            fighter.AddComponent<CombatantStats>();
        }
        if (ensureAIController && fighter.GetComponent<SlapAIController>() == null)
        {
            fighter.AddComponent<SlapAIController>();
        }

        return true;
    }

    private static void EnsureCombatManager()
    {
        var all = Object.FindObjectsOfType<SlapCombatManager>(true);
        SlapCombatManager manager = null;
        if (all != null && all.Length > 0)
        {
            manager = all[0];
            for (int i = 1; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                Object.DestroyImmediate(all[i].gameObject);
            }
        }
        if (manager == null)
        {
            var go = new GameObject("SlapCombatManager");
            manager = go.AddComponent<SlapCombatManager>();
        }

        var so = new SerializedObject(manager);
        SetString(so, "playerName", PlayerName);
        SetString(so, "aiName", OpponentName);
        SetBool(so, "requireTapToStart", true);
        SetBool(so, "autoStartBattleInDebug", false);
        SetClip(so, "firstAttackerIntroClip", LoadFirstClipAtPath(AttackerIntroClipPath));
        SetClip(so, "firstDefenderIntroClip", LoadFirstClipAtPath(DefenderIntroClipPath));
        SetClip(so, "missedRightBlockReactionClip", LoadFirstClipAtPath(MissedRightReactionClipPath));
        SetClip(so, "missedLeftBlockReactionClip", LoadFirstClipAtPath(MissedLeftReactionClipPath));
        SetClip(so, "missedSlapUpBlockReactionClip", LoadFirstClipAtPath(MissedUpReactionClipPath));
        SetClip(so, "missedSlapErcutBlockReactionClip", LoadFirstClipAtPath(MissedDownReactionClipPath));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(SerializedObject so, string field, string value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.stringValue = value;
    }

    private static void SetBool(SerializedObject so, string field, bool value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.boolValue = value;
    }

    private static void SetClip(SerializedObject so, string field, AnimationClip clip)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = clip;
    }

    private static AnimationClip LoadFirstClipAtPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all == null) return null;
        for (int i = 0; i < all.Length; i++)
        {
            var clip = all[i] as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
            return clip;
        }
        return null;
    }

    private static GameObject FindByExactName(string exactName)
    {
        var all = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (string.Equals(t.name, exactName, System.StringComparison.Ordinal))
            {
                return t.gameObject;
            }
        }
        return null;
    }
}
