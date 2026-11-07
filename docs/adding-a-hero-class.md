# Adding a New Hero Class

This document lists everything that must be reviewed or changed when adding a new
`Class` value to `Assets/Scripts/Class.cs`.

The important gotcha: **adding only an enum value is not enough.** Several methods
use `if (clas == Class.Warrior) ... else ...`, so a new class silently inherits the
Archer behavior (stats, bow, back row, no shield, no blocking). Every branch below
must be given an explicit decision for the new class.

The examples use a hypothetical `Cleric` (the class already proposed in `todo.txt`)
because it needs both melee and shield behavior. Replace it with the real class name.

---

## 1. Checklist

### Required for every new class

- [ ] `Class.cs`: add the enum member (append it, never insert/reorder).
- [ ] `Class.cs`: add the class to `ClassMethods.all`.
- [ ] `Class.cs`: add an `AsString()` mapping (lowercase, used in UI and portrait paths).
- [ ] `Class.cs`: decide how `GetRandom(Race)` can produce the class.
- [ ] `Hero.cs`: define `BackRow`.
- [ ] `Hero.cs`: define base stats in `InitCommon()`.
- [ ] `Hero.cs`: define starting equipment in `InitCommon()`.
- [ ] `Hero.cs`: define `canBlock` in `InitCombat()`.
- [ ] `Hero.cs`: define usable weapons/armor/shields in `CanEquip()`.
- [ ] `Hero.cs`: define shop progression in `BuyItems()`.
- [ ] Add `male <class>.jpg` and `female <class>.jpg` under `Assets/Resources/Portraits/`.
- [ ] Play-test new game, equipment progression, combat and save/load.

### Required only when the class needs a new equipment family

- [ ] `Item.cs`: add a `Subtype` and `Icon` if no existing weapon subtype fits.
- [ ] `Item.cs`: add all weapon levels the class can buy/enchant (normally 1-8).
- [ ] `Item.cs`: add new weapons to `cityItems` and `villageItems` where they should be sold.
- [ ] `Item.cs`: add a `GetIcon()` mapping (otherwise new weapon subtypes display as bows).
- [ ] `GameUI`: add the matching sprite to `itemIcons` in the Game scene if a new `Icon` was added.

### Usually required for consistency

- [ ] `Game.cs`: update the `GiveAll` editor helper (it currently gives every non-Warrior a bow).
- [ ] `readme.txt`: document any new player-facing mechanic or class-specific control.
- [ ] `stats.txt` / `todo.txt`: record base stats, progression and balancing decisions.

---

## 2. `Class.cs` — the class definition

Current file (32 lines) contains the enum, the `all` array, `defaultClass`,
`AsString()` and `GetRandom(Race)`.

### 2.1 Enum: append, do not insert

`Hero.clas` is serialized by `JsonUtility` as an enum **number**. Existing saves
store `Warrior = 0` and `Archer = 1`. Inserting a new value before `Archer` would
make old Archers load as the new class.

```csharp
public enum Class
{
    Warrior, // 0 — do not change
    Archer,  // 1 — do not change
    Cleric   // 2 — append new classes at the end
}
```

Do not rename or reorder existing members. If a class is removed, leave its enum
slot in place unless save migration is written.

### 2.2 `ClassMethods.all`

This array drives:

- the new-game class dropdown (`Menu.Awake`, `Menu.NewGameOk`),
- `ClassMethods.GetRandom` fallback,
- `Game.TestImages` portrait validation,
- `defaultClass`.

Add the class, choosing the dropdown order deliberately (it is not the enum order):

```csharp
public static readonly Class[] all = new[] { Class.Archer, Class.Cleric, Class.Warrior };
```

`defaultClass` is already computed with `all.IndexOf(Class.Warrior)`, so it keeps
pointing at Warrior regardless of the new class's position. Change it only if the
default selection should change.

### 2.3 `AsString()`

Used for saves, menus, character/ally screens, guild recruitment text and portrait
file names. A missing case produces `[ERROR class ...]` in UI and a broken portrait
path.

```csharp
public static string AsString(this Class clas)
{
    return clas switch
    {
        Class.Warrior => "warrior",
        Class.Archer => "archer",
        Class.Cleric => "cleric",
        _ => $"[ERROR class {clas}]"
    };
}
```

The string must be lowercase because `Hero.GetPortrait()` builds
`Portraits/{female|male} {clas.AsString()}`.

### 2.4 `GetRandom(Race)`

This controls which classes randomly generated heroes (allies, `GiveAll`, etc.) can
have. The current implementation only gives an even chance to classes in `all` for
Humans (`_ => all.RandomItem()`); Elves and Dwarves can only be Archer or Warrior.

