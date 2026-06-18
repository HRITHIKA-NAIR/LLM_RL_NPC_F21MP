using UnityEngine;
using System;

public class CombatHandler : MonoBehaviour
{
    [Header("Combat Settings")]
    public float attackRange = 2.5f;
    public float engagementRange = 6f;   // when InCombatRange switches on
    public float attackDamage = 10f;
    public float attackCooldown = 1.0f;

    [Header("References")]
    public HPSystem ownerHP;
    public NPCAnimator npcAnimator;  // null for bot — bot calls directly

    private float cooldownTimer = 0f;
    private Transform currentTarget;

    // Event that NPCAgent listens to in order to give reward
    public event Action OnSuccessfulHit;

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public void SetTarget(Transform target)
    {
        currentTarget = target;
    }

    public bool CanAttack()
    {
        if (ownerHP != null && ownerHP.IsDead()) return false;
        if (cooldownTimer > 0f) return false;
        if (currentTarget == null) return false;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        return dist <= attackRange;
    }

    public bool IsInCombatEngagementRange()
    {
        if (currentTarget == null) return false;
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        return dist <= engagementRange;
    }

    // Called by Animator Event at the hit frame of the attack clip
    // This is the ONLY place where damage is dealt — never from Update or OnActionReceived
    public void TriggerDamage()
    {
        if (currentTarget == null) return;
        if (ownerHP != null && ownerHP.IsDead()) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange + 0.5f)  // slight forgiveness for animation offset
        {
            HPSystem targetHP = currentTarget.GetComponent<HPSystem>();
            if (targetHP != null && !targetHP.IsDead())
            {
                targetHP.TakeDamage(attackDamage);
                OnSuccessfulHit?.Invoke();

                // Tell the target's animator to play hit reaction
                NPCAnimator targetNPCAnim = currentTarget.GetComponent<NPCAnimator>();
                if (targetNPCAnim != null)
                    targetNPCAnim.TriggerHitReaction();
            }
        }

        cooldownTimer = attackCooldown;
    }

    // Called by NPCAgent.OnActionReceived when action = Attack
    public void ExecuteAttack()
    {
        if (!CanAttack()) return;

        cooldownTimer = attackCooldown;

        if (npcAnimator != null)
            npcAnimator.TriggerAttackAnimation();

        TriggerDamage();      // TEMPORARY TEST
    }

    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }
}