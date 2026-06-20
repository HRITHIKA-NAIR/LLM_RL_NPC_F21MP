using UnityEngine;
using System;

public class CombatHandler : MonoBehaviour
{
    [Header("Combat Settings")]
    public float attackRange = 2.5f;
    public float engagementRange = 6f;
    public float attackDamage = 10f;
    public float attackCooldown = 0.5f;

    [Header("References")]
    public HPSystem ownerHP;
    public NPCAnimator npcAnimator;

    private float cooldownTimer = 0f;
    private Transform currentTarget;

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

    // Called ONLY by the Animator Event at the hit frame of the attack clip.
    // This is the single source of damage for NPCs.
    // Never call this from Update, OnActionReceived, or ExecuteAttack.
    public void TriggerDamage()
    {
        if (currentTarget == null) return;
        if (ownerHP != null && ownerHP.IsDead()) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange + 0.5f)
        {
            HPSystem targetHP = currentTarget.GetComponent<HPSystem>();
            if (targetHP != null && !targetHP.IsDead())
            {
                targetHP.TakeDamage(attackDamage);
                OnSuccessfulHit?.Invoke();

                NPCAnimator targetNPCAnim = currentTarget.GetComponent<NPCAnimator>();
                if (targetNPCAnim != null)
                    targetNPCAnim.TriggerHitReaction();
            }
        }

        cooldownTimer = attackCooldown;
    }

    // Called by NPCAgent.OnActionReceived when action = Attack.
    // Only triggers the animation — damage fires from the Animator Event.
    // Called by NPCAgent.OnActionReceived when action = Attack.
    // Triggers animation and immediately applies damage in code.
    public void ExecuteAttack()
    {
        if (!CanAttack()) return;

        // Lock cooldown immediately so rapid decisions don't fire twice
        cooldownTimer = attackCooldown;

        if (npcAnimator != null)
            npcAnimator.TriggerAttackAnimation();

        // Since animation events cannot be added to the read-only clip,
        // apply damage directly here.
        TriggerDamage();
    }

    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }
}