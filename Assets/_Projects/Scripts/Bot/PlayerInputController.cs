using UnityEngine;
using UnityEngine.InputSystem;

// Controls the bot when Human mode is selected via BotTypeSelector.
// Uses Starter Assets Input System (new Input System) for WASD + mouse look.
// Character always rotates to face movement direction — no backward animation used.
// Q = melee attack. E = fire orb at nearest NPC.
// When this script is enabled, BotController is disabled (and vice versa).
public class PlayerInputController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // REFERENCES — assign all in Inspector
    // ─────────────────────────────────────────────

    [Header("Bot Components")]
    public CharacterController controller;
    public Animator animator;
    public OrbLauncher orbLauncher;
    public BotController botController;
    public BotCombatHandler botCombatHandler;

    [Header("NPC References — drag all 5")]
    public Transform[] npcTransforms;
    public HPSystem[] npcHPSystems;
    public NPCAnimator[] npcAnimators;

    [Header("Camera — for camera-relative movement")]
    public Camera playerCamera;

    [Header("Mobile UI — shown only when Human mode is active")]
    public GameObject mobileControlsCanvas;

    // ─────────────────────────────────────────────
    // SETTINGS
    // ─────────────────────────────────────────────

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotateSpeed = 720f;   // degrees per second

    [Header("Melee")]
    public float meleeCooldown = 0.8f;

    // ─────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────

    private float meleeCooldownTimer = 0f;

    // Input actions
    private InputAction moveAction;
    private InputAction meleeAction;
    private InputAction orbAction;

    // ─────────────────────────────────────────────
    // SETUP
    // ─────────────────────────────────────────────

    void Awake()
    {
        // 2D composite — full WASD, all 4 directions.
        // S key gives input.y = -1 (backward input value).
        // The character handles this by rotating to face that direction
        // and playing the forward animation. No backward clip is used.
        // The Mathf.Max(0f, v) clamp that was here previously has been removed.
        moveAction = new InputAction(
            "HumanMove",
            InputActionType.Value,
            binding: null,
            expectedControlType: "Vector2"
        );
        moveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/w")
            .With("Down",  "<Keyboard>/s")
            .With("Left",  "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        meleeAction = new InputAction("HumanMelee", InputActionType.Button);
        meleeAction.AddBinding("<Keyboard>/q");    // Q = melee attack

        orbAction = new InputAction("HumanOrb", InputActionType.Button);
        orbAction.AddBinding("<Keyboard>/e");      // E = fire orb
    }

    // ─────────────────────────────────────────────
    // ENABLE / DISABLE
    // These fire when BotTypeSelector toggles this script on or off.
    // ─────────────────────────────────────────────

    void OnEnable()
    {
        // Human takes control — disable the bot AI
        if (botController != null)
            botController.enabled = false;

        // Show mobile controls if present
        if (mobileControlsCanvas != null)
            mobileControlsCanvas.SetActive(true);

        moveAction.Enable();
        meleeAction.Enable();
        orbAction.Enable();
    }

    void OnDisable()
    {
        // Bot AI resumes when human mode is turned off
        if (botController != null)
            botController.enabled = true;

        // Hide mobile controls
        if (mobileControlsCanvas != null)
            mobileControlsCanvas.SetActive(false);

        moveAction.Disable();
        meleeAction.Disable();
        orbAction.Disable();
    }

    // ─────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────

    void Update()
    {
        if (meleeCooldownTimer > 0f)
            meleeCooldownTimer -= Time.deltaTime;

        HandleMovement();
        HandleCombat();
    }

    // ─────────────────────────────────────────────
    // MOVEMENT
    // ─────────────────────────────────────────────

    void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // input.x = A/D axis
        // input.y = W/S axis — S gives -1 (backward), W gives +1 (forward)
        // We do NOT clamp input.y. The player can move in all 4 directions.
        // Moving backward (S) rotates the character to face backward
        // and plays the forward animation — no backward clip needed.

        // Build camera-relative movement direction so WASD moves
        // relative to where the camera is pointing, not world axes.
        Vector3 camForward = Vector3.forward;
        Vector3 camRight   = Vector3.right;

        if (playerCamera != null)
        {
            // Project camera axes onto the horizontal plane
            camForward = Vector3.ProjectOnPlane(
                playerCamera.transform.forward, Vector3.up).normalized;
            camRight = Vector3.ProjectOnPlane(
                playerCamera.transform.right, Vector3.up).normalized;
        }

        // World-space movement direction combining camera forward and right
        Vector3 moveDir = (camForward * input.y) + (camRight * input.x);

        if (moveDir.magnitude > 0.1f)
        {
            // ── Rotate to face movement direction ──
            // This is the same rotation logic used in NPCAnimator and
            // AnimationTestController. The character always faces travel direction
            // so the forward animation always plays correctly.
            Quaternion targetRotation = Quaternion.LookRotation(moveDir.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            );

            // ── Move ──
            controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);

            // ── Drive Animator ──
            // Speed magnitude normalised — all movement registers as SpeedZ
            // because the character has rotated to face movement direction
            float normalisedSpeed = moveDir.magnitude;
            animator.SetFloat("SpeedZ",
                normalisedSpeed, 0.1f, Time.deltaTime);
            animator.SetFloat("SpeedX",
                0f, 0.1f, Time.deltaTime);
            animator.SetFloat("SpeedMagnitude",
                normalisedSpeed, 0.1f, Time.deltaTime);
        }
        else
        {
            // Standing still — fade animation speed parameters to zero
            animator.SetFloat("SpeedMagnitude", 0f, 0.15f, Time.deltaTime);
            animator.SetFloat("SpeedZ",         0f, 0.15f, Time.deltaTime);
            animator.SetFloat("SpeedX",         0f, 0.15f, Time.deltaTime);
        }

        // Always apply gravity regardless of input
        controller.Move(Vector3.down * 9.81f * Time.deltaTime);

        // ── Combat range indicator ──
        // Switches the bot between normal idle and combat idle stance
        // when a living NPC is within engagement range
        Transform nearest = GetNearestLivingNPC();
        bool inCombatRange = nearest != null &&
            Vector3.Distance(transform.position, nearest.position) <= 6f;
        animator.SetBool("InCombatRange", inCombatRange);
    }

    // ─────────────────────────────────────────────
    // COMBAT
    // ─────────────────────────────────────────────

    void HandleCombat()
    {
        // ── Q — Melee attack ──
        // Triggers the attack animation. Actual damage is applied by
        // BotCombatHandler.TriggerDamage() which is called by the
        // Animator Event at the hit frame of the attack clip.
        if (meleeAction.WasPressedThisFrame() && meleeCooldownTimer <= 0f)
        {
            animator.SetTrigger("Attack");
            meleeCooldownTimer = meleeCooldown;
            Debug.Log("[HumanMode] Melee attack triggered");
        }

        // ── E — Fire orb at nearest living NPC ──
        if (orbAction.WasPressedThisFrame())
        {
            Transform target = GetNearestLivingNPC();
            if (target != null && orbLauncher != null)
            {
                orbLauncher.FireOrb(target);
                Debug.Log($"[HumanMode] Orb fired at {target.name}");
            }
            else
            {
                Debug.Log("[HumanMode] Orb: no valid target in range");
            }
        }
    }

    // ─────────────────────────────────────────────
    // UTILITY
    // ─────────────────────────────────────────────

    Transform GetNearestLivingNPC()
    {
        Transform nearest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (npcHPSystems[i] == null) continue;
            if (npcHPSystems[i].IsDead()) continue;

            float d = Vector3.Distance(transform.position, npcTransforms[i].position);
            if (d < minDist)
            {
                minDist = d;
                nearest = npcTransforms[i];
            }
        }
        return nearest;
    }
}