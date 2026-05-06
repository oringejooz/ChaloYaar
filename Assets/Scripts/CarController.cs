using System.Collections;
using UnityEngine;

/// <summary>
/// CarController.cs – v5
/// Added physics diagnostics to find why the van won't move.
/// </summary>
public class CarController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR REFERENCES
    // ─────────────────────────────────────────────

    [Header("References")]
    public GameObject player;
    public Transform steeringWheel;
    public Collider doorCollider;

    [Tooltip("Empty GameObject placed next to the van door — player spawns here on exit.")]
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
    public float driveForce = 4000f;
    public float turnSpeed = 60f;
    public float brakeDrag = 8f;
    public float driveDrag = 0.5f;
    public float angDrag = 5f;
    public float maxSpeed = 20f;
    public float enterDistance = 4f;
    public float steeringWheelMaxAngle = 180f;

    [Header("Audio Tuning")]
    public float idlePitch = 0.6f;
    public float maxPitch = 1.6f;
    public float pitchLerpSpeed = 3f;
    public float idleToRunDelay = 0.3f;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private Rigidbody rb;
    private bool isInCar = false;
    private bool engineStarted = false;

    private enum EngineState { Off, Starting, Idle, Running, Reversing }
    private EngineState engineState = EngineState.Off;
    private float idleTimer = 0f;

    private float throttleInput;
    private float steerInput;
    private bool isBraking;

    private float wheelRollAngle = 0f;
    private Quaternion defaultSteeringRotation;
    // For throttled debug logs
    private float debugTimer = 0f;
    private const float DEBUG_INTERVAL = 0.5f;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Start()
    {
        if (player == null) Debug.LogError("[CarController] ❌ 'player' not assigned!");
        if (doorCollider == null) Debug.LogError("[CarController] ❌ 'doorCollider' not assigned!");
        if (exitPoint == null) Debug.LogWarning("[CarController] ⚠️ 'exitPoint' not assigned.");
        if (Camera.main == null) Debug.LogError("[CarController] ❌ No MainCamera found!");

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[CarController] ❌ No Rigidbody on this GameObject!");
            return;
        }

        rb.mass = 1000f;
        rb.drag = driveDrag;
        rb.angularDrag = angDrag;
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (insideCamera) insideCamera.gameObject.SetActive(false);
        if (outsideCamera) outsideCamera.gameObject.SetActive(false);
        if(steeringWheel) defaultSteeringRotation = steeringWheel.localRotation;

        if (engineAudioSource)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            engineAudioSource.Stop();
        }

        //// ── Physics diagnostics at start ──────────────────────────────────────────
        //Debug.Log("[CarController] ── PHYSICS SETUP REPORT ──────────────────");
        //Debug.Log("[CarController] Van position Y: " + transform.position.y);
        //Debug.Log("[CarController] Rigidbody isKinematic: " + rb.isKinematic);
        //Debug.Log("[CarController] Rigidbody useGravity: " + rb.useGravity);
        //Debug.Log("[CarController] Rigidbody mass: " + rb.mass);
        //Debug.Log("[CarController] Rigidbody drag: " + rb.drag);
        //Debug.Log("[CarController] Rigidbody constraints: " + rb.constraints);

        //Collider[] cols = GetComponentsInChildren<Collider>();
        //Debug.Log("[CarController] Total colliders in hierarchy: " + cols.Length);
        //foreach (var c in cols)
        //    Debug.Log("  → Collider: " + c.name + " | type: " + c.GetType().Name
        //        + " | isTrigger: " + c.isTrigger
        //        + " | enabled: " + c.enabled);

        //Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        //Debug.Log("[CarController] Total Rigidbodies in hierarchy: " + rbs.Length);
        //foreach (var r in rbs)
        //    Debug.Log("  → Rigidbody on: " + r.gameObject.name
        //        + " | isKinematic: " + r.isKinematic);

        //Debug.Log("[CarController] ────────────────────────────────────────────");
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

            // ── Throttled driving diagnostics ─────────────────────────────────────
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
        if (!isInCar && Input.GetMouseButtonDown(0))
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return;

            Debug.Log("[CarController] 🔍 Raycast hit: " + hit.collider.name);
            if (hit.collider != doorCollider) return;

            float dist = Vector3.Distance(player.transform.position, transform.position);
            Debug.Log("[CarController] 📏 Distance: " + dist.ToString("F2") + "m (limit: " + enterDistance + "m)");
            if (dist > enterDistance)
            {
                Debug.Log("[CarController] ⛔ Too far.");
                return;
            }

            EnterCar();
        }

        if (isInCar && Input.GetKeyDown(KeyCode.E))
            ExitCar();
    }

    void EnterCar()
    {
        isInCar = true;
        Debug.Log("[CarController] 🚗 EnterCar()");

        player.SetActive(false);

        if (insideCamera != null)
            insideCamera.gameObject.SetActive(true);
        else
            Debug.LogWarning("[CarController] ⚠️ insideCamera not assigned.");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;

        // Log physics state right as we enter
        Debug.Log("[CarController] On enter — rb.isKinematic: " + rb.isKinematic
            + " | useGravity: " + rb.useGravity
            + " | drag: " + rb.drag);

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
            Transform spawnAt = exitPoint != null ? exitPoint : transform;
            player.transform.position = spawnAt.position;
            player.transform.rotation = spawnAt.rotation;
            player.SetActive(true);
        }

        if (insideCamera) insideCamera.gameObject.SetActive(false);
        if (outsideCamera) outsideCamera.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[CarController] 🚪 ExitCar() complete.");
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

        Debug.Log("[CarController] ✅ Engine ready — W/S drive, A/D steer, Space brake, E exit.");
        //Debug.Log("[CarController] Post-start rb state — drag: " + rb.drag
        //    + " | isKinematic: " + rb.isKinematic
        //    + " | velocity: " + rb.velocity);
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

        if (!isBraking && Mathf.Abs(throttleInput) > 0.01f)
        {
            bool pressingBack = throttleInput < 0f;

            if (pressingBack && speed > 0.3f)
            {
                // Moving forward + pressing S = brake, not reverse
                rb.drag = brakeDrag;
            }
            else if (Mathf.Abs(speed) < maxSpeed)
            {
                // Either pressing W, or pressing S while already stopped/reversing
                Vector3 force = transform.forward * throttleInput * driveForce;
                rb.AddForce(force, ForceMode.Force);
            }
        }

        if (Mathf.Abs(speed) > 0.5f)
        {
            float steerDir = Mathf.Sign(speed);
            float turnAmount = steerInput * turnSpeed * steerDir * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
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
                // Front wheels: spin (X) + steer (Y) + base offset (Z)
                Quaternion steerRot = Quaternion.Euler(0f, steerAngle, -90f);
                Quaternion spinRot = Quaternion.Euler(0f, wheelRollAngle, 0f);
                wheelMeshes[i].localRotation = steerRot * spinRot;
            }
            else
            {
                // Rear wheels: spin (X) + base offset (Z)
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

        // Truly reversing = pressing S AND the van is already stopped or going backward
        bool isActuallyReversing = throttleInput < -0.05f && speed <= 0.3f;
        // Braking = pressing S while still rolling forward
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
                    // Braking with S sounds like idle, not running
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
    //  COLLISION SOUND
    // ─────────────────────────────────────────────

    void OnCollisionEnter(Collision collision)
    {
        if (!isInCar) return;
        if (collision.relativeVelocity.magnitude < 1.5f) return;
        if (sfxAudioSource && collisionClip)
            sfxAudioSource.PlayOneShot(collisionClip);
    }
}