```csharp
public static Class GetRandom(Race race)
{
    return race switch
    {
        Race.Elf => Utility.Rand % 3 != 0 ? Class.Archer : Class.Warrior,
        Race.Dwarf => Utility.Rand % 3 != 0 ? Class.Warrior : Class.Archer,
        _ => all.RandomItem() // Human: includes every class in `all`
    };
}
```

If the new class should appear for Elves/Dwarves, or should have race-specific
weights, convert the branches to explicit weighted choices. Otherwise the class is
reachable only through the Human fallback.

Note: `Hero.Init()` also assigns a random race and class for generated heroes;
`Game.NewGame()` assigns the player's chosen class from `Global.playerClass` before
calling `Player.Init()`, so the player choice is not randomized.

---

## 3. `Hero.cs` — all class-specific behavior

These are the locations that currently assume "Warrior or Archer". Each one needs a
deliberate value for the new class.

| Location | Current behavior | What the new class inherits if untouched |
| --- | --- | --- |
| `BackRow` (line 72) | `clas == Class.Archer` | Front row |
| `InitCommon()` stats (line 135) | Warrior else Archer stats | Archer stats: 30 atk / 3 def / 15 dex |
| `InitCommon()` equipment (line 148) | Warrior gets club + wooden shield, else short bow | Short bow, no shield |
| `InitCombat()` (line 166) | Warrior can block, else cannot | Cannot block |
| `CanEquip()` weapon (line 363) | Warrior melee, else bow | Bow only |
| `CanEquip()` shield (line 368) | Warrior only | Cannot use shields |
| `BuyItems()` (line 484) | Warrior buys melee, else bow and skips shield | Bow progression, no shield |
| `GetPortrait()` (line 796) | `Portraits/{gender} {class}` | Blank/missing portrait (no exception) |

Line numbers are from the current working tree; use the method names if they drift.

### 3.1 `BackRow`

```csharp
public bool BackRow => clas == Class.Archer;
```

`Combat.Init` uses this to place cards and decide whether the hero can be targeted
directly. `Combat.HeroAction` and `Combat.EnemyAction` also use it for blocking.

Make the decision explicit for every class, so future classes do not silently
default to front row:

```csharp
public bool BackRow => clas switch
{
    Class.Archer => true,
    Class.Warrior => false,
    Class.Cleric => false,
    _ => false
};
```

A third row is not supported by the current combat layout; a new class must be
either front row or back row.

### 3.2 Base stats — `InitCommon()`

Current code has two hard-coded stat sets. The new class needs its own numbers:

```csharp
switch (clas)
{
case Class.Warrior:
    attack = 25;
    defense = 5;
    dex = 10;
    break;
case Class.Archer:
    attack = 30;
    defense = 3;
    dex = 15;
    break;
case Class.Cleric:
    attack = 25;
    defense = 4;
    dex = 12;
    break;
}
```

`hpMax = 100` is shared by all classes today. `SetLevel()` and `AddExpInternal()` also
use the same per-level growth for every class (`+20 hp`, `+5 attack`, `+1 defense`,
`+2 dex`). If the new class should grow differently, add class-based growth there
too; otherwise every class keeps the same progression.

### 3.3 Starting equipment — `InitCommon()`

Current code gives Warrior `club` + `wooden shield`, everyone else `short bow`, then
always `leather armor`, a potion and three rations.

```csharp
switch (clas)
{
case Class.Warrior:
    weapon = Item.Get("club");
    shield = Item.Get("wooden shield");
    break;
case Class.Archer:
    weapon = Item.Get("short bow");
    break;
case Class.Cleric:
    weapon = Item.Get("club"); // or a new mace
    shield = Item.Get("wooden shield");
    break;
}
armor = Item.Get("leather armor");
AddItem(Item.Get("potion"));
AddItem(Item.Get("ration"), 3);
```

`Item.Get(name)` throws if the name is missing. New starting weapons must exist in
`Item.items`.

`Hero.Init(startLevel)` later upgrades starting equipment to
`equipmentLevel = 1 + level / 2` and uses `weapon.subtype` to find the matching
weapon. A new weapon subtype must therefore have items at the levels used there.

### 3.4 `canBlock` — `InitCombat()`

Current code:

```csharp
if (clas == Class.Warrior)
    canBlock = true;
else
    canBlock = false;
```

`Combat.HeroAction` also sets `hero.canBlock = true` for non-back-row heroes on their
turn, and `Combat.EnemyAction` only lets a hero block if `hero.shield != null`. Decide
whether the new class can block:

