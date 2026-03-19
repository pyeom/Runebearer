using UnityEngine;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    public static ItemTooltipUI Instance { get; private set; }

    [Header("References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statsText;

    private RectTransform rect;

    void Awake()
    {
        Instance = this;
        rect = GetComponent<RectTransform>();
        gameObject.SetActive(false);
    }

    public static void Show(Item item, Vector3 worldPos)
    {
        if (Instance == null) return;
        Instance.gameObject.SetActive(true);

        Instance.nameText.text = item.itemName;
        Instance.typeText.text = $"{item.rarity}  {item.type}";
        Instance.descriptionText.text = item.description;
        Instance.statsText.text = BuildStatsText(item);

        // Position tooltip near the slot
        Instance.rect.position = worldPos + new Vector3(160, 0, 0);
    }

    public static void Hide()
    {
        if (Instance != null)
            Instance.gameObject.SetActive(false);
    }

    static string BuildStatsText(Item item)
    {
        if (item is WeaponItem w)
            return $"Damage: {w.DamageExpression()}\nRange: {(w.ranged ? w.range + " cells" : "Melee")}\n{(w.twoHanded ? "Two-Handed" : "One-Handed")}";

        if (item is ArmorItem a)
            return $"Armor Bonus: +{a.armorBonus}\nDamage Reduction: {a.damageReduction}\n{(a.requiredStrength > 0 ? "Req STR: " + a.requiredStrength : "")}";

        if (item is ConsumableItem c)
            return $"Effect: {c.effect}  ({c.value})";

        return $"Value: {item.goldValue}g  Weight: {item.weight}";
    }
}
