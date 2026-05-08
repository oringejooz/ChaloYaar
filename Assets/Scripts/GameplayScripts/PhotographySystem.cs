using UnityEngine;

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

    public bool IsCameraActive { get; private set; }

    private float _shootTimer;
    private PlayerController _player;
    private Camera _cam;

    public System.Action<PhotoRecord> OnPhotoTaken;

    void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        _cam = GetComponent<Camera>();

        if (cameraCanvas != null) cameraCanvas.SetActive(false);
    }

    void Update()
    {
        _shootTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (IsCameraActive)
                LowerCamera();
            else
                RaiseCamera();
        }

        if (IsCameraActive)
        {
            if (Input.GetButtonDown("Fire1") && _shootTimer <= 0f)
                TryTakePhoto();
        }
    }

    public void RaiseCamera()
    {
        IsCameraActive = true;
        if (playerCanvas != null) playerCanvas.SetActive(false);
        if (cameraCanvas != null) cameraCanvas.SetActive(true);
        Debug.Log("[Photography] Camera raised");
    }

    public void LowerCamera()
    {
        IsCameraActive = false;
        if (playerCanvas != null) playerCanvas.SetActive(true);
        if (cameraCanvas != null) cameraCanvas.SetActive(false);
        Debug.Log("[Photography] Camera lowered");
    }

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
            Debug.Log("[Photography] No animal in frame.");
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

        _player?.Inventory.AddItem(bestAnimal.photoItem, 1);
        OnPhotoTaken?.Invoke(record);
        audioSource?.PlayOneShot(shutterClip);

        Debug.Log($"[Photography] Shot {bestAnimal.animalName} | quality {quality:P0} | value ₹{sellValue}");
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