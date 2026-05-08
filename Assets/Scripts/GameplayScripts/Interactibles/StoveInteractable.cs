// ── Stove ─────────────────────────────────────────────────────────────────────
using UnityEngine;

public class StoveInteractable : Interactable
{
    [Header("Stove")]
    public VanSystemsHub vanHub;

    [Tooltip("Seconds to cook one meal")]
    public float cookTime = 10f;

    [Tooltip("Cooked item to add to player inventory")]
    public ItemData cookedItem;

    private bool _isCooking;
    private float _cookTimer;

    public override string InteractPrompt =>
        _isCooking ? $"Cooking… ({Mathf.CeilToInt(_cookTimer)}s)" : "Press E to Cook";

    public override bool CanInteract(PlayerController player)
        => !_isCooking && vanHub != null && vanHub.gas.current > 0;

    public override void Interact(PlayerController player)
    {
        if (_isCooking) return;
        _isCooking = true;
        _cookTimer = cookTime;
        vanHub.UseStove();
        StartCoroutine(CookRoutine(player));
    }

    System.Collections.IEnumerator CookRoutine(PlayerController player)
    {
        while (_cookTimer > 0f)
        {
            _cookTimer -= Time.deltaTime;
            yield return null;
        }
        _isCooking = false;
        if (cookedItem != null)
        {
            player.Inventory.AddItem(cookedItem, 1);
            Debug.Log($"[Stove] Cooked: {cookedItem.itemName}");
        }

        // Chef perk bonus handled in CharacterPerk.Chef branch of PlayerClass
    }
}