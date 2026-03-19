using UnityEngine;

public enum ArmorSlot { Head, Chest, Legs, Feet, Hands, Offhand, Ring, Amulet }
public enum ArmorType { None, Light, Medium, Heavy, Shield }

/// <summary>
/// Armor / accessory item. Equipping moves it from inventory to the matching
/// slot on the Equipment component.
/// </summary>
[CreateAssetMenu(fileName = "NewArmor", menuName = "Runebearer/Items/Armor")]
public class ArmorItem : Item
{
    [Header("Armor")]
    public ArmorSlot slot             = ArmorSlot.Chest;
    public ArmorType armorType        = ArmorType.Light;

    [Header("Defense")]
    public int armorBonus             = 2;   // added to base AC
    public int maxDexBonus            = 99;  // heavy armor often caps this at 0
    public int damageReduction        = 0;   // flat DR applied before HP loss

    [Header("Requirements")]
    public int requiredStrength       = 0;

    void Awake()
    {
        type      = ItemType.Armor;
        stackable = false;
        maxStack  = 1;
    }

    /// <summary>
    /// Calculates the AC this piece contributes given the wearer's DEX.
    /// </summary>
    public int CalculateAC(CharacterStats wearer)
    {
        int dexMod = Mathf.Min(CharacterStats.Modifier(wearer.dexterity), maxDexBonus);
        return 10 + armorBonus + dexMod;
    }

    public override void Use(CharacterStats user)
    {
        var equip = user.GetComponent<Equipment>();
        if (equip != null)
        {
            equip.Equip(this);
            Debug.Log($"{user.name} equipped {itemName} (+{armorBonus} AC)");
        }
    }
}
