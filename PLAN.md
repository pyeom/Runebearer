# Roguelike Game — Full Feature Development Plan
> Legend: `[ ]` = not started · `[~]` = in progress · `[x]` = done

---

## Existing Codebase Quick Reference

| File | Key API |
|------|---------|
| `PlayerMovement.cs` | `pathQueue`, `WorldToCell(Vector3)`, uses `GridGenerator.Instance.WalkableTiles` |
| `GridGenerator.cs` | `Instance`, `WalkableTiles: HashSet<Vector2Int>`, `ComputeLayout(int seed)` |
| `Pathfinder.cs` | `static FindPath(Vector2Int, Vector2Int, HashSet<Vector2Int>)` → `List<Vector2Int>` |
| `CharacterStats.cs` | `TakeDamage(int)`, `Heal(int)`, `GainExperience(int)`, `Initiative`, `MaxHp`, `OnDeath`, `OnLevelUp` |
| `Equipment.cs` | `EffectiveAC()`, `TotalDamageReduction()`, `weapon: WeaponItem` |
| `WeaponItem.cs` | `RollDamage(CharacterStats)`, `range: int`, `ranged: bool`, `DamageType` enum |
| `Item.cs` | `ItemType`, `ItemRarity` enums, `Use(CharacterStats)` |
| `Inventory.cs` | `AddItem(Item)`, `RemoveItem(Item)`, `Slots`, `OnInventoryChanged` |

---

## PHASE 1 — Turn-Based Combat Foundation

> **Why this first:** Every other system (enemies, spells, runes, HUD) requires a notion of "whose turn is it" and "how many AP does this cost." Build the skeleton first; everything else plugs into it.

---

### TASK 1.1 — `ICombatant` Interface
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/ICombatant.cs`
**Depends on:** nothing (define this first — everything else implements it)

**WHY:** TurnManager needs a unified contract to operate on both the player and enemies without knowing which is which. Using an interface keeps coupling minimal.

**WHAT:** A C# interface declaring the minimum that any combat participant must expose.

**HOW (step by step):**
1. Create folder `Assets/Scripts/Combat/`
2. Create `ICombatant.cs` with the snippet below
3. No scene work needed — this is pure code

```csharp
// Assets/Scripts/Combat/ICombatant.cs
public interface ICombatant
{
    string CombatantName { get; }
    int    Initiative    { get; }   // used to sort turn order at combat start
    bool   IsAlive       { get; }

    void TakeTurn();                      // called by TurnManager when it's this combatant's turn
    void TakeHit(DamageInfo info);        // called by CombatResolver
    void OnCombatStart();                 // reset AP, clear temp buffs, etc.
    void OnCombatEnd();
}
```

---

### TASK 1.2 — `DamageInfo` Struct
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/DamageInfo.cs`
**Depends on:** `DamageType` (already defined in `WeaponItem.cs`)

**WHY:** Passing raw `int` for damage hides context (type, source, crit). A struct keeps damage events self-describing and easy to log in the combat log.

**WHAT:** A value type bundling all info about one damage event.

**HOW:**
1. Create `Assets/Scripts/Combat/DamageInfo.cs`
2. `DamageType` enum is already in `WeaponItem.cs` — no duplication needed

```csharp
// Assets/Scripts/Combat/DamageInfo.cs
using UnityEngine;

public struct DamageInfo
{
    public int        Amount;
    public DamageType Type;
    public GameObject Source;    // who dealt the damage (for log, XP credit)
    public bool       IsCritical;

    public DamageInfo(int amount, DamageType type, GameObject source, bool isCritical = false)
    {
        Amount     = amount;
        Type       = type;
        Source     = source;
        IsCritical = isCritical;
    }

    public override string ToString() =>
        IsCritical ? $"CRIT {Amount} {Type}" : $"{Amount} {Type}";
}
```

---

### TASK 1.3 — `CombatActor` Component
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/CombatActor.cs`
**Depends on:** `ICombatant`, `DamageInfo`, `CharacterStats`

**WHY:** Both the player and every enemy need identical AP tracking. Centralising it in one component avoids duplicating the same fields across `PlayerMovement` and every enemy script.

**WHAT:** A MonoBehaviour that:
- Holds `maxAP` and `currentAP`
- Refreshes AP at the start of each turn
- Exposes `SpendAP(int)` / `CanSpend(int)`
- Partially implements `ICombatant` so subclasses only override `TakeTurn()`

**HOW:**
1. Create `Assets/Scripts/Combat/CombatActor.cs`
2. Add this component to the Player prefab alongside `CharacterStats`
3. Each enemy prefab also gets it

```csharp
// Assets/Scripts/Combat/CombatActor.cs
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatActor : MonoBehaviour, ICombatant
{
    [Header("Action Points")]
    public int maxAP = 2;

    public int  CurrentAP { get; private set; }
    public bool IsAlive   => stats != null && stats.currentHp > 0;
    public int  Initiative => stats != null ? stats.Initiative : 0;
    public string CombatantName => gameObject.name;

    public event Action<int, int> OnAPChanged;   // (current, max)

    protected CharacterStats stats;

    protected virtual void Awake()
    {
        stats = GetComponent<CharacterStats>();
    }

    // ── ICombatant ────────────────────────────────────────────────────
    public virtual void TakeTurn()
    {
        // Overridden by PlayerCombatActor and EnemyAI
    }

    public virtual void TakeHit(DamageInfo info)
    {
        int dr  = GetComponent<Equipment>()?.TotalDamageReduction() ?? 0;
        int net = Mathf.Max(1, info.Amount - dr);
        stats?.TakeDamage(net);

        CombatLog.Log($"{CombatantName} takes {net} {info.Type}" +
                      (info.IsCritical ? " (CRIT!)" : ""), LogType.Damage);
    }

    public virtual void OnCombatStart() => RefreshAP();
    public virtual void OnCombatEnd()   { }

    // ── AP ────────────────────────────────────────────────────────────
    public void RefreshAP()
    {
        CurrentAP = maxAP;
        OnAPChanged?.Invoke(CurrentAP, maxAP);
    }

    public bool CanSpend(int amount) => CurrentAP >= amount;

    public bool SpendAP(int amount)
    {
        if (!CanSpend(amount)) return false;
        CurrentAP -= amount;
        OnAPChanged?.Invoke(CurrentAP, maxAP);
        return true;
    }
}
```

---

### TASK 1.4 — `TurnManager` Singleton
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/TurnManager.cs`
**Depends on:** `ICombatant`, `CombatActor`

**WHY:** There must be a single authority for "whose turn it is." Without it, both the player and enemies would try to act simultaneously. The singleton pattern is appropriate here because there is never more than one combat in progress.

**WHAT:**
- Holds a sorted queue of `ICombatant`
- Exposes `StartCombat`, `EndTurn`, `EndCombat`
- Has a state machine: `OutOfCombat → PlayerTurn ↔ EnemyTurn → Resolving`
- Fires `OnTurnChanged` event so HUD, AI, and input all react

**HOW:**
1. Create `Assets/Scripts/Combat/TurnManager.cs`
2. In `SampleScene`: create an empty GameObject named `TurnManager`, add the component
3. Wire the `EndTurn` button in the HUD (Task 7.1) to call `TurnManager.Instance.EndTurn()`

```csharp
// Assets/Scripts/Combat/TurnManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CombatState { OutOfCombat, PlayerTurn, EnemyTurn, Resolving }

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public CombatState State { get; private set; } = CombatState.OutOfCombat;
    public ICombatant   CurrentCombatant { get; private set; }

    public event Action<ICombatant>  OnTurnChanged;   // fires at start of each turn
    public event Action              OnCombatStarted;
    public event Action              OnCombatEnded;

    private List<ICombatant>  turnOrder = new List<ICombatant>();
    private int               currentIndex = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────
    public void StartCombat(List<ICombatant> combatants)
    {
        if (State != CombatState.OutOfCombat) return;

        turnOrder = new List<ICombatant>(combatants);
        // Sort descending by initiative (highest goes first)
        turnOrder.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));

        currentIndex = 0;
        foreach (var c in turnOrder) c.OnCombatStart();

        OnCombatStarted?.Invoke();
        StartCoroutine(RunTurn());
    }

    public void EndTurn()
    {
        if (State == CombatState.PlayerTurn || State == CombatState.EnemyTurn)
            StartCoroutine(AdvanceTurn());
    }

    public void EndCombat()
    {
        StopAllCoroutines();
        foreach (var c in turnOrder) c.OnCombatEnd();
        turnOrder.Clear();
        State = CombatState.OutOfCombat;
        CurrentCombatant = null;
        OnCombatEnded?.Invoke();
    }

    // ── Turn Loop ─────────────────────────────────────────────────────
    private IEnumerator RunTurn()
    {
        // Remove dead combatants before starting turn
        turnOrder.RemoveAll(c => !c.IsAlive);

        if (turnOrder.Count == 0) { EndCombat(); yield break; }

        // Check win/lose: if player dead or all enemies dead
        // (More specific logic added in Phase 2 when EnemyAI exists)

        if (currentIndex >= turnOrder.Count)
            currentIndex = 0;

        CurrentCombatant = turnOrder[currentIndex];

        bool isPlayer = CurrentCombatant is PlayerCombatActor;
        State = isPlayer ? CombatState.PlayerTurn : CombatState.EnemyTurn;

        // Refresh AP for this combatant
        if (CurrentCombatant is CombatActor actor)
            actor.RefreshAP();

        OnTurnChanged?.Invoke(CurrentCombatant);
        CurrentCombatant.TakeTurn();

        // For enemies TakeTurn() is a coroutine-style operation —
        // we yield until their AP is exhausted (see EnemyAI)
        if (State == CombatState.EnemyTurn)
        {
            yield return new WaitUntil(() => State != CombatState.EnemyTurn);
        }
        // Player turn ends via EndTurn() call
    }

    private IEnumerator AdvanceTurn()
    {
        State = CombatState.Resolving;
        yield return null; // one frame gap for animations

        currentIndex = (currentIndex + 1) % turnOrder.Count;
        yield return StartCoroutine(RunTurn());
    }

    public bool IsPlayerTurn => State == CombatState.PlayerTurn;
}
```

**Note:** `PlayerCombatActor` is a thin subclass of `CombatActor` added in Task 1.7 so `TurnManager` can identify the player.

---

### TASK 1.5 — `CombatResolver` — Hit/Miss/Damage
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/CombatResolver.cs`
**Depends on:** `DamageInfo`, `CharacterStats`, `Equipment`, `WeaponItem`, `ICombatant`

**WHY:** Separating attack resolution from both the attacker and the target means neither needs to know how dice work. This also makes it easy to modify (add crits, rune effects, status debuffs) in one place.

**WHAT:**
- Static helper class
- `ResolveAttack(CombatActor attacker, ICombatant target)` → returns `DamageInfo?`
- d20 roll + AttackBonus vs target EffectiveAC
- Crit on natural 20 (double dice)

**HOW:**
1. Create `Assets/Scripts/Combat/CombatResolver.cs`
2. Called from `PlayerCombatActor` when player clicks an enemy, and from `EnemyAI` on its turn

```csharp
// Assets/Scripts/Combat/CombatResolver.cs
using UnityEngine;

