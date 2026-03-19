using UnityEngine;

/// <summary>
/// Controller for the Equipment panel.
///
/// Assign each EquipmentSlotUI field in the Inspector to the matching slot object.
///
/// Scene layout suggestion (parent: EquipmentPanel):
///
///              [Head]
///   [Shoulders] [Chest]  [Amulet]
///               [Belt]
///               [Legs]
///               [Feet]
///   [Ring1]   [Weapon]   [Ring2]
///
/// Each child needs an EquipmentSlotUI component with isWeaponSlot / armorSlot set.
/// </summary>
public class EquipmentUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject equipmentPanel;

    [Header("Weapon")]
    public EquipmentSlotUI weaponSlot;

    [Header("Armor")]
    public EquipmentSlotUI headSlot;
    public EquipmentSlotUI shouldersSlot;
    public EquipmentSlotUI chestSlot;
    public EquipmentSlotUI legsSlot;
    public EquipmentSlotUI feetSlot;
    public EquipmentSlotUI beltSlot;

    [Header("Accessories")]
    public EquipmentSlotUI amuletSlot;
    public EquipmentSlotUI ring1Slot;
    public EquipmentSlotUI ring2Slot;

    private Equipment equipment;
    private EquipmentSlotUI[] allSlots;

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogError("EquipmentUI: No GameObject tagged 'Player'."); return; }

        equipment = player.GetComponent<Equipment>();
        if (equipment == null) { Debug.LogError("EquipmentUI: Player has no Equipment component."); return; }

        equipment.OnEquipmentChanged += Refresh;

        allSlots = new[]
        {
            weaponSlot,
            headSlot, shouldersSlot, chestSlot, legsSlot, feetSlot, beltSlot,
            amuletSlot, ring1Slot, ring2Slot
        };

        foreach (var slot in allSlots)
            if (slot != null) slot.Setup(equipment);

        equipmentPanel.SetActive(false);
    }

    public void Refresh()
    {
        foreach (var slot in allSlots)
            if (slot != null) slot.Refresh();
    }

    void OnDestroy()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= Refresh;
    }
}
