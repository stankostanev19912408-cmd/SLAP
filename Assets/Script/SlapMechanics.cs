using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SlapMechanics : MonoBehaviour
{
    public enum Role
    {
        Attacker,
        Defender
    }

    private enum Dir
    {
        None,
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight
    }

    public enum SlapDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
        UpLeft,
        UpRight,
        DownLeft,
        DownRight
    }

    public enum VisualHand
    {
        None,
        Left,
        Right
    }

    private enum AttackPhase
    {
        Idle,
        WindupActive,
        ReturningAfterWindup,
        SlapActive,
        ReturningAfterSlap
    }

    private enum AuraRenderMode
    {
        ScreenEdges,
        AroundCharacter
    }

    private enum AttackHand
    {
        Both,
        Left,
        Right
    }

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string handMoveParam = "Hand_L_Move";
    [SerializeField] private string slapSpeedParam = "Slap_Speed";
    [SerializeField] private string idleStateName = "Akiro_Idle";

    [Header("State Names")]
    [SerializeField] private string slap1Windup = "Slap1_Windup";
    [SerializeField] private string slap1Slap = "Slap1_Slap";
    [SerializeField] private string slap3Windup = "Slap3_Windup";
    [SerializeField] private string slap3Slap = "Slap3_Slap";
    [SerializeField] private string slap7Windup = "Slap7_Windup";
    [SerializeField] private string slap7Slap = "Slap7_Slap";
    [SerializeField] private string slap9Windup = "Slap9_Windup";
    [SerializeField] private string slap9Slap = "Slap9_Slap";
    [SerializeField] private string slapLeftWindup = "SlapLeft_Windup";
    [SerializeField] private string slapLeftSlap = "SlapLeft_Slap";
    [SerializeField] private string slapRightWindup = "SlapRight_Windup";
    [SerializeField] private string slapRightSlap = "SlapRight_Slap";
    [SerializeField] private string slapUpWindup = "SlapUp_Windup";
    [SerializeField] private string slapUpSlap = "SlapUp_Slap";
    [SerializeField] private string slapErcutWindup = "SlaperCut_Windup";
    [SerializeField] private string slapErcutSlap = "SlaperCut_Slap";
    [SerializeField] private string block1Block = "Block1_Block";
    [SerializeField] private string block3Block = "Block3_Block";
    [SerializeField] private string block7Block = "Block7_Block";
    [SerializeField] private string block9Block = "Block9_Block";
    [SerializeField] private string blockLeftBlock = "BlockLeft_Block";
    [SerializeField] private string blockRightBlock = "BlockRight_Block";
    [SerializeField] private string blockUpBlock = "BlockUp_Block";
    [SerializeField] private string blockErcutBlock = "BlockerCut_Block";

    [Header("Timing")]
    [SerializeField] private float swipeDeadzonePx = 20f;
    [SerializeField] private float slapWindowSeconds = 1.5f;
    [SerializeField] private float swipeDistanceCm = 3.5f;
    [SerializeField] private float blockSwipeDistanceCm = 2f;
    [SerializeField] private float slapReverseDistanceFactor = 3f;
    [SerializeField] private bool alwaysTriggerSlapOnReverseSwipe = true;
    [SerializeField] private float minReverseSwipeDistanceCm = 0.2f;
    [SerializeField] private float windupThreshold = 0.5f;
    [SerializeField] private float minWindupForSlap = 0.5f;
    [SerializeField] private float minReverseSwipeSpeedCmPerSec = 18f;
    [SerializeField] private float minReverseHoldSecondsForSlap = 0.06f;
    [SerializeField] private float releaseDuration = 0.35f;
    [SerializeField] private bool returnToIdleByShortestRoute = true;
    [SerializeField] private float releaseToIdleCrossfadeSeconds = 0.08f;
    [SerializeField] private bool smoothReturnToIdleAfterSlap = true;
    [SerializeField] private float slapReturnCrossfadeSeconds = 0.24f;
    [SerializeField] private float slapReturnStartNormalizedTime = 0.98f;
    [SerializeField] private float fallbackDpi = 160f;
    [SerializeField] private float slapSpeedMax = 7.35f;
    [SerializeField] private float speedMinCmPerSec = 2.4f;
    [SerializeField] private float speedMaxCmPerSec = 432f;
    [SerializeField] private float swipeSpeedResponseMultiplier = 1.76f;
    [SerializeField] private float speedWeightInWindup = 0.2f;
    [SerializeField] private float minPlayableSlapSpeed = 0.05f;
    [SerializeField] private float diagonalSlapSpeedMultiplier = 1f;
    [SerializeField] private float sideSlapSpeedMultiplier = 1f;
    [SerializeField] private float upSlapSpeedMultiplier = 1f;
    [SerializeField] private float uppercutSlapSpeedMultiplier = 1f;
    [SerializeField] private float sideSlapMinPlayableSpeed = 0.05f;
    [SerializeField] private float uppercutSlapMinPlayableSpeed = 0.05f;
    [Header("Intro")]
    [SerializeField] private float introToIdleBlendSeconds = 0.84f;

    [Header("Role")]
    [SerializeField] private Role role = Role.Attacker;
    [SerializeField] public bool allowHumanInput = true;
    [Header("Stability")]
    [SerializeField] private bool lockVerticalPosition = true;
    [SerializeField] private bool lockCharacterHorizontalPosition = true;
    [SerializeField] private bool lockCharacterRotation = true;
    [SerializeField] private bool lockHipsVertical = true;
    [SerializeField] private bool lockHipsHorizontalPosition = true;
    [SerializeField] private bool lockHipsRotation = true;
    [SerializeField] private bool lockLowerBodyRotation = true;
    [SerializeField] private bool lockFeetLocalPosition = true;
    [SerializeField] private bool lockToesOnGround = true;
    [SerializeField] private float legsLockAcquireSeconds = 0.24f;
    [SerializeField] private float legsLockReleaseSeconds = 1.8f;
    [SerializeField] private float toesLockAcquireSeconds = 1.8f;
    [SerializeField] private float toesLockReleaseSeconds = 0.36f;
    [SerializeField] private bool hardLockShins = true;
    [SerializeField] private bool smoothKneeLock = true;
    [SerializeField] private float kneeLockSmoothing = 12f;
    [SerializeField] private bool lockLegsOnlyInIdle = true;
    [SerializeField] private bool lockBodyDuringAttackPhases = true;
    [SerializeField] private bool lockDefenderBodyDuringBlock = true;
    [SerializeField] private bool calibrateLegReferenceOnStartup = true;
    [SerializeField] private int legReferenceCalibrationDelayFrames = 2;
    [SerializeField] private bool blockInputUntilStartupCalibration = true;
    [SerializeField] private int startupIdleWarmupFrames = 6;
    [SerializeField] private bool forceIdleSampleBeforeStartupCalibration = true;
    [SerializeField] private bool hardStabilizeFirstRoundOnStart = true;
    [SerializeField] private bool repinBodyOnRoleSwitch = false;
    [SerializeField] private Transform hipsBone;
    [Header("Slap Start")]
    [SerializeField] private bool lockHandPoseOnSlapStart = true;
    [SerializeField] private float slapPoseBlendDuration = 0.08f;
    [SerializeField] private float slapStateCrossfadeSeconds = 0.06f;
    [SerializeField] private float slapPoseReleasePower = 2f;
    [SerializeField] private bool preserveLowerBodyInSlapPoseBlend = true;
    [SerializeField] private float cardinalSlapPoseBlendDuration = 0.18f;
    [SerializeField] private float cardinalSlapCrossfadeSeconds = 0f;
    [SerializeField] private float cardinalSlapPoseReleasePower = 3f;
    [SerializeField] private float upSlapCrossfadeSeconds = 0.06f;
    [SerializeField] private float upSlapPoseBlendDuration = 0.28f;
    [SerializeField] private float upSlapPoseReleasePower = 4f;
    [SerializeField] private float upSlapStartNormalizedTime = 0f;
    [SerializeField] private float sideUppercutSlapStartMaxNormalizedTime = 0.35f;
    [SerializeField] private bool useDirectHandTimeForCardinalSlaps = true;
    [SerializeField] private float cardinalSlapStartOffset = 0f;
    [SerializeField] private float diagonalSlapStartOffset = 0f;
    [SerializeField] private float windupStartCrossfadeSeconds = 0.105f;
    [Header("Visual")]
    [SerializeField] private bool handsOnlyVisual = false;
    [SerializeField] private bool allowGeneratedHandOnlyMeshes = false;
    [SerializeField] private bool forceCharacterCastShadows = true;
    [SerializeField] private bool forceCharacterReceiveShadows = true;
    [SerializeField] private SlapDirection preferredCombatHand = SlapDirection.Left;
    [SerializeField] private bool forceDirectionHandForHorizontal = true;
    [SerializeField] private bool horizontalSwipeUsesOppositeHand = false;
    [Header("Shoulder Caps")]
    [SerializeField] private bool enableShoulderCaps = true;
    [SerializeField] private float shoulderCapRadius = 0.045f;
    [SerializeField] private float shoulderCapLength = 0.11f;
    [SerializeField] private float shoulderCapForwardOffset = 0.01f;
    [SerializeField] private float shoulderCapUpOffset = 0.0f;
    [SerializeField] private float shoulderCapInwardOffset = 0.015f;
    [Header("Idle Breathing")]
    [SerializeField] private Transform idleBreathingBone;
    [Header("Directional Aura")]
    [SerializeField] private bool enableDirectionalAura = false;
    [SerializeField] private bool isDirectionalAuraOwner = true;
    [SerializeField] private float auraMaxAlpha = 0.85f;
    [SerializeField] private float auraMinAlphaAtAnyWindup = 0.01f;
    [SerializeField] private float auraBlobSize01 = 0.95f;
    [SerializeField] private float auraBlendSpeed = 14f;
    [SerializeField] private float auraFlashDuration = 0.12f;
    [SerializeField] private float auraFlashMaxAlpha = 0.35f;
    [SerializeField] private AuraRenderMode auraRenderMode = AuraRenderMode.ScreenEdges;
    [SerializeField] private float characterAuraPadding = 1.15f;
    [SerializeField] private float characterAuraDirectionOffset = 0.28f;
    [Header("AI Block Lock")]
    [SerializeField] private float aiHardBlockRaiseSeconds = 0.18f;
    [SerializeField, Range(0f, 1f)] private float aiHardBlockTargetHold01 = 1f;
    [SerializeField, Range(0.05f, 1f)] private float defenderBlockPoseMaxNormalizedTime = 0.82f;
    [SerializeField] private float defenderBlockReleaseSeconds = 0.16f;
    [SerializeField] private float aiBlockReacquireCooldownSeconds = 0.7f;

    private Vector2 swipeStart;
    private bool swipeActive;
    private bool windupTriggered;
    private Dir pendingDir = Dir.None;
    private float pendingTime;
    private float requiredPixels;
    private float requiredBlockPixels;
    private float maxProgress;
    private float windupCarryOffset;
    private float handVelocity;
    private string currentWindupState;
    private string currentSlapState;
    private string currentBlockState;
    private float lastSwipeSpeed;
    private Dir lastBlockDir = Dir.None;
    private bool slapPlayedThisSwipe;
    private Vector2 lastSwipePos;
    private float lastSwipeSampleTime;
    private float maxSwipeSpeedCmPerSec;
    private float reverseSwipePeakCmPerSec;
    private float reverseSwipeCurrentCmPerSec;
    private float reverseSwipeSmoothedCmPerSec;
    private float reverseAccumulatedPx;
    private float reverseIntentHeldSeconds;
    private float lastWindupReleaseTime;
    private bool suppressSlapUntilNextTouch;
    private bool releaseReturnActive;
    private bool windupReturnTrackingActive;
    private float windupReturnStartTime;
    private float windupReturnDuration;
    private float windupReturnStart01;
    private bool slapReturnTriggered;
    private float startY;
    private bool startYPinned;
    private Vector3 startPosition;
    private bool startPositionPinned;
    private Quaternion startRotation;
    private bool startRotationPinned;
    private float hipsStartLocalY;
    private bool hipsStartPinned;
    private Vector3 hipsStartLocalPosition;
    private bool hipsPositionPinned;
    private Quaternion hipsStartLocalRotation;
    private bool hipsRotationPinned;
    private bool repinBodyOnNextStableIdle;
    private Transform[] lowerBodyBones;
    private Quaternion[] lowerBodyStartLocalRotations;
    private Transform leftFootBone;
    private Transform rightFootBone;
    private Transform leftToesBone;
    private Transform rightToesBone;
    private Vector3 leftFootStartLocalPos;
    private Vector3 rightFootStartLocalPos;
    private Vector3 leftToesStartLocalPos;
    private Vector3 rightToesStartLocalPos;
    private Vector3 leftToesStartWorldPos;
    private Vector3 rightToesStartWorldPos;
    private Quaternion leftFootStartLocalRot;
    private Quaternion rightFootStartLocalRot;
    private Quaternion leftToesStartLocalRot;
    private Quaternion rightToesStartLocalRot;
    private float legsLockBlend = 1f;
    private float toesLockBlend;
    private bool feetStartLocalPosPinned;
    private bool startupLegReferenceCalibrated;
    private int startupLegCalibrationFrameCounter;
    private bool startupHardStabilized;
    private AttackPhase attackPhase = AttackPhase.Idle;
    private bool inputLockedAfterSlap;
    private float maxProjectedPx;
    private Transform[] slapPoseBones;
    private Vector3[] slapPoseStartLocalPositions;
    private Quaternion[] slapPoseStartLocalRotations;
    private HumanPoseHandler humanPoseHandler;
    private bool humanPoseAvailable;
    private HumanPose slapStartHumanPose;
    private HumanPose currentHumanPose;
    private bool[] lowerBodyMuscleMask;
    private bool slapPoseBlendActive;
    private float slapPoseBlendStartTime;
    private float activeSlapPoseBlendDuration;
    private float activeSlapPoseReleasePower;
    private float debugLastSlapPower01;
    private bool handsOnlyApplied;
    private Dir auraLastDir = Dir.Right;
    private float auraWindupSmoothed;
    private bool auraWasSlapActive;
    private float auraFlashTimer;
    private float auraFlashPower;
    private static Texture2D auraRadialTex;
    private static readonly Vector3[] AuraBoundsCorners = new Vector3[8];
    private bool defenderBlockHeld;
    private float defenderBlockStartTime;
    private float defenderBlockHoldNormalized;
    private float defenderAnimatorSpeedBeforeHold = 1f;
    private bool defenderBlockChosenThisSwipe;
    private bool defenderBlockReleaseActive;
    private float aiBlockReacquireBlockedUntilTime = -999f;
    private bool aiHardBlockLock;
    private Dir aiHardBlockDir = Dir.None;
    private float aiHardBlockBlendStartTime;
    private float aiHardBlockStartHold01;
    private string aiHardBlockLastStateName;
    private float aiHardBlockLastAppliedHold = -1f;
    private float windupStartTime;
    private bool windupStartTimeSet;
    private Dir lastSlapDir = Dir.None;
    private float idleBreathingLastOffsetY;
    private Transform attackLeftHandBone;
    private Transform attackRightHandBone;
    private Vector3 attackLeftHandStartLocalPos;
    private Vector3 attackRightHandStartLocalPos;
    private Quaternion attackLeftHandStartLocalRot;
    private Quaternion attackRightHandStartLocalRot;
    private bool attackHandStartPinned;
    private AttackHand activeAttackHand = AttackHand.Both;
    private AttackHand swipeAttackHand = AttackHand.Both;
    private bool swipeAttackHandLocked;
    private Renderer generatedLeftHandRenderer;
    private Renderer generatedRightHandRenderer;
    private Transform leftUpperArmBone;
    private Transform rightUpperArmBone;
    private Renderer leftShoulderCapRenderer;
    private Renderer rightShoulderCapRenderer;
    private Material shoulderCapMaterial;
    private Texture2D shoulderCapTexture;
    private PlayableGraph introPlayableGraph;
    private bool introPlayableGraphActive;
    private float introReturnToIdleAtTime = -1f;
    private float introBlendOutUntilTime = -1f;
    private bool introGraphBlendActive;
    private float introGraphBlendStartTime = -1f;
    private float introGraphBlendDuration = 0f;
    private AnimationMixerPlayable introMixerPlayable;
    private AnimatorControllerPlayable introControllerPlayable;
    private AnimationClipPlayable introClipPlayable;
    private RuntimeAnimatorController introOriginalController;
    private AnimatorOverrideController introOverrideController;
    private Transform[] introRigLockChain;
    private Quaternion[] introRigLockChainStartRotations;
    private bool introShouldersLockActive;
    private Transform[] introShoulderLockBones;
    private Quaternion[] introShoulderLockStartRotations;
    private bool introBodyExceptHeadLockActive;
    private Transform[] introBodyLockBones;
    private Quaternion[] introBodyLockStartRotations;

    public struct SlapEvent
    {
        public float windup01;
        public float slapPower01;
        public SlapDirection direction;
        public float windupHoldSeconds;
    }

    public static event System.Action<SlapMechanics, SlapEvent> OnSlapFired;

    private void Awake()
    {
        EnsureAnimator();
        EnsurePositiveScaleX();
        ApplyCharacterShadowSettings();
        PinStartPosition();
        PinStartY();
        PinStartRotation();
        FindHipsBoneIfNeeded();
        CacheSlapPoseBonesIfNeeded();
        SetupHumanPoseIfPossible();
        CacheLowerBodyBonesIfNeeded();
        CacheFeetBonesIfNeeded();
        CacheAttackHandBonesIfNeeded();
        PinAttackHandStartIfNeeded();
        CacheShoulderBonesIfNeeded();
        FindIdleBreathingBoneIfNeeded();
        RemoveGeneratedHandOnlyMeshesIfPresent();
        PinFeetStartLocalPosition();
        PinHipsStartY();
        PinHipsStartPosition();
        PinHipsStartRotation();
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        ApplyHandsOnlyVisualIfNeeded();
        RemoveShoulderCapsIfPresent();
        ReleaseDefenderBlockHold();
    }

    private void ApplyCharacterShadowSettings()
    {
        if (!forceCharacterCastShadows && !forceCharacterReceiveShadows) return;
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (forceCharacterCastShadows)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (forceCharacterReceiveShadows)
            {
                r.receiveShadows = true;
            }
        }
    }

    private void Start()
    {
        if (!hardStabilizeFirstRoundOnStart) return;
        if (startupHardStabilized) return;
        EnsureAnimator();
        if (animator == null) return;

        // Force deterministic idle sample before first round.
        if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            animator.Play(idleStateName, 0, 0f);
            animator.Update(0f);
        }

        RebindStabilityReferencesFromCurrentPose();
        startupLegReferenceCalibrated = true;
        startupHardStabilized = true;
    }

    private void Update()
    {
        EnsureAnimator();
        EnsurePositiveScaleX();
        if (animator == null) return;
        UpdateIntroPlayback();
        if (Time.time < introBlendOutUntilTime) return;
        if (introPlayableGraphActive) return;

        UpdateRequiredPixels();
        UpdateAttackPhaseFromAnimator();
        TryStartSmoothReturnAfterSlap();
        CancelUnintendedSlapAfterRelease();
        EnforceSlapSuppression();
        ApplyAIHardBlockLock();
        UpdateDefenderBlockRelease();

        if (pendingDir != Dir.None && slapWindowSeconds > 0f)
        {
            float elapsed = Time.time - pendingTime;
            if (elapsed > slapWindowSeconds && animator.GetFloat(handMoveParam) <= 0.02f)
            {
                pendingDir = Dir.None;
                maxProgress = 0f;
                windupCarryOffset = 0f;
                if (!inputLockedAfterSlap)
                {
                    attackPhase = AttackPhase.Idle;
                }
                windupStartTimeSet = false;
            }
        }

        bool canProcessHumanInput = allowHumanInput &&
                                    (!blockInputUntilStartupCalibration || startupLegReferenceCalibrated);
        if (canProcessHumanInput)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (!inputLockedAfterSlap)
                    {
                        swipeAttackHand = AttackHand.Both;
                        swipeAttackHandLocked = false;
                        suppressSlapUntilNextTouch = false;
                        releaseReturnActive = false;
                        slapReturnTriggered = false;
                        RepinFeetLocalPoseNow();
                        swipeActive = true;
                        windupTriggered = false;
                        slapPlayedThisSwipe = false;
                        lastBlockDir = Dir.None;
                        swipeStart = touch.position;
                        lastSwipePos = swipeStart;
                        lastSwipeSampleTime = Time.time;
                        windupCarryOffset = GetCurrentHandProgressForCombat();
                        maxProgress = windupCarryOffset;
                        maxSwipeSpeedCmPerSec = 0f;
                        reverseSwipePeakCmPerSec = 0f;
                        reverseAccumulatedPx = 0f;
                        reverseIntentHeldSeconds = 0f;
                        defenderBlockChosenThisSwipe = false;
                    }
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (swipeActive)
                    {
                        HandleSwipeMove(touch.position);
                    }
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (swipeActive)
                    {
                        HandleSwipeEnd(touch.position);
                    }
                    swipeActive = false;
                }
            }
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0))
            {
                if (!inputLockedAfterSlap)
                {
                    swipeAttackHand = AttackHand.Both;
                    swipeAttackHandLocked = false;
                    suppressSlapUntilNextTouch = false;
                    releaseReturnActive = false;
                    slapReturnTriggered = false;
                    RepinFeetLocalPoseNow();
                    swipeActive = true;
                    windupTriggered = false;
                    slapPlayedThisSwipe = false;
                    lastBlockDir = Dir.None;
                    swipeStart = Input.mousePosition;
                    lastSwipePos = swipeStart;
                    lastSwipeSampleTime = Time.time;
                    windupCarryOffset = GetCurrentHandProgressForCombat();
                    maxProgress = windupCarryOffset;
                    maxSwipeSpeedCmPerSec = 0f;
                    reverseSwipePeakCmPerSec = 0f;
                    reverseAccumulatedPx = 0f;
                    reverseIntentHeldSeconds = 0f;
                    defenderBlockChosenThisSwipe = false;
                }
            }
            if (Input.GetMouseButton(0))
            {
                if (swipeActive)
                {
                    HandleSwipeMove(Input.mousePosition);
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (swipeActive)
                {
                    HandleSwipeEnd(Input.mousePosition);
                }
                swipeActive = false;
            }
#endif
        }

        if (!swipeActive || pendingDir == Dir.None)
        {
            bool hasIdleStateForShortest =
                !string.IsNullOrEmpty(idleStateName) &&
                animator.HasState(0, Animator.StringToHash(idleStateName));
            bool shortestReturnByCrossfade =
                releaseReturnActive &&
                returnToIdleByShortestRoute &&
                !swipeActive &&
                attackPhase == AttackPhase.ReturningAfterWindup &&
                hasIdleStateForShortest;

            float v = animator.GetFloat(handMoveParam);
            if (!shortestReturnByCrossfade)
            {
                v = SmoothToZero(v);
                animator.SetFloat(handMoveParam, v);
            }
            else if (!animator.IsInTransition(0) && IsAnimatorInIdleState())
            {
                // End shortest-path return exactly when idle is reached.
                releaseReturnActive = false;
                if (!string.IsNullOrEmpty(handMoveParam))
                {
                    animator.SetFloat(handMoveParam, 0f);
                }
                ClearWindupReturnTracking();
                pendingDir = Dir.None;
                maxProgress = 0f;
                windupCarryOffset = 0f;
                windupStartTimeSet = false;
                if (!inputLockedAfterSlap) attackPhase = AttackPhase.Idle;
            }

            // End carry only after hand fully returned.
            if (!swipeActive && pendingDir != Dir.None && v <= 0.02f)
            {
                pendingDir = Dir.None;
                ClearWindupReturnTracking();
                maxProgress = 0f;
                windupCarryOffset = 0f;
                if (!inputLockedAfterSlap)
                {
                    attackPhase = AttackPhase.Idle;
                }
            }

            if (inputLockedAfterSlap && v <= 0.02f && attackPhase != AttackPhase.SlapActive)
            {
                inputLockedAfterSlap = false;
                attackPhase = AttackPhase.Idle;
            }

            // Release/cancel path: ensure we always leave ReturningAfterWindup once hand returned.
            if (!inputLockedAfterSlap &&
                pendingDir == Dir.None &&
                !swipeActive &&
                v <= 0.02f &&
                attackPhase == AttackPhase.ReturningAfterWindup)
            {
                ClearWindupReturnTracking();
                attackPhase = AttackPhase.Idle;
            }

            if (releaseReturnActive && !shortestReturnByCrossfade && v <= 0.02f)
            {
                releaseReturnActive = false;
                ClearWindupReturnTracking();
            }
        }

        UpdateDirectionalAuraState();
        UpdateHandsOnlyCombatVisibility();
        UpdateShoulderCapsVisibility();

    }

    private void LateUpdate()
    {
        if (introPlayableGraphActive || Time.time < introBlendOutUntilTime)
        {
            // Keep character from sinking/drifting during intro playback.
            if (lockVerticalPosition) KeepYStable();
            if (lockCharacterHorizontalPosition) KeepHorizontalPositionStable();
            // Hard-lock rotation during intro, independent from inspector flags.
            KeepRotationStable();
            if (lockHipsVertical) KeepHipsYStable();
            if (lockHipsHorizontalPosition) KeepHipsHorizontalStable();
            KeepHipsRotationStable();
            ApplyIntroRigRootLock();
            ApplyIntroBodyExceptHeadLock();
            ApplyIntroShoulderLock();
            // Freeze everything below the waist during intro.
            legsLockBlend = 1f;
            toesLockBlend = lockToesOnGround ? 1f : 0f;
            KeepLowerBodyRotationStable();
            KeepFeetLocalPositionStable();
            if (lockToesOnGround)
            {
                KeepToesWorldPositionStable();
            }
            return;
        }
        TryCalibrateLegReferenceOnStartup();
        TryRepinBodyReferenceOnceInStableIdle();
        ApplyStabilityLocks();
        ApplySlapPoseBlend();
        bool keepLegsLocked = !lockLegsOnlyInIdle || ShouldLockLegsNow();
        bool forceFeetLockDuringWindup =
            attackPhase == AttackPhase.WindupActive ||
            attackPhase == AttackPhase.ReturningAfterWindup ||
            windupTriggered ||
            pendingDir != Dir.None;

        float targetLegsBlend = keepLegsLocked ? 1f : 0f;
        float legsBlendSeconds = targetLegsBlend > legsLockBlend
            ? Mathf.Max(0.01f, legsLockAcquireSeconds)
            : Mathf.Max(0.01f, legsLockReleaseSeconds);
        legsLockBlend = Mathf.MoveTowards(legsLockBlend, targetLegsBlend, Time.deltaTime / legsBlendSeconds);

        if (lockLowerBodyRotation && legsLockBlend > 0.0001f)
        {
            KeepLowerBodyRotationStable(legsLockBlend);
        }
        if (lockFeetLocalPosition && legsLockBlend > 0.0001f)
        {
            KeepFeetLocalPositionStable(legsLockBlend);
        }

        float targetToesBlend = (forceFeetLockDuringWindup && lockToesOnGround) ? 1f : 0f;
        float toesBlendSeconds = targetToesBlend > toesLockBlend
            ? Mathf.Max(0.01f, toesLockAcquireSeconds)
            : Mathf.Max(0.01f, toesLockReleaseSeconds);
        toesLockBlend = Mathf.MoveTowards(toesLockBlend, targetToesBlend, Time.deltaTime / toesBlendSeconds);

        if (toesLockBlend > 0.0001f)
        {
            KeepToesLocalRotationStable(toesLockBlend);
            if (lockToesOnGround)
            {
                KeepToesWorldPositionStable(toesLockBlend);
            }
        }
        ApplyIdleBreathing();
    }

    private void TryRepinBodyReferenceOnceInStableIdle()
    {
        if (!repinBodyOnNextStableIdle) return;
        if (!ShouldLockLegsNow()) return;

        // Re-pin only body/hips references to remove first-round tilt/sink.
        // Do not touch leg reference arrays to keep knee behavior stable.
        startY = transform.position.y;
        startPosition = transform.position;
        startRotation = transform.rotation;
        startYPinned = true;
        startPositionPinned = true;
        startRotationPinned = true;

        FindHipsBoneIfNeeded();
        if (hipsBone != null)
        {
            hipsStartLocalY = hipsBone.localPosition.y;
            hipsStartLocalPosition = hipsBone.localPosition;
            hipsStartLocalRotation = hipsBone.localRotation;
            hipsStartPinned = true;
            hipsPositionPinned = true;
            hipsRotationPinned = true;
        }

        repinBodyOnNextStableIdle = false;
    }

    private void TryCalibrateLegReferenceOnStartup()
    {
        if (!calibrateLegReferenceOnStartup) return;
        if (startupLegReferenceCalibrated) return;

        startupLegCalibrationFrameCounter++;
        int waitFrames = Mathf.Max(0, legReferenceCalibrationDelayFrames) + Mathf.Max(0, startupIdleWarmupFrames);
        if (startupLegCalibrationFrameCounter < waitFrames) return;
        if (!ShouldLockLegsNow()) return;

        if (forceIdleSampleBeforeStartupCalibration &&
            animator != null &&
            !string.IsNullOrEmpty(idleStateName) &&
            animator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            animator.Play(idleStateName, 0, 0f);
            animator.Update(0f);
        }
        RebindStabilityReferencesFromCurrentPose();

        startupLegReferenceCalibrated = true;
    }

    private void RebindStabilityReferencesFromCurrentPose()
    {
        CacheLowerBodyBonesIfNeeded();
        if (lowerBodyBones != null && lowerBodyStartLocalRotations != null)
        {
            int count = Mathf.Min(lowerBodyBones.Length, lowerBodyStartLocalRotations.Length);
            for (int i = 0; i < count; i++)
            {
                var b = lowerBodyBones[i];
                if (b == null) continue;
                lowerBodyStartLocalRotations[i] = b.localRotation;
            }
        }

        CacheFeetBonesIfNeeded();
        if (leftFootBone != null) leftFootStartLocalPos = leftFootBone.localPosition;
        if (rightFootBone != null) rightFootStartLocalPos = rightFootBone.localPosition;
        if (leftToesBone != null) leftToesStartLocalPos = leftToesBone.localPosition;
        if (rightToesBone != null) rightToesStartLocalPos = rightToesBone.localPosition;
        feetStartLocalPosPinned = true;

        startY = transform.position.y;
        startPosition = transform.position;
        startRotation = transform.rotation;
        startYPinned = true;
        startPositionPinned = true;
        startRotationPinned = true;

        FindHipsBoneIfNeeded();
        if (hipsBone != null)
        {
            hipsStartLocalY = hipsBone.localPosition.y;
            hipsStartLocalPosition = hipsBone.localPosition;
            hipsStartLocalRotation = hipsBone.localRotation;
            hipsStartPinned = true;
            hipsPositionPinned = true;
            hipsRotationPinned = true;
        }
    }

    private void OnDisable()
    {
        StopIntroPlayableGraph();
    }

    private void OnDestroy()
    {
        StopIntroPlayableGraph();
    }

    private void OnGUI()
    {
        if (!enableDirectionalAura || !isDirectionalAuraOwner)
        {
            // Fallback: show defender aura for human player even if flags were reset.
            if (!(allowHumanInput && role == Role.Defender && defenderBlockHeld)) return;
        }
        if (Event.current.type != EventType.Repaint) return;
        EnsureAuraTexture();

        Dir dir = GetAuraDirection();
        if (dir == Dir.None) return;

        float windup = Mathf.Clamp01(auraWindupSmoothed);
        float alpha = 0f;
        if (windup > 0.0001f)
        {
            alpha = Mathf.Lerp(Mathf.Clamp01(auraMinAlphaAtAnyWindup), 1f, windup) * Mathf.Clamp01(auraMaxAlpha);
        }

        if (alpha > 0.0001f)
        {
            Color auraColor = role == Role.Defender
                ? new Color(0.1f, 0.45f, 1f, alpha)
                : new Color(1f, 0f, 0f, alpha);
            DrawDirectionalAura(dir, auraColor, Mathf.Clamp(auraBlobSize01, 0.2f, 2f));
        }

        if (role != Role.Defender && auraFlashTimer > 0f)
        {
            float t = auraFlashTimer / Mathf.Max(0.001f, auraFlashDuration);
            float flashAlpha = t * Mathf.Clamp01(auraFlashMaxAlpha) * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(auraFlashPower));
            DrawDirectionalAura(dir, new Color(1f, 1f, 1f, flashAlpha), Mathf.Clamp(auraBlobSize01 * 0.7f, 0.2f, 2f));
        }
    }


    private void HandleSwipeMove(Vector2 pos)
    {
        if (inputLockedAfterSlap) return;

        float now = Time.time;
        float dt = Mathf.Max(1f / 120f, now - lastSwipeSampleTime);
        Vector2 frameDelta = pos - lastSwipePos;
        float speedPxPerSec = frameDelta.magnitude / dt;
        lastSwipeSpeed = PixelsToCm(speedPxPerSec);
        if (lastSwipeSpeed > maxSwipeSpeedCmPerSec) maxSwipeSpeedCmPerSec = lastSwipeSpeed;
        UpdateReverseSwipeTracking(frameDelta, dt);

        // Allow finish-slaps during carry return even if next swipe direction differs from pending windup.
        // This check must run before the hard retarget lock below.
        if (role == Role.Attacker && !windupTriggered && pendingDir != Dir.None)
        {
            if (TryTriggerSlapFromReverse(pos, frameDelta, dt, now))
            {
                return;
            }
        }

        Vector2 deltaFromStart = pos - swipeStart;
        Vector2 dirInputDelta = deltaFromStart;
        float deadzonePx = GetDeadzonePx();

        // Defender blocks should switch by current finger movement (frame delta),
        // not only by distance from initial touch point.
        if (role == Role.Defender)
        {
            float frameDeadzone = deadzonePx * 0.5f;
            if (frameDelta.magnitude >= frameDeadzone)
            {
                dirInputDelta = frameDelta;
            }
        }

        if (dirInputDelta.magnitude < deadzonePx)
        {
            lastSwipePos = pos;
            lastSwipeSampleTime = now;
            return;
        }

        Dir dir = GetDirection(dirInputDelta);
        if (dir == Dir.None)
        {
            lastSwipePos = pos;
            lastSwipeSampleTime = now;
            return;
        }

        // Hard rule:
        // if hand is still carrying windup direction, do not retarget to another direction.
        if (role == Role.Attacker && !windupTriggered && pendingDir != Dir.None && dir != pendingDir)
        {
            lastSwipePos = pos;
            lastSwipeSampleTime = now;
            return;
        }

        if (role == Role.Defender)
        {
            if (!defenderBlockChosenThisSwipe)
            {
                SetBlockState(dir);
                if (!string.IsNullOrEmpty(currentBlockState))
                {
                    lastBlockDir = dir;
                    swipeStart = pos;
                    maxProgress = 0f;
                    windupCarryOffset = 0f;
                    defenderBlockHoldNormalized = 0f;
                    defenderBlockChosenThisSwipe = true;
                    defenderBlockStartTime = Time.time;
                }
            }
            if (defenderBlockChosenThisSwipe && !string.IsNullOrEmpty(currentBlockState) && lastBlockDir != Dir.None)
            {
                float progress = GetProgressAlongDir(pos, swipeStart, lastBlockDir, true);
                if (progress > defenderBlockHoldNormalized)
                {
                    defenderBlockHoldNormalized = progress;
                }

                // Keep defender block at reached height while finger stays on screen.
                HoldDefenderBlockState(currentBlockState, defenderBlockHoldNormalized);
            }
            windupTriggered = true;
            pendingDir = Dir.None;
            maxProgress = 0f;
            windupCarryOffset = 0f;
            animator.SetFloat(handMoveParam, 0f);
            lastSwipePos = pos;
            lastSwipeSampleTime = now;
            return;
        }

        if (TryTriggerSlapFromReverse(pos, frameDelta, dt, now))
        {
            return;
        }

        if (!windupTriggered)
        {
            RepinFeetLocalPoseNow();
            float carryFromCurrent = GetCurrentHandProgressForCombat();
            ClearWindupReturnTracking();
            SetWindupStates(dir);
            if (!string.IsNullOrEmpty(currentWindupState))
            {
                float fade = Mathf.Max(0f, windupStartCrossfadeSeconds);
                if (fade <= 0f)
                {
                    animator.Play(currentWindupState, 0, 0f);
                }
                else
                {
                    animator.CrossFadeInFixedTime(currentWindupState, fade, 0, 0f, 0f);
                }
            }
            LockSwipeAttackHandForDir(dir);
            pendingDir = dir;
            pendingTime = Time.time;
            windupTriggered = true;
            attackPhase = AttackPhase.WindupActive;
            windupCarryOffset = Mathf.Clamp01(carryFromCurrent);
            maxProgress = windupCarryOffset;
            maxProjectedPx = 0f;
            reverseSwipePeakCmPerSec = 0f;
            reverseAccumulatedPx = 0f;
            reverseIntentHeldSeconds = 0f;
            windupStartTime = Time.time;
            windupStartTimeSet = true;
        }

        if (pendingDir != Dir.None)
        {
            float distProgress = GetProgressAlongDir(pos, swipeStart, pendingDir);
            float projected = GetProjectedAlongDir(pos, swipeStart, pendingDir);
            if (projected > maxProjectedPx) maxProjectedPx = projected;
            float speedProgress = Mathf.Clamp01((lastSwipeSpeed - speedMinCmPerSec) / Mathf.Max(0.01f, speedMaxCmPerSec - speedMinCmPerSec));
            float rawProgress = Mathf.Clamp01(distProgress + speedProgress * Mathf.Clamp01(speedWeightInWindup));
            float carry = Mathf.Clamp01(windupCarryOffset);
            float progress = carry + rawProgress * (1f - carry);
            if (progress > maxProgress)
            {
                maxProgress = progress;
                if (maxProgress >= windupThreshold)
                {
                    // keep using pendingDir for slap window
                }
            }
            animator.SetFloat(handMoveParam, maxProgress);
        }

        lastSwipePos = pos;
        lastSwipeSampleTime = now;
    }

    private void UpdateReverseSwipeTracking(Vector2 frameDelta, float dt)
    {
        if (pendingDir != Dir.None)
        {
            Vector2 axis = GetAxisForDir(pendingDir);
            if (axis != Vector2.zero)
            {
                float reversePx = Mathf.Max(0f, -Vector2.Dot(frameDelta, axis));
                reverseAccumulatedPx += reversePx;
                float reversePxPerSec = reversePx / Mathf.Max(1f / 120f, dt);
                float reverseCmPerSec = PixelsToCm(reversePxPerSec);
                reverseSwipeCurrentCmPerSec = reverseCmPerSec;
                float smoothRate = Mathf.Clamp01(14f * Mathf.Max(1f / 120f, dt));
                reverseSwipeSmoothedCmPerSec = Mathf.Lerp(reverseSwipeSmoothedCmPerSec, reverseCmPerSec, smoothRate);
                if (reverseCmPerSec > reverseSwipePeakCmPerSec) reverseSwipePeakCmPerSec = reverseCmPerSec;
                return;
            }
        }

        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = Mathf.MoveTowards(reverseSwipeSmoothedCmPerSec, 0f, 20f * Mathf.Max(1f / 120f, dt));
        reverseAccumulatedPx = 0f;
    }

    private bool TryTriggerSlapFromReverse(Vector2 pos, Vector2 frameDelta, float dt, float now)
    {
        bool reverseIntent = pendingDir != Dir.None && IsSlapReverseReached(pos, frameDelta);
        if (!alwaysTriggerSlapOnReverseSwipe)
        {
            reverseIntent = reverseIntent && IsReverseSwipeIntentional();
        }

        if (reverseIntent)
        {
            reverseIntentHeldSeconds += Mathf.Max(1f / 120f, dt);
        }
        else
        {
            reverseIntentHeldSeconds = 0f;
        }

        float requiredReverseHold = alwaysTriggerSlapOnReverseSwipe ? 0f : Mathf.Max(0f, minReverseHoldSecondsForSlap);
        if (!(pendingDir != Dir.None && reverseIntent && reverseIntentHeldSeconds >= requiredReverseHold))
        {
            return false;
        }

        if (suppressSlapUntilNextTouch)
        {
            lastSwipePos = pos;
            lastSwipeSampleTime = now;
            return true;
        }

        float currentHand = GetCurrentHandProgressForCombat();
        float effectiveWindup = Mathf.Max(currentHand, maxProgress);
        if (effectiveWindup < minWindupForSlap)
        {
            // Not enough windup: do not consume this swipe.
            // Reset slap attempt data and let current movement become a fresh windup.
            slapPlayedThisSwipe = false;
            pendingDir = Dir.None;
            maxProgress = 0f;
            windupCarryOffset = 0f;
            maxProjectedPx = 0f;
            reverseSwipePeakCmPerSec = 0f;
            reverseSwipeCurrentCmPerSec = 0f;
            reverseSwipeSmoothedCmPerSec = 0f;
            reverseAccumulatedPx = 0f;
            reverseIntentHeldSeconds = 0f;
            windupTriggered = false;
            ClearWindupReturnTracking();
            lastSwipePos = pos;
            lastSwipeSampleTime = now;
            windupStartTimeSet = false;
            return true;
        }

        if (!string.IsNullOrEmpty(currentSlapState))
        {
            Dir attackDir = pendingDir;
            float slapSwipeSpeed = reverseSwipeSmoothedCmPerSec > 0.01f
                ? reverseSwipeSmoothedCmPerSec
                : Mathf.Max(0f, reverseSwipeCurrentCmPerSec);
            if (slapSwipeSpeed <= 0.01f)
            {
                slapSwipeSpeed = Mathf.Max(0f, lastSwipeSpeed);
            }
            SetSlapSpeed(slapSwipeSpeed);
            float slapStart = GetFinishSlapStartNormalizedTime(currentSlapState, currentHand);
            bool isCardinalSlap = IsCardinalSlapState(currentSlapState);
            bool isUpSlap = currentSlapState == slapUpSlap;
            BeginSlapPoseBlend(isCardinalSlap, isUpSlap);

            float fadeSeconds = isUpSlap
                ? upSlapCrossfadeSeconds
                : (isCardinalSlap ? cardinalSlapCrossfadeSeconds : slapStateCrossfadeSeconds);

            if (fadeSeconds <= 0f)
            {
                animator.Play(currentSlapState, 0, slapStart);
            }
            else
            {
                animator.CrossFadeInFixedTime(currentSlapState, fadeSeconds, 0, slapStart, 0f);
            }
            slapPlayedThisSwipe = true;
            attackPhase = AttackPhase.SlapActive;
            inputLockedAfterSlap = true;
            swipeActive = false;
            ClearWindupReturnTracking();
            lastSlapDir = attackDir;
            RaiseSlapFired();
        }

        pendingDir = Dir.None;
        windupStartTimeSet = false;
        windupTriggered = true;
        maxProgress = 0f;
        windupCarryOffset = 0f;
        maxProjectedPx = 0f;
        reverseSwipePeakCmPerSec = 0f;
        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = 0f;
        reverseAccumulatedPx = 0f;
        reverseIntentHeldSeconds = 0f;
        lastSwipePos = pos;
        lastSwipeSampleTime = now;
        return true;
    }

    private bool IsReverseSwipeIntentional()
    {
        // Prevent accidental slap on tiny touch jitter near finger release.
        float reverseSpeed = Mathf.Max(reverseSwipeCurrentCmPerSec, reverseSwipeSmoothedCmPerSec);
        return reverseSpeed >= Mathf.Max(0f, minReverseSwipeSpeedCmPerSec);
    }

    private void HandleSwipeEnd(Vector2 pos)
    {
        if (role == Role.Defender)
        {
            ReleaseDefenderBlockHold();
            if (animator != null) animator.speed = 1f;
            ForceIdlePoseSample();
            lastBlockDir = Dir.None;
            currentBlockState = null;
            defenderBlockHoldNormalized = 0f;
            defenderBlockChosenThisSwipe = false;
            defenderBlockStartTime = 0f;
            return;
        }

        float now = Time.time;
        float dt = Mathf.Max(1f / 120f, now - lastSwipeSampleTime);
        Vector2 frameDelta = pos - lastSwipePos;
        float speedPxPerSec = frameDelta.magnitude / dt;
        lastSwipeSpeed = PixelsToCm(speedPxPerSec);
        if (lastSwipeSpeed > maxSwipeSpeedCmPerSec) maxSwipeSpeedCmPerSec = lastSwipeSpeed;
        UpdateReverseSwipeTracking(frameDelta, dt);

        if (!windupTriggered)
        {
            windupStartTimeSet = false;
            reverseAccumulatedPx = 0f;
            reverseIntentHeldSeconds = 0f;
            return;
        }

        if (TryTriggerSlapFromReverse(pos, frameDelta, dt, now))
        {
            return;
        }

        bool canceledWindup = !slapPlayedThisSwipe && pendingDir != Dir.None;
        if (canceledWindup)
        {
            attackPhase = AttackPhase.ReturningAfterWindup;
            releaseReturnActive = true;
            BeginShortestReturnToIdle();
        }
        lastWindupReleaseTime = now;
        suppressSlapUntilNextTouch = true;
        if (!canceledWindup)
        {
            pendingDir = Dir.None;
            ClearWindupReturnTracking();
        }
        windupTriggered = false;
        maxProjectedPx = 0f;
        reverseSwipePeakCmPerSec = 0f;
        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = 0f;
        reverseAccumulatedPx = 0f;
        reverseIntentHeldSeconds = 0f;
        maxProgress = 0f;
        windupCarryOffset = 0f;
    }

    private void BeginShortestReturnToIdle()
    {
        if (animator == null) return;
        if (!returnToIdleByShortestRoute) return;
        if (string.IsNullOrEmpty(idleStateName)) return;
        int hash = Animator.StringToHash(idleStateName);
        if (!animator.HasState(0, hash)) return;
        float fade = GetWindupReturnCrossfadeSeconds();
        StartWindupReturnTracking(fade);
        animator.CrossFadeInFixedTime(hash, fade, 0, 0f, 0f);
        attackPhase = AttackPhase.ReturningAfterWindup;
    }

    private void TryStartSmoothReturnAfterSlap()
    {
        if (!smoothReturnToIdleAfterSlap) return;
        if (animator == null) return;
        if (role != Role.Attacker) return;

        if (!IsCurrentStateSlap())
        {
            slapReturnTriggered = false;
            return;
        }

        if (swipeActive) return;
        if (slapReturnTriggered) return;
        if (attackPhase != AttackPhase.SlapActive) return;
        float minSafeReturnPoint = Mathf.Max(0.95f, Mathf.Clamp01(slapReturnStartNormalizedTime));
        if (GetSlapProgress01() < minSafeReturnPoint) return;

        int idleHash = Animator.StringToHash(idleStateName);
        if (string.IsNullOrEmpty(idleStateName) || !animator.HasState(0, idleHash)) return;

        float fade = Mathf.Max(0.01f, slapReturnCrossfadeSeconds) * 1.0416666f;
        animator.CrossFadeInFixedTime(idleHash, fade, 0, 0f, 0f);
        attackPhase = AttackPhase.ReturningAfterSlap;
        inputLockedAfterSlap = false;
        slapReturnTriggered = true;
    }

    private void CancelUnintendedSlapAfterRelease()
    {
        if (animator == null) return;
        if (role != Role.Attacker) return;
        if (inputLockedAfterSlap) return; // intentional slap path
        if (slapPlayedThisSwipe) return;  // intentional slap in this swipe
        if (Time.time - lastWindupReleaseTime > 0.25f) return;
        if (!IsCurrentStateSlap()) return;

        if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            animator.CrossFadeInFixedTime(idleStateName, GetWindupReturnCrossfadeSeconds(), 0, 0f, 0f);
        }
        attackPhase = AttackPhase.ReturningAfterWindup;
    }

    private void EnforceSlapSuppression()
    {
        if (!suppressSlapUntilNextTouch) return;
        if (animator == null) return;
        if (!IsCurrentStateSlap()) return;

        if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            animator.CrossFadeInFixedTime(idleStateName, GetWindupReturnCrossfadeSeconds(), 0, 0f, 0f);
        }
        inputLockedAfterSlap = false;
        attackPhase = AttackPhase.ReturningAfterWindup;
    }

    private float GetWindupReturnCrossfadeSeconds()
    {
        // Canceled windup should return directly to idle via the shortest path.
        return Mathf.Max(0.01f, releaseToIdleCrossfadeSeconds) * 12.5f;
    }

    private Dir GetDirection(Vector2 delta)
    {
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        // Equal sectors: 45 degrees each.
        const float sector = 45f;

        float a0 = sector * 0.5f;          // end Right
        float a1 = a0 + sector;            // end UpRight
        float a2 = a1 + sector;            // end Up
        float a3 = a2 + sector;            // end UpLeft
        float a4 = a3 + sector;            // end Left
        float a5 = a4 + sector;            // end DownLeft
        float a6 = a5 + sector;            // end Down
        float a7 = a6 + sector;            // end DownRight (to 360)

        if (angle >= 360f - a0 || angle < a0) return Dir.Right;
        if (angle < a1) return Dir.UpRight;
        if (angle < a2) return Dir.Up;
        if (angle < a3) return Dir.UpLeft;
        if (angle < a4) return Dir.Left;
        if (angle < a5) return Dir.DownLeft;
        if (angle < a6) return Dir.Down;
        if (angle < a7) return Dir.DownRight;
        return Dir.None;
    }

    private Dir Opposite(Dir dir)
    {
        switch (dir)
        {
            case Dir.Up: return Dir.Down;
            case Dir.Down: return Dir.Up;
            case Dir.Left: return Dir.Right;
            case Dir.Right: return Dir.Left;
            case Dir.UpLeft: return Dir.DownRight;
            case Dir.UpRight: return Dir.DownLeft;
            case Dir.DownLeft: return Dir.UpRight;
            case Dir.DownRight: return Dir.UpLeft;
            default: return Dir.None;
        }
    }

    private void EnsureAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }
    }

    private void HoldDefenderBlockState(string stateName, float normalizedTime)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;

        if (!defenderBlockHeld)
        {
            defenderAnimatorSpeedBeforeHold = animator.speed;
            defenderBlockHeld = true;
        }

        animator.speed = Mathf.Max(0.01f, defenderAnimatorSpeedBeforeHold <= 0f ? 1f : defenderAnimatorSpeedBeforeHold);
        float hold01 = Mathf.Clamp01(normalizedTime);
        float maxPoseT = Mathf.Clamp(defenderBlockPoseMaxNormalizedTime, 0.05f, 1f);
        float poseT = hold01 * maxPoseT;
        animator.Play(stateName, 0, poseT);
        animator.Update(0f);
        animator.speed = 0f;
    }

    private void ReleaseDefenderBlockHold()
    {
        if (animator == null) return;
        if (!defenderBlockHeld) return;

        animator.speed = 1f;
        defenderBlockHeld = false;
        defenderBlockHoldNormalized = 0f;
        defenderBlockStartTime = 0f;
        defenderBlockReleaseActive = false;
    }

    private void FindHipsBoneIfNeeded()
    {
        if (hipsBone != null) return;
        if (animator != null)
        {
            hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone != null) return;
        }
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant().Contains("hips"))
            {
                hipsBone = t;
                return;
            }
        }
    }

    private void ApplyStabilityLocks()
    {
        bool stableIdleLocks = ShouldLockLegsNow();
        bool lockInCurrentPhase = !lockLegsOnlyInIdle || stableIdleLocks;
        bool lockBodyDuringSlap = lockBodyDuringAttackPhases &&
                                  (attackPhase == AttackPhase.WindupActive ||
                                   attackPhase == AttackPhase.ReturningAfterWindup ||
                                   attackPhase == AttackPhase.SlapActive ||
                                   attackPhase == AttackPhase.ReturningAfterSlap);
        bool lockBodyDuringDefenderBlock =
            lockDefenderBodyDuringBlock &&
            role == Role.Defender &&
            (defenderBlockHeld || IsCurrentStateBlock());
        bool lockBodyNow = lockInCurrentPhase || lockBodyDuringSlap || lockBodyDuringDefenderBlock;

        if (lockVerticalPosition)
        {
            KeepYStable();
        }
        if (lockCharacterHorizontalPosition && lockBodyNow)
        {
            KeepHorizontalPositionStable();
        }
        bool forceRotationLockDuringWindup = attackPhase == AttackPhase.WindupActive;
        if ((lockCharacterRotation && lockBodyNow) || forceRotationLockDuringWindup)
        {
            KeepRotationStable();
        }
        if (lockHipsVertical)
        {
            KeepHipsYStable();
        }
        if (lockHipsHorizontalPosition && lockBodyNow)
        {
            KeepHipsHorizontalStable();
        }
        if (lockHipsRotation && lockBodyNow)
        {
            KeepHipsRotationStable();
        }
    }

    private void CacheSlapPoseBonesIfNeeded()
    {
        if (slapPoseBones != null && slapPoseBones.Length > 0) return;
        if (animator == null) return;

        slapPoseBones = animator.GetComponentsInChildren<Transform>(true);
        int count = slapPoseBones != null ? slapPoseBones.Length : 0;
        if (count <= 0) return;
        slapPoseStartLocalPositions = new Vector3[count];
        slapPoseStartLocalRotations = new Quaternion[count];
    }

    private void SetupHumanPoseIfPossible()
    {
        // Disabled: this path caused visible artifacts on current clips/controller setup.
        humanPoseAvailable = false;
    }

    private void CacheLowerBodyBonesIfNeeded()
    {
        if (lowerBodyBones != null && lowerBodyBones.Length > 0) return;
        if (animator == null) return;

        var bones = new Transform[]
        {
            animator.GetBoneTransform(HumanBodyBones.Hips),
            animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
            animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
            animator.GetBoneTransform(HumanBodyBones.LeftFoot),
            animator.GetBoneTransform(HumanBodyBones.LeftToes),
            animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
            animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
            animator.GetBoneTransform(HumanBodyBones.RightFoot),
            animator.GetBoneTransform(HumanBodyBones.RightToes),
        };

        int count = 0;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null) count++;
        }
        if (count == 0) return;

        lowerBodyBones = new Transform[count];
        lowerBodyStartLocalRotations = new Quaternion[count];
        int idx = 0;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            lowerBodyBones[idx] = bones[i];
            lowerBodyStartLocalRotations[idx] = bones[i].localRotation;
            idx++;
        }
    }

    private void CacheFeetBonesIfNeeded()
    {
        if (leftFootBone != null || rightFootBone != null) return;
        if (animator == null) return;
        leftFootBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFootBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        leftToesBone = animator.GetBoneTransform(HumanBodyBones.LeftToes);
        rightToesBone = animator.GetBoneTransform(HumanBodyBones.RightToes);
    }

    private void PinFeetStartLocalPosition()
    {
        if (feetStartLocalPosPinned) return;
        CacheFeetBonesIfNeeded();
        if (leftFootBone != null) leftFootStartLocalPos = leftFootBone.localPosition;
        if (rightFootBone != null) rightFootStartLocalPos = rightFootBone.localPosition;
        if (leftToesBone != null) leftToesStartLocalPos = leftToesBone.localPosition;
        if (rightToesBone != null) rightToesStartLocalPos = rightToesBone.localPosition;
        if (leftToesBone != null) leftToesStartWorldPos = leftToesBone.position;
        if (rightToesBone != null) rightToesStartWorldPos = rightToesBone.position;
        if (leftFootBone != null) leftFootStartLocalRot = leftFootBone.localRotation;
        if (rightFootBone != null) rightFootStartLocalRot = rightFootBone.localRotation;
        if (leftToesBone != null) leftToesStartLocalRot = leftToesBone.localRotation;
        if (rightToesBone != null) rightToesStartLocalRot = rightToesBone.localRotation;
        feetStartLocalPosPinned = true;
    }

    private void RepinFeetLocalPoseNow()
    {
        CacheFeetBonesIfNeeded();
        if (leftFootBone != null)
        {
            leftFootStartLocalPos = leftFootBone.localPosition;
            leftFootStartLocalRot = leftFootBone.localRotation;
        }
        if (rightFootBone != null)
        {
            rightFootStartLocalPos = rightFootBone.localPosition;
            rightFootStartLocalRot = rightFootBone.localRotation;
        }
        if (leftToesBone != null)
        {
            leftToesStartLocalPos = leftToesBone.localPosition;
            leftToesStartWorldPos = leftToesBone.position;
            leftToesStartLocalRot = leftToesBone.localRotation;
        }
        if (rightToesBone != null)
        {
            rightToesStartLocalPos = rightToesBone.localPosition;
            rightToesStartWorldPos = rightToesBone.position;
            rightToesStartLocalRot = rightToesBone.localRotation;
        }
        feetStartLocalPosPinned = true;
    }

    private void KeepFeetLocalPositionStable(float blend01 = 1f)
    {
        PinFeetStartLocalPosition();
        float w = Mathf.Clamp01(blend01);
        if (leftFootBone != null) leftFootBone.localPosition = Vector3.Lerp(leftFootBone.localPosition, leftFootStartLocalPos, w);
        if (rightFootBone != null) rightFootBone.localPosition = Vector3.Lerp(rightFootBone.localPosition, rightFootStartLocalPos, w);
    }

    private void KeepFeetLocalRotationStable()
    {
        PinFeetStartLocalPosition();
        if (leftFootBone != null) leftFootBone.localRotation = leftFootStartLocalRot;
        if (rightFootBone != null) rightFootBone.localRotation = rightFootStartLocalRot;
        if (leftToesBone != null) leftToesBone.localRotation = leftToesStartLocalRot;
        if (rightToesBone != null) rightToesBone.localRotation = rightToesStartLocalRot;
    }

    private void KeepToesLocalRotationStable(float blend01 = 1f)
    {
        PinFeetStartLocalPosition();
        float w = Mathf.Clamp01(blend01);
        if (leftToesBone != null) leftToesBone.localRotation = Quaternion.Slerp(leftToesBone.localRotation, leftToesStartLocalRot, w);
        if (rightToesBone != null) rightToesBone.localRotation = Quaternion.Slerp(rightToesBone.localRotation, rightToesStartLocalRot, w);
    }

    private void KeepToesWorldPositionStable(float blend01 = 1f)
    {
        PinFeetStartLocalPosition();
        float w = Mathf.Clamp01(blend01);
        if (leftToesBone != null) leftToesBone.position = Vector3.Lerp(leftToesBone.position, leftToesStartWorldPos, w);
        if (rightToesBone != null) rightToesBone.position = Vector3.Lerp(rightToesBone.position, rightToesStartWorldPos, w);
    }

    private void CacheAttackHandBonesIfNeeded()
    {
        if (attackLeftHandBone != null && attackRightHandBone != null) return;
        if (animator == null) return;

        if (attackLeftHandBone == null) attackLeftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (attackRightHandBone == null) attackRightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void PinAttackHandStartIfNeeded()
    {
        if (attackHandStartPinned) return;
        CacheAttackHandBonesIfNeeded();
        if (attackLeftHandBone != null) attackLeftHandStartLocalPos = attackLeftHandBone.localPosition;
        if (attackRightHandBone != null) attackRightHandStartLocalPos = attackRightHandBone.localPosition;
        if (attackLeftHandBone != null) attackLeftHandStartLocalRot = attackLeftHandBone.localRotation;
        if (attackRightHandBone != null) attackRightHandStartLocalRot = attackRightHandBone.localRotation;
        attackHandStartPinned = true;
    }

    private bool TryDetermineMovedAttackHand(out AttackHand hand)
    {
        hand = AttackHand.Both;
        CacheAttackHandBonesIfNeeded();
        PinAttackHandStartIfNeeded();

        float leftMove = 0f;
        float rightMove = 0f;
        if (attackLeftHandBone != null)
        {
            float pos = (attackLeftHandBone.localPosition - attackLeftHandStartLocalPos).sqrMagnitude;
            float rot = Quaternion.Angle(attackLeftHandBone.localRotation, attackLeftHandStartLocalRot) / 180f;
            leftMove = pos + (rot * rot * 0.05f);
        }
        if (attackRightHandBone != null)
        {
            float pos = (attackRightHandBone.localPosition - attackRightHandStartLocalPos).sqrMagnitude;
            float rot = Quaternion.Angle(attackRightHandBone.localRotation, attackRightHandStartLocalRot) / 180f;
            rightMove = pos + (rot * rot * 0.05f);
        }

        const float minMove = 0.00002f;
        if (leftMove < minMove && rightMove < minMove)
        {
            return false;
        }
        if (leftMove > rightMove * 1.15f)
        {
            hand = AttackHand.Left;
            return true;
        }
        if (rightMove > leftMove * 1.15f)
        {
            hand = AttackHand.Right;
            return true;
        }

        hand = leftMove >= rightMove ? AttackHand.Left : AttackHand.Right;
        return true;
    }

    private AttackHand DetermineActiveAttackHand()
    {
        if (TryDetermineMovedAttackHand(out var hand))
        {
            return hand;
        }
        return GetPreferredAttackHand();
    }

    private AttackHand GetPreferredAttackHand()
    {
        if (preferredCombatHand == SlapDirection.Right) return AttackHand.Right;
        return AttackHand.Left;
    }

    private void KeepLowerBodyRotationStable(float blend01 = 1f)
    {
        CacheLowerBodyBonesIfNeeded();
        if (lowerBodyBones == null || lowerBodyStartLocalRotations == null) return;

        int count = Mathf.Min(lowerBodyBones.Length, lowerBodyStartLocalRotations.Length);
        float blend = Mathf.Clamp01(blend01);
        float smoothT = 1f - Mathf.Exp(-Mathf.Max(0.01f, kneeLockSmoothing) * Time.deltaTime);
        for (int i = 0; i < count; i++)
        {
            var b = lowerBodyBones[i];
            if (b == null) continue;
            Quaternion target = lowerBodyStartLocalRotations[i];
            if (hardLockShins && IsShinBoneForLock(b))
            {
                b.localRotation = Quaternion.Slerp(b.localRotation, target, blend);
                continue;
            }
            if (smoothKneeLock && IsKneeOrLegBoneForLock(b))
            {
                b.localRotation = Quaternion.Slerp(b.localRotation, target, smoothT * blend);
            }
            else
            {
                b.localRotation = Quaternion.Slerp(b.localRotation, target, blend);
            }
        }
    }

    private static bool IsKneeOrLegBoneForLock(Transform bone)
    {
        if (bone == null) return false;
        string n = bone.name.ToLowerInvariant();
        return n.Contains("upperleg") ||
               n.Contains("lowerleg") ||
               n.Contains("thigh") ||
               n.Contains("calf") ||
               n.Contains("shin") ||
               n.Contains("knee");
    }

    private static bool IsShinBoneForLock(Transform bone)
    {
        if (bone == null) return false;
        string n = bone.name.ToLowerInvariant();
        return n.Contains("lowerleg") ||
               n.Contains("calf") ||
               n.Contains("shin");
    }

    private void PinStartPosition()
    {
        if (startPositionPinned) return;
        startPosition = transform.position;
        startPositionPinned = true;
    }

    private void PinStartY()
    {
        if (startYPinned) return;
        startY = transform.position.y;
        startYPinned = true;
    }

    private void PinStartRotation()
    {
        if (startRotationPinned) return;
        startRotation = transform.rotation;
        startRotationPinned = true;
    }

    private void KeepYStable()
    {
        PinStartY();
        var p = transform.position;
        if (!Mathf.Approximately(p.y, startY))
        {
            p.y = startY;
            transform.position = p;
        }
    }

    private void KeepHorizontalPositionStable()
    {
        PinStartPosition();
        var p = transform.position;
        if (!Mathf.Approximately(p.x, startPosition.x) || !Mathf.Approximately(p.z, startPosition.z))
        {
            p.x = startPosition.x;
            p.z = startPosition.z;
            transform.position = p;
        }
    }

    private void KeepRotationStable()
    {
        PinStartRotation();
        if (transform.rotation != startRotation)
        {
            transform.rotation = startRotation;
        }
    }

    private void PinHipsStartY()
    {
        if (hipsStartPinned) return;
        if (hipsBone == null) return;
        hipsStartLocalY = hipsBone.localPosition.y;
        hipsStartPinned = true;
    }

    private void PinHipsStartPosition()
    {
        if (hipsPositionPinned) return;
        if (hipsBone == null) return;
        hipsStartLocalPosition = hipsBone.localPosition;
        hipsPositionPinned = true;
    }

    private void PinHipsStartRotation()
    {
        if (hipsRotationPinned) return;
        if (hipsBone == null) return;
        hipsStartLocalRotation = hipsBone.localRotation;
        hipsRotationPinned = true;
    }

    private void KeepHipsYStable()
    {
        if (hipsBone == null) return;
        PinHipsStartY();
        var p = hipsBone.localPosition;
        if (!Mathf.Approximately(p.y, hipsStartLocalY))
        {
            p.y = hipsStartLocalY;
            hipsBone.localPosition = p;
        }
    }

    private void KeepHipsHorizontalStable()
    {
        if (hipsBone == null) return;
        PinHipsStartPosition();
        var p = hipsBone.localPosition;
        if (!Mathf.Approximately(p.x, hipsStartLocalPosition.x) || !Mathf.Approximately(p.z, hipsStartLocalPosition.z))
        {
            p.x = hipsStartLocalPosition.x;
            p.z = hipsStartLocalPosition.z;
            hipsBone.localPosition = p;
        }
    }

    private void KeepHipsRotationStable()
    {
        if (hipsBone == null) return;
        PinHipsStartRotation();
        if (hipsBone.localRotation != hipsStartLocalRotation)
        {
            hipsBone.localRotation = hipsStartLocalRotation;
        }
    }

    private void FindIdleBreathingBoneIfNeeded()
    {
        if (idleBreathingBone != null) return;

        if (animator != null)
        {
            idleBreathingBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (idleBreathingBone == null) idleBreathingBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (idleBreathingBone == null) idleBreathingBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (idleBreathingBone != null) return;
        }

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("upperchest") || n.Contains("chest") || n.Contains("spine2") || n.Contains("spine_02"))
            {
                idleBreathingBone = t;
                return;
            }
        }
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("spine"))
            {
                idleBreathingBone = t;
                return;
            }
        }
    }

    private bool IsBreathingIdleState()
    {
        if (animator == null) return false;
        if (!IsAnimatorInIdleState()) return false;
        bool touchPrimingOnly = IsTouchPrimingOnly();
        if (swipeActive && !touchPrimingOnly) return false;
        if (windupTriggered || inputLockedAfterSlap) return false;
        if (pendingDir != Dir.None) return false;
        if (IsSlapAnimating()) return false;
        if (role == Role.Defender && IsDefenderBlocking()) return false;
        return true;
    }

    private bool IsTouchPrimingOnly()
    {
        return swipeActive &&
               !windupTriggered &&
               pendingDir == Dir.None &&
               attackPhase == AttackPhase.Idle;
    }

    private bool IsAnimatorInIdleState()
    {
        if (animator == null) return false;
        if (string.IsNullOrEmpty(idleStateName)) return false;
        int idleHash = Animator.StringToHash(idleStateName);

        var current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.fullPathHash == idleHash || current.shortNameHash == idleHash) return true;

        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            if (next.fullPathHash == idleHash || next.shortNameHash == idleHash) return true;
        }
        return false;
    }

    private void ApplyIdleBreathing()
    {
        FindIdleBreathingBoneIfNeeded();
        if (idleBreathingBone == null) return;

        if (Mathf.Abs(idleBreathingLastOffsetY) > 0.000001f)
        {
            var resetPos = idleBreathingBone.localPosition;
            resetPos.y -= idleBreathingLastOffsetY;
            idleBreathingBone.localPosition = resetPos;
            idleBreathingLastOffsetY = 0f;
        }
    }

    private void UpdateRequiredPixels()
    {
        float dpi = Screen.dpi;
        if (dpi <= 0f) dpi = fallbackDpi;
        requiredPixels = swipeDistanceCm * (dpi / 2.54f);
        if (requiredPixels <= 0f) requiredPixels = 1f;
        requiredBlockPixels = blockSwipeDistanceCm * (dpi / 2.54f);
        if (requiredBlockPixels <= 0f) requiredBlockPixels = 1f;
    }

    private float GetDeadzonePx()
    {
        float dpi = Screen.dpi;
        if (dpi <= 0f) dpi = fallbackDpi;
        return Mathf.Max(swipeDeadzonePx, 2f, dpi * 0.02f);
    }

    private float GetProgressAlongDir(Vector2 pos, Vector2 start, Dir dir, bool useBlockDistance = false)
    {
        Vector2 d = pos - start;
        Vector2 axis = GetAxisForDir(dir);
        if (axis == Vector2.zero) return 0f;
        float dist = Vector2.Dot(d, axis);
        float denominator = useBlockDistance ? requiredBlockPixels : requiredPixels;
        return Mathf.Clamp01(dist / Mathf.Max(1f, denominator));
    }

    private float GetProjectedAlongDir(Vector2 pos, Vector2 start, Dir dir)
    {
        Vector2 axis = GetAxisForDir(dir);
        if (axis == Vector2.zero) return 0f;
        return Vector2.Dot(pos - start, axis);
    }

    private Vector2 GetAxisForDir(Dir dir)
    {
        return dir switch
        {
            Dir.Up => Vector2.up,
            Dir.Down => Vector2.down,
            Dir.Left => Vector2.left,
            Dir.Right => Vector2.right,
            Dir.UpLeft => new Vector2(-1f, 1f).normalized,
            Dir.UpRight => new Vector2(1f, 1f).normalized,
            Dir.DownLeft => new Vector2(-1f, -1f).normalized,
            Dir.DownRight => new Vector2(1f, -1f).normalized,
            _ => Vector2.zero
        };
    }

    private bool IsSlapReverseReached(Vector2 pos, Vector2 frameDelta)
    {
        if (pendingDir == Dir.None) return false;
        Vector2 axis = GetAxisForDir(pendingDir);
        if (axis == Vector2.zero) return false;

        float reverseThresholdPx = requiredPixels / Mathf.Max(1f, slapReverseDistanceFactor);
        float dpi = Screen.dpi;
        if (dpi <= 0f) dpi = fallbackDpi;
        float minReversePx = Mathf.Max(1f, Mathf.Max(0.01f, minReverseSwipeDistanceCm) * (dpi / 2.54f));
        reverseThresholdPx = Mathf.Max(reverseThresholdPx, minReversePx);
        float currentProjected = Vector2.Dot(pos - swipeStart, axis);
        float reverseDistance = maxProjectedPx - currentProjected;
        float localMoveAlongWindup = Vector2.Dot(frameDelta, axis);
        bool movingBack = localMoveAlongWindup < 0f;

        bool reachedByProjection = movingBack && reverseDistance >= reverseThresholdPx;
        bool reachedByAccumulated = reverseAccumulatedPx >= reverseThresholdPx;
        return reachedByProjection || reachedByAccumulated;
    }

    private float SmoothToZero(float value)
    {
        float smoothTime = Mathf.Max(0.001f, releaseDuration);
        return Mathf.SmoothDamp(value, 0f, ref handVelocity, smoothTime);
    }

    private void StartWindupReturnTracking(float durationSeconds)
    {
        float current = 0f;
        if (animator != null && !string.IsNullOrEmpty(handMoveParam))
        {
            current = animator.GetFloat(handMoveParam);
        }

        windupReturnStart01 = Mathf.Clamp01(Mathf.Max(current, maxProgress));
        windupReturnStartTime = Time.time;
        windupReturnDuration = Mathf.Max(0.01f, durationSeconds);
        windupReturnTrackingActive = windupReturnStart01 > 0.0001f;
    }

    private void ClearWindupReturnTracking()
    {
        windupReturnTrackingActive = false;
        windupReturnStartTime = 0f;
        windupReturnDuration = 0f;
        windupReturnStart01 = 0f;
    }

    private float GetCurrentHandProgressForCombat()
    {
        float param = 0f;
        if (animator != null && !string.IsNullOrEmpty(handMoveParam))
        {
            param = animator.GetFloat(handMoveParam);
        }

        bool neutralIdle =
            attackPhase == AttackPhase.Idle &&
            pendingDir == Dir.None &&
            !windupTriggered &&
            !inputLockedAfterSlap;
        if (neutralIdle)
        {
            return 0f;
        }

        float fallback = Mathf.Clamp01(Mathf.Max(param, maxProgress));
        bool useTrackedReturn =
            windupReturnTrackingActive &&
            attackPhase == AttackPhase.ReturningAfterWindup &&
            !windupTriggered &&
            !inputLockedAfterSlap;

        if (!useTrackedReturn)
        {
            return fallback;
        }

        float t = Mathf.Clamp01((Time.time - windupReturnStartTime) / Mathf.Max(0.01f, windupReturnDuration));
        float tracked = Mathf.Lerp(windupReturnStart01, 0f, t);
        return Mathf.Clamp01(tracked);
    }

    private void SetWindupStates(Dir dir)
    {
        currentWindupState = null;
        currentSlapState = null;
        switch (dir)
        {
            case Dir.UpLeft:
                currentWindupState = ResolveWindupStateName(slap3Windup, "Slap3_Windup");
                currentSlapState = ResolveSlapStateName(slap3Slap, "Slap3_Slap");
                break;
            case Dir.UpRight:
                currentWindupState = ResolveWindupStateName(slap1Windup, "Slap1_Windup");
                currentSlapState = ResolveSlapStateName(slap1Slap, "Slap1_Slap");
                break;
            case Dir.DownLeft:
                currentWindupState = ResolveWindupStateName(slap9Windup, "Slap9_Windup");
                currentSlapState = ResolveSlapStateName(slap9Slap, "Slap9_Slap");
                break;
            case Dir.DownRight:
                currentWindupState = ResolveWindupStateName(slap7Windup, "Slap7_Windup");
                currentSlapState = ResolveSlapStateName(slap7Slap, "Slap7_Slap");
                break;
            case Dir.Left:
                currentWindupState = ResolveWindupStateName(slapLeftWindup, "SlapLeft_Windup");
                currentSlapState = ResolveSlapStateName(slapLeftSlap, "SlapLeft_Slap");
                break;
            case Dir.Right:
                currentWindupState = ResolveWindupStateName(slapRightWindup, "SlapRight_Windup");
                currentSlapState = ResolveSlapStateName(slapRightSlap, "SlapRight_Slap");
                break;
            case Dir.Up:
                currentWindupState = ResolveWindupStateName(slapUpWindup, "SlapUp_Windup");
                currentSlapState = ResolveSlapStateName(slapUpSlap, "SlapUp_Slap");
                break;
            case Dir.Down:
                currentWindupState = ResolveWindupStateName(slapErcutWindup, "SlaperCut_Windup");
                currentSlapState = ResolveSlapStateName(slapErcutSlap, "SlaperCut_Slap");
                break;
        }

        EnsureValidAttackStates(dir);
    }

    private void EnsureValidAttackStates(Dir dir)
    {
        if (IsValidWindupStateName(currentWindupState) && IsValidSlapStateName(currentSlapState))
        {
            return;
        }

        switch (dir)
        {
            case Dir.Left:
                currentWindupState = slapRightWindup;
                currentSlapState = slapRightSlap;
                break;
            case Dir.Right:
                currentWindupState = slapLeftWindup;
                currentSlapState = slapLeftSlap;
                break;
            case Dir.UpLeft:
                currentWindupState = slap1Windup;
                currentSlapState = slap1Slap;
                break;
            case Dir.UpRight:
                currentWindupState = slap3Windup;
                currentSlapState = slap3Slap;
                break;
            case Dir.DownLeft:
                currentWindupState = slap7Windup;
                currentSlapState = slap7Slap;
                break;
            case Dir.DownRight:
                currentWindupState = slap9Windup;
                currentSlapState = slap9Slap;
                break;
            case Dir.Up:
                currentWindupState = slap1Windup;
                currentSlapState = slap1Slap;
                break;
            case Dir.Down:
                currentWindupState = slap9Windup;
                currentSlapState = slap9Slap;
                break;
        }

        if (IsValidWindupStateName(currentWindupState) && IsValidSlapStateName(currentSlapState))
        {
            return;
        }

        currentWindupState = slap1Windup;
        currentSlapState = slap1Slap;
    }

    private bool IsValidWindupStateName(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName)) return false;
        if (stateName.StartsWith("Block", StringComparison.OrdinalIgnoreCase)) return false;
        if (!stateName.Contains("_Windup")) return false;
        if (animator == null) return true;
        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    private bool IsValidSlapStateName(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName)) return false;
        if (stateName.StartsWith("Block", StringComparison.OrdinalIgnoreCase)) return false;
        if (!stateName.Contains("_Slap")) return false;
        if (animator == null) return true;
        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    private string ResolveWindupStateName(string configuredName, string fallbackName)
    {
        if (IsValidWindupStateName(configuredName)) return configuredName;
        if (IsValidWindupStateName(fallbackName)) return fallbackName;
        return configuredName;
    }

    private string ResolveSlapStateName(string configuredName, string fallbackName)
    {
        if (IsValidSlapStateName(configuredName)) return configuredName;
        if (IsValidSlapStateName(fallbackName)) return fallbackName;
        return configuredName;
    }

    private void SetBlockState(Dir dir)
    {
        currentBlockState = null;
        switch (dir)
        {
            case Dir.UpLeft:
                currentBlockState = block3Block;
                break;
            case Dir.UpRight:
                currentBlockState = block1Block;
                break;
            case Dir.DownLeft:
                currentBlockState = block9Block;
                break;
            case Dir.DownRight:
                currentBlockState = block7Block;
                break;
            case Dir.Left:
                currentBlockState = blockLeftBlock;
                break;
            case Dir.Right:
                currentBlockState = blockRightBlock;
                break;
            case Dir.Up:
                currentBlockState = blockUpBlock;
                break;
            case Dir.Down:
                currentBlockState = blockErcutBlock;
                break;
        }
    }

    private void SetSlapSpeed(float swipeSpeed)
    {
        if (animator == null || string.IsNullOrEmpty(slapSpeedParam)) return;

        // 1..100 swipe power model:
        // fastest swipe = 100% speed, slowest swipe = 1% speed.
        float adjustedSwipeSpeed = Mathf.Max(0f, swipeSpeed) * Mathf.Max(0.01f, swipeSpeedResponseMultiplier);
        float swipeNormalized = Mathf.Clamp01((adjustedSwipeSpeed - speedMinCmPerSec) / Mathf.Max(0.01f, speedMaxCmPerSec - speedMinCmPerSec));
        float power01 = Mathf.Clamp(swipeNormalized, 0.01f, 1f);

        // Animation speed follows the same percent directly.
        float speed = Mathf.Max(0.01f, slapSpeedMax) * power01;
        debugLastSlapPower01 = power01;
        animator.SetFloat(slapSpeedParam, speed);
    }

    private float GetStateMinPlayableSpeed(string slapStateName)
    {
        if (slapStateName == slapLeftSlap || slapStateName == slapRightSlap)
        {
            return Mathf.Max(0.01f, sideSlapMinPlayableSpeed);
        }
        if (slapStateName == slapErcutSlap)
        {
            return Mathf.Max(0.01f, uppercutSlapMinPlayableSpeed);
        }
        return Mathf.Max(0.01f, minPlayableSlapSpeed);
    }

    private float GetStateSpeedMultiplier(string slapStateName)
    {
        if (slapStateName == slapLeftSlap || slapStateName == slapRightSlap)
        {
            return Mathf.Max(0.01f, sideSlapSpeedMultiplier);
        }
        if (slapStateName == slapUpSlap)
        {
            return Mathf.Max(0.01f, upSlapSpeedMultiplier);
        }
        if (slapStateName == slapErcutSlap)
        {
            return Mathf.Max(0.01f, uppercutSlapSpeedMultiplier);
        }
        return Mathf.Max(0.01f, diagonalSlapSpeedMultiplier);
    }

    public float GetDebugWindup01()
    {
        return GetCurrentHandProgressForCombat();
    }

    public float GetDebugSlapPower01()
    {
        return Mathf.Clamp01(debugLastSlapPower01);
    }

    public Role GetRole()
    {
        return role;
    }

    public void SetHandsOnlyVisualRuntime(bool enabled)
    {
        handsOnlyVisual = enabled;
        if (!allowGeneratedHandOnlyMeshes)
        {
            RemoveGeneratedHandOnlyMeshesIfPresent();
        }
        if (handsOnlyVisual)
        {
            handsOnlyApplied = false;
            ApplyHandsOnlyVisualIfNeeded();
            // If mesh data is not readable and no hand-only renderers were built,
            // fallback to strict hide to avoid body/shoulder artifacts in rear-camera mode.
            if (generatedLeftHandRenderer == null || generatedRightHandRenderer == null)
            {
                ApplyHandsOnlyNameFallbackVisibility();
                if (leftShoulderCapRenderer != null) leftShoulderCapRenderer.enabled = false;
                if (rightShoulderCapRenderer != null) rightShoulderCapRenderer.enabled = false;
                return;
            }
            UpdateHandsOnlyCombatVisibility();
            // Camera-driven rear view: keep shoulder caps hidden to avoid visible plugs/artifacts.
            if (leftShoulderCapRenderer != null) leftShoulderCapRenderer.enabled = false;
            if (rightShoulderCapRenderer != null) rightShoulderCapRenderer.enabled = false;
            return;
        }

        RestoreFullBodyVisibilityRuntime();
    }

    private void RestoreFullBodyVisibilityRuntime()
    {
        // Restore full character rendering when hands-only mode is disabled.
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r == generatedLeftHandRenderer || r == generatedRightHandRenderer ||
                r == leftShoulderCapRenderer || r == rightShoulderCapRenderer)
            {
                r.enabled = false;
                continue;
            }
            r.enabled = true;
        }
        if (leftShoulderCapRenderer != null) leftShoulderCapRenderer.enabled = false;
        if (rightShoulderCapRenderer != null) rightShoulderCapRenderer.enabled = false;
        activeAttackHand = AttackHand.Both;
    }

    private void ApplyHandsOnlyNameFallbackVisibility()
    {
        bool anyHandShown = false;
        Transform leftHandBone = null;
        Transform rightHandBone = null;
        if (animator != null)
        {
            leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r == generatedLeftHandRenderer || r == generatedRightHandRenderer ||
                r == leftShoulderCapRenderer || r == rightShoulderCapRenderer)
            {
                r.enabled = false;
                continue;
            }
            string n = r.gameObject.name;
            bool show = IsHandLikeName(n);
            if (!show)
            {
                var t = r.transform;
                if (leftHandBone != null && t != null && t.IsChildOf(leftHandBone)) show = true;
                if (!show && rightHandBone != null && t != null && t.IsChildOf(rightHandBone)) show = true;
            }
            r.enabled = show;
            if (show) anyHandShown = true;
        }

        // If this rig cannot isolate hands at all, fall back to full body to avoid invisible character.
        if (!anyHandShown)
        {
            RestoreFullBodyVisibilityRuntime();
        }
    }

    private void ForceIdlePoseSample()
    {
        if (animator == null) return;
        ClearWindupReturnTracking();

        if (!string.IsNullOrEmpty(handMoveParam))
        {
            animator.SetFloat(handMoveParam, 0f);
        }

        if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            animator.Play(idleStateName, 0, 0f);
            // Force-sample idle immediately to remove residual transition blending.
            animator.Update(0f);
        }
    }

    public void SetRole(Role newRole)
    {
        if (role == newRole) return;
        role = newRole;
        ClearWindupReturnTracking();
        aiBlockReacquireBlockedUntilTime = -999f;
        slapReturnTriggered = false;
        aiHardBlockLock = false;
        aiHardBlockDir = Dir.None;
        repinBodyOnNextStableIdle = repinBodyOnRoleSwitch;
        if (role == Role.Attacker)
        {
            // Ensure defender block does not keep animator frozen when switching to attacker.
            ReleaseDefenderBlockHold();
            lastBlockDir = Dir.None;
            currentBlockState = null;
            defenderBlockHoldNormalized = 0f;
            defenderBlockChosenThisSwipe = false;
            defenderBlockStartTime = 0f;

            // Reset attack pipeline so first swipe after role switch always starts with a clean windup.
            pendingDir = Dir.None;
            windupTriggered = false;
            swipeActive = false;
            slapPlayedThisSwipe = false;
            inputLockedAfterSlap = false;
            maxProgress = 0f;
            windupCarryOffset = 0f;
            maxProjectedPx = 0f;
            reverseSwipePeakCmPerSec = 0f;
            reverseSwipeCurrentCmPerSec = 0f;
            reverseSwipeSmoothedCmPerSec = 0f;
            reverseAccumulatedPx = 0f;
            reverseIntentHeldSeconds = 0f;
            windupStartTimeSet = false;
            attackPhase = AttackPhase.Idle;
            BlendToIdlePose(0.12f);
        }
        else
        {
            // Ensure defender can block immediately after role switch.
            inputLockedAfterSlap = false;
            pendingDir = Dir.None;
            windupTriggered = false;
            swipeActive = false;
            slapPlayedThisSwipe = false;
            reverseSwipePeakCmPerSec = 0f;
            reverseSwipeCurrentCmPerSec = 0f;
            reverseSwipeSmoothedCmPerSec = 0f;
            reverseAccumulatedPx = 0f;
            reverseIntentHeldSeconds = 0f;
            // Do not force idle blend here: it creates a visible one-frame calm-pose dip
            // before defender block is applied.
        }
        // Prevent color flipping on residual aura when roles swap.
        auraWindupSmoothed = 0f;
        auraFlashTimer = 0f;
        auraWasSlapActive = false;
    }

    public float PlayOneShotIntro(string stateOrClipName, float fallbackSeconds = 1.2f, float crossfadeSeconds = 0.08f, bool lockShoulders = false, bool lockBodyExceptHead = false)
    {
        EnsureAnimator();
        float fallback = Mathf.Max(0.01f, fallbackSeconds);
        if (animator == null) return fallback;
        if (string.IsNullOrWhiteSpace(stateOrClipName)) return fallback;
        animator.speed = 1f;

        StopIdleBreathingPlayableGraph();
        StopIntroPlayableGraph();
        CacheIntroRigRootLock();
        CacheIntroBodyExceptHeadLock(lockBodyExceptHead);
        CacheIntroShoulderLock(lockShoulders);
        int stateHash = Animator.StringToHash(stateOrClipName);
        if (animator.HasState(0, stateHash))
        {
            float fade = Mathf.Max(0f, crossfadeSeconds);
            if (fade <= 0f) animator.Play(stateOrClipName, 0, 0f);
            else animator.CrossFadeInFixedTime(stateOrClipName, fade, 0, 0f, 0f);
            animator.Update(0f);
            float stateLen = Mathf.Max(0.01f, fallback);
            var st = animator.GetCurrentAnimatorStateInfo(0);
            if (StateMatches(st, stateOrClipName))
            {
                stateLen = Mathf.Max(0.01f, st.length);
            }
            else if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (StateMatches(next, stateOrClipName))
                {
                    stateLen = Mathf.Max(0.01f, next.length);
                }
            }
            introPlayableGraphActive = true;
            introReturnToIdleAtTime = Time.time + stateLen;
            return stateLen;
        }

        var clip = FindClipByName(stateOrClipName);
        if (clip == null) return fallback;
        return PlayOneShotIntro(clip, fallback, lockShoulders, lockBodyExceptHead);
    }

    public float PlayOneShotIntro(AnimationClip clip, float fallbackSeconds = 1.2f, bool lockShoulders = false, bool lockBodyExceptHead = false, float playbackSpeed = 1f)
    {
        EnsureAnimator();
        float fallback = Mathf.Max(0.01f, fallbackSeconds);
        if (animator == null) return fallback;
        if (clip == null) return fallback;
        float introSpeed = Mathf.Max(0.01f, playbackSpeed);
        animator.speed = 1f;

        StopIdleBreathingPlayableGraph();
        StopIntroPlayableGraph();
        CacheIntroRigRootLock();
        CacheIntroBodyExceptHeadLock(lockBodyExceptHead);
        CacheIntroShoulderLock(lockShoulders);
        introOriginalController = animator.runtimeAnimatorController;
        if (introOriginalController == null) return fallback;

        introPlayableGraph = PlayableGraph.Create(name + "_IntroOneShot");
        introPlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(introPlayableGraph, "IntroOutput", animator);

        introMixerPlayable = AnimationMixerPlayable.Create(introPlayableGraph, 2);
        introControllerPlayable = AnimatorControllerPlayable.Create(introPlayableGraph, introOriginalController);
        introClipPlayable = AnimationClipPlayable.Create(introPlayableGraph, clip);
        introClipPlayable.SetTime(0d);
        introClipPlayable.SetSpeed(introSpeed);

        introPlayableGraph.Connect(introControllerPlayable, 0, introMixerPlayable, 0);
        introPlayableGraph.Connect(introClipPlayable, 0, introMixerPlayable, 1);
        introMixerPlayable.SetInputWeight(0, 0f);
        introMixerPlayable.SetInputWeight(1, 1f);
        output.SetSourcePlayable(introMixerPlayable);

        introPlayableGraph.Play();
        introPlayableGraphActive = true;
        introGraphBlendActive = false;
        introGraphBlendStartTime = -1f;
        introGraphBlendDuration = 0f;
        float clipLen = Mathf.Max(0.01f, clip.length / introSpeed);
        introReturnToIdleAtTime = Time.time + clipLen;
        return clipLen;
    }

    public float GetIntroBlendDuration()
    {
        return Mathf.Max(0.01f, introToIdleBlendSeconds);
    }

    public float GetIntroRemainingSeconds()
    {
        float remainClip = Mathf.Max(0f, introReturnToIdleAtTime - Time.time);
        float remainBlend = Mathf.Max(0f, introBlendOutUntilTime - Time.time);
        return Mathf.Max(remainClip, remainBlend);
    }

    public SlapDirection GetLastSlapDirection()
    {
        return ToPublicDir(lastSlapDir);
    }

    public SlapDirection GetCurrentBlockDirection()
    {
        return ToPublicDir(lastBlockDir);
    }

    public float GetDefenderBlockHold01()
    {
        return Mathf.Clamp01(defenderBlockHoldNormalized);
    }

    public float GetDefenderBlockHoldSeconds()
    {
        if (!defenderBlockHeld || defenderBlockStartTime <= 0f) return 0f;
        return Mathf.Max(0f, Time.time - defenderBlockStartTime);
    }

    public SlapDirection GetPendingDirection()
    {
        return ToPublicDir(pendingDir);
    }

    public bool TryGetHandWorldPose(
        out Vector3 leftPosition,
        out Quaternion leftRotation,
        out Vector3 rightPosition,
        out Quaternion rightRotation)
    {
        leftPosition = Vector3.zero;
        leftRotation = Quaternion.identity;
        rightPosition = Vector3.zero;
        rightRotation = Quaternion.identity;

        CacheAttackHandBonesIfNeeded();
        bool hasAny = false;
        if (attackLeftHandBone != null)
        {
            leftPosition = attackLeftHandBone.position;
            leftRotation = attackLeftHandBone.rotation;
            hasAny = true;
        }
        if (attackRightHandBone != null)
        {
            rightPosition = attackRightHandBone.position;
            rightRotation = attackRightHandBone.rotation;
            hasAny = true;
        }
        return hasAny;
    }

    public void GetHandMotionMagnitude(out float leftMotion, out float rightMotion)
    {
        leftMotion = 0f;
        rightMotion = 0f;

        CacheAttackHandBonesIfNeeded();
        PinAttackHandStartIfNeeded();

        if (attackLeftHandBone != null)
        {
            float pos = (attackLeftHandBone.localPosition - attackLeftHandStartLocalPos).sqrMagnitude;
            float rot = Quaternion.Angle(attackLeftHandBone.localRotation, attackLeftHandStartLocalRot) / 180f;
            leftMotion = pos + (rot * rot * 0.05f);
        }

        if (attackRightHandBone != null)
        {
            float pos = (attackRightHandBone.localPosition - attackRightHandStartLocalPos).sqrMagnitude;
            float rot = Quaternion.Angle(attackRightHandBone.localRotation, attackRightHandStartLocalRot) / 180f;
            rightMotion = pos + (rot * rot * 0.05f);
        }
    }

    public bool IsControlledHandMoving()
    {
        if (role == Role.Attacker)
        {
            return swipeActive ||
                   windupTriggered ||
                   pendingDir != Dir.None ||
                   attackPhase == AttackPhase.WindupActive ||
                   attackPhase == AttackPhase.SlapActive ||
                   attackPhase == AttackPhase.ReturningAfterWindup ||
                   IsCurrentStateSlap();
        }

        if (role == Role.Defender)
        {
            return IsDefenderBlocking() || IsDefenderBlockReleasing();
        }

        return false;
    }

    public VisualHand GetControlledVisualHand()
    {
        if (!IsControlledHandMoving()) return VisualHand.None;

        if (role == Role.Attacker)
        {
            AttackHand hand = AttackHand.Both;

            if (swipeAttackHandLocked && (swipeAttackHand == AttackHand.Left || swipeAttackHand == AttackHand.Right))
            {
                hand = swipeAttackHand;
            }
            else if (activeAttackHand == AttackHand.Left || activeAttackHand == AttackHand.Right)
            {
                hand = activeAttackHand;
            }
            else if (TryResolveHorizontalAttackHand(out var horizontalHand) && horizontalHand != AttackHand.Both)
            {
                hand = horizontalHand;
            }
            else if (!TryDetermineMovedAttackHand(out hand) || hand == AttackHand.Both)
            {
                hand = GetPreferredAttackHand();
            }

            return hand == AttackHand.Right ? VisualHand.Right : VisualHand.Left;
        }

        if (role == Role.Defender)
        {
            return lastBlockDir switch
            {
                Dir.Left => VisualHand.Left,
                Dir.UpLeft => VisualHand.Left,
                Dir.DownLeft => VisualHand.Left,
                Dir.Right => VisualHand.Right,
                Dir.UpRight => VisualHand.Right,
                Dir.DownRight => VisualHand.Right,
                _ => GetPreferredAttackHand() == AttackHand.Right ? VisualHand.Right : VisualHand.Left
            };
        }

        return VisualHand.None;
    }

    public bool IsDownAttackVisualActive()
    {
        if (role != Role.Attacker) return false;
        if (currentWindupState == slapErcutWindup || currentSlapState == slapErcutSlap)
        {
            return true;
        }

        if (attackPhase == AttackPhase.WindupActive ||
            attackPhase == AttackPhase.ReturningAfterWindup ||
            attackPhase == AttackPhase.SlapActive)
        {
            return pendingDir == Dir.Down;
        }

        return false;
    }

    public bool IsVerticalAttackVisualActive()
    {
        if (role != Role.Attacker) return false;
        if (currentWindupState == slapUpWindup ||
            currentSlapState == slapUpSlap ||
            currentWindupState == slapErcutWindup ||
            currentSlapState == slapErcutSlap)
        {
            return true;
        }

        if (attackPhase == AttackPhase.WindupActive ||
            attackPhase == AttackPhase.ReturningAfterWindup ||
            attackPhase == AttackPhase.SlapActive)
        {
            return pendingDir == Dir.Up || pendingDir == Dir.Down;
        }

        return false;
    }

    public float GetControlledVisualProgress01()
    {
        if (!IsControlledHandMoving()) return 0f;

        if (role == Role.Attacker)
        {
            // During slap phase keep full visibility: hand must not blink
            // when windup parameter is reset around transitions.
            if (IsCurrentStateSlap()) return 1f;

            float p = Mathf.Clamp01(GetDebugWindup01());
            return p;
        }

        if (role == Role.Defender)
        {
            return Mathf.Clamp01(GetDefenderBlockHold01());
        }

        return 0f;
    }

    public bool IsDefenderBlocking()
    {
        return role == Role.Defender && defenderBlockHeld && defenderBlockHoldNormalized > 0.01f;
    }

    public bool IsDefenderBlockReleasing()
    {
        return role == Role.Defender && defenderBlockReleaseActive;
    }

    public bool IsAttackerWindupHolding()
    {
        return role == Role.Attacker &&
               attackPhase == AttackPhase.WindupActive &&
               swipeActive &&
               windupTriggered &&
               pendingDir != Dir.None &&
               !inputLockedAfterSlap;
    }

    public void ConfigureAura(bool enable, bool owner)
    {
        enableDirectionalAura = enable;
        isDirectionalAuraOwner = owner;
    }

    public float GetWindupHoldSeconds()
    {
        if (!windupStartTimeSet) return 0f;
        return Mathf.Max(0f, Time.time - windupStartTime);
    }

    private SlapDirection ToPublicDir(Dir d)
    {
        return d switch
        {
            Dir.Up => SlapDirection.Up,
            Dir.Down => SlapDirection.Down,
            Dir.Left => SlapDirection.Left,
            Dir.Right => SlapDirection.Right,
            Dir.UpLeft => SlapDirection.UpLeft,
            Dir.UpRight => SlapDirection.UpRight,
            Dir.DownLeft => SlapDirection.DownLeft,
            Dir.DownRight => SlapDirection.DownRight,
            _ => SlapDirection.None
        };
    }

    private Dir FromPublicDir(SlapDirection d)
    {
        return d switch
        {
            SlapDirection.Up => Dir.Up,
            SlapDirection.Down => Dir.Down,
            SlapDirection.Left => Dir.Left,
            SlapDirection.Right => Dir.Right,
            SlapDirection.UpLeft => Dir.UpLeft,
            SlapDirection.UpRight => Dir.UpRight,
            SlapDirection.DownLeft => Dir.DownLeft,
            SlapDirection.DownRight => Dir.DownRight,
            _ => Dir.None
        };
    }

    private void UpdateIntroPlayback()
    {
        if (!introPlayableGraphActive) return;
        if (!introGraphBlendActive)
        {
            if (Time.time < introReturnToIdleAtTime) return;

            if (introMixerPlayable.IsValid() && introControllerPlayable.IsValid())
            {
                introGraphBlendActive = true;
                introGraphBlendStartTime = Time.time;
                introGraphBlendDuration = Mathf.Max(0.01f, introToIdleBlendSeconds);
                introBlendOutUntilTime = Time.time + introGraphBlendDuration;
                if (!string.IsNullOrEmpty(idleStateName) && animator != null)
                {
                    animator.CrossFadeInFixedTime(idleStateName, 0.05f, 0, 0f, 0f);
                }
                return;
            }

            StopIntroPlayableGraph();
            float blend = Mathf.Max(0.01f, introToIdleBlendSeconds);
            BlendToIdlePose(blend);
            introBlendOutUntilTime = Time.time + blend;
            return;
        }

        if (!introMixerPlayable.IsValid())
        {
            StopIntroPlayableGraph();
            return;
        }

        float t = Mathf.Clamp01((Time.time - introGraphBlendStartTime) / Mathf.Max(0.01f, introGraphBlendDuration));
        introMixerPlayable.SetInputWeight(0, t);
        introMixerPlayable.SetInputWeight(1, 1f - t);
        if (t >= 1f)
        {
            StopIntroPlayableGraph();
        }
    }

    private void StopIdleBreathingPlayableGraph()
    {
    }

    private void ForceDisableIdleBreathingForCombatStart()
    {
        StopIdleBreathingPlayableGraph();
    }

    private void StopIntroPlayableGraph()
    {
        if (introPlayableGraphActive && introPlayableGraph.IsValid())
        {
            introPlayableGraph.Stop();
            introPlayableGraph.Destroy();
        }
        if (animator != null && introOriginalController != null)
        {
            animator.runtimeAnimatorController = introOriginalController;
        }
        introOriginalController = null;
        introOverrideController = null;
        introPlayableGraphActive = false;
        introGraphBlendActive = false;
        introGraphBlendStartTime = -1f;
        introGraphBlendDuration = 0f;
        introReturnToIdleAtTime = -1f;
        introMixerPlayable = default;
        introControllerPlayable = default;
        introClipPlayable = default;
        introRigLockChain = null;
        introRigLockChainStartRotations = null;
        introShouldersLockActive = false;
        introShoulderLockBones = null;
        introShoulderLockStartRotations = null;
        introBodyExceptHeadLockActive = false;
        introBodyLockBones = null;
        introBodyLockStartRotations = null;
    }

    private void CacheIntroRigRootLock()
    {
        introRigLockChain = null;
        introRigLockChainStartRotations = null;
        if (animator == null) return;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform start = hips != null ? hips.parent : null;
        if (start == null)
        {
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name.IndexOf("armature", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    start = t;
                    break;
                }
            }
        }
        if (start == null) return;

        var chain = new List<Transform>(8);
        var rots = new List<Quaternion>(8);
        Transform cur = start;
        while (cur != null)
        {
            chain.Add(cur);
            rots.Add(cur.localRotation);
            if (cur == transform) break;
            cur = cur.parent;
        }

        if (chain.Count > 0)
        {
            introRigLockChain = chain.ToArray();
            introRigLockChainStartRotations = rots.ToArray();
        }
    }

    private void ApplyIntroRigRootLock()
    {
        if (introRigLockChain == null || introRigLockChainStartRotations == null) return;
        int count = Mathf.Min(introRigLockChain.Length, introRigLockChainStartRotations.Length);
        for (int i = 0; i < count; i++)
        {
            var t = introRigLockChain[i];
            if (t == null) continue;
            if (t.localRotation != introRigLockChainStartRotations[i])
            {
                t.localRotation = introRigLockChainStartRotations[i];
            }
        }
    }

    private void CacheIntroShoulderLock(bool enabled)
    {
        introShouldersLockActive = enabled;
        introShoulderLockBones = null;
        introShoulderLockStartRotations = null;
        if (!enabled || animator == null) return;

        var list = new List<Transform>(40);
        // Lock full arms chain (both sides) only for requested intro.
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftShoulder));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightShoulder));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightLowerArm));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftHand));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightHand));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftThumbDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftRingProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftRingIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftRingDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftLittleIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.LeftLittleDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightThumbProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightThumbDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightIndexProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightIndexDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightRingProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightRingIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightRingDistal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightLittleProximal));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightLittleIntermediate));
        AddUniqueTransform(list, animator.GetBoneTransform(HumanBodyBones.RightLittleDistal));
        if (list.Count == 0) return;

        introShoulderLockBones = list.ToArray();
        introShoulderLockStartRotations = new Quaternion[introShoulderLockBones.Length];
        for (int i = 0; i < introShoulderLockBones.Length; i++)
        {
            introShoulderLockStartRotations[i] = introShoulderLockBones[i].localRotation;
        }
    }

    private static void AddUniqueTransform(List<Transform> list, Transform t)
    {
        if (list == null || t == null) return;
        if (!list.Contains(t)) list.Add(t);
    }

    private void ApplyIntroShoulderLock()
    {
        if (!introShouldersLockActive) return;
        if (introShoulderLockBones == null || introShoulderLockStartRotations == null) return;
        int count = Mathf.Min(introShoulderLockBones.Length, introShoulderLockStartRotations.Length);
        for (int i = 0; i < count; i++)
        {
            var t = introShoulderLockBones[i];
            if (t == null) continue;
            t.localRotation = introShoulderLockStartRotations[i];
        }
    }

    private void CacheIntroBodyExceptHeadLock(bool enabled)
    {
        introBodyExceptHeadLockActive = enabled;
        introBodyLockBones = null;
        introBodyLockStartRotations = null;
        if (!enabled || animator == null) return;

        var list = new List<Transform>(64);
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var bone = (HumanBodyBones)i;
            if (bone == HumanBodyBones.Head) continue;
            if (bone == HumanBodyBones.Neck) continue;
            if (bone == HumanBodyBones.LeftShoulder) continue;
            if (bone == HumanBodyBones.RightShoulder) continue;
            AddUniqueTransform(list, animator.GetBoneTransform(bone));
        }
        if (list.Count == 0) return;

        introBodyLockBones = list.ToArray();
        introBodyLockStartRotations = new Quaternion[introBodyLockBones.Length];
        for (int i = 0; i < introBodyLockBones.Length; i++)
        {
            introBodyLockStartRotations[i] = introBodyLockBones[i].localRotation;
        }
    }

    private void ApplyIntroBodyExceptHeadLock()
    {
        if (!introBodyExceptHeadLockActive) return;
        if (introBodyLockBones == null || introBodyLockStartRotations == null) return;
        int count = Mathf.Min(introBodyLockBones.Length, introBodyLockStartRotations.Length);
        for (int i = 0; i < count; i++)
        {
            var t = introBodyLockBones[i];
            if (t == null) continue;
            t.localRotation = introBodyLockStartRotations[i];
        }
    }

    private bool TryStartIntroWithIdleOverride(AnimationClip clip)
    {
        if (animator == null) return false;
        if (clip == null) return false;
        if (animator.runtimeAnimatorController == null) return false;
        if (string.IsNullOrEmpty(idleStateName)) return false;

        var baseController = animator.runtimeAnimatorController;
        var clips = baseController.animationClips;
        if (clips == null || clips.Length == 0) return false;

        AnimationClip sourceIdleClip = null;
        for (int i = 0; i < clips.Length; i++)
        {
            var c = clips[i];
            if (c == null) continue;
            if (string.Equals(c.name, idleStateName, StringComparison.OrdinalIgnoreCase))
            {
                sourceIdleClip = c;
                break;
            }
        }
        if (sourceIdleClip == null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (c == null) continue;
                if (c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sourceIdleClip = c;
                    break;
                }
            }
        }
        if (sourceIdleClip == null) return false;

        introOriginalController = baseController;
        introOverrideController = new AnimatorOverrideController(baseController);
        introOverrideController[sourceIdleClip] = clip;
        animator.runtimeAnimatorController = introOverrideController;
        animator.Play(idleStateName, 0, 0f);
        introPlayableGraphActive = true;
        introReturnToIdleAtTime = Time.time + Mathf.Max(0.01f, clip.length);
        return true;
    }

    private AnimationClip FindClipByName(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName)) return null;
        string altClipName = clipName.Contains(" ")
            ? clipName.Replace(" ", "_")
            : clipName.Replace("_", " ");
        var controllerClips = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips
            : null;
        if (controllerClips != null)
        {
            foreach (var clip in controllerClips)
            {
                if (clip != null &&
                    (string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(clip.name, altClipName, StringComparison.OrdinalIgnoreCase)))
                {
                    return clip;
                }
            }
        }

        var loaded = Resources.FindObjectsOfTypeAll<AnimationClip>();
        foreach (var clip in loaded)
        {
            if (clip != null &&
                (string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(clip.name, altClipName, StringComparison.OrdinalIgnoreCase)))
            {
                return clip;
            }
        }
