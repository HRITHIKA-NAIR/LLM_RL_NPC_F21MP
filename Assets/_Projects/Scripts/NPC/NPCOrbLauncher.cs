using UnityEngine;

// Attached to NPC agents.
// Spawns NPC orb projectiles aimed at the bot.
public class NPCOrbLauncher : MonoBehaviour
{
    [Header("References")]
    public GameObject orbPrefab;
    public Transform spawnPoint;

    [Header("Settings")]
    public float orbCooldown = 1.0f;

    private float cooldownTimer = 0f;

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public bool CanFireOrb()
    {
        return cooldownTimer <= 0f;
    }

    public void FireOrb(Transform target)
    {
        if (!CanFireOrb()) return;
        if (orbPrefab == null || spawnPoint == null) return;
        if (target == null) return;

        cooldownTimer = orbCooldown;

        Vector3 targetPos = target.position + Vector3.up * 1.0f;
        Vector3 direction = (targetPos - spawnPoint.position).normalized;

        GameObject orb = Instantiate(orbPrefab, spawnPoint.position, Quaternion.identity);

        NPCOrbProjectile orbScript = orb.GetComponent<NPCOrbProjectile>();

        if (orbScript != null)
            orbScript.Launch(direction);
    }

    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }
}