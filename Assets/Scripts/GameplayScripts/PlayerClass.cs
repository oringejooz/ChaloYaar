using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerStats))]
public class PlayerClass : MonoBehaviour
{
    [Header("Character")]
    public CharacterData characterData;

    private PlayerStats _stats;
    private PlayerMovementAdvanced _movement;
    private bool _perksApplied;

    public System.Action<CharacterData> OnCharacterLoaded;

    IEnumerator Start()
    {
        _stats = GetComponent<PlayerStats>();
        _movement = GetComponent<PlayerMovementAdvanced>();

        // Wait one frame for PlayerStats.Start() to initialize values
        yield return null;

        if (characterData != null)
            ApplyCharacter(characterData);
    }

    public void ApplyCharacter(CharacterData data)
    {
        if (_perksApplied)
        {
            Debug.LogWarning("[PlayerClass] Character already applied. Reload scene to switch.");
            return;
        }

        characterData = data;
        _perksApplied = true;

        // Apply stat start overrides to CURRENT values (not the start fields)
        _stats.ApplyStat(StatType.Hunger, _stats.Hunger * data.startingHungerMult - _stats.Hunger);
        _stats.ApplyStat(StatType.Thirst, _stats.Thirst * data.startingThirstMult - _stats.Thirst);
        _stats.ApplyStat(StatType.Stamina, _stats.Stamina * data.startingStaminaMult - _stats.Stamina);
        _stats.ApplyStat(StatType.Drowsiness, -_stats.Drowsiness); // reset drowsiness to 0

        // Apply decay rate modifiers
        _stats.hungerDecayRate *= data.hungerDecayMult;
        _stats.thirstDecayRate *= data.thirstDecayMult;
        _stats.staminaSprintDrain *= data.staminaDrainMult;
        _stats.staminaRecoverRate *= data.staminaRecoverMult;
        _stats.drowsinessGainRate *= data.drowsinessGainMult;

        // Apply movement modifiers
        if (_movement != null)
        {
            _movement.walkSpeed *= data.walkSpeedMult;
            _movement.sprintSpeed *= data.sprintSpeedMult;
        }

        ApplySpecialPerks(data);

        OnCharacterLoaded?.Invoke(data);
        Debug.Log($"[PlayerClass] Applied character: {data.characterName} | Health: {_stats.Health} | Stamina: {_stats.Stamina}");
    }

    void ApplySpecialPerks(CharacterData data)
    {
        foreach (var perk in data.perks)
        {
            switch (perk)
            {
                case CharacterPerk.IronStomach:
                    break;
                case CharacterPerk.Photographer:
                    break;
                case CharacterPerk.NightOwl:
                    _stats.drowsinessGainRate *= 0.6f;
                    break;
                case CharacterPerk.Mechanic:
                    break;
                case CharacterPerk.Chef:
                    break;
                case CharacterPerk.MapReader:
                    break;
            }
        }
    }

    public string GetPerkSummary()
    {
        if (characterData == null) return "No character selected";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{characterData.characterName}</b>");
        foreach (var p in characterData.perks)
            sb.AppendLine($"  • {PerkDescription(p)}");
        return sb.ToString();
    }

    static string PerkDescription(CharacterPerk p) => p switch
    {
        CharacterPerk.IronStomach => "Iron Stomach — no flatulence side effects",
        CharacterPerk.Photographer => "Photographer — 20% photo sell bonus",
        CharacterPerk.NightOwl => "Night Owl — drowsiness builds 40% slower",
        CharacterPerk.Mechanic => "Mechanic — van repairs cost 50% less",
        CharacterPerk.Chef => "Chef — cooked food gives 20% more nutrition",
        CharacterPerk.MapReader => "Map Reader — codriver sees extra route info",
        _ => p.ToString()
    };
}