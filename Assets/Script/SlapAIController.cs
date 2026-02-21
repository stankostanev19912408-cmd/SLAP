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
    [SerializeField] private float blockRaiseDelaySeconds = 0.3f;
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

    public void SetCombat(SlapCombatManager manager, SlapMechanics owner)
    {
        combat = manager;
        slap = owner;
        startupTime = Time.time;
        if (slap != null)
        {
            slap.allowHumanInput = false;
        }
    }

    private void Update()
    {
        if (combat == null || slap == null) return;
        if (combat.GetAI() != slap) return;
        if (!combat.IsBattleStarted())
        {
            if (state != State.Idle)
            {
                slap.AI_SoftCancelWindup();
                slap.AI_SetHardBlockLock(false, SlapMechanics.SlapDirection.None);
                slap.AI_EndBlock();
                state = State.Idle;
                stateTimer = 0f;
            }
            return;
        }
        if (combat.IsWaitingForSlapEnd())
        {
            // During unresolved slap, freeze actions.
            if (slap.GetRole() == SlapMechanics.Role.Attacker)
            {
                if (state != State.Idle)
                {
                    slap.AI_SoftCancelWindup();
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
        float reactThreshold = Mathf.Clamp01(reactToWindupFrom01);
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
            pendingThreatRaiseDelaySeconds = Mathf.Max(0f, blockRaiseDelaySeconds);
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
                bool calmReady = calmNoThreatSinceTime >= 0f &&
                                 (Time.time - calmNoThreatSinceTime) >= Mathf.Max(0f, blockCalmReleaseDelaySeconds);
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
                        slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
                    }
                    state = State.Cooldown;
                    stateTimer = 0f;
                    attackInProgress = false;
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
                slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
            state = State.Cooldown;
            stateTimer = 0f;
            attackInProgress = false;
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

        attackDir = RandomDirection();
        falseWindup = Random.value < fakeWindupChance;
        windupDuration = Random.Range(windupDurationRange.x, windupDurationRange.y);
        windupHold = Random.Range(windupHoldRange.x, windupHoldRange.y);
        // Do not keep hand in windup longer than 2 seconds total.
        float maxTotal = 2f;
        float maxHold = Mathf.Max(0f, maxTotal - windupDuration);
        if (windupHold > maxHold) windupHold = maxHold;
        windupTarget = 1f;
        swipeSpeed = 0f;
        attackCooldown = Random.Range(attackCooldownRange.x, attackCooldownRange.y);

        slap.AI_BeginWindup(attackDir);
        state = State.Windup;
        stateTimer = 0f;
        attackStartTime = Time.time;
        attackInProgress = true;
    }

    private void UpdateAttackMirrorMode()
    {
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
                    slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
                    state = State.Cooldown;
                    stateTimer = 0f;
                    attackInProgress = false;
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
            slap.AI_TriggerSlap(attackDir, swipeSpeed, 3f);
            state = State.Cooldown;
            stateTimer = 0f;
            attackInProgress = false;
        }
    }

    private void StartWindupMirrorMode()
    {
        attackDir = RandomDirection();
        falseWindup = false;
        windupDuration = 0.95f;
        windupHold = 0.06f;
        windupTarget = 1f;
        swipeSpeed = 0f;
        attackCooldown = 0.3f;

        slap.AI_BeginWindup(attackDir);
        state = State.Windup;
        stateTimer = 0f;
        attackStartTime = Time.time;
        attackInProgress = true;
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
            threatBlockReleaseTime = Time.time + Mathf.Max(0f, threatBlockReleaseTailSeconds);
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
            opponent.GetDebugWindup01() >= Mathf.Clamp01(reactToWindupFrom01) &&
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
        if (opponent == null) return SlapMechanics.SlapDirection.None;

        var pending = opponent.GetPendingDirection();
        if (pending != SlapMechanics.SlapDirection.None)
        {
            return combat != null ? combat.MirrorForOpponent(pending) : pending;
        }

        var slapDir = opponent.GetLastSlapDirection();
        if (opponent.IsSlapAnimating() && slapDir != SlapMechanics.SlapDirection.None)
        {
            return combat != null ? combat.MirrorForOpponent(slapDir) : slapDir;
        }

        if (lastIncomingThreatDir != SlapMechanics.SlapDirection.None)
        {
            return lastIncomingThreatDir;
        }

        if (blockDir != SlapMechanics.SlapDirection.None)
        {
            return blockDir;
        }

        return SlapMechanics.SlapDirection.None;
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
        float baseDelay = Mathf.Max(0f, blockRaiseDelaySeconds);
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

    private SlapMechanics.SlapDirection ChooseAdaptiveAttackDirection()
    {
        var mirroredRecent = GetMostFrequentMirroredPlayerDirection();
        if (mirroredRecent != SlapMechanics.SlapDirection.None)
        {
            return mirroredRecent;
        }
        return GetMirrorDirectionFromPlayer();
    }

    private SlapMechanics.SlapDirection GetMostFrequentMirroredPlayerDirection()
    {
        if (recentPlayerSlapsCount <= 0) return SlapMechanics.SlapDirection.None;

        int up = 0, down = 0, left = 0, right = 0, upLeft = 0, upRight = 0, downLeft = 0, downRight = 0;
        for (int i = 0; i < recentPlayerSlapsCount; i++)
        {
            var d = recentPlayerSlaps[i];
            var m = combat != null ? combat.MirrorForOpponent(d) : d;
            switch (m)
            {
                case SlapMechanics.SlapDirection.Up: up++; break;
                case SlapMechanics.SlapDirection.Down: down++; break;
                case SlapMechanics.SlapDirection.Left: left++; break;
                case SlapMechanics.SlapDirection.Right: right++; break;
                case SlapMechanics.SlapDirection.UpLeft: upLeft++; break;
                case SlapMechanics.SlapDirection.UpRight: upRight++; break;
                case SlapMechanics.SlapDirection.DownLeft: downLeft++; break;
                case SlapMechanics.SlapDirection.DownRight: downRight++; break;
            }
        }

        int best = up;
        SlapMechanics.SlapDirection bestDir = SlapMechanics.SlapDirection.Up;
        if (down > best) { best = down; bestDir = SlapMechanics.SlapDirection.Down; }
        if (left > best) { best = left; bestDir = SlapMechanics.SlapDirection.Left; }
        if (right > best) { best = right; bestDir = SlapMechanics.SlapDirection.Right; }
        if (upLeft > best) { best = upLeft; bestDir = SlapMechanics.SlapDirection.UpLeft; }
        if (upRight > best) { best = upRight; bestDir = SlapMechanics.SlapDirection.UpRight; }
        if (downLeft > best) { best = downLeft; bestDir = SlapMechanics.SlapDirection.DownLeft; }
        if (downRight > best) { best = downRight; bestDir = SlapMechanics.SlapDirection.DownRight; }
        return bestDir;
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
        threatBlockLatched = false;
        threatBlockReleaseTime = 0f;
        lastImmediateThreatTime = -999f;
        pendingThreatSinceTime = -1f;
        blockReleasedAtTime = -999f;
        suppressPendingRaiseUntilTime = -999f;
        requirePendingResetAfterRelease = false;
        lastObservedOpponentWindup01 = 0f;
        hasLastObservedOpponentWindup = false;
        lastPendingThreatRaw = false;
        pendingThreatStartTime = -1f;
        pendingThreatRaiseDelaySeconds = Mathf.Max(0f, blockRaiseDelaySeconds);
        blockReacquireLockUntilNeutral = false;
        feintReblockSuppressUntilTime = -999f;
        lastIncomingThreatDir = SlapMechanics.SlapDirection.None;
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