public static class CombatResolver
{
    // Returns null on a miss.
    public static DamageInfo? ResolveAttack(CombatActor attacker, ICombatant target)
    {
        var attackerStats = attacker.GetComponent<CharacterStats>();
        var attackerEquip = attacker.GetComponent<Equipment>();
        var weapon        = attackerEquip?.weapon;

        if (attackerStats == null) return null;

        // ── Attack Roll ───────────────────────────────────────────────
        int d20       = Random.Range(1, 21);
        bool isCrit   = d20 == 20;
        bool isMiss   = d20 == 1;  // natural 1 always misses

        int attackBonus = weapon != null && weapon.ranged
            ? CharacterStats.Modifier(attackerStats.dexterity)
            : attackerStats.AttackBonus;

        int roll = d20 + attackBonus;

        // Get target AC
        int targetAC = 10;
        if (target is CombatActor targetActor)
        {
            var targetEquip = targetActor.GetComponent<Equipment>();
            targetAC = targetEquip != null
                ? targetEquip.EffectiveAC()
                : targetActor.GetComponent<CharacterStats>()?.BaseArmorClass ?? 10;
        }

        CombatLog.Log($"{attacker.CombatantName} rolls {d20}+{attackBonus}={roll} vs AC {targetAC}",
                      LogType.Info);

        if (isMiss || (!isCrit && roll < targetAC))
        {
            CombatLog.Log($"{attacker.CombatantName} MISSES {(target as CombatActor)?.CombatantName}",
                          LogType.Miss);
            return null;
        }

        // ── Damage Roll ───────────────────────────────────────────────
        int damage;
        DamageType dmgType = DamageType.Bludgeoning;

        if (weapon != null)
        {
            damage  = isCrit ? RollCrit(weapon, attackerStats) : weapon.RollDamage(attackerStats);
            dmgType = weapon.damageType;
        }
        else
        {
            // Unarmed: 1 + STR modifier
            damage  = Mathf.Max(1, 1 + attackerStats.AttackBonus);
            dmgType = DamageType.Bludgeoning;
        }

        return new DamageInfo(damage, dmgType, attacker.gameObject, isCrit);
    }

    // Double the dice (not the modifier) on a crit
    private static int RollCrit(WeaponItem weapon, CharacterStats attacker)
    {
        int dice = 0;
        // Roll twice the number of dice
        for (int i = 0; i < weapon.damageDiceCount * 2; i++)
            dice += Random.Range(1, weapon.damageDiceSides + 1);

        int attrMod = weapon.ranged
            ? CharacterStats.Modifier(attacker.dexterity)
            : CharacterStats.Modifier(attacker.strength);

        return Mathf.Max(1, dice + weapon.damageBonus + attrMod);
    }

    // ── Range Check ───────────────────────────────────────────────────
    public static bool IsInRange(Vector2Int attackerCell, Vector2Int targetCell, int range)
    {
        // Manhattan distance for grid combat
        return Mathf.Abs(attackerCell.x - targetCell.x) +
               Mathf.Abs(attackerCell.y - targetCell.y) <= range;
    }
}
```

---

### TASK 1.6 — `CombatLog` (Static Helper)
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/CombatLog.cs`
**Depends on:** nothing

**WHY:** `CombatResolver` and `CombatActor` both need to emit log lines. A static event bus avoids both needing a reference to the UI.

```csharp
// Assets/Scripts/Combat/CombatLog.cs
using System;

public enum LogType { Damage, Heal, Miss, Status, Info }

public static class CombatLog
{
    public static event Action<string, LogType> OnEntry;

    public static void Log(string message, LogType type = LogType.Info)
    {
        UnityEngine.Debug.Log($"[Combat] {message}");
        OnEntry?.Invoke(message, type);
    }
}
```

---

### TASK 1.7 — `TileHighlighter`
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/TileHighlighter.cs`
**Depends on:** `GridGenerator`, `TurnManager`, `CombatActor`

**WHY:** Without visual tile highlights, the player has no way to know how far they can move or what they can attack. This is the primary feedback mechanism for the AP system.

**WHAT:**
- Manages a pool of quad GameObjects placed on tiles
- Four colour modes: Blue (movement), Red (attack), Yellow (hover), Green (spell)
- Recalculates movement range via BFS from player position using remaining AP

**HOW:**
1. Create `Assets/Scripts/Combat/TileHighlighter.cs`
2. Add an empty child GameObject to `GridGenerator` named `TileHighlights`
3. Create a simple transparent quad material for each colour (`Assets/Materials/Highlight_*.mat`)
4. In Unity: assign materials in the Inspector on the `TileHighlighter` component

```csharp
// Assets/Scripts/Combat/TileHighlighter.cs
using System.Collections.Generic;
using UnityEngine;

public class TileHighlighter : MonoBehaviour
{
    public static TileHighlighter Instance { get; private set; }

    [Header("Materials")]
    public Material moveMaterial;       // blue, transparent
    public Material attackMaterial;     // red, transparent
    public Material hoverMaterial;      // yellow, transparent
    public Material spellMaterial;      // green, transparent

    [Header("Settings")]
    public float highlightY = 0.02f;    // just above tile surface

    private Dictionary<Vector2Int, GameObject> activeHighlights = new();
    private GameObject highlightParent;

    void Awake()
    {
        Instance = this;
        highlightParent = new GameObject("HighlightPool");
        highlightParent.transform.SetParent(transform);
    }

    // ── Public API ────────────────────────────────────────────────────
    public void ShowMovementRange(Vector2Int origin, int apRemaining)
    {
        ClearAll();
        var reachable = GetReachableTiles(origin, apRemaining);
        foreach (var cell in reachable)
            PlaceHighlight(cell, moveMaterial);
    }

    public void ShowAttackRange(Vector2Int origin, int range)
    {
        var cells = GetTilesInRange(origin, range);
        foreach (var cell in cells)
        {
            if (!activeHighlights.ContainsKey(cell))
                PlaceHighlight(cell, attackMaterial);
        }
    }

    public void SetHover(Vector2Int cell)
    {
        if (activeHighlights.TryGetValue(cell, out var existing))
            existing.GetComponent<Renderer>().material = hoverMaterial;
        else
            PlaceHighlight(cell, hoverMaterial);
    }

    public void ClearAll()
    {
        foreach (var go in activeHighlights.Values)
            Destroy(go);
        activeHighlights.Clear();
    }

    // ── BFS for movement range ─────────────────────────────────────────
    // Each tile costs 1 AP to enter (later: use TileData.moveCostMultiplier)
    private HashSet<Vector2Int> GetReachableTiles(Vector2Int origin, int apBudget)
    {
        var walkable = GridGenerator.Instance?.WalkableTiles;
        if (walkable == null) return new HashSet<Vector2Int>();

        var visited = new Dictionary<Vector2Int, int>();  // cell → AP spent
        var queue   = new Queue<(Vector2Int cell, int apSpent)>();
        queue.Enqueue((origin, 0));
        visited[origin] = 0;

        while (queue.Count > 0)
        {
            var (current, spent) = queue.Dequeue();
            int[] dx = { 1, -1, 0,  0 };
            int[] dz = { 0,  0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                var next = new Vector2Int(current.x + dx[i], current.y + dz[i]);
                int cost = spent + 1;

                if (!walkable.Contains(next))     continue;
                if (cost > apBudget)              continue;
                if (visited.TryGetValue(next, out int prev) && prev <= cost) continue;

                visited[next] = cost;
                queue.Enqueue((next, cost));
            }
        }

        visited.Remove(origin); // don't highlight current tile
        return new HashSet<Vector2Int>(visited.Keys);
    }

    private List<Vector2Int> GetTilesInRange(Vector2Int origin, int range)
    {
        var result = new List<Vector2Int>();
        for (int x = -range; x <= range; x++)
            for (int z = -range; z <= range; z++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(z) > range) continue;
                var cell = new Vector2Int(origin.x + x, origin.y + z);
                if (cell != origin) result.Add(cell);
            }
        return result;
    }

    private void PlaceHighlight(Vector2Int cell, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.SetParent(highlightParent.transform);
        go.transform.position  = new Vector3(cell.x, highlightY, cell.y);
        go.transform.rotation  = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.95f;
        go.GetComponent<Renderer>().material = mat;
        Destroy(go.GetComponent<Collider>());
        activeHighlights[cell] = go;
    }
}
```

**Scene Setup:**
- Create 4 Materials in `Assets/Materials/`:
  - `Highlight_Move.mat` — Standard, blue, Rendering Mode: Transparent, Alpha ~0.35
  - `Highlight_Attack.mat` — red, same settings
  - `Highlight_Hover.mat` — yellow
  - `Highlight_Spell.mat` — green
- Add `TileHighlighter` component to the `GridGenerator` GameObject
- Assign the 4 materials in the Inspector

---

### TASK 1.8 — Modify `PlayerMovement` for Combat
**Status:** `[ ]`
**File:** `Assets/Scripts/PlayerMovement.cs`
**Depends on:** `TurnManager`, `CombatActor`, `TileHighlighter`

**WHY:** During combat, the player must not be able to move freely — clicks must be validated against AP budget, and input must be locked during enemy turns.

**WHAT changes to `PlayerMovement.cs`:**
- Add `PlayerCombatActor` component check
- Block input when `!TurnManager.Instance.IsPlayerTurn`
- Count steps moved and call `SpendAP` accordingly
- After each step, recalculate tile highlights

**HOW:**
1. Open `Assets/Scripts/PlayerMovement.cs`
2. Add the following fields and modify `Update()` and `AdvanceToNextWaypoint()`

```csharp
// Add these fields inside PlayerMovement class:
private CombatActor combatActor;
private int stepsThisMove = 0;
private Vector2Int moveDestinationCell;

// Add to Start():
combatActor = GetComponent<CombatActor>();

// Replace the Update() method with:
void Update()
{
    // Lock input during enemy turns or when resolving
    var tm = TurnManager.Instance;
    if (tm != null && tm.State != CombatState.PlayerTurn && tm.State != CombatState.OutOfCombat)
        return;

    if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        return;

    Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
    int mask = groundLayer.value == 0 ? ~0 : (int)groundLayer;
    if (!Physics.Raycast(ray, out RaycastHit hit, 100f, mask)) return;

    Vector2Int startCell = WorldToCell(transform.position);
    Vector2Int endCell   = WorldToCell(hit.collider.transform.position);
    if (startCell == endCell) return;

    var walkable = GridGenerator.Instance?.WalkableTiles;
    if (walkable == null) return;

    // In combat: clamp path length to remaining AP
    List<Vector2Int> path = Pathfinder.FindPath(startCell, endCell, walkable);
    if (path == null || path.Count == 0) return;

    bool inCombat = tm != null && tm.State == CombatState.PlayerTurn;
    if (inCombat && combatActor != null)
    {
        int maxSteps = combatActor.CurrentAP; // 1 AP = 1 tile
        if (path.Count > maxSteps)
            path = path.GetRange(0, maxSteps);
    }

    pathQueue.Clear();
    isMoving = false;
    stepsThisMove = 0;
    moveDestinationCell = path[path.Count - 1];

    float playerY = transform.position.y;
    foreach (var cell in path)
        pathQueue.Enqueue(new Vector3(cell.x, playerY, cell.y));

    AdvanceToNextWaypoint();
}

