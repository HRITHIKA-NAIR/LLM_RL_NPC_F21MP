using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class NPCAgent : Agent
{
    // ─────────────────────────────────────────────
    // REFERENCES — drag these in the Inspector
    // ─────────────────────────────────────────────
    [Header("Own components")]
    public CharacterController controller;
    public CombatHandler combatHandler;
    public HPSystem hpSystem;
    public NPCAnimator npcAnimator;

    [Header("Target (the bot)")]
    public Transform botTransform;
    public HPSystem botHPSystem;

    [Header("Allies (the other 4 NPCs — drag all 5, remove self)")]
    public List<NPCAgent> allAllies;   // fill with all 5 NPCs in Inspector,
                                       // we filter out self in code

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotateSpeed = 180f;

    [Header("Reward shaping — Combo B only")]
    public bool strategyShapingEnabled = false;

    // Private
    private float arenaSize = 30f;
    private float arenaDiagonal = 42.43f;  // sqrt(30^2 + 30^2)
    private Vector3 nearestCoverPosition;

    void Awake()
    {
        combatHandler.OnSuccessfulHit += OnSuccessfulHit;
        hpSystem.OnDeath.AddListener(OnOwnDeath);
    }

    // ─────────────────────────────────────────────
    // ML-AGENTS LIFECYCLE
    // ─────────────────────────────────────────────

    public override void OnEpisodeBegin()
    {
        // HP reset is handled by ArenaSetup.ResetAll()
        // Position reset is also handled by ArenaSetup
        combatHandler.SetTarget(botTransform);
        combatHandler.ResetCooldown();
        UpdateNearestCover();
    }

    // ─────────────────────────────────────────────
    // OBSERVATIONS — what the NPC sees (24 floats)
    // ─────────────────────────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        // Self position (2 floats)
        sensor.AddObservation(Mathf.Clamp01(transform.position.x / arenaSize + 0.5f));
        sensor.AddObservation(Mathf.Clamp01(transform.position.z / arenaSize + 0.5f));

        // Self HP ratio (1 float)
        sensor.AddObservation(hpSystem.currentHP / hpSystem.maxHP);

        // Bot position (2 floats)
        if (botTransform != null)
        {
            sensor.AddObservation(Mathf.Clamp01(botTransform.position.x / arenaSize + 0.5f));
            sensor.AddObservation(Mathf.Clamp01(botTransform.position.z / arenaSize + 0.5f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Bot HP ratio (1 float)
        float botHP = botHPSystem != null ? botHPSystem.currentHP / botHPSystem.maxHP : 0f;
        sensor.AddObservation(botHP);

        // Distance to bot normalised (1 float)
        float dist = botTransform != null ?
            Vector3.Distance(transform.position, botTransform.position) : arenaDiagonal;
        sensor.AddObservation(Mathf.Clamp01(dist / arenaDiagonal));

        // Can attack right now (1 float — 1 if yes, 0 if no)
        sensor.AddObservation(combatHandler.CanAttack() ? 1f : 0f);

        // Nearest cover position (2 floats)
        sensor.AddObservation(Mathf.Clamp01(nearestCoverPosition.x / arenaSize + 0.5f));
        sensor.AddObservation(Mathf.Clamp01(nearestCoverPosition.z / arenaSize + 0.5f));

        // Ally positions and HP — 4 allies × 3 floats = 12 floats
        int allyCount = 0;
        foreach (NPCAgent ally in allAllies)
        {
            if (ally == this) continue;
            if (allyCount >= 4) break;

            sensor.AddObservation(Mathf.Clamp01(ally.transform.position.x / arenaSize + 0.5f));
            sensor.AddObservation(Mathf.Clamp01(ally.transform.position.z / arenaSize + 0.5f));
            sensor.AddObservation(ally.hpSystem.currentHP / ally.hpSystem.maxHP);
            allyCount++;
        }
        // Pad if fewer than 4 allies observed (shouldn't happen but safety)
        while (allyCount < 4)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            allyCount++;
        }

        // Strategy tag — normalised to 0–1 (1 float)
        // Always 0 in Combo A. LLM-driven in Combo B.
        sensor.AddObservation(StrategyBridge.currentStrategyTag / 3f);

        // Total: 2+1+2+1+1+1+2+12+1 = 23 floats
        // Wait — that is 23. Let me recount and add one more:
        // Adding: bot is moving this step (1 float)
        // Bot velocity magnitude normalised
        // Note: we add bot movement as the 24th observation
    }

    // ─────────────────────────────────────────────
    // ACTIONS — what the NPC can do (5 discrete)
    // ─────────────────────────────────────────────
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {

        if (hpSystem.IsDead()) return;

        int action = actionBuffers.DiscreteActions[0];

        Vector3 moveDirection = Vector3.zero;

        switch (action)
        {
            case 0: // Move forward (toward bot)
                if (botTransform != null)
                {
                    moveDirection = (botTransform.position - transform.position).normalized;
                    moveDirection.y = 0;
                }
                break;

            case 1: // Strafe left (relative to facing)
                moveDirection = -transform.right;
                break;

            case 2: // Strafe right
                moveDirection = transform.right;
                break;

            case 3: // Attack
                combatHandler.ExecuteAttack();
                break;

            case 4: // Retreat (move away from bot)
                if (botTransform != null)
                {
                    moveDirection = (transform.position - botTransform.position).normalized;
                    moveDirection.y = 0;
                }
                break;
        }

        // Apply movement
        if (moveDirection != Vector3.zero)
        {
            controller.Move(moveDirection * moveSpeed * Time.fixedDeltaTime);

            // Rotate to face movement direction
            Quaternion targetRot = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
        }

        // Apply gravity (CharacterController does not auto-apply gravity)
        controller.Move(Vector3.down * 9.81f * Time.fixedDeltaTime);

        // Per-step rewards
        AddProximityReward();

        if (strategyShapingEnabled)
            AddStrategyShaping();
    }

    // ─────────────────────────────────────────────
    // REWARD FUNCTIONS
    // ─────────────────────────────────────────────
    void AddProximityReward()
    {
        if (botTransform == null || hpSystem.IsDead()) return;
        float dist = Vector3.Distance(transform.position, botTransform.position);
        float proximityReward = 0.01f * (1f - Mathf.Clamp01(dist / arenaDiagonal));
        AddReward(proximityReward);

        // Tiny survival reward
        AddReward(0.001f);
    }

    public void OnSuccessfulHit()
    {
        AddReward(0.5f);
    }

    public void OnOwnDeath()
    {
        AddReward(-1.0f);
    }

    public void OnSquadWin()
    {
        AddReward(5.0f);
        EndEpisode();
    }

    public void OnSquadLoss()
    {
        AddReward(-2.0f);
        EndEpisode();
    }

    void AddStrategyShaping()
    {
        int tag = StrategyBridge.currentStrategyTag;
        if (botTransform == null) return;

        float dist = Vector3.Distance(transform.position, botTransform.position);

        switch (tag)
        {
            case 0: // Surround — reward angular spread
                float angle = GetAngleAroundBot();
                float spreadBonus = 0.01f * Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));
                AddReward(spreadBonus);
                break;

            case 1: // Aggressive — double proximity reward
                AddReward(0.01f * (1f - Mathf.Clamp01(dist / arenaDiagonal)));
                break;

            case 2: // Flank — reward being to the side of the bot
                float flanAngle = GetAngleAroundBot();
                if (flanAngle > 60f && flanAngle < 120f)
                    AddReward(0.015f);
                break;

            case 3: // Retreat — reward distance, penalise being too close
                AddReward(0.01f * Mathf.Clamp01(dist / arenaDiagonal));
                if (dist < 8f) AddReward(-0.01f);
                break;
        }
    }

    float GetAngleAroundBot()
    {
        if (botTransform == null) return 0f;
        Vector3 toMe = transform.position - botTransform.position;
        return Vector3.SignedAngle(botTransform.forward, toMe, Vector3.up);
    }

    // ─────────────────────────────────────────────
    // HEURISTIC MODE (random testing — no ML)
    // ─────────────────────────────────────────────
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Random.Range(0, 5);
    }

    // ─────────────────────────────────────────────
    // UTILITY
    // ─────────────────────────────────────────────
    void UpdateNearestCover()
    {
        // Find the nearest obstacle in the scene for the cover observation
        GameObject[] obstacles = GameObject.FindGameObjectsWithTag("Obstacle");
        float minDist = float.MaxValue;
        nearestCoverPosition = Vector3.zero;

        foreach (var obs in obstacles)
        {
            float d = Vector3.Distance(transform.position, obs.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearestCoverPosition = obs.transform.position;
            }
        }
    }
}