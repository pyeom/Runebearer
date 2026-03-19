using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages what the character has equipped (weapons + armor slots).
/// When swapping, the displaced item is returned to the Inventory component.
/// Fires OnEquipmentChanged whenever a slot changes.
/// </summary>
[DisallowMultipleComponent]
public class Equipment : MonoBehaviour
{
    // ── Weapon Slot ───────────────────────────────────────────────────
    [Header("Weapon")]
    public WeaponItem weapon;

    // ── Armor Slots ───────────────────────────────────────────────────
    [Header("Armor")]
    public ArmorItem head;
    public ArmorItem chest;
    public ArmorItem shoulders;
    public ArmorItem legs;
    public ArmorItem feet;
    public ArmorItem belt;

    // ── Accessories ───────────────────────────────────────────────────
    [Header("Accessories")]
    public ArmorItem amulet;
    public ArmorItem ring1;
    public ArmorItem ring2;

    public event Action OnEquipmentChanged;

    // ── Equip Weapon ──────────────────────────────────────────────────
    /// <summary>
    /// Equips a weapon, returning any previously equipped weapon to inventory.
    /// </summary>
    public void EquipWeapon(WeaponItem newWeapon, bool inMainHand = true)
    {
        if (newWeapon == null) return;
        var inv = GetComponent<Inventory>();

        if (weapon != null) inv?.AddItem(weapon);
        weapon = newWeapon;
        inv?.RemoveItem(newWeapon);
        OnEquipmentChanged?.Invoke();
    }

    public void UnequipWeapon(bool fromMainHand = true)
    {
        if (weapon == null) return;
        GetComponent<Inventory>()?.AddItem(weapon);
        weapon = null;
        OnEquipmentChanged?.Invoke();
    }

    // ── Equip Armor ───────────────────────────────────────────────────
    public void Equip(ArmorItem armor)
    {
        if (armor == null) return;

        // Check STR requirement
        var stats = GetComponent<CharacterStats>();
        if (stats != null && stats.strength < armor.requiredStrength)
        {
            Debug.LogWarning($"Not enough STR to equip {armor.itemName} (need {armor.requiredStrength}).");
            return;
        }

        var inv  = GetComponent<Inventory>();
        var prev = GetArmorSlot(armor.slot);
        if (prev != null) inv?.AddItem(prev);

        SetArmorSlot(armor.slot, armor);
        inv?.RemoveItem(armor);
        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(ArmorSlot slot)
    {
        var item = GetArmorSlot(slot);
        if (item == null) return;
        GetComponent<Inventory>()?.AddItem(item);
        SetArmorSlot(slot, null);
        OnEquipmentChanged?.Invoke();
    }

    // ── Derived Stat Helpers ──────────────────────────────────────────
    /// <summary>Total armor bonus from all equipped armor pieces.</summary>
    public int TotalArmorBonus()
    {
        int bonus = 0;
        foreach (var piece in AllArmor()) bonus += piece.armorBonus;
        return bonus;
    }

    /// <summary>Total flat damage reduction from all equipped armor pieces.</summary>
    public int TotalDamageReduction()
    {
        int dr = 0;
        foreach (var piece in AllArmor()) dr += piece.damageReduction;
        return dr;
    }

    /// <summary>
    /// Effective AC: character base AC + armor bonuses.
    /// Follows the highest-priority armor's DEX cap.
    /// </summary>
    public int EffectiveAC()
    {
        var stats = GetComponent<CharacterStats>();
        if (stats == null) return 10;

        int lowestDexCap = 99;
        int armorBonus   = 0;
        foreach (var piece in AllArmor())
        {
            armorBonus   += piece.armorBonus;
            if (piece.maxDexBonus < lowestDexCap)
                lowestDexCap = piece.maxDexBonus;
        }

        int dexMod = Mathf.Min(CharacterStats.Modifier(stats.dexterity), lowestDexCap);
        return 10 + armorBonus + dexMod;
    }

    // ── Slot Accessors ─────────────────────────────────────────────────
    /// <summary>Returns the armor item in the given slot (null if empty). Used by EquipmentSlotUI.</summary>
    public ArmorItem GetArmorInSlot(ArmorSlot slot) => GetArmorSlot(slot);

    ArmorItem GetArmorSlot(ArmorSlot slot) => slot switch
    {
        ArmorSlot.Head      => head,
        ArmorSlot.Chest     => chest,
        ArmorSlot.Shoulders => shoulders,
        ArmorSlot.Legs      => legs,
        ArmorSlot.Feet      => feet,
        ArmorSlot.Belt      => belt,
        ArmorSlot.Amulet    => amulet,
        ArmorSlot.Ring1     => ring1,
        ArmorSlot.Ring2     => ring2,
        _ => null
    };

    void SetArmorSlot(ArmorSlot slot, ArmorItem armor)
    {
        switch (slot)
        {
            case ArmorSlot.Head:      head      = armor; break;
            case ArmorSlot.Chest:     chest     = armor; break;
            case ArmorSlot.Shoulders: shoulders = armor; break;
            case ArmorSlot.Legs:      legs      = armor; break;
            case ArmorSlot.Feet:      feet      = armor; break;
            case ArmorSlot.Belt:      belt      = armor; break;
            case ArmorSlot.Amulet:    amulet    = armor; break;
            case ArmorSlot.Ring1:     ring1     = armor; break;
            case ArmorSlot.Ring2:     ring2     = armor; break;
        }
    }

    IEnumerable<ArmorItem> AllArmor()
    {
        if (head      != null) yield return head;
        if (chest     != null) yield return chest;
        if (shoulders != null) yield return shoulders;
        if (legs      != null) yield return legs;
        if (feet      != null) yield return feet;
        if (belt      != null) yield return belt;
        if (amulet    != null) yield return amulet;
        if (ring1     != null) yield return ring1;
        if (ring2     != null) yield return ring2;
    }
}
