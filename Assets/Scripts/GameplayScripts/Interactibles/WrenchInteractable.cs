using UnityEngine;

public class WrenchInteractable : Interactable
{
    [Header("Wrench")]
    public VanSystemsHub vanHub;
    public float repairAmount = 25f;
    public int repairCost = 50;
    public bool requireVanStopped = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip repairClip;

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
        int cost = repairCost;
        if (player.Class.characterData != null)
            foreach (var perk in player.Class.characterData.perks)
                if (perk == CharacterPerk.Mechanic) { cost = Mathf.RoundToInt(cost * 0.5f); break; }

        if (!MoneySystem.Instance.Spend(cost)) return;
        vanHub.RepairVan(repairAmount);
        audioSource?.PlayOneShot(repairClip);
        Debug.Log($"[Wrench] Van repaired by {repairAmount}. Cost ₹{cost}.");
    }
}