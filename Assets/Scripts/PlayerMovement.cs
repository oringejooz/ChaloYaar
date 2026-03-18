using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Attach to your Player prefab.
/// Requires: CharacterController, a groundCheck Transform child, and a Camera child.
/// The Camera child should be disabled by default in the prefab — this script
/// enables it only for the owning client so two cameras never fight each other.
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float gravity = -9.8f;
    public float jumpHeight = 2f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    [Header("References")]
    [Tooltip("Drag the Camera child of your Player prefab here.")]
    public Camera playerCamera;

    // ── internals ──────────────────────────────────────────────────────────
    CharacterController controller;
    Vector3 velocity;
    bool isGrounded;

    // ── NGO callbacks ───────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();

        if (IsOwner)
        {
            // Activate THIS client's camera only
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Make absolutely sure the non-owner camera stays off
            if (playerCamera != null)
                playerCamera.gameObject.SetActive(false);
        }
    }

    // ── per-frame ───────────────────────────────────────────────────────────

    void Update()
    {
        // Only the owning client drives their own character
        if (!IsOwner) return;

        HandleGrounding();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    void HandleGrounding()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // Snap down so we don't accumulate falling velocity while walking on slopes
        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;
    }

    void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        // Clamp diagonal speed to avoid faster diagonal movement
        if (move.magnitude > 1f)
            move.Normalize();

        controller.Move(move * speed * Time.deltaTime);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}