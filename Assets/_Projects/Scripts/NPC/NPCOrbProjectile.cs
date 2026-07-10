using UnityEngine;

// Attached to the OrbProjectile_NPC prefab.
// Fired by NPC agents at the opponent bot.
// Identical to OrbProjectile but damages the Bot tag instead of NPC tag.
public class NPCOrbProjectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 12f;       // slightly slower than bot orb (15f)
    public float damage = 5f;       // slightly less than bot orb (10f)
    public float lifetime = 4f;

    private Vector3 travelDirection;
    private bool launched = false;
    private bool hasHit = false;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Launch(Vector3 direction)
    {

        travelDirection = new Vector3(
            direction.x, 0f, direction.z).normalized;
        launched = true;

        if (travelDirection != Vector3.zero)
            transform.rotation =
                Quaternion.LookRotation(travelDirection);
    }

    void Update()
    {
        if (!launched) return;
        if (hasHit) return;

        transform.position +=
            travelDirection * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // NPC orbs only damage the bot
        if (!other.CompareTag("Bot")) return;

        HPSystem targetHP = other.GetComponent<HPSystem>();
        if (targetHP == null) return;
        if (targetHP.IsDead()) return;

        hasHit = true;
        targetHP.TakeDamage(damage);

        Destroy(gameObject);
    }
}