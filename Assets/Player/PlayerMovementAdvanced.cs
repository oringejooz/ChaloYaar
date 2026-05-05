using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  PlayerMovementAdvanced.cs
//  Handles: walking, sprinting, jumping, gravity, animator booleans.
//  Camera / sensitivity is handled entirely by MouseLook.cs on the camera.
//
//  Inspector wiring:
//    • Ground Check  → small empty child transform at the player's feet
//    • Ground Mask   → your "Ground" layer
//    • Animator      → Animator on the model child
//
//  Animator Parameters (all Bool):
//    • "IsWalking"  → grounded + moving, no sprint
//    • "IsRunning"  → grounded + moving + Left Shift
//    • "IsJumping"  → airborne (walk-jump and sprint-jump share same clip)
//
//  State table (mutually exclusive):
//    Idle     → IsWalking=F  IsRunning=F  IsJumping=F
//    Walk     → IsWalking=T  IsRunning=F  IsJumping=F
//    Sprint   → IsWalking=F  IsRunning=T  IsJumping=F
//    Jump     → IsWalking=F  IsRunning=F  IsJumping=T
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementAdvanced : MonoBehaviour
{
    // ── Movement ──────────────────────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("Normal walking speed (units/sec)")]
    public float walkSpeed = 4f;

    [Tooltip("Sprint speed (units/sec)")]
    public float sprintSpeed = 8f;

    [Tooltip("How quickly the player accelerates / decelerates")]
    public float speedSmoothTime = 0.1f;

    // ── Jump & Gravity ────────────────────────────────────────────────────────
    [Header("Jump & Gravity")]
    public float jumpHeight = 2f;
    public float gravity = -20f;

    [Tooltip("Extra downward force on the way down for a snappier arc")]
    public float fallMultiplier = 2.5f;

    // ── Ground Detection ──────────────────────────────────────────────────────
    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.35f;
    public LayerMask groundMask;

    // ── Animator ──────────────────────────────────────────────────────────────
    [Header("Animator")]
    public Animator animator;

    // ── Private State ─────────────────────────────────────────────────────────
    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;

    private float _currentSpeed;
    private float _speedSmoothVelocity;

    private bool _hasInput;
    private bool _isSprinting;

    // Animator hashes — avoids per-frame string allocation
    private static readonly int HashIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int HashIsJumping = Animator.StringToHash("IsJumping");

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _controller = GetComponent<CharacterController>();

        if (animator == null)
            Debug.LogWarning("[PlayerMovementAdvanced] No Animator assigned – animations will not play.");
    }

    void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        UpdateAnimator();
        Debug.Log(_isGrounded);
    }

    // ── Ground ────────────────────────────────────────────────────────────────
    void HandleGroundCheck()
    {
        _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    // ── Movement ──────────────────────────────────────────────────────────────
    void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = (transform.right * x + transform.forward * z).normalized;

        _hasInput = inputDir.magnitude > 0.05f;
        _isSprinting = _hasInput && Input.GetKey(KeyCode.LeftShift);

        float targetSpeed = _hasInput ? (_isSprinting ? sprintSpeed : walkSpeed) : 0f;

        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpeed,
            ref _speedSmoothVelocity, speedSmoothTime);

        _controller.Move(inputDir * _currentSpeed * Time.deltaTime);
    }

    // ── Jump ──────────────────────────────────────────────────────────────────
    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && _isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // ── Gravity ───────────────────────────────────────────────────────────────
    void ApplyGravity()
    {
        if (!_isGrounded)
        {
            float multiplier = (_velocity.y < 0f) ? fallMultiplier : 1f;
            _velocity.y += gravity * multiplier * Time.deltaTime;
        }

        _controller.Move(_velocity * Time.deltaTime);
    }

    // ── Animator Booleans ─────────────────────────────────────────────────────
    void UpdateAnimator()
    {
        if (animator == null) return;

        bool isJumping = !_isGrounded;
        bool isRunning = _isGrounded && _hasInput && Input.GetKey(KeyCode.LeftShift);
        bool isWalking = _isGrounded && _hasInput && !Input.GetKey(KeyCode.LeftShift);

        animator.SetBool("IsJumping", isJumping);
        animator.SetBool("IsRunning", isRunning);
        animator.SetBool("IsWalking", isWalking);
    }

    // ── Editor Gizmos ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}