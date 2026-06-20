using UnityEngine;

// Balanced Bot playstyle:
// State-dependent logic that approximates a competent mid-level player.
// Has three modes:
//   DOMINANT: HP > 60% and few NPCs nearby — advance and attack
//   THREATENED: HP < 40% or surrounded — retreat and use orb
//   HUNTING: otherwise — target the NPC dealing the most damage
// This is the PRIMARY evaluation opponent.
public class BalancedBot : BotStateMachine
{
    [Header("Balanced Bot Thresholds")]
    public float dominantHPThreshold = 0.6f;     // above this = bot feels dominant
    public float retreatHPThreshold = 0.4f;      // below this = bot retreats
    public int surroundedNPCCount = 3;            // this many NPCs within surroundRadius = retreat
    public float surroundRadius = 5f;
    public float aggressiveNPCRadius = 8f;        // fewer than this many within = advance

    // Damage tracking — BotController fills this array each time an NPC hits the bot
    [HideInInspector] public float[] npcDamageDealt;

    public override (Vector3 moveDir, BotAction action) DecideAction()
    {
        if (npcDamageDealt == null || npcDamageDealt.Length == 0)
            npcDamageDealt = new float[npcTransforms.Length];

        Transform nearest = GetNearestLivingNPC();
        if (nearest == null) return (Vector3.zero, BotAction.Idle);

        float hpRatio = ownHP.currentHP / ownHP.maxHP;
        int nearbyNPCs = CountLivingNPCsWithinRadius(aggressiveNPCRadius);
        int surroundingNPCs = CountLivingNPCsWithinRadius(surroundRadius);

        // ── RETREAT MODE ──
        // Triggered when HP is low OR bot is surrounded
        bool shouldRetreat = hpRatio < retreatHPThreshold ||
                             surroundingNPCs >= surroundedNPCCount;

        if (shouldRetreat)
        {
            // Move toward arena boundary (away from arena centre)
            Vector3 toBoundary = ownTransform.position.normalized;
            toBoundary.y = 0;
            if (toBoundary == Vector3.zero) toBoundary = Vector3.forward;

            // Fire orb at lowest HP NPC while retreating
            Transform orbTarget = GetLowestHPLivingNPC();
            if (orbTarget != null && DistanceTo(orbTarget) <= orbRange)
                return (toBoundary, BotAction.RangedAttack);

            if (DistanceTo(nearest) <= meleeRange)
                return (Vector3.zero, BotAction.MeleeAttack);

            return (toBoundary, BotAction.Move);
        }

        // ── DOMINANT MODE ──
        // High HP, few NPCs nearby — advance and fight
        if (hpRatio >= dominantHPThreshold && nearbyNPCs < 2)
        {
            Vector3 dirToNearest = (nearest.position - ownTransform.position).normalized;
            dirToNearest.y = 0;

            if (DistanceTo(nearest) <= meleeRange)
                return (Vector3.zero, BotAction.MeleeAttack);

            return (dirToNearest, BotAction.Move);
        }

        // ── HUNTING MODE ──
        // Target whoever is dealing the most damage
        Transform huntTarget = GetHighestDamageNPC(npcDamageDealt);
        if (huntTarget == null) huntTarget = nearest;

        float huntDist = DistanceTo(huntTarget);
        Vector3 dirToHunt = (huntTarget.position - ownTransform.position).normalized;
        dirToHunt.y = 0;

        if (huntDist <= meleeRange)
            return (Vector3.zero, BotAction.MeleeAttack);

        if (huntDist <= orbRange && huntDist > orbMinRange)
            return (dirToHunt, BotAction.RangedAttack);

        // don't keep walking into the NPC
        if (huntDist <= meleeRange + 0.5f)
            return (Vector3.zero, BotAction.Idle);

        return (dirToHunt, BotAction.Move);
    }

    // Called by BotController each time the bot takes damage from an NPC
    public void RecordNPCDamage(int npcIndex, float amount)
    {
        if (npcDamageDealt != null && npcIndex < npcDamageDealt.Length)
            npcDamageDealt[npcIndex] += amount;
    }

    public void ResetDamageTracking()
    {
        if (npcDamageDealt != null)
            for (int i = 0; i < npcDamageDealt.Length; i++)
                npcDamageDealt[i] = 0f;
    }
}