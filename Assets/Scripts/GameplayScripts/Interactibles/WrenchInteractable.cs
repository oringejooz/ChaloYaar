// ── Wrench / Van Repair ───────────────────────────────────────────────────────
using UnityEngine;

public class WrenchInteractable : Interactable
{
    [Header("Wrench")]
    public VanSystemsHub vanHub;

    [Tooltip("Health restored per repair action")]
    public float repairAmount = 25f;

    [Tooltip("Cost in rupees per repair action")]
    public int repairCost = 50;

    [Tooltip("Van must be stopped to repair")]
    public bool requireVanStopped = true;

    public override string InteractPrompt => $"Press E to Repair Van (₹{repairCost})";

    public override bool CanInteract(PlayerController player)
    {
        if (vanHub == null) return false;
        if (requireVanStopped && vanHub.isVanMoving) return false;
        if (vanHub.health.IsFull) return false;
        return MoneySystem.Instance.CanAfford(repairCost);
    }

    public override void Interact(PlayerController player)
    {
        // Mechanic perk: 50% cost reduction
        int cost = repairCost;
        if (player.Class.characterData != null)
            foreach (var perk in player.Class.characterData.perks)
                if (perk == CharacterPerk.Mechanic) { cost = Mathf.RoundToInt(cost * 0.5f); break; }

        if (!MoneySystem.Instance.Spend(cost)) return;
        vanHub.RepairVan(repairAmount);
        Debug.Log($"[Wrench] Van repaired by {repairAmount}. Cost ₹{cost}.");
    }
}