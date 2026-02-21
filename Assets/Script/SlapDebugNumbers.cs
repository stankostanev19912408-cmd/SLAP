using UnityEngine;

public class SlapDebugNumbers : MonoBehaviour
{
    [SerializeField] private SlapMechanics slap;
    [SerializeField] private Animator animator;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.91f, 0f);
    [SerializeField] private bool useAnchorRelativeOffset = true;
    [SerializeField] private float anchorUpOffset = 0.91f;
    [SerializeField] private float anchorForwardOffset = 0f;
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private float postSlapHoldSeconds = 1.0f;

    private Transform anchor;
    private TextMesh textMesh;
    private Camera mainCamera;
    private SlapCombatManager combat;
    private int heldWindup;
    private int heldPower;
    private float holdUntilTime = -1f;
    private bool holdPendingAfterSlapEnd;
    private SlapMechanics pendingHoldSource;

    private void Awake()
    {
        if (slap == null) slap = GetComponent<SlapMechanics>();
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        // Keep both fighters at identical label height regardless per-instance inspector overrides.
        useAnchorRelativeOffset = false;
        worldOffset = new Vector3(0f, 1.555f, 0f);
        anchor = transform;

        var go = new GameObject("SlapDebugText");
        go.transform.SetParent(null, false);
        textMesh = go.AddComponent<TextMesh>();
        textMesh.text = "0 0";
        textMesh.characterSize = 0.013333333f;
        textMesh.fontSize = 96;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
    }

    private void OnEnable()
    {
        SlapMechanics.OnSlapFired += HandleSlapFired;
    }

    private void OnDisable()
    {
        SlapMechanics.OnSlapFired -= HandleSlapFired;
    }

    private void LateUpdate()
    {
        if (textMesh == null) return;
        if (combat == null) combat = SlapCombatManager.Instance;

        SlapMechanics source = slap;
        if (combat != null)
        {
            source = combat.IsPlayerTurn() ? combat.GetPlayer() : combat.GetAI();
        }

        int windup = 0;
        int power = 0;
        bool showHeld = Time.time < holdUntilTime;
        if (holdPendingAfterSlapEnd && pendingHoldSource != null && !pendingHoldSource.IsSlapAnimating())
        {
            holdUntilTime = Time.time + Mathf.Max(0f, postSlapHoldSeconds);
            holdPendingAfterSlapEnd = false;
            pendingHoldSource = null;
            showHeld = true;
        }
        if (showHeld)
        {
            windup = heldWindup;
            power = heldPower;
        }
        else if (source != null)
        {
            float w01 = Mathf.Clamp01(source.GetDebugWindup01());
            if (w01 > 0.001f)
            {
                windup = Mathf.RoundToInt(w01 * 100f);
            }
        }

        textMesh.text = windup.ToString() + " " + power.ToString();
        // Keep this label above its own character.
        Transform displayAnchor = anchor;
        if (displayAnchor == null) displayAnchor = transform;
        Vector3 pos;
        if (useAnchorRelativeOffset && displayAnchor != null)
        {
            pos = displayAnchor.position +
                  (displayAnchor.up * anchorUpOffset) +
                  (displayAnchor.forward * anchorForwardOffset);
        }
        else
        {
            pos = displayAnchor.position + worldOffset;
        }
        textMesh.transform.position = pos;

        if (billboardToCamera)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 toCam = mainCamera.transform.position - textMesh.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    textMesh.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }
    }

    private void HandleSlapFired(SlapMechanics source, SlapMechanics.SlapEvent data)
    {
        if (combat == null) combat = SlapCombatManager.Instance;
        if (combat == null) return;
        var attacker = combat.IsPlayerTurn() ? combat.GetPlayer() : combat.GetAI();
        if (attacker == null || source != attacker) return;

        heldWindup = Mathf.RoundToInt(Mathf.Clamp01(data.windup01) * 100f);
        heldPower = Mathf.RoundToInt(Mathf.Clamp01(data.slapPower01) * 100f);
        holdPendingAfterSlapEnd = true;
        pendingHoldSource = source;
    }

    private void OnDestroy()
    {
        SlapMechanics.OnSlapFired -= HandleSlapFired;
        if (textMesh == null) return;
        if (Application.isPlaying) Destroy(textMesh.gameObject);
        else DestroyImmediate(textMesh.gameObject);
    }

}
