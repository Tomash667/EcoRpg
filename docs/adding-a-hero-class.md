# Adding a New Hero Class

This document describes how to add a new `Class` value to
`Assets/Scripts/Class.cs`, using the current data-driven `ClassInfo` design.

It also records the verification of the pending **Cleric** change. The Cleric is
currently implemented as a front-row melee/shield class with its own stats. The
planned Cleric spells, mana and maces described in `todo.txt` are **not** implemented
yet.

---

## 1. Pending Cleric change — verification result

### 1.1 Changed files

| File | Change | Verification |
| --- | --- | --- |
| `Assets/Scripts/Class.cs` | Added `None = -1`, `Cleric`, `ClassInfo`, the `classes` dictionary and `GetInfo()`; `AsString()` and `GetRandom()` now use class data | OK |
| `Assets/Scripts/Hero.cs` | Replaced Warrior/Archer branches with `ClassInfo` lookups and `BackRow` checks | OK |
| `Assets/Scripts/Race.cs` | Added `GetFavoriteClass()` used by `ClassMethods.GetRandom()` | OK |
| `Assets/Scripts/Game.cs` | `GiveAll` now uses `!hero.BackRow` instead of `clas == Class.Warrior` | OK for current row-based gear |
| `Assets/Scripts/GameDialogs/CharacterScreen.cs` | Removed placeholder `"Random text"` lines | Unrelated to classes; no class impact |
| `Assets/Resources/Portraits/female cleric.jpg` | New portrait | Present, 32x44, imported as Sprite |
| `Assets/Resources/Portraits/male cleric.jpg` | New portrait | Present, 32x44, imported as Sprite |
| `todo.txt` | Added Cleric plans (mana, mana potion, spells, background healing, maces) plus armor/race notes | Plans only; not implemented |

### 1.2 Build and static checks

- `dotnet build Assembly-CSharp.csproj` completed with **0 warnings and 0 errors**.
- `git diff --check` reported no whitespace errors.
- Every class in `ClassMethods.all` has a matching `ClassInfo` entry.
- Male and female portrait files exist for all three classes.
- No `clas == Class.Warrior` / `clas == Class.Archer` branches remain in gameplay
  code. `Class` values are referenced only in `Class.cs` (definitions) and
  `Race.cs` (favorite classes).

This is a compile/static verification. The project has no automated tests or CLI
runtime test; final gameplay verification still has to be done in the Unity Editor
(see section 8).

### 1.3 Verified gaps / follow-up work

The current Cleric implementation is **stats, row and equipment only**:

- Cleric stats are `attack = 20`, `defense = 4`, `dex = 8`, `backRow = false`.
- Front row means it uses the generic melee path: `club` and `wooden shield` at
  level 1, melee weapons and shields when shopping.
- There is no mace subtype or mace item, so the `todo.txt` line
  `cleric -> use maces, warrior -> use swords` is not implemented.
- There is no mana, mana potion, spell, spell button, holy damage or background
  healing. Those remain future work (section 5).
- `ClassInfo` has no per-class `hp`, `weaponSubtype`, `canUseShield` or
  `canBlock` fields. `backRow` currently controls all of those behaviors.
- `Class.None` is a sentinel only. It has no `ClassInfo` entry, so do not assign it
  to a real hero; `GetInfo()` throws a `KeyNotFoundException` for it.

---

## 2. How class data works now

### 2.1 `Class` enum

```csharp
public enum Class
{
    None = -1,
    Warrior, // 0
    Archer,  // 1
    Cleric   // 2
}
```

`Hero.clas` is serialized by `JsonUtility` as an enum **number**. The explicit
`None = -1` keeps `Warrior = 0` and `Archer = 1`, so existing saves still load
correctly. Append new classes at the end; never insert or reorder values.

`None` is not a playable class. It is used by `Race.GetFavoriteClass()` to mean
"this race has no favorite class".

### 2.2 `ClassInfo`

```csharp
public class ClassInfo
{
    public string name;
    public int attack, defense, dex;
    public bool backRow;
}
```

These fields are the minimum needed for the current behavior:

- `name` is the lowercase display name and portrait filename.
- `attack`, `defense`, `dex` are the level-1 base stats.
- `backRow` controls combat row, starting equipment, blocking and equipment type.

### 2.3 `ClassMethods.classes` dictionary