// Modify AdvanceToNextWaypoint() — add AP spend when a step completes:
void AdvanceToNextWaypoint()
{
    // Spend AP for the step we just completed
    if (stepsThisMove > 0 && combatActor != null && TurnManager.Instance?.IsPlayerTurn == true)
    {
        combatActor.SpendAP(1);
        TileHighlighter.Instance?.ShowMovementRange(
            WorldToCell(transform.position), combatActor.CurrentAP);
    }

    if (pathQueue.Count == 0)
    {
        isMoving = false;
        return;
    }

    currentWaypoint = pathQueue.Dequeue();
    isMoving = true;
    stepsThisMove++;

    Vector3 dir = currentWaypoint - transform.position;
    if (dir.sqrMagnitude > 0.001f)
        transform.rotation = Quaternion.LookRotation(dir);
}
```

---

### TASK 1.9 — `PlayerCombatActor` (Player-Specific Subclass)
**Status:** `[ ]`
**File:** `Assets/Scripts/Combat/PlayerCombatActor.cs`
**Depends on:** `CombatActor`, `TurnManager`, `TileHighlighter`

**WHY:** `TurnManager` needs to distinguish the player from enemies. A thin subclass also handles player-specific start-of-turn logic (show highlights).

```csharp
// Assets/Scripts/Combat/PlayerCombatActor.cs
using UnityEngine;

public class PlayerCombatActor : CombatActor
{
    private Vector2Int PlayerCell =>
        new Vector2Int(Mathf.RoundToInt(transform.position.x),
                       Mathf.RoundToInt(transform.position.z));

    public override void TakeTurn()
    {
        // Show movement range at start of player turn
        TileHighlighter.Instance?.ShowMovementRange(PlayerCell, CurrentAP);
    }

    public override void OnCombatEnd()
    {
        TileHighlighter.Instance?.ClearAll();
    }
}
```

**Scene Setup:** On the Player GameObject, add `PlayerCombatActor` (it replaces the base `CombatActor` — remove the base component if added separately).

---

### TASK 1.10 — `CombatHUD` (Phase 1 minimal version)
**Status:** `[ ]`
**File:** `Assets/Scripts/UI/CombatHUD.cs`
**Depends on:** `TurnManager`, `CombatActor`, `CharacterStats`, `CombatLog`

**WHY:** The player needs to see their HP and AP at all times, and an End Turn button. Without this, combat is unplayable.

**WHAT (minimum viable HUD):**
- HP bar (Slider)
- AP dots (N circles showing current/max AP)
- End Turn button
- Combat log (ScrollRect with Text)

**HOW:**
1. In `SampleScene`, create a Canvas (`UI Scale Mode: Scale with Screen Size`)
2. Add the following child GameObjects:
   - `HPBar` — UI Slider, anchored bottom-left
   - `APPanel` — Horizontal Layout Group, 2 Image children named `AP_Dot_0`, `AP_Dot_1`
   - `EndTurnButton` — Button + Text "End Turn", anchored bottom-center
   - `CombatLogPanel` — ScrollRect with Content/Text child
3. Create `CombatHUD.cs` and attach to the Canvas

```csharp
// Assets/Scripts/UI/CombatHUD.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatHUD : MonoBehaviour
{
    [Header("HP")]
    public Slider    hpBar;
    public TMP_Text  hpLabel;

    [Header("AP")]
    public List<Image> apDots;          // assign in Inspector
    public Color apActiveColor   = Color.yellow;
    public Color apDepletedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("Turn")]
    public Button   endTurnButton;
    public TMP_Text turnLabel;

    [Header("Combat Log")]
    public TMP_Text combatLogText;
    public ScrollRect combatLogScroll;

    private CharacterStats playerStats;
    private CombatActor    playerActor;

    void Start()
    {
        // Find player components
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerStats = player.GetComponent<CharacterStats>();
            playerActor = player.GetComponent<CombatActor>();
        }

        // Hook events
        if (playerStats != null) playerStats.OnHpChanged  += UpdateHP;
        if (playerActor  != null) playerActor.OnAPChanged  += UpdateAP;

        TurnManager.Instance.OnTurnChanged  += UpdateTurnLabel;
        TurnManager.Instance.OnCombatEnded  += OnCombatEnded;
        TurnManager.Instance.OnCombatStarted += OnCombatStarted;

        endTurnButton.onClick.AddListener(() => TurnManager.Instance.EndTurn());
        CombatLog.OnEntry += AddLogEntry;

        gameObject.SetActive(false); // hidden until combat starts
    }

    void OnDestroy()
    {
        if (playerStats != null) playerStats.OnHpChanged -= UpdateHP;
        if (playerActor  != null) playerActor.OnAPChanged -= UpdateAP;
        CombatLog.OnEntry -= AddLogEntry;
    }

    private void OnCombatStarted()
    {
        gameObject.SetActive(true);
        RefreshAll();
    }

    private void OnCombatEnded()
    {
        gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        if (playerStats != null) UpdateHP(playerStats.currentHp, playerStats.MaxHp);
        if (playerActor  != null) UpdateAP(playerActor.CurrentAP, playerActor.maxAP);
    }

    private void UpdateHP(int current, int max)
    {
        if (hpBar   != null) hpBar.value       = (float)current / max;
        if (hpLabel != null) hpLabel.text       = $"{current} / {max}";
    }

    private void UpdateAP(int current, int max)
    {
        for (int i = 0; i < apDots.Count; i++)
        {
            if (i < max)
            {
                apDots[i].gameObject.SetActive(true);
                apDots[i].color = i < current ? apActiveColor : apDepletedColor;
            }
            else
            {
                apDots[i].gameObject.SetActive(false);
            }
        }
    }

    private void UpdateTurnLabel(ICombatant current)
    {
        bool isPlayer = current is PlayerCombatActor;
        if (turnLabel     != null) turnLabel.text = isPlayer ? "YOUR TURN" : $"{current.CombatantName}'s Turn";
        if (endTurnButton != null) endTurnButton.interactable = isPlayer;
    }

    private void AddLogEntry(string message, LogType type)
    {
        Color c = type switch
        {
            LogType.Damage => Color.red,
            LogType.Heal   => Color.green,
            LogType.Miss   => Color.yellow,
            LogType.Status => Color.cyan,
            _              => Color.white,
        };
        string hex = ColorUtility.ToHtmlStringRGB(c);
        combatLogText.text += $"\n<color=#{hex}>{message}</color>";

        // Scroll to bottom
        Canvas.ForceUpdateCanvases();
        combatLogScroll.verticalNormalizedPosition = 0f;
    }
}
```

---

### Phase 1 — Scene Setup Checklist
```
[ ] Create empty GameObject "TurnManager" → add TurnManager.cs
[ ] Create empty GameObject "TileHighlighter" (child of GridGenerator) → add TileHighlighter.cs
[ ] On Player GameObject: add PlayerCombatActor.cs, tag Player as "Player"
[ ] Create Canvas "CombatHUD" → add CombatHUD.cs → wire all Inspector fields
[ ] Create 4 highlight Materials in Assets/Materials/
[ ] Create End Turn Button and wire to CombatHUD
[ ] Test: manually call TurnManager.Instance.StartCombat() from a test script
```

---

## PHASE 2 — Enemy System

### TASK 2.1 — `EnemyData` ScriptableObject
**Status:** `[ ]`
**File:** `Assets/Scripts/Enemy/EnemyData.cs`
**Depends on:** `Item` (for loot table reference)

**WHY:** Hard-coding enemy stats in MonoBehaviours makes tuning painful. A ScriptableObject means designers can create new enemy types in the Unity editor without touching code.

```csharp
// Assets/Scripts/Enemy/EnemyData.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Runebearer/Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName   = "Enemy";
    public Sprite portrait;

    [Header("Stats")]
    public int   maxHP        = 20;
    public int   strength     = 10;
    public int   dexterity    = 10;
    public int   constitution = 10;
    public int   armorClass   = 11;
    public int   maxAP        = 2;

    [Header("Weapon")]
    public WeaponItem weapon;        // null = unarmed

    [Header("Loot")]
    [Range(0f, 1f)]
    public float dropChance   = 0.4f;
    public List<Item> possibleDrops = new List<Item>();
    public int   goldMin      = 0;
    public int   goldMax      = 5;
    public int   xpReward     = 50;

    [Header("Behaviour")]
    public int   detectionRadius = 6;   // tiles
    public int   attackRange     = 1;   // tiles (1 = melee)
    [Range(0f, 1f)]
    public float retreatThreshold = 0.2f; // retreat when HP < 20%
}
```

**Asset creation:** Right-click in Project → Runebearer/Enemy/EnemyData → create one asset per enemy type.

---

### TASK 2.2 — `EnemyStats` Component
**Status:** `[ ]`
**File:** `Assets/Scripts/Enemy/EnemyStats.cs`
**Depends on:** `EnemyData`, `CharacterStats`

**WHY:** Enemies need runtime stat state (current HP) but their base values come from `EnemyData`. This bridges the two.

```csharp
// Assets/Scripts/Enemy/EnemyStats.cs
using UnityEngine;

/// <summary>
/// Bootstraps CharacterStats from an EnemyData ScriptableObject at runtime.
/// Add alongside CharacterStats on every enemy prefab.
/// </summary>
[RequireComponent(typeof(CharacterStats))]
public class EnemyStats : MonoBehaviour
{
    public EnemyData data;

    void Awake()
    {
        if (data == null) return;
        var stats = GetComponent<CharacterStats>();
        stats.strength     = data.strength;
        stats.dexterity    = data.dexterity;
        stats.constitution = data.constitution;
        // currentHp is set in CharacterStats.Awake() via MaxHp
        // MaxHp uses CON modifier, so setting CON before Awake completes is enough
        // Force HP to data.maxHP override if needed:
        // stats.currentHp is set in CharacterStats.Awake — to override with data.maxHP,
        // use LateAwake pattern below.
    }

    void Start()
    {
        // After CharacterStats.Awake has run, override HP with explicit value
        var stats = GetComponent<CharacterStats>();
        stats.currentHp = data != null ? data.maxHP : stats.MaxHp;
    }
}
```

---

### TASK 2.3 — `EnemyAI` Component (FSM)
**Status:** `[ ]`
**File:** `Assets/Scripts/Enemy/EnemyAI.cs`
**Depends on:** `CombatActor`, `EnemyData`, `TurnManager`, `CombatResolver`, `Pathfinder`, `GridGenerator`

**WHY:** Enemies need autonomous decision-making within their AP budget. A Finite State Machine keeps each behaviour isolated and easy to extend.

**States:** `Idle → Alert → Chase → Attack → Retreat`

```csharp
// Assets/Scripts/Enemy/EnemyAI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyState { Idle, Alert, Chase, Attack, Retreat }

[RequireComponent(typeof(CombatActor))]
public class EnemyAI : MonoBehaviour, ICombatant
{
    [Header("Data")]
    public EnemyData data;

