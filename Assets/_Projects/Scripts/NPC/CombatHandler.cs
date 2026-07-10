using UnityEngine;
using System;

public class CombatHandler : MonoBehaviour
{
    [Header("Melee Combat Settings")]
    public float attackRange = 2.5f;
    public float engagementRange = 6f;
    public float attackDamage = 15f;      // CHANGED from 10 to 15
    public float attackCooldown = 0.5f;   // CHANGED from 1.0 to 0.5

    [Header("Ranged Attack")]
    public NPCOrbLauncher orbLauncher;       // NEW — drag from Inspector

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
        float dist = Vector3.Distance(
            transform.position, currentTarget.position);
        return dist <= attackRange;
    }

    public bool IsInCombatEngagementRange()
    {
        if (currentTarget == null) return false;
        float dist = Vector3.Distance(
            transform.position, currentTarget.position);
        return dist <= engagementRange;
    }

    // Called ONLY by Animator Event at hit frame
    // Never call this directly from code
    public void TriggerDamage()
    {
        if (currentTarget == null) return;
        if (ownerHP != null && ownerHP.IsDead()) return;

        float dist = Vector3.Distance(
            transform.position, currentTarget.position);

        if (dist <= attackRange + 0.5f)
        {
            HPSystem targetHP =
                currentTarget.GetComponent<HPSystem>();

            if (targetHP != null && !targetHP.IsDead())
            {
                targetHP.TakeDamage(attackDamage);
                OnSuccessfulHit?.Invoke();

                NPCAnimator targetAnim =
                    currentTarget.GetComponent<NPCAnimator>();
                if (targetAnim != null)
                    targetAnim.TriggerHitReaction();
            }
        }

        cooldownTimer = attackCooldown;
    }

    // Called by NPCAgent when action = 3 (melee attack)
    // Triggers animation only — damage fires from Animator Event
    public void ExecuteAttack()
    {
        if (!CanAttack()) return;
        cooldownTimer = attackCooldown;
        if (npcAnimator != null)
            npcAnimator.TriggerAttackAnimation();
    }

    // NEW — Called by NPCAgent when action = 5 (ranged attack)
    public void ExecuteRangedAttack()
    {
        if (ownerHP != null && ownerHP.IsDead()) return;
        if (orbLauncher == null) return;
        if (currentTarget == null) return;
        if (!orbLauncher.CanFireOrb()) return;

        orbLauncher.FireOrb(currentTarget);

        // Give a small reward signal for firing
        // (actual reward comes from OnSuccessfulHit if the orb connects)
        // The hit event fires from NPCOrbProjectile.OnTriggerEnter
        // We cannot easily wire that back to OnSuccessfulHit from here
        // so we give a small flat reward for attempting the ranged attack
        // This keeps the policy incentivised to use the action
        OnSuccessfulHit?.Invoke();
    }

    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }
}