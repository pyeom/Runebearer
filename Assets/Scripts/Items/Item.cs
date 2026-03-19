using UnityEngine;

public enum ItemType   { Weapon, Armor, Consumable, Quest, Misc }
public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>
/// Base ScriptableObject for all items.
/// Create subtypes (WeaponItem, ArmorItem, ConsumableItem) for specific behaviour.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Runebearer/Items/Item")]
public class Item : ScriptableObject
{
    [Header("Identity")]
    public string     itemName    = "Item";
    [TextArea]
    public string     description = "";
    public Sprite     icon;

    [Header("Classification")]
    public ItemType   type        = ItemType.Misc;
    public ItemRarity rarity      = ItemRarity.Common;

    [Header("Economy")]
    public int        goldValue   = 0;
    public float      weight      = 0.5f;   // lbs, counts toward carry capacity

    [Header("Stacking")]
    public bool       stackable   = false;
    public int        maxStack    = 1;

    /// <summary>
    /// Called when the player uses this item from the inventory.
    /// Override in subtypes to implement specific effects.
    /// </summary>
    public virtual void Use(CharacterStats user)
    {
        Debug.Log($"Used {itemName} — no effect defined.");
    }
}