    public string CombatantName => data != null ? data.enemyName : gameObject.name;
    public int    Initiative    => GetComponent<CharacterStats>()?.Initiative ?? 0;
    public bool   IsAlive       => GetComponent<CharacterStats>()?.currentHp > 0;

    private EnemyState   state      = EnemyState.Idle;
    private CombatActor  actor;
    private Transform    playerTransform;
    private Vector2Int   CurrentCell => WorldToCell(transform.position);

    void Awake()
    {
        actor = GetComponent<CombatActor>();
        // Register self as ICombatant with EnemyAI instead of CombatActor
        // TurnManager.StartCombat() receives this component directly
    }

    void Start()
    {
        playerTransform = GameObject.FindWithTag("Player")?.transform;
    }

    // ── ICombatant ────────────────────────────────────────────────────
    public void TakeTurn()   => StartCoroutine(ExecuteTurn());
    public void TakeHit(DamageInfo info) => actor.TakeHit(info);
    public void OnCombatStart() { actor.RefreshAP(); EvaluateState(); }
    public void OnCombatEnd()   { state = EnemyState.Idle; }

    // ── Turn Execution ────────────────────────────────────────────────
    private IEnumerator ExecuteTurn()
    {
        EvaluateState();

        while (actor.CurrentAP > 0)
        {
            yield return new WaitForSeconds(0.4f); // pacing — feel deliberate

            switch (state)
            {
                case EnemyState.Chase:
                    if (!StepTowardPlayer()) goto done;
                    break;

                case EnemyState.Attack:
                    AttackPlayer();
                    goto done; // attacking costs all remaining AP

                case EnemyState.Retreat:
                    StepAwayFromPlayer();
                    goto done;

                default:
                    goto done;
            }

            EvaluateState();
        }

        done:
        TurnManager.Instance.EndTurn();
    }

    private void EvaluateState()
    {
        if (playerTransform == null) { state = EnemyState.Idle; return; }

        var stats = GetComponent<CharacterStats>();
        if (stats != null && data != null &&
            (float)stats.currentHp / data.maxHP <= data.retreatThreshold)
        {
            state = EnemyState.Retreat;
            return;
        }

        int dist = ManhattanDistance(CurrentCell, WorldToCell(playerTransform.position));

        if (dist <= (data?.attackRange ?? 1))
            state = EnemyState.Attack;
        else if (dist <= (data?.detectionRadius ?? 6))
            state = EnemyState.Chase;
        else
            state = EnemyState.Idle;
    }

    // ── Actions ───────────────────────────────────────────────────────
    private bool StepTowardPlayer()
    {
        if (!actor.SpendAP(1)) return false;

        var walkable = GridGenerator.Instance?.WalkableTiles;
        if (walkable == null) return false;

        var path = Pathfinder.FindPath(CurrentCell, WorldToCell(playerTransform.position), walkable);
        if (path == null || path.Count == 0) return false;

        // Move one step (block enemy tiles from walkable for pathfinding in future)
        Vector2Int next = path[0];
        transform.position = new Vector3(next.x, transform.position.y, next.y);
        return true;
    }

    private void AttackPlayer()
    {
        if (!actor.SpendAP(1)) return;

        var playerActor = playerTransform?.GetComponent<CombatActor>();
        if (playerActor == null) return;

        var result = CombatResolver.ResolveAttack(actor, playerActor);
        if (result.HasValue)
            playerActor.TakeHit(result.Value);
    }

    private void StepAwayFromPlayer()
    {
        if (!actor.SpendAP(1)) return;
        // Simple: move in the direction opposite to player
        var dir = (CurrentCell - WorldToCell(playerTransform.position));
        dir = new Vector2Int(
            Mathf.Clamp(dir.x, -1, 1),
            Mathf.Clamp(dir.y, -1, 1));
        var next = CurrentCell + dir;
        var walkable = GridGenerator.Instance?.WalkableTiles;
        if (walkable != null && walkable.Contains(next))
            transform.position = new Vector3(next.x, transform.position.y, next.y);
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static Vector2Int WorldToCell(Vector3 pos) =>
        new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));

    private static int ManhattanDistance(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
```

---

### TASK 2.4 — `EnemySpawner`
**Status:** `[ ]`
**File:** `Assets/Scripts/Enemy/EnemySpawner.cs`
**Depends on:** `EnemyData`, `EnemyAI`, `GridGenerator`, `TurnManager`

**WHY:** Enemies must be placed on walkable tiles that are not too close to the player start. The spawner also kicks off combat by calling `TurnManager.StartCombat()`.

```csharp
// Assets/Scripts/Enemy/EnemySpawner.cs
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Config")]
    public GameObject    enemyPrefab;      // prefab with EnemyAI + CombatActor + CharacterStats
    public List<EnemyData> enemyPool;      // pick from this list
    public int           enemyCount  = 3;
    public int           minDistFromPlayer = 5;  // tiles

    private List<EnemyAI> spawnedEnemies = new List<EnemyAI>();

    void Start()
    {
        SpawnEnemies();
        BeginCombat();
    }

    private void SpawnEnemies()
    {
        if (enemyPrefab == null || enemyPool.Count == 0) return;

        var walkable = GridGenerator.Instance?.WalkableTiles;
        if (walkable == null) return;

        var player  = GameObject.FindWithTag("Player");
        Vector2Int playerCell = player != null
            ? new Vector2Int(Mathf.RoundToInt(player.transform.position.x),
                             Mathf.RoundToInt(player.transform.position.z))
            : Vector2Int.zero;

        var validTiles = new List<Vector2Int>();
        foreach (var tile in walkable)
        {
            int dist = Mathf.Abs(tile.x - playerCell.x) + Mathf.Abs(tile.y - playerCell.y);
            if (dist >= minDistFromPlayer)
                validTiles.Add(tile);
        }

        for (int i = 0; i < enemyCount && validTiles.Count > 0; i++)
        {
            int idx  = Random.Range(0, validTiles.Count);
            var cell = validTiles[idx];
            validTiles.RemoveAt(idx);

            var go  = Instantiate(enemyPrefab,
                                  new Vector3(cell.x, 0.7f, cell.y),
                                  Quaternion.identity);
            var ai  = go.GetComponent<EnemyAI>();
            var es  = go.GetComponent<EnemyStats>();

            // Assign a random EnemyData
            var data    = enemyPool[Random.Range(0, enemyPool.Count)];
            ai.data     = data;
            if (es != null) es.data = data;

            spawnedEnemies.Add(ai);
        }
    }

    private void BeginCombat()
    {
        var player    = GameObject.FindWithTag("Player")?.GetComponent<PlayerCombatActor>();
        if (player == null) return;

        var combatants = new List<ICombatant> { player };
        foreach (var e in spawnedEnemies)
            combatants.Add(e);

        TurnManager.Instance.StartCombat(combatants);
    }
}
```

---

## PHASE 3 — Dungeon Generation

### TASK 3.1 — BSP Room Generator
**Status:** `[ ]`
**File:** `Assets/Scripts/Dungeon/DungeonGenerator.cs`
**Depends on:** `GridGenerator` (replaces/extends it), `FloorManager`

**WHY:** The current Perlin noise map has no guaranteed rooms or corridors, making level design impossible. BSP produces predictable rectangular rooms connected by corridors — the standard roguelike layout.

**WHAT:**
- Binary Space Partitioning splits the grid recursively into regions
- Each leaf region gets one room
- Rooms are connected by L-shaped corridors
- Exposes `WalkableTiles` (compatible with existing `Pathfinder`)

**HOW:**
1. Create `Assets/Scripts/Dungeon/DungeonGenerator.cs`
2. In the scene: disable or remove `GridGenerator` — replace with `DungeonGenerator` on the same GameObject (keep the `Instance` reference pattern)
3. Keep `GridGenerator.Instance.WalkableTiles` populated by having `DungeonGenerator` set a static reference or implement the same interface

```csharp
// Assets/Scripts/Dungeon/DungeonGenerator.cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Room
{
    public RectInt bounds;
    public Vector2Int center => new Vector2Int(bounds.x + bounds.width / 2,
                                               bounds.y + bounds.height / 2);
    public RoomType type = RoomType.Normal;
}

public enum RoomType { Normal, Start, Boss, Loot, Shrine }

public class DungeonGenerator : MonoBehaviour
{
    public static DungeonGenerator Instance { get; private set; }
    public HashSet<Vector2Int> WalkableTiles { get; private set; } = new();

    [Header("Prefabs")]
    public GameObject floorTilePrefab;
    public GameObject wallTilePrefab;

    [Header("BSP Settings")]
    public int  mapWidth       = 60;
    public int  mapHeight      = 60;
    public int  minRoomSize    = 5;
    public int  maxRoomSize    = 12;
    public int  splitIterations = 4;

    [Header("Player")]
    public GameObject player;
    public float      playerHeightOffset = 0.7f;

    public List<Room> Rooms { get; private set; } = new();

    void Awake() => Instance = this;

    void Start() => Generate();

    public void Generate()
    {
        Rooms.Clear();
        WalkableTiles.Clear();

        // 1. BSP split
        var rootArea = new RectInt(0, 0, mapWidth, mapHeight);
        var leaves   = new List<RectInt>();
        SplitArea(rootArea, splitIterations, leaves);

        // 2. Carve a room in each leaf
        var roomCenters = new List<Vector2Int>();
        Room startRoom  = null;

        foreach (var leaf in leaves)
        {
            var room  = CarveRoom(leaf);
            Rooms.Add(room);
            roomCenters.Add(room.center);
            if (startRoom == null) { startRoom = room; room.type = RoomType.Start; }
        }

        // Mark boss room (furthest from start)
        Room bossRoom = null;
        float maxDist = 0;
        foreach (var r in Rooms)
        {
            float d = Vector2Int.Distance(startRoom.center, r.center);
            if (d > maxDist) { maxDist = d; bossRoom = r; }
        }
        if (bossRoom != null && bossRoom != startRoom)
            bossRoom.type = RoomType.Boss;

        // 3. Connect rooms with corridors (simple: connect each to nearest neighbour)
        ConnectRooms();

        // 4. Instantiate tiles
        SpawnTiles();

        // 5. Place player in start room
        if (startRoom != null && player != null)
        {
            var c = startRoom.center;
            player.transform.position = new Vector3(c.x, playerHeightOffset, c.y);
        }
    }

    private void SplitArea(RectInt area, int depth, List<RectInt> output)
    {
        if (depth == 0 || area.width < minRoomSize * 2 || area.height < minRoomSize * 2)
        {
            output.Add(area);
            return;
        }

        bool splitH = area.height > area.width;
        if (splitH)
        {
            int split = Random.Range(area.y + minRoomSize, area.y + area.height - minRoomSize);
            SplitArea(new RectInt(area.x, area.y, area.width, split - area.y), depth - 1, output);
            SplitArea(new RectInt(area.x, split, area.width, area.y + area.height - split), depth - 1, output);
        }
        else
        {
            int split = Random.Range(area.x + minRoomSize, area.x + area.width - minRoomSize);
            SplitArea(new RectInt(area.x, area.y, split - area.x, area.height), depth - 1, output);
            SplitArea(new RectInt(split, area.y, area.x + area.width - split, area.height), depth - 1, output);
        }
    }

