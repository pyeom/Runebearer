using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public GameObject inventoryPanel;
    public Transform slotsParent;           // Grid layout container
    public GameObject slotPrefab;           // InventorySlotUI prefab
    public TextMeshProUGUI weightText;
    public TextMeshProUGUI goldText;

    [Header("Open/Close Key")]
    public Key toggleKey = Key.I;

    private Inventory inventory;
    private readonly List<InventorySlotUI> slotUIs = new();

    void Start()
    {
        // Grab Inventory from the player GameObject
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogError("InventoryUI: No GameObject tagged 'Player' found."); return; }

        inventory = player.GetComponent<Inventory>();
        if (inventory == null) { Debug.LogError("InventoryUI: Player has no Inventory component."); return; }

        inventory.OnInventoryChanged += Refresh;

        BuildSlots();
        inventoryPanel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            Toggle();
    }

    void Toggle()
    {
        bool open = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(open);
        if (open) Refresh();
    }

    void BuildSlots()
    {
        // Clear any old slots (useful if called again at runtime)
        foreach (Transform child in slotsParent)
            Destroy(child.gameObject);
        slotUIs.Clear();

        for (int i = 0; i < inventory.maxSlots; i++)
        {
            var go = Instantiate(slotPrefab, slotsParent);
            var slotUI = go.GetComponent<InventorySlotUI>();
            slotUI.Setup(inventory, i);
            slotUIs.Add(slotUI);
        }
    }

    void Refresh()
    {
        foreach (var slotUI in slotUIs)
            slotUI.Refresh();

        if (weightText)
        {
            var stats = inventory.GetComponent<CharacterStats>();
            string cap = stats != null ? stats.CarryCapacity.ToString() : "?";
            weightText.text = $"Weight: {inventory.TotalWeight():F1} / {cap}";
        }

        // goldText requires a gold field on Inventory — add later if needed
    }

    void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }
}
