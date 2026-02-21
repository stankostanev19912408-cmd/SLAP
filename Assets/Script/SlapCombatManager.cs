using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class SlapCombatManager : MonoBehaviour
{
    public static SlapCombatManager Instance { get; private set; }

    [Header("Stamina Drain")]
    [SerializeField] private float staminaDrainSeconds = 10f;

    [Header("Actors")]
    [SerializeField] private string playerName = "idle";
    [SerializeField] private string aiName = "idle (1)";
    [Header("Start")]
    [SerializeField] private bool requireTapToStart = true;
    [Header("Camera")]
    [SerializeField] private bool attachMainCameraToIdleHead = true;
    [SerializeField] private Vector3 idleHeadCameraLocalOffset = new Vector3(0f, 0.0255f, 0.08f);
    [SerializeField] private Vector3 idleHeadCameraLocalEuler = Vector3.zero;
    [SerializeField, Range(0f, 1f)] private float idleHeadCameraRotationInfluence = 0.05f;
    [SerializeField, Range(0f, 1f)] private float idleHeadCameraDiagonalInfluenceMultiplier = 0.38475f;
    [SerializeField, Range(0f, 1f)] private float idleHeadCameraVerticalInfluenceMultiplier = 0.85f;
    [SerializeField] private float idleHeadCameraFovMultiplier = 1.2f;
    [Header("Edit Hands")]
    [SerializeField] private bool createHandsInPlay = true;
    [SerializeField] private float editHandsHeight = 1.35f;
    [SerializeField, Range(0f, 1f)] private float editHandsBetween01 = 0.5f;
    [SerializeField] private float editHandsTowardPlayer = 0.12f;
    [SerializeField] private float editHandsSideOffset = 0.18f;
    [SerializeField] private bool autoPositionEditHands = false;

    [Header("Damage")]
    [SerializeField] private float damagePercentScale = 10f;

    [Header("AI Mode")]
    [SerializeField] private bool advancedAIEnabled = true;
    [Header("Round Start")]
    [SerializeField] private bool playFirstAttackerIntro = true;
    [SerializeField] private string firstAttackerIntroName = "AKIROmodel@Arm Stretching";
    [SerializeField] private float firstAttackerIntroFallbackSeconds = 1.2f;
    [SerializeField] private float firstAttackerIntroSpeedMultiplier = 1.7f;
    [SerializeField] private float firstAttackerIntroPostDelaySeconds = 0f;
    [SerializeField] private bool playFirstDefenderIntro = true;
    [SerializeField] private string firstDefenderIntroName = "AKIROmodel@Neck Stretching";
    [SerializeField] private float firstDefenderIntroFallbackSeconds = 1.2f;
    [Header("Hit Reaction")]
    [SerializeField] private bool playMissedRightBlockReaction = true;
    [SerializeField] private string missedRightBlockReactionName = "AKIROmodel@Falling Forward Death";
    [SerializeField] private string missedLeftBlockReactionName = "AKIROmodel@Falling Forward Death (1)";
    [SerializeField] private string missedSlapUpBlockReactionName = "AKIROmodel@Dying 1";
    [SerializeField] private string missedSlapErcutBlockReactionName = "AKIROmodel@Dyingslapercut";
    [SerializeField] private float missedRightBlockReactionFallbackSeconds = 1.2f;
    [Header("Hit Resolve Timing")]
    [SerializeField] private bool usePerDirectionHitResolveTiming = true;
    [SerializeField, Range(0.5f, 0.98f)] private float blockedHitResolveProgress = 0.60f;
    [SerializeField, Range(0.5f, 0.98f)] private float hitResolveProgressSide = 0.80f;
    [SerializeField, Range(0.5f, 0.98f)] private float hitResolveProgressDiagonal = 0.80f;
    [SerializeField, Range(0.5f, 0.98f)] private float hitResolveProgressUp = 0.80f;
    [SerializeField, Range(0.5f, 0.98f)] private float hitResolveProgressDown = 0.80f;
    [SerializeField, Range(0.5f, 0.98f)] private float legacyHitResolveProgress = 0.80f;

    [Header("Perfect Block")]
    [SerializeField] private float perfectBlockMaxHoldSeconds = 1f;
    [SerializeField] private float perfectBlockMinHold01 = 0.98f;
    [SerializeField] private float perfectTextSeconds = 0.8f;

    private SlapMechanics player;
    private SlapMechanics ai;
    private SlapAIController aiController;
    private bool lastAppliedAdvancedAIEnabled;
    private CombatantStats playerStats;
    private CombatantStats aiStats;
    private bool playerTurn = true;
    private bool pendingTurnSwitch;
    private SlapMechanics pendingAttacker;
    private bool pendingFlip;
    private float aiAttackDelayTimer;
    private bool firstAttackerIntroPlayed;
    private bool introSequenceActive;
    private float introSequenceUntilTime = -1f;
    private bool playerInputBeforeIntro;
    private bool battleStarted;
    private AnimationClip resolvedFirstAttackerIntroClip;
    private AnimationClip resolvedFirstDefenderIntroClip;
    private AnimationClip resolvedMissedRightBlockReactionClip;
    private AnimationClip resolvedMissedLeftBlockReactionClip;
    private AnimationClip resolvedMissedSlapUpBlockReactionClip;
    private AnimationClip resolvedMissedSlapErcutBlockReactionClip;

    private float perfectTimer;
    private bool gameOver;
    private GUIStyle perfectStyle;
    private GUIStyle gameOverStyle;
    private GUIStyle timerStyle;
    private GUIStyle turnStyle;
    private GUIStyle hitStyle;
    private GUIStyle startButtonStyle;
    private string hitMessage;
    private float hitMessageTimer;
    private bool pendingHit;
    private SlapMechanics pendingHitAttacker;
    private SlapMechanics pendingHitDefender;
    private float pendingDamagePercent;
    private SlapMechanics.SlapDirection pendingHitDir;
    private SlapMechanics.SlapDirection pendingBlockedDir;
    private static Texture2D whiteTex;
    private const float playerTurnCountdownSeconds = 3f;
    private Camera cachedMainCamera;
    private Transform idleHeadCameraAnchor;
    private bool cachedMainCameraBaseFovSet;
    private float cachedMainCameraBaseFov;
    private Transform editHandsRoot;
    private Transform editPlayerHandsRoot;
    private Transform editOpponentHandsRoot;
    private SkinnedMeshRenderer editPlayerLeftSource;
    private SkinnedMeshRenderer editPlayerRightSource;
    private SkinnedMeshRenderer editOpponentLeftSource;
    private SkinnedMeshRenderer editOpponentRightSource;
    private MeshFilter editPlayerLeftFilter;
    private MeshFilter editPlayerRightFilter;
    private MeshFilter editOpponentLeftFilter;
    private MeshFilter editOpponentRightFilter;
    private MeshRenderer editPlayerLeftRenderer;
    private MeshRenderer editPlayerRightRenderer;
    private MeshRenderer editOpponentLeftRenderer;
    private MeshRenderer editOpponentRightRenderer;
    private Mesh editPlayerLeftMesh;
    private Mesh editPlayerRightMesh;
    private Mesh editOpponentLeftMesh;
    private Mesh editOpponentRightMesh;
    private Transform editPlayerOwnerRoot;
    private Transform editOpponentOwnerRoot;
    private bool pendingEnsureHandsHierarchy;
    private Transform playerLeftProxyVisual;
    private Transform playerRightProxyVisual;
    private Transform opponentLeftProxyVisual;
    private Transform opponentRightProxyVisual;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("SlapCombatManager");
        Instance = go.AddComponent<SlapCombatManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        EnsureSceneProxyRig();
        EnsureHandsCopyHierarchyOnly();

        if (!Application.isPlaying)
        {
            if (Instance == null) Instance = this;
            return;
        }

        createHandsInPlay = true;
        autoPositionEditHands = true;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureWhiteTex();
        ResolveFirstAttackerIntroClip();
        ResolveFirstDefenderIntroClip();
        ResolveMissedRightBlockReactionClip();
        ResolveMissedLeftBlockReactionClip();
        ResolveMissedSlapUpBlockReactionClip();
        ResolveMissedSlapErcutBlockReactionClip();
        FindActors();
        if (!requireTapToStart)
        {
            StartBattle();
        }
        else
        {
            battleStarted = false;
            if (player != null) player.allowHumanInput = false;
        }
        lastAppliedAdvancedAIEnabled = advancedAIEnabled;
        EnsureHandsCopyHierarchyOnly();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            SlapMechanics.OnSlapFired += HandleSlapFired;
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SlapMechanics.OnSlapFired -= HandleSlapFired;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        pendingEnsureHandsHierarchy = true;
        AutoBindEditHandsRootsIfMissing();
    }

    private void Update()
    {
        EnsureSceneProxyRig();
        if (pendingEnsureHandsHierarchy)
        {
            pendingEnsureHandsHierarchy = false;
            EnsureHandsCopyHierarchyOnly();
        }

        if (!Application.isPlaying)
        {
            UpdateEditHandsCopies();
            return;
        }

        if (player == null || ai == null)
        {
            FindActors();
        }

        if (!battleStarted)
        {
            if (player != null) player.allowHumanInput = false;
            aiAttackDelayTimer = 0f;
            UpdateEditHandsCopies();
            return;
        }

        UpdateIntroSequence();
        if (!gameOver)
        {
            DrainStamina();
        }
        if (!gameOver)
        {
            if ((playerStats != null && playerStats.Health <= 0f) ||
                (aiStats != null && aiStats.Health <= 0f))
            {
                gameOver = true;
            }
        }
        EnsureAuraVisibility();
        if (perfectTimer > 0f)
        {
            perfectTimer = Mathf.Max(0f, perfectTimer - Time.deltaTime);
        }
        if (aiAttackDelayTimer > 0f)
        {
            aiAttackDelayTimer = Mathf.Max(0f, aiAttackDelayTimer - Time.deltaTime);
        }
        if (hitMessageTimer > 0f)
        {
            hitMessageTimer = Mathf.Max(0f, hitMessageTimer - Time.deltaTime);
            if (hitMessageTimer <= 0f)
            {
                hitMessage = null;
            }
        }
        if (pendingTurnSwitch)
        {
            if (pendingAttacker == null ||
                !pendingAttacker.IsSlapAnimating())
            {
                if (pendingFlip)
                {
                    playerTurn = !playerTurn;
                }
                pendingTurnSwitch = false;
                pendingAttacker = null;
                pendingFlip = false;
                ApplyTurnRoles();
            }
        }
        if (pendingHit)
        {
            if (pendingHitDefender != null && pendingHitDefender.IsDefenderBlocking())
            {
                pendingBlockedDir = pendingHitDefender.GetCurrentBlockDirection();
            }
            ResolvePendingHit();
        }
        if (aiController != null && lastAppliedAdvancedAIEnabled != advancedAIEnabled)
        {
            aiController.SetAdvancedAIEnabled(advancedAIEnabled);
            lastAppliedAdvancedAIEnabled = advancedAIEnabled;
        }

        UpdateEditHandsCopies();
    }

    private void LateUpdate()
    {
        UpdateMainCameraHeadFollow();
    }

    private void HandleSlapFired(SlapMechanics source, SlapMechanics.SlapEvent data)
    {
        if (gameOver) return;
        FindActors();
        var attacker = GetCurrentAttacker();
        var defender = GetCurrentDefender();
        if (attacker == null || defender == null) return;
        if (source != attacker) return;

        float windup01 = Mathf.Clamp01(data.windup01);
        float slapPower01 = Mathf.Clamp01(data.slapPower01);
        float baseDamagePercent = (windup01 + slapPower01) * damagePercentScale;
        pendingHit = true;
        pendingHitAttacker = attacker;
        pendingHitDefender = defender;
        pendingDamagePercent = baseDamagePercent;
        pendingHitDir = data.direction;
        pendingBlockedDir = SlapMechanics.SlapDirection.None;

        if (gameOver) return;
    }

    private bool IsPerfectBlock(SlapMechanics def, SlapMechanics.SlapDirection attackDir)
    {
        if (def == null) return false;
        if (attackDir == SlapMechanics.SlapDirection.None) return false;
        if (def.GetCurrentBlockDirection() != MirrorForOpponent(attackDir)) return false;
        if (def.GetDefenderBlockHold01() < perfectBlockMinHold01) return false;
        if (def.GetDefenderBlockHoldSeconds() > perfectBlockMaxHoldSeconds) return false;
        return true;
    }

    private bool IsBlock(SlapMechanics def, SlapMechanics.SlapDirection attackDir)
    {
        if (def == null) return false;
        if (attackDir == SlapMechanics.SlapDirection.None) return false;
        if (def.GetCurrentBlockDirection() != MirrorForOpponent(attackDir)) return false;
        return def.IsDefenderBlocking();
    }

    private void SetHitMessage(SlapMechanics attacker, SlapMechanics defender, bool blocked)
    {
        if (attacker == null || defender == null) return;
        string msg = null;
        if (blocked && defender == player)
        {
            msg = "YOU BLOCK HIM";
        }
        else if (blocked)
        {
            msg = "BLOCK";
        }
        else if (attacker == player)
        {
            msg = "SLAP";
        }
        else
        {
            msg = "BOOM";
        }
        if (!string.IsNullOrEmpty(msg))
        {
            hitMessage = msg;
            hitMessageTimer = 1.0f;
        }
    }

    private void ResolvePendingHit()
    {
        if (pendingHitAttacker == null || pendingHitDefender == null)
        {
            pendingHit = false;
            return;
        }

        bool blocked = false;
        if (pendingHitDefender != null)
        {
            var expected = MirrorForOpponent(pendingHitDir);
            if (pendingHitDefender.IsDefenderBlocking() &&
                pendingHitDefender.GetCurrentBlockDirection() == expected)
            {
                blocked = true;
            }
        }
        bool perfect = IsPerfectBlock(pendingHitDefender, pendingHitDir);
        float slapProgress = pendingHitAttacker.GetSlapProgress01();
        float resolveAtProgress = GetResolveAtProgress(pendingHitDir, blocked || perfect);
        if (slapProgress < resolveAtProgress)
        {
            return;
        }

        if (blocked || perfect)
        {
            pendingHitAttacker.InterruptSlapForSuccessfulBlock(blockedHitResolveProgress);
        }
        float damagePercent = pendingDamagePercent;

        if (perfect || blocked)
        {
            damagePercent = 0f;
            if (perfect)
            {
                perfectTimer = Mathf.Max(perfectTimer, perfectTextSeconds);
            }
        }

        var attackerStats = GetStats(pendingHitAttacker);
        var defenderStats = GetStats(pendingHitDefender);
        if (attackerStats != null && attackerStats.Stamina <= 0f && attackerStats.Health > 0f)
        {
            damagePercent *= 0.5f;
        }

        if (defenderStats != null)
        {
            float dmg = damagePercent * (defenderStats.MaxHealth / 100f);
            defenderStats.TakeDamage(dmg);
            if (defenderStats.Health <= 0f)
            {
                gameOver = true;
            }
        }
        if (!blocked &&
            !perfect &&
            playMissedRightBlockReaction &&
            pendingHitDefender != null)
        {
            bool rightLikeHit =
                pendingHitDir == SlapMechanics.SlapDirection.Right ||
                pendingHitDir == SlapMechanics.SlapDirection.DownRight ||
                pendingHitDir == SlapMechanics.SlapDirection.UpRight;
            bool leftLikeHit =
                pendingHitDir == SlapMechanics.SlapDirection.Left ||
                pendingHitDir == SlapMechanics.SlapDirection.DownLeft ||
                pendingHitDir == SlapMechanics.SlapDirection.UpLeft;
            bool slapUpHit = pendingHitDir == SlapMechanics.SlapDirection.Up;
            bool slapErcutHit = pendingHitDir == SlapMechanics.SlapDirection.Down;
            if (rightLikeHit || leftLikeHit || slapUpHit || slapErcutHit)
            {
                AnimationClip reactionClip;
                string reactionName;
                if (slapUpHit)
                {
                    reactionClip = resolvedMissedSlapUpBlockReactionClip;
                    reactionName = missedSlapUpBlockReactionName;
                }
                else if (slapErcutHit)
                {
                    reactionClip = resolvedMissedSlapErcutBlockReactionClip;
                    reactionName = missedSlapErcutBlockReactionName;
                }
                else
                {
                    bool leftHit = leftLikeHit && !rightLikeHit;
                    reactionClip = leftHit ? resolvedMissedLeftBlockReactionClip : resolvedMissedRightBlockReactionClip;
                    reactionName = leftHit ? missedLeftBlockReactionName : missedRightBlockReactionName;
                }
                float reactionLen = reactionClip != null
                    ? pendingHitDefender.PlayOneShotIntro(reactionClip, missedRightBlockReactionFallbackSeconds, false, true)
                    : pendingHitDefender.PlayOneShotIntro(reactionName, missedRightBlockReactionFallbackSeconds, 0.08f, false, true);
                if (reactionLen <= 0.011f)
                {
                    Debug.LogWarning($"[SlapCombat] Reaction '{reactionName}' length is too small. Check animator state and clip.");
                }
                else
                {
                    Debug.Log($"[SlapCombat] Missed-block reaction on '{pendingHitDefender.gameObject.name}', state='{reactionName}', len={reactionLen:0.00}s");
                }
            }
        }

        SetHitMessage(pendingHitAttacker, pendingHitDefender, blocked || perfect);

        pendingHit = false;

        if (gameOver) return;
        pendingTurnSwitch = true;
        pendingAttacker = pendingHitAttacker;
        pendingFlip = true;
    }

    private void FindActors()
    {
        var all = FindObjectsOfType<SlapMechanics>(true);
        player = null;
        ai = null;
        foreach (var s in all)
        {
            if (s == null) continue;
            if (player == null && s.gameObject.name == playerName)
            {
                player = s;
                continue;
            }
            if (ai == null && s.gameObject.name == aiName)
            {
                ai = s;
                continue;
            }
        }

        if (player == null || ai == null)
        {
            foreach (var s in all)
            {
                if (s == null) continue;
                if (player == null)
                {
                    player = s;
                    continue;
                }
                if (ai == null && s != player)
                {
                    ai = s;
                    break;
                }
            }
        }

        if (player != null)
        {
            playerStats = player.GetComponent<CombatantStats>();
            if (playerStats == null) playerStats = player.gameObject.AddComponent<CombatantStats>();
        }
        if (ai != null)
        {
            aiStats = ai.GetComponent<CombatantStats>();
            if (aiStats == null) aiStats = ai.gameObject.AddComponent<CombatantStats>();
            aiController = ai.GetComponent<SlapAIController>();
            if (aiController == null) aiController = ai.gameObject.AddComponent<SlapAIController>();
            aiController.SetCombat(this, ai);
            aiController.SetAdvancedAIEnabled(advancedAIEnabled);
            lastAppliedAdvancedAIEnabled = advancedAIEnabled;
        }

        ResolveIdleHeadCameraAnchor();

        ApplyTurnRoles();
    }

    private void ResolveIdleHeadCameraAnchor()
    {
        idleHeadCameraAnchor = null;
        if (!attachMainCameraToIdleHead) return;
        if (player == null) return;

        var animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            idleHeadCameraAnchor = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        if (idleHeadCameraAnchor != null) return;

        foreach (var t in player.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("head"))
            {
                idleHeadCameraAnchor = t;
                return;
            }
        }
    }

    private void UpdateMainCameraHeadFollow()
    {
        if (!attachMainCameraToIdleHead) return;
        if (cachedMainCamera == null) cachedMainCamera = Camera.main;
        if (cachedMainCamera == null) return;
        if (!cachedMainCameraBaseFovSet)
        {
            cachedMainCameraBaseFov = cachedMainCamera.fieldOfView;
            cachedMainCameraBaseFovSet = true;
        }
        if (player == null) return;
        if (idleHeadCameraAnchor == null) ResolveIdleHeadCameraAnchor();
        if (idleHeadCameraAnchor == null) return;

        cachedMainCamera.transform.position = idleHeadCameraAnchor.TransformPoint(idleHeadCameraLocalOffset);
        Quaternion baseRot = player.transform.rotation * Quaternion.Euler(idleHeadCameraLocalEuler);
        Quaternion headRot = idleHeadCameraAnchor.rotation * Quaternion.Euler(idleHeadCameraLocalEuler);
        float t = Mathf.Clamp01(idleHeadCameraRotationInfluence);
        Quaternion blendedRot = Quaternion.Slerp(baseRot, headRot, t);
        Vector3 baseEuler = baseRot.eulerAngles;
        Vector3 blendedEuler = blendedRot.eulerAngles;
        float diag = Mathf.Clamp01(idleHeadCameraDiagonalInfluenceMultiplier);
        float vertical = Mathf.Clamp01(idleHeadCameraVerticalInfluenceMultiplier);
        float dx = Mathf.DeltaAngle(baseEuler.x, blendedEuler.x) * diag * vertical;
        float dy = Mathf.DeltaAngle(baseEuler.y, blendedEuler.y) * diag;
        blendedEuler.x = baseEuler.x + dx;
        blendedEuler.y = baseEuler.y + dy;
        cachedMainCamera.transform.rotation = Quaternion.Euler(blendedEuler);
        cachedMainCamera.fieldOfView = Mathf.Clamp(cachedMainCameraBaseFov * Mathf.Max(0.1f, idleHeadCameraFovMultiplier), 1f, 179f);
    }

    private void UpdateEditHandsCopies()
    {
        if (Application.isPlaying && !createHandsInPlay) return;
        EnsureHandsCopyHierarchyOnly();

        if (!TryFindEditActors(out var playerSource, out var opponentSource))
        {
            TryResolveHandSourcesFromSceneFallback();
            RefreshEditHandCopiesSkinned();
            UpdateVisibleProxyHands();
            return;
        }

        bool playerOk = EnsureEditHandsPair(playerSource, true);
        bool opponentOk = EnsureEditHandsPair(opponentSource, false);
        if (!playerOk || !opponentOk)
        {
            if (!TryResolveHandSourcesFromSceneFallback()) return;
        }
        if (autoPositionEditHands || Application.isPlaying)
        {
            UpdateEditHandsPose(playerSource.transform, opponentSource.transform);
        }
        if (autoPositionEditHands || Application.isPlaying)
        {
            if (editPlayerOwnerRoot == null) editPlayerOwnerRoot = playerSource.transform;
            if (editOpponentOwnerRoot == null) editOpponentOwnerRoot = opponentSource.transform;
        }
        RefreshEditHandCopiesBakedMeshes();
        RefreshEditHandCopiesSkinned();
        UpdateVisibleProxyHands();
    }

    private bool TryResolveHandSourcesFromSceneFallback()
    {
        var all = FindObjectsOfType<SkinnedMeshRenderer>(true);
        if (all == null || all.Length == 0) return false;

        var left = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        var right = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null || r.sharedMesh == null) continue;
            if (string.Equals(r.gameObject.name, "LeftHandOnlyMesh", System.StringComparison.Ordinal))
            {
                left.Add(r);
            }
            else if (string.Equals(r.gameObject.name, "RightHandOnlyMesh", System.StringComparison.Ordinal))
            {
                right.Add(r);
            }
        }
        if (left.Count == 0 || right.Count == 0) return false;

        Transform playerOwner = FindTransformByExactName(playerName);
        Transform opponentOwner = FindTransformByExactName(aiName);

        editPlayerLeftSource = PickNearest(left, playerOwner);
        editPlayerRightSource = PickNearest(right, playerOwner);

        var leftForOpponent = new System.Collections.Generic.List<SkinnedMeshRenderer>(left);
        leftForOpponent.Remove(editPlayerLeftSource);
        var rightForOpponent = new System.Collections.Generic.List<SkinnedMeshRenderer>(right);
        rightForOpponent.Remove(editPlayerRightSource);

        editOpponentLeftSource = PickNearest(leftForOpponent.Count > 0 ? leftForOpponent : left, opponentOwner);
        editOpponentRightSource = PickNearest(rightForOpponent.Count > 0 ? rightForOpponent : right, opponentOwner);

        if (editPlayerLeftSource == null || editPlayerRightSource == null ||
            editOpponentLeftSource == null || editOpponentRightSource == null)
        {
            return false;
        }

        editPlayerOwnerRoot = playerOwner != null ? playerOwner : editPlayerLeftSource.transform.root;
        editOpponentOwnerRoot = opponentOwner != null ? opponentOwner : editOpponentLeftSource.transform.root;

        return true;
    }

    private static Transform FindTransformByExactName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var all = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (string.Equals(t.name, exactName, System.StringComparison.Ordinal)) return t;
        }
        return null;
    }

    private static SkinnedMeshRenderer PickNearest(System.Collections.Generic.List<SkinnedMeshRenderer> list, Transform owner)
    {
        if (list == null || list.Count == 0) return null;
        if (owner == null) return list[0];
        float best = float.MaxValue;
        SkinnedMeshRenderer pick = null;
        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r == null) continue;
            float d = (r.transform.position - owner.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                pick = r;
            }
        }
        return pick ?? list[0];
    }

    private void UpdateVisibleProxyHands()
    {
        UpdateVisibleProxyHandsForOwner(editPlayerHandsRoot, ref playerLeftProxyVisual, ref playerRightProxyVisual, FindTransformByExactName(playerName), new Color(0.95f, 0.75f, 0.62f, 1f));
        UpdateVisibleProxyHandsForOwner(editOpponentHandsRoot, ref opponentLeftProxyVisual, ref opponentRightProxyVisual, FindTransformByExactName(aiName), new Color(0.78f, 0.92f, 1f, 1f));
    }

    private void UpdateVisibleProxyHandsForOwner(
        Transform copyRoot,
        ref Transform leftProxy,
        ref Transform rightProxy,
        Transform owner,
        Color color)
    {
        if (copyRoot == null || owner == null) return;
        Transform left = copyRoot.Find("Left");
        Transform right = copyRoot.Find("Right");
        if (left == null || right == null) return;

        if (!HasRenderable(left))
        {
            leftProxy = EnsureProxyVisual(left, "ProxyVisual", color);
        }
        if (!HasRenderable(right))
        {
            rightProxy = EnsureProxyVisual(right, "ProxyVisual", color);
        }

        ResolveOwnerHandTransforms(owner, out var leftHand, out var rightHand);
        PlaceProxy(left, leftHand, owner, true);
        PlaceProxy(right, rightHand, owner, false);
    }

    private static bool HasRenderable(Transform t)
    {
        if (t == null) return false;
        if (t.GetComponent<SkinnedMeshRenderer>() != null) return true;
        if (t.GetComponent<MeshRenderer>() != null && t.GetComponent<MeshFilter>() != null) return true;
        return t.GetComponentInChildren<Renderer>(true) != null;
    }

    private static Transform EnsureProxyVisual(Transform parent, string name, Color color)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = true;
        }
        return go.transform;
    }

    private static void ResolveOwnerHandTransforms(Transform owner, out Transform leftHand, out Transform rightHand)
    {
        leftHand = null;
        rightHand = null;
        if (owner == null) return;

        var animator = owner.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (leftHand == null || rightHand == null)
        {
            var all = owner.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                string n = t.name.ToLowerInvariant();
                if (leftHand == null && n.Contains("lefthand")) leftHand = t;
                if (rightHand == null && n.Contains("righthand")) rightHand = t;
                if (leftHand != null && rightHand != null) break;
            }
        }
    }

    private static void PlaceProxy(Transform proxyRoot, Transform hand, Transform owner, bool left)
    {
        if (proxyRoot == null || owner == null) return;
        Vector3 pos;
        Quaternion rot;
        if (hand != null)
        {
            pos = hand.position + owner.forward * 0.12f;
            rot = hand.rotation;
        }
        else
        {
            float side = left ? -0.18f : 0.18f;
            pos = owner.position + owner.up * 1.2f + owner.forward * 0.2f + owner.right * side;
            rot = owner.rotation;
        }
        proxyRoot.position = pos;
        proxyRoot.rotation = rot;
    }

    private bool TryFindEditActors(out SlapMechanics playerSource, out SlapMechanics opponentSource)
    {
        playerSource = player;
        opponentSource = ai;
        if (playerSource != null && opponentSource != null && playerSource != opponentSource)
        {
            return true;
        }

        playerSource = null;
        opponentSource = null;

        var all = FindObjectsOfType<SlapMechanics>(true);
        if (all == null || all.Length == 0) return false;

        // Hard mapping for edit copies:
        // PlayerHands_Copy  <- "idle"
        // OpponentHands_Copy <- "idle (1)"
        for (int i = 0; i < all.Length; i++)
        {
            var s = all[i];
            if (s == null) continue;
            if (playerSource == null && !string.IsNullOrEmpty(playerName) && string.Equals(s.gameObject.name, playerName, System.StringComparison.Ordinal))
            {
                playerSource = s;
                continue;
            }
            if (opponentSource == null && !string.IsNullOrEmpty(aiName) && string.Equals(s.gameObject.name, aiName, System.StringComparison.Ordinal))
            {
                opponentSource = s;
                continue;
            }
        }

        // Fallback only if one of named actors is truly missing in scene.
        if (playerSource == null || opponentSource == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                if (playerSource == null)
                {
                    playerSource = s;
                    continue;
                }
                if (opponentSource == null && s != playerSource)
                {
                    opponentSource = s;
                    break;
                }
            }
        }

        if (playerSource == null || opponentSource == null) return false;
        if (playerSource == opponentSource) return false;
        return true;
    }

    private bool EnsureEditHandsPair(SlapMechanics source, bool isPlayer)
    {
        if (source == null) return false;
        if (!TryResolveHandSourcesForEdit(source, out var leftSource, out var rightSource)) return false;

        if (!EnsureEditHandsHierarchy()) return false;
        if (isPlayer && editPlayerHandsRoot == null) return false;
        if (!isPlayer && editOpponentHandsRoot == null) return false;

        if (isPlayer)
        {
            editPlayerOwnerRoot = source.transform;
            editPlayerLeftSource = leftSource;
            editPlayerRightSource = rightSource;
            EnsureEditHandCopy(editPlayerHandsRoot, "Left", leftSource, ref editPlayerLeftFilter, ref editPlayerLeftRenderer, ref editPlayerLeftMesh);
            EnsureEditHandCopy(editPlayerHandsRoot, "Right", rightSource, ref editPlayerRightFilter, ref editPlayerRightRenderer, ref editPlayerRightMesh);
        }
        else
        {
            editOpponentOwnerRoot = source.transform;
            editOpponentLeftSource = leftSource;
            editOpponentRightSource = rightSource;
            EnsureEditHandCopy(editOpponentHandsRoot, "Left", leftSource, ref editOpponentLeftFilter, ref editOpponentLeftRenderer, ref editOpponentLeftMesh);
            EnsureEditHandCopy(editOpponentHandsRoot, "Right", rightSource, ref editOpponentRightFilter, ref editOpponentRightRenderer, ref editOpponentRightMesh);
        }
        return true;
    }

    private bool TryResolveHandSourcesForEdit(SlapMechanics source, out SkinnedMeshRenderer leftSource, out SkinnedMeshRenderer rightSource)
    {
        leftSource = null;
        rightSource = null;
        if (source == null) return false;

        if (source.TryGetHandOnlySkinnedRenderers(out leftSource, out rightSource) &&
            leftSource != null && rightSource != null)
        {
            return true;
        }

        // Fallback: if hand-only mesh build failed on this fighter, reuse any visible skinned meshes.
        // This keeps two visible copies in edit mode instead of hiding everything.
        var smrs = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < smrs.Length; i++)
        {
            var r = smrs[i];
            if (r == null || r.sharedMesh == null) continue;
            string n = r.name.ToLowerInvariant();
            if (leftSource == null && (n.Contains("left") || n.Contains("_l") || n.Contains("l_")))
            {
                leftSource = r;
            }
            if (rightSource == null && (n.Contains("right") || n.Contains("_r") || n.Contains("r_")))
            {
                rightSource = r;
            }
        }
        if (leftSource == null || rightSource == null)
        {
            for (int i = 0; i < smrs.Length; i++)
            {
                var r = smrs[i];
                if (r == null || r.sharedMesh == null) continue;
                if (leftSource == null) leftSource = r;
                else if (rightSource == null) rightSource = r;
                if (leftSource != null && rightSource != null) break;
            }
        }

        if (leftSource == null || rightSource == null)
        {
            foreach (var t in source.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string tn = t.name;
                if (leftSource == null && string.Equals(tn, "LeftHandOnlyMesh", System.StringComparison.Ordinal))
                {
                    leftSource = t.GetComponent<SkinnedMeshRenderer>();
                }
                if (rightSource == null && string.Equals(tn, "RightHandOnlyMesh", System.StringComparison.Ordinal))
                {
                    rightSource = t.GetComponent<SkinnedMeshRenderer>();
                }
                if (leftSource != null && rightSource != null) break;
            }
        }

        if (leftSource == null || rightSource == null)
        {
            TryResolveHandSourcesGlobal(source.transform, ref leftSource, ref rightSource);
        }
        return leftSource != null && rightSource != null;
    }

    private void TryResolveHandSourcesGlobal(Transform owner, ref SkinnedMeshRenderer leftSource, ref SkinnedMeshRenderer rightSource)
    {
        if (owner == null) return;

        var allSmr = FindObjectsOfType<SkinnedMeshRenderer>(true);
        float bestLeft = float.MaxValue;
        float bestRight = float.MaxValue;

        for (int i = 0; i < allSmr.Length; i++)
        {
            var r = allSmr[i];
            if (r == null || r.sharedMesh == null) continue;
            string n = r.gameObject.name;
            if (!string.Equals(n, "LeftHandOnlyMesh", System.StringComparison.Ordinal) &&
                !string.Equals(n, "RightHandOnlyMesh", System.StringComparison.Ordinal))
            {
                continue;
            }

            float d = (r.transform.position - owner.position).sqrMagnitude;
            if (string.Equals(n, "LeftHandOnlyMesh", System.StringComparison.Ordinal))
            {
                if (leftSource == null || d < bestLeft)
                {
                    leftSource = r;
                    bestLeft = d;
                }
            }
            else
            {
                if (rightSource == null || d < bestRight)
                {
                    rightSource = r;
                    bestRight = d;
                }
            }
        }
    }

    private bool EnsureEditHandsHierarchy()
    {
        EnsureHandsCopyHierarchyOnly();
        return false;
    }

    private void EnsureHandsCopyHierarchyOnly()
    {
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!string.Equals(t.name, "FirstPersonHandsCopies", System.StringComparison.Ordinal)) continue;
            if (Application.isPlaying)
            {
                Destroy(t.gameObject);
            }
            else
            {
#if UNITY_EDITOR
                if (Selection.activeTransform == t || Selection.Contains(t.gameObject))
                {
                    Selection.activeGameObject = gameObject;
                }
#endif
                Destroy(t.gameObject);
            }
        }
        editHandsRoot = null;
        editPlayerHandsRoot = null;
        editOpponentHandsRoot = null;
    }

    private static Transform EnsureNamedHandChild(Transform parent, string name)
    {
        if (parent == null) return null;
        var t = parent.Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        t = go.transform;
        t.SetParent(parent, false);
        return t;
    }

    private void EnsureSceneProxyRig()
    {
        if (GetComponent<SceneHandProxyRig>() != null) return;
        gameObject.AddComponent<SceneHandProxyRig>();
    }

    private void AutoBindEditHandsRootsIfMissing()
    {
    }

    private void EnsureEditHandCopy(
        Transform parent,
        string name,
        SkinnedMeshRenderer source,
        ref MeshFilter filter,
        ref MeshRenderer renderer,
        ref Mesh bakedMesh)
    {
        if (parent == null || source == null) return;

        Transform child = ResolveExistingEditHandChild(parent, name);
        if (child == null) return;

        filter = child.GetComponent<MeshFilter>();
        if (filter == null) filter = child.gameObject.AddComponent<MeshFilter>();
        renderer = child.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = child.gameObject.AddComponent<MeshRenderer>();

        if (bakedMesh == null)
        {
            bakedMesh = new Mesh { name = source.name + "_" + name + "_EditCopy" };
        }

        source.BakeMesh(bakedMesh);
        filter.sharedMesh = bakedMesh;
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = true;
    }

    private Transform ResolveExistingEditHandChild(Transform parent, string handName)
    {
        if (parent == null) return null;

        var exact = parent.Find(handName);
        if (exact != null) return exact;

        string token = handName.ToLowerInvariant();
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c == null) continue;
            if (c.name.ToLowerInvariant().Contains(token)) return c;
        }

        // Keep user's existing arrangement: only reuse existing children.
        if (parent.childCount > 0)
        {
            int index = handName == "Left" ? 0 : Mathf.Min(1, parent.childCount - 1);
            return parent.GetChild(index);
        }

        var go = new GameObject(handName);
        var t = go.transform;
        t.SetParent(parent, false);
        return t;
    }

    private void RefreshEditHandCopiesBakedMeshes()
    {
        BakeToCopy(editPlayerLeftSource, editPlayerLeftFilter, editPlayerLeftRenderer, ref editPlayerLeftMesh, editPlayerOwnerRoot, editPlayerHandsRoot);
        BakeToCopy(editPlayerRightSource, editPlayerRightFilter, editPlayerRightRenderer, ref editPlayerRightMesh, editPlayerOwnerRoot, editPlayerHandsRoot);
        BakeToCopy(editOpponentLeftSource, editOpponentLeftFilter, editOpponentLeftRenderer, ref editOpponentLeftMesh, editOpponentOwnerRoot, editOpponentHandsRoot);
        BakeToCopy(editOpponentRightSource, editOpponentRightFilter, editOpponentRightRenderer, ref editOpponentRightMesh, editOpponentOwnerRoot, editOpponentHandsRoot);
    }

    private void BakeToCopy(
        SkinnedMeshRenderer source,
        MeshFilter filter,
        MeshRenderer renderer,
        ref Mesh bakedMesh,
        Transform ownerRoot,
        Transform handsRoot)
    {
        if (source == null || filter == null || renderer == null) return;
        if (bakedMesh == null)
        {
            bakedMesh = new Mesh { name = source.name + "_EditCopy" };
        }
        source.BakeMesh(bakedMesh);
        filter.sharedMesh = bakedMesh;
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.enabled = true;

        var copyTf = filter.transform;
        if (copyTf == null) return;

        if (ownerRoot != null && handsRoot != null)
        {
            Vector3 relative = source.transform.position - ownerRoot.position;
            copyTf.position = handsRoot.position + relative;
            copyTf.rotation = source.transform.rotation;
            copyTf.localScale = source.transform.lossyScale;
        }
        else
        {
            copyTf.position = source.transform.position;
            copyTf.rotation = source.transform.rotation;
            copyTf.localScale = source.transform.lossyScale;
        }
    }

    private void RefreshEditHandCopiesSkinned()
    {
        SyncSkinnedCopy(editPlayerHandsRoot, "Left", editPlayerLeftSource, editPlayerOwnerRoot);
        SyncSkinnedCopy(editPlayerHandsRoot, "Right", editPlayerRightSource, editPlayerOwnerRoot);
        SyncSkinnedCopy(editOpponentHandsRoot, "Left", editOpponentLeftSource, editOpponentOwnerRoot);
        SyncSkinnedCopy(editOpponentHandsRoot, "Right", editOpponentRightSource, editOpponentOwnerRoot);
    }

    private void SyncSkinnedCopy(Transform parent, string handName, SkinnedMeshRenderer source, Transform ownerRoot)
    {
        if (parent == null || source == null) return;
        Transform child = ResolveExistingEditHandChild(parent, handName);
        if (child == null) return;

        var mf = child.GetComponent<MeshFilter>();
        if (mf != null)
        {
            if (Application.isPlaying) Destroy(mf);
            else DestroyImmediate(mf);
        }
        var mr = child.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (Application.isPlaying) Destroy(mr);
            else DestroyImmediate(mr);
        }

        var copy = child.GetComponent<SkinnedMeshRenderer>();
        if (copy == null) copy = child.gameObject.AddComponent<SkinnedMeshRenderer>();

        copy.sharedMesh = source.sharedMesh;
        copy.bones = source.bones;
        copy.rootBone = source.rootBone;
        copy.sharedMaterials = source.sharedMaterials;
        copy.updateWhenOffscreen = true;
        copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.enabled = true;

        Vector3 ownerOffset = Vector3.zero;
        if (ownerRoot != null)
        {
            ownerOffset = parent.position - ownerRoot.position;
        }
        child.position = source.transform.position + ownerOffset;
        child.rotation = source.transform.rotation;
        child.localScale = source.transform.lossyScale;
    }

    private void UpdateEditHandsPose(Transform playerTf, Transform opponentTf)
    {
        if (editHandsRoot == null || playerTf == null || opponentTf == null) return;

        editHandsRoot.position = Vector3.zero;
        editHandsRoot.rotation = Quaternion.identity;

        PositionHandsRootForOwner(editPlayerHandsRoot, playerTf, opponentTf, -1f);
        PositionHandsRootForOwner(editOpponentHandsRoot, opponentTf, playerTf, 1f);
    }

    private void PositionHandsRootForOwner(Transform handsRoot, Transform owner, Transform otherOwner, float sideSign)
    {
        if (handsRoot == null || owner == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(owner.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.000001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.000001f) right = Vector3.right;
        right.Normalize();

        float towardCenter01 = Mathf.Clamp01(editHandsBetween01) * 0.35f;
        Vector3 basePos = owner.position;
        if (otherOwner != null)
        {
            basePos = Vector3.Lerp(owner.position, otherOwner.position, towardCenter01);
        }

        Vector3 pos = basePos;
        pos += Vector3.up * editHandsHeight;
        pos += forward * editHandsTowardPlayer;
        pos += right * (Mathf.Max(0f, editHandsSideOffset) * sideSign);

        handsRoot.position = pos;
        handsRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private void OnGUI()
    {
        if (!battleStarted)
        {
            DrawStartButton();
            return;
        }

        if (playerStats != null)
        {
            DrawBars(playerStats, true);
        }
        if (aiStats != null)
        {
            DrawBars(aiStats, false);
        }

        if (perfectTimer > 0f)
        {
            EnsurePerfectStyle();
            var rect = new Rect(0f, Screen.height * 0.25f, Screen.width, 40f);
            GUI.Label(rect, "Perfect", perfectStyle);
        }

        if (gameOver)
        {
            EnsureGameOverStyle();
            var rect = new Rect(0f, Screen.height * 0.45f, Screen.width, 50f);
            GUI.Label(rect, "GAME OVER", gameOverStyle);
        }

        if (!gameOver)
        {
            EnsureTurnStyle();
            float h = playerTurn ? 300f : 160f;
            float y = playerTurn ? Screen.height * 0.16f : Screen.height * 0.22f;
            var rect = new Rect(0f, y, Screen.width, h);
            if (playerTurn && introSequenceActive)
            {
                float remain = Mathf.Max(0f, introSequenceUntilTime - Time.time);
                if (remain <= playerTurnCountdownSeconds)
                {
                    int sec = Mathf.Max(1, Mathf.CeilToInt(remain));
                    GUI.Label(rect, sec.ToString(), turnStyle);
                }
            }
            else
            {
                GUI.Label(rect, playerTurn ? "YOU\nTURN" : "HIM", turnStyle);
            }
        }

        if (!gameOver && !string.IsNullOrEmpty(hitMessage) && hitMessageTimer > 0f)
        {
            EnsureHitStyle();
            var rect = new Rect(0f, Screen.height * 0.30f, Screen.width, 80f);
            GUI.Label(rect, hitMessage, hitStyle);
        }

        if (!gameOver && !IsPlayerTurn() && aiAttackDelayTimer > 0f)
        {
            EnsureTimerStyle();
            float y = Screen.height - (Screen.height * 0.10f) - 160f;
            var rect = new Rect(0f, y, Screen.width, 160f);
            GUI.Label(rect, $"{aiAttackDelayTimer:0.0}", timerStyle);
        }
    }

    private void DrawBars(CombatantStats stats, bool left)
    {
        float w = Mathf.Clamp(Screen.width * 0.35f, 180f, 320f);
        float h = 16f;
        float pad = 12f;
        float shiftX = Screen.width * 0.05f;
        float x = left ? pad + shiftX : Screen.width - pad - w - shiftX;
        float y = pad + Screen.height * 0.2f;

        DrawBar(new Rect(x, y, w, h), stats.Health01, new Color(0.85f, 0.1f, 0.1f, 1f));
        DrawBar(new Rect(x, y + h + 6f, w, h), stats.Stamina01, new Color(0.1f, 0.45f, 1f, 1f));
    }

    private void DrawBar(Rect rect, float fill01, Color fillColor)
    {
        EnsureWhiteTex();
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(rect, whiteTex);
        rect.width = rect.width * Mathf.Clamp01(fill01);
        GUI.color = fillColor;
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = prev;
    }

    private void EnsureWhiteTex()
    {
        if (whiteTex != null) return;
        whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
    }

    private void EnsurePerfectStyle()
    {
        if (perfectStyle != null) return;
        perfectStyle = new GUIStyle(GUI.skin.label);
        perfectStyle.alignment = TextAnchor.MiddleCenter;
        perfectStyle.fontSize = 34;
        perfectStyle.fontStyle = FontStyle.Bold;
        perfectStyle.normal.textColor = Color.white;
    }

    private void EnsureGameOverStyle()
    {
        if (gameOverStyle != null) return;
        gameOverStyle = new GUIStyle(GUI.skin.label);
        gameOverStyle.alignment = TextAnchor.MiddleCenter;
        gameOverStyle.fontSize = 36;
        gameOverStyle.fontStyle = FontStyle.Bold;
        gameOverStyle.normal.textColor = Color.white;
    }

    private void EnsureTimerStyle()
    {
        if (timerStyle != null) return;
        timerStyle = new GUIStyle(GUI.skin.label);
        timerStyle.alignment = TextAnchor.MiddleCenter;
        timerStyle.fontSize = 100;
        timerStyle.fontStyle = FontStyle.Bold;
        timerStyle.normal.textColor = Color.white;
    }

    private void EnsureTurnStyle()
    {
        if (turnStyle != null) return;
        turnStyle = new GUIStyle(GUI.skin.label);
        turnStyle.alignment = TextAnchor.MiddleCenter;
        turnStyle.fontSize = 126;
        turnStyle.fontStyle = FontStyle.Bold;
        turnStyle.normal.textColor = Color.white;
    }

    private void EnsureHitStyle()
    {
        if (hitStyle != null) return;
        hitStyle = new GUIStyle(GUI.skin.label);
        hitStyle.alignment = TextAnchor.MiddleCenter;
        hitStyle.fontSize = 72;
        hitStyle.fontStyle = FontStyle.Bold;
        hitStyle.normal.textColor = Color.white;
    }

    private void EnsureStartButtonStyle()
    {
        if (startButtonStyle != null) return;
        startButtonStyle = new GUIStyle(GUI.skin.button);
        startButtonStyle.alignment = TextAnchor.MiddleCenter;
        startButtonStyle.fontSize = 128;
        startButtonStyle.fontStyle = FontStyle.Bold;
    }

    private void DrawStartButton()
    {
        EnsureStartButtonStyle();
        float w = Mathf.Clamp(Screen.width * 0.68f, 440f, 920f);
        float h = Mathf.Clamp(Screen.height * 0.28f, 180f, 340f);
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;
        var rect = new Rect(x, y, w, h);
        if (GUI.Button(rect, "SLAP", startButtonStyle))
        {
            StartBattle();
        }
    }

    private void DrainStamina()
    {
        var attacker = GetCurrentAttacker();
        var defender = GetCurrentDefender();
        var attackerStats = GetStats(attacker);
        var defenderStats = GetStats(defender);
        if (attackerStats != null && attacker != null && attacker.GetDebugWindup01() > 0.01f)
        {
            float rate = attackerStats.MaxStamina / Mathf.Max(0.01f, staminaDrainSeconds);
            attackerStats.SpendStamina(rate * Time.deltaTime);
        }
        if (defenderStats != null && defender != null && defender.IsDefenderBlocking())
        {
            float rate = defenderStats.MaxStamina / Mathf.Max(0.01f, staminaDrainSeconds);
            defenderStats.SpendStamina(rate * Time.deltaTime);
        }
    }

    private CombatantStats GetStats(SlapMechanics s)
    {
        if (s == null) return null;
        if (s == player) return playerStats;
        if (s == ai) return aiStats;
        return s.GetComponent<CombatantStats>();
    }

    private SlapMechanics GetCurrentAttacker()
    {
        return playerTurn ? player : ai;
    }

    private SlapMechanics GetCurrentDefender()
    {
        return playerTurn ? ai : player;
    }

    private void ApplyTurnRoles()
    {
        if (player == null || ai == null) return;
        if (playerTurn)
        {
            player.SetRole(SlapMechanics.Role.Attacker);
            ai.SetRole(SlapMechanics.Role.Defender);
        }
        else
        {
            player.SetRole(SlapMechanics.Role.Defender);
            ai.SetRole(SlapMechanics.Role.Attacker);
        }
        EnsureAuraVisibility();
        if (!battleStarted)
        {
            aiAttackDelayTimer = 0f;
            return;
        }
        if (!playerTurn)
        {
            aiAttackDelayTimer = ai != null ? ai.GetIntroRemainingSeconds() : 0f;
        }
        TryPlayFirstAttackerIntro();
    }

    private void TryPlayFirstAttackerIntro()
    {
        if (!battleStarted) return;
        if (!playFirstAttackerIntro && !playFirstDefenderIntro) return;
        if (firstAttackerIntroPlayed) return;
        if (player == null || ai == null) return;
        var attacker = GetCurrentAttacker();
        var defender = GetCurrentDefender();
        if (attacker == null) return;
        if (defender == null) return;

        firstAttackerIntroPlayed = true;
        playerInputBeforeIntro = player.allowHumanInput;
        player.allowHumanInput = false;

        float attackerIntroLen = 0f;
        if (playFirstAttackerIntro)
        {
            attackerIntroLen = resolvedFirstAttackerIntroClip != null
                ? attacker.PlayOneShotIntro(resolvedFirstAttackerIntroClip, firstAttackerIntroFallbackSeconds, false, false, firstAttackerIntroSpeedMultiplier)
                : attacker.PlayOneShotIntro(firstAttackerIntroName, firstAttackerIntroFallbackSeconds);
            if (attackerIntroLen <= 0.011f)
            {
                Debug.LogWarning($"[SlapCombat] Intro '{firstAttackerIntroName}' length is too small. Check animator state and clip.");
            }
            else
            {
                Debug.Log($"[SlapCombat] Intro start on '{attacker.gameObject.name}', state='{firstAttackerIntroName}', len={attackerIntroLen:0.00}s");
            }
        }

        float defenderIntroLen = 0f;
        if (playFirstDefenderIntro)
        {
            defenderIntroLen = resolvedFirstDefenderIntroClip != null
                ? defender.PlayOneShotIntro(resolvedFirstDefenderIntroClip, firstDefenderIntroFallbackSeconds, true)
                : defender.PlayOneShotIntro(firstDefenderIntroName, firstDefenderIntroFallbackSeconds, 0.08f, true);
            if (defenderIntroLen <= 0.011f)
            {
                Debug.LogWarning($"[SlapCombat] Intro '{firstDefenderIntroName}' length is too small. Check animator state and clip.");
            }
            else
            {
                Debug.Log($"[SlapCombat] Intro start on '{defender.gameObject.name}', state='{firstDefenderIntroName}', len={defenderIntroLen:0.00}s");
            }
        }

        float attackerTotal = 0f;
        if (playFirstAttackerIntro)
        {
            attackerTotal = attackerIntroLen + Mathf.Max(0f, firstAttackerIntroPostDelaySeconds);
        }
        float defenderTotal = 0f;
        if (playFirstDefenderIntro)
        {
            defenderTotal = defenderIntroLen + Mathf.Max(0f, firstAttackerIntroPostDelaySeconds);
        }
        float total = Mathf.Max(0.01f, Mathf.Max(attackerTotal, defenderTotal));
        introSequenceUntilTime = Time.time + total;
        introSequenceActive = true;

        if (!playerTurn)
        {
            aiAttackDelayTimer = Mathf.Max(aiAttackDelayTimer, total);
        }
    }
    private void UpdateIntroSequence()
    {
        if (!introSequenceActive) return;
        if (Time.time < introSequenceUntilTime) return;
        introSequenceActive = false;
        introSequenceUntilTime = -1f;
        if (player != null)
        {
            player.allowHumanInput = playerInputBeforeIntro;
        }
    }

    private void ResolveFirstAttackerIntroClip()
    {
        resolvedFirstAttackerIntroClip = null;
#if UNITY_EDITOR
        const string introFbxPath = "Assets/Animation/Animations+/AKIROmodel@Arm Stretching.fbx";
        var assets = AssetDatabase.LoadAllAssetsAtPath(introFbxPath);
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                var clip = obj as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
                resolvedFirstAttackerIntroClip = clip;
                break;
            }
        }
        if (playFirstAttackerIntro && resolvedFirstAttackerIntroClip == null)
        {
            Debug.LogWarning($"[SlapCombat] Attacker intro clip not found in '{introFbxPath}'.");
        }
#endif
    }

    private void ResolveFirstDefenderIntroClip()
    {
        resolvedFirstDefenderIntroClip = null;
#if UNITY_EDITOR
        const string introFbxPath = "Assets/Animation/Animations+/AKIROmodel@Neck Stretching.fbx";
        var assets = AssetDatabase.LoadAllAssetsAtPath(introFbxPath);
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                var clip = obj as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
                resolvedFirstDefenderIntroClip = clip;
                break;
            }
        }
        if (playFirstDefenderIntro && resolvedFirstDefenderIntroClip == null)
        {
            Debug.LogWarning($"[SlapCombat] Defender intro clip not found in '{introFbxPath}'.");
        }
