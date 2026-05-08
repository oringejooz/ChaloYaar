using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [Header("Inventory Slots (5)")]
    public InventorySlotUI[] slots;

    [Header("References")]
    public PlayerController playerController;

    private Inventory _inventory;

    void Start()
    {
        _inventory = playerController.Inventory;
        _inventory.OnInventoryChanged += RefreshUI;
        _inventory.OnItemConsumed += OnItemUsed;
        RefreshUI();
    }

    void Update()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i < _inventory.Slots.Count)
                _inventory.ConsumeItem(_inventory.Slots[i].item, playerController);
        }
    }

    void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.OnInventoryChanged -= RefreshUI;
            _inventory.OnItemConsumed -= OnItemUsed;
        }
    }

    void RefreshUI()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < _inventory.Slots.Count)
            {
                var slot = _inventory.Slots[i];
                slots[i].icon.sprite = slot.item.icon;
                slots[i].icon.enabled = true;
                slots[i].quantityText.text = slot.quantity > 1 ? slot.quantity.ToString() : "";
                slots[i].slotHighlight.SetActive(slot.item.itemType == ItemType.Consumable);
            }
            else
            {
                slots[i].icon.sprite = null;
                slots[i].icon.enabled = false;
                slots[i].quantityText.text = "";
                slots[i].slotHighlight.SetActive(false);
            }
        }
    }

    void OnItemUsed(ItemData item)
    {
        // Optional: flash the slot or play a use animation
        Debug.Log($"[InventoryUI] Used: {item.itemName}");
    }
}

[System.Serializable]
public class InventorySlotUI
{
    public Image icon;
    public TextMeshProUGUI quantityText;
    public GameObject slotHighlight;  // border/glow for consumables
    public int slotNumber;  // 1-5, press to use item
}