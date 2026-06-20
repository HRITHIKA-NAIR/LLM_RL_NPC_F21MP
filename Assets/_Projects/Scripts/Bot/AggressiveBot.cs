using UnityEngine;

// Aggressive Bot playstyle:
// Always charges the nearest NPC at full speed.
// No health-based retreat. No target prioritisation beyond proximity.
// Fires orb if melee is on cooldown and NPC is in orb range.
// Represents a berserker / beginner human player.
public class AggressiveBot : BotStateMachine
{
    public override (Vector3 moveDir, BotAction action) DecideAction()
    {
        Transform target = GetNearestLivingNPC();

        if (target == null)
            return (Vector3.zero, BotAction.Idle);

        float dist = DistanceTo(target);
        Vector3 dirToTarget = (target.position - ownTransform.position).normalized;
        dirToTarget.y = 0;

        // Within melee range — attack
        if (dist <= meleeRange)
            return (Vector3.zero, BotAction.MeleeAttack);

        // Within orb range but outside melee — use orb while closing in
        if (dist <= orbRange && dist > orbMinRange)
            return (dirToTarget, BotAction.RangedAttack);

        // Otherwise charge forward
        return (dirToTarget, BotAction.Move);
    }
}