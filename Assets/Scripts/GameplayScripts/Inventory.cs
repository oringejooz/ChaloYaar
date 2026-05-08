using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Inventory.cs  +  ItemData.cs  +  ConsumablePickup.cs
//  Chalo Yaar! — Item data, player inventory, and world pickups.
//
//  All items are defined as ItemData ScriptableObjects. The Inventory holds a
//  list of ItemSlots (item + quantity). ConsumablePickup is an Interactable
//  placed in the world (fridge, van shelf, store counter) that gives the player
//  an item or lets them consume it directly.
//
//  Create items: Right-click → Chalo Yaar → Item Data
// ─────────────────────────────────────────────────────────────────────────────

// ── Inventory Slot ────────────────────────────────────────────────────────────
[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int      quantity;

    public InventorySlot(ItemData item, int qty)
    {
        this.item     = item;
        this.quantity = qty;
    }

    public bool IsEmpty => item == null || quantity <= 0;
}

// ── Inventory Component ───────────────────────────────────────────────────────
public class Inventory : MonoBehaviour
{
    [Header("Capacity")]
    public int maxSlots = 12;

    [Header("Starting Items")]
    public List<InventorySlot> startingItems = new List<InventorySlot>();

    // Runtime
    private List<InventorySlot> _slots = new List<InventorySlot>();

    // Events
    public System.Action OnInventoryChanged;
    public System.Action<ItemData> OnItemAdded;
    public System.Action<ItemData> OnItemRemoved;
    public System.Action<ItemData> OnItemConsumed;

    public IReadOnlyList<InventorySlot> Slots => _slots;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        foreach (var s in startingItems)
            if (s.item != null && s.quantity > 0)
                AddItem(s.item, s.quantity);
    }

    // ── Add / Remove ──────────────────────────────────────────────────────────

    /// <summary>Returns the number of items that couldn't be added (overflow).</summary>
    public int AddItem(ItemData item, int quantity = 1)
    {
        int remaining = quantity;

        // Fill existing stacks first
        if (item.isStackable)
        {
            foreach (var slot in _slots)
            {
                if (slot.item != item) continue;
                int canFit = item.maxStack - slot.quantity;
                if (canFit <= 0) continue;
                int toAdd = Mathf.Min(remaining, canFit);
                slot.quantity += toAdd;
                remaining -= toAdd;
                if (remaining <= 0) break;
            }
        }

        // Create new slots for remainder
        while (remaining > 0)
        {
            if (_slots.Count >= maxSlots)
            {
                Debug.Log($"[Inventory] Full — couldn't add {remaining}x {item.itemName}");
                break;
            }

            int toAdd = item.isStackable ? Mathf.Min(remaining, item.maxStack) : 1;
            _slots.Add(new InventorySlot(item, toAdd));
            remaining -= toAdd;
        }

        if (remaining < quantity)
        {
            OnItemAdded?.Invoke(item);
            OnInventoryChanged?.Invoke();
        }

        return remaining; // 0 = all added, >0 = overflow
    }

    /// <summary>Remove quantity from inventory. Returns false if not enough.</summary>
    public bool RemoveItem(ItemData item, int quantity = 1)
    {
        int available = CountItem(item);
        if (available < quantity) return false;

        int toRemove = quantity;
        for (int i = _slots.Count - 1; i >= 0 && toRemove > 0; i--)
        {
            if (_slots[i].item != item) continue;
            int take = Mathf.Min(_slots[i].quantity, toRemove);
            _slots[i].quantity -= take;
            toRemove -= take;
            if (_slots[i].quantity <= 0) _slots.RemoveAt(i);
        }

        OnItemRemoved?.Invoke(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public int CountItem(ItemData item)
    {
        int total = 0;
        foreach (var slot in _slots)
            if (slot.item == item) total += slot.quantity;
        return total;
    }

    public bool HasItem(ItemData item, int quantity = 1) => CountItem(item) >= quantity;

    // ── Consume ───────────────────────────────────────────────────────────────

    /// <summary>Consume one of this item and apply its effect to the player.</summary>
    public void ConsumeItem(ItemData item, PlayerController player)
    {
        if (item.itemType != ItemType.Consumable)
        {
            Debug.LogWarning($"[Inventory] {item.itemName} is not consumable.");
            return;
        }

        if (!RemoveItem(item, 1)) return;

        StartCoroutine(ApplyConsumeDelay(item, player));
        OnItemConsumed?.Invoke(item);
    }

    System.Collections.IEnumerator ApplyConsumeDelay(ItemData item, PlayerController player)
    {
        // Play animation
        if (player.Movement.TryGetComponent(out Animator anim) && !string.IsNullOrEmpty(item.consumeAnimTrigger))
            anim.SetTrigger(item.consumeAnimTrigger);

        yield return new WaitForSeconds(item.consumeDelay);

        player.Stats.ApplyConsumable(item.effect);
        Debug.Log($"[Inventory] Consumed: {item.itemName}");
    }
}