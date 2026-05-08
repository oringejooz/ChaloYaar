using UnityEngine;

// ── World Pickup ──────────────────────────────────────────────────────────────
public class ConsumablePickup : Interactable
{
    [Header("Item")]
    public ItemData item;
    public int quantity = 1;

    [Tooltip("If true, consume immediately instead of adding to inventory")]
    public bool consumeOnPickup = false;

    [Tooltip("If true, this object is destroyed after pickup")]
    public bool destroyOnPickup = true;

    public override string InteractPrompt =>
        consumeOnPickup
            ? $"Press E to {item?.consumeAnimTrigger ?? "use"} {item?.itemName}"
            : $"Press E to pick up {item?.itemName} x{quantity}";

    public override bool CanInteract(PlayerController player) => item != null;

    public override void Interact(PlayerController player)
    {
        if (item == null) return;

        if (consumeOnPickup && item.itemType == ItemType.Consumable)
        {
            // Add to inventory briefly then consume, or consume directly
            player.Inventory.AddItem(item, quantity);
            player.Inventory.ConsumeItem(item, player);
        }
        else
        {
            int overflow = player.Inventory.AddItem(item, quantity);
            if (overflow > 0)
            {
                Debug.Log($"[Pickup] Inventory full — {overflow} items left on ground.");
                return; // don't destroy if we couldn't pick up all
            }
        }

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}
