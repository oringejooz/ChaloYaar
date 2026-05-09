using UnityEngine;
using TMPro;

public class PhotographySystem : MonoBehaviour
{
    [Header("Camera Settings")]
    public float photoRange = 20f;
    public float photoFOV = 40f;
    public LayerMask animalLayer;

    [Header("Cooldown")]
    public float shootCooldown = 1.5f;

    [Header("Canvas Switching")]
    public GameObject playerCanvas;
    public GameObject cameraCanvas;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip shutterClip;
    public AudioClip noSubjectClip;
    public AudioClip zoomInClip;
    public AudioClip zoomOutClip;
    public AudioClip cameraRaiseClip;
    public AudioClip cameraLowerClip;


    [Header("Photo Stats UI")]
    [Tooltip("TMP text element that shows photo results and alerts")]
    public TextMeshProUGUI photoStatsText;
    [Tooltip("How long the photo stats message stays on screen")]
    public float statDisplayDuration = 3f;

    [Header("Zoom")]
    public float defaultFOV = 60f;
    public float zoomedFOV = 25f;
    [Tooltip("How fast the camera lerps between FOVs")]
    public float zoomSpeed = 8f;

    [Header("Spawn Points (for Respawnable Animals)")]
    [Tooltip("Empty GameObjects used as potential relocation points")]
    public Transform[] animalSpawnPoints;

    public bool IsCameraActive { get; private set; }
    public bool IsZoomed { get; private set; }

    private float _shootTimer;
    private float _statDisplayTimer;
    private float _targetFOV;
    private PlayerController _player;
    private Camera _cam;

    public System.Action<PhotoRecord> OnPhotoTaken;

    void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        _cam = GetComponent<Camera>();
        _targetFOV = defaultFOV;

