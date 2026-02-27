using UnityEngine;

public static class SlapCombatManagerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureCombatManagerPresent()
    {
        var existing = Object.FindObjectsOfType<SlapCombatManager>(true);
        if (existing != null && existing.Length > 0) return;

        var root = new GameObject("SlapCombatManager_Auto");
        root.AddComponent<SlapCombatManager>();
    }
}
