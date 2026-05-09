using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  PlayerMovementAdvanced.cs
//  Handles: walking, sprinting, jumping, gravity, animator booleans,
//           footstep audio (land / water), water-zone entry via OnTrigger.
//
//  Inspector wiring:
//    • Ground Check    → small empty child transform at the player's feet
//    • Ground Mask     → your "Ground" layer
//    • Animator        → Animator on the model child
//    • Audio Source    → AudioSource on this GameObject (or child)
//    • Walk Clips      → one or more land footstep clips
//    • Run Clips       → one or more land run clips
//    • Water Walk Clips→ one or more water footstep clips
//
//  Water setup:
//    • Add a trigger collider to your water plane (Is Trigger = true)
//    • Tag the water plane GameObject with the tag  "Water"
//      (or change waterTag below to match your own tag)
//
//  Animator Parameters (all Bool):
//    • "IsWalking"  → grounded + moving, no sprint
//    • "IsRunning"  → grounded + moving + Left Shift (land only)
//    • "IsJumping"  → airborne
//
//  State table (mutually exclusive):
//    Idle     → IsWalking=F  IsRunning=F  IsJumping=F
//    Walk     → IsWalking=T  IsRunning=F  IsJumping=F
//    Sprint   → IsWalking=F  IsRunning=T  IsJumping=F  (disabled in water)
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

    // ── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio - Master")]
    [Tooltip("AudioSource used for all footstep sounds")]
    public AudioSource audioSource;

    [Tooltip("Master volume multiplied on top of each SFX volume. Can exceed 1.")]
    [Range(0f, 3f)]
    public float masterVolume = 1f;

    [Header("Audio - Walk SFX")]
    public AudioClip walkClip;

    [Tooltip("Volume for walk sound. Can exceed 1 for boost.")]
    [Range(0f, 3f)]
    public float walkVolume = 1f;

    [Tooltip("Pitch / tempo of walk sound.")]
    [Range(0.1f, 3f)]
    public float walkPitch = 1f;

    [Header("Audio - Run SFX")]
    public AudioClip runClip;

    [Tooltip("Volume for run sound. Can exceed 1 for boost.")]
    [Range(0f, 3f)]
    public float runVolume = 1f;

    [Tooltip("Pitch / tempo of run sound.")]
    [Range(0.1f, 3f)]
    public float runPitch = 1f;

    [Header("Audio - Water Walk SFX")]
    public AudioClip waterWalkClip;

    [Tooltip("Volume for water walk sound. Can exceed 1 for boost.")]
    [Range(0f, 3f)]
    public float waterWalkVolume = 1f;

    [Tooltip("Pitch / tempo of water walk sound.")]
    [Range(0.1f, 3f)]
    public float waterWalkPitch = 1f;

    // ── Water ─────────────────────────────────────────────────────────────────
    [Header("Water")]
    [Tooltip("Tag assigned to your water plane trigger collider")]
    public string waterTag = "Water";

    [Tooltip("Walk speed while in water (units/sec)")]
    public float waterWalkSpeed = 2f;

    [Tooltip("Animator speed multiplier while in water (1 = normal, 0.5 = half speed)")]
    [Range(0.1f, 1f)]
    public float waterAnimatorSpeed = 0.6f;

    // ── Private State ─────────────────────────────────────────────────────────
    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;

    private float _currentSpeed;
    private float _speedSmoothVelocity;

    private bool _hasInput;
    private bool _isSprinting;

    public bool IsSprinting => _isSprinting;

    // Water state
    private bool _isInWater;

    // Tracks whether the player intentionally jumped (used in water to avoid
    // false IsJumping when the ground check loses contact with the water floor)
    private bool _jumpedIntentionally;

    // Tracks which state is currently playing so we can detect switches
    private enum FootstepState { None, Walk, Run, WaterWalk }
    private FootstepState _currentFootstepState = FootstepState.None;

    private PlayerStats _stats;

    // Animator hashes — avoids per-frame string allocation
    private static readonly int HashIsWalking = Animator.StringToHash("IsWalking");
    private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");
    private static readonly int HashIsJumping = Animator.StringToHash("IsJumping");

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();

        if (animator == null)
            Debug.LogWarning("[PlayerMovementAdvanced] No Animator assigned – animations will not play.");

        if (audioSource == null)
            Debug.LogWarning("[PlayerMovementAdvanced] No AudioSource assigned – footstep sounds will not play.");
    }

    void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        ApplyGravity();
        UpdateAnimator();
        HandleFootstepAudio();
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

        Transform cam = Camera.main.transform;

        Vector3 forward = cam.forward;
        Vector3 right = cam.right;
        
        // flatten so looking up/down doesn't move vertically
        forward.y = 0f;
        right.y = 0f;
        
        forward.Normalize();
        right.Normalize();
        
        Vector3 inputDir = (right * x + forward * z).normalized;

        _hasInput = inputDir.magnitude > 0.05f;
        _isSprinting = _hasInput && !_isInWater && Input.GetKey(KeyCode.LeftShift);

        // ← ADD THIS: sync sprint state to PlayerStats so stamina actually drains
        var stats = GetComponent<PlayerStats>();
        if (stats != null) stats.IsSprinting = _isSprinting;

        float targetSpeed = _hasInput
            ? (_isSprinting ? sprintSpeed : (_isInWater ? waterWalkSpeed : walkSpeed))
            : 0f;

        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpeed,
            ref _speedSmoothVelocity, speedSmoothTime);

        _controller.Move(inputDir * _currentSpeed * Time.deltaTime);
    }

    // ── Jump ──────────────────────────────────────────────────────────────────
    void HandleJump()
    {
        // Clear the flag as soon as the player lands
        if (_isGrounded)
            _jumpedIntentionally = false;

        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpedIntentionally = true;
        }
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

        // In water, only flag as jumping if the player explicitly pressed jump.
        // This prevents the animator firing IsJumping just because the ground
        // check loses contact with the water floor.
        bool isJumping = _isInWater ? _jumpedIntentionally : !_isGrounded;
        bool isRunning = _isGrounded && _hasInput && _isSprinting;
        bool isWalking = _isGrounded && _hasInput && !_isSprinting;

        animator.speed = _isInWater ? waterAnimatorSpeed : 1f;

        animator.SetBool(HashIsJumping, isJumping);
        animator.SetBool(HashIsRunning, isRunning);
        animator.SetBool(HashIsWalking, isWalking);
    }

    // ── Footstep Audio ────────────────────────────────────────────────────────
    void HandleFootstepAudio()
    {
        if (audioSource == null) return;

        bool shouldPlay = _isGrounded && _hasInput;

        if (!shouldPlay)
        {
            SetFootstepState(FootstepState.None);
            return;
        }

        FootstepState targetState = _isInWater ? FootstepState.WaterWalk
                                  : _isSprinting ? FootstepState.Run
                                  : FootstepState.Walk;

        SetFootstepState(targetState);
    }

    void SetFootstepState(FootstepState state)
    {
        // Always update volume/pitch live so inspector tweaks take effect immediately
        if (state != FootstepState.None)
            ApplyAudioSettings(state);

        // Only restart the AudioSource when the state actually changes
        if (state == _currentFootstepState) return;
        _currentFootstepState = state;

        if (state == FootstepState.None)
        {
            audioSource.Stop();
            audioSource.clip = null;
            return;
        }

        AudioClip clip = state == FootstepState.WaterWalk ? waterWalkClip
                       : state == FootstepState.Run ? runClip
                       : walkClip;

        if (clip == null) { audioSource.Stop(); return; }

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    void ApplyAudioSettings(FootstepState state)
    {
        switch (state)
        {
            case FootstepState.Walk:
                audioSource.volume = walkVolume * masterVolume;
                audioSource.pitch = walkPitch;
                break;
            case FootstepState.Run:
                audioSource.volume = runVolume * masterVolume;
                audioSource.pitch = runPitch;
                break;
            case FootstepState.WaterWalk:
                audioSource.volume = waterWalkVolume * masterVolume;
                audioSource.pitch = waterWalkPitch;
                break;
        }
    }

    // ── Water Trigger Detection ───────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(waterTag))
        {
            _isInWater = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(waterTag))
        {
            _isInWater = false;
        }
    }

    // ── Editor Gizmos ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}