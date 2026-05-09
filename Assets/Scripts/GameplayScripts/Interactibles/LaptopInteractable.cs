using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  LaptopInteractable.cs
//  ───────────────────────────────────────────────────────────────────────────
//  Place on a laptop. Press E to sell ALL photos in inventory.
//  Shows a nice summary panel of what you sold, no animations.
// ─────────────────────────────────────────────────────────────────────────────

public class LaptopInteractable : Interactable
{
    [Header("Settings")]
    [Tooltip("Item type that identifies photo items (usually QuestItem)")]
    public ItemType photoItemType = ItemType.QuestItem;

    [Header("Audio")]
    public AudioClip sellSound;
    public AudioSource audioSource;

    [Header("UI Feedback")]
    public GameObject sellSummaryPanel;
    public TMPro.TextMeshProUGUI summaryText;
    public float summaryDisplayTime = 3f;

    public override string InteractPrompt => "Press E to sell all photos";

    public override bool CanInteract(PlayerController player)
    {
        return HasAnyPhoto(player.Inventory);
    }

    public override void Interact(PlayerController player)
    {
        SellAllPhotos(player);
    }

    private bool HasAnyPhoto(Inventory inventory)
    {
        foreach (var slot in inventory.Slots)
        {
            if (slot.item != null && slot.item.itemType == photoItemType && slot.quantity > 0)
                return true;
        }
        return false;
    }

    private void SellAllPhotos(PlayerController player)
    {
        Inventory inventory = player.Inventory;
        List<PhotoSaleInfo> soldPhotos = new List<PhotoSaleInfo>();
        int totalEarned = 0;

        // Collect all photos first
        for (int i = inventory.Slots.Count - 1; i >= 0; i--)
        {
            var slot = inventory.Slots[i];

            if (slot.item != null && slot.item.itemType == photoItemType)
            {
                int value = slot.quantity * slot.item.sellPrice;

                soldPhotos.Add(new PhotoSaleInfo
                {
                    itemName = slot.item.itemName,
                    quantity = slot.quantity,
                    totalValue = value
                });

                totalEarned += value;

                // Remove all of this photo type
                inventory.RemoveItem(slot.item, slot.quantity);
            }
        }

        if (soldPhotos.Count == 0) return;

        // Add money
        if (MoneySystem.Instance != null)
            MoneySystem.Instance.Earn(totalEarned);

        // Play sound
        if (audioSource != null && sellSound != null)
            audioSource.PlayOneShot(sellSound);

        // Show summary
        ShowSellSummary(soldPhotos, totalEarned);

        Debug.Log($"[Laptop] Sold {soldPhotos.Count} photo types for {totalEarned}");
    }

    private void ShowSellSummary(List<PhotoSaleInfo> soldPhotos, int total)
    {
        if (sellSummaryPanel != null && summaryText != null)
        {
            string summary = "<b>Photos Sold!</b>\n\n";
            foreach (var photo in soldPhotos)
            {
                summary += $"{photo.itemName} x{photo.quantity} = {photo.totalValue}\n";
            }
            summary += $"\n<b>Total: {total}</b>";

            summaryText.text = summary;
            sellSummaryPanel.SetActive(true);

            // Auto-hide after delay
            Invoke(nameof(HideSummary), summaryDisplayTime);
        }
        else
        {
            // Fallback to HUD alert if no panel assigned
            HUDManager hud = FindObjectOfType<HUDManager>();
            if (hud != null)
                hud.ShowAlert($"Sold photos! +{total}");
        }
    }

    private void HideSummary()
    {
        if (sellSummaryPanel != null)
            sellSummaryPanel.SetActive(false);
    }

    // Helper class for summary data
    private class PhotoSaleInfo
    {
        public string itemName;
        public int quantity;
        public int totalValue;
    }
}