        if (cameraCanvas != null) cameraCanvas.SetActive(false);
        if (photoStatsText != null) photoStatsText.gameObject.SetActive(false);
    }

    void Update()
    {
        _shootTimer -= Time.deltaTime;

        // Hide photo stats text after duration
        if (_statDisplayTimer > 0f)
        {
            _statDisplayTimer -= Time.deltaTime;
            if (_statDisplayTimer <= 0f && photoStatsText != null)
                photoStatsText.gameObject.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (IsCameraActive) LowerCamera();
            else RaiseCamera();
        }

        if (IsCameraActive)
        {
            // Zoom toggle on right click
            if (Input.GetMouseButtonDown(1))
            {
                if (!IsZoomed)
                {
                    IsZoomed = true;
                    _targetFOV = zoomedFOV;
                    audioSource?.PlayOneShot(zoomInClip);
                }
                else
                {
                    IsZoomed = false;
                    _targetFOV = defaultFOV;
                    audioSource?.PlayOneShot(zoomOutClip);
                }
            }

            // Smoothly lerp camera FOV toward target
            if (_cam != null)
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetFOV, Time.deltaTime * zoomSpeed);

            if (Input.GetButtonDown("Fire1") && _shootTimer <= 0f)
                TryTakePhoto();
        }
    }

    public void RaiseCamera()
    {
        IsCameraActive = true;
        _targetFOV = defaultFOV;
        IsZoomed = false;
        if (_cam != null) _cam.fieldOfView = defaultFOV;
        if (playerCanvas != null) playerCanvas.SetActive(false);
        if (cameraCanvas != null) cameraCanvas.SetActive(true);
        audioSource?.PlayOneShot(cameraRaiseClip);  // ← add
    }

    public void LowerCamera()
    {
        IsCameraActive = false;
        IsZoomed = false;
        if (_cam != null) _cam.fieldOfView = defaultFOV;
        if (playerCanvas != null) playerCanvas.SetActive(true);
        if (cameraCanvas != null) cameraCanvas.SetActive(false);
        audioSource?.PlayOneShot(cameraLowerClip);  // ← add
    }

    // ── Display Helpers ───────────────────────────────────────────────────────

    void ShowPhotoStat(string message)
    {
        if (photoStatsText == null) return;
        photoStatsText.text = message;
        photoStatsText.gameObject.SetActive(true);
        _statDisplayTimer = statDisplayDuration;
    }

    // ── Photo Logic ───────────────────────────────────────────────────────────

    void TryTakePhoto()
    {
        _shootTimer = shootCooldown;

        Collider[] hits = Physics.OverlapSphere(transform.position, photoRange, animalLayer);
        RareAnimal bestAnimal = null;
        float bestAngle = photoFOV / 2f;

        foreach (var col in hits)
        {
            if (!col.TryGetComponent(out RareAnimal animal)) continue;

            Vector3 dir = (col.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dir);

            if (angle < bestAngle)
            {
                if (Physics.Linecast(transform.position, col.transform.position, out RaycastHit los)
                    && los.collider != col)
                    continue;

                bestAngle = angle;
                bestAnimal = animal;
            }
        }

        if (bestAnimal == null)
        {
            audioSource?.PlayOneShot(noSubjectClip);
            ShowPhotoStat("No animal in frame.");
            return;
        }

        float distScore = 1f - (Vector3.Distance(transform.position, bestAnimal.transform.position) / photoRange);
        float centerScore = 1f - (bestAngle / (photoFOV / 2f));
        float quality = Mathf.Clamp01((distScore + centerScore) * 0.5f);

        bool hasPhotographerPerk = false;
        if (_player?.Class?.characterData != null)
            foreach (var p in _player.Class.characterData.perks)
                if (p == CharacterPerk.Photographer) { hasPhotographerPerk = true; break; }

        int baseValue = Mathf.RoundToInt(bestAnimal.basePhotoValue * quality);
        int sellValue = hasPhotographerPerk ? Mathf.RoundToInt(baseValue * 1.2f) : baseValue;

        var record = new PhotoRecord
        {
            animalName = bestAnimal.animalName,
            quality = quality,
            sellValue = sellValue,
            timestamp = Time.time
        };

        if (bestAnimal.photoItem != null)
            _player?.Inventory.AddItem(bestAnimal.photoItem, 1);
        else
            Debug.LogWarning($"[Photography] {bestAnimal.animalName} has no photoItem assigned!");

        OnPhotoTaken?.Invoke(record);
        ObjectiveSystem.Instance?.NotifyPhotoTaken(bestAnimal.animalName);
        audioSource?.PlayOneShot(shutterClip);

        bestAnimal.photosTaken++;
        int remaining = bestAnimal.maxPhotos - bestAnimal.photosTaken;

        string qualityLabel = quality switch
        {
            >= 0.9f => "Perfect!",
            >= 0.75f => "Excellent",
            >= 0.55f => "Good",
            >= 0.35f => "Ok",
            _ => "Bad"
        };

        string statsMessage =
            $"{bestAnimal.animalName}\n" +
            $"Rarity: {bestAnimal.rarity}\n" +
            $"Quality: {qualityLabel}\n" +
            $"Value: ₹{sellValue}\n" +
            $"Photos left: {Mathf.Max(remaining, 0)}";

        ShowPhotoStat(statsMessage);

        if (bestAnimal.photosTaken >= bestAnimal.maxPhotos)
        {
            if (bestAnimal.isRespawnable && animalSpawnPoints != null && animalSpawnPoints.Length > 0)
            {
                Transform newSpawn = GetRandomSpawnPoint(bestAnimal.transform.position);
                bestAnimal.transform.position = newSpawn.position;
                bestAnimal.photosTaken = 0;
            }
            else
            {
                Destroy(bestAnimal.gameObject);
            }
        }
    }

    Transform GetRandomSpawnPoint(Vector3 currentPos)
    {
        Transform chosen = animalSpawnPoints[Random.Range(0, animalSpawnPoints.Length)];
        for (int i = 0; i < 5; i++)
        {
            Transform t = animalSpawnPoints[Random.Range(0, animalSpawnPoints.Length)];
            if (Vector3.Distance(t.position, currentPos) > 2f) { chosen = t; break; }
        }
        return chosen;
    }
}

// ── Supporting Types ──────────────────────────────────────────────────────────
public enum Rarity { Common, Uncommon, Rare, Legendary }

[System.Serializable]
public class PhotoRecord
{
    public string animalName;
    public float quality;
    public int sellValue;
    public float timestamp;
}