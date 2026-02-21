using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AkiroRootBakeBatch
{
    private const string SlapFolder = "Assets/Animation/slap";
    private const string BlockFolder = "Assets/Animation/block";
    private const string AnimationRoot = "Assets/Animation";

    [MenuItem("Tools/Akiro/Fix Vertical Drift (Use Original Y)")]
    public static void FixVerticalDrift()
    {
        var folders = new HashSet<string>();
        if (AssetDatabase.IsValidFolder(SlapFolder)) folders.Add(SlapFolder);
        if (AssetDatabase.IsValidFolder(BlockFolder)) folders.Add(BlockFolder);

        // Fallback: if folders differ in this project, process all FBX under Assets/Animation
        if (AssetDatabase.IsValidFolder(AnimationRoot))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx")) continue;
                var dir = Path.GetDirectoryName(path)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(dir)) folders.Add(dir);
            }
        }

        int changed = 0;
        foreach (var folder in folders)
            changed += ProcessFolder(folder);

        AssetDatabase.SaveAssets();
        Debug.Log($"Vertical drift fix complete. Updated {changed} FBX files across {folders.Count} folders.");
    }

    private static int ProcessFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.Log("Skip missing folder: " + folder);
            return 0;
        }

        int count = 0;
        var fbxFiles = Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories);
        foreach (var f in fbxFiles)
        {
            var path = f.Replace("\\", "/");
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            bool changed = false;
            var clips = importer.clipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    // Match old working project: keep original Y to prevent sinking.
                    if (!c.keepOriginalPositionY || c.keepOriginalPositionXZ || c.keepOriginalOrientation)
                    {
                        c.keepOriginalPositionY = true;
                        c.keepOriginalPositionXZ = false;
                        c.keepOriginalOrientation = false;
                        clips[i] = c;
                        changed = true;
                    }
                }
                importer.clipAnimations = clips;
            }
            else
            {
                // Force creation of clipAnimations from default, then edit
                clips = importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = false;
                    c.keepOriginalOrientation = false;
                    clips[i] = c;
                }
                importer.clipAnimations = clips;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                count++;
            }
        }

        return count;
    }
}
