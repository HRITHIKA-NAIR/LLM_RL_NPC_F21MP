using UnityEngine;
using UnityEngine.InputSystem;

// TESTING ONLY — AnimationTestScene use only
// Character always faces movement direction and plays forward animation
// No backward animation clips are ever used — only forward + turn
public class AnimationTestController : MonoBehaviour
{
    [Header("Drag the TestDummy_NPC Animator here")]
    public Animator characterAnimator;

    [Header("Drag the TestDummy_NPC CharacterController here")]
    public CharacterController characterController;

    [Header("Movement Speed")]
    public float moveSpeed = 4f;

    [Header("Rotation Speed (degrees per second)")]
    public float rotateSpeed = 720f;

    private InputAction moveAction;
    private InputAction attackRightAction;
    private InputAction attackLeftAction;
    private InputAction takeDamageAction;
    private InputAction dieAction;
    private InputAction jumpAction;
    private InputAction combatToggleAction;

    void Awake()
    {
        // 2D composite — WASD in all 4 directions
        // S key will produce input.y = -1 (backward input)
        // but we rotate the character to face that direction
        // and play the forward animation — no backward clip used
        moveAction = new InputAction("Move", InputActionType.Value,
                                     binding: null,
                                     expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/w")
            .With("Down",  "<Keyboard>/s")
            .With("Left",  "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        attackRightAction  = new InputAction("AttackRight",   InputActionType.Button);
        attackLeftAction   = new InputAction("AttackLeft",    InputActionType.Button);
        takeDamageAction   = new InputAction("TakeDamage",    InputActionType.Button);
        dieAction          = new InputAction("Die",           InputActionType.Button);
        jumpAction         = new InputAction("Jump",          InputActionType.Button);
        combatToggleAction = new InputAction("CombatToggle",  InputActionType.Button);

        attackRightAction.AddBinding("<Keyboard>/space");
        attackLeftAction.AddBinding("<Keyboard>/l");     // L key — left attack
        takeDamageAction.AddBinding("<Keyboard>/h");
        dieAction.AddBinding("<Keyboard>/k");
        jumpAction.AddBinding("<Keyboard>/j");
        combatToggleAction.AddBinding("<Keyboard>/c");
    }

    void OnEnable()
    {
        moveAction.Enable();
        attackRightAction.Enable();
        attackLeftAction.Enable();
        takeDamageAction.Enable();
        dieAction.Enable();
        jumpAction.Enable();
        combatToggleAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        attackRightAction.Disable();
        attackLeftAction.Disable();
        takeDamageAction.Disable();
        dieAction.Disable();
        jumpAction.Disable();
        combatToggleAction.Disable();
    }

    void Update()
    {
        HandleMovement();
        HandleAnimationTriggers();
    }

    void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // input.x = A/D (left/right strafe)
        // input.y = W/S (forward/backward)
        // S gives input.y = -1 — this is valid movement input
        // we do NOT clamp it to 0 anymore

        Vector3 moveDir = new Vector3(input.x, 0f, input.y);

        if (moveDir.magnitude > 0.01f)
        {
            // ── ROTATE character to face the direction it is moving ──
            // This is what makes backward movement look correct —
            // the character turns around and walks forward visually
            if (characterController != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                characterController.transform.rotation = Quaternion.RotateTowards(
                    characterController.transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            // ── MOVE in the direction of input ──
            if (characterController != null)
            {
                characterController.Move(
                    moveDir.normalized * moveSpeed * Time.deltaTime
                );
            }
        }

        // Always apply gravity
        if (characterController != null)
            characterController.Move(Vector3.down * 9.81f * Time.deltaTime);

        // ── ANIMATOR always gets forward-only speed ──
        // Because the character has rotated to face movement direction,
        // all movement is now "forward" from the character's perspective.
        // SpeedZ is always positive (forward), SpeedX stays for diagonal lean.
        // We use world-space magnitude as SpeedMagnitude to drive walk/run/sprint.
        float speedMag = moveDir.magnitude;

        if (characterAnimator != null)
        {
            // SpeedZ = full forward speed regardless of actual world direction
            // because the character has rotated to face travel direction
            characterAnimator.SetFloat("SpeedZ", speedMag, 0.1f, Time.deltaTime);
            // SpeedX for slight diagonal lean (optional — can be 0 for simplicity)
            characterAnimator.SetFloat("SpeedX", 0f, 0.1f, Time.deltaTime);
            characterAnimator.SetFloat("SpeedMagnitude", speedMag, 0.1f, Time.deltaTime);
        }
    }

    void HandleAnimationTriggers()
    {
        if (characterAnimator == null) return;

        // C — toggle combat range on/off
        if (combatToggleAction.WasPressedThisFrame())
        {
            bool current = characterAnimator.GetBool("InCombatRange");
            characterAnimator.SetBool("InCombatRange", !current);
            Debug.Log($"[AnimTest] InCombatRange → {!current}");
        }

        // Space — right hand attack
        if (attackRightAction.WasPressedThisFrame())
        {
            characterAnimator.SetTrigger("Attack");
            Debug.Log("[AnimTest] Attack (right) triggered");
        }

        // L — left hand attack
        // If this still does not fire, see debug note below
        if (attackLeftAction.WasPressedThisFrame())
        {
            characterAnimator.SetTrigger("AttackLeft");
            Debug.Log("[AnimTest] AttackLeft (left) triggered");
        }

        // H — take damage hit reaction
        if (takeDamageAction.WasPressedThisFrame())
        {
            characterAnimator.SetTrigger("TakeDamage");
            Debug.Log("[AnimTest] TakeDamage triggered");
        }

        // K — death
        if (dieAction.WasPressedThisFrame())
        {
            characterAnimator.SetTrigger("Die");
            Debug.Log("[AnimTest] Die triggered");
        }

        // J — jump sequence
        if (jumpAction.WasPressedThisFrame())
        {
            characterAnimator.SetTrigger("Jump");
            characterAnimator.SetBool("IsGrounded", false);
            Debug.Log("[AnimTest] Jump triggered");
            Invoke(nameof(SimulateLanding), 0.9f);
        }
    }

    void SimulateLanding()
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetBool("IsGrounded", true);
            Debug.Log("[AnimTest] Landed — IsGrounded = true");
        }
    }
}