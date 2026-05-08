// ── Sink ──────────────────────────────────────────────────────────────────────
using UnityEngine;

public class SinkInteractable : Interactable
{
    [Header("Sink")]
    [Tooltip("How much cleanliness (if tracked later) restored per use")]
    public float cleanAmount = 30f;

    public override string InteractPrompt => "Press E to wash hands";

    public override void Interact(PlayerController player)
    {
        // Placeholder: in v2 add a Hygiene stat
        // For now just play an animation / sound
        Debug.Log("[Sink] Player washed hands.");
        // player.Stats.ApplyStat(StatType.Hygiene, cleanAmount);
    }
}