    private Room CarveRoom(RectInt leaf)
    {
        int w    = Random.Range(minRoomSize, Mathf.Min(maxRoomSize, leaf.width  - 1));
        int h    = Random.Range(minRoomSize, Mathf.Min(maxRoomSize, leaf.height - 1));
        int x    = Random.Range(leaf.x + 1, leaf.x + leaf.width  - w);
        int y    = Random.Range(leaf.y + 1, leaf.y + leaf.height - h);

        var room = new Room { bounds = new RectInt(x, y, w, h) };

        for (int rx = x; rx < x + w; rx++)
            for (int ry = y; ry < y + h; ry++)
                WalkableTiles.Add(new Vector2Int(rx, ry));

        return room;
    }

    private void ConnectRooms()
    {
        // Connect each room to the next in list with an L-corridor
        for (int i = 0; i < Rooms.Count - 1; i++)
            CarveCorridor(Rooms[i].center, Rooms[i + 1].center);
    }

    private void CarveCorridor(Vector2Int a, Vector2Int b)
    {
        // Horizontal then vertical (L-shape)
        int x = a.x;
        while (x != b.x)
        {
            WalkableTiles.Add(new Vector2Int(x, a.y));
            x += x < b.x ? 1 : -1;
        }
        int y = a.y;
        while (y != b.y)
        {
            WalkableTiles.Add(new Vector2Int(b.x, y));
            y += y < b.y ? 1 : -1;
        }
        WalkableTiles.Add(b);
    }

    private void SpawnTiles()
    {
        foreach (var cell in WalkableTiles)
            Instantiate(floorTilePrefab,
                        new Vector3(cell.x, 0f, cell.y),
                        Quaternion.identity, transform);
    }
}
```

**Migration note:** Update all references from `GridGenerator.Instance.WalkableTiles` to `DungeonGenerator.Instance.WalkableTiles` (or make GridGenerator forward to DungeonGenerator). The cleanest approach is to add `public static HashSet<Vector2Int> WalkableTiles` to a shared static `MapData` class both can write to.

---

### TASK 3.2 — `FloorManager`
**Status:** `[ ]`
**File:** `Assets/Scripts/Dungeon/FloorManager.cs`
**Depends on:** `DungeonGenerator`, `EnemySpawner`, `TurnManager`

**WHY:** Someone must track floor depth, know when all enemies are dead, and trigger the next floor. FloorManager owns this responsibility.

```csharp
// Assets/Scripts/Dungeon/FloorManager.cs
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FloorManager : MonoBehaviour
{
    public static FloorManager Instance { get; private set; }
    public int FloorDepth { get; private set; } = 1;

    [Header("Settings")]
    public int enemiesPerFloorBase = 3;
    public float enemiesPerFloorScale = 1.5f;

    private int enemiesRemaining;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterEnemy(EnemyAI enemy)
    {
        enemiesRemaining++;
        enemy.GetComponent<CharacterStats>().OnDeath += () => OnEnemyDied(enemy);
    }

    private void OnEnemyDied(EnemyAI enemy)
    {
        enemiesRemaining--;
        CombatLog.Log($"{enemy.data?.enemyName ?? "Enemy"} defeated. {enemiesRemaining} remaining.");

        if (enemiesRemaining <= 0)
            OnFloorCleared();
    }

    private void OnFloorCleared()
    {
        TurnManager.Instance.EndCombat();
        CombatLog.Log($"Floor {FloorDepth} cleared! Find the stairs to descend.");
        // TODO Phase 9: show floor summary UI, reveal exit stairs tile
    }

    public void GoToNextFloor()
    {
        FloorDepth++;
        // Reload scene — FloorManager survives via DontDestroyOnLoad
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public int EnemiesForCurrentFloor() =>
        Mathf.RoundToInt(enemiesPerFloorBase * Mathf.Pow(enemiesPerFloorScale, FloorDepth - 1));
}
```

---

### TASK 3.3 — `FogOfWar`
**Status:** `[ ]`
**File:** `Assets/Scripts/Dungeon/FogOfWar.cs`
**Depends on:** `DungeonGenerator`, `PlayerMovement`

**WHY:** Fog of war is the defining visual of a dungeon crawler. It creates tension and makes exploration meaningful.

**WHAT:**
- Three states per tile: `Hidden` (black overlay), `Revealed` (dark overlay), `Visible` (clear)
- Recalculates visible tiles every time the player moves (using ray-cast BFS limited by vision radius)

```csharp
// Assets/Scripts/Dungeon/FogOfWar.cs
using System.Collections.Generic;
using UnityEngine;

public enum TileVisibility { Hidden, Revealed, Visible }

public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance { get; private set; }

    [Header("Settings")]
    public int   visionRadius  = 6;
    public float fogY          = 0.03f;

    [Header("Materials")]
    public Material hiddenMat;    // opaque black
    public Material revealedMat;  // dark transparent
    // Visible tiles have no overlay

    private Dictionary<Vector2Int, TileVisibility> visibility = new();
    private Dictionary<Vector2Int, GameObject>     fogObjects = new();

    void Awake() => Instance = this;

    void Start()
    {
        InitFog();
        var player = GameObject.FindWithTag("Player");
        if (player != null)
            player.GetComponent<PlayerMovement>(); // hook into move events in Task 3.3b
        // For now, update every frame (optimise later to only on player move)
    }

    void Update()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var cell = WorldToCell(player.transform.position);
        UpdateVisibility(cell);
    }

    private void InitFog()
    {
        var walkable = DungeonGenerator.Instance?.WalkableTiles;
        if (walkable == null) return;

        foreach (var cell in walkable)
        {
            visibility[cell] = TileVisibility.Hidden;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.position  = new Vector3(cell.x, fogY, cell.y);
            go.transform.rotation  = Quaternion.Euler(90f, 0f, 0f);
            go.transform.SetParent(transform);
            go.GetComponent<Renderer>().material = hiddenMat;
            Destroy(go.GetComponent<Collider>());
            fogObjects[cell] = go;
        }
    }

    private void UpdateVisibility(Vector2Int playerCell)
    {
        // Mark previously visible as revealed
        foreach (var kv in visibility)
            if (kv.Value == TileVisibility.Visible)
                visibility[kv.Key] = TileVisibility.Revealed;

        // BFS vision
        var visible = RaycastVision(playerCell);

        foreach (var cell in visible)
        {
            visibility[cell] = TileVisibility.Visible;
        }

        // Apply materials
        foreach (var kv in fogObjects)
        {
            var mat = visibility[kv.Key] switch
            {
                TileVisibility.Visible  => null,       // no overlay
                TileVisibility.Revealed => revealedMat,
                _                       => hiddenMat,
            };

            if (mat == null) kv.Value.SetActive(false);
            else
            {
                kv.Value.SetActive(true);
                kv.Value.GetComponent<Renderer>().material = mat;
            }
        }
    }

    private HashSet<Vector2Int> RaycastVision(Vector2Int origin)
    {
        var result   = new HashSet<Vector2Int> { origin };
        var walkable = DungeonGenerator.Instance?.WalkableTiles;
        if (walkable == null) return result;

        // Cast rays to all tiles within radius
        for (int x = -visionRadius; x <= visionRadius; x++)
            for (int z = -visionRadius; z <= visionRadius; z++)
            {
                if (x * x + z * z > visionRadius * visionRadius) continue;
                var target = new Vector2Int(origin.x + x, origin.y + z);
                if (!walkable.Contains(target)) continue;
                if (HasLineOfSight(origin, target, walkable))
                    result.Add(target);
            }

        return result;
    }

    private bool HasLineOfSight(Vector2Int from, Vector2Int to, HashSet<Vector2Int> walkable)
    {
        // Bresenham's line
        int x0 = from.x, y0 = from.y, x1 = to.x, y1 = to.y;
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 == x1 && y0 == y1) break;
            if (!walkable.Contains(new Vector2Int(x0, y0))) return false;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 <  dx) { err += dx; y0 += sy; }
        }
        return true;
    }

    private static Vector2Int WorldToCell(Vector3 pos) =>
        new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
}
```

---

## PHASE 4 — Rune System

### TASK 4.1 — `RuneData` ScriptableObject
**Status:** `[ ]`
**File:** `Assets/Scripts/Runes/RuneData.cs`
**Depends on:** `Item` (extends it so runes can appear in inventory)

**WHY:** Runes are items that the player equips, so they belong in the inventory system. Extending `Item` means all existing inventory/tooltip UI works for free.

```csharp
// Assets/Scripts/Runes/RuneData.cs
using UnityEngine;

public enum RuneType    { Passive, Active, Field }
public enum RuneRarity  { Common, Uncommon, Rare, Legendary }

[CreateAssetMenu(fileName = "NewRune", menuName = "Runebearer/Runes/RuneData")]
public class RuneData : Item
{
    [Header("Rune")]
    public RuneType   runeType;
    public RuneRarity runeRarity;
    public int        levelRequirement = 1;

    [Header("Passive Effect (if Passive rune)")]
    public PassiveRuneEffectType passiveEffect;
    public float effectValue;       // meaning depends on effect type

    [Header("Field Rune (if Field rune)")]
    public int   fieldDuration = 3; // turns before it expires
    public int   fieldRadius   = 1;
    public float fieldDamage   = 5f;
    public DamageType fieldDamageType = DamageType.Fire;

    [Header("Active Rune (if Active rune)")]
    public ActiveRuneEffectType activeEffect;
    public float activeEffectValue = 1f;

    void Awake()
    {
        type      = ItemType.Misc;  // shown in inventory
        stackable = false;
    }
}

public enum PassiveRuneEffectType
{
    None,
    FlatDamageReduction,    // Rune of Iron
    BonusAP,                // Rune of Swiftness
    MaxHPPercent,           // Rune of Vitality
    SpellAPReduction,       // Rune of Attunement
    LootQualityBonus,       // Rune of Fortune
    HitNegationChance,      // Rune of Warding
}

public enum ActiveRuneEffectType
{
    None,
    BonusRange,             // Rune of Reach
    Echo,                   // Rune of Echo
    Seeking,                // Rune of Seeking
    ArmorPiercing,          // Rune of Piercing
    Chain,                  // Rune of Chain
    Overload,               // Rune of Overload
}
```

---

### TASK 4.2 — Passive Rune Slots in `Equipment`
**Status:** `[ ]`
**File:** `Assets/Scripts/Character/Equipment.cs` (modify)
**Depends on:** `RuneData`

**WHY:** Passive runes must be persistently equipped, not just in inventory. The equipment component is the right owner.

**Add to `Equipment.cs`:**
```csharp
// Add these fields:
[Header("Passive Rune Slots")]
public RuneData[] passiveRunes = new RuneData[3];  // 3 slots max

// Add these methods:
public bool EquipPassiveRune(RuneData rune, int slot)
{
    if (slot < 0 || slot >= passiveRunes.Length) return false;
    if (rune.runeType != RuneType.Passive) return false;

    // Return old rune to inventory
    if (passiveRunes[slot] != null)
        GetComponent<Inventory>()?.AddItem(passiveRunes[slot]);

    passiveRunes[slot] = rune;
    GetComponent<Inventory>()?.RemoveItem(rune);
    OnEquipmentChanged?.Invoke();
    return true;
}

