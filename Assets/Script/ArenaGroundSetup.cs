using UnityEngine;

[ExecuteAlways]
public class ArenaGroundSetup : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Renderer groundPlane;      // Plane (floor)
    public Renderer farStripQuad;     // Quad (transition strip)
    public Texture2D sandTex;         // sand_matched_sunset.jpeg
    public Texture2D farStripTex;     // ground_far_strip.jpeg

    [Header("Tuning")]
    public Vector2 sandTiling = new Vector2(3f, 3f);
    [Range(0.2f, 8f)] public float fadePower = 2.2f;
    [Range(-1f, 1f)] public float fadeOffset = 0.0f;

    private void Start()
    {
        ApplySetup();
    }

    private void OnEnable()
    {
        ApplySetup();
    }

    private void OnValidate()
    {
        ApplySetup();
    }

    private void ApplySetup()
    {
        EnsureObjects();

        if (groundPlane != null && sandTex != null)
        {
            var groundMat = new Material(groundPlane.sharedMaterial);
            groundMat.mainTexture = sandTex;
            groundMat.mainTextureScale = sandTiling;
            groundPlane.material = groundMat;
        }

        if (farStripQuad != null && farStripTex != null)
        {
            var stripMat = new Material(Shader.Find("Unlit/FarStripFade"));
            stripMat.SetTexture("_MainTex", farStripTex);
            stripMat.SetColor("_Tint", new Color(1, 1, 1, 1));
            stripMat.SetFloat("_FadePower", fadePower);
            stripMat.SetFloat("_FadeOffset", fadeOffset);
            farStripQuad.material = stripMat;
        }
    }

    private void EnsureObjects()
    {
        if (groundPlane == null)
        {
            GameObject g = GameObject.Find("Arena_GroundPlane");
            if (g == null)
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "Arena_GroundPlane";
                g.transform.position = new Vector3(0f, -0.02f, 0.4f);
                g.transform.rotation = Quaternion.identity;
                g.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            }
            groundPlane = g.GetComponent<Renderer>();
        }

        if (farStripQuad == null)
        {
            GameObject q = GameObject.Find("Arena_GroundFarStrip");
            if (q == null)
            {
                q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Arena_GroundFarStrip";
                q.transform.position = new Vector3(0f, 0.581f, 3.35f);
                q.transform.rotation = Quaternion.identity;
                q.transform.localScale = new Vector3(6f, 1.2f, 1f);
            }
            farStripQuad = q.GetComponent<Renderer>();
        }
    }
}
