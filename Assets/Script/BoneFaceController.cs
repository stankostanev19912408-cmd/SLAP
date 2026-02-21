using UnityEngine;

[DisallowMultipleComponent]
public class BoneFaceController : MonoBehaviour
{
    public enum ExpressionPreset
    {
        Neutral = 0,
        Angry = 1,
        Happy = 2
    }

    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform jawBone;
    [SerializeField] private Transform leftEyeBone;
    [SerializeField] private Transform rightEyeBone;
    [SerializeField] private Transform leftBrowBone;
    [SerializeField] private Transform rightBrowBone;

    [Header("Expression")]
    [SerializeField] private ExpressionPreset expression = ExpressionPreset.Angry;
    [SerializeField] private float expressionBlendSpeed = 8f;

    [Header("Blink")]
    [SerializeField] private bool enableBlink = true;
    [SerializeField] private float blinkIntervalMin = 2f;
    [SerializeField] private float blinkIntervalMax = 5f;
    [SerializeField] private float blinkDuration = 0.08f;
    [SerializeField] private float eyeBlinkAngle = 8f;

    [Header("Mouth Idle")]
    [SerializeField] private bool enableMouthIdle = true;
    [SerializeField] private float mouthIdleAngle = 2f;
    [SerializeField] private float mouthIdleFrequency = 1.25f;

    [Header("Brow")]
    [SerializeField] private float angryBrowDownAngle = 6f;
    [SerializeField] private float happyBrowUpAngle = 4f;

    private Quaternion jawBaseRot;
    private Quaternion leftEyeBaseRot;
    private Quaternion rightEyeBaseRot;
    private Quaternion leftBrowBaseRot;
    private Quaternion rightBrowBaseRot;

    private float expressionWeight;
    private float blinkWeight;
    private float nextBlinkAt;
    private float blinkStartedAt = -100f;
    private bool hasInit;

    private void Awake()
    {
        TryInit();
    }

    private void OnEnable()
    {
        ScheduleBlink();
    }

    private void LateUpdate()
    {
        if (!TryInit()) return;

        UpdateBlink();
        UpdateExpressionWeight();
        ApplyFacePose();
    }

    private bool TryInit()
    {
        if (hasInit) return true;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) return false;

        if (jawBone == null) jawBone = animator.GetBoneTransform(HumanBodyBones.Jaw);
        if (leftEyeBone == null) leftEyeBone = animator.GetBoneTransform(HumanBodyBones.LeftEye);
        if (rightEyeBone == null) rightEyeBone = animator.GetBoneTransform(HumanBodyBones.RightEye);

        CacheBaseRotations();
        hasInit = true;
        return true;
    }

    private void CacheBaseRotations()
    {
        if (jawBone != null) jawBaseRot = jawBone.localRotation;
        if (leftEyeBone != null) leftEyeBaseRot = leftEyeBone.localRotation;
        if (rightEyeBone != null) rightEyeBaseRot = rightEyeBone.localRotation;
        if (leftBrowBone != null) leftBrowBaseRot = leftBrowBone.localRotation;
        if (rightBrowBone != null) rightBrowBaseRot = rightBrowBone.localRotation;
    }

    private void UpdateExpressionWeight()
    {
        expressionWeight = Mathf.MoveTowards(expressionWeight, 1f, expressionBlendSpeed * Time.deltaTime);
    }

    private void UpdateBlink()
    {
        if (!enableBlink)
        {
            blinkWeight = 0f;
            return;
        }

        if (Time.time >= nextBlinkAt && blinkStartedAt < 0f)
        {
            blinkStartedAt = Time.time;
        }

        if (blinkStartedAt < 0f)
        {
            blinkWeight = 0f;
            return;
        }

        float t = (Time.time - blinkStartedAt) / Mathf.Max(0.01f, blinkDuration);
        if (t >= 1f)
        {
            blinkWeight = 0f;
            blinkStartedAt = -100f;
            ScheduleBlink();
            return;
        }

        blinkWeight = t < 0.5f ? (t * 2f) : (2f - t * 2f);
    }

    private void ScheduleBlink()
    {
        if (!enableBlink)
        {
            nextBlinkAt = float.PositiveInfinity;
            return;
        }

        float wait = Random.Range(Mathf.Max(0.1f, blinkIntervalMin), Mathf.Max(blinkIntervalMin, blinkIntervalMax));
        nextBlinkAt = Time.time + wait;
        blinkStartedAt = -100f;
    }

    private void ApplyFacePose()
    {
        float expr = Mathf.Clamp01(expressionWeight);
        float mouthOpen = 0f;
        if (enableMouthIdle)
        {
            mouthOpen = (Mathf.Sin(Time.time * Mathf.Max(0.01f, mouthIdleFrequency) * Mathf.PI * 2f) * 0.5f + 0.5f) * mouthIdleAngle;
        }

        if (jawBone != null)
        {
            jawBone.localRotation = jawBaseRot * Quaternion.Euler(mouthOpen * expr, 0f, 0f);
        }

        float eyeClose = eyeBlinkAngle * blinkWeight;
        if (leftEyeBone != null)
        {
            leftEyeBone.localRotation = leftEyeBaseRot * Quaternion.Euler(eyeClose, 0f, 0f);
        }
        if (rightEyeBone != null)
        {
            rightEyeBone.localRotation = rightEyeBaseRot * Quaternion.Euler(eyeClose, 0f, 0f);
        }

        float browAngle = 0f;
        if (expression == ExpressionPreset.Angry) browAngle = -angryBrowDownAngle;
        else if (expression == ExpressionPreset.Happy) browAngle = happyBrowUpAngle;

        if (leftBrowBone != null)
        {
            leftBrowBone.localRotation = leftBrowBaseRot * Quaternion.Euler(browAngle * expr, 0f, 0f);
        }
        if (rightBrowBone != null)
        {
            rightBrowBone.localRotation = rightBrowBaseRot * Quaternion.Euler(browAngle * expr, 0f, 0f);
        }
    }
}
