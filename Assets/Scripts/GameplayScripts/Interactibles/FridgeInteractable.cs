// ── Fridge ────────────────────────────────────────────────────────────────────
using UnityEngine;
public class FridgeInteractable : Interactable
{
    public override string InteractPrompt => "Press E to open Fridge";

    // Events — subscribe in InventoryUI to open the filtered panel
    public System.Action<PlayerController> OnFridgeOpened;

    public override void Interact(PlayerController player)
    {
        OnFridgeOpened?.Invoke(player);
        // InventoryUI listens and filters to ItemType.Consumable
    }
}