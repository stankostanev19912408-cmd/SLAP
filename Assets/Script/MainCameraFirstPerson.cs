using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class MainCameraFirstPerson : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private string sourceFighterName = "idle";
    [SerializeField] private string targetFighterName = "idle (1)";

    [Header("Head Follow")]
    [SerializeField] private Vector3 sourceHeadLocalOffset = new Vector3(0f, 0.0255f, 0.08f);
    [SerializeField] private Vector3 targetHeadLookOffset = new Vector3(0f, 0.06f, 0f);
    [SerializeField] private bool applyInEditMode = true;

    [Header("View Settings")]
    [SerializeField] private float fixedFieldOfView = 60f;
    [SerializeField] private Quaternion fallbackRotation = new Quaternion(
        -0.0000000034f, 0.99685884f, -0.079198554f, -0.0000000435f);
    [SerializeField] private Vector3 headForwardEulerOffset = Vector3.zero;
    [SerializeField] private bool rotateWithSourceHead = true;
    [SerializeField, Range(0f, 1f)] private float headPitchInfluence = 0.02777778f;
    [SerializeField, Range(0f, 1f)] private float headYawInfluence = 0.01388889f;
    [SerializeField, Range(0f, 1f)] private float headRollInfluence = 0.05555556f;
    [SerializeField] private bool lockRoll = true;
    [SerializeField] private float rotationLerp = 20f;

    private Transform sourceHead;
    private Transform targetHead;
    private Camera cachedCamera;

    private void LateUpdate()
    {
        if (!Application.isPlaying && !applyInEditMode) return;
        if (cachedCamera == null) cachedCamera = GetComponent<Camera>();
        if (cachedCamera == null) return;

        var sourceRoot = FindByName(sourceFighterName);
        if (sourceRoot == null) return;

        if (sourceHead == null || !sourceHead.IsChildOf(sourceRoot))
        {
            sourceHead = FindHead(sourceRoot);
        }
        if (sourceHead == null) return;

        transform.position = sourceHead.TransformPoint(sourceHeadLocalOffset);

        Quaternion desiredRotation;
        if (rotateWithSourceHead)
        {
            Quaternion baseRotation = sourceRoot.rotation * Quaternion.Euler(headForwardEulerOffset);
            Quaternion headRotation = sourceHead.rotation * Quaternion.Euler(headForwardEulerOffset);
            Quaternion localDelta = Quaternion.Inverse(baseRotation) * headRotation;
            Vector3 localEuler = NormalizeEuler(localDelta.eulerAngles);

            float pitch = localEuler.x * Mathf.Clamp01(headPitchInfluence);
            float yaw = localEuler.y * Mathf.Clamp01(headYawInfluence);
            float roll = localEuler.z * Mathf.Clamp01(headRollInfluence);
            desiredRotation = baseRotation * Quaternion.Euler(pitch, yaw, roll);
        }
        else
        {
            var targetRoot = FindByName(targetFighterName);
            if (targetRoot == null) return;

            if (targetHead == null || !targetHead.IsChildOf(targetRoot))
            {
                targetHead = FindHead(targetRoot);
            }
            if (targetHead == null) return;

            Vector3 lookTarget = targetHead.position + targetHeadLookOffset;
            Vector3 lookDirection = lookTarget - transform.position;
            desiredRotation = lookDirection.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up) * Quaternion.Euler(headForwardEulerOffset)
                : fallbackRotation;
        }

        if (lockRoll)
        {
            var e = desiredRotation.eulerAngles;
            e.z = 0f;
            desiredRotation = Quaternion.Euler(e);
        }

        if (rotationLerp > 0f)
        {
            float t = Mathf.Clamp01(rotationLerp * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }
        else
        {
            transform.rotation = desiredRotation;
        }

        cachedCamera.fieldOfView = fixedFieldOfView;
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(NormalizeAngle(euler.x), NormalizeAngle(euler.y), NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private static Transform FindByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;
        var go = GameObject.Find(objectName);
        return go != null ? go.transform : null;
    }

    private static Transform FindHead(Transform root)
    {
        if (root == null) return null;

        var animator = root.GetComponent<Animator>();
        if (animator != null)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) return head;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name.ToLowerInvariant().Contains("head")) return t;
        }

        return null;
    }
}
