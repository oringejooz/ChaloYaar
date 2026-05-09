using UnityEngine;

public class TapInteractable : Interactable
{
    [Header("Tap")]
    public VanSystemsHub vanHub;
    public float thirstRestored = 20f;
    public float waterDrained = 5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip drinkingClip;

    public override string InteractPrompt => "Press E to drink water";

    public override bool CanInteract(PlayerController player)
    {
        if (vanHub == null) return false;
        if (vanHub.water.current < waterDrained) return false;
        return !player.Stats.GetNormalized(StatType.Thirst).Equals(1f);
    }

    public override void Interact(PlayerController player)
    {
        vanHub.water.Drain(waterDrained);
        player.Stats.ApplyStat(StatType.Thirst, thirstRestored);
        audioSource?.PlayOneShot(drinkingClip);
        Debug.Log($"[Tap] Player drank water. Van water: {vanHub.water.current}");
    }
}