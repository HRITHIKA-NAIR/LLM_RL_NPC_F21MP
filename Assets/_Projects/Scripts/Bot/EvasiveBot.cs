using UnityEngine;

// Evasive Bot playstyle:
// Always moves AWAY from the nearest NPC.
// Fires orb while retreating.
// Avoids arena walls by circling back toward the center.
// Uses melee only when enemies catch it.
// Represents an experienced kiting human player.
public class EvasiveBot : BotStateMachine
{
    [Header("Arena")]
    public float wallDistance = 12f;   // for a 30x30 arena

    public override (Vector3 moveDir, BotAction action) DecideAction()
    {
        Transform nearest = GetNearestLivingNPC();

        if (nearest == null)
            return (Vector3.zero, BotAction.Idle);

        float distToNearest = DistanceTo(nearest);

        // If an enemy gets close enough, fight back
        if (distToNearest <= meleeRange)
        {
            Transform lowestHP = GetLowestHPLivingNPC();

            if (lowestHP != null && DistanceTo(lowestHP) <= meleeRange)
                return (Vector3.zero, BotAction.MeleeAttack);
        }

        //----------------------------------------
        // Normal flee direction
        //----------------------------------------
        Vector3 fleeDir =
            (ownTransform.position - nearest.position);

        fleeDir.y = 0;

        if (fleeDir.magnitude < 0.1f)
            fleeDir = Vector3.forward;

        fleeDir.Normalize();

        //----------------------------------------
        // Wall avoidance
        //----------------------------------------
        float distanceFromCenter =
            new Vector2(
                ownTransform.position.x,
                ownTransform.position.z
            ).magnitude;

        if (distanceFromCenter > wallDistance)
        {
            // Direction toward arena center
            Vector3 toCenter = -ownTransform.position;
            toCenter.y = 0;
            toCenter.Normalize();

            // Sideways direction to circle around
            Vector3 side =
                Vector3.Cross(Vector3.up, toCenter).normalized;

            // Blend center + side movement
            fleeDir = (toCenter + side).normalized;
        }

        //----------------------------------------
        // Fire orb while retreating
        //----------------------------------------
        Transform orbTarget = GetLowestHPLivingNPC();

        if (orbTarget != null)
        {
            float orbDist = DistanceTo(orbTarget);

            if (orbDist <= orbRange)
                return (fleeDir, BotAction.RangedAttack);
        }

        //----------------------------------------
        // Otherwise just retreat
        //----------------------------------------
        return (fleeDir, BotAction.Move);
    }
}