public void UnequipPassiveRune(int slot)
{
    if (passiveRunes[slot] == null) return;
    GetComponent<Inventory>()?.AddItem(passiveRunes[slot]);
    passiveRunes[slot] = null;
    OnEquipmentChanged?.Invoke();
}

// Apply all passive rune effects — call this from CombatActor or CharacterStats
public int GetPassiveDamageReduction()
{
    int total = 0;
    foreach (var r in passiveRunes)
        if (r != null && r.passiveEffect == PassiveRuneEffectType.FlatDamageReduction)
            total += Mathf.RoundToInt(r.effectValue);
    return total;
}

public int GetBonusAP()
{
    int total = 0;
    foreach (var r in passiveRunes)
        if (r != null && r.passiveEffect == PassiveRuneEffectType.BonusAP)
            total += Mathf.RoundToInt(r.effectValue);
    return total;
}

public bool RollHitNegation()
{
    foreach (var r in passiveRunes)
        if (r != null && r.passiveEffect == PassiveRuneEffectType.HitNegationChance)
            if (Random.value < r.effectValue)
                return true;
    return false;
}
```

**Also update `CombatActor.TakeHit()`:**
```csharp
public virtual void TakeHit(DamageInfo info)
{
    var equip = GetComponent<Equipment>();

    // Passive: hit negation
    if (equip != null && equip.RollHitNegation())
    {
        CombatLog.Log($"{CombatantName} negates the hit!", LogType.Status);
        return;
    }

    int dr  = (equip?.TotalDamageReduction() ?? 0) + (equip?.GetPassiveDamageReduction() ?? 0);
    int net = Mathf.Max(1, info.Amount - dr);
    stats?.TakeDamage(net);

    CombatLog.Log($"{CombatantName} takes {net} {info.Type}" +
                  (info.IsCritical ? " (CRIT!)" : ""), LogType.Damage);
}
```

**Also update `CombatActor.RefreshAP()`:**
```csharp
public void RefreshAP()
{
    int bonus = GetComponent<Equipment>()?.GetBonusAP() ?? 0;
    CurrentAP = maxAP + bonus;
    OnAPChanged?.Invoke(CurrentAP, maxAP + bonus);
}
```

---

### TASK 4.3 — `FieldRuneInstance`
**Status:** `[ ]`
**File:** `Assets/Scripts/Runes/FieldRuneInstance.cs`
**Depends on:** `RuneData`, `TurnManager`, `CombatActor`

**WHY:** Field runes are persistent world objects that affect combat. They need to track duration and apply effects each turn.

```csharp
// Assets/Scripts/Runes/FieldRuneInstance.cs
using System.Collections.Generic;
using UnityEngine;

public class FieldRuneInstance : MonoBehaviour
{
    public RuneData data;
    public int      turnsRemaining;
    public Vector2Int Cell => new Vector2Int(
        Mathf.RoundToInt(transform.position.x),
        Mathf.RoundToInt(transform.position.z));

    private GameObject placer;   // who placed it (for siphon heal)

    public void Initialize(RuneData runeData, int duration, GameObject owner)
    {
        data           = runeData;
        turnsRemaining = duration;
        placer         = owner;

        // Block this tile from pathfinding
        GridGenerator.Instance?.WalkableTiles.Remove(Cell);

        // Subscribe to turn changes to tick each player turn
        TurnManager.Instance.OnTurnChanged += OnTurnAdvanced;
    }

    private void OnTurnAdvanced(ICombatant current)
    {
        if (!(current is PlayerCombatActor)) return;  // tick on player turns

        turnsRemaining--;
        ApplyEffect();

        if (turnsRemaining <= 0)
            DestroyRune();
    }

    private void ApplyEffect()
    {
        // Find all enemies in radius
        var enemies = FindEnemiesInRadius(Cell, data.fieldRadius);

        foreach (var enemy in enemies)
        {
            switch (data.runeType)
            {
                // Field rune type determines effect — use fieldDamage/fieldDamageType
                case RuneType.Field when data.itemName.Contains("Flame"):
                case RuneType.Field when data.itemName.Contains("Frost"):
                    var hit = new DamageInfo(
                        Mathf.RoundToInt(data.fieldDamage),
                        data.fieldDamageType,
                        placer);
                    enemy.TakeHit(hit);
                    break;
            }
        }
    }

    private List<CombatActor> FindEnemiesInRadius(Vector2Int center, int radius)
    {
        var result = new List<CombatActor>();
        var all    = FindObjectsOfType<CombatActor>();
        foreach (var actor in all)
        {
            if (actor is PlayerCombatActor) continue;
            var cell = new Vector2Int(
                Mathf.RoundToInt(actor.transform.position.x),
                Mathf.RoundToInt(actor.transform.position.z));
            int dist = Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y);
            if (dist <= radius)
                result.Add(actor);
        }
        return result;
    }

    private void DestroyRune()
    {
        TurnManager.Instance.OnTurnChanged -= OnTurnAdvanced;
        // Restore walkability
        GridGenerator.Instance?.WalkableTiles.Add(Cell);
        Destroy(gameObject);
    }
}
```

---

## PHASE 5 — Spell System

### TASK 5.1 — `SpellData` ScriptableObject
**Status:** `[ ]`
**File:** `Assets/Scripts/Spells/SpellData.cs`
**Depends on:** `DamageType` (from WeaponItem.cs)

```csharp
// Assets/Scripts/Spells/SpellData.cs
using UnityEngine;

public enum SpellSchool   { Arcane, Necrotic, Runic }
public enum AoEShape      { Single, Line, Cone, Radius }

[CreateAssetMenu(fileName = "NewSpell", menuName = "Runebearer/Spells/SpellData")]
public class SpellData : ScriptableObject
{
    [Header("Identity")]
    public string     spellName    = "Unnamed Spell";
    [TextArea]
    public string     description  = "";
    public Sprite     icon;
    public SpellSchool school      = SpellSchool.Arcane;

    [Header("Cost & Cooldown")]
    public int        apCost       = 1;
    public int        cooldownTurns = 0;   // 0 = usable every turn

    [Header("Range & Area")]
    public int        range        = 4;    // tiles
    public AoEShape   aoeShape     = AoEShape.Single;
    public int        aoeRadius    = 0;    // for Radius shape

    [Header("Damage")]
    public int        damageDiceCount = 1;
    public int        damageDiceSides = 8;
    public DamageType damageType   = DamageType.Arcane;
    public bool       usesINT      = true;   // false = WIS (necrotic)

    [Header("Status Effect")]
    public bool       appliesStatus = false;
    // (Phase 6: add StatusEffectType field here)

    [Header("Unlock")]
    public int        levelRequirement = 1;

    public int RollDamage(CharacterStats caster)
    {
        int total = 0;
        for (int i = 0; i < damageDiceCount; i++)
            total += UnityEngine.Random.Range(1, damageDiceSides + 1);
        int mod = usesINT
            ? CharacterStats.Modifier(caster.intelligence)
            : CharacterStats.Modifier(caster.wisdom);
        return Mathf.Max(1, total + mod);
    }
}
```

---

### TASK 5.2 — `SpellCaster` Component
**Status:** `[ ]`
**File:** `Assets/Scripts/Spells/SpellCaster.cs`
**Depends on:** `SpellData`, `CombatActor`, `TurnManager`, `CombatResolver`

**WHY:** The player needs to hold, track cooldowns for, and cast up to 8 spells. This component owns the spell bar state.

```csharp
// Assets/Scripts/Spells/SpellCaster.cs
using System.Collections.Generic;
using UnityEngine;

public class SpellCaster : MonoBehaviour
{
    public static int MaxSpells = 8;

    public List<SpellData>       knownSpells  = new List<SpellData>();   // all learned spells
    public SpellData[]           equippedSpells = new SpellData[MaxSpells];
    private Dictionary<SpellData, int> cooldowns = new();

    private CombatActor actor;

    void Awake()
    {
        actor = GetComponent<CombatActor>();
        TurnManager.Instance.OnTurnChanged += OnTurnChanged;
    }

    void OnDestroy() => TurnManager.Instance.OnTurnChanged -= OnTurnChanged;

    // ── Casting ───────────────────────────────────────────────────────
    public bool CanCast(SpellData spell)
    {
        if (spell == null) return false;
        if (!actor.CanSpend(spell.apCost)) return false;
        if (IsOnCooldown(spell)) return false;
        return true;
    }

    public void CastAtTarget(SpellData spell, ICombatant target)
    {
        if (!CanCast(spell)) return;
        if (!actor.SpendAP(spell.apCost)) return;

        var stats = GetComponent<CharacterStats>();

        // Resolve hits based on AoE shape
        switch (spell.aoeShape)
        {
            case AoEShape.Single:
                HitTarget(spell, target, stats);
                break;
            case AoEShape.Radius:
                // Hit all combatants within radius of target
                // TODO Phase 5: implement AoE lookup
                HitTarget(spell, target, stats);
                break;
        }

        // Set cooldown
        if (spell.cooldownTurns > 0)
            cooldowns[spell] = spell.cooldownTurns;
    }

    private void HitTarget(SpellData spell, ICombatant target, CharacterStats caster)
    {
        int damage = spell.RollDamage(caster);
        var info   = new DamageInfo(damage, spell.damageType, gameObject);
        target.TakeHit(info);
        CombatLog.Log($"{gameObject.name} casts {spell.spellName} → {damage} {spell.damageType}", LogType.Damage);
    }

    // ── Cooldowns ─────────────────────────────────────────────────────
    public bool IsOnCooldown(SpellData spell) =>
        cooldowns.TryGetValue(spell, out int cd) && cd > 0;

    public int GetCooldown(SpellData spell) =>
        cooldowns.TryGetValue(spell, out int cd) ? cd : 0;

    private void OnTurnChanged(ICombatant current)
    {
        if (!(current is PlayerCombatActor)) return;
        // Tick down all cooldowns
        var keys = new List<SpellData>(cooldowns.Keys);
        foreach (var k in keys)
            if (cooldowns[k] > 0) cooldowns[k]--;
    }

    // ── Learning ──────────────────────────────────────────────────────
    public void LearnSpell(SpellData spell)
    {
        if (!knownSpells.Contains(spell))
            knownSpells.Add(spell);
    }
}
```

---

## PHASE 6 — Status Effects

### TASK 6.1 — `StatusEffect` Base & `StatusEffectManager`
**Status:** `[ ]`
**Files:** `Assets/Scripts/Combat/StatusEffect.cs`, `Assets/Scripts/Combat/StatusEffectManager.cs`
**Depends on:** `CombatActor`, `TurnManager`

```csharp
// Assets/Scripts/Combat/StatusEffect.cs
using UnityEngine;

public abstract class StatusEffect
{
    public string    EffectName  { get; protected set; }
    public Sprite    Icon        { get; protected set; }
    public int       Duration    { get; set; }           // turns remaining
    public bool      IsExpired   => Duration <= 0;

