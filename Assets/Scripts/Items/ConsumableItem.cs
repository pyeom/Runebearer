using UnityEngine;

public enum ConsumableEffect
{
    HealHp,
    GainExperience,
    BoostStrength,
    BoostDexterity,
    BoostConstitution,
    BoostIntelligence,
    BoostWisdom,
    BoostCharisma,
}

/// <summary>
/// Consumable item (potions, scrolls, food).
/// Applying the item calls Use() which triggers the chosen effect on the character.
/// Consumables are stackable by default.
/// </summary>
[CreateAssetMenu(fileName = "NewConsumable", menuName = "Runebearer/Items/Consumable")]
public class ConsumableItem : Item
{
    [Header("Effect")]
    public ConsumableEffect effect   = ConsumableEffect.HealHp;
    [Tooltip("Magnitude of the effect (HP restored, XP gained, stat increase, etc.)")]
    public int              value    = 10;

    void Awake()
    {
        type      = ItemType.Consumable;
        stackable = true;
        maxStack  = 99;
    }

    public override void Use(CharacterStats user)
    {
        switch (effect)
        {
            case ConsumableEffect.HealHp:
                user.Heal(value);
                Debug.Log($"{user.name} drank {itemName} and recovered {value} HP.");
                break;

            case ConsumableEffect.GainExperience:
                user.GainExperience(value);
                Debug.Log($"{user.name} used {itemName} and gained {value} XP.");
                break;

            case ConsumableEffect.BoostStrength:
                user.strength = Mathf.Clamp(user.strength + value, 1, 30);
                Debug.Log($"{user.name} STR increased by {value}.");
                break;

            case ConsumableEffect.BoostDexterity:
                user.dexterity = Mathf.Clamp(user.dexterity + value, 1, 30);
                Debug.Log($"{user.name} DEX increased by {value}.");
                break;

            case ConsumableEffect.BoostConstitution:
                user.constitution = Mathf.Clamp(user.constitution + value, 1, 30);
                // Recalculate and top up HP for the new constitution
                user.Heal(CharacterStats.Modifier(value) * user.level);
                Debug.Log($"{user.name} CON increased by {value}.");
                break;

            case ConsumableEffect.BoostIntelligence:
                user.intelligence = Mathf.Clamp(user.intelligence + value, 1, 30);
                Debug.Log($"{user.name} INT increased by {value}.");
                break;

            case ConsumableEffect.BoostWisdom:
                user.wisdom = Mathf.Clamp(user.wisdom + value, 1, 30);
                Debug.Log($"{user.name} WIS increased by {value}.");
                break;

            case ConsumableEffect.BoostCharisma:
                user.charisma = Mathf.Clamp(user.charisma + value, 1, 30);
                Debug.Log($"{user.name} CHA increased by {value}.");
                break;
        }
    }
}
