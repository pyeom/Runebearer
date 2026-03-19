using UnityEngine;

public enum WeaponType { Sword, Axe, Mace, Dagger, Staff, Wand, Bow, Crossbow }
public enum DamageType { Slashing, Piercing, Bludgeoning, Fire, Ice, Lightning, Arcane, Necrotic }

/// <summary>
/// Weapon item. Damage is expressed as XdY + bonus (e.g. 1d8+2).
/// Ranged weapons use dexterity instead of strength for the attack bonus.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Runebearer/Items/Weapon")]
public class WeaponItem : Item
{
    [Header("Weapon")]
    public WeaponType weaponType  = WeaponType.Sword;
    public DamageType damageType  = DamageType.Slashing;

    [Header("Damage (XdY + bonus)")]
    public int damageDiceCount    = 1;   // X
    public int damageDiceSides    = 6;   // Y
    public int damageBonus        = 0;   // flat bonus on top

    [Header("Properties")]
    public bool twoHanded         = false;
    public bool ranged            = false;
    public int  range             = 1;   // in grid cells

    void Awake()
    {
        type      = ItemType.Weapon;
        stackable = false;
        maxStack  = 1;
    }

    /// <summary>
    /// Roll weapon damage including the wielder's relevant attribute modifier.
    /// Ranged weapons use DEX; melee weapons use STR.
    /// </summary>
    public int RollDamage(CharacterStats attacker)
    {
        int total = 0;
        for (int i = 0; i < damageDiceCount; i++)
            total += Random.Range(1, damageDiceSides + 1);

        int attrMod = ranged
            ? CharacterStats.Modifier(attacker.dexterity)
            : CharacterStats.Modifier(attacker.strength);

        return Mathf.Max(1, total + damageBonus + attrMod);
    }

    /// <summary>
    /// Human-readable damage expression, e.g. "1d8+2".
    /// </summary>
    public string DamageExpression() =>
        damageBonus == 0
            ? $"{damageDiceCount}d{damageDiceSides}"
            : $"{damageDiceCount}d{damageDiceSides}{damageBonus:+0;-#}";

    public override void Use(CharacterStats user)
    {
        var equip = user.GetComponent<Equipment>();
        if (equip != null)
        {
            equip.EquipWeapon(this);
            Debug.Log($"{user.name} equipped {itemName} ({DamageExpression()} {damageType})");
        }
    }
}
