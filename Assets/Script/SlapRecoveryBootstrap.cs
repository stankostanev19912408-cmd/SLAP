using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SlapRecoveryBootstrap
{
    private const string PlayerName = "idle";
    private const string OpponentName = "idle (1)";
    private const string DefaultControllerPath = "Assets/Animation/Akiro.controller";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureCombatSetup();
    }

    private static void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        EnsureCombatSetup();
    }

    private static void EnsureCombatSetup()
    {
        var fighters = FindFighters();
        var player = EnsureFighter(fighters.player);
        var opponent = EnsureFighter(fighters.opponent);
        if (player == null || opponent == null)
        {
            return;
        }

        // Restores the combat runtime singleton if scene lost it.
        SlapCombatManager.EnsureExists();
    }

    private static SlapMechanics EnsureFighter(GameObject fighter)
    {
        if (fighter == null)
        {
            return null;
        }

        var animator = ResolveAnimator(fighter);
        if (animator == null)
        {
            return null;
        }

        if (!EnsureAnimatorController(animator))
        {
            return null;
        }

        var mechanics = fighter.GetComponent<SlapMechanics>();
        if (mechanics == null)
        {
            mechanics = fighter.AddComponent<SlapMechanics>();
        }

        return mechanics;
    }

    private static Animator ResolveAnimator(GameObject fighter)
    {
        if (fighter == null) return null;

        var animator = fighter.GetComponent<Animator>();
        if (animator != null) return animator;

        return fighter.GetComponentInChildren<Animator>(true);
    }

    private static bool EnsureAnimatorController(Animator animator)
    {
        if (animator == null) return false;
        if (animator.runtimeAnimatorController != null) return true;

        RuntimeAnimatorController fallback = FindAnyExistingController();
        if (fallback == null)
        {
            fallback = FindLoadedAkiroController();
        }
#if UNITY_EDITOR
        if (fallback == null)
        {
            fallback = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DefaultControllerPath);
        }
#endif
        if (fallback == null)
        {
            return false;
        }

        animator.runtimeAnimatorController = fallback;
        return animator.runtimeAnimatorController != null;
    }

    private static RuntimeAnimatorController FindAnyExistingController()
    {
        var animators = Object.FindObjectsOfType<Animator>(true);
        foreach (var candidate in animators)
        {
            if (candidate == null || candidate.runtimeAnimatorController == null) continue;
            return candidate.runtimeAnimatorController;
        }
        return null;
    }

    private static RuntimeAnimatorController FindLoadedAkiroController()
    {
        var controllers = Resources.FindObjectsOfTypeAll<RuntimeAnimatorController>();
        foreach (var controller in controllers)
        {
            if (controller == null) continue;
            string name = controller.name;
            if (!string.IsNullOrEmpty(name) &&
                name.ToLowerInvariant().Contains("akiro"))
            {
                return controller;
            }
        }
        return null;
    }

    private static (GameObject player, GameObject opponent) FindFighters()
    {
        GameObject player = GameObject.Find(PlayerName);
        GameObject opponent = GameObject.Find(OpponentName);
        if (player != null && opponent != null && player != opponent)
        {
            return (player, opponent);
        }

        var candidates = new List<GameObject>();
        var animators = Object.FindObjectsOfType<Animator>(true);
        foreach (var animator in animators)
        {
            if (animator == null || animator.gameObject == null) continue;
            if (animator.gameObject.GetComponentInParent<Animator>() != animator) continue;
            candidates.Add(animator.gameObject);
        }

        if (player == null)
        {
            player = candidates.Count > 0 ? candidates[0] : null;
        }
        if (opponent == null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != player)
                {
                    opponent = candidates[i];
                    break;
                }
            }
        }

        return (player, opponent);
    }
}