```csharp
public static readonly Dictionary<Class, ClassInfo> classes = new()
{
    [Class.Warrior] = new()
    {
        name = "warrior",
        attack = 25,
        defense = 5,
        dex = 10
    },
    [Class.Archer] = new()
    {
        name = "archer",
        attack = 30,
        defense = 3,
        dex = 15,
        backRow = true
    },
    [Class.Cleric] = new()
    {
        name = "cleric",
        attack = 20,
        defense = 4,
        dex = 8
    }
};
```

This dictionary is required for every playable class. `AsString()` uses
`TryGetValue` and returns `[ERROR class ...]` for an unknown value, but
`ClassMethods.GetInfo()` uses the dictionary indexer:

```csharp
public static ClassInfo GetInfo(this Class clas)
{
    return classes[clas];
}
```

`Hero.BackRow` and `Hero.InitCommon()` call `GetInfo()`, so an enum value or `all`
entry without a dictionary entry will throw `KeyNotFoundException` as soon as the
class is used.

### 2.4 `ClassMethods.all`

```csharp
public static readonly Class[] all = new[] { Class.Archer, Class.Cleric, Class.Warrior };
```

This array drives:

- the new-game class dropdown (`Menu.Awake` / `Menu.NewGameOk`),
- the fallback random class in `ClassMethods.GetRandom()`,
- `Game.TestImages` portrait validation,
- `defaultClass`.

The order controls the dropdown order only. `defaultClass` is computed with
`all.IndexOf(Class.Warrior)`, so it remains on Warrior unless changed explicitly.

### 2.5 `AsString()`

```csharp
public static string AsString(this Class clas)
{
    if (classes.TryGetValue(clas, out ClassInfo info))
        return info.name;
    else
        return $"[ERROR class {clas}]";
}
```

The `name` must be lowercase because `Hero.GetPortrait()` builds
`Portraits/{female|male} {clas.AsString()}`.

### 2.6 `GetRandom(Race)`

```csharp
public static Class GetRandom(Race race)
{
    Class favorite = race.GetFavoriteClass();
    if (favorite != Class.None && Utility.Rand % all.Length == 0)
        return favorite;
    else
        return all.RandomItem();
}
```

The race's favorite class gets a `1 / all.Length` chance; otherwise any class in
`all` is chosen uniformly. New classes in `all` are immediately reachable by
randomly generated heroes (allies and the `GiveAll` helper).

`Race.GetFavoriteClass()` is defined in `Race.cs`:

```csharp
public static Class GetFavoriteClass(this Race race)
{
    return race switch
    {
        Race.Elf => Class.Archer,
        Race.Dwarf => Class.Warrior,
        _ => Class.None
    };
}
```

Humans have no favorite. If the new class should be favored by a race, add it here;
otherwise no race edit is required.

`Hero.Init()` assigns random race and class for generated heroes. `Game.NewGame()`
assigns the player's chosen class from `Global.playerClass` before `Player.Init()`,
so the player's menu choice is not randomized.

---

## 3. Adding another class — required steps

### 3.1 Append the enum value

Add the class after `Cleric` (or after the last existing class):

```csharp
public enum Class
{
    None = -1,
    Warrior,
    Archer,
    Cleric,
    Paladin // new class
}
```

Never insert before `Warrior`/`Archer`: old saves store their numeric values.

### 3.2 Add the class to `ClassMethods.all`

Add it in the desired dropdown position:

```csharp
public static readonly Class[] all =
    new[] { Class.Archer, Class.Cleric, Class.Paladin, Class.Warrior };
```

`defaultClass` stays on Warrior because it is computed by `IndexOf`.

### 3.3 Add a `ClassInfo` entry

This is mandatory, not optional:

```csharp
[Class.Paladin] = new()
{
    name = "paladin",
    attack = 27,
    defense = 6,
    dex = 9,
    backRow = false
}
```

If the entry is missing:

- the dropdown still shows the class because it comes from `all`;
- `AsString` returns `[ERROR class Paladin]`;
- selecting it crashes in `Hero.InitCommon()` or `Hero.BackRow` because
  `GetInfo()` uses the dictionary indexer.

Do not add `Class.None` to the dictionary unless `None` becomes a real playable
class; it is intentionally a sentinel.

### 3.4 Decide `backRow` and current behavior

Because `backRow` is the only behavior flag, setting it has broad effects:

