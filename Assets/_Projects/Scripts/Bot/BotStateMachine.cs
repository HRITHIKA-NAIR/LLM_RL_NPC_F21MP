using UnityEngine;

// Possible actions the bot can take each Update tick
public enum BotAction
{
    Idle,
    Move,
    MeleeAttack,
    RangedAttack
}

// Base class — each bot type inherits this and overrides DecideAction()
// BotController reads DecideAction() every Update and acts on the result
public abstract class BotStateMachine : MonoBehaviour
{
    // ── Shared references (set by BotController on Start) ──
    [HideInInspector] public Transform[] npcTransforms;
    [HideInInspector] public HPSystem[] npcHPSystems;
    [HideInInspector] public HPSystem ownHP;
    [HideInInspector] public Transform ownTransform;

    // ── Ranges ──
    [Header("Combat Ranges")]
    public float meleeRange = 3.0f;
    public float orbRange = 12.0f;
    public float orbMinRange = 6.0f;   // closer than this use melee instead

    // Each bot type implements this differently
    // Returns: move direction (can be Vector3.zero if not moving)
    //          and what action to perform this tick
    public abstract (Vector3 moveDir, BotAction action) DecideAction();

    // ── SHARED HELPER METHODS (all 3 bot types use these) ──

    // Returns the nearest LIVING NPC transform
    protected Transform GetNearestLivingNPC()
    {
        Transform nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i].IsDead()) continue;
            float d = Vector3.Distance(ownTransform.position, npcTransforms[i].position);
            if (d < minDist)
            {
                minDist = d;
                nearest = npcTransforms[i];
            }
        }
        return nearest;
    }

    // Returns the living NPC with the LOWEST current HP
    protected Transform GetLowestHPLivingNPC()
    {
        Transform target = null;
        float lowestHP = float.MaxValue;

        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i].IsDead()) continue;
            if (npcHPSystems[i].currentHP < lowestHP)
            {
                lowestHP = npcHPSystems[i].currentHP;
                target = npcTransforms[i];
            }
        }
        return target;
    }

    // Returns the living NPC that has dealt the most cumulative damage to the bot
    // BalancedBot uses this. Damage tracking is stored in BotController.
    protected Transform GetHighestDamageNPC(float[] damageDealtByNPC)
    {
        Transform target = null;
        float maxDmg = -1f;

        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i].IsDead()) continue;
            if (damageDealtByNPC[i] > maxDmg)
            {
                maxDmg = damageDealtByNPC[i];
                target = npcTransforms[i];
            }
        }
        // If no NPC has dealt damage yet, fall back to nearest
        return target ?? GetNearestLivingNPC();
    }

    // Returns how many living NPCs are within a given radius
    protected int CountLivingNPCsWithinRadius(float radius)
    {
        int count = 0;
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i].IsDead()) continue;
            if (Vector3.Distance(ownTransform.position, npcTransforms[i].position) <= radius)
                count++;
        }
        return count;
    }

    // Returns how many NPCs are alive total
    protected int CountLivingNPCs()
    {
        int count = 0;
        foreach (var hp in npcHPSystems)
            if (!hp.IsDead()) count++;
        return count;
    }

    // Distance to a given transform
    protected float DistanceTo(Transform t)
    {
        if (t == null) return float.MaxValue;
        return Vector3.Distance(ownTransform.position, t.position);
    }
}