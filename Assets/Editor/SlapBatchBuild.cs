using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SlapBatchBuild
{
    private const string OutputApkPath = "slap.apk";

    public static void BuildAndroidApk()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes in Build Settings.");

        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var result = report.summary.result;
        if (result != BuildResult.Succeeded)
        {
            throw new Exception(
                $"Android build failed. Result: {result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}");
        }

        Debug.Log($"Android APK build succeeded: {OutputApkPath} ({report.summary.totalSize} bytes)");
    }
}
