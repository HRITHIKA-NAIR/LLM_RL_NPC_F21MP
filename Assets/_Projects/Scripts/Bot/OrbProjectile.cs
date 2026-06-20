using UnityEngine;

// Attached to the OrbProjectile prefab.
// Travels in a straight line, damages the first NPC it hits.
public class OrbProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 17f;
    public float damage = 10f;
    public float lifetime = 4f;   // destroy after this many seconds if nothing hit

    private Vector3 travelDirection;
    private bool hasHit = false;

    public void Launch(Vector3 direction)
    {
        travelDirection = direction.normalized;
        travelDirection.y = 0;   // keep it horizontal — no arc
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (hasHit) return;
        // Move in straight line using Transform (not Rigidbody velocity)
        // because we want consistent speed regardless of physics step
        transform.position += travelDirection * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Only damage NPCs
        if (!other.CompareTag("NPC")) return;

        HPSystem targetHP = other.GetComponent<HPSystem>();
        if (targetHP == null) return;
        if (targetHP.IsDead()) return;

        hasHit = true;
        targetHP.TakeDamage(damage);

        // Trigger hit reaction animation on the NPC that was hit
        NPCAnimator npcAnim = other.GetComponent<NPCAnimator>();
        if (npcAnim != null)
            npcAnim.TriggerHitReaction();

        // Destroy orb immediately on hit
        Destroy(gameObject);
    }
}