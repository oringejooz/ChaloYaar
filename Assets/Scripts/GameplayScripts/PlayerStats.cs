using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public const float MAX_VALUE = 100f;
    public const float MIN_VALUE = 0f;

    // ── Stat Values ───────────────────────────────────────────────────────────
    [Header("Starting Values (0–100)")]
    [Range(0f, 100f)] public float startHunger = 80f;
    [Range(0f, 100f)] public float startThirst = 80f;
    [Range(0f, 100f)] public float startStamina = 100f;
    [Range(0f, 100f)] public float startDrowsiness = 0f;
    [Range(0f, 100f)] public float startHealth = 100f;

    // ── Decay Rates ───────────────────────────────────────────────────────────
    [Header("Decay Rates (units per real second)")]
    public float hungerDecayRate = 0.05f;
    public float thirstDecayRate = 0.07f;
    public float staminaSprintDrain = 0.8f;
    public float staminaRecoverRate = 0.3f;
    public float drowsinessGainRate = 0.02f;
    public float drowsinessDecayRate = 2f;

    [Header("Health Damage Settings")]
    [Tooltip("Health lost per tick when hunger is 0")]
    public float hungerHealthPenalty = 5f;
    [Tooltip("Health lost per tick when thirst is 0")]
    public float thirstHealthPenalty = 3f;
    [Tooltip("Health lost per tick when drowsiness is 100 (max)")]
    public float drowsinessHealthPenalty = 2f;
    [Tooltip("Seconds between health damage ticks")]
    public float healthDamageInterval = 5f;

    // ── Penalty Thresholds ────────────────────────────────────────────────────
    [Header("Penalty Thresholds")]
    public float hungerPenaltyThreshold = 20f;
    public float thirstPenaltyThreshold = 15f;
    public float drowsinessPenaltyThreshold = 75f;

    // ── Runtime State ─────────────────────────────────────────────────────────
    [Header("Current Values — Runtime")]
    [SerializeField, ReadOnly] private float _hunger;
    [SerializeField, ReadOnly] private float _thirst;
    [SerializeField, ReadOnly] private float _stamina;
    [SerializeField, ReadOnly] private float _drowsiness;
    [SerializeField, ReadOnly] private float _health;

    // ── Events ────────────────────────────────────────────────────────────────
    public System.Action<StatType> OnStatDepleted;
    public System.Action<StatType, float> OnStatChanged;
    public System.Action OnPlayerDeath;

    // ── Properties ────────────────────────────────────────────────────────────
    public float Hunger => _hunger;
    public float Thirst => _thirst;
    public float Stamina => _stamina;
    public float Drowsiness => _drowsiness;
    public float Health => _health;

    public bool IsSprinting { get; set; }

    // ── Private ───────────────────────────────────────────────────────────────
    private float _healthDamageTimer;
    private bool _isDead;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _hunger = startHunger;
        _thirst = startThirst;
        _stamina = startStamina;
        _drowsiness = startDrowsiness;
        _health = startHealth;
        _healthDamageTimer = healthDamageInterval;
    }

    void Update()
    {
        if (_isDead) return;
        TickDecay();
        TickHealthDamage();
    }

    // ── Decay ─────────────────────────────────────────────────────────────────
    void TickDecay()
    {
        ModifyStat(ref _hunger, -hungerDecayRate * Time.deltaTime, StatType.Hunger);
        ModifyStat(ref _thirst, -thirstDecayRate * Time.deltaTime, StatType.Thirst);

        float staminaDelta = IsSprinting
            ? -staminaSprintDrain * Time.deltaTime
            : staminaRecoverRate * Time.deltaTime;
        ModifyStat(ref _stamina, staminaDelta, StatType.Stamina);

        ModifyStat(ref _drowsiness, drowsinessGainRate * Time.deltaTime, StatType.Drowsiness);
    }

    // ── Health Damage from Depletion ──────────────────────────────────────────
    void TickHealthDamage()
    {
        _healthDamageTimer -= Time.deltaTime;
        if (_healthDamageTimer > 0f) return;

        _healthDamageTimer = healthDamageInterval;

        float damage = 0f;

        if (_hunger <= MIN_VALUE)
            damage += hungerHealthPenalty;

        if (_thirst <= MIN_VALUE)
            damage += thirstHealthPenalty;

        if (_drowsiness >= MAX_VALUE)
            damage += drowsinessHealthPenalty;

        if (damage > 0f)
        {
            ModifyStat(ref _health, -damage, StatType.Health);

            if (_health <= MIN_VALUE)
            {
                _isDead = true;
                OnPlayerDeath?.Invoke();
                Debug.Log("[PlayerStats] Player has died!");
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void ApplyStat(StatType stat, float delta)
    {
        switch (stat)
        {
            case StatType.Hunger: ModifyStat(ref _hunger, delta, stat); break;
            case StatType.Thirst: ModifyStat(ref _thirst, delta, stat); break;
            case StatType.Stamina: ModifyStat(ref _stamina, delta, stat); break;
            case StatType.Drowsiness: ModifyStat(ref _drowsiness, delta, stat); break;
            case StatType.Health: ModifyStat(ref _health, delta, stat); break;
        }
    }

    public void ApplyConsumable(ConsumableEffect effect)
    {
        if (effect.hungerDelta != 0) ApplyStat(StatType.Hunger, effect.hungerDelta);
        if (effect.thirstDelta != 0) ApplyStat(StatType.Thirst, effect.thirstDelta);
        if (effect.staminaDelta != 0) ApplyStat(StatType.Stamina, effect.staminaDelta);
        if (effect.drowsinessDelta != 0) ApplyStat(StatType.Drowsiness, effect.drowsinessDelta);
        if (effect.healthDelta != 0) ApplyStat(StatType.Health, effect.healthDelta);

        if (effect.causesFlatulence)
            GetComponent<PlayerNeeds>()?.TriggerFlatulence();

        if (effect.causesDrowsinessAfterDelay > 0)
            StartCoroutine(DelayedDrowsiness(effect.causesDrowsinessAfterDelay, effect.delayedDrowsinessAmount));
    }

    public float GetNormalized(StatType stat) => stat switch
    {
        StatType.Hunger => _hunger / MAX_VALUE,
        StatType.Thirst => _thirst / MAX_VALUE,
        StatType.Stamina => _stamina / MAX_VALUE,
        StatType.Drowsiness => _drowsiness / MAX_VALUE,
        StatType.Health => _health / MAX_VALUE,
        _ => 0f
    };

    public bool IsHungry => _hunger < hungerPenaltyThreshold;
    public bool IsThirsty => _thirst < thirstPenaltyThreshold;
    public bool IsDrowsy => _drowsiness > drowsinessPenaltyThreshold;
    public bool IsExhausted => _stamina < 5f;
    public bool IsDead => _isDead;

    // ── Internal ──────────────────────────────────────────────────────────────
    void ModifyStat(ref float stat, float delta, StatType type)
    {
        float prev = stat;
        stat = Mathf.Clamp(stat + delta, MIN_VALUE, MAX_VALUE);

        if (Mathf.Approximately(prev, stat)) return;

        OnStatChanged?.Invoke(type, stat);

        if (stat <= MIN_VALUE && prev > MIN_VALUE)
            OnStatDepleted?.Invoke(type);
    }

    System.Collections.IEnumerator DelayedDrowsiness(float delay, float amount)
    {
        yield return new WaitForSeconds(delay);
        ApplyStat(StatType.Drowsiness, amount);
    }
}

// ── StatType Enum ────────────────────────────────────────────────────────────
public enum StatType
{
    Hunger,
    Thirst,
    Stamina,
    Drowsiness,
    Health
}

// ── ConsumableEffect ──────────────────────────────────────────────────────────
[System.Serializable]
public class ConsumableEffect
{
    [Header("Stat Deltas (positive = increase)")]
    public float hungerDelta;
    public float thirstDelta;
    public float staminaDelta;
    public float drowsinessDelta;
    public float healthDelta;

    [Header("Side Effects")]
    public bool causesFlatulence;

    public float causesDrowsinessAfterDelay;
    public float delayedDrowsinessAmount;
}

// ── ReadOnly Attribute ────────────────────────────────────────────────────────
#if UNITY_EDITOR
public class ReadOnlyAttribute : UnityEngine.PropertyAttribute { }

[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect pos, UnityEditor.SerializedProperty prop, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(pos, prop, label);
        GUI.enabled = true;
    }
    public override float GetPropertyHeight(UnityEditor.SerializedProperty prop, GUIContent label)
        => UnityEditor.EditorGUI.GetPropertyHeight(prop, label);
}
#endif