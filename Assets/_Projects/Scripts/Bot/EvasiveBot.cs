using UnityEngine;

// Evasive Bot playstyle:
// Always moves AWAY from the nearest NPC.
// Attacks only if an NPC gets within melee range despite evasion —
//   targets the LOWEST HP NPC in range (finishes wounded targets).
// Fires orb at any living NPC within orb range while retreating.
// Represents an experienced kiting human player.
public class EvasiveBot : BotStateMachine
{
    public override (Vector3 moveDir, BotAction action) DecideAction()
    {
        Transform nearest = GetNearestLivingNPC();

        if (nearest == null)
            return (Vector3.zero, BotAction.Idle);

        float distToNearest = DistanceTo(nearest);

        // If an NPC is within melee range despite evasion — melee attack lowest HP
        if (distToNearest <= meleeRange)
        {
            Transform lowestHP = GetLowestHPLivingNPC();
            if (lowestHP != null && DistanceTo(lowestHP) <= meleeRange)
                return (Vector3.zero, BotAction.MeleeAttack);
        }

        // Flee direction = away from nearest NPC
        Vector3 fleeDir = (ownTransform.position - nearest.position);
        fleeDir.y = 0;

        if (fleeDir.magnitude < 0.1f)
            return (Vector3.zero, BotAction.Idle);

        fleeDir.Normalize();

        // Check if any NPC is in orb range — fire while retreating
        Transform orbTarget = GetLowestHPLivingNPC();
        if (orbTarget != null)
        {
            float orbDist = DistanceTo(orbTarget);
            if (orbDist <= orbRange && orbDist > meleeRange)
                return (fleeDir, BotAction.RangedAttack);
        }

        // Just flee
        return (fleeDir, BotAction.Move);
    }
}