#endif
    }

    private void ResolveMissedRightBlockReactionClip()
    {
        resolvedMissedRightBlockReactionClip = null;
#if UNITY_EDITOR
        const string reactionFbxPath = "Assets/Animation/Animations+/AKIROmodel@Falling Forward Death.fbx";
        string usedPath = null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(reactionFbxPath);
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                var clip = obj as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
                resolvedMissedRightBlockReactionClip = clip;
                usedPath = reactionFbxPath;
                break;
            }
        }
        if (playMissedRightBlockReaction && resolvedMissedRightBlockReactionClip == null)
        {
            Debug.LogWarning($"[SlapCombat] Missed-right-block reaction clip not found in '{reactionFbxPath}'.");
        }
        else if (playMissedRightBlockReaction)
        {
            Debug.Log($"[SlapCombat] Missed-right-block reaction ready from '{usedPath}', clip='{resolvedMissedRightBlockReactionClip.name}'.");
        }
#endif
    }

    private void ResolveMissedLeftBlockReactionClip()
    {
        resolvedMissedLeftBlockReactionClip = null;
#if UNITY_EDITOR
        const string reactionFbxPath = "Assets/Animation/Animations+/AKIROmodel@Falling Forward Death (1).fbx";
        string usedPath = null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(reactionFbxPath);
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                var clip = obj as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
                resolvedMissedLeftBlockReactionClip = clip;
                usedPath = reactionFbxPath;
                break;
            }
        }
        if (playMissedRightBlockReaction && resolvedMissedLeftBlockReactionClip == null)
        {
            Debug.LogWarning($"[SlapCombat] Missed-left-block reaction clip not found in '{reactionFbxPath}'.");
        }
        else if (playMissedRightBlockReaction)
        {
            Debug.Log($"[SlapCombat] Missed-left-block reaction ready from '{usedPath}', clip='{resolvedMissedLeftBlockReactionClip.name}'.");
        }
