using UnityEngine;

/// <summary>
/// Shared static state for the drag-and-drop system.
/// Set by the drag source on BeginDrag; cleared on EndDrag or a successful drop.
/// </summary>
public static class DragDropState
{
    public static bool IsActive => DraggedItem != null;

    // ── Dragged payload ───────────────────────────────────────────────
    public static Item   DraggedItem;
    public static GameObject DragIcon;      // floating image that follows the cursor

    // ── Source: inventory slot ────────────────────────────────────────
    public static Inventory SourceInventory;
    public static int       SourceSlotIndex = -1;

    // ── Source: equipment slot ────────────────────────────────────────
    public static Equipment SourceEquipment;
    public static bool      SourceIsWeaponSlot;
    public static bool      SourceIsMainHand;
    public static ArmorSlot SourceArmorSlot;

    // ── Begin helpers ─────────────────────────────────────────────────
    public static void BeginFromInventory(Inventory inv, int index, Item item)
    {
        DraggedItem       = item;
        SourceInventory   = inv;
        SourceSlotIndex   = index;
        SourceEquipment   = null;
    }

    public static void BeginFromEquipment(Equipment eq, Item item,
                                          bool isWeapon, bool isMainHand,
                                          ArmorSlot armorSlot = default)
    {
        DraggedItem        = item;
        SourceEquipment    = eq;
        SourceIsWeaponSlot = isWeapon;
        SourceIsMainHand   = isMainHand;
        SourceArmorSlot    = armorSlot;
        SourceInventory    = null;
        SourceSlotIndex    = -1;
    }

    // ── Clear ─────────────────────────────────────────────────────────
    public static void Clear()
    {
        if (DragIcon != null) Object.Destroy(DragIcon);
        DragIcon        = null;
        DraggedItem     = null;
        SourceInventory = null;
        SourceSlotIndex = -1;
        SourceEquipment = null;
    }
}