```csharp
canBlock = clas == Class.Warrior || clas == Class.Cleric;
```

If the class is front row and has a shield, blocking works automatically. If it is
front row but has no shield, `canBlock` has no practical effect because enemies
require a shield to be intercepted. A back-row class cannot block repeatedly even
if `InitCombat()` sets `canBlock = true`, because `HeroAction` only refreshes
`canBlock` for non-back-row heroes; supporting that needs a combat change.

### 3.5 `CanEquip()` — equipment restrictions

Current code:

```csharp
case Item.Type.Weapon:
    if (clas == Class.Warrior)
        return item.subtype == Item.Subtype.Melee;
    else
        return item.subtype == Item.Subtype.Bow;
case Item.Type.Shield:
    return clas == Class.Warrior;
case Item.Type.Armor:
    return true;
```

This affects the Equip button in the character screen, `WillTakeItem()` (whether an
ally accepts loot), and the `BuyItems()`/`EnchantItems()` progression.

```csharp
case Item.Type.Weapon:
    return clas switch
    {
        Class.Warrior => item.subtype == Item.Subtype.Melee,
        Class.Archer => item.subtype == Item.Subtype.Bow,
        Class.Cleric => item.subtype == Item.Subtype.Melee,
        _ => false
    };
case Item.Type.Shield:
    return clas == Class.Warrior || clas == Class.Cleric;
case Item.Type.Armor:
    return true;
```

If a new weapon subtype is added, the new class's case must reference it and
`Item.Subtype` must contain it.

### 3.6 `BuyItems()` — automatic shop progression

`BuyItems()` runs whenever an ally receives gold in a safe location. It buys
rations/potions, then upgrades weapon, armor and shield to
`Item.MaxLevelCity` (4) or `Item.MaxLevelVillage` (3), selling the old item.

Current code:

```csharp
Item.Subtype weaponSubtype;
if (clas == Class.Warrior)
    weaponSubtype = Item.Subtype.Melee;
else
{
    weaponSubtype = Item.Subtype.Bow;
    shieldLevel = maxLevel; // non-warriors effectively skip shields
}
```

Change this to match the class's equipment:

```csharp
Item.Subtype weaponSubtype = clas switch
{
    Class.Warrior => Item.Subtype.Melee,
    Class.Archer => Item.Subtype.Bow,
    Class.Cleric => Item.Subtype.Melee,
    _ => throw new System.ArgumentOutOfRangeException(nameof(clas))
};

if (clas != Class.Warrior && clas != Class.Cleric)
    shieldLevel = maxLevel; // class does not use a shield
```

The `while` loop calls `Item.items.First(...)` for the next level. If any level is
missing for the class's weapon subtype, the game throws at runtime. This is the most
common way a new class breaks several in-game days later, when an ally levels up and
tries to shop.

`EnchantItems()` uses the same three equipment slots but reads levels from the
equipped items; a null shield is treated as `Item.MaxLevelEnchant`, so a class
without a shield is skipped automatically. It still requires weapon/armor items at
levels 5-8 for `GetEnchanted()`.

### 3.7 `GetPortrait()` — art asset naming

```csharp
string imageName = $"Portraits/{(female ? "female" : "male")} {clas.AsString()}";
```

No code change is normally needed, but the following assets must exist:

```
Assets/Resources/Portraits/male cleric.jpg
Assets/Resources/Portraits/female cleric.jpg
```

Use the exact lowercase `AsString()` value and a space before it. `Game.TestImages`
(Context Menu on the `Game` component) checks every class in `ClassMethods.all` and
logs a missing-file error. Let Unity generate the `.meta` files.

If a class should not use the generic portrait scheme, override `GetPortrait()` in
`Player` or change the path logic; currently all heroes share this scheme.

---

## 4. `Item.cs` — equipment and shops

### 4.1 If the class uses existing Melee or Bow weapons

No `Item.cs` change is required. Existing shops already contain melee and bow
weapons at levels 1-4:

- `cityItems`: club, axe, sword, two handed sword, short bow, long bow, composite
  bow, elven bow, plus armor and shields.
- `villageItems`: levels 1-3 equivalents.

Existing shields and armor are used by all classes unless `CanEquip()` restricts
them.

### 4.2 If the class needs a new weapon family (mace, staff, spear, etc.)

Add the subtype and icon:

```csharp
public enum Subtype
{
    None,
    Melee,
    Bow,
    Ingredient,
    Treasure,
    Mace // append new subtypes
}

public enum Icon
{
    Sword,
    Bow,
    Shield,
    Armor,
    Potion,
    Tool,
    Herb,
    Food,
    Treasure,
    Mace // must match a sprite index in GameUI.itemIcons
}
```