    public abstract void OnApply(CombatActor target);
    public abstract void OnTick(CombatActor target);     // called each turn
    public abstract void OnRemove(CombatActor target);
}
```

```csharp
// Assets/Scripts/Combat/StatusEffectManager.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CombatActor))]
public class StatusEffectManager : MonoBehaviour
{
    private List<StatusEffect> effects = new List<StatusEffect>();
    private CombatActor actor;

    public IReadOnlyList<StatusEffect> Effects => effects;

    public event System.Action OnEffectsChanged;

    void Awake()
    {
        actor = GetComponent<CombatActor>();
        TurnManager.Instance.OnTurnChanged += OnTurnAdvanced;
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnChanged -= OnTurnAdvanced;
    }

    public void ApplyEffect(StatusEffect effect)
    {
        // Check for existing same-type effect (refresh duration instead of stacking)
        var existing = effects.Find(e => e.EffectName == effect.EffectName);
        if (existing != null)
        {
            existing.Duration = Mathf.Max(existing.Duration, effect.Duration);
            CombatLog.Log($"{gameObject.name}: {effect.EffectName} refreshed", LogType.Status);
        }
        else
        {
            effects.Add(effect);
            effect.OnApply(actor);
            CombatLog.Log($"{gameObject.name} is {effect.EffectName}", LogType.Status);
        }
        OnEffectsChanged?.Invoke();
    }

    private void OnTurnAdvanced(ICombatant current)
    {
        // Tick effects on this actor's own turn
        if (current != (ICombatant)actor) return;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            effects[i].OnTick(actor);
            effects[i].Duration--;
            if (effects[i].IsExpired)
            {
                effects[i].OnRemove(actor);
                CombatLog.Log($"{gameObject.name}: {effects[i].EffectName} expired", LogType.Status);
                effects.RemoveAt(i);
            }
        }
        OnEffectsChanged?.Invoke();
    }
}
```

**Concrete effects (create one file per effect in `Assets/Scripts/Combat/Effects/`):**

```csharp
// BurnEffect.cs
public class BurnEffect : StatusEffect
{
    private int damagePerTurn;
    public BurnEffect(int dmg, int turns)
    {
        EffectName    = "Burn";
        damagePerTurn = dmg;
        Duration      = turns;
    }
    public override void OnApply(CombatActor t)  { }
    public override void OnTick(CombatActor t)
        => t.TakeHit(new DamageInfo(damagePerTurn, DamageType.Fire, null));
    public override void OnRemove(CombatActor t) { }
}

// SlowedEffect.cs
public class SlowedEffect : StatusEffect
{
    public SlowedEffect(int turns) { EffectName = "Slowed"; Duration = turns; }
    public override void OnApply(CombatActor t)  => t.SpendAP(1); // lose 1 AP this turn
    public override void OnTick(CombatActor t)   { }
    public override void OnRemove(CombatActor t) { }
}

// RootedEffect.cs
public class RootedEffect : StatusEffect
{
    public RootedEffect(int turns) { EffectName = "Rooted"; Duration = turns; }
    public override void OnApply(CombatActor t)  { /* set movement blocked flag */ }
    public override void OnTick(CombatActor t)   { }
    public override void OnRemove(CombatActor t) { /* clear movement blocked flag */ }
}
```

---

## PHASE 7 — Full HUD (extends Task 1.10)

### TASK 7.1 — Turn Order Strip
**Status:** `[ ]`
**File:** `Assets/Scripts/UI/TurnOrderUI.cs`
**Depends on:** `TurnManager`, `CharacterStats`

```csharp
// Assets/Scripts/UI/TurnOrderUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnOrderUI : MonoBehaviour
{
    [System.Serializable]
    public class TurnSlot
    {
        public Image    portrait;
        public Image    hpBar;
        public TMP_Text nameLabel;
    }

    public List<TurnSlot> slots = new List<TurnSlot>(); // assign 5 slots in Inspector

    void Start()
    {
        TurnManager.Instance.OnTurnChanged += _ => Refresh();
        TurnManager.Instance.OnCombatStarted += Refresh;
    }

    private void Refresh()
    {
        // TurnManager exposes turnOrder list — add a public accessor
        // For now, show placeholder: current combatant name
        // Full implementation: iterate TurnManager.TurnOrder from currentIndex
    }
}
```

**Note:** Add `public IReadOnlyList<ICombatant> TurnOrder => turnOrder;` to `TurnManager`.

---

### TASK 7.2 — Spell Bar UI
**Status:** `[ ]`
**File:** `Assets/Scripts/UI/SpellBarUI.cs`
**Depends on:** `SpellCaster`, `TurnManager`

```csharp
// Assets/Scripts/UI/SpellBarUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpellBarUI : MonoBehaviour
{
    [System.Serializable]
    public class SpellSlotUI
    {
        public Image    icon;
        public Image    cooldownOverlay;
        public TMP_Text cooldownLabel;
        public TMP_Text hotkeyLabel;
        public Button   button;
    }

    public List<SpellSlotUI> slots = new List<SpellSlotUI>(); // 8 slots

    private SpellCaster caster;

    void Start()
    {
        caster = GameObject.FindWithTag("Player")?.GetComponent<SpellCaster>();
        if (caster == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            int idx = i;
            slots[i].hotkeyLabel.text = (i + 1).ToString();
            slots[i].button.onClick.AddListener(() => OnSpellClicked(idx));
        }

        TurnManager.Instance.OnTurnChanged += _ => RefreshCooldowns();
        Refresh();
    }

    void Update()
    {
        // Hotkeys 1-8
        for (int i = 0; i < 8; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                OnSpellClicked(i);
    }

    private void OnSpellClicked(int idx)
    {
        if (!TurnManager.Instance.IsPlayerTurn) return;
        var spell = caster?.equippedSpells[idx];
        if (spell == null || !caster.CanCast(spell)) return;
        // TODO: enter targeting mode — highlight valid tiles, wait for click
        // For Phase 5 MVP: find nearest enemy and cast at them
        var target = FindNearestEnemy();
        if (target != null) caster.CastAtTarget(spell, target);
        Refresh();
    }

    private void Refresh()
    {
        if (caster == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            var spell = caster.equippedSpells[i];
            bool hasSpell = spell != null;
            slots[i].icon.enabled = hasSpell;
            if (hasSpell) slots[i].icon.sprite = spell.icon;
            RefreshCooldown(i);
        }
    }

    private void RefreshCooldowns()
    {
        for (int i = 0; i < slots.Count; i++) RefreshCooldown(i);
    }

    private void RefreshCooldown(int i)
    {
        if (caster == null) return;
        var spell = caster.equippedSpells[i];
        if (spell == null) { slots[i].cooldownOverlay.gameObject.SetActive(false); return; }
        int cd = caster.GetCooldown(spell);
        slots[i].cooldownOverlay.gameObject.SetActive(cd > 0);
        slots[i].cooldownLabel.text = cd > 0 ? cd.ToString() : "";
    }

    private ICombatant FindNearestEnemy()
    {
        // Placeholder: find any EnemyAI in scene
        var enemy = FindObjectOfType<EnemyAI>();
        return enemy;
    }
}
```

---

## PHASE 8 — Loot System

### TASK 8.1 — `LootTable` ScriptableObject
**Status:** `[ ]`
**File:** `Assets/Scripts/Loot/LootTable.cs`

```csharp
// Assets/Scripts/Loot/LootTable.cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LootEntry
{
    public Item   item;
    [Range(0f, 1f)]
    public float  weight = 1f;
}

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Runebearer/Loot/LootTable")]
public class LootTable : ScriptableObject
{
    public List<LootEntry> entries = new List<LootEntry>();
    [Range(0, 3)]
    public int minDrops = 0;
    public int maxDrops = 2;

    public List<Item> Roll(int floorDepth)
    {
        var result = new List<Item>();
        int count  = Random.Range(minDrops, maxDrops + 1);

        for (int i = 0; i < count; i++)
        {
            float totalWeight = 0;
            foreach (var e in entries) totalWeight += e.weight;
            float pick = Random.value * totalWeight;
            float running = 0;
            foreach (var e in entries)
            {
                running += e.weight;
                if (pick <= running) { result.Add(e.item); break; }
            }
        }
        return result;
    }
}
```

---

### TASK 8.2 — `LootBag` (World Drop)
**Status:** `[ ]`
**File:** `Assets/Scripts/Loot/LootBag.cs`
**Depends on:** `LootTable`, `Inventory`

**WHY:** Items should drop as world objects that the player walks over to collect. This creates satisfying loot pickup moments.

```csharp
// Assets/Scripts/Loot/LootBag.cs
using System.Collections.Generic;
using UnityEngine;

public class LootBag : MonoBehaviour
{
    public List<Item> contents = new List<Item>();
    public int        goldAmount = 0;

    private bool collected = false;

    public static LootBag Spawn(Vector3 position, List<Item> items, int gold)
    {
        // Requires a LootBag prefab in Resources/Prefabs/LootBag
        var prefab = Resources.Load<GameObject>("Prefabs/LootBag");
        if (prefab == null) return null;
        var go  = Instantiate(prefab, position + Vector3.up * 0.1f, Quaternion.identity);
        var bag = go.GetComponent<LootBag>();
        bag.contents   = new List<Item>(items);
        bag.goldAmount = gold;
        return bag;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        var inv   = other.GetComponent<Inventory>();
        var stats = other.GetComponent<CharacterStats>();

        foreach (var item in contents)
            inv?.AddItem(item);

        if (stats != null && goldAmount > 0)
        {
            stats.gold += goldAmount;   // add `public int gold` to CharacterStats
            CombatLog.Log($"Picked up {goldAmount} gold", LogType.Info);
        }

        collected = true;
        Destroy(gameObject);
    }
}
```

**Add to `CharacterStats.cs`:**
```csharp
[Header("Economy")]
public int gold = 0;
```

**Add to enemy death (in `EnemyAI` or `EnemyStats`):**
```csharp
// In EnemyStats.Start(), subscribe to OnDeath:
GetComponent<CharacterStats>().OnDeath += () =>
{
    var items = data?.lootTable?.Roll(FloorManager.Instance?.FloorDepth ?? 1)
                ?? new List<Item>();
    int gold = data != null ? Random.Range(data.goldMin, data.goldMax + 1) : 0;
    LootBag.Spawn(transform.position, items, gold);

    // Award XP to player
    var player = GameObject.FindWithTag("Player")?.GetComponent<CharacterStats>();
    player?.GainExperience(data?.xpReward ?? 0);
};
```

---

## PHASE 9 — Progression & Meta Loop

### TASK 9.1 — Level-Up Spell Draft UI
**Status:** `[ ]`
**File:** `Assets/Scripts/UI/LevelUpUI.cs`
**Depends on:** `CharacterStats`, `SpellCaster`, `SpellData`

**HOW:** Subscribe to `CharacterStats.OnLevelUp` in Start. On trigger, pick 3 random spells the player doesn't know and show a modal.

```csharp
// Assets/Scripts/UI/LevelUpUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelUpUI : MonoBehaviour
{
    [Header("References")]
    public GameObject        panel;
    public List<Button>      spellChoiceButtons;   // 3 buttons
    public List<Image>       spellIcons;
    public List<TMP_Text>    spellNames;
    public List<TMP_Text>    spellDescriptions;

    [Header("All Spells in Game")]
    public List<SpellData>   allSpells;   // assign all SpellData assets in Inspector

    private SpellCaster  caster;
    private List<SpellData> currentChoices = new();

    void Start()
    {
        caster = GameObject.FindWithTag("Player")?.GetComponent<SpellCaster>();
        var stats = GameObject.FindWithTag("Player")?.GetComponent<CharacterStats>();
        if (stats != null) stats.OnLevelUp += ShowDraft;
        panel.SetActive(false);
    }

    private void ShowDraft(int newLevel)
    {
        // Pick 3 random spells the player doesn't know yet and meets level req
        var pool = allSpells.FindAll(s =>
            !caster.knownSpells.Contains(s) && s.levelRequirement <= newLevel);

        currentChoices.Clear();
        while (currentChoices.Count < 3 && pool.Count > 0)
        {
            int idx = Random.Range(0, pool.Count);
            currentChoices.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        for (int i = 0; i < spellChoiceButtons.Count; i++)
        {
            bool valid = i < currentChoices.Count;
            spellChoiceButtons[i].gameObject.SetActive(valid);
            if (!valid) continue;

            var spell = currentChoices[i];
            spellIcons[i].sprite          = spell.icon;
            spellNames[i].text            = spell.spellName;
            spellDescriptions[i].text     = spell.description;

            int idx = i;
            spellChoiceButtons[i].onClick.RemoveAllListeners();
            spellChoiceButtons[i].onClick.AddListener(() => ChooseSpell(idx));
        }

        Time.timeScale = 0f;  // pause game during selection
        panel.SetActive(true);
    }

    private void ChooseSpell(int idx)
    {
        caster.LearnSpell(currentChoices[idx]);
        // Auto-equip if there's a free spell bar slot
        for (int i = 0; i < caster.equippedSpells.Length; i++)
        {
            if (caster.equippedSpells[i] == null)
            {
                caster.equippedSpells[i] = currentChoices[idx];
                break;
            }
        }
        Time.timeScale = 1f;
        panel.SetActive(false);
    }
}
```

---

## PHASE 10 — Audio

### TASK 10.1 — `AudioManager`
**Status:** `[ ]`
**File:** `Assets/Scripts/Audio/AudioManager.cs`
**Depends on:** `SettingsData` (already exists)

```csharp
// Assets/Scripts/Audio/AudioManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    public AudioMixer mixer;     // already set up for Settings menu

    [Header("Music")]
    public AudioSource musicSource;
    public List<AudioClip> floorMusic = new List<AudioClip>(); // index 0 = floors 1-3, etc.

    [Header("SFX Pool")]
    public int sfxPoolSize = 8;

    [System.Serializable]
    public class SFXEntry { public string key; public AudioClip clip; }
    public List<SFXEntry> sfxLibrary = new List<SFXEntry>();

    private Dictionary<string, AudioClip> sfxDict = new();
    private List<AudioSource>             sfxPool  = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var e in sfxLibrary) sfxDict[e.key] = e.clip;

        for (int i = 0; i < sfxPoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = mixer?.FindMatchingGroups("SFX")?[0];
            sfxPool.Add(src);
        }
    }

    public void PlaySFX(string key)
    {
        if (!sfxDict.TryGetValue(key, out var clip)) return;
        var src = sfxPool.Find(s => !s.isPlaying) ?? sfxPool[0];
        src.PlayOneShot(clip);
    }

    public void PlayMusic(int floorDepth)
    {
        int idx = floorDepth <= 3 ? 0 : floorDepth <= 6 ? 1 : 2;
        if (idx >= floorMusic.Count) return;
        if (musicSource.clip == floorMusic[idx] && musicSource.isPlaying) return;
        musicSource.clip = floorMusic[idx];
        musicSource.loop = true;
        musicSource.Play();
    }
}
```

**SFX keys to create assets for:**
`"footstep"`, `"attack_hit"`, `"attack_miss"`, `"spell_cast"`, `"item_pickup"`, `"death"`, `"level_up"`

---

## PHASE 11 — Save System

### TASK 11.1 — `SaveData` & `SaveManager`
**Status:** `[ ]`
**Files:** `Assets/Scripts/Save/SaveData.cs`, `Assets/Scripts/Save/SaveManager.cs`

```csharp
// Assets/Scripts/Save/SaveData.cs
using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int    floorDepth;
    public int    gold;
    public int    level;
    public int    experience;
    public int    strength, dexterity, constitution, intelligence, wisdom, charisma;
    public int    currentHp;
    public List<string> knownSpellNames  = new();   // match by SpellData.spellName
    public List<string> equippedSpellNames = new();
    // inventory serialization: item asset names (loaded from Resources)
}
```

```csharp
// Assets/Scripts/Save/SaveManager.cs
using UnityEngine;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Save()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        var stats  = player.GetComponent<CharacterStats>();
        var caster = player.GetComponent<SpellCaster>();

        var data = new SaveData
        {
            floorDepth  = FloorManager.Instance?.FloorDepth ?? 1,
            gold        = stats.gold,
            level       = stats.level,
            experience  = stats.experience,
            strength    = stats.strength,
            dexterity   = stats.dexterity,
            constitution = stats.constitution,
            intelligence = stats.intelligence,
            wisdom      = stats.wisdom,
            charisma    = stats.charisma,
            currentHp   = stats.currentHp,
        };

        if (caster != null)
        {
            foreach (var s in caster.knownSpells)
                if (s != null) data.knownSpellNames.Add(s.spellName);
            foreach (var s in caster.equippedSpells)
                if (s != null) data.equippedSpellNames.Add(s.spellName);
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"Game saved to {SavePath}");
    }

    public bool HasSave() => File.Exists(SavePath);

    public void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }

    public SaveData Load()
    {
        if (!HasSave()) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
    }
}
```

---

## PHASE 12 — Polish

### TASK 12.1 — `DamagePopup`
**Status:** `[ ]`
**File:** `Assets/Scripts/UI/DamagePopup.cs`

```csharp
// Assets/Scripts/UI/DamagePopup.cs
using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public TMP_Text label;
    public float    floatSpeed  = 1.5f;
    public float    fadeTime    = 0.8f;

    private float   elapsed;
    private Color   startColor;

    public static void Spawn(Vector3 worldPos, string text, Color color)
    {
        var prefab = Resources.Load<GameObject>("Prefabs/DamagePopup");
        if (prefab == null) return;
        var go  = Instantiate(prefab, worldPos + Vector3.up * 0.5f, Quaternion.identity);
        var dp  = go.GetComponent<DamagePopup>();
        dp.label.text  = text;
        dp.label.color = color;
        dp.startColor  = color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        float alpha = 1f - (elapsed / fadeTime);
        label.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

        if (elapsed >= fadeTime) Destroy(gameObject);
    }
}
```

**Hook into `CombatActor.TakeHit()`:**
```csharp
DamagePopup.Spawn(transform.position, $"-{net}", Color.red);
// For crits:
DamagePopup.Spawn(transform.position, $"-{net} CRIT!", Color.yellow);
```

---

## Full Implementation Order (with Dependencies)

```
Phase 1:
  [1.1] ICombatant           — no deps
  [1.2] DamageInfo           — no deps (DamageType already exists)
  [1.3] CombatActor          — needs 1.1, 1.2
  [1.4] CombatLog            — no deps
  [1.5] TurnManager          — needs 1.1, 1.3
  [1.6] CombatResolver       — needs 1.2, 1.3, CharacterStats, Equipment, WeaponItem
  [1.7] TileHighlighter      — needs GridGenerator, TurnManager
  [1.8] PlayerMovement mods  — needs TurnManager, CombatActor, TileHighlighter
  [1.9] PlayerCombatActor    — needs CombatActor, TileHighlighter
  [1.10] CombatHUD (MVP)     — needs TurnManager, CombatActor, CombatLog, CharacterStats

