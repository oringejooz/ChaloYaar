using UnityEngine;

public class RareAnimal : MonoBehaviour
{
    [Header("Animal Data")]
    public string animalName = "Spotted Deer";
    public int basePhotoValue = 150;
    public Rarity rarity = Rarity.Common;

    [Header("Photo Item")]
    [Tooltip("ItemData with ItemType = QuestItem representing the photo")]
    public ItemData photoItem;

    [Header("Photo Limit")]
    [Tooltip("How many photos can be taken before this animal despawns/relocates")]
    public int maxPhotos = 3;

    [Header("Respawn")]
    [Tooltip("If true, animal relocates to a random spawn point instead of despawning permanently")]
    public bool isRespawnable = false;

    [Header("Behaviour")]
    [Tooltip("Animal flees when player is closer than this")]
    public float fleeDistance = 8f;

    // Tracked by PhotographySystem — do not set manually
    [HideInInspector] public int photosTaken = 0;

    private Transform _player;

    void Start()
    {
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null) _player = pc.transform;
    }

    void Update()
    {
        if (_player == null) return;
        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist < fleeDistance) Flee();
    }

    void Flee()
    {
        Vector3 away = (transform.position - _player.position).normalized;
        transform.position += away * 3f * Time.deltaTime;
    }
}