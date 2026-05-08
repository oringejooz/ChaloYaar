// ── Trash Bin ─────────────────────────────────────────────────────────────────
using UnityEngine;

public class TrashBinInteractable : Interactable
{
    [Header("Trash Bin")]
    public VanSystemsHub vanHub;

    [Tooltip("Van must be stopped to empty trash")]
    public bool requireVanStopped = true;

    public override string InteractPrompt => "Press E to empty Trash Bin";

    public override bool CanInteract(PlayerController player)
    {
        if (vanHub == null) return false;
        if (requireVanStopped && vanHub.isVanMoving) return false;
        return vanHub.trash.current > 0;
    }

    public override void Interact(PlayerController player)
    {
        vanHub.EmptyTrash();
        Debug.Log("[TrashBin] Trash emptied.");
    }
}