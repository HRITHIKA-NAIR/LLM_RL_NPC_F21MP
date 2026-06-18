using UnityEngine;

// Bridges between the result of ML-Agents decisions and the Animator.
// ML-Agents moves the NPC via CharacterController in NPCAgent.OnActionReceived().
// This script reads the resulting velocity each FixedUpdate and:
//   1. Rotates the NPC to always face its direction of travel
//   2. Drives the Animator parameters so the correct animation plays
//   3. Switches between idle and combat idle based on proximity to bot
//   4. Listens to HPSystem and CombatHandler events for triggered animations
//
// Nothing in this script is hardcoded to a specific action or decision.
// The ML-Agents policy produces behaviour. This script makes it look correct.
public class NPCAnimator : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // REFERENCES — assign all in Inspector
    // ─────────────────────────────────────────────

    [Header("References")]
    public Animator animator;
    public CharacterController controller;
    public CombatHandler combatHandler;
    public HPSystem hpSystem;

    // ─────────────────────────────────────────────
    // SETTINGS
    // ─────────────────────────────────────────────

    [Header("Speed — adjust if walk/run/sprint blend feels wrong")]
    public float maxSpeed = 6f;

    [Header("Rotation — how fast the NPC turns to face movement direction")]
    // 200 degrees per second is a good starting value.
    // If NPCs appear to slide sideways before catching up visually, increase this.
    // If rotation looks snappy and robotic, decrease it.
    public float rotateSpeed = 200f;

    [Header("Animation smoothing — damping time for SetFloat transitions")]
    // Lower = snappier transitions. Higher = smoother but slightly delayed.
    public float animDamping = 0.1f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    private bool wasInCombatRange = false;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    void Awake()
    {
        // Subscribe to HPSystem death event.
        // When the NPC's HP reaches 0, HPSystem fires OnDeath.
        // We listen here and trigger the death animation.
        if (hpSystem != null)
            hpSystem.OnDeath.AddListener(OnDeath);
        else
            Debug.LogWarning($"[NPCAnimator] {gameObject.name}: hpSystem is null. " +
                             "Assign it in the Inspector.");
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks if GameObject is destroyed
        if (hpSystem != null)
            hpSystem.OnDeath.RemoveListener(OnDeath);
    }

    // ─────────────────────────────────────────────
    // FIXED UPDATE — runs every physics step
    // ─────────────────────────────────────────────

    void FixedUpdate()
    {
        // Do nothing if the NPC is dead — death animation is playing
        if (hpSystem != null && hpSystem.IsDead()) return;

        HandleRotationAndAnimation();
        HandleCombatRangeTransition();
        HandleGroundedState();
    }

    // ─────────────────────────────────────────────
    // ROTATION AND ANIMATION
    // ─────────────────────────────────────────────

    void HandleRotationAndAnimation()
    {
        // Get the NPC's current velocity from CharacterController.
        // This is the result of whatever movement NPCAgent.OnActionReceived() applied.
        Vector3 velocity = controller.velocity;

        // Flatten to horizontal plane — ignore any vertical component
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speed = flatVelocity.magnitude;

        // ── ROTATION ──
        // Rotate the NPC to face its direction of travel.
        // This is the core of "no backward animation" — the NPC always
        // turns to face where it is going, so the forward animation always plays.
        // This works for ALL movement directions:
        //   - Forward: no rotation needed, forward anim plays
        //   - Backward (retreat action): NPC turns 180°, forward anim plays
        //   - Left/Right strafe: NPC turns to face that direction, forward anim plays
        if (speed > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flatVelocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.fixedDeltaTime
            );
        }

        // ── ANIMATOR PARAMETERS ──
        // Because the character has rotated to face movement direction,
        // all velocity is now "forward" from the character's local perspective.
        // We pass speed as SpeedZ (forward) and 0 as SpeedX (no lateral component).
        // The blend tree drives walk/run/sprint based on SpeedMagnitude.
        //
        // We normalise speed against maxSpeed so the blend tree range is 0–1.
        // At speed 0 → Idle. At maxSpeed → Sprint.
        float normalisedSpeed = Mathf.Clamp01(speed / maxSpeed);

        animator.SetFloat("SpeedZ",
            normalisedSpeed, animDamping, Time.fixedDeltaTime);
        animator.SetFloat("SpeedX",
            0f, animDamping, Time.fixedDeltaTime);
        animator.SetFloat("SpeedMagnitude",
            normalisedSpeed, animDamping, Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────
    // COMBAT RANGE TRANSITION
    // ─────────────────────────────────────────────

    void HandleCombatRangeTransition()
    {
        // Switch between normal Idle and CombatIdle based on proximity to the bot.
        // combatHandler.IsInCombatEngagementRange() returns true when the NPC
        // is within the engagement radius (6 units by default in CombatHandler).
        // We only set the bool when the value changes to avoid calling SetBool every frame.
        if (combatHandler == null) return;

        bool inRange = combatHandler.IsInCombatEngagementRange();
        if (inRange != wasInCombatRange)
        {
            animator.SetBool("InCombatRange", inRange);
            wasInCombatRange = inRange;
        }
    }

    // ─────────────────────────────────────────────
    // GROUNDED STATE
    // ─────────────────────────────────────────────

    void HandleGroundedState()
    {
        // Drives the IsGrounded bool for jump state transitions.
        // CharacterController.isGrounded is reliable for detecting
        // whether the character is standing on ground.
        animator.SetBool("IsGrounded", controller.isGrounded);
    }

    // ─────────────────────────────────────────────
    // EVENT-DRIVEN TRIGGERED ANIMATIONS
    // These are called by external events, not by polling in FixedUpdate.
    // ─────────────────────────────────────────────

    // Called by CombatHandler when the NPC selects an attack action
    // and CanAttack() returns true.
    // useLeft = true plays the left hand attack, false plays the right.
    public void TriggerAttackAnimation(bool useLeft = false)
    {
        if (hpSystem != null && hpSystem.IsDead()) return;

        if (useLeft)
            animator.SetTrigger("AttackLeft");
        else
            animator.SetTrigger("Attack");
    }

    // Called by HPSystem.OnDeath event (subscribed in Awake).
    // Triggers the death animation. The character freezes on the
    // last frame of the death clip — this is correct behaviour.
    void OnDeath()
    {
        animator.SetTrigger("Die");
    }

    // Called by CombatHandler.TriggerDamage() when THIS NPC is hit
    // (i.e., when an attacker's damage event reaches this NPC's HPSystem).
    // Also called directly from OrbProjectile when the orb hits this NPC.
    public void TriggerHitReaction()
    {
        // Do not play hit reaction if already dead
        if (hpSystem != null && hpSystem.IsDead()) return;
        animator.SetTrigger("TakeDamage");
    }
}