| `backRow` | Combat row | Starting gear | Weapon type | Shield | Blocking |
| --- | --- | --- | --- | --- | --- |
| `false` (front) | Front | `club` + `wooden shield` | Melee | Yes | Yes |
| `true` (back) | Back | `short bow` | Bow | No | No |

This is why the current Cleric behaves like a Warrior with lower stats. A class
that should be front row but use a bow, or back row but use melee, cannot be
expressed with the current `ClassInfo`. To support that, extend `ClassInfo` (see
section 4.2) and replace the `!backRow` checks in `Hero`/`Game`.

### 3.5 Base stats and progression

`Hero.InitCommon()` reads `attack`, `defense` and `dex` from `ClassInfo`:

```csharp
ClassInfo classInfo = clas.GetInfo();
level = 1;
hpMax = 100;
hp = hpMax;
attack = classInfo.attack;
defense = classInfo.defense;
dex = classInfo.dex;
```

`hpMax = 100` is shared by every class. `SetLevel()` and `AddExpInternal()` also
use identical growth for every class (`+20 hp`, `+5 attack`, `+1 defense`, `+2 dex`
per level). If the new class needs different HP or growth, extend `ClassInfo` and
change those methods; there is no per-class growth data today.

### 3.6 Starting equipment

`InitCommon()` still chooses gear from the row flag:

```csharp
if (!classInfo.backRow)
{
    weapon = Item.Get("club");
    shield = Item.Get("wooden shield");
}
else
    weapon = Item.Get("short bow");
armor = Item.Get("leather armor");
AddItem(Item.Get("potion"));
AddItem(Item.Get("ration"), 3);
```

`Item.Get(name)` throws if an item name is missing. New starting gear must exist in
`Item.items`.

`Hero.Init(startLevel)` later upgrades starting equipment using `weapon.subtype`.
A new weapon subtype therefore needs items at every level used by that method.

### 3.7 `canBlock`

`InitCombat()` now uses the row:

```csharp
canBlock = !BackRow;
```

`Combat.HeroAction` also sets `hero.canBlock = true` for non-back-row heroes at the
start of their action, and `Combat.EnemyAction` only lets a hero intercept with
`canBlock && shield != null`. A front-row class with a shield therefore blocks
automatically. A back-row class cannot block repeatedly because its `canBlock` is
not refreshed by `HeroAction`; supporting back-row blocking needs a combat change.

### 3.8 `CanEquip()`

The current restrictions are row-based:

```csharp
case Item.Type.Weapon:
    if (!BackRow)
        return item.subtype == Item.Subtype.Melee;
    else
        return item.subtype == Item.Subtype.Bow;
case Item.Type.Shield:
    return !BackRow;
case Item.Type.Armor:
    return true;
```

Front-row classes can use every melee weapon and every shield. Back-row classes can
use bows and cannot use shields. This affects the character-screen Equip button,
`WillTakeItem()` and ally shopping.

### 3.9 `BuyItems()`

Allies automatically upgrade weapon, armor and shield to the local shop cap
(`MaxLevelCity = 4`, `MaxLevelVillage = 3`), then sell the old item. The row decides
the weapon subtype:

```csharp
Item.Subtype weaponSubtype;
if (!BackRow)
    weaponSubtype = Item.Subtype.Melee;
else
{
    weaponSubtype = Item.Subtype.Bow;
    shieldLevel = maxLevel; // back-row classes skip shields
}
```

The upgrade loop calls `Item.items.First(...)` for the next level. If any level for
the chosen subtype is missing, shopping throws at runtime. This is the most common
way a new class breaks later, when an ally tries to shop.

`EnchantItems()` uses the equipped weapon/armor/shield and requires items at levels
5-8 for `GetEnchanted()`. A null shield is treated as `MaxLevelEnchant`, so classes
without shields are skipped automatically.

### 3.10 Portrait assets

`GetPortrait()` derives the asset name from `AsString()`:

```csharp
string imageName = $"Portraits/{(female ? "female" : "male")} {clas.AsString()}";
```

Add:

```
Assets/Resources/Portraits/male paladin.jpg
Assets/Resources/Portraits/female paladin.jpg
```

The existing portraits are 32x44 JPGs imported as Sprites (`textureType: 8`). Let
Unity generate the `.meta` files. `Game.TestImages` (Context Menu on the `Game`
component) checks every class in `ClassMethods.all` and logs missing portraits.

