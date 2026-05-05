using UnityEngine;

public class SimpleFPSController : MonoBehaviour
{
    [Header("References")]
    public Transform playerBody;
    public Transform headBone;

    [Header("Sensitivity")]
    public float mouseSensitivity = 2f;
    public bool invertY = false;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float smoothing = 0.05f;

    [Header("Vertical Clamp")]
    public float minCameraPitch = -80f;
    public float maxCameraPitch = 80f;

    [Header("Head Look Limits")]
    public float maxYaw = 30f;   // left/right
    public float maxPitch = 30f; // up/down (separate from camera clamp)

    public Vector3 cameraOffset = new Vector3(0f, 0f, 0f);
    float yawOffset = 0f;
    float xRotation = 0f;
    Vector2 currentDelta;
    Vector2 smoothDelta;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        HandleMouseLook();
        FollowHeadPosition();
    }

    void HandleMouseLook()
    {
        float rawX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float rawY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (invertY) rawY = -rawY;

        currentDelta = new Vector2(rawX, rawY);
        smoothDelta = Vector2.Lerp(smoothDelta, currentDelta, 1f - smoothing);

        // ── HEAD YAW (left/right freelook) ──
        yawOffset += smoothDelta.x;
        yawOffset = Mathf.Clamp(yawOffset, -maxYaw, maxYaw);

        // if exceeded limit → rotate body instead
        if (Mathf.Abs(yawOffset) >= maxYaw)
        {
            float extra = smoothDelta.x;
            playerBody.Rotate(Vector3.up * extra);
        }

        // ── HEAD PITCH (up/down) ──
        xRotation -= smoothDelta.y;
        xRotation = Mathf.Clamp(xRotation, -maxPitch, maxPitch);

        // apply to camera (visual look)
        transform.localRotation = Quaternion.Euler(xRotation, yawOffset, 0f);
    }

    void FollowHeadPosition()
    {
        if (headBone == null) return;

        // apply offset in head's local space
        Vector3 offsetWorld =
            headBone.right * cameraOffset.x +
            headBone.up * cameraOffset.y +
            headBone.forward * cameraOffset.z;

        transform.position = headBone.position + offsetWorld;

        // keep your head rotation logic
        headBone.localRotation = Quaternion.Euler(xRotation, yawOffset, 0f);
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}