#endif
    }

    private void ResolveMissedSlapUpBlockReactionClip()
    {
        resolvedMissedSlapUpBlockReactionClip = null;
#if UNITY_EDITOR
        const string reactionFbxPath = "Assets/Animation/Animations+/AKIROmodel@Dying 1.fbx";
        string usedPath = null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(reactionFbxPath);
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                var clip = obj as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
                resolvedMissedSlapUpBlockReactionClip = clip;
                usedPath = reactionFbxPath;
                break;
            }
        }
        if (playMissedRightBlockReaction && resolvedMissedSlapUpBlockReactionClip == null)
        {
            Debug.LogWarning($"[SlapCombat] Missed-slapup-block reaction clip not found in '{reactionFbxPath}'.");
        }
        else if (playMissedRightBlockReaction)
        {
            Debug.Log($"[SlapCombat] Missed-slapup-block reaction ready from '{usedPath}', clip='{resolvedMissedSlapUpBlockReactionClip.name}'.");
        }
#endif
    }

    private void ResolveMissedSlapErcutBlockReactionClip()
    {
        resolvedMissedSlapErcutBlockReactionClip = null;
#if UNITY_EDITOR
        const string reactionFbxPath = "Assets/Animation/Animations+/AKIROmodel@Dyingslapercut.fbx";
        string usedPath = null;
        var assets = AssetDatabase.LoadAllAssetsAtPath(reactionFbxPath);
        if (assets != null)
        {
            foreach (var obj in assets)
            {
                var clip = obj as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase)) continue;
                resolvedMissedSlapErcutBlockReactionClip = clip;
                usedPath = reactionFbxPath;
                break;
            }
        }
        if (playMissedRightBlockReaction && resolvedMissedSlapErcutBlockReactionClip == null)
        {
            Debug.LogWarning($"[SlapCombat] Missed-slapercut-block reaction clip not found in '{reactionFbxPath}'.");
        }
        else if (playMissedRightBlockReaction)
        {
            Debug.Log($"[SlapCombat] Missed-slapercut-block reaction ready from '{usedPath}', clip='{resolvedMissedSlapErcutBlockReactionClip.name}'.");
        }
