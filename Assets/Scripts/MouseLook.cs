using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("References")]
    public Transform playerBody;

    [Header("Sensitivity")]
    public float mouseSensitivity = 2f;       // Lower, more sensible default
    public bool invertY = false;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float smoothing = 0.05f;           // 0 = no smoothing, higher = more lag

    [Header("Vertical Clamp")]
    public float minPitch = -80f;
    public float maxPitch = 80f;

    // Internal state
    float xRotation = 0f;
    Vector2 currentDelta;
    Vector2 smoothDelta;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Use GetAxisRaw-style reading without Time.deltaTime
        // Mouse axes are already in pixels-per-frame; deltaTime makes it frame-rate dependent
        float rawX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float rawY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (invertY) rawY = -rawY;

        // Exponential smoothing to reduce jitter / nausea
        currentDelta = new Vector2(rawX, rawY);
        smoothDelta = Vector2.Lerp(smoothDelta, currentDelta, 1f - smoothing);

        // Vertical rotation (camera pitch)
        xRotation -= smoothDelta.y;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation (player body yaw)
        playerBody.Rotate(Vector3.up * smoothDelta.x);
    }

    // Optional: call this from a pause menu to unlock the cursor
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}