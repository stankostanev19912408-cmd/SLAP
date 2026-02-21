using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SlapSetupAkiro
{
    private const string AkiroAssetPath = "Assets/Animation/AKIROrightslap.fbx";
    private const string ControllerPath = "Assets/Animation/Akiro.controller";
    private const string MenuPath = "Tools/Slap/Setup AKIRO";
    private const string ListMenuPath = "Tools/Slap/List AKIRO Clips";
    private const string AssignMenuPath = "Tools/Slap/Assign AKIRO Clips";
    private const string SideMenuPath = "Tools/Slap/Add AKIRO Side";

    [MenuItem(MenuPath)]
    private static void SetupAkiro()
    {
        var akiroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AkiroAssetPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (akiroPrefab == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", $"Не найдено: {AkiroAssetPath}", "OK");
            return;
        }
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", $"Не найдено: {ControllerPath}", "OK");
            return;
        }

        var target = ResolveTarget();
        if (target == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", "Не найден объект с SlapMechanics. Выдели его и повтори.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup AKIRO");

        DisableSkinnedRenderers(target);

        var akiroInstance = FindExistingAkiro(target) ?? (GameObject)PrefabUtility.InstantiatePrefab(akiroPrefab, target.transform);
        if (akiroInstance == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", "Не удалось создать AKIROrightslap.", "OK");
            return;
        }

        akiroInstance.name = akiroPrefab.name;
        akiroInstance.transform.localPosition = Vector3.zero;
        akiroInstance.transform.localRotation = Quaternion.identity;
        akiroInstance.transform.localScale = Vector3.one;

        var animator = ResolveAnimator(target, akiroInstance);
        if (animator != null)
        {
            animator.runtimeAnimatorController = controller;
            var avatar = FindAvatar(AkiroAssetPath);
            if (avatar != null)
            {
                animator.avatar = avatar;
            }
            AssignAnimatorToSlap(target, animator);
        }

        TryAssignAkiroClips(controller);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SlapSetup] AKIRO setup complete.");
    }

    [MenuItem(ListMenuPath)]
    private static void ListAkiroClips()
    {
        var clips = GetAkiroClips();
        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog("Slap Setup", "В AKIROrightslap.fbx не найдено AnimationClip.", "OK");
            return;
        }

        Debug.Log("[SlapSetup] AKIRO clips:\n- " + string.Join("\n- ", clips.Select(c => c.name)));
    }

    [MenuItem(AssignMenuPath)]
    private static void AssignAkiroClipsOnly()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", $"Не найдено: {ControllerPath}", "OK");
            return;
        }

        TryAssignAkiroClips(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SlapSetup] Reassigned AKIRO clips.");
    }

    [MenuItem(SideMenuPath)]
    private static void AddAkiroSide()
    {
        var akiroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AkiroAssetPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (akiroPrefab == null || controller == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", "Не найден AKIROrightslap.fbx или Akiro.controller.", "OK");
            return;
        }

        var primary = ResolveTarget();
        if (primary == null)
        {
            EditorUtility.DisplayDialog("Slap Setup", "Не найден объект с SlapMechanics. Выдели его и повтори.", "OK");
            return;
        }

        var root = new GameObject("AKIRO_Side");
        Undo.RegisterCreatedObjectUndo(root, "Create AKIRO Side");
        root.transform.position = primary.transform.position + new Vector3(2f, 0f, 0f);
        root.transform.rotation = primary.transform.rotation;
        root.transform.localScale = primary.transform.localScale;

        var akiroInstance = (GameObject)PrefabUtility.InstantiatePrefab(akiroPrefab, root.transform);
        akiroInstance.name = akiroPrefab.name;
        akiroInstance.transform.localPosition = Vector3.zero;
        akiroInstance.transform.localRotation = Quaternion.identity;
        akiroInstance.transform.localScale = Vector3.one;

        var animator = ResolveAnimator(root, akiroInstance);
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;

        var avatar = FindAvatar(AkiroAssetPath);
        if (avatar != null) animator.avatar = avatar;

        var newSlap = root.AddComponent<SlapMechanics>();
        CopySlapSettings(primary, newSlap);
        AssignAnimatorToSlap(root, animator);

        TryAssignAkiroClips(controller);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SlapSetup] Added AKIRO side character.");
    }

    private static GameObject ResolveTarget()
    {
        var selected = Selection.activeGameObject;
        if (selected != null)
        {
            var slap = selected.GetComponentInParent<SlapMechanics>();
            if (slap != null) return slap.gameObject;
        }

        var slapInScene = Object.FindObjectsOfType<SlapMechanics>(true).FirstOrDefault();
        return slapInScene != null ? slapInScene.gameObject : null;
    }

    private static void DisableSkinnedRenderers(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in renderers)
        {
            Undo.RecordObject(r, "Disable SkinnedMeshRenderer");
            r.enabled = false;
        }
    }

    private static GameObject FindExistingAkiro(GameObject root)
    {
        foreach (Transform child in root.transform)
        {
            if (child.name == "AKIROrightslap")
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private static Animator ResolveAnimator(GameObject target, GameObject akiroInstance)
    {
        var animator = target.GetComponent<Animator>() ?? target.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = akiroInstance.GetComponent<Animator>() ?? akiroInstance.GetComponentInChildren<Animator>(true);
        }
        return animator;
    }

    private static void AssignAnimatorToSlap(GameObject target, Animator animator)
    {
        var slap = target.GetComponent<SlapMechanics>();
        if (slap == null) return;
        var so = new SerializedObject(slap);
        var prop = so.FindProperty("animator");
        if (prop != null)
        {
            prop.objectReferenceValue = animator;
            so.ApplyModifiedProperties();
        }
    }

    private static void CopySlapSettings(GameObject source, SlapMechanics dest)
    {
        var src = source.GetComponent<SlapMechanics>();
        if (src == null) return;
        EditorUtility.CopySerialized(src, dest);
    }

    private static Avatar FindAvatar(string assetPath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        return all.OfType<Avatar>().FirstOrDefault();
    }

    private static void TryAssignAkiroClips(AnimatorController controller)
    {
        var clips = GetAkiroClips();
        if (clips.Length == 0)
        {
            Debug.LogWarning("[SlapSetup] В AKIROrightslap.fbx нет AnimationClip. Проверь вкладку Animation и раздел Clips.");
            return;
        }

        // Пользователь указал, что основная анимация называется "Armature Scene".
        var main = PickClipExact(clips, "Armature Scene");
        var idle = PickClipExact(clips, "Idle") ?? PickClipExact(clips, "AKIRO_Idle") ?? PickClip(clips, "idle") ?? main;
        var windup = PickClipExact(clips, "Windup") ?? PickClipExact(clips, "AKIRO_Windup") ?? PickClip(clips, "up", "windup") ?? main;
        var slap = PickClipExact(clips, "Slap") ?? PickClipExact(clips, "AKIRO_Slap") ?? PickClip(clips, "down", "slap") ?? main;

        AssignClipToState(controller, "Akiro_Idle", idle);
        AssignClipToState(controller, "Akiro_Hand_L_Up_Move", windup);
        AssignClipToState(controller, "Akiro_Hand_L_Down_Move", slap);

        Debug.Log($"[SlapSetup] Assigned clips: Idle={idle?.name ?? "none"}, Windup={windup?.name ?? "none"}, Slap={slap?.name ?? "none"}");
    }

    private static AnimationClip[] GetAkiroClips()
    {
        return AssetDatabase.LoadAllAssetsAtPath(AkiroAssetPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", System.StringComparison.Ordinal))
            .ToArray();
    }

    private static AnimationClip PickClipExact(AnimationClip[] clips, string name)
    {
        return clips.FirstOrDefault(c => c.name == name);
    }

    private static AnimationClip PickClip(AnimationClip[] clips, params string[] keywords)
    {
        foreach (var clip in clips)
        {
            var name = clip.name.ToLowerInvariant();
            if (keywords.Any(k => name.Contains(k)))
            {
                return clip;
            }
        }
        return null;
    }

    private static void AssignClipToState(AnimatorController controller, string stateName, AnimationClip clip)
    {
        if (clip == null) return;
        foreach (var layer in controller.layers)
        {
            var state = layer.stateMachine.states.FirstOrDefault(s => s.state.name == stateName).state;
            if (state != null)
            {
                state.motion = clip;
                EditorUtility.SetDirty(controller);
                return;
            }
        }
    }
}

