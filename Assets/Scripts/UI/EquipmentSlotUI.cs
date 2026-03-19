using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI representation of one equipment slot (weapon or armor).
///
/// Set up in the Inspector:
///   • isWeaponSlot  → true for the Weapon slot
///   • armorSlot     → which armor slot  (only relevant when isWeaponSlot = false)
///
/// Interactions:
///   • Hover → tooltip
///   • Right-click → unequip back to inventory
///   • Drag → drag equipped item back to inventory or to another equipment slot
///   • Drop → receive item from inventory / other equipment slot to equip
/// </summary>
public class EquipmentSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler
{
    // ── Inspector ─────────────────────────────────────────────────────
    [Header("Slot Definition")]
    public bool      isWeaponSlot;
    public ArmorSlot armorSlot;     // used only when isWeaponSlot = false

    [Header("References")]
    public Image           iconImage;
    public TextMeshProUGUI slotLabel;   // optional — shows slot name when empty
    public Image           highlight;
    public Image           background;  // tint when something is equipped

    // ── Runtime ───────────────────────────────────────────────────────
    private Equipment equipment;
    private Inventory inventory;

    // ── Init ─────────────────────────────────────────────────────────
    public void Setup(Equipment eq)
    {
        equipment = eq;
        inventory = eq.GetComponent<Inventory>();
        Refresh();
    }

    // ── Refresh ───────────────────────────────────────────────────────
    public void Refresh()
    {
        var item = GetEquippedItem();

        if (item != null)
        {
            iconImage.sprite = item.icon;
            iconImage.color  = Color.white;
            iconImage.gameObject.SetActive(true);
            if (slotLabel) slotLabel.gameObject.SetActive(false);
        }
        else
        {
            iconImage.gameObject.SetActive(false);
            if (slotLabel)
            {
                slotLabel.gameObject.SetActive(true);
                slotLabel.text = SlotName();
            }
        }

        if (highlight) highlight.enabled = false;
    }

    // ── Click ─────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        Unequip();
    }

    // ── Hover ─────────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData eventData)
    {
        var item = GetEquippedItem();
        if (item == null) return;
        if (highlight) highlight.enabled = true;
        ItemTooltipUI.Show(item, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlight) highlight.enabled = false;
        ItemTooltipUI.Hide();
    }

    // ── Drag ─────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        var item = GetEquippedItem();
        if (item == null)
        {
            eventData.pointerDrag = null;
            return;
        }

        ItemTooltipUI.Hide();
        DragDropState.BeginFromEquipment(equipment, item,
            isWeaponSlot, isMainHand: true, armorSlot);  // isMainHand unused but kept for DragDropState signature
        SpawnDragIcon(item.icon, eventData);
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
        Refresh();
    }

    // ── Drop ─────────────────────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        if (!DragDropState.IsActive) return;

        var item = DragDropState.DraggedItem;

        if (DragDropState.SourceInventory != null)
        {
            // Drag from inventory → try to equip
            TryEquip(item);
        }
        else if (DragDropState.SourceEquipment != null && DragDropState.SourceEquipment == equipment)
        {
            // Drag from another equipment slot on the same character → swap
            // Unequip source slot first (item goes to inventory)
            if (DragDropState.SourceIsWeaponSlot)
                equipment.UnequipWeapon(DragDropState.SourceIsMainHand);
            else
                equipment.Unequip(DragDropState.SourceArmorSlot);

            // Then equip it into this slot
            TryEquip(item);
        }

        DragDropState.Clear();
    }

    // ── Helpers ──────────────────────────────────────────────────────
    Item GetEquippedItem()
    {
        if (equipment == null) return null;
        if (isWeaponSlot) return equipment.weapon;
        return equipment.GetArmorInSlot(armorSlot);
    }

    void TryEquip(Item item)
    {
        if (isWeaponSlot)
        {
            if (item is WeaponItem w)
                equipment.EquipWeapon(w);
        }
        else
        {
            if (item is ArmorItem armor && armor.slot == armorSlot)
                equipment.Equip(armor);
        }
    }

    void Unequip()
    {
        if (isWeaponSlot)
            equipment.UnequipWeapon();
        else
            equipment.Unequip(armorSlot);
    }

    void SpawnDragIcon(Sprite sprite, PointerEventData eventData)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("DragIcon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        img.sprite        = sprite;
        img.raycastTarget = false;

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = ((RectTransform)transform).sizeDelta;
        rect.position  = eventData.position;

        DragDropState.DragIcon = go;
    }

    string SlotName()
    {
        if (isWeaponSlot) return "Weapon";
        return armorSlot.ToString();
    }
}
