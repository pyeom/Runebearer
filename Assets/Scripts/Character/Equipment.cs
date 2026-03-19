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
    // ── Weapon Slots ─────────────────────────────────────────────────
    [Header("Weapons")]
    public WeaponItem mainHand;
    public WeaponItem offHand;   // null when mainHand is two-handed

    // ── Armor Slots ───────────────────────────────────────────────────
    [Header("Armor")]
    public ArmorItem head;
    public ArmorItem chest;
    public ArmorItem legs;
    public ArmorItem feet;
    public ArmorItem hands;
    public ArmorItem offhandArmor;  // shield
    public ArmorItem ring;
    public ArmorItem amulet;

    public event Action OnEquipmentChanged;

    // ── Equip Weapon ──────────────────────────────────────────────────
    /// <summary>
    /// Equips a weapon into the main-hand or off-hand slot.
    /// The displaced weapon (if any) is pushed back into inventory.
    /// </summary>
    public void EquipWeapon(WeaponItem weapon, bool inMainHand = true)
    {
        if (weapon == null) return;
        var inv = GetComponent<Inventory>();

        if (inMainHand)
        {
            if (mainHand != null) inv?.AddItem(mainHand);
            mainHand = weapon;

            // Two-handed weapons clear the off-hand
            if (weapon.twoHanded)
            {
                if (offHand != null) inv?.AddItem(offHand);
                offHand = null;
            }
        }
        else
        {
            if (weapon.twoHanded)
            {
                Debug.LogWarning("Two-handed weapons cannot be equipped in the off-hand.");
                return;
            }
            if (offHand != null) inv?.AddItem(offHand);
            offHand = weapon;
        }

        inv?.RemoveItem(weapon);
        OnEquipmentChanged?.Invoke();
    }

    public void UnequipWeapon(bool fromMainHand = true)
    {
        var inv = GetComponent<Inventory>();
        if (fromMainHand && mainHand != null)
        {
            inv?.AddItem(mainHand);
            mainHand = null;
        }
        else if (!fromMainHand && offHand != null)
        {
            inv?.AddItem(offHand);
            offHand = null;
        }
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
    ArmorItem GetArmorSlot(ArmorSlot slot) => slot switch
    {
        ArmorSlot.Head    => head,
        ArmorSlot.Chest   => chest,
        ArmorSlot.Legs    => legs,
        ArmorSlot.Feet    => feet,
        ArmorSlot.Hands   => hands,
        ArmorSlot.Offhand => offhandArmor,
        ArmorSlot.Ring    => ring,
        ArmorSlot.Amulet  => amulet,
        _ => null
    };

    void SetArmorSlot(ArmorSlot slot, ArmorItem armor)
    {
        switch (slot)
        {
            case ArmorSlot.Head:    head          = armor; break;
            case ArmorSlot.Chest:   chest         = armor; break;
            case ArmorSlot.Legs:    legs          = armor; break;
            case ArmorSlot.Feet:    feet          = armor; break;
            case ArmorSlot.Hands:   hands         = armor; break;
            case ArmorSlot.Offhand: offhandArmor  = armor; break;
            case ArmorSlot.Ring:    ring          = armor; break;
            case ArmorSlot.Amulet:  amulet        = armor; break;
        }
    }

    IEnumerable<ArmorItem> AllArmor()
    {
        if (head         != null) yield return head;
        if (chest        != null) yield return chest;
        if (legs         != null) yield return legs;
        if (feet         != null) yield return feet;
        if (hands        != null) yield return hands;
        if (offhandArmor != null) yield return offhandArmor;
        if (ring         != null) yield return ring;
        if (amulet       != null) yield return amulet;
    }
}
