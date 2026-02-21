using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RebuildSampleScene
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TextureFon = "Assets/Animation/fon.png";
    private const string TexturePol = "Assets/Animation/pol.png";
    private const string IdleFbx = "Assets/Animation/idle.fbx";

    [MenuItem("Tools/Rebuild SampleScene (Approx)")]
    private static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild SampleScene",
                "This will overwrite SampleScene.unity with an approximate reconstruction. Continue?",
                "Rebuild",
                "Cancel"))
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 1.0976f, 2.15136f);
        camGo.transform.rotation = Quaternion.Euler(9.085f, 180f, 0f);

        // Directional light
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // SlapRoot
        var slapRoot = new GameObject("SlapRoot");
        slapRoot.transform.position = new Vector3(-0.27177507f, 0.2590248f, -1.9227875f);

        // Characters
        var idle = InstantiateModel(IdleFbx, "idle");
        if (idle != null)
        {
            idle.transform.position = new Vector3(0f, 0f, -0.51f);
            idle.transform.rotation = Quaternion.identity;
            idle.transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        var idle1 = InstantiateModel(IdleFbx, "idle (1)");
        if (idle1 != null)
        {
            idle1.transform.position = new Vector3(0f, 0f, 1.3f);
            idle1.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            idle1.transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        // Backgrounds and floor
        var fonTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFon);
        var polTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePol);
        var fonMat = CreateUnlitTextureMaterial("Fon_Mat", fonTex);
        var polMat = CreateUnlitTextureMaterial("Pol_Mat", polTex);

        CreateQuad("Stadion_Background", fonMat,
            new Vector3(0.02f, 1.15f, 2.71f),
            Quaternion.Euler(8.7f, 0f, 0f),
            new Vector3(2.684f, 3.982f, 1f));

        CreateQuad("Stadion_Background (1)", fonMat,
            new Vector3(0.02f, 1.15f, 3.45f),
            Quaternion.Euler(7.1f, 0f, 0f),
            new Vector3(2.44f, 3.62f, 1f));

        CreateQuad("Stadion_Background_Side", fonMat,
            new Vector3(1.25f, 1.1f, 0f),
            Quaternion.Euler(0f, -90f, 0f),
            new Vector3(2.5f, 3.2f, 1f));

        CreateQuad("Stadion_Background_Idle1", fonMat,
            new Vector3(-0.02f, 1.15f, -1.92f),
            Quaternion.Euler(8.7f, 180f, 0f),
            new Vector3(2.684f, 3.982f, 1f));

        CreateQuad("Pesok_Floor", polMat,
            new Vector3(0.01f, 0.02f, -1.175f),
            Quaternion.identity,
            new Vector3(0.23f, 1f, 0.316f));

        CreateQuad("Pesok_Floor_FarHalf", polMat,
            new Vector3(0.01f, 0.02f, 1.975f),
            Quaternion.identity,
            new Vector3(0.23f, 1f, 0.316f));

        // Mirror camera
        var mirrorCamGo = new GameObject("Main Camera_MirrorIdle1");
        var mirrorCam = mirrorCamGo.AddComponent<Camera>();
        mirrorCam.enabled = false;
        mirrorCamGo.AddComponent<AudioListener>().enabled = false;
        mirrorCamGo.transform.position = new Vector3(0f, 0.65f, 1.34f);
        mirrorCamGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Secondary light
        var lightGo2 = new GameObject("Directional Light (1)");
        var light2 = lightGo2.AddComponent<Light>();
        light2.type = LightType.Directional;
        light2.intensity = 1.0f;
        lightGo2.transform.rotation = Quaternion.Euler(55f, 30f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
    }

    private static GameObject InstantiateModel(string path, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("Missing model at: " + path);
            return null;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance != null)
        {
            instance.name = name;
        }
        return instance;
    }

    private static Material CreateUnlitTextureMaterial(string name, Texture2D tex)
    {
        var shader = Shader.Find("Unlit/Texture");
        var mat = new Material(shader) { name = name };
        if (tex != null)
        {
            mat.mainTexture = tex;
        }
        return mat;
    }

    private static GameObject CreateQuad(string name, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.position = pos;
        quad.transform.rotation = rot;
        quad.transform.localScale = scale;
        var renderer = quad.GetComponent<MeshRenderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }
        var collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        return quad;
    }
}
