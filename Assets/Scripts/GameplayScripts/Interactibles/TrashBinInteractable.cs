using UnityEngine;

public class TrashBinInteractable : Interactable
{
    [Header("Trash Bin")]
    public VanSystemsHub vanHub;
    public bool requireVanStopped = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip emptyTrashClip;

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
        audioSource?.PlayOneShot(emptyTrashClip);
        Debug.Log("[TrashBin] Trash emptied.");
    }
}