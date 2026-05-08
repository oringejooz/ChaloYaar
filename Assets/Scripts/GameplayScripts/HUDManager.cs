using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    // ── Player Sliders (Health + Stamina) ──────────────────────────────────────
    [Header("Player Sliders")]
    public Slider healthBar;
    public Slider staminaBar;

    [Header("Slider Colors")]
    public Image healthFill;
    public Image staminaFill;

    // ── Player Text Stats (Hunger, Thirst, Drowsiness) ─────────────────────────
    [Header("Player Text Stats")]
    public TextMeshProUGUI hungerText;
    public TextMeshProUGUI thirstText;
    public TextMeshProUGUI drowsinessText;

    [Header("Critical Color")]
    public Color criticalColor = Color.red;

    // ── Money ──────────────────────────────────────────────────────────────────
    [Header("Money")]
    public TextMeshProUGUI moneyText;

    // ── Interaction Prompt ─────────────────────────────────────────────────────
    [Header("Interaction Prompt")]
    public GameObject interactPromptRoot;
    public TextMeshProUGUI interactPromptText;

    // ── Objective Text ─────────────────────────────────────────────────────────
    [Header("Objective")]
    public TextMeshProUGUI objectiveText;

    // ── Alert Toast ────────────────────────────────────────────────────────────
    [Header("Alert Toast")]
    public GameObject alertRoot;
    public TextMeshProUGUI alertText;
    public float alertDuration = 4f;

    // ── Van Bars ───────────────────────────────────────────────────────────────
    [Header("Van HUD")]
    public GameObject vanHUDRoot;
    public Slider fuelBar;
    public Slider vanHealthBar;
    public Slider gasBar;
    public Slider waterBar;
    public Slider trashBar;

    // ── Scene References ───────────────────────────────────────────────────────
    [Header("Scene References")]
    public PlayerController playerController;
    public VanSystemsHub vanHub;
    public InteractionSystem interactionSystem;

    // ── Private ────────────────────────────────────────────────────────────────
    private PlayerStats _stats;
    private float _alertTimer;

    // ──────────────────────────────────────────────────────────────────────────
    void Start()
    {
        _stats = playerController.Stats;

        Debug.Log($"[HUDManager] healthBar assigned: {healthBar != null}");
        Debug.Log($"[HUDManager] staminaBar assigned: {staminaBar != null}");
        Debug.Log($"[HUDManager] Health value: {_stats.Health}, Stamina value: {_stats.Stamina}");

        _stats.OnStatChanged += HandleStatChanged;
        _stats.OnStatChanged += (type, value) => {
            if (type == StatType.Health)
                SetSlider(healthBar, value / PlayerStats.MAX_VALUE);
            if (type == StatType.Stamina)
                SetSlider(staminaBar, value / PlayerStats.MAX_VALUE);
        };
        _stats.OnPlayerDeath += HandlePlayerDeath;

        if (MoneySystem.Instance != null)
            MoneySystem.Instance.OnBalanceChanged += HandleMoneyChanged;

        if (interactionSystem != null)
            interactionSystem.OnFocusPromptChanged += HandlePromptChanged;

        if (vanHub != null)
        {
            vanHub.fuel.OnChanged += v => SetSlider(fuelBar, v);
            vanHub.health.OnChanged += v => SetSlider(vanHealthBar, v);
            vanHub.gas.OnChanged += v => SetSlider(gasBar, v);
            vanHub.water.OnChanged += v => SetSlider(waterBar, v);
            vanHub.trash.OnChanged += v => SetSlider(trashBar, v);
            vanHub.OnSystemAlert += ShowAlert;
        }

        RefreshAllStats();
        RefreshMoney();
        HidePrompt();
    }

    void OnDestroy()
    {
        if (_stats != null)
        {
            _stats.OnStatChanged -= HandleStatChanged;
            _stats.OnPlayerDeath -= HandlePlayerDeath;
        }
        if (MoneySystem.Instance != null)
            MoneySystem.Instance.OnBalanceChanged -= HandleMoneyChanged;
        if (interactionSystem != null)
            interactionSystem.OnFocusPromptChanged -= HandlePromptChanged;
    }

    void Update()
    {
        // Alert toast countdown
        if (_alertTimer > 0f)
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f && alertRoot != null)
                alertRoot.SetActive(false);
        }
    }

    // ── Stat Updates ──────────────────────────────────────────────────────────
    void HandleStatChanged(StatType type, float value)
    {
        float norm = value / PlayerStats.MAX_VALUE;
        float percentage = Mathf.RoundToInt(norm * 100f);

        switch (type)
        {
            // Sliders — no color change
            case StatType.Health:
                SetSlider(healthBar, norm);
                break;

            case StatType.Stamina:
                SetSlider(staminaBar, norm);
                break;

            // Text % — only change to criticalColor when depleted
            case StatType.Hunger:
                UpdateStatText(hungerText, percentage, norm, inverse: false);
                break;

            case StatType.Thirst:
                UpdateStatText(thirstText, percentage, norm, inverse: false);
                break;

            case StatType.Drowsiness:
                UpdateStatText(drowsinessText, percentage, norm, inverse: true);
                break;
        }
    }

    void UpdateStatText(TextMeshProUGUI textElement, float percentage, float normalized, bool inverse)
    {
        if (textElement == null) return;

        textElement.text = $"{percentage}%";

        // Only change to criticalColor when the stat is critically low (or high for drowsiness)
        bool isCritical = inverse
            ? normalized >= 1f    // drowsiness at 100% = critical
            : normalized <= 0f;    // hunger/thirst at 0% = critical

        if (isCritical)
            textElement.color = criticalColor;
    }

    void RefreshAllStats()
    {
        if (_stats == null) return;
        HandleStatChanged(StatType.Hunger, _stats.Hunger);
        HandleStatChanged(StatType.Thirst, _stats.Thirst);
        HandleStatChanged(StatType.Stamina, _stats.Stamina);
        HandleStatChanged(StatType.Drowsiness, _stats.Drowsiness);
        HandleStatChanged(StatType.Health, _stats.Health);
    }

    // ── Player Death ──────────────────────────────────────────────────────────
    void HandlePlayerDeath()
    {
        ShowAlert("☠️ You have died! Game Over.");
        // TODO: Disable player movement, show death screen, offer respawn
    }

    // ── Money ─────────────────────────────────────────────────────────────────
    void HandleMoneyChanged(int newBalance, int delta) => RefreshMoney();

    void RefreshMoney()
    {
        if (moneyText == null || MoneySystem.Instance == null) return;
        moneyText.text = $"₹ {MoneySystem.Instance.Balance:N0}";
    }

    // ── Interaction Prompt ────────────────────────────────────────────────────
    void HandlePromptChanged(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            HidePrompt();
            return;
        }

        if (interactPromptRoot != null) interactPromptRoot.SetActive(true);
        if (interactPromptText != null) interactPromptText.text = prompt;
    }

    void HidePrompt()
    {
        if (interactPromptRoot != null) interactPromptRoot.SetActive(false);
    }

    // ── Alert Toast ───────────────────────────────────────────────────────────
    public void ShowAlert(string message)
    {
        if (alertRoot == null) return;
        alertRoot.SetActive(true);
        if (alertText != null) alertText.text = message;
        _alertTimer = alertDuration;
    }

    // ── Objective Text ────────────────────────────────────────────────────────
    public void SetObjective(string text)
    {
        if (objectiveText != null)
            objectiveText.text = text;
    }

    // ── Van HUD ───────────────────────────────────────────────────────────────
    public void SetVanHUDVisible(bool visible)
    {
        if (vanHUDRoot != null) vanHUDRoot.SetActive(visible);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static void SetSlider(Slider s, float normalizedValue)
    {
        if (s == null) return;
        //Debug.Log($"[HUDManager] SetSlider called: {normalizedValue}");
        s.value = Mathf.Clamp01(normalizedValue);
    }
}