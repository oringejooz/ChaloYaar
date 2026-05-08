using UnityEngine;

/// <summary>
/// Attach to the driver seat / door GameObject (on the Interactable layer).
/// The player looks at it, sees the prompt, and presses E to enter the van.
/// </summary>
public class DriverSeatInteractable : Interactable
{
    [Header("Driver Seat")]
    public CarController carController;

    public override string InteractPrompt => "Press E to enter van";

    public override bool CanInteract(PlayerController player)
        => carController != null && !carController.IsInCar;

    public override void Interact(PlayerController player)
        => carController.EnterCar();
}