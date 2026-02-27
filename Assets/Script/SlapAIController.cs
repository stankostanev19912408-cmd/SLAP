using UnityEngine;

public class SlapAIController : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool advancedAIEnabled = true;
    [SerializeField] private AIStyle advancedStyle = AIStyle.Balanced;

    [Header("Attack")]
    // Intentionally fixed behavior: no false windups, no slow attacks.
    [SerializeField] private Vector2 windupDurationRange = new Vector2(0.9f, 1.5f);
    [SerializeField] private Vector2 windupHoldRange = new Vector2(0.05f, 0.1f);
    [SerializeField] private Vector2 windupTargetRange = new Vector2(1.0f, 1.0f);
    [SerializeField] private Vector2 slowSwipeSpeedRange = new Vector2(0f, 0f);
    [SerializeField] private Vector2 fastSwipeSpeedRange = new Vector2(0f, 0f);

    [Header("Block")]
    [SerializeField] private float blockChance = 0.8f;
    [SerializeField] private float blockSpeedFast = 3.5f;
    [SerializeField] private float blockSpeedSlow = 1.5f;
    [SerializeField] private float blockFastChance = 0.5f;
    [SerializeField, Range(0f, 1f)] private float reactToWindupFrom01 = 0.55f;
    [SerializeField] private float blockHoldAfterSlapStartSeconds = 0.25f;
    [SerializeField] private float blockReleaseGraceSeconds = 0.1f;
    [SerializeField] private float blockMinCommitSeconds = 0.22f;
    [SerializeField] private float blockReleaseWindupThreshold01 = 0.06f;
    [SerializeField] private float blockThreatMemorySeconds = 0.18f;
    [SerializeField] private float blockNoDropAfterThreatSeconds = 0.28f;
    [SerializeField] private float blockHardLatchSeconds = 0.35f;
    [SerializeField] private float blockCalmReleaseDelaySeconds = 0.25f;
    [SerializeField] private float threatBlockReleaseTailSeconds = 0.22f;
    [SerializeField] private float feintMinDurationSeconds = 0.12f;
    [SerializeField] private float feintMinWindup01 = 0.2f;
    [SerializeField] private int feintsForMistake = 3;
    [SerializeField, Range(0f, 1f)] private float feintMistakeChance = 0.5f;
    [SerializeField, Range(0f, 1f)] private float lowStaminaForMistake01 = 0.5f;
    [SerializeField, Range(0f, 1f)] private float feintMistakeMaxIncomingSlapPower01 = 0.5f;
    [SerializeField] private float feintMistakeOpenBlockSeconds = 0.16f;
    [SerializeField] private bool useDeterministicBlockControl = true;
    [SerializeField] private float pendingThreatArmSeconds = 0.08f;
    [SerializeField] private float blockReraiseCooldownAfterReleaseSeconds = 0.2f;
    [SerializeField] private float pendingReraiseSuppressSeconds = 0.35f;
    [SerializeField] private float blockRaiseDelayPenaltySeconds = 0.5f;
    [SerializeField] private float feintReblockSuppressSeconds = 0.7f;

    [Header("Cooldowns")]
    [SerializeField] private Vector2 attackCooldownRange = new Vector2(0.2f, 0.6f);
    [Header("Startup")]
    [SerializeField] private bool suppressDefenseAtRoundStart = true;
    [SerializeField] private float suppressDefenseSeconds = 1.2f;

    private SlapCombatManager combat;
    private SlapMechanics slap;
    private SlapMechanics opponent;
    private SlapMechanics.SlapDirection lastObservedPlayerSlap = SlapMechanics.SlapDirection.None;
    private readonly SlapMechanics.SlapDirection[] recentPlayerSlaps = new SlapMechanics.SlapDirection[10];
    private int recentPlayerSlapsCount;
    private int recentPlayerSlapsWriteIndex;
    private bool combatEventsSubscribed;

    private enum AIStyle
    {
        Safe,
        Balanced,
        Aggro
    }

    private enum State
    {
        Idle,
        Windup,
        Hold,
        Cooldown,
        Blocking
    }

    private struct AIDifficultyProfile
    {
        public GameDifficulty difficulty;
        public float aiDifficultyTelemetry01;
        public float minWindupDuration;
        public float windupDurationMultiplier;
        public float minPostHold;
        public float attackCooldownAdd;
        public float swipeSpeedMultiplier;
        public float explorationProb;
        public float greedyProbMultiplier;
        public float tunedReactToWindupFrom01;
        public float tunedBlockRaiseDelaySeconds;
        public float tunedBlockMistakeChance;
        public float tunedLatchHoldExtraSeconds;
        public float calmBlockNoThreatCapSeconds;
        public float hardBlockHoldCapSeconds;
        public float calmBlockReleaseGraceSeconds;
    }

    private State state = State.Idle;
    private float stateTimer;
    private float windupDuration;
    private float windupHold;
    private float windupTarget;
    private float attackCooldown;
    private bool falseWindup;
    private float swipeSpeed;
    private SlapMechanics.SlapDirection attackDir = SlapMechanics.SlapDirection.Right;
    private float blockSpeed;
    private SlapMechanics.SlapDirection blockDir = SlapMechanics.SlapDirection.None;
    private SlapMechanics.SlapDirection lastIncomingThreatDir = SlapMechanics.SlapDirection.None;
    private float attackStartTime;
    private bool attackInProgress;
    private float startupTime;
    private CombatantStats selfStats;
    private bool opponentWasSlapping;
    private float blockLatchedUntilTime;
    private float blockCommitUntilTime;
    private float lastThreatSeenTime = -999f;
    private float calmNoThreatSinceTime = -1f;
    private float lastOpponentSlapEndTime = -999f;
    private bool feintCandidateActive;
    private float feintCandidateStartTime;
    private float feintCandidatePeakWindup01;
    private int feintCounter;
    private bool mistakeArmed;
    private float mistakeOpenUntilTime;
    private float blockHardLatchUntilTime;
    private bool threatBlockLatched;
    private float threatBlockReleaseTime;
    private float lastImmediateThreatTime = -999f;
    private float pendingThreatSinceTime = -1f;
    private float blockReleasedAtTime = -999f;
    private float suppressPendingRaiseUntilTime = -999f;
    private bool requirePendingResetAfterRelease;
    private float lastObservedOpponentWindup01;
    private bool hasLastObservedOpponentWindup;
    private bool lastPendingThreatRaw;
    private float pendingThreatStartTime = -1f;
    private float pendingThreatRaiseDelaySeconds = 0.3f;
    private bool blockReacquireLockUntilNeutral;
    private float feintReblockSuppressUntilTime = -999f;
    private const int FastSkillWindowSize = 6;
    private const int SlowSkillWindowSize = 16;
    private readonly System.Collections.Generic.Queue<ExchangeSample> fastWindow = new System.Collections.Generic.Queue<ExchangeSample>(FastSkillWindowSize);
    private readonly System.Collections.Generic.Queue<ExchangeSample> slowWindow = new System.Collections.Generic.Queue<ExchangeSample>(SlowSkillWindowSize);
    private readonly int[] playerAttackHist = new int[8];
    private readonly int[] playerBlockHist = new int[8];
    private const int PlayerHoldRecentWindow = 10;
    private readonly System.Collections.Generic.Queue<float> playerBlockHoldRecent = new System.Collections.Generic.Queue<float>(PlayerHoldRecentWindow);
    private readonly System.Collections.Generic.Queue<float> playerWindupHoldRecent = new System.Collections.Generic.Queue<float>(PlayerHoldRecentWindow);
    private float playerAvgBlockHoldSecondsLast10;
    private float playerAvgWindupHoldSecondsLast10;
    private const float MinBlockMistakeChance = 0.03f;
    private const float GreedyVulnerabilityDurationSeconds = 0.6f;
    private const float CalmBlockReleaseGraceMinSeconds = 0.15f;
    private const float CalmBlockReleaseGraceMaxSeconds = 0.25f;
    private const float PostTargetHoldLowSkillSeconds = 0.35f;
    private const float PostTargetHoldHighSkillSeconds = 0.12f;
    private const float PostTargetHoldRandomMaxSeconds = 0.06f;
    private const int WindupTelemetrySampleCap = 200;
    private float playerSkillFast = 0.5f;
    private float playerSkillSlow = 0.5f;
    private float playerSkill = 0.5f;
    private float aiDifficulty = 0.5f;
    private float aiSwipeSpeedLast;
    private SlapMechanics.SlapDirection chosenAttackDir = SlapMechanics.SlapDirection.None;
    private string chosenAttackReason = "none";
    private float lastExplorationProb;
    private float lastGreedyProb;
    private float patternConfidence;
    private float tunedReactToWindupFrom01 = 0.55f;
    private float tunedBlockRaiseDelaySeconds = 0.3f;
    private float tunedBlockMistakeChance = 0.12f;
    private float tunedLatchHoldExtraSeconds = 0.2f;
    private float greedyVulnerabilityUntilTime = -999f;
    private bool lastMinWindupDurationApplied;
    private bool lastMinPostHoldApplied;
    private bool anyMinWindupDurationApplied;
    private bool anyMinPostHoldApplied;
    private float noThreatBlockSeconds;
    private readonly System.Collections.Generic.List<float> aiWindupChargeSamples = new System.Collections.Generic.List<float>(WindupTelemetrySampleCap);
    private readonly System.Collections.Generic.List<float> aiWindupPostHoldSamples = new System.Collections.Generic.List<float>(WindupTelemetrySampleCap);
    private bool aiWindupTelemetryActive;
    private float aiWindupChargeSecondsCurrent;
    private float aiWindupPostHoldSecondsCurrent;
    private bool aiWindupTargetReachedCurrent;
    private float aiWindupStartTimeCurrent;
    private float aiWindupTargetReachedTimeCurrent;
    private bool forcedBlockReleaseSignal;
    private SlapMechanics.SlapDirection lastPlayerBlockDir = SlapMechanics.SlapDirection.None;
    private SlapMechanics.SlapDirection lastExpectedBlockSourceDir = SlapMechanics.SlapDirection.None;
    private SlapMechanics.SlapDirection lastExpectedBlockResolvedDir = SlapMechanics.SlapDirection.None;
    private int playerSameBlockStreak;
    private GameDifficulty currentDifficulty = GameDifficulty.Normal;
    private AIDifficultyProfile currentProfile;
    private bool hasDifficultyProfile;
    private static readonly SlapMechanics.SlapDirection[] AttackDirections =
    {
        SlapMechanics.SlapDirection.Up,
        SlapMechanics.SlapDirection.Down,
        SlapMechanics.SlapDirection.Left,
        SlapMechanics.SlapDirection.Right,
        SlapMechanics.SlapDirection.UpLeft,
        SlapMechanics.SlapDirection.UpRight,
        SlapMechanics.SlapDirection.DownLeft,
        SlapMechanics.SlapDirection.DownRight
    };

    private struct ExchangeSample
    {
        public bool playerWasAttacker;
        public bool playerLandedHit;
        public bool playerBlocked;
        public bool playerPerfectBlocked;
        public float appliedDamage;
        public float baseDamagePercent;
        public float finalDamagePercent;
    }

    public void SetCombat(SlapCombatManager manager, SlapMechanics owner)
    {
        UnsubscribeCombatEvents();
        combat = manager;
        slap = owner;
        startupTime = Time.time;
        SubscribeCombatEvents();
        RefreshDifficultyProfileFromSettings(true);
        ResetAdaptiveState();
        if (slap != null)
        {
            slap.allowHumanInput = false;
        }
    }

    private void OnDisable()
    {
        FinalizeAIWindupTelemetry();
        UnsubscribeCombatEvents();
    }

    private void OnDestroy()
    {
        FinalizeAIWindupTelemetry();
        UnsubscribeCombatEvents();
    }

    private void Update()
    {
        if (combat == null || slap == null) return;
        if (combat.GetAI() != slap) return;
        if (!combat.IsBattleStarted())
        {
            RefreshDifficultyProfileFromSettings();
        }
        if (!combat.IsBattleStarted())
        {
            if (state != State.Idle)
            {
                slap.AI_SoftCancelWindup();
                FinalizeAIWindupTelemetry();
                slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
                slap.AI_EndBlock();
                state = State.Idle;
                stateTimer = 0f;
            }
            return;
        }
        RecomputeDefenseTuning();
        UpdateAIWindupTelemetry();
        if (aiWindupTelemetryActive && !attackInProgress)
        {
            FinalizeAIWindupTelemetry();
        }
        if (combat.IsWaitingForSlapEnd())
        {
            // During unresolved slap, freeze actions.
            if (slap.GetRole() == SlapMechanics.Role.Attacker)
            {
                if (state != State.Idle)
                {
                    slap.AI_SoftCancelWindup();
                    FinalizeAIWindupTelemetry();
                    state = State.Idle;
                    stateTimer = 0f;
                }
            }
            else
            {
                // While hit is unresolved, force defender block every frame.
                UpdateThreatAndFeints();
                if (TryTriggerFeintMistake())
                {
                    return;
                }
                if (MaintainThreatBlockLatch())
                {
                    return;
                }
                if (TryKeepBlockingIfThreat())
                {
                    return;
                }
            }
            return;
        }
        opponent = combat.GetPlayer();
        TrackPlayerDirectionHistory();

        if (slap.GetRole() == SlapMechanics.Role.Attacker)
        {
            if (combat.GetAIAttackDelayRemaining() > 0f)
            {
                if (state == State.Blocking && TryKeepBlockingIfThreat())
                {
                    return;
                }
                if (state != State.Idle)
                {
                    slap.AI_CancelWindup();
                    FinalizeAIWindupTelemetry();
                    state = State.Idle;
                    stateTimer = 0f;
                }
                return;
            }
            if (state == State.Blocking)
            {
                // As attacker, never stay in defender block state.
                ForceEndBlock();
                state = State.Idle;
                stateTimer = 0f;
            }
            if (advancedAIEnabled) UpdateAttack();
            else UpdateAttackMirrorMode();
        }
        else
        {
            // Keep controller state aligned with actual held block to avoid false restart jitter.
            if (slap.IsDefenderBlockReleasing() && !HasImmediateIncomingThreat())
            {
                state = State.Idle;
            }
            else if (slap.IsDefenderBlocking() && !slap.IsDefenderBlockReleasing() && state != State.Blocking)
            {
                state = State.Blocking;
            }
            else if (!slap.IsDefenderBlocking() && state == State.Blocking && !HasImmediateIncomingThreat())
            {
                state = State.Idle;
            }

            if (state != State.Blocking && state != State.Idle)
            {
                slap.AI_SoftCancelWindup();
                FinalizeAIWindupTelemetry();
                state = State.Idle;
                stateTimer = 0f;
            }
            if (!useDeterministicBlockControl)
            {
                useDeterministicBlockControl = true;
            }
            UpdateBlockDeterministic();
            return;
        }
    }

    private void UpdateBlockDeterministic()
    {
        UpdateThreatAndFeints();
        RefreshHardBlockLatch();

        if (TryTriggerFeintMistake())
        {
            return;
        }

        bool slapThreat = opponent != null && opponent.IsSlapAnimating();
        float windup01 = opponent != null ? opponent.GetDebugWindup01() : 0f;
        float prevWindup01 = hasLastObservedOpponentWindup ? lastObservedOpponentWindup01 : windup01;
        bool windupRising = !hasLastObservedOpponentWindup || windup01 >= (prevWindup01 - 0.001f);
        lastObservedOpponentWindup01 = windup01;
        hasLastObservedOpponentWindup = opponent != null;
        float reactThreshold = Mathf.Clamp01(tunedReactToWindupFrom01);
        bool attackCycleThreat = opponent != null && opponent.IsAttackCycleActive();
        bool pendingThreatRaw = opponent != null && opponent.GetPendingDirection() != SlapMechanics.SlapDirection.None;
        if (pendingThreatRaw && !lastPendingThreatRaw)
        {
            pendingThreatStartTime = Time.time;
            pendingThreatRaiseDelaySeconds = ResolveRaiseDelayForNewThreat();
        }
        else if (!pendingThreatRaw)
        {
            pendingThreatStartTime = -1f;
            pendingThreatRaiseDelaySeconds = Mathf.Max(0f, tunedBlockRaiseDelaySeconds);
        }
        lastPendingThreatRaw = pendingThreatRaw;
        bool pendingThreat = pendingThreatRaw;
        if (requirePendingResetAfterRelease)
        {
            if (!pendingThreatRaw)
            {
                requirePendingResetAfterRelease = false;
            }
            else
            {
                pendingThreat = false;
            }
        }
        bool unresolvedHit = combat != null && combat.IsWaitingForSlapEnd();

        // After releasing block from a fake windup, ignore pending until attacker fully returns to neutral.
        if (blockReacquireLockUntilNeutral)
        {
            bool opponentNeutral = !slapThreat && !pendingThreatRaw && !attackCycleThreat && windup01 <= 0.02f;
            if (opponentNeutral)
            {
                blockReacquireLockUntilNeutral = false;
            }
            else
            {
                pendingThreat = false;
            }
        }
        if (pendingThreat && !slapThreat && Time.time < suppressPendingRaiseUntilTime)
        {
            pendingThreat = false;
        }
        if (pendingThreat && !slapThreat && Time.time < feintReblockSuppressUntilTime)
        {
            pendingThreat = false;
        }
        if (pendingThreat && !slapThreat)
        {
            if (pendingThreatSinceTime < 0f) pendingThreatSinceTime = Time.time;
        }
        else
        {
            pendingThreatSinceTime = -1f;
        }

        bool qualifiedPendingThreat = pendingThreat && windup01 >= reactThreshold && windupRising;
        bool immediateThreat = slapThreat || qualifiedPendingThreat;
        if (immediateThreat)
        {
            lastImmediateThreatTime = Time.time;
        }

        bool noRealThreat =
            !immediateThreat &&
            !pendingThreatRaw &&
            !attackCycleThreat &&
            !slapThreat &&
            !unresolvedHit;
        bool isBlockingNow = state == State.Blocking || (slap != null && slap.IsDefenderBlocking());
        if (isBlockingNow && noRealThreat)
        {
            noThreatBlockSeconds += Time.deltaTime;
        }
        else
        {
            noThreatBlockSeconds = 0f;
        }

        float calmCap = hasDifficultyProfile
            ? Mathf.Max(0.05f, currentProfile.calmBlockNoThreatCapSeconds)
            : 0.26f;
        if (isBlockingNow && noThreatBlockSeconds > calmCap)
        {
            ForceReleaseBlockForEconomyCap();
            return;
        }

        float hardCap = hasDifficultyProfile
            ? Mathf.Max(0.1f, currentProfile.hardBlockHoldCapSeconds)
            : 0.92f;
        float currentBlockHoldSeconds = (slap != null && isBlockingNow)
            ? Mathf.Max(0f, slap.GetDefenderBlockHoldSeconds())
            : 0f;
        if (isBlockingNow && currentBlockHoldSeconds > hardCap)
        {
            ForceReleaseBlockForEconomyCap();
            return;
        }

        // If block is already releasing and there is no real active threat,
        // never re-grab it from tail memory.
        if (slap != null && slap.IsDefenderBlockReleasing() && !immediateThreat && !unresolvedHit)
        {
            state = State.Idle;
            return;
        }

        bool shouldKeepBlock = immediateThreat || unresolvedHit;
        bool isReleasingNow = slap != null && slap.IsDefenderBlockReleasing();
        bool inReraiseCooldown =
            (Time.time - blockReleasedAtTime) < Mathf.Max(0f, blockReraiseCooldownAfterReleaseSeconds);

        // Hard rule: while release animation is in progress, never re-raise block.
        // This removes the visual down-up-down loop after fake windups.
        if (isReleasingNow)
        {
            state = State.Idle;
            return;
        }

        float requiredRaiseDelay = Mathf.Max(pendingThreatRaiseDelaySeconds, Mathf.Max(0f, pendingThreatArmSeconds));
        float pendingAge = pendingThreatStartTime >= 0f ? (Time.time - pendingThreatStartTime) : 0f;
        bool canRaiseFromIdle =
            slapThreat ||
            (qualifiedPendingThreat &&
             pendingAge >= requiredRaiseDelay);
        if (state != State.Blocking && inReraiseCooldown)
        {
            canRaiseFromIdle = false;
        }

        bool canKeepExistingBlock = state == State.Blocking && !isReleasingNow;
        if (inReraiseCooldown && !slapThreat && !unresolvedHit)
        {
            canKeepExistingBlock = false;
        }

        if (shouldKeepBlock && (canKeepExistingBlock || canRaiseFromIdle))
        {
            AlignBlockToCurrentThreat();
            if (slap != null)
            {
                slap.AI_SetHardBlockLock(true, ResolveExpectedBlockDir());
            }
            if (state != State.Blocking)
            {
                state = State.Blocking;
                stateTimer = 0f;
                blockSpeed = blockSpeedFast;
            }
            UpdateBlockHold();
            return;
        }

        if (shouldKeepBlock)
        {
            // Threat exists but is not stable enough to re-raise from idle.
            return;
        }

        if (slap != null)
        {
            slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
            if (state == State.Blocking || slap.IsDefenderBlocking())
            {
                float calmThreshold = Mathf.Max(0f, blockCalmReleaseDelaySeconds);
                bool noThreatAndNoPending =
                    !unresolvedHit &&
                    !immediateThreat &&
                    !slapThreat &&
                    !attackCycleThreat &&
                    !pendingThreatRaw &&
                    !threatBlockLatched &&
                    Time.time >= blockCommitUntilTime &&
                    Time.time >= blockHardLatchUntilTime;
                if (noThreatAndNoPending)
                {
                    calmThreshold = ResolveCalmBlockReleaseGraceSeconds();
                }

                bool calmReady = calmNoThreatSinceTime >= 0f &&
                                 (Time.time - calmNoThreatSinceTime) >= calmThreshold;
                if (!calmReady)
                {
                    return;
                }
                blockReleasedAtTime = Time.time;
                suppressPendingRaiseUntilTime = Time.time + Mathf.Max(0f, pendingReraiseSuppressSeconds);
                requirePendingResetAfterRelease = true;
                blockReacquireLockUntilNeutral = true;
                slap.AI_EndBlock();
            }
        }
        state = State.Idle;
    }

    private void UpdateAttack()
    {
        UpdateAIWindupTelemetry();
        switch (state)
        {
            case State.Idle:
                StartWindup();
                break;
            case State.Windup:
                stateTimer += Time.deltaTime;
                float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, windupDuration));
                slap.AI_SetWindupProgress(Mathf.Lerp(0f, windupTarget, t));
                if (t >= 1f)
                {
                    state = State.Hold;
                    stateTimer = 0f;
                }
                break;
            case State.Hold:
                stateTimer += Time.deltaTime;
                if (stateTimer >= windupHold)
                {
                    if (falseWindup)
                    {
                        // False windup should return smoothly to idle pose.
                        slap.AI_SoftCancelWindup();
                    }
                    else
                    {
                        aiSwipeSpeedLast = swipeSpeed;
                        slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
                    }
                    state = State.Cooldown;
                    stateTimer = 0f;
                    attackInProgress = false;
                    FinalizeAIWindupTelemetry();
                }
                break;
            case State.Cooldown:
                stateTimer += Time.deltaTime;
                if (stateTimer >= attackCooldown)
                {
                    state = State.Idle;
                    stateTimer = 0f;
                }
                break;
        }
        // Hard failsafe: never keep hand raised longer than 2 seconds.
        if (attackInProgress && Time.time - attackStartTime > 2f)
        {
            aiSwipeSpeedLast = swipeSpeed;
            slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
            state = State.Cooldown;
            stateTimer = 0f;
            attackInProgress = false;
            FinalizeAIWindupTelemetry();
        }
    }

    private void UpdateBlock()
    {
        GetAdvancedStyleTuning(out float tunedBlockChance, out float tunedFastChance, out float tunedReactFrom01, out _);
        UpdateThreatAndFeints();
        RefreshHardBlockLatch();

        if (suppressDefenseAtRoundStart && Time.time - startupTime < Mathf.Max(0f, suppressDefenseSeconds))
        {
            if (state == State.Blocking)
            {
                if (TryKeepBlockingIfThreat()) return;
                ForceEndBlock();
            }
            return;
        }

        if (TryTriggerFeintMistake())
        {
            return;
        }

        // Hard rule with latch: while immediate threat (and short tail) is present, defender must keep block.
        if (MaintainThreatBlockLatch())
        {
            return;
        }

        if (opponent != null && !opponent.IsSlapAnimating())
        {
            float w = opponent.GetDebugWindup01();
            if (w < Mathf.Clamp01(tunedReactFrom01))
            {
                if (state == State.Blocking)
                {
                    if (TryKeepBlockingIfThreat()) return;
                    ForceEndBlock();
                }
                return;
            }
        }

        var pending = opponent != null ? opponent.GetPendingDirection() : SlapMechanics.SlapDirection.None;
        if (pending == SlapMechanics.SlapDirection.None)
        {
            if (state == State.Blocking)
            {
                if (TryKeepBlockingIfThreat()) return;
                ForceEndBlock();
            }
            return;
        }

        if (state != State.Blocking)
        {
            if (Random.value > tunedBlockChance) return;
            blockDir = combat != null ? combat.MirrorForOpponent(pending) : pending;
            blockSpeed = Random.value < tunedFastChance ? blockSpeedFast : blockSpeedSlow;
            slap.AI_StartBlock(blockDir);
            state = State.Blocking;
            stateTimer = 0f;
            blockCommitUntilTime = Time.time + Mathf.Max(0f, blockMinCommitSeconds);
        }

        stateTimer += Time.deltaTime;
        float hold = Mathf.Clamp01(stateTimer * blockSpeed);
        slap.AI_UpdateBlockHold(hold);
    }

    private void StartWindup()
    {
        GetAdvancedStyleTuning(out _, out _, out _, out float fakeWindupChance);

        attackDir = ChooseAdaptiveAttackDirection();
        falseWindup = Random.value < fakeWindupChance;
        windupTarget = ResolveWindupTarget01();
        windupDuration = ResolveWindupDurationSeconds(windupTarget);
        windupHold = ResolvePostTargetHoldSeconds();
        // Do not keep hand in windup longer than 2 seconds total.
        float maxTotal = 2f;
        float maxHold = Mathf.Max(0f, maxTotal - windupDuration);
        if (windupHold > maxHold) windupHold = maxHold;
        swipeSpeed = GetAISwipeSpeedCmPerSec();
        aiSwipeSpeedLast = swipeSpeed;
        attackCooldown = ResolveAttackCooldownSeconds(Random.Range(attackCooldownRange.x, attackCooldownRange.y));

        slap.AI_BeginWindup(attackDir);
        state = State.Windup;
        stateTimer = 0f;
        attackStartTime = Time.time;
        attackInProgress = true;
        BeginAIWindupTelemetry();
    }

    private void UpdateAttackMirrorMode()
    {
        UpdateAIWindupTelemetry();
        switch (state)
        {
            case State.Idle:
                StartWindupMirrorMode();
                break;
            case State.Windup:
                stateTimer += Time.deltaTime;
                float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, windupDuration));
                slap.AI_SetWindupProgress(Mathf.Lerp(0f, windupTarget, t));
                if (t >= 1f)
                {
                    state = State.Hold;
                    stateTimer = 0f;
                }
                break;
            case State.Hold:
                stateTimer += Time.deltaTime;
                if (stateTimer >= windupHold)
                {
                    aiSwipeSpeedLast = swipeSpeed;
                    slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
                    state = State.Cooldown;
                    stateTimer = 0f;
                    attackInProgress = false;
                    FinalizeAIWindupTelemetry();
                }
                break;
            case State.Cooldown:
                stateTimer += Time.deltaTime;
                if (stateTimer >= attackCooldown)
                {
                    state = State.Idle;
                    stateTimer = 0f;
                }
                break;
        }

        if (attackInProgress && Time.time - attackStartTime > 2f)
        {
            aiSwipeSpeedLast = swipeSpeed;
            slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
            state = State.Cooldown;
            stateTimer = 0f;
            attackInProgress = false;
            FinalizeAIWindupTelemetry();
        }
    }

    private void StartWindupMirrorMode()
    {
        attackDir = ChooseAdaptiveAttackDirection();
        falseWindup = false;
        windupTarget = ResolveWindupTarget01();
        windupDuration = ResolveWindupDurationSeconds(windupTarget);
        windupHold = ResolvePostTargetHoldSeconds();
        swipeSpeed = GetAISwipeSpeedCmPerSec();
        aiSwipeSpeedLast = swipeSpeed;
        attackCooldown = ResolveAttackCooldownSeconds(0.3f);

        slap.AI_BeginWindup(attackDir);
        state = State.Windup;
        stateTimer = 0f;
        attackStartTime = Time.time;
        attackInProgress = true;
        BeginAIWindupTelemetry();
    }

    private void UpdateBlockMirrorMode()
    {
        UpdateThreatAndFeints();
        RefreshHardBlockLatch();
        if (TryTriggerFeintMistake())
        {
            return;
        }

        if (MaintainThreatBlockLatch())
        {
            return;
        }

        var pending = opponent != null ? opponent.GetPendingDirection() : SlapMechanics.SlapDirection.None;
        if (pending == SlapMechanics.SlapDirection.None)
        {
            if (state == State.Blocking)
            {
                if (TryKeepBlockingIfThreat()) return;
                ForceEndBlock();
            }
            return;
        }

        var expected = combat != null ? combat.MirrorForOpponent(pending) : pending;
        if (state != State.Blocking || blockDir != expected)
        {
            blockDir = expected;
            blockSpeed = blockSpeedFast;
            slap.AI_StartBlock(blockDir);
            state = State.Blocking;
            stateTimer = 0f;
            blockCommitUntilTime = Time.time + Mathf.Max(0f, blockMinCommitSeconds);
        }

        stateTimer += Time.deltaTime;
        float hold = Mathf.Clamp01(stateTimer * blockSpeedFast);
        slap.AI_UpdateBlockHold(hold);
    }

    private void UpdateBlockHold()
    {
        stateTimer += Time.deltaTime;
        float speed = blockSpeed > 0f ? blockSpeed : blockSpeedFast;
        float hold = Mathf.Clamp01(stateTimer * speed);
        slap.AI_UpdateBlockHold(hold);
    }

    private void UpdateThreatAndFeints()
    {
        if (opponent == null) return;

        bool attackCycle = opponent.IsAttackCycleActive();
        bool isSlap = opponent.IsSlapAnimating();
        var pending = opponent.GetPendingDirection();
        float windup = opponent.GetDebugWindup01();

        if (pending != SlapMechanics.SlapDirection.None)
        {
            lastIncomingThreatDir = combat != null ? combat.MirrorForOpponent(pending) : pending;
        }
        else if (isSlap)
        {
            var slapDir = opponent.GetLastSlapDirection();
            if (slapDir != SlapMechanics.SlapDirection.None)
            {
                lastIncomingThreatDir = combat != null ? combat.MirrorForOpponent(slapDir) : slapDir;
            }
        }
        if (attackCycle ||
            pending != SlapMechanics.SlapDirection.None ||
            windup > Mathf.Clamp01(blockReleaseWindupThreshold01))
        {
            lastThreatSeenTime = Time.time;
            calmNoThreatSinceTime = -1f;
        }
        else if (calmNoThreatSinceTime < 0f)
        {
            calmNoThreatSinceTime = Time.time;
        }

        if (isSlap && !opponentWasSlapping)
        {
            blockLatchedUntilTime = Mathf.Max(blockLatchedUntilTime, Time.time + Mathf.Max(0f, blockHoldAfterSlapStartSeconds));
            feintCandidateActive = false;
            feintCandidateStartTime = 0f;
            feintCandidatePeakWindup01 = 0f;
            feintCounter = 0;
        }
        if (!isSlap && opponentWasSlapping)
        {
            lastOpponentSlapEndTime = Time.time;
        }
        opponentWasSlapping = isSlap;

        if (isSlap) return;

        if (mistakeOpenUntilTime > 0f && Time.time >= mistakeOpenUntilTime)
        {
            mistakeOpenUntilTime = 0f;
        }

        var w = windup;
        if (pending != SlapMechanics.SlapDirection.None)
        {
            if (!feintCandidateActive)
            {
                feintCandidateActive = true;
                feintCandidateStartTime = Time.time;
                feintCandidatePeakWindup01 = w;
            }
            else
            {
                feintCandidatePeakWindup01 = Mathf.Max(feintCandidatePeakWindup01, w);
            }
        }
        else if (feintCandidateActive && pending == SlapMechanics.SlapDirection.None)
        {
            float dur = Time.time - feintCandidateStartTime;
            bool qualifiesAsFeint =
                !attackCycle &&
                !isSlap &&
                dur >= Mathf.Max(0f, feintMinDurationSeconds) &&
                feintCandidatePeakWindup01 >= Mathf.Clamp01(feintMinWindup01);
            feintCandidateActive = false;
            feintCandidateStartTime = 0f;
            feintCandidatePeakWindup01 = 0f;
            if (qualifiesAsFeint)
            {
                feintReblockSuppressUntilTime = Time.time + Mathf.Max(0f, feintReblockSuppressSeconds);
                feintCounter++;
                int threshold = Mathf.Max(1, feintsForMistake);
                if (feintCounter >= threshold)
                {
                    feintCounter = 0;
                    if (IsLowStaminaForMistake() && Random.value <= Mathf.Clamp01(feintMistakeChance))
                    {
                        mistakeArmed = true;
                    }
                }
            }
        }
    }

    private bool ShouldKeepBlockForThreat()
    {
        if (opponent == null) return false;
        if (Time.time < mistakeOpenUntilTime) return false;
        if (Time.time < blockHardLatchUntilTime) return true;
        if (Time.time < blockCommitUntilTime) return true;
        if (Time.time - lastThreatSeenTime <= Mathf.Max(0f, blockNoDropAfterThreatSeconds)) return true;
        if (opponent.IsSlapAnimating()) return true;
        if (Time.time - lastThreatSeenTime <= Mathf.Max(0f, blockThreatMemorySeconds)) return true;
        if (opponent.GetDebugWindup01() > Mathf.Clamp01(blockReleaseWindupThreshold01)) return true;
        if (Time.time < blockLatchedUntilTime) return true;
        if (lastOpponentSlapEndTime > 0f &&
            Time.time - lastOpponentSlapEndTime <= Mathf.Max(0f, blockReleaseGraceSeconds))
        {
            return true;
        }
        return false;
    }

    private bool TryTriggerFeintMistake()
    {
        if (!mistakeArmed) return false;
        if (opponent == null) return false;
        if (!opponent.IsSlapAnimating()) return false;
        if (opponent.GetDebugSlapPower01() >= Mathf.Clamp01(feintMistakeMaxIncomingSlapPower01)) return false;
        if (state != State.Blocking) return false;

        mistakeArmed = false;
        mistakeOpenUntilTime = Time.time + Mathf.Max(0f, feintMistakeOpenBlockSeconds);
        ForceEndBlock(true);
        return true;
    }

    private bool TryKeepBlockingIfThreat()
    {
        if (!ShouldKeepBlockForThreat()) return false;
        if (slap != null && slap.IsDefenderBlockReleasing() && !HasImmediateIncomingThreat()) return false;
        // Do not re-raise block from memory-only tail after a canceled/false windup.
        if (state != State.Blocking && !HasImmediateIncomingThreat()) return false;
        AlignBlockToCurrentThreat();
        if (slap != null)
        {
            slap.AI_SetHardBlockLock(true, ResolveExpectedBlockDir());
        }
        UpdateBlockHold();
        return true;
    }

    private bool TryForceBlockForImmediateThreat()
    {
        if (opponent == null) return false;
        if (Time.time < mistakeOpenUntilTime) return false;
        if (!HasImmediateIncomingThreat()) return false;

        AlignBlockToCurrentThreat();
        if (slap != null)
        {
            slap.AI_SetHardBlockLock(true, ResolveExpectedBlockDir());
        }
        if (state == State.Blocking)
        {
            UpdateBlockHold();
            return true;
        }
        return false;
    }

    private bool MaintainThreatBlockLatch()
    {
        if (opponent == null || slap == null) return false;
        if (Time.time < mistakeOpenUntilTime) return false;

        bool immediateThreat = HasImmediateIncomingThreat();
        bool unresolvedHit = combat != null && combat.IsWaitingForSlapEnd();
        if (immediateThreat || unresolvedHit)
        {
            threatBlockLatched = true;
            threatBlockReleaseTime = Time.time +
                                     Mathf.Max(0f, threatBlockReleaseTailSeconds) +
                                     Mathf.Max(0f, tunedLatchHoldExtraSeconds);
            AlignBlockToCurrentThreat();
            slap.AI_SetHardBlockLock(true, ResolveExpectedBlockDir());
            if (state != State.Blocking)
            {
                state = State.Blocking;
                stateTimer = 0f;
                blockSpeed = blockSpeedFast;
            }
            UpdateBlockHold();
            return true;
        }

        if (threatBlockLatched && Time.time < threatBlockReleaseTime)
        {
            if (opponent.GetPendingDirection() == SlapMechanics.SlapDirection.None)
            {
                float calmGrace = ResolveCalmBlockReleaseGraceSeconds();
                threatBlockReleaseTime = Mathf.Min(threatBlockReleaseTime, Time.time + calmGrace);
            }
            // Tail may keep an existing block, but must not re-raise from idle after a fake windup.
            if (state != State.Blocking)
            {
                threatBlockLatched = false;
                if (slap != null)
                {
                    slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
                }
                return false;
            }
            AlignBlockToCurrentThreat();
            slap.AI_SetHardBlockLock(true, ResolveExpectedBlockDir());
            UpdateBlockHold();
            return true;
        }

        if (threatBlockLatched)
        {
            threatBlockLatched = false;
            slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
        }
        return false;
    }

    private bool HasImmediateIncomingThreat()
    {
        if (opponent == null) return false;
        if (opponent.IsSlapAnimating()) return true;
        if (opponent.GetPendingDirection() != SlapMechanics.SlapDirection.None &&
            opponent.GetDebugWindup01() >= Mathf.Clamp01(tunedReactToWindupFrom01) &&
            opponent.IsAttackCycleActive())
        {
            return true;
        }
        return false;
    }

    private void AlignBlockToCurrentThreat()
    {
        if (opponent == null || slap == null) return;
        var expected = ResolveExpectedBlockDir();
        if (expected == SlapMechanics.SlapDirection.None) return;

        // Never restart block while already blocking: this causes visible one-frame hand drops.
        if (state == State.Blocking)
        {
            return;
        }

        if (state != State.Blocking || blockDir != expected)
        {
            blockDir = expected;
            blockSpeed = blockSpeedFast;
            slap.AI_StartBlock(blockDir);
            state = State.Blocking;
            stateTimer = 0f;
            blockCommitUntilTime = Time.time + Mathf.Max(0f, blockMinCommitSeconds);
        }
    }

    private SlapMechanics.SlapDirection ResolveExpectedBlockDir()
    {
        if (opponent == null)
        {
            lastExpectedBlockSourceDir = SlapMechanics.SlapDirection.None;
            lastExpectedBlockResolvedDir = SlapMechanics.SlapDirection.None;
            return SlapMechanics.SlapDirection.None;
        }

        SlapMechanics.SlapDirection sourceExpected = SlapMechanics.SlapDirection.None;
        var pending = opponent.GetPendingDirection();
        if (pending != SlapMechanics.SlapDirection.None)
        {
            sourceExpected = combat != null ? combat.MirrorForOpponent(pending) : pending;
        }
        else
        {
            var slapDir = opponent.GetLastSlapDirection();
            if (opponent.IsSlapAnimating() && slapDir != SlapMechanics.SlapDirection.None)
            {
                sourceExpected = combat != null ? combat.MirrorForOpponent(slapDir) : slapDir;
            }
            else if (lastIncomingThreatDir != SlapMechanics.SlapDirection.None)
            {
                sourceExpected = lastIncomingThreatDir;
            }
            else if (blockDir != SlapMechanics.SlapDirection.None)
            {
                sourceExpected = blockDir;
            }
        }

        if (sourceExpected == SlapMechanics.SlapDirection.None)
        {
            lastExpectedBlockSourceDir = SlapMechanics.SlapDirection.None;
            lastExpectedBlockResolvedDir = SlapMechanics.SlapDirection.None;
            return SlapMechanics.SlapDirection.None;
        }

        if (sourceExpected == lastExpectedBlockSourceDir &&
            lastExpectedBlockResolvedDir != SlapMechanics.SlapDirection.None)
        {
            return lastExpectedBlockResolvedDir;
        }

        var resolved = sourceExpected;
        if (Random.value < Mathf.Clamp(tunedBlockMistakeChance, MinBlockMistakeChance, 1f))
        {
            resolved = PickNeighborDirection(sourceExpected);
        }

        lastExpectedBlockSourceDir = sourceExpected;
        lastExpectedBlockResolvedDir = resolved;
        return resolved;
    }

    private void ForceReleaseBlockForEconomyCap()
    {
        threatBlockLatched = false;
        threatBlockReleaseTime = 0f;
        blockCommitUntilTime = 0f;
        blockHardLatchUntilTime = 0f;
        blockLatchedUntilTime = 0f;
        pendingThreatSinceTime = -1f;
        pendingThreatStartTime = -1f;
        lastPendingThreatRaw = false;
        requirePendingResetAfterRelease = true;
        blockReacquireLockUntilNeutral = true;
        suppressPendingRaiseUntilTime = Time.time + Mathf.Max(0f, pendingReraiseSuppressSeconds);

        if (slap != null)
        {
            slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
            slap.AI_EndBlock();
        }
        forcedBlockReleaseSignal = true;
        blockReleasedAtTime = Time.time;
        state = State.Idle;
        stateTimer = 0f;
        noThreatBlockSeconds = 0f;
        calmNoThreatSinceTime = -1f;
    }

    private void ForceEndBlock(bool allowDuringIncomingThreat = false)
    {
        if (!allowDuringIncomingThreat)
        {
            bool immediateThreat = HasImmediateIncomingThreat();
            bool waitingForHitResolve = combat != null && combat.IsWaitingForSlapEnd();
            bool inNoDropWindow = Time.time - lastThreatSeenTime <= Mathf.Max(0f, blockNoDropAfterThreatSeconds);
            bool inHardLatchWindow = Time.time < blockHardLatchUntilTime;
            bool calmDelayPassed = calmNoThreatSinceTime >= 0f &&
                                   (Time.time - calmNoThreatSinceTime) >= Mathf.Max(0f, blockCalmReleaseDelaySeconds);
            if (immediateThreat || waitingForHitResolve || inNoDropWindow || inHardLatchWindow || threatBlockLatched || !calmDelayPassed)
            {
                return;
            }
        }
        if (slap != null)
        {
            slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
        }
        if (slap != null)
        {
            slap.AI_EndBlock();
        }
        state = State.Idle;
    }

    private void RefreshHardBlockLatch()
    {
        if (state != State.Blocking) return;
        if (Time.time < mistakeOpenUntilTime) return;

        bool immediateThreat = HasImmediateIncomingThreat();
        bool waitingForHitResolve = combat != null && combat.IsWaitingForSlapEnd();
        bool recentThreat = Time.time - lastThreatSeenTime <= Mathf.Max(0f, blockNoDropAfterThreatSeconds);
        if (immediateThreat || waitingForHitResolve || recentThreat)
        {
            blockHardLatchUntilTime = Mathf.Max(
                blockHardLatchUntilTime,
                Time.time + Mathf.Max(0f, blockHardLatchSeconds));
        }
    }

    private bool IsLowStaminaForMistake()
    {
        if (selfStats == null && slap != null)
        {
            selfStats = slap.GetComponent<CombatantStats>();
        }
        if (selfStats == null) return false;
        return selfStats.Stamina01 < Mathf.Clamp01(lowStaminaForMistake01);
    }

    private float ResolveRaiseDelayForNewThreat()
    {
        float baseDelay = Mathf.Max(0f, tunedBlockRaiseDelaySeconds);
        if (selfStats == null && slap != null)
        {
            selfStats = slap.GetComponent<CombatantStats>();
        }
        if (selfStats == null)
        {
            return baseDelay;
        }

        // Random slower reaction by stamina bands:
        // 30-50% => 1/10, 15-30% => 2/10, 0-15% => 3/10.
        float s = selfStats.Stamina01 * 100f;
        float chance = 0f;
        if (s < 15f) chance = 0.3f;
        else if (s < 30f) chance = 0.2f;
        else if (s < 50f) chance = 0.1f;

        if (chance > 0f && Random.value < chance)
        {
            return baseDelay + Mathf.Max(0f, blockRaiseDelayPenaltySeconds);
        }
        return baseDelay;
    }

    private SlapMechanics.SlapDirection GetRepeatDirectionFromPlayer()
    {
        var src = combat != null ? combat.GetPlayer() : opponent;
        if (src != null)
        {
            var dir = src.GetLastSlapDirection();
            if (dir != SlapMechanics.SlapDirection.None)
            {
                return dir;
            }
        }
        return RandomDirection();
    }

    private SlapMechanics.SlapDirection GetMirrorDirectionFromPlayer()
    {
        var src = combat != null ? combat.GetPlayer() : opponent;
        if (src != null)
        {
            var dir = src.GetLastSlapDirection();
            if (dir != SlapMechanics.SlapDirection.None)
            {
                return combat != null ? combat.MirrorForOpponent(dir) : dir;
            }
        }
        return SlapMechanics.SlapDirection.Up;
    }

    private void TrackPlayerDirectionHistory()
    {
        if (opponent == null) return;
        var dir = opponent.GetLastSlapDirection();
        if (dir == SlapMechanics.SlapDirection.None) return;
        if (dir == lastObservedPlayerSlap) return;
        lastObservedPlayerSlap = dir;
        recentPlayerSlaps[recentPlayerSlapsWriteIndex] = dir;
        recentPlayerSlapsWriteIndex = (recentPlayerSlapsWriteIndex + 1) % recentPlayerSlaps.Length;
        if (recentPlayerSlapsCount < recentPlayerSlaps.Length) recentPlayerSlapsCount++;
    }

    private SlapMechanics.SlapDirection ChooseAdaptiveAttackDirection(float playerSkill01)
    {
        float ps = Mathf.Clamp01(playerSkill01);
        patternConfidence = GetPatternConfidence(playerSameBlockStreak);
        float explorationProb = hasDifficultyProfile
            ? Mathf.Clamp01(currentProfile.explorationProb)
            : 0.30f;
        float greedyBase = Mathf.Clamp01((0.15f + 0.55f * patternConfidence) * (0.5f + 0.5f * ps));
        float greedyProbMultiplier = hasDifficultyProfile
            ? Mathf.Max(0f, currentProfile.greedyProbMultiplier)
            : 1f;
        float greedyProb = Mathf.Clamp01(greedyBase * greedyProbMultiplier);

        lastExplorationProb = explorationProb;
        lastGreedyProb = greedyProb;

        if (Random.value < explorationProb)
        {
            var explored = ChooseRandomDirectionAvoidingRepeat();
            chosenAttackDir = explored;
            chosenAttackReason = "explore";
            return explored;
        }

        bool canGreedyCounter =
            playerSameBlockStreak >= 2 &&
            lastPlayerBlockDir != SlapMechanics.SlapDirection.None &&
            Random.value < greedyProb;
        if (canGreedyCounter)
        {
            var greedy = ChooseGreedyCounterDirection(lastPlayerBlockDir);
            chosenAttackDir = greedy;
            chosenAttackReason = "greedy_counter";
            greedyVulnerabilityUntilTime = Time.time + GreedyVulnerabilityDurationSeconds;
            return greedy;
        }

        var adapted = ChooseDirectionByBlockHabits();
        chosenAttackDir = adapted;
        chosenAttackReason = "counter_block_habit";
        return adapted;
    }

    private SlapMechanics.SlapDirection ChooseAdaptiveAttackDirection()
    {
        return ChooseAdaptiveAttackDirection(playerSkill);
    }

    private static float GetPatternConfidence(int streak)
    {
        if (streak >= 4) return 1f;
        if (streak == 3) return 0.75f;
        if (streak == 2) return 0.5f;
        return 0f;
    }

    private SlapMechanics.SlapDirection ChooseRandomDirectionAvoidingRepeat()
    {
        if (AttackDirections == null || AttackDirections.Length <= 0)
        {
            return SlapMechanics.SlapDirection.Right;
        }

        int idx = Random.Range(0, AttackDirections.Length);
        int prevIdx = DirectionToIndex(chosenAttackDir);
        if (prevIdx >= 0 && AttackDirections.Length > 1 && idx == prevIdx && Random.value < 0.75f)
        {
            idx = (idx + Random.Range(1, AttackDirections.Length)) % AttackDirections.Length;
        }
        return AttackDirections[idx];
    }

    private SlapMechanics.SlapDirection ChooseDirectionByBlockHabits()
    {
        int topBlockIdx = -1;
        int secondBlockIdx = -1;
        GetTopTwoHistogramIndices(playerBlockHist, out topBlockIdx, out secondBlockIdx);
        int maxAttackHist = GetMaxHistogramValue(playerAttackHist);

        float totalWeight = 0f;
        float[] weights = new float[AttackDirections.Length];
        for (int i = 0; i < AttackDirections.Length; i++)
        {
            var dir = AttackDirections[i];
            var requiredBlockDir = combat != null ? combat.MirrorForOpponent(dir) : dir;
            int requiredBlockIdx = DirectionToIndex(requiredBlockDir);
            if (requiredBlockIdx < 0) continue;

            float blockCount = playerBlockHist[requiredBlockIdx];
            float weight = 1f / (1f + blockCount);
            if (requiredBlockIdx == topBlockIdx) weight *= 0.25f;
            else if (requiredBlockIdx == secondBlockIdx) weight *= 0.6f;

            float attackHabitBias = maxAttackHist > 0 ? (float)playerAttackHist[i] / maxAttackHist : 0f;
            weight *= Mathf.Lerp(0.9f, 1.1f, attackHabitBias);
            weight = Mathf.Max(0.0001f, weight);
            weights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.0001f)
        {
            return ChooseRandomDirectionAvoidingRepeat();
        }

        float roll = Random.value * totalWeight;
        float acc = 0f;
        for (int i = 0; i < AttackDirections.Length; i++)
        {
            acc += weights[i];
            if (roll <= acc)
            {
                return AttackDirections[i];
            }
        }

        return AttackDirections[AttackDirections.Length - 1];
    }

    private SlapMechanics.SlapDirection ChooseGreedyCounterDirection(SlapMechanics.SlapDirection repeatedBlockDir)
    {
        int repeatedBlockIdx = DirectionToIndex(repeatedBlockDir);
        if (repeatedBlockIdx < 0)
        {
            return ChooseDirectionByBlockHabits();
        }

        var preferred = AttackDirections[repeatedBlockIdx];
        var requiredForPreferred = combat != null ? combat.MirrorForOpponent(preferred) : preferred;
        if (requiredForPreferred != repeatedBlockDir && Random.value < 0.85f)
        {
            return preferred;
        }

        float totalWeight = 0f;
        float[] weights = new float[AttackDirections.Length];
        for (int i = 0; i < AttackDirections.Length; i++)
        {
            var dir = AttackDirections[i];
            var requiredBlockDir = combat != null ? combat.MirrorForOpponent(dir) : dir;
            int requiredBlockIdx = DirectionToIndex(requiredBlockDir);
            if (requiredBlockIdx < 0) continue;

            float weight = 1f;
            if (requiredBlockIdx == repeatedBlockIdx)
            {
                weight *= 0.1f;
            }
            float blockCount = playerBlockHist[requiredBlockIdx];
            weight *= 1f / (1f + blockCount);
            weight = Mathf.Max(0.0001f, weight);
            weights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.0001f)
        {
            return ChooseDirectionByBlockHabits();
        }

        float roll = Random.value * totalWeight;
        float acc = 0f;
        for (int i = 0; i < AttackDirections.Length; i++)
        {
            acc += weights[i];
            if (roll <= acc)
            {
                return AttackDirections[i];
            }
        }

        return ChooseDirectionByBlockHabits();
    }

    private static void GetTopTwoHistogramIndices(int[] histogram, out int topIndex, out int secondIndex)
    {
        topIndex = -1;
        secondIndex = -1;
        if (histogram == null || histogram.Length <= 0) return;

        int topVal = int.MinValue;
        int secondVal = int.MinValue;
        for (int i = 0; i < histogram.Length; i++)
        {
            int value = histogram[i];
            if (value > topVal)
            {
                secondVal = topVal;
                secondIndex = topIndex;
                topVal = value;
                topIndex = i;
            }
            else if (value > secondVal)
            {
                secondVal = value;
                secondIndex = i;
            }
        }

        if (topVal <= 0)
        {
            topIndex = -1;
            secondIndex = -1;
            return;
        }
        if (secondVal <= 0)
        {
            secondIndex = -1;
        }
    }

    private static int GetMaxHistogramValue(int[] histogram)
    {
        if (histogram == null || histogram.Length <= 0) return 0;
        int max = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            if (histogram[i] > max) max = histogram[i];
        }
        return max;
    }

    private void GetAdvancedStyleTuning(out float tunedBlockChance, out float tunedFastChance, out float tunedReactFrom01, out float fakeWindupChance)
    {
        tunedBlockChance = blockChance;
        tunedFastChance = blockFastChance;
        tunedReactFrom01 = reactToWindupFrom01;
        fakeWindupChance = 0.08f;

        switch (advancedStyle)
        {
            case AIStyle.Safe:
                tunedBlockChance = Mathf.Clamp01(blockChance + 0.15f);
                tunedFastChance = Mathf.Clamp01(blockFastChance + 0.2f);
                tunedReactFrom01 = Mathf.Clamp01(reactToWindupFrom01 - 0.12f);
                fakeWindupChance = 0.04f;
                break;
            case AIStyle.Aggro:
                tunedBlockChance = Mathf.Clamp01(blockChance - 0.2f);
                tunedFastChance = Mathf.Clamp01(blockFastChance - 0.15f);
                tunedReactFrom01 = Mathf.Clamp01(reactToWindupFrom01 + 0.1f);
                fakeWindupChance = 0.2f;
                break;
            default:
                tunedBlockChance = Mathf.Clamp01(blockChance);
                tunedFastChance = Mathf.Clamp01(blockFastChance);
                tunedReactFrom01 = Mathf.Clamp01(reactToWindupFrom01);
                fakeWindupChance = 0.08f;
                break;
        }
    }

    public void SetAdvancedAIEnabled(bool enabled)
    {
        advancedAIEnabled = enabled;
        if (slap != null)
        {
            slap.AI_SoftCancelWindup();
            slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
            slap.AI_EndBlock();
        }
        FinalizeAIWindupTelemetry();
        state = State.Idle;
        stateTimer = 0f;
        attackInProgress = false;
        opponentWasSlapping = false;
        blockLatchedUntilTime = 0f;
        blockCommitUntilTime = 0f;
        lastThreatSeenTime = -999f;
        calmNoThreatSinceTime = -1f;
        lastOpponentSlapEndTime = -999f;
        feintCandidateActive = false;
        feintCandidateStartTime = 0f;
        feintCandidatePeakWindup01 = 0f;
        feintCounter = 0;
        mistakeArmed = false;
        mistakeOpenUntilTime = 0f;
        blockHardLatchUntilTime = 0f;
        noThreatBlockSeconds = 0f;
        threatBlockLatched = false;
        threatBlockReleaseTime = 0f;
        forcedBlockReleaseSignal = false;
        lastImmediateThreatTime = -999f;
        pendingThreatSinceTime = -1f;
        blockReleasedAtTime = -999f;
        suppressPendingRaiseUntilTime = -999f;
        requirePendingResetAfterRelease = false;
        lastObservedOpponentWindup01 = 0f;
        hasLastObservedOpponentWindup = false;
        lastPendingThreatRaw = false;
        pendingThreatStartTime = -1f;
        pendingThreatRaiseDelaySeconds = Mathf.Max(0f, tunedBlockRaiseDelaySeconds);
        blockReacquireLockUntilNeutral = false;
        feintReblockSuppressUntilTime = -999f;
        lastIncomingThreatDir = SlapMechanics.SlapDirection.None;
        ResetAdaptiveState();
    }

    [System.Serializable]
    public struct AIDebugSnapshot
    {
        public string state;
        public float stateTimer;
        public bool advancedAIEnabled;
        public string role;
        public string attackDir;
        public string blockDir;
        public string expectedBlockDir;
        public bool attackInProgress;
        public bool falseWindup;
        public float windupDuration;
        public float windupHold;
        public float windupTarget;
        public float swipeSpeed;
        public float attackCooldown;
        public bool opponentIsSlapping;
        public bool opponentAttackCycle;
        public string opponentPendingDir;
        public float opponentWindup01;
        public float opponentSlapPower01;
        public bool immediateThreat;
        public bool keepBlockByThreatRules;
        public bool threatBlockLatched;
        public float blockLatchedSecondsRemaining;
        public float blockCommitSecondsRemaining;
        public float blockHardLatchSecondsRemaining;
        public float pendingRaiseSuppressSecondsRemaining;
        public float feintReblockSuppressSecondsRemaining;
        public bool mistakeArmed;
        public float mistakeOpenSecondsRemaining;
        public float playerSkillFast;
        public float playerSkillSlow;
        public float playerSkill;
        public float aiDifficulty;
        public string selectedDifficulty;
        public float patternConfidence;
        public int playerSameBlockStreak;
        public float aiSwipeSpeedLast;
        public string skillBand;
        public string chosenAttackDir;
        public string chosenAttackReason;
        public float explorationProb;
        public float greedyProb;
        public float greedyProbMultiplier;
        public float swipeSpeedMultiplier;
        public float attackCooldownAdd;
        public float tunedReactToWindupFrom01;
        public float tunedBlockRaiseDelaySeconds;
        public float tunedBlockMistakeChance;
        public float tunedLatchHoldExtraSeconds;
        public float greedyVulnerabilityRemaining;
        public float minWindupDuration;
        public float minPostHold;
        public bool minWindupDurationApplied;
        public bool minPostHoldApplied;
        public float playerAvgBlockHoldSecondsLast10;
        public float playerAvgWindupHoldSecondsLast10;
        public string reason;
    }

    public AIDebugSnapshot GetDebugSnapshot()
    {
        float currentSwipeSpeed = Mathf.Max(0f, swipeSpeed);
        float lastSwipeSpeed = Mathf.Max(0f, aiSwipeSpeedLast);
        if (lastSwipeSpeed <= 0.0001f && currentSwipeSpeed > 0.0001f)
        {
            lastSwipeSpeed = currentSwipeSpeed;
        }

        var snap = new AIDebugSnapshot
        {
            state = state.ToString(),
            stateTimer = stateTimer,
            advancedAIEnabled = advancedAIEnabled,
            role = slap != null ? slap.GetRole().ToString() : "None",
            attackDir = attackDir.ToString(),
            blockDir = blockDir.ToString(),
            expectedBlockDir = ResolveExpectedBlockDir().ToString(),
            attackInProgress = attackInProgress,
            falseWindup = falseWindup,
            windupDuration = windupDuration,
            windupHold = windupHold,
            windupTarget = windupTarget,
            swipeSpeed = currentSwipeSpeed,
            attackCooldown = attackCooldown,
            opponentIsSlapping = opponent != null && opponent.IsSlapAnimating(),
            opponentAttackCycle = opponent != null && opponent.IsAttackCycleActive(),
            opponentPendingDir = opponent != null ? opponent.GetPendingDirection().ToString() : SlapMechanics.SlapDirection.None.ToString(),
            opponentWindup01 = opponent != null ? Mathf.Clamp01(opponent.GetDebugWindup01()) : 0f,
            opponentSlapPower01 = opponent != null ? Mathf.Clamp01(opponent.GetDebugSlapPower01()) : 0f,
            immediateThreat = HasImmediateIncomingThreat(),
            keepBlockByThreatRules = ShouldKeepBlockForThreat(),
            threatBlockLatched = threatBlockLatched,
            blockLatchedSecondsRemaining = Mathf.Max(0f, blockLatchedUntilTime - Time.time),
            blockCommitSecondsRemaining = Mathf.Max(0f, blockCommitUntilTime - Time.time),
            blockHardLatchSecondsRemaining = Mathf.Max(0f, blockHardLatchUntilTime - Time.time),
            pendingRaiseSuppressSecondsRemaining = Mathf.Max(0f, suppressPendingRaiseUntilTime - Time.time),
            feintReblockSuppressSecondsRemaining = Mathf.Max(0f, feintReblockSuppressUntilTime - Time.time),
            mistakeArmed = mistakeArmed,
            mistakeOpenSecondsRemaining = Mathf.Max(0f, mistakeOpenUntilTime - Time.time),
            playerSkillFast = Mathf.Clamp01(playerSkillFast),
            playerSkillSlow = Mathf.Clamp01(playerSkillSlow),
            playerSkill = Mathf.Clamp01(playerSkill),
            aiDifficulty = Mathf.Clamp01(aiDifficulty),
            selectedDifficulty = GetCurrentDifficultyLabel(),
            patternConfidence = Mathf.Clamp01(patternConfidence),
            playerSameBlockStreak = Mathf.Max(0, playerSameBlockStreak),
            aiSwipeSpeedLast = lastSwipeSpeed,
            skillBand = GetSkillBand(),
            chosenAttackDir = chosenAttackDir.ToString(),
            chosenAttackReason = chosenAttackReason,
            explorationProb = hasDifficultyProfile ? Mathf.Clamp01(currentProfile.explorationProb) : Mathf.Clamp01(lastExplorationProb),
            greedyProb = Mathf.Clamp01(lastGreedyProb),
            greedyProbMultiplier = hasDifficultyProfile ? Mathf.Max(0f, currentProfile.greedyProbMultiplier) : 1f,
            swipeSpeedMultiplier = hasDifficultyProfile ? Mathf.Clamp(currentProfile.swipeSpeedMultiplier, 0.5f, 1.15f) : 1f,
            attackCooldownAdd = hasDifficultyProfile ? currentProfile.attackCooldownAdd : 0f,
            tunedReactToWindupFrom01 = Mathf.Clamp01(tunedReactToWindupFrom01),
            tunedBlockRaiseDelaySeconds = Mathf.Max(0f, tunedBlockRaiseDelaySeconds),
            tunedBlockMistakeChance = Mathf.Clamp(tunedBlockMistakeChance, MinBlockMistakeChance, 1f),
            tunedLatchHoldExtraSeconds = Mathf.Max(0f, tunedLatchHoldExtraSeconds),
            greedyVulnerabilityRemaining = Mathf.Max(0f, greedyVulnerabilityUntilTime - Time.time),
            minWindupDuration = ResolveMinWindupDurationSeconds(),
            minPostHold = ResolveMinPostTargetHoldSeconds(),
            minWindupDurationApplied = lastMinWindupDurationApplied,
            minPostHoldApplied = lastMinPostHoldApplied,
            playerAvgBlockHoldSecondsLast10 = Mathf.Max(0f, playerAvgBlockHoldSecondsLast10),
            playerAvgWindupHoldSecondsLast10 = Mathf.Max(0f, playerAvgWindupHoldSecondsLast10),
            reason = BuildDecisionReason()
        };

        return snap;
    }

    private string BuildDecisionReason()
    {
        if (slap == null || combat == null)
        {
            return "AI context is not initialized";
        }

        if (!combat.IsBattleStarted())
        {
            return "Battle has not started yet";
        }

        bool waitingForResolve = combat.IsWaitingForSlapEnd();
        if (waitingForResolve)
        {
            return slap.GetRole() == SlapMechanics.Role.Attacker
                ? "Waiting for hit resolution as attacker"
                : "Holding defense during unresolved hit";
        }

        if (slap.GetRole() == SlapMechanics.Role.Attacker)
        {
            if (combat.GetAIAttackDelayRemaining() > 0f)
            {
                return "Waiting attack delay before next action";
            }

            return state switch
            {
                State.Idle => "Choosing next attack pattern",
                State.Windup => $"Charging windup toward {windupTarget:0.00}",
                State.Hold => falseWindup ? "Holding fake windup (feint)" : "Holding windup before strike release",
                State.Cooldown => "Cooling down after previous attack",
                State.Blocking => "Temporarily blocking despite attacker role",
                _ => "Attacker state active"
            };
        }

        if (mistakeOpenUntilTime > Time.time)
        {
            return "Feint mistake window: defense intentionally opened";
        }

        if (slap.IsDefenderBlockReleasing())
        {
            return "Block is releasing because no immediate threat";
        }

        if (HasImmediateIncomingThreat())
        {
            return "Immediate incoming threat detected (slap or qualified windup)";
        }

        if (threatBlockLatched)
        {
            return "Holding block due threat latch tail";
        }

        if (Time.time < blockHardLatchUntilTime)
        {
            return "Holding block due hard latch timer";
        }

        if (Time.time < blockCommitUntilTime)
        {
            return "Holding block due minimum commit timer";
        }

        if (ShouldKeepBlockForThreat())
        {
            return "Holding block due threat-memory/no-drop rules";
        }

        return "No immediate threat: AI can stay/release to idle";
    }

    private void SubscribeCombatEvents()
    {
        if (combatEventsSubscribed) return;
        if (combat == null || slap == null) return;
        SlapCombatManager.OnHitResolved += HandleHitResolvedForAdaptiveSkill;
        SlapMechanics.OnSlapFired += HandleSlapFiredForAdaptiveSkill;
        combatEventsSubscribed = true;
    }

    private void UnsubscribeCombatEvents()
    {
        if (!combatEventsSubscribed) return;
        SlapCombatManager.OnHitResolved -= HandleHitResolvedForAdaptiveSkill;
        SlapMechanics.OnSlapFired -= HandleSlapFiredForAdaptiveSkill;
        combatEventsSubscribed = false;
    }

    private void HandleSlapFiredForAdaptiveSkill(SlapMechanics source, SlapMechanics.SlapEvent data)
    {
        if (combat == null || source == null) return;
        if (source == slap)
        {
            aiSwipeSpeedLast = Mathf.Max(aiSwipeSpeedLast, Mathf.Max(0f, swipeSpeed));
        }
        var player = combat.GetPlayer();
        if (player == null || source != player) return;
        int dirIndex = DirectionToIndex(data.direction);
        if (dirIndex >= 0)
        {
            playerAttackHist[dirIndex]++;
        }
        PushRecentHoldSample(playerWindupHoldRecent, Mathf.Max(0f, data.windupHoldSeconds), PlayerHoldRecentWindow, out playerAvgWindupHoldSecondsLast10);
    }

    private void HandleHitResolvedForAdaptiveSkill(SlapCombatManager.HitResolvedEvent data)
    {
        if (combat == null) return;
        var player = combat.GetPlayer();
        if (player == null) return;

        bool playerWasAttacker = data.attacker == player;
        bool playerWasDefender = data.defender == player;
        if (!playerWasAttacker && !playerWasDefender) return;

        bool playerBlocked = playerWasDefender && data.blocked;
        bool playerPerfectBlocked = playerWasDefender && data.perfectBlock;
        bool playerLandedHit =
            playerWasAttacker &&
            !data.blocked &&
            !data.perfectBlock &&
            data.appliedDamage > 0f;

        SlapMechanics.SlapDirection playerResolvedBlockDir = SlapMechanics.SlapDirection.None;
        if (playerWasDefender && (playerBlocked || playerPerfectBlocked))
        {
            playerResolvedBlockDir = combat.MirrorForOpponent(data.direction);
            int blockIndex = DirectionToIndex(playerResolvedBlockDir);
            if (blockIndex >= 0)
            {
                playerBlockHist[blockIndex]++;
            }
            if (playerResolvedBlockDir == lastPlayerBlockDir)
            {
                playerSameBlockStreak++;
            }
            else
            {
                lastPlayerBlockDir = playerResolvedBlockDir;
                playerSameBlockStreak = 1;
            }
            PushRecentHoldSample(
                playerBlockHoldRecent,
                Mathf.Max(0f, player.GetDefenderBlockHoldSeconds()),
                PlayerHoldRecentWindow,
                out playerAvgBlockHoldSecondsLast10);
        }
        else if (playerWasDefender)
        {
            playerSameBlockStreak = 0;
            lastPlayerBlockDir = SlapMechanics.SlapDirection.None;
        }

        var sample = new ExchangeSample
        {
            playerWasAttacker = playerWasAttacker,
            playerLandedHit = playerLandedHit,
            playerBlocked = playerBlocked,
            playerPerfectBlocked = playerPerfectBlocked,
            appliedDamage = Mathf.Max(0f, data.appliedDamage),
            baseDamagePercent = Mathf.Max(0f, data.baseDamagePercent),
            finalDamagePercent = Mathf.Max(0f, data.finalDamagePercent)
        };

        PushExchangeSample(fastWindow, sample, FastSkillWindowSize);
        PushExchangeSample(slowWindow, sample, SlowSkillWindowSize);
        RecalculateAdaptiveSkill();
    }

    private void PushExchangeSample(System.Collections.Generic.Queue<ExchangeSample> window, ExchangeSample sample, int maxSize)
    {
        if (window == null) return;
        window.Enqueue(sample);
        while (window.Count > Mathf.Max(1, maxSize))
        {
            window.Dequeue();
        }
    }

    private void RecalculateAdaptiveSkill()
    {
        playerSkillFast = CalculateWindowScore(fastWindow, playerSkillFast);
        playerSkillSlow = CalculateWindowScore(slowWindow, playerSkillSlow);
        playerSkill = Mathf.Clamp01(0.65f * playerSkillSlow + 0.35f * playerSkillFast);
        UpdateAIDifficulty();
    }

    private void UpdateAIDifficulty()
    {
        aiDifficulty = hasDifficultyProfile
            ? Mathf.Clamp01(currentProfile.aiDifficultyTelemetry01)
            : 0.5f;
    }

    private void RefreshDifficultyProfileFromSettings(bool force = false)
    {
        GameDifficulty selected = GameSettings.SelectedDifficulty;
        if (!force && hasDifficultyProfile && selected == currentDifficulty) return;
        ApplyDifficultyProfile(selected);
    }

    private void ApplyDifficultyProfile(GameDifficulty difficulty)
    {
        currentDifficulty = difficulty;
        currentProfile = BuildDifficultyProfile(difficulty);
        hasDifficultyProfile = true;
        aiDifficulty = Mathf.Clamp01(currentProfile.aiDifficultyTelemetry01);
        RecomputeDefenseTuning();
    }

    private static AIDifficultyProfile BuildDifficultyProfile(GameDifficulty difficulty)
    {
        return difficulty switch
        {
            GameDifficulty.Easy => new AIDifficultyProfile
            {
                difficulty = GameDifficulty.Easy,
                aiDifficultyTelemetry01 = 0.25f,
                minWindupDuration = 1.35f,
                windupDurationMultiplier = 1.35f,
                minPostHold = 0.25f,
                attackCooldownAdd = 0.25f,
                swipeSpeedMultiplier = 0.70f,
                explorationProb = 0.45f,
                greedyProbMultiplier = 0.50f,
                tunedReactToWindupFrom01 = 0.67f,
                tunedBlockRaiseDelaySeconds = 0.43f,
                tunedBlockMistakeChance = 0.21f,
                tunedLatchHoldExtraSeconds = 0.16f,
                calmBlockNoThreatCapSeconds = 0.35f,
                hardBlockHoldCapSeconds = 1.10f,
                calmBlockReleaseGraceSeconds = 0.25f
            },
            GameDifficulty.Hard => new AIDifficultyProfile
            {
                difficulty = GameDifficulty.Hard,
                aiDifficultyTelemetry01 = 0.75f,
                minWindupDuration = 0.65f,
                windupDurationMultiplier = 1.25f,
                minPostHold = 0.10f,
                attackCooldownAdd = -0.10f,
                swipeSpeedMultiplier = 0.95f,
                explorationProb = 0.15f,
                greedyProbMultiplier = 1.25f,
                tunedReactToWindupFrom01 = 0.55f,
                tunedBlockRaiseDelaySeconds = 0.24f,
                tunedBlockMistakeChance = 0.06f,
                tunedLatchHoldExtraSeconds = 0.24f,
                calmBlockNoThreatCapSeconds = 0.20f,
                hardBlockHoldCapSeconds = 0.78f,
                calmBlockReleaseGraceSeconds = 0.16f
            },
            _ => new AIDifficultyProfile
            {
                difficulty = GameDifficulty.Normal,
                aiDifficultyTelemetry01 = 0.50f,
                minWindupDuration = 1.00f,
                windupDurationMultiplier = 1.15f,
                minPostHold = 0.18f,
                attackCooldownAdd = 0.00f,
                swipeSpeedMultiplier = 0.85f,
                explorationProb = 0.30f,
                greedyProbMultiplier = 1.00f,
                tunedReactToWindupFrom01 = 0.61f,
                tunedBlockRaiseDelaySeconds = 0.33f,
                tunedBlockMistakeChance = 0.13f,
                tunedLatchHoldExtraSeconds = 0.20f,
                calmBlockNoThreatCapSeconds = 0.26f,
                hardBlockHoldCapSeconds = 0.92f,
                calmBlockReleaseGraceSeconds = 0.20f
            }
        };
    }

    private void RecomputeDefenseTuning()
    {
        if (!hasDifficultyProfile)
        {
            ApplyDifficultyProfile(GameDifficulty.Normal);
        }

        tunedReactToWindupFrom01 = Mathf.Clamp01(currentProfile.tunedReactToWindupFrom01);
        tunedBlockRaiseDelaySeconds = Mathf.Max(0f, currentProfile.tunedBlockRaiseDelaySeconds);
        tunedBlockMistakeChance = Mathf.Clamp(currentProfile.tunedBlockMistakeChance, MinBlockMistakeChance, 1f);
        tunedLatchHoldExtraSeconds = Mathf.Max(0f, currentProfile.tunedLatchHoldExtraSeconds);
    }

    private float ResolvePostTargetHoldSeconds()
    {
        float profile01 = hasDifficultyProfile ? Mathf.Clamp01(currentProfile.aiDifficultyTelemetry01) : 0.5f;
        float baseHold = Mathf.Lerp(PostTargetHoldLowSkillSeconds, PostTargetHoldHighSkillSeconds, profile01);
        float minHold = Mathf.Max(0f, windupHoldRange.x);
        float maxHold = Mathf.Max(minHold, Mathf.Max(windupHoldRange.y, baseHold));
        float hold = Mathf.Max(0f, baseHold + Random.Range(0f, PostTargetHoldRandomMaxSeconds));
        float minPostHold = ResolveMinPostTargetHoldSeconds();
        bool floorApplied = hold < minPostHold;
        hold = Mathf.Max(hold, minPostHold);
        lastMinPostHoldApplied = floorApplied;
        if (floorApplied)
        {
            anyMinPostHoldApplied = true;
        }
        return Mathf.Clamp(hold, minHold, maxHold + PostTargetHoldRandomMaxSeconds);
    }

    private float ResolveWindupTarget01()
    {
        float d = hasDifficultyProfile ? Mathf.Clamp01(currentProfile.aiDifficultyTelemetry01) : 0.5f;
        if (d < 0.45f)
        {
            return 1f;
        }

        float midToHigh = Mathf.InverseLerp(0.45f, 1f, d);
        float minTarget = Mathf.Lerp(0.95f, 0.85f, midToHigh);
        float maxTarget = 1f;
        return Mathf.Clamp01(Random.Range(minTarget, maxTarget));
    }

    private float ResolveWindupDurationSeconds(float target01)
    {
        float d = hasDifficultyProfile ? Mathf.Clamp01(currentProfile.aiDifficultyTelemetry01) : 0.5f;
        float minDur = Mathf.Min(windupDurationRange.x, windupDurationRange.y);
        float maxDur = Mathf.Max(windupDurationRange.x, windupDurationRange.y);
        float baseDuration = Random.Range(minDur, maxDur);
        float durationMultiplier = Mathf.Lerp(0.65f, 0.42f, d);
        float targetScale = Mathf.Clamp(target01, 0.85f, 1f);
        float duration = Mathf.Max(0.12f, baseDuration * durationMultiplier * targetScale);
        float profileWindupDurationMultiplier = hasDifficultyProfile
            ? Mathf.Max(0.01f, currentProfile.windupDurationMultiplier)
            : 1f;
        duration *= profileWindupDurationMultiplier;
        float minWindupDuration = ResolveMinWindupDurationSeconds();
        bool floorApplied = duration < minWindupDuration;
        duration = Mathf.Max(duration, minWindupDuration);
        if (IsAIExhaustedForAttackTempo())
        {
            duration += Random.Range(0.12f, 0.20f);
        }
        lastMinWindupDurationApplied = floorApplied;
        if (floorApplied)
        {
            anyMinWindupDurationApplied = true;
        }
        return duration;
    }

    private float ResolveMinWindupDurationSeconds()
    {
        if (!hasDifficultyProfile) return 0.85f;
        return Mathf.Max(0f, currentProfile.minWindupDuration);
    }

    private float ResolveMinPostTargetHoldSeconds()
    {
        if (!hasDifficultyProfile) return 0.15f;
        return Mathf.Max(0f, currentProfile.minPostHold);
    }

    private float ResolveAttackCooldownSeconds(float rawCooldownSeconds)
    {
        float add = hasDifficultyProfile ? currentProfile.attackCooldownAdd : 0f;
        float cooldownFloor = Mathf.Max(0.05f, Mathf.Min(attackCooldownRange.x, attackCooldownRange.y) + add);
        float cooldown = Mathf.Max(0f, rawCooldownSeconds + add);
        if (IsAIExhaustedForAttackTempo())
        {
            cooldownFloor = Mathf.Max(cooldownFloor, 0.55f);
        }
        return Mathf.Max(0f, Mathf.Max(cooldown, cooldownFloor));
    }

    private bool IsAIExhaustedForAttackTempo()
    {
        if (selfStats == null && slap != null)
        {
            selfStats = slap.GetComponent<CombatantStats>();
        }
        if (selfStats == null) return false;
        return selfStats.Stamina <= 0f && selfStats.Health > 0f;
    }

    private float ResolveCalmBlockReleaseGraceSeconds()
    {
        if (!hasDifficultyProfile)
        {
            return 0.20f;
        }
        return Mathf.Clamp(
            currentProfile.calmBlockReleaseGraceSeconds,
            CalmBlockReleaseGraceMinSeconds,
            CalmBlockReleaseGraceMaxSeconds);
    }

    private void BeginAIWindupTelemetry()
    {
        aiWindupTelemetryActive = true;
        aiWindupStartTimeCurrent = -1f;
        aiWindupTargetReachedCurrent = false;
        aiWindupTargetReachedTimeCurrent = -1f;
        aiWindupChargeSecondsCurrent = 0f;
        aiWindupPostHoldSecondsCurrent = 0f;
    }

    private void UpdateAIWindupTelemetry()
    {
        if (!aiWindupTelemetryActive || slap == null) return;

        float now = Time.time;
        float target = Mathf.Clamp01(windupTarget);
        float windup01 = Mathf.Clamp01(slap.GetDebugWindup01());
        float reachThreshold = Mathf.Clamp01(target - 0.01f);

        if (aiWindupStartTimeCurrent < 0f)
        {
            if (attackInProgress && windup01 > 0.02f)
            {
                aiWindupStartTimeCurrent = now;
            }
            else
            {
                return;
            }
        }

        float start = aiWindupStartTimeCurrent;
        float elapsed = Mathf.Max(0f, now - start);

        if (!aiWindupTargetReachedCurrent && windup01 >= reachThreshold)
        {
            aiWindupTargetReachedCurrent = true;
            aiWindupTargetReachedTimeCurrent = now;
        }

        if (aiWindupTargetReachedCurrent)
        {
            aiWindupChargeSecondsCurrent = Mathf.Max(0f, aiWindupTargetReachedTimeCurrent - start);
            aiWindupPostHoldSecondsCurrent = Mathf.Max(0f, now - aiWindupTargetReachedTimeCurrent);
        }
        else
        {
            aiWindupChargeSecondsCurrent = elapsed;
            aiWindupPostHoldSecondsCurrent = 0f;
        }
    }

    private void FinalizeAIWindupTelemetry()
    {
        if (!aiWindupTelemetryActive) return;
        UpdateAIWindupTelemetry();
        if (aiWindupStartTimeCurrent >= 0f)
        {
            AddCappedSample(aiWindupChargeSamples, aiWindupChargeSecondsCurrent);
            AddCappedSample(aiWindupPostHoldSamples, aiWindupPostHoldSecondsCurrent);
        }
        aiWindupTelemetryActive = false;
        aiWindupStartTimeCurrent = 0f;
        aiWindupTargetReachedCurrent = false;
        aiWindupTargetReachedTimeCurrent = 0f;
        aiWindupChargeSecondsCurrent = 0f;
        aiWindupPostHoldSecondsCurrent = 0f;
    }

    public bool ConsumeForcedBlockReleaseSignal()
    {
        if (!forcedBlockReleaseSignal) return false;
        forcedBlockReleaseSignal = false;
        return true;
    }

    private static void AddCappedSample(System.Collections.Generic.List<float> sink, float value)
    {
        if (sink == null) return;
        float sample = Mathf.Max(0f, value);
        sink.Add(sample);
        if (sink.Count > WindupTelemetrySampleCap)
        {
            sink.RemoveAt(0);
        }
    }

    private static float CalculateListAverage(System.Collections.Generic.List<float> samples, float? extraSample = null)
    {
        int count = samples != null ? samples.Count : 0;
        if (extraSample.HasValue) count++;
        if (count <= 0) return 0f;

        float sum = 0f;
        if (samples != null)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                sum += Mathf.Max(0f, samples[i]);
            }
        }
        if (extraSample.HasValue)
        {
            sum += Mathf.Max(0f, extraSample.Value);
        }
        return sum / count;
    }

    private static float CalculateListP90(System.Collections.Generic.List<float> samples, float? extraSample = null)
    {
        int baseCount = samples != null ? samples.Count : 0;
        int totalCount = baseCount + (extraSample.HasValue ? 1 : 0);
        if (totalCount <= 0) return 0f;

        var copy = new System.Collections.Generic.List<float>(totalCount);
        if (samples != null)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                copy.Add(Mathf.Max(0f, samples[i]));
            }
        }
        if (extraSample.HasValue)
        {
            copy.Add(Mathf.Max(0f, extraSample.Value));
        }
        copy.Sort();
        int index = Mathf.Clamp(Mathf.FloorToInt(0.9f * (copy.Count - 1)), 0, copy.Count - 1);
        return copy[index];
    }

    private static void PushRecentHoldSample(System.Collections.Generic.Queue<float> queue, float value, int maxSize, out float average)
    {
        average = 0f;
        if (queue == null) return;
        queue.Enqueue(Mathf.Max(0f, value));
        while (queue.Count > Mathf.Max(1, maxSize))
        {
            queue.Dequeue();
        }
        if (queue.Count <= 0) return;

        float sum = 0f;
        foreach (float sample in queue)
        {
            sum += Mathf.Max(0f, sample);
        }
        average = sum / queue.Count;
    }

    private float CalculateWindowScore(System.Collections.Generic.Queue<ExchangeSample> window, float fallback)
    {
        if (window == null || window.Count <= 0)
        {
            return Mathf.Clamp01(fallback);
        }

        float sum = 0f;
        int count = 0;
        foreach (var sample in window)
        {
            sum += GetPlayerExchangeScore(sample);
            count++;
        }
        if (count <= 0) return Mathf.Clamp01(fallback);
        return Mathf.Clamp01(sum / count);
    }

    private static float GetPlayerExchangeScore(ExchangeSample sample)
    {
        if (sample.playerWasAttacker)
        {
            return Mathf.Clamp01(sample.appliedDamage / 60f);
        }
        if (sample.playerPerfectBlocked) return 1f;
        if (sample.playerBlocked) return 0.75f;
        return 0f;
    }

    private float GetAISwipeSpeedCmPerSec()
    {
        float s = 0.5f;
        float low = Mathf.Lerp(5f, 18f, s);
        float high = Mathf.Lerp(12f, 35f, s);

        float cfgSlowMin = Mathf.Min(slowSwipeSpeedRange.x, slowSwipeSpeedRange.y);
        float cfgSlowMax = Mathf.Max(slowSwipeSpeedRange.x, slowSwipeSpeedRange.y);
        float cfgFastMin = Mathf.Min(fastSwipeSpeedRange.x, fastSwipeSpeedRange.y);
        float cfgFastMax = Mathf.Max(fastSwipeSpeedRange.x, fastSwipeSpeedRange.y);
        bool hasConfigRange = cfgSlowMax > 0.01f || cfgFastMax > 0.01f;
        if (hasConfigRange)
        {
            if (cfgSlowMax <= 0.01f)
            {
                cfgSlowMin = low;
                cfgSlowMax = high;
            }
            if (cfgFastMax <= 0.01f)
            {
                cfgFastMin = low;
                cfgFastMax = high;
            }

            float cfgLow = Mathf.Lerp(cfgSlowMin, cfgFastMin, s);
            float cfgHigh = Mathf.Lerp(cfgSlowMax, cfgFastMax, s);
            if (cfgHigh < cfgLow)
            {
                float tmp = cfgLow;
                cfgLow = cfgHigh;
                cfgHigh = tmp;
            }

            low = Mathf.Lerp(low, cfgLow, 0.5f);
            high = Mathf.Lerp(high, cfgHigh, 0.5f);
        }

        low = Mathf.Max(0.01f, low);
        high = Mathf.Max(low + 0.01f, high);
        float speed = Random.Range(low, high);
        float multiplier = hasDifficultyProfile
            ? Mathf.Clamp(currentProfile.swipeSpeedMultiplier, 0.5f, 1.15f)
            : 1f;
        speed *= multiplier;
        return Mathf.Clamp(speed, 0.01f, 32f);
    }

    private void ResetAdaptiveState()
    {
        RefreshDifficultyProfileFromSettings(true);
        fastWindow.Clear();
        slowWindow.Clear();
        System.Array.Clear(playerAttackHist, 0, playerAttackHist.Length);
        System.Array.Clear(playerBlockHist, 0, playerBlockHist.Length);
        playerBlockHoldRecent.Clear();
        playerWindupHoldRecent.Clear();
        playerAvgBlockHoldSecondsLast10 = 0f;
        playerAvgWindupHoldSecondsLast10 = 0f;
        playerSkillFast = 0.5f;
        playerSkillSlow = 0.5f;
        playerSkill = 0.5f;
        aiDifficulty = hasDifficultyProfile ? currentProfile.aiDifficultyTelemetry01 : 0.5f;
        patternConfidence = 0f;
        aiSwipeSpeedLast = 0f;
        chosenAttackDir = SlapMechanics.SlapDirection.None;
        chosenAttackReason = "none";
        lastExplorationProb = 0f;
        lastGreedyProb = 0f;
        lastPlayerBlockDir = SlapMechanics.SlapDirection.None;
        lastExpectedBlockSourceDir = SlapMechanics.SlapDirection.None;
        lastExpectedBlockResolvedDir = SlapMechanics.SlapDirection.None;
        playerSameBlockStreak = 0;
        greedyVulnerabilityUntilTime = -999f;
        lastMinWindupDurationApplied = false;
        lastMinPostHoldApplied = false;
        anyMinWindupDurationApplied = false;
        anyMinPostHoldApplied = false;
        RecomputeDefenseTuning();
        pendingThreatRaiseDelaySeconds = Mathf.Max(0f, tunedBlockRaiseDelaySeconds);
        noThreatBlockSeconds = 0f;
        aiWindupChargeSamples.Clear();
        aiWindupPostHoldSamples.Clear();
        aiWindupTelemetryActive = false;
        aiWindupTargetReachedCurrent = false;
        aiWindupStartTimeCurrent = 0f;
        aiWindupTargetReachedTimeCurrent = 0f;
        aiWindupChargeSecondsCurrent = 0f;
        aiWindupPostHoldSecondsCurrent = 0f;
        forcedBlockReleaseSignal = false;
    }

    public float GetAvgAIWindupChargeSeconds()
    {
        if (aiWindupTelemetryActive) UpdateAIWindupTelemetry();
        return CalculateListAverage(aiWindupChargeSamples, aiWindupTelemetryActive ? aiWindupChargeSecondsCurrent : (float?)null);
    }

    public float GetP90AIWindupChargeSeconds()
    {
        if (aiWindupTelemetryActive) UpdateAIWindupTelemetry();
        return CalculateListP90(aiWindupChargeSamples, aiWindupTelemetryActive ? aiWindupChargeSecondsCurrent : (float?)null);
    }

    public float GetAvgAIWindupPostHoldSeconds()
    {
        if (aiWindupTelemetryActive) UpdateAIWindupTelemetry();
        return CalculateListAverage(aiWindupPostHoldSamples, aiWindupTelemetryActive ? aiWindupPostHoldSecondsCurrent : (float?)null);
    }

    public float GetP90AIWindupPostHoldSeconds()
    {
        if (aiWindupTelemetryActive) UpdateAIWindupTelemetry();
        return CalculateListP90(aiWindupPostHoldSamples, aiWindupTelemetryActive ? aiWindupPostHoldSecondsCurrent : (float?)null);
    }

    public float GetMinWindupDurationSecondsForTelemetry()
    {
        return ResolveMinWindupDurationSeconds();
    }

    public float GetMinPostHoldSecondsForTelemetry()
    {
        return ResolveMinPostTargetHoldSeconds();
    }

    public string GetCurrentDifficultyLabel()
    {
        return currentDifficulty.ToString();
    }

    public float GetSwipeSpeedMultiplierForTelemetry()
    {
        return hasDifficultyProfile ? Mathf.Clamp(currentProfile.swipeSpeedMultiplier, 0.5f, 1.15f) : 1f;
    }

    public float GetExplorationProbForTelemetry()
    {
        return hasDifficultyProfile ? Mathf.Clamp01(currentProfile.explorationProb) : 0.30f;
    }

    public float GetGreedyProbMultiplierForTelemetry()
    {
        return hasDifficultyProfile ? Mathf.Max(0f, currentProfile.greedyProbMultiplier) : 1f;
    }

    public float GetAttackCooldownAddForTelemetry()
    {
        return hasDifficultyProfile ? currentProfile.attackCooldownAdd : 0f;
    }

    public bool GetMinWindupDurationAppliedAny()
    {
        return anyMinWindupDurationApplied;
    }

    public bool GetMinPostHoldAppliedAny()
    {
        return anyMinPostHoldApplied;
    }

    private string GetSkillBand()
    {
        if (aiDifficulty < 0.35f) return "Novice";
        if (aiDifficulty < 0.7f) return "Mid";
        return "Pro";
    }

    private static int DirectionToIndex(SlapMechanics.SlapDirection dir)
    {
        return dir switch
        {
            SlapMechanics.SlapDirection.Up => 0,
            SlapMechanics.SlapDirection.Down => 1,
            SlapMechanics.SlapDirection.Left => 2,
            SlapMechanics.SlapDirection.Right => 3,
            SlapMechanics.SlapDirection.UpLeft => 4,
            SlapMechanics.SlapDirection.UpRight => 5,
            SlapMechanics.SlapDirection.DownLeft => 6,
            SlapMechanics.SlapDirection.DownRight => 7,
            _ => -1
        };
    }

    private SlapMechanics.SlapDirection PickNeighborDirection(SlapMechanics.SlapDirection sourceDir)
    {
        SlapMechanics.SlapDirection[] ring =
        {
            SlapMechanics.SlapDirection.Up,
            SlapMechanics.SlapDirection.UpRight,
            SlapMechanics.SlapDirection.Right,
            SlapMechanics.SlapDirection.DownRight,
            SlapMechanics.SlapDirection.Down,
            SlapMechanics.SlapDirection.DownLeft,
            SlapMechanics.SlapDirection.Left,
            SlapMechanics.SlapDirection.UpLeft
        };

        int idx = -1;
        for (int i = 0; i < ring.Length; i++)
        {
            if (ring[i] == sourceDir)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0) return sourceDir;

        int offset = Random.value < 0.5f ? -1 : 1;
        int nextIdx = (idx + offset + ring.Length) % ring.Length;
        return ring[nextIdx];
    }

    private SlapMechanics.SlapDirection RandomDirection()
    {
        int i = Random.Range(0, 8);
        return i switch
        {
            0 => SlapMechanics.SlapDirection.Up,
            1 => SlapMechanics.SlapDirection.Down,
            2 => SlapMechanics.SlapDirection.Left,
            3 => SlapMechanics.SlapDirection.Right,
            4 => SlapMechanics.SlapDirection.UpLeft,
            5 => SlapMechanics.SlapDirection.UpRight,
            6 => SlapMechanics.SlapDirection.DownLeft,
            _ => SlapMechanics.SlapDirection.DownRight,
        };
    }
}