### 3.11 Usually no changes needed elsewhere

These already read `ClassMethods`/`ClassInfo` and pick up the new class:

- `Menu.Awake` / `Menu.NewGameOk`: dropdown from `ClassMethods.all`.
- `Global.playerClass`: already a `Class`.
- `Game.NewGame`: copies `global.playerClass` into the player.
- `Game.SpawnHero`: uses `Hero.Init()` → `ClassMethods.GetRandom(race)`.
- `Game.SaveGame`: writes `clas.AsString()` in the save summary.
- `CharacterScreen` / `AllyScreen`: display `clas.AsString()` and `GetPortrait()`.
- `GuildScreen.Recruit`: displays `clas.AsString()`.
- `Combat`: uses `BackRow`, `canBlock`, `Attack`/`Defense` and weapon subtype; it
  does not switch on the `Class` enum directly.

`Game.GiveAll` deserves a check for every new class. It currently branches on the
row:

```csharp
if (!hero.BackRow)
{
    hero.weapon = Item.Get("magic sword");
    hero.shield = Item.Get("magic shield");
}
else
    hero.weapon = Item.Get("magic bow");
```

A front-row class that uses a different weapon family (for example maces) receives
a magic sword here. Update the helper when the gear model changes.

---

## 4. If the class needs a new equipment family

The current Cleric reuses melee weapons and shields, so it has no `Item.cs` changes.
The `todo.txt` plan for Cleric maces is not implemented. Adding a real mace family
requires all of the following.

### 4.1 Extend `Item` enums and icons

```csharp
public enum Subtype
{
    None,
    Melee,
    Bow,
    Ingredient,
    Treasure,
    Mace // append
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
    Mace // append; must match a sprite index in GameUI.itemIcons
}
```

Update `GetIcon()`; the current weapon branch treats every non-Melee weapon as a
Bow:

```csharp
case Type.Weapon:
    return subtype switch
    {
        Subtype.Bow => Icon.Bow,
        Subtype.Mace => Icon.Mace,
        _ => Icon.Sword
    };
```

If a new `Icon` value is appended, open `Assets/Scenes/Game.unity`, select the
`GameUI` component and add a sprite to `itemIcons` at the same index. The character,
ally, craft, store and shop screens index that array with
`(int)item.GetIcon()`.

### 4.2 Add items at every reachable level

Add the weapon items to `Item.items` for levels 1-8:

- levels 1-4 are bought in cities/villages (`MaxLevelCity = 4`,
  `MaxLevelVillage = 3`);
- levels 5-8 are created by `GetEnchanted()` at the Mage Tower
  (`MaxLevelEnchant = 8`);
- level 1 is used by `InitCommon()` and `Hero.Init(startLevel)`.

Follow the existing naming style, for example `mace`, `war mace`, `steel mace`,
`blessed mace`, `magic mace`, `magic mace +1`, `magic mace +2`,
`magic mace +3`.

Add the purchasable levels to `cityItems` and `villageItems`:

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

`GetEnchanted()` matches `type + subtype + level`, and `BuyItems()` uses
`First(...)`; gaps throw exceptions.

### 4.3 Extend `ClassInfo` and the row checks

`backRow` alone cannot say "front row, maces, shield". Add the needed data, for
example:

```csharp
public class ClassInfo
{
    public string name;
    public int attack, defense, dex;
    public bool backRow;
    public Item.Subtype weaponSubtype = Item.Subtype.Melee;
    public bool canUseShield = true;
    public string startingWeapon;
}
```

Then replace the row-based decisions:

- `Hero.InitCommon()`: use `classInfo.startingWeapon`/`canUseShield`.
- `Hero.CanEquip()`: compare against `classInfo.weaponSubtype` and
  `classInfo.canUseShield`.
- `Hero.BuyItems()`: use `classInfo.weaponSubtype`; skip shields when
  `!classInfo.canUseShield`.
- `Game.GiveAll`: give gear based on the same data, not `!hero.BackRow`.

This is the cleanest way to implement the planned `cleric -> use maces,
warrior -> use swords` distinction.

---

## 5. Class abilities and the planned Cleric (not implemented)

`todo.txt` now lists these Cleric plans:

