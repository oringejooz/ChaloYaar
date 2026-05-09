// ── Item Types ────────────────────────────────────────────────────────────────
using UnityEngine;
public enum ItemType
{
    Consumable,    // food, drink
    Tool,          // wrench, camera
    VanSupply,     // petrol canister, gas cylinder, water bottle
    Currency,      // money (usually not a physical item, but just in case)
    QuestItem,
}

// ── ItemData ScriptableObject ─────────────────────────────────────────────────
[CreateAssetMenu(menuName = "Chalo Yaar/Item Data", fileName = "NewItem")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemName = "Item";
    public string description = "";
    public Sprite icon;
    public GameObject worldPrefab; // dropped / displayed in world
    public ItemType itemType = ItemType.Consumable;

    [Header("Stack")]
    public bool isStackable = true;
    public int maxStack = 10;

    [Header("Economy")]
    public int buyPrice = 10;  // cost at store
    public int sellPrice = 5;   // sell back value

    [Header("Consumable Effect (ignored if not Consumable type)")]
    public ConsumableEffect effect;

    [Header("Consume Animation")]
    [Tooltip("Animator trigger name when consuming (e.g. Eat, Drink)")]
    public string consumeAnimTrigger = "Eat";
    [Tooltip("Seconds before stat effect applies (eat animation delay)")]
    public float consumeDelay = 1f;
    public AudioClip consumeSound; // ← add this
}