using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SlapFightTraceRecorder : MonoBehaviour
{
    [Header("Recording")]
    [SerializeField] private bool autoRecordOnPlay = true;
    [SerializeField] private bool recordFrameSnapshots = true;
    [SerializeField, Min(1f)] private float frameSamplesPerSecond = 60f;
    [SerializeField] private bool flushEachWrite = false;
    [SerializeField] private bool logPathToConsole = true;

    [Header("Live Console")]
    [SerializeField] private bool mirrorRecordsToConsole = true;
    [SerializeField] private bool mirrorFrameSnapshotsToConsole = true;
    [SerializeField] private string liveConsolePrefix = "[FightTraceLive]";

    private SlapCombatManager combat;
    private SlapMechanics player;
    private SlapMechanics ai;
    private SlapAIController aiController;
    private CombatantStats playerStats;
    private CombatantStats aiStats;

    private StreamWriter writer;
    private string sessionId;
    private string filePath;
    private bool recording;
    private bool hasLoggedStart;
    private float nextSampleTime;
    private int sampleIndex;

    private bool lastBattleStarted;
    private bool lastWaitingResolve;
    private bool lastPlayerTurn;
    private bool lastGameOver;
    private bool hasTurnState;

    private bool hasStatSnapshot;
    private float lastPlayerHealth;
    private float lastPlayerStamina;
    private float lastAIHealth;
    private float lastAIStamina;

    private bool hasAIDecisionSnapshot;
    private SlapAIController.AIDebugSnapshot lastAIDecision;
    private float lastObservedRealtime;
    private int lastObservedFrame;
    private float lastKnownPlayerHealth;
    private float lastKnownAIHealth;
    private float lastKnownPlayerStamina;
    private float lastKnownAIStamina;
    private bool hasKnownPlayerStats;
    private bool hasKnownAIStats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var all = FindObjectsByType<SlapFightTraceRecorder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all != null && all.Length > 0) return;

        var go = new GameObject("SlapFightTraceRecorder");
        DontDestroyOnLoad(go);
        go.AddComponent<SlapFightTraceRecorder>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SlapMechanics.OnSlapFired += HandleSlapFired;
        SlapCombatManager.OnHitResolved += HandleHitResolved;
        SlapCombatManager.OnStaminaDrained += HandleStaminaDrained;
        TryResolveContext();
        if (autoRecordOnPlay)
        {
            TryBeginRecording();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SlapMechanics.OnSlapFired -= HandleSlapFired;
        SlapCombatManager.OnHitResolved -= HandleHitResolved;
        SlapCombatManager.OnStaminaDrained -= HandleStaminaDrained;
        EndRecording("component_disabled");
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!autoRecordOnPlay) return;

        lastObservedRealtime = Time.realtimeSinceStartup;
        lastObservedFrame = Time.frameCount;

        TryResolveContext();
        if (!recording)
        {
            TryBeginRecording();
            if (!recording) return;
        }

        if (!hasLoggedStart)
        {
            WriteSessionStart();
        }

        bool battleStarted = combat != null && combat.IsBattleStarted();
        bool waitingResolve = combat != null && combat.IsWaitingForSlapEnd();
        bool playerTurn = combat != null && combat.IsPlayerTurn();
        bool gameOver = IsGameOver();

        if (battleStarted != lastBattleStarted)
        {
            WriteCombatEvent("battle_state", battleStarted ? "started" : "stopped");
            lastBattleStarted = battleStarted;
        }

        if (waitingResolve != lastWaitingResolve)
        {
            WriteCombatEvent("hit_resolution", waitingResolve ? "waiting" : "resolved");
            lastWaitingResolve = waitingResolve;
        }

        if (!hasTurnState || playerTurn != lastPlayerTurn)
        {
            hasTurnState = true;
            lastPlayerTurn = playerTurn;
            WriteCombatEvent("turn", playerTurn ? "player_attacker" : "ai_attacker");
        }

        TrackStatChanges();
        TrackAIDecisions();

        if (recordFrameSnapshots)
        {
            float now = Time.unscaledTime;
            if (now >= nextSampleTime)
            {
                WriteFrameSnapshot(battleStarted, waitingResolve, playerTurn);
                sampleIndex++;
                float step = 1f / Mathf.Max(1f, frameSamplesPerSecond);
                nextSampleTime = Mathf.Max(nextSampleTime + step, now + (step * 0.25f));
            }
        }

        if (gameOver && !lastGameOver)
        {
            lastGameOver = true;
            WriteCombatEvent("game_over", BuildGameOverText());
            EndRecording("game_over");
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying) return;

        if (recording)
        {
            EndRecording($"scene_changed:{scene.name}");
        }

        ResetRuntimeState();
        TryResolveContext();
        if (autoRecordOnPlay)
        {
            TryBeginRecording();
        }
    }

    private void HandleSlapFired(SlapMechanics source, SlapMechanics.SlapEvent data)
    {
        if (!recording || writer == null) return;

        var rec = new SlapEventRecord
        {
            type = "slap_fired",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            actorName = source != null ? source.gameObject.name : "unknown",
            actorRole = source != null ? source.GetRole().ToString() : "None",
            direction = data.direction.ToString(),
            windup01 = Mathf.Clamp01(data.windup01),
            slapPower01 = Mathf.Clamp01(data.slapPower01),
            windupHoldSeconds = Mathf.Max(0f, data.windupHoldSeconds)
        };
        WriteRecord(rec);
    }

    private void HandleStaminaDrained(SlapCombatManager.StaminaDrainEvent data)
    {
        if (!recording || writer == null) return;

        var rec = new StaminaDrainRecord
        {
            type = "stamina_drain",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            actorName = data.actor != null ? data.actor.gameObject.name : "unknown",
            actorRole = data.actor != null ? data.actor.GetRole().ToString() : "None",
            reason = data.reason,
            amount = data.amount,
            ratePerSecond = data.ratePerSecond,
            staminaBefore = data.staminaBefore,
            staminaAfter = data.staminaAfter
        };
        WriteRecord(rec);
    }

    private void HandleHitResolved(SlapCombatManager.HitResolvedEvent data)
    {
        if (!recording || writer == null) return;

        var rec = new HitResolvedRecord
        {
            type = "hit_resolved",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            attackerName = data.attacker != null ? data.attacker.gameObject.name : "unknown",
            defenderName = data.defender != null ? data.defender.gameObject.name : "unknown",
            direction = data.direction.ToString(),
            blocked = data.blocked,
            perfectBlock = data.perfectBlock,
            slapProgressAtResolve = data.slapProgressAtResolve,
            resolveAtProgress = data.resolveAtProgress,
            baseDamagePercent = data.baseDamagePercent,
            finalDamagePercent = data.finalDamagePercent,
            appliedDamage = data.appliedDamage,
            attackerExhaustedDamagePenalty = data.attackerExhaustedDamagePenalty,
            attackerStaminaBefore = data.attackerStaminaBefore,
            attackerStaminaAfter = data.attackerStaminaAfter,
            defenderHealthBefore = data.defenderHealthBefore,
            defenderHealthAfter = data.defenderHealthAfter
        };
        WriteRecord(rec);
    }

    private void TryResolveContext()
    {
        if (combat == null)
        {
            combat = SlapCombatManager.Instance;
            if (combat == null)
            {
                var allCombat = FindObjectsByType<SlapCombatManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (allCombat != null && allCombat.Length > 0)
                {
                    combat = allCombat[0];
                }
            }
        }

        if (combat != null)
        {
            player = combat.GetPlayer();
            ai = combat.GetAI();
        }

        if (player != null && playerStats == null)
        {
            playerStats = player.GetComponent<CombatantStats>();
        }

        if (ai != null)
        {
            if (aiStats == null)
            {
                aiStats = ai.GetComponent<CombatantStats>();
            }
            if (aiController == null)
            {
                aiController = ai.GetComponent<SlapAIController>();
            }
        }
    }

    private void TryBeginRecording()
    {
        if (recording) return;

        string root = Path.Combine(Application.persistentDataPath, "fight_logs");
        try
        {
            Directory.CreateDirectory(root);
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = Path.Combine(root, $"fight_trace_{sessionId}.jsonl");
            writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
            writer.AutoFlush = flushEachWrite;
            recording = true;
            nextSampleTime = Time.unscaledTime;
            sampleIndex = 0;
            hasLoggedStart = false;
            hasStatSnapshot = false;
            hasAIDecisionSnapshot = false;

            if (logPathToConsole)
            {
                Debug.Log($"[FightTrace] Recording to: {filePath}");
            }
        }
        catch (Exception ex)
        {
            recording = false;
            writer = null;
            Debug.LogError($"[FightTrace] Failed to start recording: {ex.Message}");
        }
    }

    private void EndRecording(string reason)
    {
        if (!recording) return;

        WriteSessionEnd(reason);

        try
        {
            writer?.Flush();
            writer?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FightTrace] Failed to finalize log writer: {ex.Message}");
        }

        writer = null;
        recording = false;
    }

    private void ResetRuntimeState()
    {
        combat = null;
        player = null;
        ai = null;
        aiController = null;
        playerStats = null;
        aiStats = null;
        hasLoggedStart = false;
        lastBattleStarted = false;
        lastWaitingResolve = false;
        lastPlayerTurn = false;
        lastGameOver = false;
        hasTurnState = false;
        hasStatSnapshot = false;
        hasAIDecisionSnapshot = false;
        lastObservedRealtime = 0f;
        lastObservedFrame = 0;
        hasKnownPlayerStats = false;
        hasKnownAIStats = false;
    }

    private bool IsGameOver()
    {
        return (playerStats != null && playerStats.Health <= 0f) ||
               (aiStats != null && aiStats.Health <= 0f);
    }

    private string BuildGameOverText()
    {
        bool playerDead = playerStats != null && playerStats.Health <= 0f;
        bool aiDead = aiStats != null && aiStats.Health <= 0f;

        if (playerDead && aiDead) return "double_ko";
        if (aiDead) return "player_wins";
        if (playerDead) return "ai_wins";
        return "unknown";
    }

    private void TrackStatChanges()
    {
        if (playerStats == null || aiStats == null) return;

        lastKnownPlayerHealth = playerStats.Health;
        lastKnownPlayerStamina = playerStats.Stamina;
        lastKnownAIHealth = aiStats.Health;
        lastKnownAIStamina = aiStats.Stamina;
        hasKnownPlayerStats = true;
        hasKnownAIStats = true;

        if (!hasStatSnapshot)
        {
            hasStatSnapshot = true;
            lastPlayerHealth = playerStats.Health;
            lastPlayerStamina = playerStats.Stamina;
            lastAIHealth = aiStats.Health;
            lastAIStamina = aiStats.Stamina;
            return;
        }

        EmitStatDelta("player_health", playerStats.Health, ref lastPlayerHealth);
        EmitStatDelta("player_stamina", playerStats.Stamina, ref lastPlayerStamina);
        EmitStatDelta("ai_health", aiStats.Health, ref lastAIHealth);
        EmitStatDelta("ai_stamina", aiStats.Stamina, ref lastAIStamina);
    }

    private void EmitStatDelta(string metric, float currentValue, ref float lastValue)
    {
        float delta = currentValue - lastValue;
        if (Mathf.Abs(delta) <= 0.0001f) return;

        var rec = new StatChangeRecord
        {
            type = "stat_change",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            metric = metric,
            previousValue = lastValue,
            currentValue = currentValue,
            delta = delta
        };
        WriteRecord(rec);
        lastValue = currentValue;
    }

    private void TrackAIDecisions()
    {
        if (aiController == null || !recording) return;

        var snap = aiController.GetDebugSnapshot();
        bool changed = !hasAIDecisionSnapshot ||
                       !string.Equals(lastAIDecision.state, snap.state, StringComparison.Ordinal) ||
                       !string.Equals(lastAIDecision.reason, snap.reason, StringComparison.Ordinal) ||
                       !string.Equals(lastAIDecision.chosenAttackDir, snap.chosenAttackDir, StringComparison.Ordinal) ||
                       !string.Equals(lastAIDecision.chosenAttackReason, snap.chosenAttackReason, StringComparison.Ordinal) ||
                       !string.Equals(lastAIDecision.skillBand, snap.skillBand, StringComparison.Ordinal) ||
                       !string.Equals(lastAIDecision.expectedBlockDir, snap.expectedBlockDir, StringComparison.Ordinal) ||
                       !string.Equals(lastAIDecision.blockDir, snap.blockDir, StringComparison.Ordinal) ||
                       Mathf.Abs(lastAIDecision.playerSkillFast - snap.playerSkillFast) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.playerSkillSlow - snap.playerSkillSlow) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.playerSkill - snap.playerSkill) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.aiDifficulty - snap.aiDifficulty) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.patternConfidence - snap.patternConfidence) > 0.0001f ||
                       lastAIDecision.playerSameBlockStreak != snap.playerSameBlockStreak ||
                       Mathf.Abs(lastAIDecision.explorationProb - snap.explorationProb) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.greedyProb - snap.greedyProb) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.tunedReactToWindupFrom01 - snap.tunedReactToWindupFrom01) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.tunedBlockRaiseDelaySeconds - snap.tunedBlockRaiseDelaySeconds) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.tunedBlockMistakeChance - snap.tunedBlockMistakeChance) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.tunedLatchHoldExtraSeconds - snap.tunedLatchHoldExtraSeconds) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.greedyVulnerabilityRemaining - snap.greedyVulnerabilityRemaining) > 0.0001f ||
                       Mathf.Abs(lastAIDecision.aiSwipeSpeedLast - snap.aiSwipeSpeedLast) > 0.0001f ||
                       lastAIDecision.immediateThreat != snap.immediateThreat ||
                       lastAIDecision.keepBlockByThreatRules != snap.keepBlockByThreatRules ||
                       lastAIDecision.mistakeOpenSecondsRemaining <= 0f && snap.mistakeOpenSecondsRemaining > 0f;

        if (!changed) return;

        hasAIDecisionSnapshot = true;
        lastAIDecision = snap;

        var rec = new AIDecisionRecord
        {
            type = "ai_decision",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            snapshot = ToAIFrame(snap)
        };
        WriteRecord(rec);
    }

    private void WriteFrameSnapshot(bool battleStarted, bool waitingResolve, bool playerTurn)
    {
        var rec = new FrameRecord
        {
            type = "frame",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            sampleIndex = sampleIndex,
            scene = SceneManager.GetActiveScene().name,
            battleStarted = battleStarted,
            waitingHitResolve = waitingResolve,
            playerTurn = playerTurn,
            player = BuildFighterFrame(player, playerStats),
            ai = BuildFighterFrame(ai, aiStats),
            aiDecision = aiController != null ? ToAIFrame(aiController.GetDebugSnapshot()) : null
        };
        WriteRecord(rec);
    }

    private FighterFrame BuildFighterFrame(SlapMechanics source, CombatantStats stats)
    {
        if (source == null) return null;

        var frame = new FighterFrame
        {
            name = source.gameObject.name,
            role = source.GetRole().ToString(),
            pendingDirection = source.GetPendingDirection().ToString(),
            lastSlapDirection = source.GetLastSlapDirection().ToString(),
            blockDirection = source.GetCurrentBlockDirection().ToString(),
            controlledHandMoving = source.IsControlledHandMoving(),
            controlledVisualHand = source.GetControlledVisualHand().ToString(),
            controlledVisualProgress01 = Mathf.Clamp01(source.GetControlledVisualProgress01()),
            windup01 = Mathf.Clamp01(source.GetDebugWindup01()),
            slapPower01 = Mathf.Clamp01(source.GetDebugSlapPower01()),
            windupHoldSeconds = Mathf.Max(0f, source.GetWindupHoldSeconds()),
            slapProgress01 = Mathf.Clamp01(source.GetSlapProgress01()),
            slapAnimating = source.IsSlapAnimating(),
            attackCycleActive = source.IsAttackCycleActive(),
            attackerWindupHolding = source.IsAttackerWindupHolding(),
            defenderBlocking = source.IsDefenderBlocking(),
            defenderBlockReleasing = source.IsDefenderBlockReleasing(),
            defenderBlockHold01 = Mathf.Clamp01(source.GetDefenderBlockHold01()),
            defenderBlockHoldSeconds = Mathf.Max(0f, source.GetDefenderBlockHoldSeconds())
        };

        if (stats != null)
        {
            frame.health = stats.Health;
            frame.stamina = stats.Stamina;
            frame.maxHealth = stats.MaxHealth;
            frame.maxStamina = stats.MaxStamina;
        }

        source.TryGetHandWorldPose(out frame.leftHandPos, out frame.leftHandRot, out frame.rightHandPos, out frame.rightHandRot);
        source.GetHandMotionMagnitude(out frame.leftHandMotion, out frame.rightHandMotion);
        return frame;
    }

    private AIFrame ToAIFrame(SlapAIController.AIDebugSnapshot snap)
    {
        return new AIFrame
        {
            state = snap.state,
            stateTimer = snap.stateTimer,
            advancedAIEnabled = snap.advancedAIEnabled,
            role = snap.role,
            attackDir = snap.attackDir,
            blockDir = snap.blockDir,
            expectedBlockDir = snap.expectedBlockDir,
            attackInProgress = snap.attackInProgress,
            falseWindup = snap.falseWindup,
            windupDuration = snap.windupDuration,
            windupHold = snap.windupHold,
            windupTarget = snap.windupTarget,
            swipeSpeed = snap.swipeSpeed,
            attackCooldown = snap.attackCooldown,
            opponentIsSlapping = snap.opponentIsSlapping,
            opponentAttackCycle = snap.opponentAttackCycle,
            opponentPendingDir = snap.opponentPendingDir,
            opponentWindup01 = snap.opponentWindup01,
            opponentSlapPower01 = snap.opponentSlapPower01,
            immediateThreat = snap.immediateThreat,
            keepBlockByThreatRules = snap.keepBlockByThreatRules,
            threatBlockLatched = snap.threatBlockLatched,
            blockLatchedSecondsRemaining = snap.blockLatchedSecondsRemaining,
            blockCommitSecondsRemaining = snap.blockCommitSecondsRemaining,
            blockHardLatchSecondsRemaining = snap.blockHardLatchSecondsRemaining,
            pendingRaiseSuppressSecondsRemaining = snap.pendingRaiseSuppressSecondsRemaining,
            feintReblockSuppressSecondsRemaining = snap.feintReblockSuppressSecondsRemaining,
            mistakeArmed = snap.mistakeArmed,
            mistakeOpenSecondsRemaining = snap.mistakeOpenSecondsRemaining,
            playerSkillFast = snap.playerSkillFast,
            playerSkillSlow = snap.playerSkillSlow,
            playerSkill = snap.playerSkill,
            aiDifficulty = snap.aiDifficulty,
            patternConfidence = snap.patternConfidence,
            playerSameBlockStreak = snap.playerSameBlockStreak,
            aiSwipeSpeedLast = snap.aiSwipeSpeedLast,
            skillBand = snap.skillBand,
            chosenAttackDir = snap.chosenAttackDir,
            chosenAttackReason = snap.chosenAttackReason,
            explorationProb = snap.explorationProb,
            greedyProb = snap.greedyProb,
            tunedReactToWindupFrom01 = snap.tunedReactToWindupFrom01,
            tunedBlockRaiseDelaySeconds = snap.tunedBlockRaiseDelaySeconds,
            tunedBlockMistakeChance = snap.tunedBlockMistakeChance,
            tunedLatchHoldExtraSeconds = snap.tunedLatchHoldExtraSeconds,
            greedyVulnerabilityRemaining = snap.greedyVulnerabilityRemaining,
            reason = snap.reason
        };
    }

    private void WriteSessionStart()
    {
        hasLoggedStart = true;
        var rec = new SessionStartRecord
        {
            type = "session_start",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            sessionId = sessionId,
            scene = SceneManager.GetActiveScene().name,
            logPath = filePath,
            playerName = player != null ? player.gameObject.name : "none",
            aiName = ai != null ? ai.gameObject.name : "none",
            playerMaxHealth = playerStats != null ? playerStats.MaxHealth : 0f,
            aiMaxHealth = aiStats != null ? aiStats.MaxHealth : 0f,
            playerMaxStamina = playerStats != null ? playerStats.MaxStamina : 0f,
            aiMaxStamina = aiStats != null ? aiStats.MaxStamina : 0f
        };
        WriteRecord(rec);
    }

    private void WriteSessionEnd(string reason)
    {
        if (writer == null) return;

        float realtime = Time.realtimeSinceStartup;
        int frame = Time.frameCount;
        if (frame <= 0 && lastObservedFrame > 0)
        {
            frame = lastObservedFrame;
        }
        if (realtime <= 0.0001f && lastObservedRealtime > 0f)
        {
            realtime = lastObservedRealtime;
        }

        float playerHealth = hasKnownPlayerStats ? lastKnownPlayerHealth : (playerStats != null ? playerStats.Health : 0f);
        float aiHealth = hasKnownAIStats ? lastKnownAIHealth : (aiStats != null ? aiStats.Health : 0f);
        float playerStamina = hasKnownPlayerStats ? lastKnownPlayerStamina : (playerStats != null ? playerStats.Stamina : 0f);
        float aiStamina = hasKnownAIStats ? lastKnownAIStamina : (aiStats != null ? aiStats.Stamina : 0f);
        float configuredStaminaDrainSeconds = combat != null ? combat.GetStaminaDrainSeconds() : 0f;
        float maxStaminaBasis = 0f;
        if (playerStats != null) maxStaminaBasis = playerStats.MaxStamina;
        else if (aiStats != null) maxStaminaBasis = aiStats.MaxStamina;
        float drainRateSTAperSec = configuredStaminaDrainSeconds > 0.0001f
            ? maxStaminaBasis / configuredStaminaDrainSeconds
            : 0f;

        var rec = new SessionEndRecord
        {
            type = "session_end",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = realtime,
            frame = frame,
            sessionId = sessionId,
            reason = reason,
            result = BuildGameOverText(),
            playerHealth = playerHealth,
            aiHealth = aiHealth,
            playerStamina = playerStamina,
            aiStamina = aiStamina,
            staminaDrainSeconds = configuredStaminaDrainSeconds,
            drainRateSTAperSec = drainRateSTAperSec,
            samplesWritten = sampleIndex
        };
        WriteRecord(rec);
    }

    private void WriteCombatEvent(string eventName, string details)
    {
        var rec = new CombatEventRecord
        {
            type = "combat_event",
            utc = DateTime.UtcNow.ToString("o"),
            realtime = Time.realtimeSinceStartup,
            frame = Time.frameCount,
            eventName = eventName,
            details = details
        };
        WriteRecord(rec);
    }

    private void WriteRecord(object record)
    {
        if (!recording || writer == null || record == null) return;

        try
        {
            string line = JsonUtility.ToJson(record);
            writer.WriteLine(line);
            if (flushEachWrite) writer.Flush();

            if (mirrorRecordsToConsole)
            {
                if (record is FrameRecord && !mirrorFrameSnapshotsToConsole) return;
                Debug.Log($"{liveConsolePrefix} {line}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FightTrace] Failed writing record: {ex.Message}");
        }
    }

    [Serializable]
    private class SessionStartRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string sessionId;
        public string scene;
        public string logPath;
        public string playerName;
        public string aiName;
        public float playerMaxHealth;
        public float aiMaxHealth;
        public float playerMaxStamina;
        public float aiMaxStamina;
    }

    [Serializable]
    private class SessionEndRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string sessionId;
        public string reason;
        public string result;
        public float playerHealth;
        public float aiHealth;
        public float playerStamina;
        public float aiStamina;
        public float staminaDrainSeconds;
        public float drainRateSTAperSec;
        public int samplesWritten;
    }

    [Serializable]
    private class CombatEventRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string eventName;
        public string details;
    }

    [Serializable]
    private class StatChangeRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string metric;
        public float previousValue;
        public float currentValue;
        public float delta;
    }

    [Serializable]
    private class StaminaDrainRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string actorName;
        public string actorRole;
        public string reason;
        public float amount;
        public float ratePerSecond;
        public float staminaBefore;
        public float staminaAfter;
    }

    [Serializable]
    private class HitResolvedRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string attackerName;
        public string defenderName;
        public string direction;
        public bool blocked;
        public bool perfectBlock;
        public float slapProgressAtResolve;
        public float resolveAtProgress;
        public float baseDamagePercent;
        public float finalDamagePercent;
        public float appliedDamage;
        public bool attackerExhaustedDamagePenalty;
        public float attackerStaminaBefore;
        public float attackerStaminaAfter;
        public float defenderHealthBefore;
        public float defenderHealthAfter;
    }

    [Serializable]
    private class SlapEventRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public string actorName;
        public string actorRole;
        public string direction;
        public float windup01;
        public float slapPower01;
        public float windupHoldSeconds;
    }

    [Serializable]
    private class AIDecisionRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public AIFrame snapshot;
    }

    [Serializable]
    private class FrameRecord
    {
        public string type;
        public string utc;
        public float realtime;
        public int frame;
        public int sampleIndex;
        public string scene;
        public bool battleStarted;
        public bool waitingHitResolve;
        public bool playerTurn;
        public FighterFrame player;
        public FighterFrame ai;
        public AIFrame aiDecision;
    }

    [Serializable]
    private class FighterFrame
    {
        public string name;
        public string role;
        public string pendingDirection;
        public string lastSlapDirection;
        public string blockDirection;
        public bool controlledHandMoving;
        public string controlledVisualHand;
        public float controlledVisualProgress01;
        public float windup01;
        public float slapPower01;
        public float windupHoldSeconds;
        public float slapProgress01;
        public bool slapAnimating;
        public bool attackCycleActive;
        public bool attackerWindupHolding;
        public bool defenderBlocking;
        public bool defenderBlockReleasing;
        public float defenderBlockHold01;
        public float defenderBlockHoldSeconds;
        public float health;
        public float stamina;
        public float maxHealth;
        public float maxStamina;
        public Vector3 leftHandPos;
        public Quaternion leftHandRot;
        public Vector3 rightHandPos;
        public Quaternion rightHandRot;
        public float leftHandMotion;
        public float rightHandMotion;
    }

    [Serializable]
    private class AIFrame
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
        public float patternConfidence;
        public int playerSameBlockStreak;
        public float aiSwipeSpeedLast;
        public string skillBand;
        public string chosenAttackDir;
        public string chosenAttackReason;
        public float explorationProb;
        public float greedyProb;
        public float tunedReactToWindupFrom01;
        public float tunedBlockRaiseDelaySeconds;
        public float tunedBlockMistakeChance;
        public float tunedLatchHoldExtraSeconds;
        public float greedyVulnerabilityRemaining;
        public string reason;
    }
}
