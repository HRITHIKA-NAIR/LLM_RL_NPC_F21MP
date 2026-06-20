using UnityEngine;

// Handles the bot's melee damage delivery.
// TriggerDamage() is called by Animator Event at the hit frame —
// the same event you added to the attack clips on Day 2.
// This component sits on OpponentBot.
public class BotCombatHandler : MonoBehaviour
{
    [Header("Settings")]
    public float meleeDamage = 15f;
    public float meleeRange = 3.5f;   // slightly generous for bot fairness

    [Header("NPC references")]
    public Transform[] npcTransforms;
    public HPSystem[] npcHPSystems;
    public NPCAnimator[] npcAnimators;

    // Called by the Animator Event embedded in the attack clip at the hit frame
    // (same event you placed on Day 2 — the function name must match exactly)
    public void TriggerDamage()
    {
        // Find nearest NPC within range
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i].IsDead()) continue;
            float dist = Vector3.Distance(transform.position, npcTransforms[i].position);
            if (dist <= meleeRange)
            {
                npcHPSystems[i].TakeDamage(meleeDamage);

                if (npcAnimators[i] != null)
                    npcAnimators[i].TriggerHitReaction();

                // Only hit the one closest NPC per swing
                break;
            }
        }
    }
}