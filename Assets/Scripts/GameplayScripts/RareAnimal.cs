// ── Rare Animal Component ─────────────────────────────────────────────────────
using UnityEngine;

public class RareAnimal : MonoBehaviour
{
    [Header("Animal Data")]
    public string animalName = "Spotted Deer";
    public int basePhotoValue = 150;   // rupees for perfect shot
    public Rarity rarity = Rarity.Common;

    [Header("Photo Item")]
    [Tooltip("ItemData with ItemType = QuestItem representing the photo")]
    public ItemData photoItem;

    [Header("Behaviour")]
    [Tooltip("Animal flees when player is closer than this")]
    public float fleeDistance = 8f;

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
        // Simple flee: move away. Replace with NavMesh in Phase 4.
        Vector3 away = (transform.position - _player.position).normalized;
        transform.position += away * 3f * Time.deltaTime;
    }
}