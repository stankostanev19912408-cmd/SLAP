using System.IO;
using UnityEditor;
using UnityEngine;

public static class AkiroRigBatchSetup
{
    private const string SlapFolder = "Assets/Animation/slap";
    private const string BlockFolder = "Assets/Animation/block";

    [MenuItem("Tools/Akiro/Set Humanoid Rig For Slap+Block")]
    public static void SetHumanoidRig()
    {
        var avatar = FindIdleAvatar();
        if (avatar == null)
        {
            Debug.LogError("idleAvatar not found. Make sure idle.fbx is imported as Humanoid and has an Avatar named 'idleAvatar'.");
            return;
        }

        int changed = 0;
        changed += ProcessFolder(SlapFolder, avatar);
        changed += ProcessFolder(BlockFolder, avatar);

        AssetDatabase.SaveAssets();
        Debug.Log($"Rig setup complete. Updated {changed} FBX files.");
    }

    private static Avatar FindIdleAvatar()
    {
        var guids = AssetDatabase.FindAssets("t:Avatar idleAvatar");
        if (guids != null && guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Avatar>(path);
        }
        return null;
    }

    private static int ProcessFolder(string folder, Avatar avatar)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("Folder not found: " + folder);
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
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                changed = true;
            }
            if (importer.sourceAvatar != avatar)
            {
                importer.sourceAvatar = avatar;
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
