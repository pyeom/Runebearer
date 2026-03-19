using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One slot in the inventory: an item reference and a stack count.
/// </summary>
[Serializable]
public class ItemSlot
{
    public Item item;
    public int  quantity;

    public ItemSlot(Item item, int qty) { this.item = item; quantity = qty; }
    public bool IsEmpty => item == null || quantity <= 0;
}

/// <summary>
/// Fixed-size inventory component. Supports stacking, add, remove, and use.
/// Fires OnInventoryChanged whenever the contents change.
/// </summary>
[DisallowMultipleComponent]
public class Inventory : MonoBehaviour
{
    [Header("Settings")]
    public int maxSlots = 20;

    [Header("Starting Items")]
    public List<Item> startingItems = new List<Item>();

    [SerializeField] // visible in inspector for debugging
    private List<ItemSlot> slots = new List<ItemSlot>();

    public IReadOnlyList<ItemSlot> Slots => slots;

    public event Action OnInventoryChanged;

    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        // Pre-fill list with empty slots
        slots.Clear();
        for (int i = 0; i < maxSlots; i++)
            slots.Add(new ItemSlot(null, 0));
    }

    void Start()
    {
        foreach (var item in startingItems)
            if (item != null) AddItem(item);
    }

    // ── Add ───────────────────────────────────────────────────────────
    /// <summary>
    /// Adds <paramref name="qty"/> of <paramref name="item"/> to the inventory.
    /// Returns true if all units were placed; false if the inventory was full.
    /// </summary>
    public bool AddItem(Item item, int qty = 1)
    {
        if (item == null || qty <= 0) return false;

        // Try to top up existing stacks first
        if (item.stackable)
        {
            foreach (var slot in slots)
            {
                if (slot.item == item && slot.quantity < item.maxStack)
                {
                    int space = item.maxStack - slot.quantity;
                    int placed = Mathf.Min(qty, space);
                    slot.quantity += placed;
                    qty -= placed;
                    if (qty <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
        }

        // Fill empty slots
        foreach (var slot in slots)
        {
            if (slot.IsEmpty)
            {
                slot.item = item;
                slot.quantity = Mathf.Min(qty, item.maxStack);
                qty -= slot.quantity;
                if (qty <= 0) { OnInventoryChanged?.Invoke(); return true; }
            }
        }

        OnInventoryChanged?.Invoke();
        return qty <= 0; // false = couldn't fit everything
    }

    // ── Remove ────────────────────────────────────────────────────────
    /// <summary>
    /// Removes <paramref name="qty"/> of <paramref name="item"/> from the inventory.
    /// Returns true if found and removed.
    /// </summary>
    public bool RemoveItem(Item item, int qty = 1)
    {
        if (item == null || qty <= 0) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item != item) continue;

            slots[i].quantity -= qty;
            if (slots[i].quantity <= 0)
            {
                slots[i].item     = null;
                slots[i].quantity = 0;
            }
            OnInventoryChanged?.Invoke();
            return true;
        }
        return false;
    }

    // ── Use ───────────────────────────────────────────────────────────
    /// <summary>
    /// Uses the item in slot <paramref name="slotIndex"/>.
    /// Consumables are automatically removed after use.
    /// </summary>
    public void UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return;
        var slot = slots[slotIndex];
        if (slot.IsEmpty) return;

        var stats = GetComponent<CharacterStats>();
        slot.item.Use(stats);

        if (slot.item.type == ItemType.Consumable)
            RemoveItem(slot.item, 1);
    }

    // ── Query ─────────────────────────────────────────────────────────
    public bool HasItem(Item item)
        => slots.Exists(s => s.item == item && s.quantity > 0);

    public int CountItem(Item item)
    {
        int total = 0;
        foreach (var s in slots)
            if (s.item == item) total += s.quantity;
        return total;
    }

    public float TotalWeight()
    {
        float w = 0f;
        foreach (var s in slots)
            if (!s.IsEmpty) w += s.item.weight * s.quantity;
        return w;
    }

    public bool IsOverEncumbered()
    {
        var stats = GetComponent<CharacterStats>();
        return stats != null && TotalWeight() > stats.CarryCapacity;
    }

    // ── Reorder ───────────────────────────────────────────────────────
    /// <summary>Swaps the contents of two slots (used for drag-and-drop reordering).</summary>
    public void SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= slots.Count) return;
        if (indexB < 0 || indexB >= slots.Count) return;
        if (indexA == indexB) return;

        (slots[indexA].item, slots[indexB].item)         = (slots[indexB].item, slots[indexA].item);
        (slots[indexA].quantity, slots[indexB].quantity) = (slots[indexB].quantity, slots[indexA].quantity);

        OnInventoryChanged?.Invoke();
    }
}
