using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Generates a full set of test ScriptableObject items covering every weapon type,
/// armor slot, and consumable effect. Run via  Tools > Runebearer > Generate Test Items.
/// A second menu item adds all generated items directly to the player's Inventory so
/// you can immediately test the UI without dragging things in manually.
/// </summary>
public static class TestItemGenerator
{
    private const string OutputFolder = "Assets/TestItems";

    // -------------------------------------------------------------------------
    //  Menu items
    // -------------------------------------------------------------------------

    [MenuItem("Tools/Runebearer/Generate Test Items")]
    public static void GenerateAll()
    {
        EnsureFolder(OutputFolder);
        EnsureFolder(OutputFolder + "/Weapons");
        EnsureFolder(OutputFolder + "/Armor");
        EnsureFolder(OutputFolder + "/Consumables");

        GenerateWeapons();
        GenerateArmor();
        GenerateConsumables();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TestItemGenerator] All test items created in {OutputFolder}");
        EditorUtility.DisplayDialog(
            "Test Items Generated",
            $"Created:\n  • 8 weapons (one per WeaponType)\n  • 9 armor pieces (one per ArmorSlot)\n  • 8 consumables (one per effect)\n\nAssets saved to {OutputFolder}",
            "OK");
    }

    [MenuItem("Tools/Runebearer/Add Test Items to Player Inventory")]
    public static void AddToPlayerInventory()
    {
        var inventory = FindPlayerInventory();
        if (inventory == null)
        {
            EditorUtility.DisplayDialog("No Inventory Found",
                "Could not find an Inventory component in the active scene.\n" +
                "Make sure your Player GameObject is in the scene and has an Inventory component.",
                "OK");
            return;
        }

        // Load every asset inside the TestItems folder
        string[] guids = AssetDatabase.FindAssets("t:Item", new[] { OutputFolder });
        int added = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item == null) continue;

            Undo.RecordObject(inventory, "Add Test Item to Inventory");
            inventory.startingItems.Add(item);
            added++;
        }

        EditorUtility.SetDirty(inventory);

        Debug.Log($"[TestItemGenerator] Added {added} test items to {inventory.gameObject.name}'s startingItems list.");
        EditorUtility.DisplayDialog(
            "Items Added",
            $"Added {added} test items to the Player's startingItems list.\n" +
            "They will appear in the inventory the next time you enter Play Mode.",
            "OK");
    }

    // -------------------------------------------------------------------------
    //  Generators
    // -------------------------------------------------------------------------

    static void GenerateWeapons()
    {
        // (weaponType, damageType, diceCount, diceSides, bonus, twoHanded, ranged, range, goldValue, weight, rarity, description)
        var weapons = new (WeaponType wt, DamageType dt, int dc, int ds, int bonus, bool twoH, bool ranged, int range, int gold, float weight, ItemRarity rarity, string desc)[]
        {
            (WeaponType.Sword,     DamageType.Slashing,     1,  8, 0, false, false, 1,  15,  3f, ItemRarity.Common,   "A reliable longsword with a balanced hilt."),
            (WeaponType.Axe,       DamageType.Slashing,     1, 12, 0, true,  false, 1,  18,  7f, ItemRarity.Common,   "A heavy battle-axe that cleaves through armour."),
            (WeaponType.Mace,      DamageType.Bludgeoning,  1,  6, 2, false, false, 1,  12,  4f, ItemRarity.Common,   "A flanged mace favoured by war-clerics."),
            (WeaponType.Dagger,    DamageType.Piercing,     1,  4, 1, false, false, 1,   5,  1f, ItemRarity.Common,   "A slim dagger — quick to draw, easy to hide."),
            (WeaponType.Staff,     DamageType.Bludgeoning,  1,  6, 0, true,  false, 1,   8,  4f, ItemRarity.Uncommon, "An arcane focus carved from silverwood."),
            (WeaponType.Wand,      DamageType.Arcane,       2,  4, 0, false, true,  6,  25,  1f, ItemRarity.Uncommon, "A slender wand crackling with arcane energy."),
            (WeaponType.Bow,       DamageType.Piercing,     1,  8, 0, true,  true, 15,  20,  2f, ItemRarity.Common,   "A longbow strung with elven gut."),
            (WeaponType.Crossbow,  DamageType.Piercing,     1, 10, 0, true,  true, 12,  22,  5f, ItemRarity.Uncommon, "A heavy crossbow with a windlass mechanism."),
        };

        foreach (var w in weapons)
        {
            string assetName = $"Test_{w.wt}";
            string path = $"{OutputFolder}/Weapons/{assetName}.asset";
            if (AssetExists(path)) continue;

            var item = ScriptableObject.CreateInstance<WeaponItem>();
            item.itemName       = $"Test {w.wt}";
            item.description    = w.desc;
            item.type           = ItemType.Weapon;
            item.rarity         = w.rarity;
            item.goldValue      = w.gold;
            item.weight         = w.weight;
            item.stackable      = false;
            item.maxStack       = 1;
            item.weaponType     = w.wt;
            item.damageType     = w.dt;
            item.damageDiceCount = w.dc;
            item.damageDiceSides = w.ds;
            item.damageBonus    = w.bonus;
            item.twoHanded      = w.twoH;
            item.ranged         = w.ranged;
            item.range          = w.range;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"  Created weapon: {path}");
        }
    }

    static void GenerateArmor()
    {
        // (slot, armorType, armorBonus, maxDexBonus, damageReduction, requiredStr, goldValue, weight, rarity, name, description)
        var pieces = new (ArmorSlot slot, ArmorType at, int bonus, int maxDex, int dr, int reqStr, int gold, float weight, ItemRarity rarity, string name, string desc)[]
        {
            (ArmorSlot.Head,      ArmorType.Light,  1, 99, 0,  0, 10, 2f, ItemRarity.Common,   "Leather Skullcap",   "Soft leather cap offering basic head protection."),
            (ArmorSlot.Chest,     ArmorType.Heavy,  6,  0, 1, 13, 75, 15f, ItemRarity.Uncommon, "Plate Cuirass",     "A thick steel breastplate that restricts movement."),
            (ArmorSlot.Shoulders, ArmorType.Medium, 2,  2, 0,  0, 20, 4f, ItemRarity.Common,   "Scale Pauldrons",   "Overlapping metal scales protecting the shoulders."),
            (ArmorSlot.Legs,      ArmorType.Medium, 3,  2, 0, 11, 30, 8f, ItemRarity.Common,   "Chain Chausses",    "Chainmail leggings covering the legs and knees."),
            (ArmorSlot.Feet,      ArmorType.Light,  1, 99, 0,  0,  8, 2f, ItemRarity.Common,   "Ranger's Boots",    "Supple leather boots built for silent movement."),
            (ArmorSlot.Belt,      ArmorType.None,   0, 99, 0,  0, 12, 1f, ItemRarity.Common,   "Adventurer's Belt", "A wide belt with pouches for quick-access items."),
            (ArmorSlot.Amulet,    ArmorType.None,   1, 99, 0,  0, 50, 0.2f, ItemRarity.Rare,   "Amulet of Warding", "A carved jade pendant that deflects minor blows."),
            (ArmorSlot.Ring1,     ArmorType.None,   1, 99, 0,  0, 40, 0.1f, ItemRarity.Rare,   "Ring of Protection","A faintly glowing ring that shimmers on impact."),
            (ArmorSlot.Ring2,     ArmorType.None,   0, 99, 1,  0, 35, 0.1f, ItemRarity.Uncommon,"Ring of Iron Skin", "Grants a hint of stone-hard resilience."),
        };

        foreach (var p in pieces)
        {
            string assetName = $"Test_Armor_{p.slot}";
            string path = $"{OutputFolder}/Armor/{assetName}.asset";
            if (AssetExists(path)) continue;

            var item = ScriptableObject.CreateInstance<ArmorItem>();
            item.itemName         = p.name;
            item.description      = p.desc;
            item.type             = ItemType.Armor;
            item.rarity           = p.rarity;
            item.goldValue        = p.gold;
            item.weight           = p.weight;
            item.stackable        = false;
            item.maxStack         = 1;
            item.slot             = p.slot;
            item.armorType        = p.at;
            item.armorBonus       = p.bonus;
            item.maxDexBonus      = p.maxDex;
            item.damageReduction  = p.dr;
            item.requiredStrength = p.reqStr;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"  Created armor: {path}");
        }
    }

    static void GenerateConsumables()
    {
        // (effect, value, goldValue, weight, rarity, name, description)
        var consumables = new (ConsumableEffect fx, int val, int gold, float weight, ItemRarity rarity, string name, string desc)[]
        {
            (ConsumableEffect.HealHp,            20,  5, 0.5f, ItemRarity.Common,   "Health Potion",        "A ruby-red vial. Restores 20 HP when consumed."),
            (ConsumableEffect.GainExperience,   100, 15, 0.3f, ItemRarity.Uncommon, "Tome of Insight",      "Pages radiate warm light. Grants 100 XP."),
            (ConsumableEffect.BoostStrength,      2, 20, 0.3f, ItemRarity.Uncommon, "Potion of Strength",   "Tastes of iron. Permanently raises STR by 2."),
            (ConsumableEffect.BoostDexterity,     2, 20, 0.3f, ItemRarity.Uncommon, "Potion of Agility",    "Effervescent and light. Permanently raises DEX by 2."),
            (ConsumableEffect.BoostConstitution,  2, 20, 0.3f, ItemRarity.Uncommon, "Potion of Fortitude",  "Thick and bitter. Permanently raises CON by 2."),
            (ConsumableEffect.BoostIntelligence,  2, 25, 0.3f, ItemRarity.Rare,     "Potion of Brilliance", "Smells of ozone. Permanently raises INT by 2."),
            (ConsumableEffect.BoostWisdom,        2, 25, 0.3f, ItemRarity.Rare,     "Potion of Clarity",    "Still and clear. Permanently raises WIS by 2."),
            (ConsumableEffect.BoostCharisma,      2, 25, 0.3f, ItemRarity.Rare,     "Potion of Allure",     "Smells wonderful. Permanently raises CHA by 2."),
        };

        foreach (var c in consumables)
        {
            string assetName = $"Test_{c.fx}";
            string path = $"{OutputFolder}/Consumables/{assetName}.asset";
            if (AssetExists(path)) continue;

            var item = ScriptableObject.CreateInstance<ConsumableItem>();
            item.itemName   = c.name;
            item.description = c.desc;
            item.type       = ItemType.Consumable;
            item.rarity     = c.rarity;
            item.goldValue  = c.gold;
            item.weight     = c.weight;
            item.stackable  = true;
            item.maxStack   = 99;
            item.effect     = c.fx;
            item.value      = c.val;

            AssetDatabase.CreateAsset(item, path);
            Debug.Log($"  Created consumable: {path}");
        }
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    static bool AssetExists(string path) => AssetDatabase.LoadAssetAtPath<Object>(path) != null;

    static Inventory FindPlayerInventory()
    {
        // Try tagged player first
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            var inv = playerGO.GetComponent<Inventory>();
            if (inv != null) return inv;
        }
        // Fall back to any Inventory in the scene
        return Object.FindFirstObjectByType<Inventory>();
    }
}