#endif
    }

    private void StartBattle()
    {
        if (battleStarted) return;
        battleStarted = true;
        if (player != null)
        {
            player.allowHumanInput = true;
        }
        ApplyTurnRoles();
    }

    public SlapMechanics GetPlayer()
    {
        return player;
    }

    public SlapMechanics GetAI()
    {
        return ai;
    }

    public bool IsPlayerTurn()
    {
        return playerTurn;
    }

    public float GetAIAttackDelayRemaining()
    {
        return Mathf.Max(0f, aiAttackDelayTimer);
    }

    public bool IsWaitingForSlapEnd()
    {
        return pendingTurnSwitch || pendingHit;
    }

    public bool IsBattleStarted()
    {
        return battleStarted;
    }

    private float GetResolveAtProgress(SlapMechanics.SlapDirection dir, bool wasBlocked)
    {
        if (wasBlocked)
        {
            return Mathf.Clamp(blockedHitResolveProgress, 0.5f, 0.98f);
        }
        if (!usePerDirectionHitResolveTiming)
        {
            return Mathf.Clamp(legacyHitResolveProgress, 0.5f, 0.98f);
        }

        float value = dir switch
        {
            SlapMechanics.SlapDirection.Left => hitResolveProgressSide,
            SlapMechanics.SlapDirection.Right => hitResolveProgressSide,
            SlapMechanics.SlapDirection.UpLeft => hitResolveProgressDiagonal,
            SlapMechanics.SlapDirection.UpRight => hitResolveProgressDiagonal,
            SlapMechanics.SlapDirection.DownLeft => hitResolveProgressDiagonal,
            SlapMechanics.SlapDirection.DownRight => hitResolveProgressDiagonal,
            SlapMechanics.SlapDirection.Up => hitResolveProgressUp,
            SlapMechanics.SlapDirection.Down => hitResolveProgressDown,
            _ => legacyHitResolveProgress
        };
        return Mathf.Clamp(value, 0.5f, 0.98f);
    }

    private void EnsureAuraVisibility()
    {
        if (player != null)
        {
            player.ConfigureAura(true, true);
        }
        if (ai != null)
        {
            ai.ConfigureAura(false, false);
        }
    }


    public SlapMechanics.SlapDirection MirrorForOpponent(SlapMechanics.SlapDirection dir)
    {
        return dir switch
        {
            SlapMechanics.SlapDirection.Left => SlapMechanics.SlapDirection.Right,
            SlapMechanics.SlapDirection.Right => SlapMechanics.SlapDirection.Left,
            SlapMechanics.SlapDirection.UpLeft => SlapMechanics.SlapDirection.UpRight,
            SlapMechanics.SlapDirection.UpRight => SlapMechanics.SlapDirection.UpLeft,
            SlapMechanics.SlapDirection.DownLeft => SlapMechanics.SlapDirection.DownRight,
            SlapMechanics.SlapDirection.DownRight => SlapMechanics.SlapDirection.DownLeft,
            _ => dir
        };
    }

}


