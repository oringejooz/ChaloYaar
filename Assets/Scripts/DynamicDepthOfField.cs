using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dynamic Depth of Field for Unity 2022.3 URP
/// -----------------------------------------------
/// Attach this script to your main Camera GameObject.
///
/// SETUP CHECKLIST:
///   1. Add a Global Volume to your scene (GameObject > Volume > Global Volume).
///   2. Create a new Volume Profile and add a "Depth Of Field" override to it.
///   3. Enable "Focus Distance", "Focal Length", and "Aperture" checkboxes inside the override.
///   4. Assign that Volume to the 'postProcessVolume' field below (or let auto-find do it).
///   5. Make sure "Post Processing" is enabled on your URP Camera component.
///   6. In your URP Renderer (UniversalRenderPipelineAsset_Renderer), confirm
///      "Depth Texture" is enabled on the URP Asset (required for accurate depth).
/// </summary>
[RequireComponent(typeof(Camera))]
public class DynamicDepthOfField : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Inspector Settings
    // ──────────────────────────────────────────────

    [Header("Volume Reference")]
    [Tooltip("Drag your Global/Local Volume here. Leave empty to auto-find.")]
    public Volume postProcessVolume;

    [Header("Raycast Settings")]
    [Tooltip("Maximum distance the focus rays will travel.")]
    public float maxFocusDistance = 50f;

    [Tooltip("Layer mask — only these layers affect focus. Use 'Everything' to focus on anything.")]
    public LayerMask focusLayerMask = ~0;

    [Tooltip("Offset added to the hit distance. Useful to pull focus slightly in front of a surface.")]
    public float focusOffset = 0f;

    [Header("Cone Settings")]
    [Tooltip("Half-angle of the cone in degrees. 3-8 feels natural.")]
    [Range(0.5f, 45f)]
    public float coneAngle = 5f;

    [Tooltip("Number of rays spread around the cone. More = smoother average.")]
    [Range(1, 32)]
    public int coneRayCount = 8;

    [Tooltip("If true, rays that miss are excluded from the average. If false, misses count as maxFocusDistance.")]
    public bool ignoreMisses = true;

    [Header("Focus Smoothing")]
    [Tooltip("How quickly the focus distance lerps to the target during normal use.")]
    [Range(0.5f, 20f)]
    public float focusSpeed = 5f;

    [Tooltip("Smooth the aperture change as well.")]
    [Range(0.5f, 20f)]
    public float apertureSpeed = 3f;

    [Header("Close Focus")]
    [Tooltip("If the averaged hit distance is below this, close focus speed kicks in.")]
    public float closeDistanceThreshold = 2f;

    [Tooltip("Focus speed used when the subject is within closeDistanceThreshold. " +
             "Higher than focusSpeed = snappier close-up lock.")]
    [Range(0.5f, 40f)]
    public float closeFocusSpeed = 15f;

    [Header("Depth of Field Values")]
    [Tooltip("Focal length in mm when something is in focus up close.")]
    [Range(1f, 300f)]
    public float focalLengthNear = 50f;

    [Tooltip("Focal length in mm when focusing on a distant object.")]
    [Range(1f, 300f)]
    public float focalLengthFar = 85f;

    [Tooltip("Aperture (f-stop) when subject is close — shallower DoF.")]
    [Range(1f, 32f)]
    public float apertureClose = 1.8f;

    [Tooltip("Aperture (f-stop) when subject is far — deeper DoF.")]
    [Range(1f, 32f)]
    public float apertureFar = 8f;

    [Header("Limits")]
    [Tooltip("Minimum allowed focus distance (prevents focus from snapping to zero).")]
    public float minFocusDistance = 0.3f;

    [Header("Debug")]
    [Tooltip("Draw rays in the Scene view showing what the camera is focused on.")]
    public bool drawDebugRay = true;

    // ──────────────────────────────────────────────
    //  Private State
    // ──────────────────────────────────────────────

    private DepthOfField _dof;
    private Camera _cam;

    private float _currentFocusDistance;
    private float _targetFocusDistance;

    // ──────────────────────────────────────────────
    //  Unity Messages
    // ──────────────────────────────────────────────

    private void Awake()
    {
        _cam = GetComponent<Camera>();

        // Auto-find a volume in the scene if none assigned
        if (postProcessVolume == null)
        {
            postProcessVolume = FindObjectOfType<Volume>();
            if (postProcessVolume == null)
            {
                Debug.LogError("[DynamicDoF] No Volume found in the scene. " +
                               "Please add a Global Volume with a Depth of Field override.");
                enabled = false;
                return;
            }
        }

        // Grab or add DepthOfField component from the profile
        if (!postProcessVolume.profile.TryGet(out _dof))
        {
            _dof = postProcessVolume.profile.Add<DepthOfField>(true);
            Debug.LogWarning("[DynamicDoF] No Depth of Field override found on the Volume Profile. " +
                             "One was added automatically.");
        }

        // Make sure the DoF override is active
        _dof.active = true;

        // Ensure the parameters we'll drive are overridden
        _dof.focusDistance.overrideState = true;
        _dof.focalLength.overrideState = true;
        _dof.aperture.overrideState = true;

        // Bootstrap current values from the camera's near clip
        _currentFocusDistance = Mathf.Max(_cam.nearClipPlane + 0.5f, minFocusDistance);
        _targetFocusDistance = _currentFocusDistance;
    }

    private void Update()
    {
        UpdateTargetFocusDistance();
        SmoothApplyDoF();
    }

    // ──────────────────────────────────────────────
    //  Core Logic
    // ──────────────────────────────────────────────

    /// <summary>
    /// Shoots a cone of rays from screen centre, averages hit distances,
    /// and uses that as the focus target.
    /// </summary>
    private void UpdateTargetFocusDistance()
    {
        Transform camT = _cam.transform;
        Vector3 origin = camT.position;
        Vector3 forward = camT.forward;

        float totalDist = 0f;
        int hitCount = 0;

        // Centre ray — always included, weighted x2
        Ray centreRay = new Ray(origin, forward);
        if (Physics.Raycast(centreRay, out RaycastHit centreHit, maxFocusDistance, focusLayerMask))
        {
            totalDist += centreHit.distance * 2f;
            hitCount += 2; // counts as 2 so it has double weight in the average
            if (drawDebugRay) Debug.DrawLine(origin, centreHit.point, Color.cyan);
        }
        else
        {
            if (!ignoreMisses) { totalDist += maxFocusDistance * 2f; hitCount += 2; }
            if (drawDebugRay) Debug.DrawRay(origin, forward * maxFocusDistance, Color.yellow);
        }

        // Cone rays — spread using golden angle for even distribution
        float goldenAngle = 137.5077f;
        for (int i = 0; i < coneRayCount; i++)
        {
            float elevation = coneAngle * Mathf.Sqrt((float)(i + 1) / coneRayCount);
            float azimuth = goldenAngle * i;

            Quaternion rot = Quaternion.AngleAxis(azimuth, forward) *
                             Quaternion.AngleAxis(elevation, camT.right);
            Vector3 dir = rot * forward;

            Ray ray = new Ray(origin, dir);
            if (Physics.Raycast(ray, out RaycastHit hit, maxFocusDistance, focusLayerMask))
            {
                totalDist += hit.distance;
                hitCount += 1;
                if (drawDebugRay) Debug.DrawLine(origin, hit.point, Color.green);
            }
            else
            {
                if (!ignoreMisses) { totalDist += maxFocusDistance; hitCount += 1; }
                if (drawDebugRay) Debug.DrawRay(origin, dir * maxFocusDistance, Color.grey);
            }
        }

        if (hitCount > 0)
            _targetFocusDistance = Mathf.Max((totalDist / hitCount) + focusOffset, minFocusDistance);
        else
            _targetFocusDistance = maxFocusDistance; // everything missed
    }

    /// <summary>
    /// Lerps the current DoF values towards their targets and writes them to the Volume.
    /// Uses closeFocusSpeed when the target is within closeDistanceThreshold.
    /// </summary>
    private void SmoothApplyDoF()
    {
        // Pick which speed to use based on how close the target is
        float activeSpeed = (_targetFocusDistance <= closeDistanceThreshold)
            ? closeFocusSpeed
            : focusSpeed;

        // Smooth the focus distance
        _currentFocusDistance = Mathf.Lerp(
            _currentFocusDistance,
            _targetFocusDistance,
            Time.deltaTime * activeSpeed
        );

        // Derive a 0–1 blend ratio based on how far the focus is
        float t = Mathf.InverseLerp(minFocusDistance, maxFocusDistance, _currentFocusDistance);

        // Focal length grows as focus distance increases (telephoto feel at distance)
        float targetFocalLength = Mathf.Lerp(focalLengthNear, focalLengthFar, t);

        // Aperture widens (smaller f-stop number) at close range — shallower DoF
        float targetAperture = Mathf.Lerp(apertureClose, apertureFar, t);

        // Smooth the aperture separately so it doesn't snap
        float currentAperture = _dof.aperture.value;
        float smoothAperture = Mathf.Lerp(currentAperture, targetAperture, Time.deltaTime * apertureSpeed);

        // Write to the Volume Profile
        _dof.focusDistance.value = _currentFocusDistance;
        _dof.focalLength.value = targetFocalLength;
        _dof.aperture.value = smoothAperture;
    }

    // ──────────────────────────────────────────────
    //  Public API  (call these from other scripts)
    // ──────────────────────────────────────────────

    /// <summary>Instantly snap focus to a specific distance (no lerp).</summary>
    public void SnapFocusToDistance(float distance)
    {
        _targetFocusDistance = Mathf.Max(distance, minFocusDistance);
        _currentFocusDistance = _targetFocusDistance;
        _dof.focusDistance.value = _currentFocusDistance;
    }

    /// <summary>Override the focus target to a world-space Transform for one frame.</summary>
    public void FocusOnTarget(Transform target)
    {
        if (target == null) return;
        float dist = Vector3.Distance(_cam.transform.position, target.position);
        _targetFocusDistance = Mathf.Max(dist, minFocusDistance);
    }

    /// <summary>Enable or disable the DoF effect at runtime.</summary>
    public void SetEnabled(bool state)
    {
        _dof.active = state;
    }
}