#if UNITY_EDITOR
        // Editor-only hard fallback: locate clip asset directly on disk.
        string[] guids = AssetDatabase.FindAssets($"t:AnimationClip {clipName}");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null &&
                (string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(clip.name, altClipName, StringComparison.OrdinalIgnoreCase)))
            {
                return clip;
            }
        }
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
        }
#endif
        return null;
    }

    private float ResolveClipLengthByName(string clipName, float fallback)
    {
        var clip = FindClipByName(clipName);
        if (clip == null) return Mathf.Max(0.01f, fallback);
        return Mathf.Max(0.01f, clip.length);
    }

    public void AI_BeginWindup(SlapDirection dir)
    {
        if (role != Role.Attacker) return;
        Dir d = FromPublicDir(dir);
        if (d == Dir.None) return;
        RepinFeetLocalPoseNow();
        ForceDisableIdleBreathingForCombatStart();
        SetWindupStates(d);
        if (!string.IsNullOrEmpty(currentWindupState))
        {
            float fade = Mathf.Max(0f, windupStartCrossfadeSeconds);
            if (fade <= 0f)
            {
                animator.Play(currentWindupState, 0, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(currentWindupState, fade, 0, 0f, 0f);
            }
        }
        pendingDir = d;
        slapReturnTriggered = false;
        pendingTime = Time.time;
        windupTriggered = true;
        attackPhase = AttackPhase.WindupActive;
        maxProgress = 0f;
        windupCarryOffset = 0f;
        maxProjectedPx = 0f;
        reverseSwipePeakCmPerSec = 0f;
        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = 0f;
        reverseAccumulatedPx = 0f;
        reverseIntentHeldSeconds = 0f;
        slapPlayedThisSwipe = false;
        inputLockedAfterSlap = false;
        swipeActive = true;
        windupStartTime = Time.time;
        windupStartTimeSet = true;
    }

    public void AI_SetWindupProgress(float windup01)
    {
        if (role != Role.Attacker) return;
        maxProgress = Mathf.Clamp01(windup01);
        if (animator != null && !string.IsNullOrEmpty(handMoveParam))
        {
            animator.SetFloat(handMoveParam, maxProgress);
        }
    }

    public void AI_TriggerSlap(SlapDirection dir, float swipeSpeedCmPerSec)
    {
        AI_TriggerSlap(dir, swipeSpeedCmPerSec, 1f);
    }

    public void AI_TriggerSlap(SlapDirection dir, float swipeSpeedCmPerSec, float speedMultiplier)
    {
        if (role != Role.Attacker) return;
        Dir d = FromPublicDir(dir);
        if (d == Dir.None) return;
        ForceDisableIdleBreathingForCombatStart();
        SetWindupStates(d);
        if (string.IsNullOrEmpty(currentSlapState)) return;
        float currentHand = GetCurrentHandProgressForCombat();
        float effectiveWindup = Mathf.Max(currentHand, maxProgress);
        if (effectiveWindup < minWindupForSlap)
        {
            // Same gate as player input: AI cannot fire slap below minimal windup.
            AI_SoftCancelWindup();
            return;
        }
        SetSlapSpeed(swipeSpeedCmPerSec);
        if (animator != null && !string.IsNullOrEmpty(slapSpeedParam))
        {
            float current = animator.GetFloat(slapSpeedParam);
            float mul = Mathf.Max(0.01f, speedMultiplier);
            animator.SetFloat(slapSpeedParam, current * mul);
        }
        float slapStart = GetSlapStartNormalizedTime(currentSlapState, currentHand);
        bool isCardinalSlap = IsCardinalSlapState(currentSlapState);
        bool isUpSlap = currentSlapState == slapUpSlap;
        BeginSlapPoseBlend(isCardinalSlap, isUpSlap);

        float fadeSeconds = isUpSlap
            ? upSlapCrossfadeSeconds
            : (isCardinalSlap ? cardinalSlapCrossfadeSeconds : slapStateCrossfadeSeconds);

        if (fadeSeconds <= 0f)
        {
            animator.Play(currentSlapState, 0, slapStart);
        }
        else
        {
            animator.CrossFadeInFixedTime(currentSlapState, fadeSeconds, 0, slapStart, 0f);
        }

        slapPlayedThisSwipe = true;
        slapReturnTriggered = false;
        attackPhase = AttackPhase.SlapActive;
        inputLockedAfterSlap = true;
        swipeActive = false;
        ClearWindupReturnTracking();
        lastSlapDir = d;
        RaiseSlapFired();

        pendingDir = Dir.None;
        windupStartTimeSet = false;
        maxProgress = 0f;
        windupCarryOffset = 0f;
        maxProjectedPx = 0f;
        reverseSwipePeakCmPerSec = 0f;
        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = 0f;
        reverseAccumulatedPx = 0f;
        reverseIntentHeldSeconds = 0f;
    }

    public void AI_CancelWindup()
    {
        ClearWindupReturnTracking();
        pendingDir = Dir.None;
        windupTriggered = false;
        maxProgress = 0f;
        windupCarryOffset = 0f;
        maxProjectedPx = 0f;
        reverseSwipePeakCmPerSec = 0f;
        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = 0f;
        reverseAccumulatedPx = 0f;
        reverseIntentHeldSeconds = 0f;
        windupStartTimeSet = false;
        swipeActive = false;
        if (animator != null && !string.IsNullOrEmpty(handMoveParam))
        {
            animator.SetFloat(handMoveParam, 0f);
        }
    }

    public void AI_SoftCancelWindup()
    {
        ClearWindupReturnTracking();
        if (role == Role.Attacker && !inputLockedAfterSlap)
        {
            attackPhase = AttackPhase.ReturningAfterWindup;
            releaseReturnActive = true;
            BeginShortestReturnToIdle();
        }
        pendingDir = Dir.None;
        windupTriggered = false;
        maxProgress = 0f;
        windupCarryOffset = 0f;
        maxProjectedPx = 0f;
        reverseSwipePeakCmPerSec = 0f;
        reverseSwipeCurrentCmPerSec = 0f;
        reverseSwipeSmoothedCmPerSec = 0f;
        reverseAccumulatedPx = 0f;
        reverseIntentHeldSeconds = 0f;
        windupStartTimeSet = false;
        swipeActive = false;
        // Do not force hand param to zero; let smoothing handle it.
    }

    public void AI_StartBlock(SlapDirection dir)
    {
        if (role != Role.Defender) return;
        if (Time.time < aiBlockReacquireBlockedUntilTime) return;
        Dir d = FromPublicDir(dir);
        if (d == Dir.None) return;
        defenderBlockReleaseActive = false;

        // During hard lock, block pose is driven from ApplyAIHardBlockLock().
        // Do not restart state here to avoid one-frame jitter.
        if (aiHardBlockLock)
        {
            if (aiHardBlockDir == Dir.None) aiHardBlockDir = d;
            return;
        }

        SetBlockState(d);
        if (string.IsNullOrEmpty(currentBlockState)) return;

        // If already holding this block, do not restart from 0 (prevents visual hand drop).
        if (defenderBlockHeld && lastBlockDir == d && defenderBlockHoldNormalized > 0f)
        {
            HoldDefenderBlockState(currentBlockState, defenderBlockHoldNormalized);
            return;
        }

        lastBlockDir = d;
        defenderBlockChosenThisSwipe = true;
        // Preserve already reached hold to avoid one-frame down-up jitter on re-issue.
        if (!defenderBlockHeld)
        {
            defenderBlockHoldNormalized = 0f;
            defenderBlockStartTime = Time.time;
        }
        HoldDefenderBlockState(currentBlockState, defenderBlockHoldNormalized);
    }

    public void AI_UpdateBlockHold(float hold01)
    {
        if (role != Role.Defender) return;
        if (aiHardBlockLock) return;
        if (string.IsNullOrEmpty(currentBlockState)) return;
        if (lastBlockDir == Dir.None) return;
        float h = Mathf.Clamp01(hold01);
        if (h > defenderBlockHoldNormalized)
        {
            defenderBlockHoldNormalized = h;
        }
        HoldDefenderBlockState(currentBlockState, defenderBlockHoldNormalized);
    }

    public void AI_EndBlock()
    {
        if (role != Role.Defender) return;
        if (aiHardBlockLock) return;
        aiBlockReacquireBlockedUntilTime = Time.time + Mathf.Max(0f, aiBlockReacquireCooldownSeconds);
        if (!defenderBlockHeld)
        {
            lastBlockDir = Dir.None;
            currentBlockState = null;
            defenderBlockHoldNormalized = 0f;
            defenderBlockChosenThisSwipe = false;
            defenderBlockReleaseActive = false;
            return;
        }
        // Direct smooth return to idle avoids block-state reverse jitter (down-up-down).
        ReleaseDefenderBlockHold();
        lastBlockDir = Dir.None;
        currentBlockState = null;
        defenderBlockChosenThisSwipe = false;
        defenderBlockReleaseActive = false;
        BlendToIdlePose(defenderBlockReleaseSeconds);
    }

    public void AI_SetHardBlockLock(bool enabled, SlapDirection dir)
    {
        if (role != Role.Defender)
        {
            aiHardBlockLock = false;
            aiHardBlockDir = Dir.None;
            aiHardBlockLastStateName = null;
            aiHardBlockLastAppliedHold = -1f;
            return;
        }

        if (!enabled)
        {
            aiHardBlockLock = false;
            aiHardBlockDir = Dir.None;
            aiHardBlockLastStateName = null;
            aiHardBlockLastAppliedHold = -1f;
            return;
        }

        if (Time.time < aiBlockReacquireBlockedUntilTime)
        {
            return;
        }

        // Keep lock direction stable for the whole incoming threat.
        if (aiHardBlockLock && aiHardBlockDir != Dir.None)
        {
            return;
        }

        Dir d = FromPublicDir(dir);
        if (d == Dir.None)
        {
            d = aiHardBlockDir != Dir.None ? aiHardBlockDir : (lastBlockDir != Dir.None ? lastBlockDir : Dir.Up);
        }

        aiHardBlockLock = true;
        aiHardBlockDir = d;
        defenderBlockReleaseActive = false;
        aiHardBlockStartHold01 = Mathf.Clamp01(defenderBlockHoldNormalized);
        aiHardBlockBlendStartTime = Time.time;
        aiHardBlockLastStateName = null;
        aiHardBlockLastAppliedHold = -1f;
    }

    private void RaiseSlapFired()
    {
        if (role != Role.Attacker) return;
        var ev = new SlapEvent
        {
            windup01 = GetDebugWindup01(),
            slapPower01 = GetDebugSlapPower01(),
            direction = ToPublicDir(lastSlapDir),
            windupHoldSeconds = GetWindupHoldSeconds()
        };
        OnSlapFired?.Invoke(this, ev);
    }

    private float PixelsToCm(float pixelsPerSecond)
    {
        float dpi = Screen.dpi;
        if (dpi <= 0f) dpi = fallbackDpi;
        return pixelsPerSecond / (dpi / 2.54f);
    }

    private void EnsurePositiveScaleX()
    {
        var s = transform.localScale;
        if (s.x < 0f)
        {
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    private void UpdateAttackPhaseFromAnimator()
    {
        if (animator == null) return;
        if (role != Role.Attacker) return;

        bool isSlapAnim = IsCurrentStateSlap();
        if (attackPhase == AttackPhase.SlapActive && !isSlapAnim)
        {
            attackPhase = AttackPhase.ReturningAfterSlap;
        }
        else if (isSlapAnim)
        {
            attackPhase = AttackPhase.SlapActive;
        }
    }

    private bool IsCurrentStateSlap()
    {
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (StateNameIsSlap(st)) return true;
        var next = animator.GetNextAnimatorStateInfo(0);
        return StateNameIsSlap(next);
    }

    private void ApplyAIHardBlockLock()
    {
        if (!aiHardBlockLock) return;
        if (role != Role.Defender) return;
        if (animator == null) return;
        defenderBlockReleaseActive = false;

        Dir d = aiHardBlockDir != Dir.None ? aiHardBlockDir : (lastBlockDir != Dir.None ? lastBlockDir : Dir.Up);
        SetBlockState(d);
        if (string.IsNullOrEmpty(currentBlockState)) return;

        lastBlockDir = d;
        defenderBlockChosenThisSwipe = true;
        float targetHold = Mathf.Clamp01(aiHardBlockTargetHold01);
        float raiseSeconds = Mathf.Max(0.001f, aiHardBlockRaiseSeconds);
        float t = Mathf.Clamp01((Time.time - aiHardBlockBlendStartTime) / raiseSeconds);
        defenderBlockHoldNormalized = Mathf.Lerp(aiHardBlockStartHold01, targetHold, t);
        if (defenderBlockStartTime <= 0f) defenderBlockStartTime = Time.time;

        // Do not restart block state every frame; update pose only when needed.
        bool stateChanged = !string.Equals(aiHardBlockLastStateName, currentBlockState, StringComparison.Ordinal);
        bool holdChanged = aiHardBlockLastAppliedHold < 0f || Mathf.Abs(defenderBlockHoldNormalized - aiHardBlockLastAppliedHold) > 0.002f;
        if (stateChanged || holdChanged)
        {
            HoldDefenderBlockState(currentBlockState, defenderBlockHoldNormalized);
            aiHardBlockLastStateName = currentBlockState;
            aiHardBlockLastAppliedHold = defenderBlockHoldNormalized;
        }
    }

    private void UpdateDefenderBlockRelease()
    {
        if (!defenderBlockReleaseActive) return;
        if (aiHardBlockLock) return;
        if (!defenderBlockHeld)
        {
            defenderBlockReleaseActive = false;
            return;
        }
        if (string.IsNullOrEmpty(currentBlockState))
        {
            ReleaseDefenderBlockHold();
            lastBlockDir = Dir.None;
            defenderBlockChosenThisSwipe = false;
            return;
        }

        float downRate = 1f / Mathf.Max(0.001f, defenderBlockReleaseSeconds);
        defenderBlockHoldNormalized = Mathf.MoveTowards(defenderBlockHoldNormalized, 0f, downRate * Time.deltaTime);
        HoldDefenderBlockState(currentBlockState, defenderBlockHoldNormalized);

        if (defenderBlockHoldNormalized <= 0.0001f)
        {
            ReleaseDefenderBlockHold();
            lastBlockDir = Dir.None;
            currentBlockState = null;
            defenderBlockChosenThisSwipe = false;
        }
    }

    private bool IsCurrentStateBlock()
    {
        if (animator == null) return false;
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (StateNameIsBlock(st)) return true;
        var next = animator.GetNextAnimatorStateInfo(0);
        return StateNameIsBlock(next);
    }

    public bool IsSlapAnimating()
    {
        if (animator == null) return false;
        return IsCurrentStateSlap();
    }

    public bool IsAttackCycleActive()
    {
        // Robust threat signal for AI defender: keeps true across windup->slap transitions.
        if (role != Role.Attacker) return false;
        if (attackPhase == AttackPhase.ReturningAfterWindup &&
            pendingDir == Dir.None &&
            !windupTriggered &&
            !swipeActive &&
            !inputLockedAfterSlap &&
            animator != null &&
            animator.GetFloat(handMoveParam) <= 0.02f &&
            !IsCurrentStateSlap())
        {
            return false;
        }
        if (attackPhase != AttackPhase.Idle) return true;
        if (pendingDir != Dir.None) return true;
        if (windupTriggered || swipeActive) return true;
        if (inputLockedAfterSlap) return true;
        return IsCurrentStateSlap();
    }

    public float GetSlapProgress01()
    {
        if (animator == null) return 0f;
        float best = 0f;
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (StateNameIsSlap(st))
        {
            best = Mathf.Max(best, Mathf.Clamp01(st.normalizedTime));
        }
        var next = animator.GetNextAnimatorStateInfo(0);
        if (StateNameIsSlap(next))
        {
            best = Mathf.Max(best, Mathf.Clamp01(next.normalizedTime));
        }
        return best;
    }

    public void InterruptSlapForSuccessfulBlock(float minProgress01 = 0.6f)
    {
        EnsureAnimator();
        if (animator == null) return;
        if (role != Role.Attacker) return;
        if (!IsCurrentStateSlap()) return;
        if (GetSlapProgress01() < Mathf.Clamp01(minProgress01)) return;

        int idleHash = Animator.StringToHash(idleStateName);
        if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, idleHash))
        {
            // Shortest path back to neutral right after successful block.
            float fade = Mathf.Max(0.12f, releaseToIdleCrossfadeSeconds);
            animator.CrossFadeInFixedTime(idleStateName, fade, 0, 0f, 0f);
        }

        attackPhase = AttackPhase.ReturningAfterSlap;
        inputLockedAfterSlap = false;
        swipeActive = false;
        windupTriggered = false;
        pendingDir = Dir.None;
        releaseReturnActive = true;
        slapReturnTriggered = true;
    }

    private bool StateNameIsSlap(AnimatorStateInfo stateInfo)
    {
        return StateMatches(stateInfo, slap1Slap) ||
               StateMatches(stateInfo, slap3Slap) ||
               StateMatches(stateInfo, slap7Slap) ||
               StateMatches(stateInfo, slap9Slap) ||
               StateMatches(stateInfo, slapLeftSlap) ||
               StateMatches(stateInfo, slapRightSlap) ||
               StateMatches(stateInfo, slapUpSlap) ||
               StateMatches(stateInfo, slapErcutSlap);
    }

    private bool StateNameIsWindup(AnimatorStateInfo stateInfo)
    {
        return StateMatches(stateInfo, slap1Windup) ||
               StateMatches(stateInfo, slap3Windup) ||
               StateMatches(stateInfo, slap7Windup) ||
               StateMatches(stateInfo, slap9Windup) ||
               StateMatches(stateInfo, slapLeftWindup) ||
               StateMatches(stateInfo, slapRightWindup) ||
               StateMatches(stateInfo, slapUpWindup) ||
               StateMatches(stateInfo, slapErcutWindup);
    }

    private bool StateNameIsBlock(AnimatorStateInfo stateInfo)
    {
        return StateMatches(stateInfo, block1Block) ||
               StateMatches(stateInfo, block3Block) ||
               StateMatches(stateInfo, block7Block) ||
               StateMatches(stateInfo, block9Block) ||
               StateMatches(stateInfo, blockLeftBlock) ||
               StateMatches(stateInfo, blockRightBlock) ||
               StateMatches(stateInfo, blockUpBlock) ||
               StateMatches(stateInfo, blockErcutBlock);
    }

    private bool StateMatches(AnimatorStateInfo stateInfo, string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return false;
        int h = Animator.StringToHash(stateName);
        if (stateInfo.shortNameHash == h || stateInfo.fullPathHash == h) return true;
        if (stateInfo.IsName(stateName)) return true;
        return stateInfo.IsName("Base Layer." + stateName);
    }

    private bool StateNameIsCombat(AnimatorStateInfo stateInfo)
    {
        return StateNameIsWindup(stateInfo) ||
               StateNameIsSlap(stateInfo) ||
               StateNameIsBlock(stateInfo);
    }

    private bool IsAnimatorInCombatMotion()
    {
        if (animator == null) return false;
        var st = animator.GetCurrentAnimatorStateInfo(0);
        if (StateNameIsCombat(st)) return true;
        var next = animator.GetNextAnimatorStateInfo(0);
        return StateNameIsCombat(next);
    }

    private bool ShouldLockLegsNow()
    {
        if (lockDefenderBodyDuringBlock && role == Role.Defender && (defenderBlockHeld || IsCurrentStateBlock()))
        {
            return true;
        }
        if (IsTouchPrimingOnly()) return true;
        if (IsAnimatorInCombatMotion()) return false;
        if (attackPhase != AttackPhase.Idle) return false;
        if (swipeActive || windupTriggered || inputLockedAfterSlap) return false;
        if (pendingDir != Dir.None) return false;
        return true;
    }

    private void BeginSlapPoseBlend(bool isCardinalSlap, bool isUpSlap)
    {
        if (!lockHandPoseOnSlapStart) return;
        CacheSlapPoseBonesIfNeeded();
        EnsureLowerBodyMuscleMaskIfNeeded();
        if (humanPoseAvailable && humanPoseHandler != null)
        {
            humanPoseHandler.GetHumanPose(ref slapStartHumanPose);
        }
        else
        {
            if (slapPoseBones == null || slapPoseBones.Length == 0) return;
            if (slapPoseStartLocalPositions == null || slapPoseStartLocalPositions.Length != slapPoseBones.Length) return;
            if (slapPoseStartLocalRotations == null || slapPoseStartLocalRotations.Length != slapPoseBones.Length) return;

            for (int i = 0; i < slapPoseBones.Length; i++)
            {
                var bone = slapPoseBones[i];
                if (bone == null) continue;
                slapPoseStartLocalPositions[i] = bone.localPosition;
                slapPoseStartLocalRotations[i] = bone.localRotation;
            }
        }
        slapPoseBlendStartTime = Time.time;
        if (isUpSlap)
        {
            activeSlapPoseBlendDuration = upSlapPoseBlendDuration;
            activeSlapPoseReleasePower = upSlapPoseReleasePower;
        }
        else
        {
            activeSlapPoseBlendDuration = isCardinalSlap ? cardinalSlapPoseBlendDuration : slapPoseBlendDuration;
            activeSlapPoseReleasePower = isCardinalSlap ? cardinalSlapPoseReleasePower : slapPoseReleasePower;
        }
        slapPoseBlendActive = true;
    }

    private void ApplySlapPoseBlend()
    {
        if (!slapPoseBlendActive) return;
        float d = Mathf.Max(0.001f, activeSlapPoseBlendDuration > 0f ? activeSlapPoseBlendDuration : slapPoseBlendDuration);
        float t01 = Mathf.Clamp01((Time.time - slapPoseBlendStartTime) / d);
        float releasePower = activeSlapPoseReleasePower > 0f ? activeSlapPoseReleasePower : slapPoseReleasePower;
        float t = Mathf.Pow(t01, Mathf.Max(0.01f, releasePower));
        if (t >= 1f)
        {
            slapPoseBlendActive = false;
            return;
        }

        // Keep the exact start pose on slap start, then release into slap animation.
        if (humanPoseAvailable && humanPoseHandler != null)
        {
            humanPoseHandler.GetHumanPose(ref currentHumanPose);
            currentHumanPose.bodyPosition = Vector3.Lerp(slapStartHumanPose.bodyPosition, currentHumanPose.bodyPosition, t);
            currentHumanPose.bodyRotation = Quaternion.Slerp(slapStartHumanPose.bodyRotation, currentHumanPose.bodyRotation, t);

            int count = Mathf.Min(
                slapStartHumanPose.muscles != null ? slapStartHumanPose.muscles.Length : 0,
                currentHumanPose.muscles != null ? currentHumanPose.muscles.Length : 0);
            for (int i = 0; i < count; i++)
            {
                if (preserveLowerBodyInSlapPoseBlend &&
                    lowerBodyMuscleMask != null &&
                    i < lowerBodyMuscleMask.Length &&
                    lowerBodyMuscleMask[i])
                {
                    continue;
                }
                currentHumanPose.muscles[i] = Mathf.Lerp(slapStartHumanPose.muscles[i], currentHumanPose.muscles[i], t);
            }

            humanPoseHandler.SetHumanPose(ref currentHumanPose);
            return;
        }

        if (slapPoseBones == null || slapPoseBones.Length == 0) return;

        for (int i = 0; i < slapPoseBones.Length; i++)
        {
            var bone = slapPoseBones[i];
            if (bone == null) continue;
            if (preserveLowerBodyInSlapPoseBlend && IsLowerBodyBoneForSlapBlend(bone.name)) continue;
            bone.localPosition = Vector3.Lerp(slapPoseStartLocalPositions[i], bone.localPosition, t);
            bone.localRotation = Quaternion.Slerp(slapPoseStartLocalRotations[i], bone.localRotation, t);
        }
    }

    private void EnsureLowerBodyMuscleMaskIfNeeded()
    {
        if (lowerBodyMuscleMask != null && lowerBodyMuscleMask.Length == HumanTrait.MuscleCount) return;

        int count = HumanTrait.MuscleCount;
        lowerBodyMuscleMask = new bool[count];
        for (int i = 0; i < count; i++)
        {
            string n = HumanTrait.MuscleName[i];
            if (string.IsNullOrEmpty(n)) continue;
            string lower = n.ToLowerInvariant();
            lowerBodyMuscleMask[i] =
                lower.Contains("leg") ||
                lower.Contains("knee") ||
                lower.Contains("foot") ||
                lower.Contains("toe") ||
                lower.Contains("hips") ||
                lower.Contains("pelvis");
        }
    }

    private static bool IsLowerBodyBoneForSlapBlend(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return false;
        string n = boneName.ToLowerInvariant();
        return n.Contains("hip") ||
               n.Contains("pelvis") ||
               n.Contains("thigh") ||
               n.Contains("upperleg") ||
               n.Contains("lowerleg") ||
               n.Contains("calf") ||
               n.Contains("shin") ||
               n.Contains("knee") ||
               n.Contains("foot") ||
               n.Contains("toe");
    }

    private float GetSlapStartNormalizedTime(string slapStateName, float hand01)
    {
        if (slapStateName == slapUpSlap)
        {
            return Mathf.Clamp01(upSlapStartNormalizedTime);
        }

        float h = Mathf.Clamp01(hand01);
        bool isSideOrUppercut = slapStateName == slapLeftSlap ||
                                slapStateName == slapRightSlap ||
                                slapStateName == slapErcutSlap;
        if (isSideOrUppercut)
        {
            float sideUppercutMaxStart = Mathf.Clamp01(sideUppercutSlapStartMaxNormalizedTime);
            if (useDirectHandTimeForCardinalSlaps)
            {
                // Slap clip starts from wound-up pose. Invert hand progress so small windup
                // starts later in clip and does not travel full arc.
                float start = (1f - h) + cardinalSlapStartOffset;
                return Mathf.Clamp(start, 0f, sideUppercutMaxStart);
            }
            float fallbackStart = (1f - h) + diagonalSlapStartOffset;
            return Mathf.Clamp(fallbackStart, 0f, sideUppercutMaxStart);
        }

        bool isCardinal = slapStateName == slapLeftSlap ||
                          slapStateName == slapRightSlap ||
                          slapStateName == slapUpSlap ||
                          slapStateName == slapErcutSlap;

        if (isCardinal && useDirectHandTimeForCardinalSlaps)
        {
            return Mathf.Clamp01((1f - h) + cardinalSlapStartOffset);
        }

        return Mathf.Clamp01((1f - h) + diagonalSlapStartOffset);
    }

    private float GetFinishSlapStartNormalizedTime(string slapStateName, float hand01)
    {
        float h = Mathf.Clamp01(hand01);
        bool isCardinal = slapStateName == slapLeftSlap ||
                          slapStateName == slapRightSlap ||
                          slapStateName == slapUpSlap ||
                          slapStateName == slapErcutSlap;

        if (isCardinal)
        {
            float offset = useDirectHandTimeForCardinalSlaps
                ? cardinalSlapStartOffset
                : diagonalSlapStartOffset;
            return Mathf.Clamp01((1f - h) + offset);
        }

        return Mathf.Clamp01((1f - h) + diagonalSlapStartOffset);
    }

    private bool IsCardinalSlapState(string slapStateName)
    {
        return slapStateName == slapLeftSlap ||
               slapStateName == slapRightSlap ||
               slapStateName == slapUpSlap ||
               slapStateName == slapErcutSlap;
    }

    private void UpdateDirectionalAuraState()
    {
        if (!enableDirectionalAura || !isDirectionalAuraOwner) return;

        float dt = Mathf.Max(0f, Time.deltaTime);
        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, auraBlendSpeed) * dt);
        auraWindupSmoothed = Mathf.Lerp(auraWindupSmoothed, GetAuraProgress01(), k);

        if (role == Role.Defender)
        {
            if (lastBlockDir != Dir.None)
            {
                auraLastDir = lastBlockDir;
            }
        }
        else if (pendingDir != Dir.None)
        {
            auraLastDir = pendingDir;
        }
        else if (currentSlapState == slap1Slap) auraLastDir = Dir.UpRight;
        else if (currentSlapState == slap3Slap) auraLastDir = Dir.UpLeft;
        else if (currentSlapState == slap7Slap) auraLastDir = Dir.DownRight;
        else if (currentSlapState == slap9Slap) auraLastDir = Dir.DownLeft;
        else if (currentSlapState == slapLeftSlap) auraLastDir = Dir.Left;
        else if (currentSlapState == slapRightSlap) auraLastDir = Dir.Right;
        else if (currentSlapState == slapUpSlap) auraLastDir = Dir.Up;
        else if (currentSlapState == slapErcutSlap) auraLastDir = Dir.Down;

        bool slapNow = attackPhase == AttackPhase.SlapActive;
        if (slapNow && !auraWasSlapActive)
        {
            auraFlashTimer = Mathf.Max(0.01f, auraFlashDuration);
            auraFlashPower = Mathf.Clamp01(debugLastSlapPower01);
        }
        auraWasSlapActive = slapNow;
        if (auraFlashTimer > 0f)
        {
            auraFlashTimer = Mathf.Max(0f, auraFlashTimer - dt);
        }
    }

    private Dir GetAuraDirection()
    {
        if (role == Role.Defender)
        {
            if (lastBlockDir != Dir.None) return lastBlockDir;
            return auraLastDir;
        }

        if (pendingDir != Dir.None) return pendingDir;
        return auraLastDir;
    }

    private float GetAuraProgress01()
    {
        if (role != Role.Defender) return GetDebugWindup01();

        float v = Mathf.Clamp01(defenderBlockHoldNormalized);
        if (defenderBlockHeld)
        {
            v = Mathf.Max(v, 0.2f);
        }
        if (allowHumanInput && swipeActive)
        {
            v = Mathf.Max(v, 0.2f);
        }
        return v;
    }

    private void EnsureAuraTexture()
    {
        if (auraRadialTex != null) return;
        const int size = 256;
        auraRadialTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        auraRadialTex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float inv = 1f / center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) * inv;
                float dy = (y - center) * inv;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.Clamp01(d);
                a = Mathf.Pow(a, 2.2f);
                auraRadialTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        auraRadialTex.Apply();
    }

    private Rect GetAuraZoneRect(Dir dir, float w, float h)
    {
        switch (dir)
        {
            case Dir.UpLeft: return new Rect(0f, 0f, w * 0.5f, h * 0.5f);
            case Dir.Up: return new Rect(w * 0.25f, 0f, w * 0.5f, h * 0.5f);
            case Dir.UpRight: return new Rect(w * 0.5f, 0f, w * 0.5f, h * 0.5f);
            case Dir.Left: return new Rect(0f, h * 0.25f, w * 0.5f, h * 0.5f);
            case Dir.Right: return new Rect(w * 0.5f, h * 0.25f, w * 0.5f, h * 0.5f);
            case Dir.DownLeft: return new Rect(0f, h * 0.5f, w * 0.5f, h * 0.5f);
            case Dir.Down: return new Rect(w * 0.25f, h * 0.5f, w * 0.5f, h * 0.5f);
            case Dir.DownRight: return new Rect(w * 0.5f, h * 0.5f, w * 0.5f, h * 0.5f);
            default: return new Rect(0f, 0f, w, h);
        }
    }

    private Vector2 GetAuraAnchorInZone(Dir dir)
    {
        switch (dir)
        {
            case Dir.UpLeft: return new Vector2(0.08f, 0.08f);
            case Dir.Up: return new Vector2(0.5f, 0.08f);
            case Dir.UpRight: return new Vector2(0.92f, 0.08f);
            case Dir.Left: return new Vector2(0.08f, 0.5f);
            case Dir.Right: return new Vector2(0.92f, 0.5f);
            case Dir.DownLeft: return new Vector2(0.08f, 0.92f);
            case Dir.Down: return new Vector2(0.5f, 0.92f);
            case Dir.DownRight: return new Vector2(0.92f, 0.92f);
            default: return new Vector2(0.5f, 0.5f);
        }
    }

    private void DrawAuraBlobInZone(Rect zone, Vector2 anchor01, Color color, float size01)
    {
        if (zone.width <= 0f || zone.height <= 0f) return;
        float blobSize = Mathf.Max(zone.width, zone.height) * size01;
        float cx = zone.x + zone.width * anchor01.x;
        float cy = zone.y + zone.height * anchor01.y;
        Rect blobRect = new Rect(cx - blobSize * 0.5f, cy - blobSize * 0.5f, blobSize, blobSize);

        Color prev = GUI.color;
        GUI.BeginGroup(zone);
        GUI.color = color;
        Rect localRect = new Rect(blobRect.x - zone.x, blobRect.y - zone.y, blobRect.width, blobRect.height);
        GUI.DrawTexture(localRect, auraRadialTex);
        GUI.color = prev;
        GUI.EndGroup();
    }

    private void DrawDirectionalAura(Dir dir, Color color, float size01)
    {
        if (auraRenderMode == AuraRenderMode.AroundCharacter && TryGetCharacterAuraRect(out Rect bodyRect))
        {
            DrawAuraAroundCharacter(bodyRect, dir, color, size01);
            return;
        }

        Rect zone = GetAuraZoneRect(dir, Screen.width, Screen.height);
        Vector2 anchor = GetAuraAnchorInZone(dir);
        bool unclipForVertical = dir == Dir.Up || dir == Dir.Down;
        if (unclipForVertical)
        {
            zone = new Rect(0f, 0f, Screen.width, Screen.height);
            if (dir == Dir.Up)
            {
                anchor.y = Mathf.Clamp01(anchor.y - 0.4f);
            }
            else
            {
                anchor.y = Mathf.Clamp01(anchor.y + 0.2f);
            }
        }
        DrawAuraBlobInZone(zone, anchor, color, size01);
    }

    private void DrawAuraAroundCharacter(Rect bodyRect, Dir dir, Color color, float size01)
    {
        float pad = Mathf.Max(0.8f, characterAuraPadding);
        float w = bodyRect.width * pad;
        float h = bodyRect.height * pad;
        float cx = bodyRect.x + bodyRect.width * 0.5f;
        float cy = bodyRect.y + bodyRect.height * 0.5f;

        Vector2 offset01 = GetAuraOffset(dir);
        float dirOffset = Mathf.Clamp(characterAuraDirectionOffset, 0f, 1f);
        cx += offset01.x * w * dirOffset;
        cy += offset01.y * h * dirOffset;

        float blobSize = Mathf.Max(w, h) * Mathf.Clamp(size01, 0.2f, 2f);
        Rect blobRect = new Rect(cx - blobSize * 0.5f, cy - blobSize * 0.5f, blobSize, blobSize);

        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(blobRect, auraRadialTex);
        GUI.color = prev;
    }

    private Vector2 GetAuraOffset(Dir dir)
    {
        switch (dir)
        {
            case Dir.UpLeft: return new Vector2(-0.65f, -0.65f);
            case Dir.Up: return new Vector2(0f, -1f);
            case Dir.UpRight: return new Vector2(0.65f, -0.65f);
            case Dir.Left: return new Vector2(-1f, 0f);
            case Dir.Right: return new Vector2(1f, 0f);
            case Dir.DownLeft: return new Vector2(-0.65f, 0.65f);
            case Dir.Down: return new Vector2(0f, 1f);
            case Dir.DownRight: return new Vector2(0.65f, 0.65f);
            default: return Vector2.zero;
        }
    }

    private bool TryGetCharacterAuraRect(out Rect rect)
    {
        rect = default;
        Camera cam = Camera.main;
        if (cam == null) return false;

        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return false;

        bool hasPoint = false;
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            Bounds b = r.bounds;
            if (b.size.sqrMagnitude <= 0.000001f) continue;

            Vector3 c = b.center;
            Vector3 e = b.extents;
            AuraBoundsCorners[0] = c + new Vector3(-e.x, -e.y, -e.z);
            AuraBoundsCorners[1] = c + new Vector3(-e.x, -e.y, e.z);
            AuraBoundsCorners[2] = c + new Vector3(-e.x, e.y, -e.z);
            AuraBoundsCorners[3] = c + new Vector3(-e.x, e.y, e.z);
            AuraBoundsCorners[4] = c + new Vector3(e.x, -e.y, -e.z);
            AuraBoundsCorners[5] = c + new Vector3(e.x, -e.y, e.z);
            AuraBoundsCorners[6] = c + new Vector3(e.x, e.y, -e.z);
            AuraBoundsCorners[7] = c + new Vector3(e.x, e.y, e.z);

            for (int i = 0; i < AuraBoundsCorners.Length; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(AuraBoundsCorners[i]);
                if (sp.z <= 0f) continue;
                float gx = sp.x;
                float gy = Screen.height - sp.y;
                minX = Mathf.Min(minX, gx);
                minY = Mathf.Min(minY, gy);
                maxX = Mathf.Max(maxX, gx);
                maxY = Mathf.Max(maxY, gy);
                hasPoint = true;
            }
        }

        if (!hasPoint) return false;
        float width = Mathf.Max(1f, maxX - minX);
        float height = Mathf.Max(1f, maxY - minY);
        rect = new Rect(minX, minY, width, height);
        return true;
    }


    private void ApplyHandsOnlyVisualIfNeeded()
    {
        if (!handsOnlyVisual) return;
        if (handsOnlyApplied) return;

        bool hasSideHandRenderers = false;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            string n = r.gameObject.name;
            if (IsLeftHandLikeName(n) || IsRightHandLikeName(n))
            {
                hasSideHandRenderers = true;
                break;
            }
        }

        if (!hasSideHandRenderers && allowGeneratedHandOnlyMeshes)
        {
            TryBuildHandOnlySkinnedMeshes(true);
        }

        handsOnlyApplied = true;
    }

    private void UpdateHandsOnlyCombatVisibility()
    {
        if (!handsOnlyVisual) return;
        ApplyHandsOnlyVisualIfNeeded();

        AttackHand hand = AttackHand.Both;
        bool combatHands = role == Role.Attacker &&
                           (swipeActive ||
                            windupTriggered ||
                            pendingDir != Dir.None ||
                            attackPhase == AttackPhase.WindupActive ||
                            attackPhase == AttackPhase.SlapActive ||
                            attackPhase == AttackPhase.ReturningAfterWindup ||
                            IsCurrentStateSlap());
        if (combatHands)
        {
            bool verticalAttack = IsVerticalAttackActive();
            if (verticalAttack)
            {
                hand = AttackHand.Both;
            }
            else if (swipeAttackHandLocked)
            {
                hand = swipeAttackHand;
            }
            else if (TryResolveHorizontalAttackHand(out var horizontalHand))
            {
                hand = horizontalHand;
                swipeAttackHand = hand;
                swipeAttackHandLocked = hand != AttackHand.Both;
            }
            else
            {
                hand = DetermineActiveAttackHand();
                if (hand != AttackHand.Both)
                {
                    swipeAttackHand = hand;
                    swipeAttackHandLocked = true;
                }
            }
            if (hand == AttackHand.Both && !verticalAttack)
            {
                hand = GetPreferredAttackHand();
            }
        }

        activeAttackHand = hand;
        ApplyHandsOnlyVisibility(activeAttackHand);
    }

    private void CacheShoulderBonesIfNeeded()
    {
        if (leftUpperArmBone != null && rightUpperArmBone != null) return;
        if (animator == null) return;

        if (leftUpperArmBone == null) leftUpperArmBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (rightUpperArmBone == null) rightUpperArmBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        if (leftUpperArmBone == null || rightUpperArmBone == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (leftUpperArmBone == null &&
                    (n.Contains("leftarm") || n.Contains("left_upperarm") || n.Contains("upperarm_l") || n.Contains("l_upperarm")))
                {
                    leftUpperArmBone = t;
                }
                if (rightUpperArmBone == null &&
                    (n.Contains("rightarm") || n.Contains("right_upperarm") || n.Contains("upperarm_r") || n.Contains("r_upperarm")))
                {
                    rightUpperArmBone = t;
                }
            }
        }
    }

    private void BlendToIdlePose(float fadeSeconds)
    {
        if (animator == null) return;
        ClearWindupReturnTracking();
        if (!string.IsNullOrEmpty(handMoveParam))
        {
            animator.SetFloat(handMoveParam, 0f);
        }

        if (!string.IsNullOrEmpty(idleStateName) && animator.HasState(0, Animator.StringToHash(idleStateName)))
        {
            animator.CrossFadeInFixedTime(idleStateName, Mathf.Max(0.01f, fadeSeconds), 0, 0f, 0f);
        }
    }

    private void EnsureShoulderCaps()
    {
        if (!enableShoulderCaps) return;
        CacheShoulderBonesIfNeeded();
        if (leftUpperArmBone == null || rightUpperArmBone == null) return;

        if (leftShoulderCapRenderer == null)
        {
            leftShoulderCapRenderer = CreateShoulderCap("LeftShoulderCap", leftUpperArmBone, true);
        }
        if (rightShoulderCapRenderer == null)
        {
            rightShoulderCapRenderer = CreateShoulderCap("RightShoulderCap", rightUpperArmBone, false);
        }
    }

    private Renderer CreateShoulderCap(string name, Transform parentBone, bool left)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parentBone, false);
        float radius = Mathf.Max(0.005f, shoulderCapRadius);
        float length = Mathf.Max(radius * 2.2f, shoulderCapLength);
        go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        float sideToCenter = left ? 1f : -1f;
        go.transform.localPosition = new Vector3(
            sideToCenter * shoulderCapInwardOffset,
            shoulderCapUpOffset,
            shoulderCapForwardOffset);

        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (shoulderCapMaterial == null)
            {
                shoulderCapMaterial = BuildShoulderCapMaterial();
            }
            if (shoulderCapMaterial != null) r.sharedMaterial = shoulderCapMaterial;
        }
        return r;
    }

    private Material BuildShoulderCapMaterial()
    {
        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;

        var mat = new Material(shader);
        Color c = Color.white;
        if (shoulderCapTexture == null)
        {
            shoulderCapTexture = BuildShoulderCapTexture();
        }
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", shoulderCapTexture);
        return mat;
    }

    private Texture2D BuildShoulderCapTexture()
    {
        // Fixed 4x4 sleeve-like gray gradient to match reference swatch.
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color top = new Color(0.26f, 0.28f, 0.32f, 1f);
        Color mid = new Color(0.19f, 0.21f, 0.25f, 1f);
        Color bottom = new Color(0.11f, 0.13f, 0.16f, 1f);
        for (int y = 0; y < 4; y++)
        {
            float t = y / 3f;
            Color row = t < 0.5f
                ? Color.Lerp(top, mid, t / 0.5f)
                : Color.Lerp(mid, bottom, (t - 0.5f) / 0.5f);
            for (int x = 0; x < 4; x++)
            {
                tex.SetPixel(x, 3 - y, row);
            }
        }
        tex.Apply();
        return tex;
    }

    private void LockSwipeAttackHandForDir(Dir dir)
    {
        if (dir == Dir.Up || dir == Dir.Down)
        {
            swipeAttackHand = AttackHand.Both;
            swipeAttackHandLocked = false;
            return;
        }

        if (forceDirectionHandForHorizontal && (dir == Dir.Left || dir == Dir.Right))
        {
            if (horizontalSwipeUsesOppositeHand)
            {
                swipeAttackHand = dir == Dir.Left ? AttackHand.Right : AttackHand.Left;
            }
            else
            {
                swipeAttackHand = dir == Dir.Left ? AttackHand.Left : AttackHand.Right;
            }
            swipeAttackHandLocked = true;
            return;
        }

        // Diagonal mapping by side:
        // slap1/slap7 (right side) -> right hand
        // slap3/slap9 (left side)  -> left hand
        if (dir == Dir.UpRight || dir == Dir.DownRight)
        {
            swipeAttackHand = AttackHand.Right;
            swipeAttackHandLocked = true;
            return;
        }
        if (dir == Dir.UpLeft || dir == Dir.DownLeft)
        {
            swipeAttackHand = AttackHand.Left;
            swipeAttackHandLocked = true;
            return;
        }

        swipeAttackHand = GetPreferredAttackHand();
        swipeAttackHandLocked = true;
    }

    private bool IsVerticalAttackActive()
    {
        if (role != Role.Attacker) return false;
        if (currentWindupState == slapUpWindup || currentWindupState == slapErcutWindup)
        {
            return true;
        }
        if (currentSlapState == slapUpSlap || currentSlapState == slapErcutSlap)
        {
            return true;
        }

        if (attackPhase == AttackPhase.WindupActive ||
            attackPhase == AttackPhase.ReturningAfterWindup ||
            attackPhase == AttackPhase.SlapActive)
        {
            if (pendingDir == Dir.Up || pendingDir == Dir.Down)
            {
                return true;
            }
        }
        return false;
    }

    private bool TryResolveHorizontalAttackHand(out AttackHand hand)
    {
        hand = AttackHand.Both;
        if (!forceDirectionHandForHorizontal) return false;

        Dir d = pendingDir;
        if (d == Dir.None)
        {
            if (currentSlapState == slapLeftSlap) d = Dir.Left;
            else if (currentSlapState == slapRightSlap) d = Dir.Right;
            else if (currentWindupState == slapLeftWindup) d = Dir.Left;
            else if (currentWindupState == slapRightWindup) d = Dir.Right;
        }

        if (d != Dir.Left && d != Dir.Right) return false;

        if (horizontalSwipeUsesOppositeHand)
        {
            hand = d == Dir.Left ? AttackHand.Right : AttackHand.Left;
        }
        else
        {
            hand = d == Dir.Left ? AttackHand.Left : AttackHand.Right;
        }
        return true;
    }

    private void ApplyHandsOnlyVisibility(AttackHand hand)
    {
        bool leftVisible = hand != AttackHand.Right;
        bool rightVisible = hand != AttackHand.Left;

        if (generatedLeftHandRenderer != null)
        {
            generatedLeftHandRenderer.enabled = leftVisible;
        }
        if (generatedRightHandRenderer != null)
        {
            generatedRightHandRenderer.enabled = rightVisible;
        }
        if (leftShoulderCapRenderer != null) leftShoulderCapRenderer.enabled = leftVisible && handsOnlyVisual && enableShoulderCaps;
        if (rightShoulderCapRenderer != null) rightShoulderCapRenderer.enabled = rightVisible && handsOnlyVisual && enableShoulderCaps;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r == generatedLeftHandRenderer || r == generatedRightHandRenderer ||
                r == leftShoulderCapRenderer || r == rightShoulderCapRenderer) continue;

            string n = r.gameObject.name;
            if (hand == AttackHand.Both)
            {
                r.enabled = IsHandLikeName(n);
            }
            else if (IsLeftHandLikeName(n))
            {
                r.enabled = leftVisible;
            }
            else if (IsRightHandLikeName(n))
            {
                r.enabled = rightVisible;
            }
            else
            {
                r.enabled = false;
            }
        }
    }

    private void UpdateShoulderCapsVisibility()
    {
        RemoveShoulderCapsIfPresent();
    }

    private void RemoveShoulderCapsIfPresent()
    {
        if (leftShoulderCapRenderer != null)
        {
            var go = leftShoulderCapRenderer.gameObject;
            leftShoulderCapRenderer = null;
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }
        if (rightShoulderCapRenderer != null)
        {
            var go = rightShoulderCapRenderer.gameObject;
            rightShoulderCapRenderer = null;
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        // Cleanup orphan shoulder-cap objects left from previous runs/iterations.
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (!n.Contains("shouldercap")) continue;
            var go = t.gameObject;
            if (go == null || go == gameObject) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    private bool IsHandLikeName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        string name = n.ToLowerInvariant();
        bool handLike =
            name.Contains("hand") ||
            name.Contains("forearm") ||
            (name.Contains("arm") && !name.Contains("armature"));
        if (!handLike) return false;

        bool bodyLike =
            name.Contains("head") ||
            name.Contains("neck") ||
            name.Contains("spine") ||
            name.Contains("hips") ||
            name.Contains("leg") ||
            name.Contains("foot") ||
            name.Contains("toe") ||
            name.Contains("body") ||
            name.Contains("torso");
        return !bodyLike;
    }

    private bool IsLeftHandLikeName(string n)
    {
        if (!IsHandLikeName(n)) return false;
        string name = n.ToLowerInvariant();
        return name.Contains("left") || name.Contains("_l") || name.Contains(" l ");
    }

    private bool IsRightHandLikeName(string n)
    {
        if (!IsHandLikeName(n)) return false;
        string name = n.ToLowerInvariant();
        return name.Contains("right") || name.Contains("_r") || name.Contains(" r ");
    }

    private bool IsHandBoneName(string lowerName)
    {
        bool handBone = lowerName.Contains("hand") ||
                        lowerName.Contains("forearm") ||
                        lowerName.Contains("shoulder") ||
                        lowerName.Contains("arm") ||
                        lowerName.Contains("thumb") ||
                        lowerName.Contains("index") ||
                        lowerName.Contains("middle") ||
                        lowerName.Contains("ring") ||
                        lowerName.Contains("pinky");
        bool bodyBone = lowerName.Contains("spine") ||
                        lowerName.Contains("hips") ||
                        lowerName.Contains("head") ||
                        lowerName.Contains("neck") ||
                        lowerName.Contains("leg") ||
                        lowerName.Contains("foot") ||
                        lowerName.Contains("toe");
        return handBone && !bodyBone;
    }

    private bool BoneNameIsLeft(string lowerName)
    {
        return lowerName.Contains("left") || lowerName.Contains("_l") || lowerName.Contains(" l ");
    }

    private bool BoneNameIsRight(string lowerName)
    {
        return lowerName.Contains("right") || lowerName.Contains("_r") || lowerName.Contains(" r ");
    }

    public bool TryGetHandOnlySkinnedRenderers(out SkinnedMeshRenderer left, out SkinnedMeshRenderer right)
    {
        left = generatedLeftHandRenderer as SkinnedMeshRenderer;
        right = generatedRightHandRenderer as SkinnedMeshRenderer;
        if (left != null && right != null) return true;

        if (!allowGeneratedHandOnlyMeshes) return false;
        TryBuildHandOnlySkinnedMeshes(false);
        left = generatedLeftHandRenderer as SkinnedMeshRenderer;
        right = generatedRightHandRenderer as SkinnedMeshRenderer;
        return left != null && right != null;
    }

    private void RemoveGeneratedHandOnlyMeshesIfPresent()
    {
        if (generatedLeftHandRenderer != null)
        {
            var go = generatedLeftHandRenderer.gameObject;
            generatedLeftHandRenderer = null;
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }
        if (generatedRightHandRenderer != null)
        {
            var go = generatedRightHandRenderer.gameObject;
            generatedRightHandRenderer = null;
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }
    }

    private void TryBuildHandOnlySkinnedMeshes(bool disableOriginalRenderers)
    {
        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs == null || smrs.Length == 0) return;

        foreach (var source in smrs)
        {
            if (source == null || source.sharedMesh == null) continue;
            if (!source.sharedMesh.isReadable) continue;
            if (source.bones == null || source.bones.Length == 0) continue;

            var leftBones = new HashSet<int>();
            var rightBones = new HashSet<int>();
            for (int i = 0; i < source.bones.Length; i++)
            {
                var b = source.bones[i];
                if (b == null) continue;
                string n = b.name.ToLowerInvariant();
                if (!IsHandBoneName(n)) continue;
                if (BoneNameIsLeft(n)) leftBones.Add(i);
                else if (BoneNameIsRight(n)) rightBones.Add(i);
            }
            if (leftBones.Count == 0 || rightBones.Count == 0) continue;

            Mesh leftMesh = BuildHandOnlyMesh(source, leftBones, "_LeftHandOnly");
            Mesh rightMesh = BuildHandOnlyMesh(source, rightBones, "_RightHandOnly");
            if (leftMesh == null || rightMesh == null) continue;

            if (disableOriginalRenderers)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null) r.enabled = false;
                }
            }

            generatedLeftHandRenderer = CreateHandRenderer(source, "LeftHandOnlyMesh", leftMesh);
            generatedRightHandRenderer = CreateHandRenderer(source, "RightHandOnlyMesh", rightMesh);
            return;
        }
    }

    private Mesh BuildHandOnlyMesh(SkinnedMeshRenderer source, HashSet<int> allowedBones, string suffix)
    {
        Mesh src = source.sharedMesh;
        if (src == null || src.vertexCount <= 0) return null;
        if (src.boneWeights == null || src.boneWeights.Length != src.vertexCount) return null;

        var keepVertex = new bool[src.vertexCount];
        var boneWeights = src.boneWeights;
        for (int i = 0; i < boneWeights.Length; i++)
        {
            BoneWeight bw = boneWeights[i];
            float handWeight = 0f;
            if (allowedBones.Contains(bw.boneIndex0)) handWeight += bw.weight0;
            if (allowedBones.Contains(bw.boneIndex1)) handWeight += bw.weight1;
            if (allowedBones.Contains(bw.boneIndex2)) handWeight += bw.weight2;
            if (allowedBones.Contains(bw.boneIndex3)) handWeight += bw.weight3;
            keepVertex[i] = handWeight >= 0.45f;
        }

        int[] tris = src.triangles;
        if (tris == null || tris.Length < 3) return null;
        var used = new HashSet<int>();
        var keptTri = new List<int>(tris.Length);
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            int a = tris[i];
            int b = tris[i + 1];
            int c = tris[i + 2];
            if (!keepVertex[a] || !keepVertex[b] || !keepVertex[c]) continue;
            keptTri.Add(a);
            keptTri.Add(b);
            keptTri.Add(c);
            used.Add(a);
            used.Add(b);
            used.Add(c);
        }
        if (keptTri.Count < 30) return null;

        var map = new Dictionary<int, int>(used.Count);
        var srcVerts = src.vertices;
        var srcNormals = src.normals;
        var srcTangents = src.tangents;
        var srcUv = src.uv;

        var newVerts = new List<Vector3>(used.Count);
        var newNormals = (srcNormals != null && srcNormals.Length == srcVerts.Length) ? new List<Vector3>(used.Count) : null;
        var newTangents = (srcTangents != null && srcTangents.Length == srcVerts.Length) ? new List<Vector4>(used.Count) : null;
        var newUv = (srcUv != null && srcUv.Length == srcVerts.Length) ? new List<Vector2>(used.Count) : null;
        var newBw = new List<BoneWeight>(used.Count);

        foreach (int oldIndex in used)
        {
            int newIndex = newVerts.Count;
            map[oldIndex] = newIndex;
            newVerts.Add(srcVerts[oldIndex]);
            if (newNormals != null) newNormals.Add(srcNormals[oldIndex]);
            if (newTangents != null) newTangents.Add(srcTangents[oldIndex]);
            if (newUv != null) newUv.Add(srcUv[oldIndex]);
            newBw.Add(boneWeights[oldIndex]);
        }

        var remapTris = new int[keptTri.Count];
        for (int i = 0; i < keptTri.Count; i++)
        {
            remapTris[i] = map[keptTri[i]];
        }

        var handMesh = new Mesh();
        handMesh.name = source.sharedMesh.name + suffix;
        handMesh.SetVertices(newVerts);
        if (newNormals != null) handMesh.SetNormals(newNormals);
        if (newTangents != null) handMesh.SetTangents(newTangents);
        if (newUv != null) handMesh.SetUVs(0, newUv);
        handMesh.boneWeights = newBw.ToArray();
        handMesh.bindposes = src.bindposes;
        handMesh.SetTriangles(remapTris, 0, true);
        if (newNormals == null) handMesh.RecalculateNormals();
        handMesh.RecalculateBounds();
        return handMesh;
    }

    private Renderer CreateHandRenderer(SkinnedMeshRenderer source, string goName, Mesh mesh)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(source.transform, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = source.bones;
        smr.rootBone = source.rootBone;
        if (source.sharedMaterials != null && source.sharedMaterials.Length > 0)
        {
            smr.sharedMaterial = source.sharedMaterials[0];
        }
        return smr;
    }
}

