using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  PlayerNeeds.cs
//  Chalo Yaar! — Translates low/high stat values into gameplay effects.
//
//  Reads from PlayerStats every frame and:
//    • Scales movement speed via PlayerMovementAdvanced
//    • Fires screen-effect events (hook to your post-process volume)
//    • Handles flatulence trigger (audio + animation)
//    • Notifies when player should sleep/rest
//
//  Inspector wiring:
//    • movement   → PlayerMovementAdvanced on same GO
//    • stats      → PlayerStats on same GO
//    • Assign OnScreenEffect callbacks in your UI/PostProcessManager
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerMovementAdvanced))]
public class PlayerNeeds : MonoBehaviour
{
    [Header("Speed Penalties")]
    [Tooltip("Walk speed multiplier when hungry (< hunger threshold)")]
    [Range(0.3f, 1f)] public float hungrySpeedMult   = 0.7f;
    [Tooltip("Walk speed multiplier when drowsy (> drowsiness threshold)")]
    [Range(0.3f, 1f)] public float drowsySpeedMult   = 0.6f;
    [Tooltip("Sprint is blocked when stamina is fully depleted")]
    public bool blockSprintOnExhaustion = true;

    [Header("Flatulence")]
    public AudioClip[] flatulenceClips;
    public AudioSource sfxSource;
    [Tooltip("Min delay between flatulence triggers (seconds)")]
    public float flatulenceCooldown = 8f;

    [Header("Screen Effects — Intensity Curves")]
    [Tooltip("Vignette intensity driven by thirst (0=full thirst, 1=empty thirst)")]
    public AnimationCurve thirstVignetteCurve  = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("Blur/dof intensity driven by drowsiness (0=awake, 1=max drowsy)")]
    public AnimationCurve drowsinessBlurCurve  = AnimationCurve.Linear(0, 0, 1, 1);

    // ── Events (subscribe in PostProcessManager / HUDManager) ─────────────────
    public System.Action<float> OnVignetteChanged;  // 0–1 intensity
    public System.Action<float> OnBlurChanged;       // 0–1 intensity
    public System.Action OnPassedOut;                // fully drowsy → blackscreen

    // ── Private ───────────────────────────────────────────────────────────────
    private PlayerStats _stats;
    private PlayerMovementAdvanced _movement;

    private float _baseWalkSpeed;
    private float _baseSprintSpeed;

    private float _flatulenceTimer;
    private bool  _hasPassed;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _stats    = GetComponent<PlayerStats>();
        _movement = GetComponent<PlayerMovementAdvanced>();

        _baseWalkSpeed   = _movement.walkSpeed;
        _baseSprintSpeed = _movement.sprintSpeed;

        _stats.OnStatDepleted += HandleStatDepleted;
    }

    void OnDestroy()
    {
        if (_stats != null)
            _stats.OnStatDepleted -= HandleStatDepleted;
    }

    void Update()
    {
        ApplySpeedPenalties();
        ApplyScreenEffects();
        TickFlatulence();

        // feed IsSprinting back to stats for stamina drain
        _stats.IsSprinting = _movement.IsSprinting;
    }

    // ── Speed Penalties ───────────────────────────────────────────────────────
    void ApplySpeedPenalties()
    {
        float mult = 1f;
        if (_stats.IsHungry)  mult *= hungrySpeedMult;
        if (_stats.IsDrowsy)  mult *= drowsySpeedMult;

        _movement.walkSpeed   = _baseWalkSpeed   * mult;
        _movement.sprintSpeed = _baseSprintSpeed * mult;

        // Block sprint when exhausted
        if (blockSprintOnExhaustion && _stats.IsExhausted)
            _movement.sprintSpeed = _movement.walkSpeed; // effectively disables sprint
    }

    // ── Screen Effects ────────────────────────────────────────────────────────
    void ApplyScreenEffects()
    {
        // Thirst vignette
        float thirstT = 1f - _stats.GetNormalized(StatType.Thirst); // 0 = full, 1 = empty
        if (_stats.IsThirsty)
            OnVignetteChanged?.Invoke(thirstVignetteCurve.Evaluate(thirstT));
        else
            OnVignetteChanged?.Invoke(0f);

        // Drowsiness blur
        float drowsinessT = _stats.GetNormalized(StatType.Drowsiness);
        if (_stats.IsDrowsy)
            OnBlurChanged?.Invoke(drowsinessBlurCurve.Evaluate(drowsinessT));
        else
            OnBlurChanged?.Invoke(0f);

        // Pass-out at max drowsiness
        if (_stats.Drowsiness >= PlayerStats.MAX_VALUE && !_hasPassed)
        {
            _hasPassed = true;
            OnPassedOut?.Invoke();
        }
        else if (_stats.Drowsiness < PlayerStats.MAX_VALUE)
        {
            _hasPassed = false;
        }
    }

    // ── Flatulence ────────────────────────────────────────────────────────────
    float _flatuluencePending;

    public void TriggerFlatulence()
    {
        // schedule a flatulence event on cooldown
        _flatuluencePending = flatulenceCooldown * Random.Range(0.5f, 1f);
    }

    void TickFlatulence()
    {
        if (_flatuluencePending <= 0f) return;
        _flatuluencePending -= Time.deltaTime;
        if (_flatuluencePending > 0f) return;

        _flatuluencePending = 0f;
        PlayFlatulence();
    }

    void PlayFlatulence()
    {
        if (sfxSource == null || flatulenceClips == null || flatulenceClips.Length == 0) return;
        AudioClip clip = flatulenceClips[Random.Range(0, flatulenceClips.Length)];
        sfxSource.PlayOneShot(clip);
        // TODO: trigger fart animation param on Animator if desired
    }

    // ── Stat Depleted Handler ─────────────────────────────────────────────────
    void HandleStatDepleted(StatType stat)
    {
        switch (stat)
        {
            case StatType.Hunger:
                Debug.Log("[PlayerNeeds] Player is starving!");
                // TODO: play starve voice line, add screen desaturation
                break;
            case StatType.Thirst:
                Debug.Log("[PlayerNeeds] Player is severely dehydrated!");
                break;
            case StatType.Stamina:
                // Stamina hitting 0 is handled via blockSprintOnExhaustion
                break;
        }
    }
}
