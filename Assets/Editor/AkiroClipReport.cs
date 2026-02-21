using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AkiroClipReport
{
    [MenuItem("Tools/Akiro/Report AnimationClips")]
    public static void Report()
    {
        var guids = AssetDatabase.FindAssets("t:AnimationClip");
        var names = guids.Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
                         .Where(c => c != null)
                         .Select(c => c.name)
                         .Distinct()
                         .OrderBy(n => n)
                         .ToArray();
        Debug.Log("Clips: " + string.Join(", ", names));
    }
}
