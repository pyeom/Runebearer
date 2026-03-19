using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;
    public Image highlight;

    private Inventory inventory;
    private int slotIndex;

    public void Setup(Inventory inv, int index)
    {
        inventory = inv;
        slotIndex = index;
        Refresh();
    }

    public void Refresh()
    {
        var slot = inventory.Slots[slotIndex];

        if (slot.item != null)
        {
            iconImage.sprite = slot.item.icon;
            iconImage.gameObject.SetActive(true);
            quantityText.text = slot.quantity > 1 ? slot.quantity.ToString() : "";
        }
        else
        {
            iconImage.gameObject.SetActive(false);
            quantityText.text = "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var slot = inventory.Slots[slotIndex];
        if (slot.item == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            inventory.UseItem(slotIndex);
        }
    }

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
}
