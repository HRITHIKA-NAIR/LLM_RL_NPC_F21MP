using UnityEngine;

// Attached to the OpponentBot.
// Spawns orb projectiles aimed at a target NPC.
public class OrbLauncher : MonoBehaviour
{
    [Header("References")]
    public GameObject orbPrefab;
    public Transform spawnPoint;   // empty child GameObject at hand position

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

        // Direction from spawn point toward target's centre (chest height)
        Vector3 targetPos = target.position + Vector3.up * 1.0f;
        Vector3 direction = (targetPos - spawnPoint.position).normalized;

        GameObject orb = Instantiate(orbPrefab, spawnPoint.position, Quaternion.identity);
        OrbProjectile orbScript = orb.GetComponent<OrbProjectile>();
        if (orbScript != null)
            orbScript.Launch(direction);
    }

    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }
}