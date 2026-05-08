// ── Perk Enum ─────────────────────────────────────────────────────────────────
using UnityEngine;

public enum CharacterPerk
{
    None,
    IronStomach,
    Photographer,
    NightOwl,
    Mechanic,
    Chef,
    MapReader,
}

// ── ScriptableObject ──────────────────────────────────────────────────────────
[CreateAssetMenu(menuName = "Chalo Yaar/Character Data", fileName = "NewCharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Character";
    public bool isFemale;

    [Header("Portrait / Avatar")]
    public Sprite portrait;
    public GameObject prefab;

    [Header("Perks (up to 2)")]
    public CharacterPerk[] perks = new CharacterPerk[1];

    [Header("Stat Start Multipliers (1 = default)")]
    [Range(0.5f, 1.5f)] public float startingHungerMult = 1f;
    [Range(0.5f, 1.5f)] public float startingThirstMult = 1f;
    [Range(0.5f, 1.5f)] public float startingStaminaMult = 1f;

    [Header("Decay Rate Multipliers (1 = default, <1 = slower)")]
    [Range(0.5f, 1.5f)] public float hungerDecayMult = 1f;
    [Range(0.5f, 1.5f)] public float thirstDecayMult = 1f;
    [Range(0.5f, 1.5f)] public float staminaDrainMult = 1f;
    [Range(0.5f, 1.5f)] public float staminaRecoverMult = 1f;
    [Range(0.5f, 1.5f)] public float drowsinessGainMult = 1f;

    [Header("Movement Multipliers (1 = default)")]
    [Range(0.7f, 1.5f)] public float walkSpeedMult = 1f;
    [Range(0.7f, 1.5f)] public float sprintSpeedMult = 1f;
}
