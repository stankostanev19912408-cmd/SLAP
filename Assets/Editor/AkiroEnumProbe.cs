using System;
using UnityEditor;
using UnityEngine;

public static class AkiroEnumProbe
{
    [MenuItem("Tools/Akiro/Probe ModelImporterAnimationType")]
    public static void Probe()
    {
        var names = Enum.GetNames(typeof(ModelImporterAnimationType));
        Debug.Log("ModelImporterAnimationType: " + string.Join(", ", names));
    }
}
