using System.IO;
using UnityEditor;
using UnityEngine;

public static class AkiroRootBakeReport
{
    private const string SlapFolder = "Assets/Animation/slap";
    private const string BlockFolder = "Assets/Animation/block";

    [MenuItem("Tools/Akiro/Report Root Bake Flags")]
    public static void Report()
    {
        ReportFolder(SlapFolder);
        ReportFolder(BlockFolder);
    }

    private static void ReportFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("Folder not found: " + folder);
            return;
        }

        var fbxFiles = Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories);
        foreach (var f in fbxFiles)
        {
            var path = f.Replace("\\", "/");
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            foreach (var c in clips)
            {
                Debug.Log($"{path} | {c.name} | lockRot={c.lockRootRotation} lockY={c.lockRootHeightY} lockXZ={c.lockRootPositionXZ}");
            }
        }
    }
}