Phase 2:
  [2.1] EnemyData SO         — needs WeaponItem, Item
  [2.2] EnemyStats           — needs EnemyData, CharacterStats
  [2.3] EnemyAI              — needs CombatActor, EnemyData, TurnManager, CombatResolver
  [2.4] EnemySpawner         — needs EnemyAI, EnemyStats, GridGenerator, TurnManager

Phase 3:
  [3.1] DungeonGenerator     — replaces GridGenerator; needs Pathfinder
  [3.2] FloorManager         — needs DungeonGenerator, EnemySpawner, TurnManager
  [3.3] FogOfWar             — needs DungeonGenerator, PlayerMovement

Phase 4:
  [4.1] RuneData SO          — needs Item, DamageType
  [4.2] Passive Rune Slots   — modify Equipment.cs; needs RuneData
  [4.3] FieldRuneInstance    — needs RuneData, TurnManager, CombatActor

Phase 5:
  [5.1] SpellData SO         — needs DamageType, CharacterStats
  [5.2] SpellCaster          — needs SpellData, CombatActor, TurnManager

Phase 6:
  [6.1] StatusEffect + Mgr   — needs CombatActor, TurnManager
  [6.2] Concrete effects     — needs StatusEffect, DamageInfo

Phase 7:
  [7.1] Full CombatHUD       — needs all Phase 1 + SpellCaster
  [7.2] SpellBarUI           — needs SpellCaster, TurnManager
  [7.3] TurnOrderUI          — needs TurnManager
  [7.4] EnemyHealthBarUI     — needs CombatActor, CharacterStats (world-space canvas)

Phase 8:
  [8.1] LootTable SO         — needs Item
  [8.2] LootBag              — needs Inventory, CharacterStats
  (Add gold to CharacterStats)

Phase 9:
  [9.1] FloorManager ext     — extend with floor summary
  [9.2] LevelUpUI            — needs CharacterStats.OnLevelUp, SpellCaster
  [9.3] Gold economy         — needs CharacterStats.gold, shop UI

Phase 10:
  [10.1] AudioManager        — needs SettingsData

Phase 11:
  [11.1] SaveData + Manager  — needs CharacterStats, SpellCaster, FloorManager

Phase 12:
  [12.1] DamagePopup         — needs CombatActor.TakeHit
  [12.2] ScreenShake         — standalone
```

---

## Notes & Gotchas

- `DamageType` enum is defined in `WeaponItem.cs` — do NOT redefine it elsewhere. Import from there.
- `GridGenerator.Instance` is referenced by `PlayerMovement`, `TileHighlighter`, `FogOfWar`, and `EnemySpawner`. If you replace GridGenerator with DungeonGenerator, you must update all 4 call sites or keep a facade.
- `Equipment.EffectiveAC()` (not `GetEffectiveArmorClass()`) — use the correct method name.
- `CharacterStats.Awake()` sets `currentHp = MaxHp`. `EnemyStats.Start()` overrides this with `data.maxHP` — execution order matters.
- `TurnManager.EndTurn()` must be called by `EnemyAI` at the end of its coroutine OR it will deadlock.
- `Time.timeScale = 0` during LevelUpUI pauses coroutines — EndTurn and EnemyAI must not be mid-coroutine when level-up fires.
- All ScriptableObjects that need to survive scene loads (SpellData, RuneData) must be in `Resources/` or referenced via the Inspector — they cannot be runtime-instantiated from nothing.
