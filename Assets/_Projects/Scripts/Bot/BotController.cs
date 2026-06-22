using UnityEngine;

public class BotController : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Animator animator;
    public OrbLauncher orbLauncher;
    public HPSystem hpSystem;

    [Header("NPC references — drag all 5 in Inspector")]
    public Transform[] npcTransforms;
    public HPSystem[] npcHPSystems;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotateSpeed = 200f;

    [Header("Melee attack")]
    public float meleeCooldown = 0.5f;
    public float meleeDamage = 15f;
    public float meleeRange = 4.0f;

    private BotStateMachine activeStateMachine;
    private float meleeCooldownTimer = 0f;
    private float[] npcDamageDealt;

    void Start()
    {
        RefreshActiveStateMachine();

        if (npcTransforms != null)
            npcDamageDealt = new float[npcTransforms.Length];
    }

    void Update()
    {
        if (hpSystem == null || hpSystem.IsDead())
            return;

        if (activeStateMachine == null)
            return;

        if (meleeCooldownTimer > 0f)
            meleeCooldownTimer -= Time.deltaTime;

        var (moveDir, action) = activeStateMachine.DecideAction();

        // Always apply gravity
        if (controller != null)
            controller.Move(Vector3.down * 9.81f * Time.deltaTime);

        switch (action)
        {
            case BotAction.Move:
                HandleMove(moveDir);
                break;

            case BotAction.MeleeAttack:
                HandleMelee();
                break;

            case BotAction.RangedAttack:
                HandleRanged(moveDir);
                break;

            case BotAction.Idle:
                HandleIdle();
                break;
        }

        // Combat range for animator
        Transform nearest = GetNearestLivingNPC();

        bool inCombatRange =
            nearest != null &&
            Vector3.Distance(transform.position, nearest.position) <= 6f;

        if (animator != null)
            animator.SetBool("InCombatRange", inCombatRange);
    }

    // ───────────────── MOVE ─────────────────
    void HandleMove(Vector3 moveDir)
    {
        if (moveDir == Vector3.zero)
        {
            SetAnimatorStopped();
            return;
        }

        if (controller != null)
        {
            CollisionFlags flags =
                controller.Move(moveDir * moveSpeed * Time.deltaTime);

            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Hit a wall/obstacle → strafe sideways
                moveDir = Vector3.Cross(Vector3.up, moveDir).normalized;
                controller.Move(moveDir * moveSpeed * Time.deltaTime);
            }
        }

        Quaternion targetRot = Quaternion.LookRotation(moveDir);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime);

        float speed = moveDir.magnitude;

        if (animator != null)
        {
            animator.SetFloat("SpeedZ", speed, 0.1f, Time.deltaTime);
            animator.SetFloat("SpeedX", 0f, 0.1f, Time.deltaTime);
            animator.SetFloat("SpeedMagnitude", speed, 0.1f, Time.deltaTime);
        }
    }

    // ───────────────── MELEE ATTACK ─────────────────
    void HandleMelee()
    {
        SetAnimatorStopped();

        Transform target = GetNearestLivingNPC();

        if (target != null)
        {
            Vector3 dirToTarget = target.position - transform.position;
            dirToTarget.y = 0;

            if (dirToTarget.magnitude > 0.01f)
            {
                Quaternion targetRot =
                    Quaternion.LookRotation(dirToTarget.normalized);

                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotateSpeed * Time.deltaTime);
            }
        }

        if (meleeCooldownTimer > 0f) return;
        if (target == null) return;
        if (Vector3.Distance(transform.position, target.position) > meleeRange) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0;
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > 35f) return;

        meleeCooldownTimer = meleeCooldown;

        if (animator != null)
            animator.SetTrigger("Attack");

        BotCombatHandler combatHandler = GetComponent<BotCombatHandler>();
        if (combatHandler != null)
            combatHandler.TriggerDamage();
    }

    // ───────────────── RANGED ATTACK ─────────────────
    void HandleRanged(Vector3 alsoMoveDir)
    {
        if (alsoMoveDir != Vector3.zero)
        {
            if (controller != null)
                controller.Move(alsoMoveDir * moveSpeed * Time.deltaTime);

            Quaternion targetRot = Quaternion.LookRotation(alsoMoveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotateSpeed * Time.deltaTime);

            float speed = alsoMoveDir.magnitude;

            if (animator != null)
            {
                animator.SetFloat("SpeedZ", speed, 0.1f, Time.deltaTime);
                animator.SetFloat("SpeedX", 0f, 0.1f, Time.deltaTime);
                animator.SetFloat("SpeedMagnitude", speed, 0.1f, Time.deltaTime);
            }
        }
        else
        {
            SetAnimatorStopped();
        }

        if (orbLauncher == null) return;
        if (!orbLauncher.CanFireOrb()) return;

        Transform orbTarget = GetNearestLivingNPC();
        if (orbTarget == null) return;

        orbLauncher.FireOrb(orbTarget);
    }

    // ───────────────── IDLE ─────────────────
    void HandleIdle()
    {
        SetAnimatorStopped();
    }

    // ───────────────── HELPERS ─────────────────
    void SetAnimatorStopped()
    {
        if (animator == null) return;

        animator.SetFloat("SpeedMagnitude", 0f, 0.15f, Time.deltaTime);
        animator.SetFloat("SpeedZ", 0f, 0.15f, Time.deltaTime);
        animator.SetFloat("SpeedX", 0f, 0.15f, Time.deltaTime);
    }

    Transform GetNearestLivingNPC()
    {
        Transform nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i] == null || npcHPSystems[i].IsDead()) continue;

            float d = Vector3.Distance(transform.position, npcTransforms[i].position);
            if (d < minDist)
            {
                minDist = d;
                nearest = npcTransforms[i];
            }
        }

        return nearest;
    }

    public void RefreshActiveStateMachine()
    {
        var allMachines = GetComponents<BotStateMachine>();
        activeStateMachine = null;

        foreach (var m in allMachines)
        {
            if (m.enabled)
            {
                activeStateMachine = m;
                m.npcTransforms = npcTransforms;
                m.npcHPSystems = npcHPSystems;
                m.ownHP = hpSystem;
                m.ownTransform = transform;

                Debug.Log($"[BotController] Active state machine set to: {m.GetType().Name}");
                break;
            }
        }

        if (activeStateMachine == null)
            Debug.LogWarning("[BotController] RefreshActiveStateMachine: no enabled BotStateMachine found.");
    }

    public void ResetForNewEpisode()
    {
        meleeCooldownTimer = 0f;

        if (orbLauncher != null)
            orbLauncher.ResetCooldown();

        if (npcDamageDealt != null)
            for (int i = 0; i < npcDamageDealt.Length; i++)
                npcDamageDealt[i] = 0f;

        BalancedBot balanced = GetComponent<BalancedBot>();
        if (balanced != null)
            balanced.ResetDamageTracking();
    }

    // ── FIXED: reads from cached activeStateMachine, no NullRef risk ──
    public string GetCurrentBotType()
    {
        if (activeStateMachine == null)
            return "Unknown";

        return activeStateMachine.GetType().Name
            .Replace("Bot", "");  // "AggressiveBot" → "Aggressive" etc.
    }
}