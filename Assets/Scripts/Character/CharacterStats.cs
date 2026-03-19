using System;
using UnityEngine;

/// <summary>
/// D&D 5e-inspired character statistics. Attach to any character (player or enemy).
/// Provides the six core attributes, derived stats, HP management, and XP/leveling.
/// </summary>
[DisallowMultipleComponent]
public class CharacterStats : MonoBehaviour
{
    // ── Core Attributes ──────────────────────────────────────────────
    [Header("Core Attributes")]
    [Range(1, 30)] public int strength     = 10;  // melee damage, carry weight
    [Range(1, 30)] public int dexterity    = 10;  // AC, initiative, ranged attacks
    [Range(1, 30)] public int constitution = 10;  // max HP
    [Range(1, 30)] public int intelligence = 10;  // spell power, skill points
    [Range(1, 30)] public int wisdom       = 10;  // perception, magic defense
    [Range(1, 30)] public int charisma     = 10;  // social encounters, luck

    // ── Progression ──────────────────────────────────────────────────
    [Header("Progression")]
    public int level      = 1;
    public int experience = 0;

    // ── Runtime State ────────────────────────────────────────────────
    [HideInInspector] public int currentHp;

    // ── Events ───────────────────────────────────────────────────────
    public event Action<int, int> OnHpChanged;  // (current, max)
    public event Action<int>      OnLevelUp;    // new level number
    public event Action           OnDeath;

    // ── D&D Modifier formula: floor((score - 10) / 2) ────────────────
    public static int Modifier(int score) => Mathf.FloorToInt((score - 10) / 2f);

    // ── Derived Stats ─────────────────────────────────────────────────
    /// <summary>Maximum HP: 10 base + CON modifier per level.</summary>
    public int MaxHp             => 10 + Modifier(constitution) * Mathf.Max(1, level);

    /// <summary>Base Armor Class (before equipment). DEX modifier applies.</summary>
    public int BaseArmorClass    => 10 + Modifier(dexterity);

    /// <summary>Melee attack bonus from STR.</summary>
    public int AttackBonus       => Modifier(strength);

    /// <summary>Spell attack bonus from INT.</summary>
    public int SpellAttackBonus  => Modifier(intelligence);

    /// <summary>Initiative roll bonus from DEX.</summary>
    public int Initiative        => Modifier(dexterity);

    /// <summary>Passive Perception: 10 + WIS modifier.</summary>
    public int PassivePerception => 10 + Modifier(wisdom);

    /// <summary>Maximum carry weight in lbs: STR × 15.</summary>
    public int CarryCapacity     => strength * 15;

    // ── XP thresholds for levels 1-20 (D&D 5e) ───────────────────────
    static readonly int[] XpThresholds =
    {
        0, 300, 900, 2700, 6500, 14000, 23000,
        34000, 48000, 64000, 85000, 100000,
        120000, 140000, 165000, 195000, 225000,
        265000, 305000, 355000
    };

    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        currentHp = MaxHp;
    }

    // ── HP Management ─────────────────────────────────────────────────
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        currentHp = Mathf.Max(0, currentHp - amount);
        OnHpChanged?.Invoke(currentHp, MaxHp);
        if (currentHp == 0)
            OnDeath?.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHp = Mathf.Min(MaxHp, currentHp + amount);
        OnHpChanged?.Invoke(currentHp, MaxHp);
    }

    public void FullRestore() => Heal(MaxHp);

    // ── Experience & Leveling ─────────────────────────────────────────
    public void GainExperience(int xp)
    {
        if (xp <= 0) return;
        experience += xp;

        int newLevel = 1;
        for (int i = XpThresholds.Length - 1; i >= 1; i--)
        {
            if (experience >= XpThresholds[i]) { newLevel = i + 1; break; }
        }
        newLevel = Mathf.Min(newLevel, 20);

        if (newLevel > level)
        {
            level = newLevel;
            FullRestore();
            OnLevelUp?.Invoke(level);
            Debug.Log($"{name} reached level {level}!");
        }
    }

    /// <summary>XP needed to reach the next level. Returns 0 at max level.</summary>
    public int XpForNextLevel()
    {
        if (level >= 20) return 0;
        return XpThresholds[level] - experience;
    }

    // ── Debug ─────────────────────────────────────────────────────────
    public override string ToString() =>
        $"{name} | Lvl {level} | HP {currentHp}/{MaxHp} | " +
        $"STR {strength}({Modifier(strength):+0;-#}) " +
        $"DEX {dexterity}({Modifier(dexterity):+0;-#}) " +
        $"CON {constitution}({Modifier(constitution):+0;-#}) " +
        $"INT {intelligence}({Modifier(intelligence):+0;-#}) " +
        $"WIS {wisdom}({Modifier(wisdom):+0;-#}) " +
        $"CHA {charisma}({Modifier(charisma):+0;-#})";
}
