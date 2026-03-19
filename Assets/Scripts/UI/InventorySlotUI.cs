using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One slot in the inventory grid.
/// Supports:
///   • Hover → tooltip
///   • Right-click → equip (weapons/armor) or use (consumables)
///   • Drag-and-drop → reorder inventory slots or equip by dropping on an equipment slot
///   • Receive drops from other inventory slots or from equipment slots
/// </summary>
public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler
{
    [Header("References")]
    public Image              iconImage;
    public TextMeshProUGUI    quantityText;
    public Image              highlight;

    private Inventory  inventory;
    private int        slotIndex;
    private Equipment  equipment;   // cached for equip on right-click / drop

    // ── Setup ─────────────────────────────────────────────────────────
    public void Setup(Inventory inv, int index)
    {
        inventory = inv;
        slotIndex = index;
        equipment = inv.GetComponent<Equipment>();
        Refresh();
    }

    public void Refresh()
    {
        var slot = inventory.Slots[slotIndex];
        if (slot.item != null)
        {
            iconImage.sprite = slot.item.icon;
            iconImage.color  = Color.white;
            quantityText.text = slot.quantity > 1 ? slot.quantity.ToString() : "";
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color  = Color.clear;  // invisible but keeps the raycast target alive so OnDrop still fires
            quantityText.text = "";
        }

        // Always keep the icon image active — deactivating it removes the only
        // raycast-enabled graphic on empty slots, which silently swallows drop events.
        iconImage.gameObject.SetActive(true);
    }

    // ── Click ─────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;

        var slot = inventory.Slots[slotIndex];
        if (slot.item == null) return;

        // Weapons and armor: equip on right-click
        if (slot.item is WeaponItem weapon && equipment != null)
        {
            equipment.EquipWeapon(weapon);
        }
        else if (slot.item is ArmorItem armor && equipment != null)
        {
            equipment.Equip(armor);
        }
        else
        {
            inventory.UseItem(slotIndex);
        }
    }

    // ── Hover ─────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData eventData)
    {
        var slot = inventory.Slots[slotIndex];
        if (slot.item == null) return;
        if (highlight) highlight.enabled = true;
        ItemTooltipUI.Show(slot.item, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlight) highlight.enabled = false;
        ItemTooltipUI.Hide();
    }

    // ── Drag ──────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        var slot = inventory.Slots[slotIndex];
        if (slot.IsEmpty)
        {
            // Cancel drag — nothing to drag
            eventData.pointerDrag = null;
            return;
        }

        ItemTooltipUI.Hide();
        DragDropState.BeginFromInventory(inventory, slotIndex, slot.item);
        SpawnDragIcon(slot.item.icon, eventData);

        // Dim source icon so the slot looks "empty" during drag
        iconImage.color = new Color(1f, 1f, 1f, 0.35f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (DragDropState.DragIcon != null)
            DragDropState.DragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragDropState.Clear();
        Refresh(); // restore icon appearance
    }

    // ── Drop ──────────────────────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        if (!DragDropState.IsActive) return;

        if (DragDropState.SourceInventory == inventory)
        {
            // Reorder within same inventory
            inventory.SwapSlots(DragDropState.SourceSlotIndex, slotIndex);
        }
        else if (DragDropState.SourceEquipment != null)
        {
            // Dragged from an equipment slot → unequip goes to any free slot,
            // then swap so it lands on this specific slot
            var eq = DragDropState.SourceEquipment;
            int countBefore = inventory.Slots.Count;

            if (DragDropState.SourceIsWeaponSlot)
                eq.UnequipWeapon(DragDropState.SourceIsMainHand);
            else
                eq.Unequip(DragDropState.SourceArmorSlot);

            // Find where it landed and swap to target slot
            var item = DragDropState.DraggedItem;
            int landedAt = FindSlotIndex(item);
            if (landedAt >= 0 && landedAt != slotIndex)
                inventory.SwapSlots(landedAt, slotIndex);
        }

        DragDropState.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────
    void SpawnDragIcon(Sprite sprite, PointerEventData eventData)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("DragIcon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling(); // render on top

        var img = go.GetComponent<Image>();
        img.sprite        = sprite;
        img.raycastTarget = false; // don't block drops on slots below

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = ((RectTransform)transform).sizeDelta;
        rect.position  = eventData.position;

        DragDropState.DragIcon = go;
    }

    int FindSlotIndex(Item item)
    {
        for (int i = 0; i < inventory.Slots.Count; i++)
            if (inventory.Slots[i].item == item) return i;
        return -1;
    }
}
