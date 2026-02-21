using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AkiroClipSetup
{
    private const string SlapFolder = "Assets/Animation/slap";
    private const string BlockFolder = "Assets/Animation/block";
    private const string IdlePath = "Assets/Animation/idle/idle.fbx";

    [MenuItem("Tools/Akiro/Setup Clips From FBX Names")]
    public static void SetupClips()
    {
        int updated = 0;
        updated += SetupSlapClips();
        updated += SetupBlockClips();
        updated += SetupIdleClip();

        AssetDatabase.SaveAssets();
        Debug.Log($"Clip setup complete. Updated {updated} FBX files.");
    }

    private static int SetupSlapClips()
    {
        if (!AssetDatabase.IsValidFolder(SlapFolder))
        {
            Debug.LogWarning("Folder not found: " + SlapFolder);
            return 0;
        }

        int count = 0;
        var fbxFiles = Directory.GetFiles(SlapFolder, "*.fbx", SearchOption.AllDirectories);
        foreach (var f in fbxFiles)
        {
            var path = f.Replace("\\", "/");
            var baseName = Path.GetFileNameWithoutExtension(path);
            var clipBase = SlapBaseName(baseName);
            if (string.IsNullOrEmpty(clipBase))
            {
                Debug.LogWarning("Unrecognized slap file: " + path);
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            var clips = new List<ModelImporterClipAnimation>
            {
                NewClip(clipBase + "_Windup", 1f, 11f),
                NewClip(clipBase + "_Slap", 11f, 21f),
                NewClip(clipBase + "_Return", 21f, 30f)
            };

            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
            count++;
        }

        return count;
    }

    private static int SetupBlockClips()
    {
        if (!AssetDatabase.IsValidFolder(BlockFolder))
        {
            Debug.LogWarning("Folder not found: " + BlockFolder);
            return 0;
        }

        int count = 0;
        var fbxFiles = Directory.GetFiles(BlockFolder, "*.fbx", SearchOption.AllDirectories);
        foreach (var f in fbxFiles)
        {
            var path = f.Replace("\\", "/");
            var baseName = Path.GetFileNameWithoutExtension(path);
            var clipBase = BlockBaseName(baseName);
            if (string.IsNullOrEmpty(clipBase))
            {
                Debug.LogWarning("Unrecognized block file: " + path);
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            var clips = new List<ModelImporterClipAnimation>
            {
                NewClip(clipBase + "_Block", 1f, 10f),
                NewClip(clipBase + "_Return", 10f, 20f)
            };

            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
            count++;
        }

        return count;
    }

    private static int SetupIdleClip()
    {
        if (!File.Exists(IdlePath))
        {
            Debug.LogWarning("Idle FBX not found: " + IdlePath);
            return 0;
        }

        var importer = AssetImporter.GetAtPath(IdlePath) as ModelImporter;
        if (importer == null) return 0;

        var clips = new List<ModelImporterClipAnimation>
        {
            NewClip("Akiro_Idle", 0f, 30f, true)
        };

        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
        return 1;
    }

    private static ModelImporterClipAnimation NewClip(string name, float start, float end, bool loop = false)
    {
        return new ModelImporterClipAnimation
        {
            name = name,
            firstFrame = start,
            lastFrame = end,
            loopTime = loop
        };
    }

    private static string SlapBaseName(string file)
    {
        switch (file.ToLowerInvariant())
        {
            case "slap1": return "Slap1";
            case "slap3": return "Slap3";
            case "slap7": return "Slap7";
            case "slap9": return "Slap9";
            case "slapleft": return "SlapLeft";
            case "slapright": return "SlapRight";
            case "slapup": return "SlapUp";
            case "slapercut": return "SlaperCut"; // keep your naming
            default: return string.Empty;
        }
    }

    private static string BlockBaseName(string file)
    {
        switch (file.ToLowerInvariant())
        {
            case "block1": return "Block1";
            case "block3": return "Block3";
            case "block7": return "Block7";
            case "block9": return "Block9";
            case "blockleft": return "BlockLeft";
            case "blockright": return "BlockRight";
            case "blockup": return "BlockUp";
            case "blockercut": return "BlockerCut"; // keep your naming
            default: return string.Empty;
        }
    }
}