Then update `GetIcon()`; the current weapon branch treats every non-Melee weapon as
a Bow:

```csharp
case Type.Weapon:
    return subtype switch
    {
        Subtype.Bow => Icon.Bow,
        Subtype.Mace => Icon.Mace,
        _ => Icon.Sword
    };
```

Add the weapon items to `Item.items` for **every level the class can reach**, not
just the starting level:

- levels 1-4 are bought in cities/villages (`MaxLevelCity = 4`,
  `MaxLevelVillage = 3`);
- levels 5-8 are created by `GetEnchanted()` at the Mage Tower
  (`MaxLevelEnchant = 8`);
- level 1 is used by `InitCommon()` and by `Hero.Init(startLevel)`.

`GetEnchanted()` looks up `type + subtype + level`, and `BuyItems()` uses
`First(...)`, so gaps throw exceptions. Follow the existing naming convention, e.g.
`mace`, `war mace`, `steel mace`, `blessed mace`, `magic mace`,
`magic mace +1`, `magic mace +2`, `magic mace +3`.

Add the purchasable levels to the shop arrays:

```csharp
public static readonly Item[] cityItems = new[]
{
    // ...
    Get("mace"),
    Get("war mace"),
    Get("steel mace"),
    Get("blessed mace"),
    // ...
};
```

Also add the lower levels to `villageItems` if they should be available there.

If a new `Icon` value is appended, open `Assets/Scenes/Game.unity`, select the object
with the `GameUI` component and add a sprite to `itemIcons` at the same index.
`CharacterScreen`, `AllyScreen`, `CraftScreen`, `StoreItemsScreen` and `ShopScreen`
index this array with `(int)item.GetIcon()`, so a missing sprite shows a blank icon
or an out-of-range error.

### 4.3 Shields

Shield items already exist for levels 1-8 under `Item.Type.Shield` with
`Subtype.None`. A new class can reuse them by returning `true` from `CanEquip()` for
shields and not skipping `shieldLevel` in `BuyItems()`.

If the class is two-handed / cannot use a shield, leave `shield` null, return `false`
from `CanEquip()` for shields, and set `shieldLevel = maxLevel` in `BuyItems()` so
the upgrade loop does not wait on an unusable slot.

---

## 5. `Game.cs` and combat references

### 5.1 `Game.GiveAll` (editor Context Menu, line 2285)

The debug helper gives every hero magic equipment:

```csharp
if (hero.clas == Class.Warrior)
{
    hero.weapon = Item.Get("magic sword");
    hero.shield = Item.Get("magic shield");
}
else
    hero.weapon = Item.Get("magic bow");
```

A new class falls into the `else` and receives a bow even if it cannot equip one.
Update it alongside `CanEquip()`:

```csharp
switch (hero.clas)
{
case Class.Warrior:
case Class.Cleric:
    hero.weapon = Item.Get("magic sword");
    hero.shield = Item.Get("magic shield");
    break;
case Class.Archer:
    hero.weapon = Item.Get("magic bow");
    break;
}
```

### 5.2 `Game.TestImages` (editor Context Menu, line 2390)

Automatically iterates `ClassMethods.all` and checks both portrait files. It needs no
code change, but is the quickest verification that the new class is registered and
has both images.

### 5.3 Combat — `Assets/Scripts/GameDialogs/Combat.cs`

Most combat logic is class-agnostic and uses `Hero.Attack` / `Hero.Defense`, so it
works once `Hero` is updated. The class-sensitive parts are:

- `Init` (lines 87-97): uses `BackRow` to place cards in one or two rows and computes
  `multiRow`. No change if `BackRow` is defined correctly.
- `HeroAction` (lines 231-237): sets `canBlock` for non-back-row heroes at the start
  of their action. Uses `BackRow`, no direct `Class` check.
- `HeroAction` attack animation (lines 341 and 409): checks
  `hero.weapon.subtype == Item.Subtype.Bow` to fire `Arrow2`; everything else uses a
  melee `CharacterCard.Attack()`. A new ranged subtype needs the same check updated
  (or a helper such as `weapon.subtype.IsRanged()`).
- `EnemyAction` (lines 529-538): back-row heroes can be protected by a front-row
  blocker with `canBlock && shield != null`.
- The player action buttons only expose Escape and Heal (`Action` enum). There is no
  class-ability system yet. If the new class needs a unique combat action (e.g.
  Cleric healing, Mage spells), that is a separate feature requiring:
  - a new `Action` value,
  - handling in `HeroAction`,
  - a button in the combat UI and a class gate,
  - any new runtime/serialized state on `Hero`.

