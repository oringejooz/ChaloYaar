using System.Collections;
using UnityEngine;

/// <summary>
/// CarController.cs  –  v3 (NO WheelColliders)
///
/// HOW TO SET UP:
/// 1. Attach this script to the car GameObject (the one with the Rigidbody).
/// 2. The car needs: a Rigidbody + a normal box/mesh Collider on its body.
/// 3. Assign all Inspector references.
/// 4. Wheel meshes are purely visual — just assign the 4 Transform references.
/// 5. No WheelColliders needed at all.
/// </summary>
public class CarController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  INSPECTOR REFERENCES
    // ─────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The player character GameObject.")]
    public GameObject player;

    [Tooltip("The steering wheel mesh Transform (rotates on local Z).")]
    public Transform steeringWheel;

    [Tooltip("The door collider the player clicks to enter.")]
    public Collider doorCollider;

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
    [Tooltip("Forward / reverse force in Newtons.")]
    public float driveForce = 4000f;

    [Tooltip("How strongly the car turns (degrees per second).")]
    public float turnSpeed = 60f;

    [Tooltip("Drag applied while braking (Space).")]
    public float brakeDrag = 8f;

    [Tooltip("Normal linear drag while driving.")]
    public float driveDrag = 0.5f;

    [Tooltip("Normal angular drag (prevents spin).")]
    public float angDrag = 5f;

    [Tooltip("Max speed in m/s before force is no longer added.")]
    public float maxSpeed = 20f;

    [Tooltip("How close the player must be to enter (metres).")]
    public float enterDistance = 4f;

    [Tooltip("Max steering wheel mesh rotation (degrees each side).")]
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

    // Wheel spin tracking
    private float wheelRollAngle = 0f;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[CarController] No Rigidbody found on this GameObject!");
            return;
        }

        // Stable defaults
        rb.mass = 1200f;
        rb.drag = driveDrag;
        rb.angularDrag = angDrag;
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Freeze rotation on X and Z so the car doesn't tumble
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Kill any stray velocity
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (insideCamera) insideCamera.gameObject.SetActive(false);
        if (outsideCamera) outsideCamera.gameObject.SetActive(false);

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
            DriveCar();

        UpdateWheelVisuals();
    }

    // ─────────────────────────────────────────────
    //  ENTER / EXIT
    // ─────────────────────────────────────────────

    void HandleEnterExit()
    {
        if (!isInCar && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider == doorCollider)
                {
                    float dist = Vector3.Distance(player.transform.position, transform.position);
                    if (dist <= enterDistance)
                        EnterCar();
                }
            }
        }

        if (isInCar && Input.GetKeyDown(KeyCode.E))
            ExitCar();
    }

    void EnterCar()
    {
        isInCar = true;
        if (Camera.main) Camera.main.gameObject.SetActive(false);
        if (insideCamera) insideCamera.gameObject.SetActive(true);
        StartCoroutine(StartEngine());
    }

    void ExitCar()
    {
        isInCar = false;
        engineStarted = false;
        engineState = EngineState.Off;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.drag = 10f; // high drag = parked

        if (engineAudioSource) engineAudioSource.Stop();

        if (insideCamera) insideCamera.gameObject.SetActive(false);
        if (outsideCamera) outsideCamera.gameObject.SetActive(false);
        if (Camera.main) Camera.main.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────
    //  ENGINE START
    // ─────────────────────────────────────────────

    System.Collections.IEnumerator StartEngine()
    {
        engineState = EngineState.Starting;

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
    //  CAR PHYSICS  (force-based, no WheelColliders)
    // ─────────────────────────────────────────────

    void DriveCar()
    {
        // ── Braking ──────────────────────────────────────────────────────
        rb.drag = isBraking ? brakeDrag : driveDrag;

        // ── Forward / backward force ──────────────────────────────────────
        float speed = Vector3.Dot(rb.velocity, transform.forward);

        if (!isBraking && Mathf.Abs(throttleInput) > 0.01f)
        {
            // Only add force if under max speed
            if (Mathf.Abs(speed) < maxSpeed)
            {
                Vector3 force = transform.forward * throttleInput * driveForce;
                rb.AddForce(force, ForceMode.Force);
            }
        }

        // ── Steering (only when moving) ───────────────────────────────────
        if (Mathf.Abs(speed) > 0.5f)
        {
            // Steer in the direction of travel (flips when reversing)
            float steerDir = Mathf.Sign(speed);
            float turnAmount = steerInput * turnSpeed * steerDir * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
        }

        // ── Steering wheel mesh ───────────────────────────────────────────
        if (steeringWheel)
        {
            float targetAngle = -steerInput * steeringWheelMaxAngle;
            steeringWheel.localRotation = Quaternion.Lerp(
                steeringWheel.localRotation,
                Quaternion.Euler(0f, 0f, targetAngle),
                Time.fixedDeltaTime * 8f
            );
        }
    }

    // ─────────────────────────────────────────────
    //  WHEEL VISUALS  (spin + steer, purely cosmetic)
    // ─────────────────────────────────────────────

    void UpdateWheelVisuals()
    {
        if (wheelMeshes == null) return;

        float speed = rb != null ? Vector3.Dot(rb.velocity, transform.forward) : 0f;

        // Accumulate roll angle based on speed
        float wheelRadius = 0.35f;  // tweak to match your tyre size
        wheelRollAngle += (speed / wheelRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;
        wheelRollAngle = Mathf.Repeat(wheelRollAngle, 360f);

        float steerAngle = steerInput * 30f;

        for (int i = 0; i < wheelMeshes.Length; i++)
        {
            if (wheelMeshes[i] == null) continue;

            if (i < 2)
            {
                // Front wheels: roll + steer
                wheelMeshes[i].localRotation = Quaternion.Euler(wheelRollAngle, steerAngle, 0f);
            }
            else
            {
                // Rear wheels: roll only
                wheelMeshes[i].localRotation = Quaternion.Euler(wheelRollAngle, 0f, 0f);
            }
        }
    }

    // ─────────────────────────────────────────────
    //  ENGINE SOUND STATE MACHINE
    // ─────────────────────────────────────────────

    void UpdateEngineSound()
    {
        bool isMovingInput = Mathf.Abs(throttleInput) > 0.05f;
        bool isReversing = throttleInput < -0.05f;

        switch (engineState)
        {
            case EngineState.Idle:
                if (isReversing)
                {
                    engineState = EngineState.Reversing;
                    PlayEngineClip(reverseClip, idlePitch);
                }
                else if (isMovingInput)
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
                if (isReversing)
                {
                    engineState = EngineState.Reversing;
                    PlayEngineClip(reverseClip, idlePitch);
                }
                else if (!isMovingInput)
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
                if (!isReversing)
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