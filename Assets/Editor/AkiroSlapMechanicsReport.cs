using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AkiroSlapMechanicsReport
{
    [MenuItem("Tools/Akiro/Report SlapMechanics")]
    public static void Report()
    {
        int total = 0;
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                var comps = root.GetComponentsInChildren<SlapMechanics>(true);
                foreach (var c in comps)
                {
                    total++;
                    var animator = c.GetComponentInChildren<Animator>(true);
                    Debug.Log($"SlapMechanics on '{c.gameObject.name}' | role={GetRole(c)} | animator={(animator != null ? animator.name : "null")}");
                }
            }
        }
        Debug.Log($"SlapMechanics total: {total}");
    }

    private static string GetRole(SlapMechanics c)
    {
        var field = typeof(SlapMechanics).GetField("role", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? field.GetValue(c).ToString() : "unknown";
    }
}