Combat does not read `Class` directly, so no other combat edits are needed for a
class that only changes stats, row, equipment and blocking.

### 5.4 No changes normally needed

These already use `ClassMethods` or `AsString()` and pick up the new class
automatically:

- `Menu.Awake` / `Menu.NewGameOk`: dropdown options and selection index come from
  `ClassMethods.all`.
- `Global.playerClass`: already a `Class` field.
- `Game.NewGame`: copies `global.playerClass` into the player.
- `Game.SpawnHero`: uses `Hero.Init()`, which calls `ClassMethods.GetRandom(race)`.
- `Game.SaveGame`: uses `clas.AsString()` in the save summary.
- `CharacterScreen` / `AllyScreen`: display `clas.AsString()` and `GetPortrait()`.
- `GuildScreen.Recruit`: displays `clas.AsString()` in recruit text.
- `Hero.GetPortrait`: derives the asset path from `AsString()`.

---

## 6. Save/load compatibility

`Game.SaveGame()` serializes the whole `Game` object with
`JsonUtility.ToJson(this)`; `LoadGame()` uses `FromJsonOverwrite`. There is no
migration or versioning.

Rules for adding a class:

1. **Append the enum value.** Existing saves store `Class` as an integer. Appending
   keeps `Warrior = 0` and `Archer = 1` valid.
2. **Do not rename `Hero.clas`.** Renaming the field silently loses the class on old
   saves.
3. **`ClassMethods.all` order only controls the new-game dropdown order.** The
   dropdown maps its selected index back through `all`, and `defaultClass` is
   recomputed with `all.IndexOf(Class.Warrior)`, so reordering is safe for saves but
   changes the list order players see.
4. **New `Hero` fields need defaults.** Old saves deserialize without them and get
   the field's default value (`0`, `false`, `null`). Initialize them in
   `InitCommon()` and guard their use in `Attack`, `Defense`, combat, etc.
5. **Items are saved by name**, not by index (`ItemSlot.OnBeforeSerialize` writes
   `item.name`, `OnAfterDeserialize` calls `Item.Get(name)`). Renaming an existing
   item breaks old saves; adding new items or subtypes is safe.
6. **New `[NonSerialized]` runtime state** must be rebuilt after load, just like
   `Hero.canBlock`, `potionTimer`, etc.
7. **Test an old save** from before the change. The existing Warrior/Archer heroes
   must load with the same class, portrait, equipment and combat behavior.

---

## 7. Balance and documentation

- Add the class's base stats and per-level growth to `stats.txt` (it currently covers
  locations, enemies and quest rewards, but has no class table).
- Update `todo.txt` if the class was listed there (e.g. the planned `cleric`).
- Update `readme.txt` only if the class adds player-facing controls or mechanics.
  Existing class selection already appears in the new-game dialog; no shortcut
  change is needed for stats/equipment-only classes.
- Keep all new content in code arrays (`ClassMethods`, `Item.items`,
  `Item.cityItems`, `Item.villageItems`). There are no ScriptableObjects.

---

## 8. Verification in the Unity Editor

There is no CLI test/build command; the only real verification is compiling and
playing in the editor.

1. Compile and check the Console for errors.
2. Open `Assets/Scenes/Menu.unity`, start a new game. Confirm the new class appears
   in the Class dropdown, is selectable, and starts with the intended stats and
   equipment.
3. Open the character screen (`C`). Confirm class name, portrait, attack, defense
   and health.
4. Check the equipment list: the starting weapon/armor/shield are equipped and
   incompatible items do not show an Equip button.
5. In a city/village, verify allies of the new class buy the correct weapon/shield
   levels over time. At the Mage Tower, verify weapon/armor (and shield if used)
   enchant through level 8.
6. Fight a battle with the new class:
   - it appears in the correct row (front/back),
   - enemy targeting and blocking behave as intended,
   - ranged animation uses an arrow if the weapon subtype is Bow,
   - damage uses the expected `Attack`/`Defense`.
7. Recruit allies from the guild (`G` in a city) until the new class appears; check
   the recruit text uses the correct lowercase class name.
8. Save, exit to menu, continue, and confirm the class and equipment persist.
9. Run the `Game` component's Context Menu `Test images`; there must be no
   `Missing 'Portraits/...'` errors.
10. Run `Give all` in the editor; the new class must receive usable magic equipment.
11. Regression: load an old save and confirm existing Warrior/Archer heroes still
    load and fight correctly.
