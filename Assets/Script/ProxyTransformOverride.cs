using UnityEngine;

[DisallowMultipleComponent]
public class ProxyTransformOverride : MonoBehaviour
{
    public Vector3 PositionOffset = Vector3.zero;
    public Vector3 RotationOffset = Vector3.zero;
    public Vector3 BaseScale = Vector3.one;
    public Vector3 ScaleMultiplier = Vector3.one;
    public Vector3 LastAppliedLocalPosition;
    public Quaternion LastAppliedLocalRotation = Quaternion.identity;
    public Vector3 LastAppliedScale = Vector3.one;
    public Vector3 BaseLocalPosition;
    public Quaternion BaseLocalRotation = Quaternion.identity;
    public bool HasBasePose;

    private const float PosEpsilon = 0.0005f;
    private const float RotEpsilon = 0.1f;
    private const float ScaleEpsilon = 0.0005f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (BaseScale == Vector3.zero)
        {
            BaseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        }

        if (!HasBasePose) return;

        PositionOffset = transform.localPosition - BaseLocalPosition;
        RotationOffset = (Quaternion.Inverse(BaseLocalRotation) * transform.localRotation).eulerAngles;

        Vector3 safeBase = BaseScale == Vector3.zero ? Vector3.one : BaseScale;
        ScaleMultiplier = new Vector3(
            safeBase.x == 0f ? 1f : transform.localScale.x / safeBase.x,
            safeBase.y == 0f ? 1f : transform.localScale.y / safeBase.y,
            safeBase.z == 0f ? 1f : transform.localScale.z / safeBase.z
        );
    }
#endif

    public bool TryConsumeManualEditLocal(Transform proxy, Vector3 baseLocalPos, Quaternion baseLocalRot)
    {
        if (proxy == null) return false;

        bool posChanged = (proxy.localPosition - LastAppliedLocalPosition).sqrMagnitude > PosEpsilon * PosEpsilon;
        bool rotChanged = Quaternion.Angle(proxy.localRotation, LastAppliedLocalRotation) > RotEpsilon;
        bool scaleChanged = (proxy.localScale - LastAppliedScale).sqrMagnitude > ScaleEpsilon * ScaleEpsilon;

        if (!posChanged && !rotChanged && !scaleChanged)
        {
            return false;
        }

        PositionOffset = proxy.localPosition - baseLocalPos;
        RotationOffset = (Quaternion.Inverse(baseLocalRot) * proxy.localRotation).eulerAngles;

        Vector3 safeBase = BaseScale == Vector3.zero ? Vector3.one : BaseScale;
        ScaleMultiplier = new Vector3(
            safeBase.x == 0f ? 1f : proxy.localScale.x / safeBase.x,
            safeBase.y == 0f ? 1f : proxy.localScale.y / safeBase.y,
            safeBase.z == 0f ? 1f : proxy.localScale.z / safeBase.z
        );

        return true;
    }

    public void UpdateBasePose(Vector3 baseLocalPos, Quaternion baseLocalRot)
    {
        BaseLocalPosition = baseLocalPos;
        BaseLocalRotation = baseLocalRot;
        HasBasePose = true;
    }
}
