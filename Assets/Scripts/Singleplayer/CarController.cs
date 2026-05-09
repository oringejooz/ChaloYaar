using System.Collections;
using UnityEngine;

/// <summary>
/// CarController.cs – v7
/// Fixes:
///   - Immediate exit bug (E key cooldown)
///   - Player floating/van flying (CharacterController + Movement disabled on enter)
///   - Exit position jitter (slight upward offset)
///   - Q toggles inside/outside camera while driving
///   - InteractionSystem disabled while driving
/// </summary>
public class CarController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR REFERENCES
    // ─────────────────────────────────────────────

    [Header("References")]
    public GameObject player;
    public PlayerController playerController;
    public Transform steeringWheel;
    public Collider doorCollider;

    [Tooltip("Empty GameObject placed next to the van door — repositioned to player entry point at runtime.")]
    public Transform exitPoint;

    [Header("Cameras")]
    public Camera insideCamera;
    public Camera outsideCamera;

    [Header("Wheel Meshes  [FL, FR, RL, RR]  (visual only)")]
    public Transform[] wheelMeshes = new Transform[4];

    [Header("Audio Sources")]
    public AudioSource engineAudioSource;
    public AudioSource sfxAudioSource;

    [Header("Audio Clips")]
    public AudioClip startClip;
    public AudioClip idleClip;
    public AudioClip runningClip;
    public AudioClip reverseClip;
    public AudioClip collisionClip;

    // ─────────────────────────────────────────────
    //  DRIVING TUNING
    // ─────────────────────────────────────────────

    [Header("Driving Tuning")]
    public float driveForce = 180f;
    public float turnForce = 500f;   // doesn't matter much if using MoveRotation steering
    public float turnSpeed = 28f;
    public float brakeDrag = 6f;
    public float driveDrag = 2f;
    public float angDrag = 7f;
    public float maxSpeed = 4.5f;
    public float enterDistance = 4f;
    public float steeringWheelMaxAngle = 180f;

    [Header("Audio Tuning")]
    public float idlePitch = 0.6f;
    public float maxPitch = 1.6f;
    public float pitchLerpSpeed = 3f;
    public float idleToRunDelay = 0.3f;

    [Header("Van Damage")]
    public VanSystemsHub vanHub;

    [Tooltip("Minimum collision impact speed to register damage (units/sec)")]
    public float minImpactSpeed = 3f;

    [Tooltip("Damage dealt per unit of impact speed above the threshold")]
    public float damagePerSpeedUnit = 0.5f;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private Rigidbody rb;
    public bool IsInCar => isInCar;
    private bool isInCar = false;
    private bool engineStarted = false;
    private float _exitCooldown = 0f;

    private enum EngineState { Off, Starting, Idle, Running, Reversing }
    private EngineState engineState = EngineState.Off;
    private float idleTimer = 0f;

    private float throttleInput;
    private float steerInput;
    private bool isBraking;

    private float wheelRollAngle = 0f;
    private Quaternion defaultSteeringRotation;

    private float debugTimer = 0f;
    private const float DEBUG_INTERVAL = 0.5f;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Start()
    {
        if (player == null) Debug.LogError("[CarController] ❌ 'player' not assigned!");
        if (playerController == null) Debug.LogWarning("[CarController] ⚠️ 'playerController' not assigned — InteractionSystem won't be toggled.");
        if (exitPoint == null) Debug.LogWarning("[CarController] ⚠️ 'exitPoint' not assigned.");

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[CarController] ❌ No Rigidbody on this GameObject!");
            return;
        }

        rb.centerOfMass = new Vector3(0f, -0.2f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.None;
        rb.angularDrag = angDrag;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (insideCamera) insideCamera.gameObject.SetActive(false);
        if (outsideCamera) outsideCamera.gameObject.SetActive(false);
        if (steeringWheel) defaultSteeringRotation = steeringWheel.localRotation;

        if (engineAudioSource)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            engineAudioSource.Stop();
        }
    }

    void Update()
    {
        HandleEnterExit();

        if (isInCar && engineStarted)
        {
            GatherInput();
            UpdateEngineSound();
        }
        else
        {
            throttleInput = 0f;
            steerInput = 0f;
            isBraking = false;
        }
    }

    void FixedUpdate()
    {
        if (isInCar && engineStarted)
        {
            DriveCar();

            debugTimer += Time.fixedDeltaTime;
            if (debugTimer >= DEBUG_INTERVAL)
            {
                debugTimer = 0f;
                float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
                Debug.Log("[CarController] 🚗 throttle=" + throttleInput.ToString("F2")
                    + " | speed=" + forwardSpeed.ToString("F2")
                    + " | velocity=" + rb.velocity.magnitude.ToString("F2")
                    + " | pos=" + transform.position
                    + " | isKinematic=" + rb.isKinematic
                    + " | drag=" + rb.drag.ToString("F2"));
            }
        }

        UpdateWheelVisuals();
    }

    // ─────────────────────────────────────────────
    //  ENTER / EXIT
    // ─────────────────────────────────────────────

    void HandleEnterExit()
    {
        _exitCooldown -= Time.deltaTime;

        // E exits — cooldown prevents same-frame exit right after entering
        if (isInCar && _exitCooldown <= 0f && Input.GetKeyDown(KeyCode.E))
            ExitCar();

        // Q toggles inside/outside camera while driving
        if (isInCar && Input.GetKeyDown(KeyCode.Q))
            ToggleCamera();
    }

    void ToggleCamera()
    {
        if (insideCamera == null || outsideCamera == null) return;
        bool insideActive = insideCamera.gameObject.activeSelf;
        insideCamera.gameObject.SetActive(!insideActive);
        outsideCamera.gameObject.SetActive(insideActive);
    }

    /// <summary>Called by DriverSeatInteractable when the player presses E on the seat.</summary>
    public void EnterCar()
    {
        // Cooldown prevents HandleEnterExit firing ExitCar on the same frame
        _exitCooldown = 0.3f;
        isInCar = true;

        // Snapshot entry position — ExitCar returns player exactly here
        if (exitPoint != null && player != null)
        {
            exitPoint.position = player.transform.position + Vector3.up * 0.1f;
            exitPoint.rotation = player.transform.rotation;
        }

        // Disable CharacterController + movement so they don't fight the van Rigidbody
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var movement = player.GetComponent<PlayerMovementAdvanced>();
        if (movement != null) movement.enabled = false;

        // Disable InteractionSystem so E is exclusively used for exiting
        if (playerController != null && playerController.interactionSystem != null)
            playerController.interactionSystem.enabled = false;

        // Clear the interaction prompt
        FindObjectOfType<HUDManager>()?.HidePrompt(); // ← add this

        player.SetActive(false);

        if (insideCamera != null) insideCamera.gameObject.SetActive(true);
        if (outsideCamera != null) outsideCamera.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        StartCoroutine(StartEngine());
    }

    void ExitCar()
    {
        isInCar = false;
        engineStarted = false;
        engineState = EngineState.Off;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.drag = 10f;

        if (engineAudioSource) engineAudioSource.Stop();

        if (player != null)
        {
            // Return player to where they entered
            Transform spawnAt = exitPoint != null ? exitPoint : transform;
            player.transform.position = spawnAt.position;
            player.transform.rotation = Quaternion.Euler(0f, spawnAt.rotation.eulerAngles.y, 0f);

            player.SetActive(true);

            // Re-enable CharacterController and movement after repositioning
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            var movement = player.GetComponent<PlayerMovementAdvanced>();
            if (movement != null) movement.enabled = true;
        }

        // Re-enable InteractionSystem now that E is free again
        if (playerController != null && playerController.interactionSystem != null)
            playerController.interactionSystem.enabled = true;

        if (insideCamera) insideCamera.gameObject.SetActive(false);
        if (outsideCamera) outsideCamera.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[CarController] 🚪 ExitCar() — player returned to entry position.");
    }

    // ─────────────────────────────────────────────
    //  ENGINE START
    // ─────────────────────────────────────────────

    IEnumerator StartEngine()
    {
        engineState = EngineState.Starting;
        Debug.Log("[CarController] 🔑 Engine starting...");

        if (sfxAudioSource && startClip)
        {
            sfxAudioSource.PlayOneShot(startClip);
            yield return new WaitForSeconds(startClip.length);
        }
        else
        {
            yield return new WaitForSeconds(1.2f);
        }

        engineStarted = true;
        engineState = EngineState.Idle;
        rb.drag = driveDrag;
        PlayEngineClip(idleClip, idlePitch);

        Debug.Log("[CarController] ✅ Engine ready — W/S drive, A/D steer, Space brake, E exit, Q camera.");
    }

    // ─────────────────────────────────────────────
    //  INPUT
    // ─────────────────────────────────────────────

    void GatherInput()
    {
        throttleInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);
    }

    // ─────────────────────────────────────────────
    //  CAR PHYSICS
    // ─────────────────────────────────────────────

    void DriveCar()
    {
        rb.drag = isBraking ? brakeDrag : driveDrag;

        float speed = Vector3.Dot(rb.velocity, transform.forward);
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

        // kill sideways sliding
        localVelocity.x *= 0.2f;

        // rebuild world velocity
        rb.velocity = transform.TransformDirection(localVelocity);

        Debug.Log($"[Drive] force={(transform.forward * throttleInput * driveForce)} | vel={rb.velocity.magnitude}");

        if (!isBraking && Mathf.Abs(throttleInput) > 0.01f)
        {
            bool pressingBack = throttleInput < 0f;

            if (pressingBack && speed > 0.3f)
            {
                rb.drag = brakeDrag;
            }
            else if (Mathf.Abs(speed) < maxSpeed)
            {
                Vector3 force = transform.forward * throttleInput * driveForce;
                rb.AddForce(force, ForceMode.Force);
            }
        }

        if (Mathf.Abs(speed) > 0.2f)
        {
            float steerDir = Mathf.Sign(speed);
        
            float turnAmount = steerInput * turnSpeed * steerDir * Time.fixedDeltaTime;
        
            Quaternion targetRotation =
                rb.rotation * Quaternion.Euler(0f, turnAmount, 0f);
        
            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    0.8f
                )
            );
        }

        if (steeringWheel)
        {
            float targetAngle = steerInput * steeringWheelMaxAngle;
            Quaternion targetRotation = defaultSteeringRotation * Quaternion.Euler(0f, targetAngle, 0f);
            steeringWheel.localRotation = Quaternion.Lerp(
                steeringWheel.localRotation,
                targetRotation,
                Time.fixedDeltaTime * 8f
            );
        }
    }

    // ─────────────────────────────────────────────
    //  WHEEL VISUALS
    // ─────────────────────────────────────────────

    void UpdateWheelVisuals()
    {
        if (wheelMeshes == null) return;

        float speed = rb != null ? Vector3.Dot(rb.velocity, transform.forward) : 0f;

        float wheelRadius = 0.35f;
        wheelRollAngle += (speed / wheelRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;
        wheelRollAngle = Mathf.Repeat(wheelRollAngle, 360f);

        float steerAngle = steerInput * 30f;

        for (int i = 0; i < wheelMeshes.Length; i++)
        {
            if (wheelMeshes[i] == null) continue;

            if (i < 2)
            {
                Quaternion steerRot = Quaternion.Euler(0f, steerAngle, -90f);
                Quaternion spinRot = Quaternion.Euler(0f, wheelRollAngle, 0f);
                wheelMeshes[i].localRotation = steerRot * spinRot;
            }
            else
            {
                Quaternion baseOffsetRot = Quaternion.Euler(0f, 0f, -90f);
                Quaternion spinRot = Quaternion.Euler(0f, wheelRollAngle, 0f);
                wheelMeshes[i].localRotation = baseOffsetRot * spinRot;
            }
        }
    }

    // ─────────────────────────────────────────────
    //  ENGINE SOUND STATE MACHINE
    // ─────────────────────────────────────────────

    void UpdateEngineSound()
    {
        float speed = Vector3.Dot(rb.velocity, transform.forward);
        bool isMovingInput = Mathf.Abs(throttleInput) > 0.05f;

        bool isActuallyReversing = throttleInput < -0.05f && speed <= 0.3f;
        bool isBrakingWithS = throttleInput < -0.05f && speed > 0.3f;

        switch (engineState)
        {
            case EngineState.Idle:
                if (isActuallyReversing)
                {
                    engineState = EngineState.Reversing;
                    PlayEngineClip(reverseClip, idlePitch);
                }
                else if (isMovingInput && !isBrakingWithS)
                {
                    idleTimer += Time.deltaTime;
                    if (idleTimer >= idleToRunDelay)
                    {
                        idleTimer = 0f;
                        engineState = EngineState.Running;
                        PlayEngineClip(runningClip, idlePitch);
                    }
                }
                else
                {
                    idleTimer = 0f;
                    if (engineAudioSource)
                        engineAudioSource.pitch = Mathf.Lerp(
                            engineAudioSource.pitch, idlePitch, Time.deltaTime * pitchLerpSpeed);
                }
                break;

            case EngineState.Running:
                if (isActuallyReversing)
                {
                    engineState = EngineState.Reversing;
                    PlayEngineClip(reverseClip, idlePitch);
                }
                else if (!isMovingInput || isBrakingWithS)
                {
                    engineState = EngineState.Idle;
                    PlayEngineClip(idleClip, idlePitch);
                }
                else
                {
                    float targetPitch = Mathf.Lerp(idlePitch, maxPitch, Mathf.Abs(throttleInput));
                    if (engineAudioSource)
                        engineAudioSource.pitch = Mathf.Lerp(
                            engineAudioSource.pitch, targetPitch, Time.deltaTime * pitchLerpSpeed);
                }
                break;

            case EngineState.Reversing:
                if (!isActuallyReversing)
                {
                    engineState = EngineState.Idle;
                    PlayEngineClip(idleClip, idlePitch);
                }
                else
                {
                    float targetPitch = Mathf.Lerp(idlePitch, maxPitch * 0.85f, Mathf.Abs(throttleInput));
                    if (engineAudioSource)
                        engineAudioSource.pitch = Mathf.Lerp(
                            engineAudioSource.pitch, targetPitch, Time.deltaTime * pitchLerpSpeed);
                }
                break;
        }
    }

    void PlayEngineClip(AudioClip clip, float pitch)
    {
        if (!engineAudioSource || clip == null) return;
        if (engineAudioSource.clip == clip && engineAudioSource.isPlaying) return;

        engineAudioSource.clip = clip;
        engineAudioSource.pitch = pitch;
        engineAudioSource.loop = true;
        engineAudioSource.Play();
    }

    // ─────────────────────────────────────────────
    //  COLLISION
    // ─────────────────────────────────────────────

    void OnCollisionEnter(Collision collision)
    {
        if (!isInCar) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        float silentBelow = 4f;
        float fullVolumeAt = 15f;

        if (impactSpeed < silentBelow) return;

        float volume = Mathf.InverseLerp(silentBelow, fullVolumeAt, impactSpeed);

        if (sfxAudioSource && collisionClip)
            sfxAudioSource.PlayOneShot(collisionClip, volume);

        if (vanHub != null && impactSpeed > minImpactSpeed)
        {
            float damage = (impactSpeed - minImpactSpeed) * damagePerSpeedUnit;
            vanHub.DamageVan(damage);
            Debug.Log($"[CarController] Collision damage: {damage:F1} | impact: {impactSpeed:F1} | volume: {volume:F2}");
        }
    }
}