// ── Player Controller Facade ──────────────────────────────────────────────────
// Aggregates all player components so interactables have a single reference.
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerNeeds))]
[RequireComponent(typeof(PlayerClass))]
[RequireComponent(typeof(PlayerMovementAdvanced))]
public class PlayerController : MonoBehaviour
{
    [HideInInspector] public PlayerStats Stats;
    [HideInInspector] public PlayerNeeds Needs;
    [HideInInspector] public PlayerClass Class;
    [HideInInspector] public PlayerMovementAdvanced Movement;
    [HideInInspector] public Inventory Inventory;

    [Header("Interaction")]
    public InteractionSystem interactionSystem;  // on Camera child

    void Awake()
    {
        Stats = GetComponent<PlayerStats>();
        Needs = GetComponent<PlayerNeeds>();
        Class = GetComponent<PlayerClass>();
        Movement = GetComponent<PlayerMovementAdvanced>();
        Inventory = GetComponent<Inventory>();
    }

    void Start()
    {
        if (interactionSystem != null)
            interactionSystem.playerController = this;
    }

    /// <summary>Returns true if this player is the local (human-controlled) player.</summary>
    public bool IsLocalPlayer => true; // swap for Netcode ownership check later
}