using UnityEngine;

// ── Interaction System ────────────────────────────────────────────────────────
// Attach to the Camera GameObject (same as SimpleFPSController)
public class InteractionSystem : MonoBehaviour
{
    [Header("Raycast")]
    public float raycastDistance = 3f;
    public LayerMask interactableLayer;

    [Header("References")]
    [Tooltip("The local PlayerController — set at runtime by PlayerController.Start")]
    public PlayerController playerController;

    // ── Events (subscribe in HUDManager) ──────────────────────────────────────
    public System.Action<string> OnFocusPromptChanged;  // null = hide prompt
    public System.Action OnInteracted;

    // ── Private ───────────────────────────────────────────────────────────────
    private IInteractable _currentTarget;

    void Update()
    {
        // TEMP DEBUG — will show if E key works and if raycast hits anything
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[InteractionSystem] E pressed. Current target: {(_currentTarget != null ? _currentTarget.InteractPrompt : "NULL")}");

            Ray debugRay = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(debugRay, out RaycastHit debugHit, raycastDistance))
                Debug.Log($"[InteractionSystem] Raycast hit: {debugHit.collider.name} | Layer: {LayerMask.LayerToName(debugHit.collider.gameObject.layer)}");
            else
                Debug.Log($"[InteractionSystem] Raycast hit NOTHING within {raycastDistance}m");
        }

        CheckFocus();

        if (_currentTarget != null && Input.GetKeyDown(KeyCode.E))
            TryInteract();
    }

    void CheckFocus()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, interactableLayer);

        if (hit)
        {
            // Check the hit object AND its parents
            IInteractable target = hitInfo.collider.GetComponent<IInteractable>()
                                ?? hitInfo.collider.GetComponentInParent<IInteractable>();

            if (target != null)
            {
                if (target != _currentTarget)
                {
                    _currentTarget?.OnFocusExit();
                    _currentTarget = target;
                    _currentTarget.OnFocusEnter();
                }

                bool can = _currentTarget.CanInteract(playerController);
                OnFocusPromptChanged?.Invoke(can ? _currentTarget.InteractPrompt : $"[Can't] {_currentTarget.InteractPrompt}");
            }
            else
            {
                ClearTarget();
            }
        }
        else
        {
            ClearTarget();
        }
    }

    void ClearTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget.OnFocusExit();
            _currentTarget = null;
            OnFocusPromptChanged?.Invoke(null);
        }
    }

    void TryInteract()
    {
        if (!_currentTarget.CanInteract(playerController))
        {
            Debug.Log("[Interaction] Cannot interact right now.");
            return;
        }

        _currentTarget.Interact(playerController);
        OnInteracted?.Invoke();
    }
}