- mana and mana potions;
- spells cast from combat buttons, with AI use and an optional player auto-use;
- heal and holy damage spells, plus a stronger spell to unlock;
- background healing on rest/travel;
- cleric maces, warrior swords;
- races with favorite/disliked classes.

None of these are in the pending code. The current combat action enum only has
`None`, `Escape` and `Heal` (the Heal action uses a potion, not a spell):

```csharp
private enum Action
{
    None,
    Escape,
    Heal
}
```

Adding real spells would require:

- new serialized `Hero` state (mana, known spells, cooldowns) with defaults for old
  saves;
- new item data for mana potions and a way to use them;
- new `Action` values and handling in `Combat.HeroAction`, including ally AI;
- combat UI buttons and a class/`ClassInfo` gate;
- background healing hooks in rest/travel/camping;
- tests/play-testing for each spell and save/load round-trip.

Until then, document Cleric as a melee/shield class with different stats, not as a
spellcaster.

---

## 6. Save/load compatibility

`Game.SaveGame()` serializes the entire `Game` object with
`JsonUtility.ToJson(this)`; `LoadGame()` uses `FromJsonOverwrite`. There is no
versioning or migration.

Rules:

1. **Append enum values.** `Class.None = -1` keeps `Warrior = 0`, `Archer = 1` and
   `Cleric = 2`. Appending new classes preserves all existing save values.
2. **Do not rename `Hero.clas`.** Renaming the field silently loses the class on old
   saves.
3. **`ClassInfo` is code-only.** The dictionary is not serialized; class stats and
   behavior come from the current build. Changing `ClassInfo` changes existing saves
   too, which is intended.
4. **New `Hero` fields need defaults.** Old saves deserialize them as `0`, `false`
   or `null`. Initialize them in `InitCommon()` and guard their use. This will
   matter for planned mana/spell fields.
5. **Items are saved by name**, not index (`ItemSlot` writes `item.name` and restores
   with `Item.Get(name)`). Renaming an existing item breaks saves; adding items or
   subtypes is safe.
6. **`[NonSerialized]` combat state** (`canBlock`, `potionTimer`, `potionsUsed`,
   `poison`, `confused`) is rebuilt by `InitCombat()`.
7. **`Class.None` must not appear in a save as a hero class.** `GetInfo()` throws for
   it, and `GetRandom()` only uses it as a local "no favorite" sentinel.

---

## 7. Balance and documentation

- `stats.txt` still has no class table. Add base stats and growth for each class.
- `todo.txt` now tracks the Cleric plan; keep implemented and planned features
  separate so the docs do not claim spells/mana exist.
- Update `readme.txt` only when the class adds player-facing controls or mechanics.
  Class selection already exists in the new-game dialog.
- All content remains in code arrays (`ClassMethods.classes`, `Item.items`,
  `Item.cityItems`, `Item.villageItems`); there are no ScriptableObjects.

---

## 8. Verification checklist for any new class

There is no automated gameplay test or CLI run of the game. Do the following in the
Unity Editor after the compile/static checks pass.

1. Compile in Unity and check the Console for errors.
2. Open `Assets/Scenes/Menu.unity`, open New Game, confirm the new class appears in
   the Class dropdown and is selectable.
3. Start a new game. Confirm base stats, row, portrait, starting weapon/armor/shield
   and blocking.
4. Open the character screen (`C`) and an ally screen (`1`/`2`); confirm class text,
   portrait, stats and Equip availability.
5. Shop in a city/village and let an ally of the class receive gold; confirm the
   correct weapon/shield progression and no `First()` exceptions.
6. Enchant at the Mage Tower through level 8; confirm the class's weapon/armor (and
   shield if used) upgrade correctly.
7. Fight a battle: verify front/back row placement, enemy targeting, shield blocking
   and melee/bow animation.
8. Recruit from the guild (`G` in a city) until the class appears; confirm the recruit
   text uses the correct lowercase name.
9. Save, exit to menu, continue, and confirm the class, stats and equipment persist.
10. Load an old save created before the class was added; existing Warrior/Archer
    heroes must keep their class and gear.
11. Run the `Game` component's Context Menu `Test images`; there must be no
    `Missing 'Portraits/...'` errors.
12. Run `Give all`; the new class must receive usable magic equipment.
13. Regression-test Warrior and Archer behavior, including Archer back-row
    positioning and bow animation.
