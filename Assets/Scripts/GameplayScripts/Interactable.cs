using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  IInteractable.cs  +  InteractionSystem.cs  +  Interactable.cs
//  Chalo Yaar! — Unified first-person interaction framework.
//
//  How it works:
//    • InteractionSystem sits on the Camera (same GO as SimpleFPSController).
//    • Every frame it raycasts forward; if it hits an IInteractable it shows
//      the interaction prompt and fires Interact() on button press.
//    • Interactable is a convenient abstract MonoBehaviour base to inherit from.
//    • Specific interactables (ConsumablePickup, VanSystem, etc.) extend it.
//
//  Button: "Interact" — add to Input Manager as key E (or use new Input System).
// ─────────────────────────────────────────────────────────────────────────────

// ── Interface ─────────────────────────────────────────────────────────────────
public interface IInteractable
{
    string InteractPrompt { get; }         // e.g. "Press E to eat Apple"
    bool   CanInteract(PlayerController player);
    void   Interact(PlayerController player);
    void   OnFocusEnter();                 // cursor hovering over
    void   OnFocusExit();                  // cursor left
}

// ── Convenience Base MonoBehaviour ────────────────────────────────────────────
public abstract class Interactable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("Shown in the HUD when player looks at this object")]
    public string promptText = "Interact";

    [Tooltip("Distance the player must be within to interact")]
    public float interactRange = 2.5f;

    [Header("Highlight")]
    [Tooltip("Optional outline/glow component to enable on focus")]
    public Renderer[] highlightRenderers;
    public Material   highlightMaterial;
    private Material[] _originalMaterials;

    public virtual string InteractPrompt => promptText;

    public virtual bool CanInteract(PlayerController player) => true;

    public abstract void Interact(PlayerController player);

    public virtual void OnFocusEnter()
    {
        if (highlightRenderers == null) return;
        _originalMaterials = new Material[highlightRenderers.Length];
        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            _originalMaterials[i] = highlightRenderers[i].sharedMaterial;
            if (highlightMaterial != null)
                highlightRenderers[i].sharedMaterial = highlightMaterial;
        }
    }

    public virtual void OnFocusExit()
    {
        if (highlightRenderers == null || _originalMaterials == null) return;
        for (int i = 0; i < highlightRenderers.Length; i++)
            highlightRenderers[i].sharedMaterial = _originalMaterials[i];
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
#endif
}