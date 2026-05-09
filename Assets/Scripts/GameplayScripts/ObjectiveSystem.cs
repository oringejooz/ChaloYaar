using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ── Objective Definition ──────────────────────────────────────────────────────
[System.Serializable]
public class Objective
{
    public string title = "New Objective";
    [TextArea] public string description = "";
    public ObjectiveTrigger trigger;

    // Trigger-specific targets (fill whichever matches your trigger type)
    [Tooltip("For RepairVan: health threshold to reach. For EarnMoney: amount to earn.")]
    public float targetValue = 100f;
    [Tooltip("For PhotoAnimal: which animal name must be photographed, For others: what item name must be consumed or collected")]
    public string targetItemName = "";
}

public enum ObjectiveTrigger
{
    RepairVan,          // van health reaches targetValue
    EarnMoney,          // balance reaches targetValue
    PhotoAnimal,        // photograph animal matching targetAnimalName
    CollectItem,        // pick up item matching targetAnimalName (reused as itemName)
    ConsumeItem,        // consume an item
    Manual,             // call ObjectiveSystem.CompleteCurrentObjective() from code
}

// ── Objective System ──────────────────────────────────────────────────────────
public class ObjectiveSystem : MonoBehaviour
{
    public static ObjectiveSystem Instance { get; private set; }

    [Header("Objectives (in order)")]
    public List<Objective> objectives = new List<Objective>();

    [Header("References")]
    public HUDManager hudManager;
    public VanSystemsHub vanHub;

    [Header("Timing")]
    [Tooltip("How long the completed objective stays visible before fading")]
    public float completedHoldDuration = 1.5f;
    [Tooltip("Fade-out duration in seconds")]
    public float fadeDuration = 1f;
    [Tooltip("How long after fade before next objective appears")]
    public float nextObjectiveDelay = 0.5f;

    private int _currentIndex = -1;
    private bool _checking = true;

    public System.Action<Objective> OnObjectiveStarted;
    public System.Action<Objective> OnObjectiveCompleted;
    public System.Action OnAllObjectivesComplete;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Subscribe to game events for automatic trigger checking
        if (vanHub != null)
            vanHub.health.OnChanged += _ => CheckCurrentObjective();

        if (MoneySystem.Instance != null)
            MoneySystem.Instance.OnBalanceChanged += (bal, _) => CheckCurrentObjective();

        AdvanceToNextObjective();
    }

    void OnDestroy()
    {
        if (vanHub != null)
            vanHub.health.OnChanged -= _ => CheckCurrentObjective();
    }

    // ── Objective Flow ────────────────────────────────────────────────────────

    void AdvanceToNextObjective()
    {
        _currentIndex++;
        _checking = true;

        if (_currentIndex >= objectives.Count)
        {
            hudManager?.SetObjective("");
            OnAllObjectivesComplete?.Invoke();
            Debug.Log("[Objectives] All objectives complete!");
            return;
        }

        var obj = objectives[_currentIndex];
        hudManager?.SetObjective($"[ ] {obj.title}\n{obj.description}");
        OnObjectiveStarted?.Invoke(obj);
        Debug.Log($"[Objectives] Started: {obj.title}");

        // Check immediately in case condition is already met
        CheckCurrentObjective();
    }

    void CheckCurrentObjective()
    {
        if (!_checking || _currentIndex >= objectives.Count) return;
        var obj = objectives[_currentIndex];

        bool met = obj.trigger switch
        {
            ObjectiveTrigger.RepairVan => vanHub != null && vanHub.health.current >= obj.targetValue,
            ObjectiveTrigger.EarnMoney => MoneySystem.Instance != null && MoneySystem.Instance.Balance >= (int)obj.targetValue,
            ObjectiveTrigger.Manual => false, // only via CompleteCurrentObjective()
            ObjectiveTrigger.PhotoAnimal => false, // fired from PhotographySystem event
            ObjectiveTrigger.CollectItem => false, // fired from Inventory event
            ObjectiveTrigger.ConsumeItem => false, // fired from inventory event
            _ => false
        };

        if (met) StartCoroutine(CompleteSequence(obj));
    }

    IEnumerator CompleteSequence(Objective obj)
    {
        _checking = false;
        OnObjectiveCompleted?.Invoke(obj);
        Debug.Log($"[Objectives] Completed: {obj.title}");

        // Show green completed text
        hudManager?.SetObjective($"<color=green>✓ {obj.title}</color>");

        yield return new WaitForSeconds(completedHoldDuration);

        // Fade out
        yield return StartCoroutine(FadeObjectiveText());

        yield return new WaitForSeconds(nextObjectiveDelay);

        AdvanceToNextObjective();
    }

    IEnumerator FadeObjectiveText()
    {
        // Requires objectiveText to be a TMP element — fade via alpha
        var tmp = hudManager?.objectiveText;
        if (tmp == null) yield break;

        float elapsed = 0f;
        Color original = tmp.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            tmp.color = new Color(original.r, original.g, original.b, a);
            yield return null;
        }

        tmp.color = new Color(original.r, original.g, original.b, 1f); // reset alpha for next
        hudManager?.SetObjective("");
    }
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call this from PhotographySystem or Inventory for Manual/Photo/Item triggers.</summary>
    public void CompleteCurrentObjective()
    {
        if (_currentIndex >= objectives.Count || !_checking) return;
        StartCoroutine(CompleteSequence(objectives[_currentIndex]));
    }

    /// <summary>Call with animal name after a photo is taken.</summary>
    public void NotifyPhotoTaken(string animalName)
    {
        if (!_checking || _currentIndex >= objectives.Count) return;
        var obj = objectives[_currentIndex];
        if (obj.trigger != ObjectiveTrigger.PhotoAnimal) return;

        bool nameMatches = string.IsNullOrEmpty(obj.targetItemName) ||
                           obj.targetItemName.Equals(animalName, System.StringComparison.OrdinalIgnoreCase);

        if (nameMatches)
            StartCoroutine(CompleteSequence(obj));
    }

    /// <summary>Call with item name when an item is picked up.</summary>
    public void NotifyItemCollected(string itemName)
    {
        if (!_checking || _currentIndex >= objectives.Count) return;
        var obj = objectives[_currentIndex];
        if (obj.trigger != ObjectiveTrigger.CollectItem) return;

        bool nameMatches = string.IsNullOrEmpty(obj.targetItemName) ||
                           obj.targetItemName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase);

        if (nameMatches)
            StartCoroutine(CompleteSequence(obj));
    }

    /// <summary>Call with item name when an item is consumed.</summary>
    public void NotifyItemConsumed(string itemName)
    {
        if (!_checking || _currentIndex >= objectives.Count) return;
        var obj = objectives[_currentIndex];
        if (obj.trigger != ObjectiveTrigger.ConsumeItem) return;

        bool nameMatches = string.IsNullOrEmpty(obj.targetItemName) ||
                           obj.targetItemName.Equals(itemName, System.StringComparison.OrdinalIgnoreCase);

        if (nameMatches)
            StartCoroutine(CompleteSequence(obj));
    }

    public Objective CurrentObjective => _currentIndex < objectives.Count ? objectives[_currentIndex] : null;
}