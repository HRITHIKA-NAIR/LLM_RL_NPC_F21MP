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
        npcDamageDealt = new float[npcTransforms.Length];
    }

    void Update()
    {
        if (hpSystem.IsDead()) return;
        if (activeStateMachine == null) return;

        if (meleeCooldownTimer > 0f)
            meleeCooldownTimer -= Time.deltaTime;

        var (moveDir, action) = activeStateMachine.DecideAction();

        // Always apply gravity
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

        // Combat range for animator — drives CombatIdle vs Idle
        Transform nearest = GetNearestLivingNPC();
        bool inCombatRange = nearest != null &&
            Vector3.Distance(transform.position, nearest.position) <= 6f;
        animator.SetBool("InCombatRange", inCombatRange);
    }

    // ── MOVE ──
    void HandleMove(Vector3 moveDir)
    {
        if (moveDir == Vector3.zero)
        {
            SetAnimatorStopped();
            return;
        }

        // Move
        controller.Move(moveDir * moveSpeed * Time.deltaTime);

        // Rotate to face movement direction — same pattern as NPCAnimator
        Quaternion targetRot = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotateSpeed * Time.deltaTime);

        // Animator — always forward since character faces movement direction
        float speed = moveDir.magnitude;
        animator.SetFloat("SpeedZ", speed, 0.1f, Time.deltaTime);
        animator.SetFloat("SpeedX", 0f, 0.1f, Time.deltaTime);
        animator.SetFloat("SpeedMagnitude", speed, 0.1f, Time.deltaTime);
    }

    // ── MELEE ATTACK ──
    void HandleMelee()
    {
        // Stop movement animation
        SetAnimatorStopped();

        // Rotate to face nearest NPC before swinging
        // This ensures the animation looks correct and the hit lands
        Transform target = GetNearestLivingNPC();
        if (target != null)
        {
            Vector3 dirToTarget = (target.position - transform.position);
            dirToTarget.y = 0;
            if (dirToTarget.magnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            }
        }

        // Fire attack if cooldown ready
        if (meleeCooldownTimer > 0f) return;
        if (target == null) return;
        if (Vector3.Distance(transform.position, target.position) > meleeRange)
            return;

        // make sure the bot is actually facing the target
        Vector3 dir = (target.position - transform.position);
        dir.y = 0;

        float angle = Vector3.Angle(transform.forward, dir);

        if (angle > 35f)
            return;

        meleeCooldownTimer = meleeCooldown;
        animator.SetTrigger("Attack");

        // Since the attack clip is read-only,
        // apply damage directly in code.
        BotCombatHandler combatHandler = GetComponent<BotCombatHandler>();

        if (combatHandler != null)
            combatHandler.TriggerDamage();
    }

    // ── RANGED ATTACK ──
    void HandleRanged(Vector3 alsoMoveDir)
    {
        // Keep moving if a direction was provided (e.g., evasive bot retreating while firing)
        if (alsoMoveDir != Vector3.zero)
        {
            controller.Move(alsoMoveDir * moveSpeed * Time.deltaTime);
            Quaternion targetRot = Quaternion.LookRotation(alsoMoveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotateSpeed * Time.deltaTime);

            float speed = alsoMoveDir.magnitude;
            animator.SetFloat("SpeedZ", speed, 0.1f, Time.deltaTime);
            animator.SetFloat("SpeedX", 0f, 0.1f, Time.deltaTime);
            animator.SetFloat("SpeedMagnitude", speed, 0.1f, Time.deltaTime);
        }
        else
        {
            SetAnimatorStopped();
        }

        // Fire orb regardless of movement
        if (orbLauncher == null || !orbLauncher.CanFireOrb()) return;
        Transform orbTarget = GetNearestLivingNPC();
        if (orbTarget == null) return;
        orbLauncher.FireOrb(orbTarget);
    }

    // ── IDLE ──
    void HandleIdle()
    {
        SetAnimatorStopped();
    }

    // ── HELPERS ──
    void SetAnimatorStopped()
    {
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
            if (d < minDist) { minDist = d; nearest = npcTransforms[i]; }
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
                break;
            }
        }
    }

    public void ResetForNewEpisode()
    {
        meleeCooldownTimer = 0f;
        if (orbLauncher != null) orbLauncher.ResetCooldown();
        if (npcDamageDealt != null)
            for (int i = 0; i < npcDamageDealt.Length; i++)
                npcDamageDealt[i] = 0f;

        BalancedBot balanced = GetComponent<BalancedBot>();
        if (balanced != null) balanced.ResetDamageTracking();
    }
}