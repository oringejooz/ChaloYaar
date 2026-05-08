using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  VanSystems.cs
//  Chalo Yaar! — All van resource systems in one file.
//
//  Systems:
//    • VanFuel        — petrol gauge, triggers low-fuel warning
//    • VanHealth      — van condition, takes damage from rough terrain events
//    • VanGas         — stove gas cylinder level
//    • VanWater       — drinking water dispenser
//    • VanTrash       — trash accumulation
//    • VanSystemsHub  — master component, holds refs to all systems
//
//  Each system is a plain C# class (not MonoBehaviour) so VanSystemsHub owns
//  the Update loop — one component, zero extra GameObjects.
//
//  Inspector wiring (VanSystemsHub):
//    • Assign all AudioClips and UI delegates via the hub.
//    • Interactables (filling stations, sinks, bins) call the hub's methods.
// ─────────────────────────────────────────────────────────────────────────────

// ── Base Resource System ───────────────────────────────────────────────────────
[System.Serializable]
public class VanResource
{
    public float maxValue    = 100f;
    public float startValue  = 100f;

    [System.NonSerialized]
    public float current;

    [System.NonSerialized]
    public System.Action<float> OnChanged;   // normalized 0-1
    [System.NonSerialized]
    public System.Action        OnDepleted;
    [System.NonSerialized]
    public System.Action        OnFull;

    public float Normalized => current / maxValue;
    public bool  IsDepleted => current <= 0f;
    public bool  IsFull     => current >= maxValue;

    public void Init()
    {
        current = startValue;
    }

    public void Modify(float delta)
    {
        float prev = current;
        current = Mathf.Clamp(current + delta, 0f, maxValue);
        if (Mathf.Approximately(prev, current)) return;

        OnChanged?.Invoke(Normalized);
        if (current <= 0f && prev > 0f) OnDepleted?.Invoke();
        if (current >= maxValue && prev < maxValue) OnFull?.Invoke();
    }

    public void Refill(float amount) => Modify(amount);
    public void Refill()             => Modify(maxValue);
    public void Drain(float amount)  => Modify(-amount);
}

// ─────────────────────────────────────────────────────────────────────────────
//  VanSystemsHub — the single MonoBehaviour that owns all van systems
// ─────────────────────────────────────────────────────────────────────────────
public class VanSystemsHub : MonoBehaviour
{
    // ── Fuel ──────────────────────────────────────────────────────────────────
    [Header("Fuel")]
    public VanResource fuel = new VanResource { maxValue = 100f, startValue = 80f };

    [Tooltip("Fuel drained per second while van is moving")]
    public float fuelDrainPerSecond = 0.03f;

    [Tooltip("Fuel level (0-1) that triggers low-fuel warning")]
    public float lowFuelThreshold = 0.2f;

    // ── Health ────────────────────────────────────────────────────────────────
    [Header("Van Health")]
    public VanResource health = new VanResource { maxValue = 100f, startValue = 100f };

    [Tooltip("Health level that triggers \"van needs repair\" warning")]
    public float lowHealthThreshold = 0.25f;

    // ── Stove Gas ─────────────────────────────────────────────────────────────
    [Header("Stove Gas")]
    public VanResource gas = new VanResource { maxValue = 100f, startValue = 100f };

    [Tooltip("Gas drained per second while stove is on")]
    public float gasDrainPerCook = 5f;   // per cooking session

    // ── Water Dispenser ───────────────────────────────────────────────────────
    [Header("Water")]
    public VanResource water = new VanResource { maxValue = 100f, startValue = 100f };

    [Tooltip("Water drained per drink action")]
    public float waterPerDrink = 5f;

    // ── Trash ─────────────────────────────────────────────────────────────────
    [Header("Trash")]
    public VanResource trash = new VanResource { maxValue = 100f, startValue = 0f };

    [Tooltip("Trash added each time a consumable is used")]
    public float trashPerConsumable = 5f;

    [Tooltip("Trash level (0-1) that shows the trash-is-full warning")]
    public float trashFullThreshold = 0.9f;

    // ── Van Moving State ──────────────────────────────────────────────────────
    [Header("Driving")]
    [Tooltip("Set this true by your driving system while the van is in motion")]
    public bool isVanMoving = false;

    // ── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioSource alertSource;
    public AudioClip   lowFuelClip;
    public AudioClip   lowHealthClip;
    public AudioClip   trashFullClip;

    // ── Events (subscribe in HUDManager) ──────────────────────────────────────
    public System.Action<string> OnSystemAlert;   // message to show in HUD

    // ── Alert cooldowns ───────────────────────────────────────────────────────
    private float _fuelAlertCooldown;
    private float _healthAlertCooldown;
    private float _trashAlertCooldown;
    private const float ALERT_INTERVAL = 30f;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        fuel.Init();
        health.Init();
        gas.Init();
        water.Init();
        trash.Init();

        // Force UI update on start — ADD THESE 5 LINES:
        fuel.OnChanged?.Invoke(fuel.Normalized);
        health.OnChanged?.Invoke(health.Normalized);
        gas.OnChanged?.Invoke(gas.Normalized);
        water.OnChanged?.Invoke(water.Normalized);
        trash.OnChanged?.Invoke(trash.Normalized);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Drain fuel while driving
        if (isVanMoving)
            fuel.Drain(fuelDrainPerSecond * dt);

        // Check alerts (rate-limited)
        TickAlert(ref _fuelAlertCooldown,   fuel.Normalized   < lowFuelThreshold,   "⛽ Low fuel! Find a filling station.",       lowFuelClip,   dt);
        TickAlert(ref _healthAlertCooldown, health.Normalized < lowHealthThreshold, "🔧 Van needs repair! Use the wrench.",       lowHealthClip, dt);
        TickAlert(ref _trashAlertCooldown,  trash.Normalized  > trashFullThreshold, "🗑️ Trash bin is full! Empty it at a stop.", trashFullClip, dt);
    }

    void TickAlert(ref float cooldown, bool condition, string message, AudioClip clip, float dt)
    {
        cooldown -= dt;
        if (!condition || cooldown > 0f) return;
        cooldown = ALERT_INTERVAL;
        OnSystemAlert?.Invoke(message);
        if (alertSource != null && clip != null)
            alertSource.PlayOneShot(clip);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Fuel
    public bool CanRefuelFuel(int litres) => MoneySystem.Instance.CanAfford(litres * 2); // ₹2/unit
    public void RefuelFuel(float amount)  => fuel.Refill(amount);

    // Health / Repair
    public void RepairVan(float amount)   => health.Refill(amount);
    public void DamageVan(float amount)   => health.Drain(amount);

    // Gas
    public void UseStove()                => gas.Drain(gasDrainPerCook);
    public void RefillGas(float amount)   => gas.Refill(amount);

    // Water
    public bool DrinkFromDispenser(PlayerController player)
    {
        if (water.current < waterPerDrink)
        {
            OnSystemAlert?.Invoke("💧 Water dispenser empty! Refill at a water station.");
            return false;
        }
        water.Drain(waterPerDrink);
        player.Stats.ApplyStat(StatType.Thirst, 20f);
        return true;
    }
    public void RefillWater(float amount) => water.Refill(amount);

    // Trash
    public void AddTrash(float amount = 0f)
    {
        trash.Refill(amount > 0 ? amount : trashPerConsumable);
    }
    public void EmptyTrash()              => trash.Drain(trash.current);
}
