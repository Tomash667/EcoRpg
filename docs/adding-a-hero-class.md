# Adding a New Hero Class

This document describes how the current `Class` and `ClassInfo` system works, what
has to change to add a new playable class, and how class data reaches `Hero`,
`Combat`, the menus and the save file.

It is a design/implementation guide, not a record of an implemented feature. No
gameplay code is changed by this document.

Line numbers are from the 2026-11-12 working tree and will drift. Each reference
also names the symbol or surrounding method so it can be found after a refactor.
The project has no test suite and no CLI gameplay runner; verification is
compiling plus playing in the Unity Editor (section 8).

See also:

- `docs/adding-a-property.md` - properties, upgrades and the `Game.properties`
  data pattern;
- `docs/adding-a-quest.md` - quests and the class/`ClassInfo` dictionary pattern
  used there as a comparison.

---

## 0. At a glance

| Question | Short answer |
| --- | --- |
| Where is class data? | `ClassMethods.classes`, a static `Dictionary<Class, ClassInfo>` in `Assets/Scripts/Class.cs:26-60`. There are no ScriptableObjects or class assets. |
| How is a class represented? | One `Class` enum value plus one `ClassInfo` metadata entry. `Hero` stores the enum number, not the metadata. |
| What is in a save? | `Hero.clas` (enum number), `mp`/`mpMax`, the spell list by name, and equipment by item name. `ClassInfo` itself is code-only. |
| What controls combat row and gear? | `ClassInfo.backRow`. It also controls starting gear, weapon subtype, shield permission and blocking. |
| How do spells work? | `Spell.spells` holds reusable spell data. A class lists starter spells and level-gated unlockable spells. `Combat` adds buttons and handles `Spell.Type.Damage`/`Spell.Type.Heal`. |
| How is mana handled? | `ClassInfo.useMana` starts a hero at `100/100` MP. Every level adds `+10` max MP when `mpMax > 0`. Mana potions, elixirs and rest restore MP. |
| Where is the class selected? | `Menu.Awake` fills the dropdown from `ClassMethods.all`; `Menu.NewGameOk` stores `Global.playerClass`; `Game.NewGame` assigns it before `Player.Init()`. |
| What is the biggest data-only risk? | Adding a class to `ClassMethods.all` without a `ClassInfo` entry shows it in the menu, then throws from `GetInfo()` when the class is used. |
| What is the biggest equipment risk? | A new weapon subtype or class that shops/enchants needs items at every level `BuyItems()` and `GetEnchanted()` can request. A missing level throws from `First(...)`. |
| Save compatibility | `Class` is stored as an int. Append enum values; never insert or reorder. Spell and item names are also save keys. |

---

## 1. Current model

### 1.1 `Class` enum

```csharp
public enum Class
{
    None = -1,
    Warrior, // 0
    Archer,  // 1
    Cleric   // 2
}
```

`Hero.clas` is a serialized enum and is stored by `JsonUtility` as a **number**.
The explicit `None = -1` keeps `Warrior = 0` and `Archer = 1` stable, so existing
saves continue to load after a new class is appended.

`Class.None` is a sentinel, not a playable class:

- it has no `ClassInfo` entry;
- `AsString()` returns `[ERROR class None]`;
- `GetInfo()` throws `KeyNotFoundException` for it.

`Race.GetFavoriteClass()` uses `Class.None` to mean "this race has no favorite
class" (`Race.cs:28-36`). Do not put `None` in the class dropdown or assign it to
a real hero.

### 1.2 `ClassInfo`

```csharp
public class ClassInfo
{
    public string name;
    public Spell[] spells;
    public (Spell spell, int level)[] unlockableSpells;
    public int attack, defense, dex;
    public bool backRow, useMana;
}
```

| Field | Meaning and caveats |
| --- | --- |
| `name` | Lowercase display name and portrait filename. Must not be empty because UI code calls `ToUpper1()` and `GetPortrait()` builds `Portraits/{gender} {name}`. |
| `spells` | Starter spells copied into `Hero.spells` by `Hero.InitCommon()` (`Hero.cs:132-157`). May be `null` for a non-caster. |
| `unlockableSpells` | `(Spell spell, int level)` pairs. `Hero.LearnSpells()` adds each spell when the hero reaches that level. May be `null`. |
| `attack`, `defense`, `dex` | Level-1 base stats. No class-specific HP or growth data exists. |
| `backRow` | Combat row and the single flag that currently controls starting gear, weapon subtype, shield use and blocking. |
| `useMana` | Gives the class `mp = mpMax = 100` at creation and enables MP growth and mana-related UI. |

`ClassInfo` is static code data, not serialized. New heroes read it in
`Hero.InitCommon()`. Existing saves keep their serialized stats and MP totals, but
live reads such as `AsString()` and `BackRow` use the current entry, and future
`LearnSpells()` unlocks use the current `unlockableSpells` list. Changing base
`attack`/`defense`/`dex` therefore does not retrofit existing heroes.

`Hero.BackRow` is a property, not a saved field:

```csharp
public bool BackRow => clas.GetInfo().backRow;
```

It calls `GetInfo()` every time it is read. A class value with no dictionary entry
therefore fails as soon as row-dependent code runs.

### 1.3 `ClassMethods`

The dictionary is the authoritative metadata table:

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
        spells = new Spell[]
        {
            Spell.Get("heal"),
            Spell.Get("holy bolt")
        },
        unlockableSpells = new (Spell, int)[]
        {
            (Spell.Get("holy wave"), 10)
        },
        attack = 20,
        defense = 4,
        dex = 8,
        useMana = true
    }
};
```

The dropdown array is separate from the dictionary:

```csharp
public static readonly Class[] all = new[] { Class.Archer, Class.Cleric, Class.Warrior };
public static readonly int defaultClass = all.IndexOf(Class.Warrior);
```

`all` drives:

- the new-game class dropdown (`Menu.Awake` / `Menu.NewGameOk`);
- the fallback random class in `ClassMethods.GetRandom()`;
- the portrait validation in `Game.TestImages`;
- `defaultClass`.

The order of `all` controls the dropdown order only; it is not part of the save.
`defaultClass` stays on Warrior because it is computed with
`all.IndexOf(Class.Warrior)`. If the order changes, the default dropdown position
follows.

Lookup and formatting:

```csharp
public static string AsString(this Class clas)
{
    if (classes.TryGetValue(clas, out ClassInfo info))
        return info.name;
    else
        return $"[ERROR class {clas}]";
}

public static ClassInfo GetInfo(this Class clas)
{
    return classes[clas];
}
```

`AsString()` is safe-ish: an unknown class produces an error string instead of
throwing. `GetInfo()` is not: it uses the dictionary indexer. `Hero.BackRow` and
`Hero.InitCommon()` call it, so a missing entry crashes when the class is used.

`GetRandom(Race)` combines the full class list with the race's favorite:

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

Every class in `all` is immediately reachable by randomly generated allies and
`Game.SpawnHero`/`Game.GiveAll`. If a new class should be favored by a race, add
it to `Race.GetFavoriteClass()` in `Race.cs:28-36`; otherwise no race edit is
needed.

### 1.4 `Hero` runtime state

Relevant fields (`Hero.cs:12-29`):

```csharp
public List<Spell> spells;
public List<string> savedSpells;
public Item weapon, armor, shield;
public Class clas;
public Race race;
public int level, exp, hp, hpMax, mp, mpMax, attack, defense, dex, gold, ...;
```

`Hero.Init(startLevel)` (`Hero.cs:86-130`) does two different jobs:

1. For a new hero, it randomizes gender, race and class, then calls
   `InitCommon()`.
2. For a generated hero at `startLevel > 0`, it also calls `SetLevel()` and
   upgrades starting equipment.

`Hero.InitCommon()` is the per-class initialization used for the player and for
generated allies (`Hero.cs:132-157`):

```csharp
ClassInfo classInfo = clas.GetInfo();

skills = new();
level = 1;
hp = hpMax = 100;
attack = classInfo.attack;
defense = classInfo.defense;
dex = classInfo.dex;
if (classInfo.useMana)
    mp = mpMax = 100;
if (classInfo.spells != null)
    spells = classInfo.spells.ToList();

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

The player does **not** go through the random `Hero.Init(int)` path:
`Game.NewGame()` assigns `global.playerClass` to the new `Player`, then calls
`Player.Init()`, which calls `InitCommon()` (`Game.cs:1615-1620`,
`Player.cs:23-29`).

Shared progression (`Hero.cs:168-229`):

- `hpMax` +20 per level;
- `mpMax` +10 per level if `mpMax > 0`, preserving the MP ratio;
- `attack` +5, `defense` +1, `dex` +2 per level;
- `LearnSpells()` after every level change.

There is no per-class HP, growth rate or stat curve. A class that needs different
growth requires new `ClassInfo` data and changes to `SetLevel()`/`AddExpInternal()`.

### 1.5 Row, equipment and combat

`backRow` currently decides five separate things:

| `backRow` | Combat row | Starting weapon | Starting shield | Weapon subtype | Blocking |
| --- | --- | --- | --- | --- | --- |
| `false` (front) | Front | `club` | `wooden shield` | `Melee` | Yes |
| `true` (back) | Back | `short bow` | none | `Bow` | No |

The same flag is used by:

- `Hero.CanEquip()` (`Hero.cs:389-405`):

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

  This gates the character-screen Equip button, `WillTakeItem()` and any code
  that asks whether a hero can use an item.

- `Hero.BuyItems()` (`Hero.cs:455-634`). Allies automatically upgrade their
  equipment when they receive gold in a safe location:

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

  The loop uses `Item.items.First(...)` for the next level. Missing melee, bow,
  shield or armor levels throw at runtime when an ally shops.

- `Hero.Init(startLevel)` (`Hero.cs:104-111`), which upgrades a generated hero's
  weapon, armor and shield by level:

  ```csharp
  weapon = Item.items.First(x => x.type == Item.Type.Weapon
      && x.subtype == weapon.subtype
      && x.level == weaponLevel);
  if (shield != null)
      shield = Item.items.First(x => x.type == Item.Type.Shield
          && x.level == weaponLevel);
  ```

  The new class's starting weapon subtype therefore needs items at every level
  this method can request, not only level 1.

- `Game.GiveAll()` (`Game.cs:2531-2570`), the editor helper:

  ```csharp
  if (!hero.BackRow)
  {
      hero.weapon = Item.Get("magic sword");
      hero.shield = Item.Get("magic shield");
  }
  else
      hero.weapon = Item.Get("magic bow");
  hero.armor = Item.Get("magic armor");
  ```

  A front-row class that uses another weapon family still receives a magic sword
  here until the helper is generalized.

- Blocking. `Hero.InitCombat()` sets `canBlock = !BackRow` (`Hero.cs:159-166`).
  `Combat.HeroAction` refreshes `canBlock = true` once per round for front-row
  heroes (`Combat.cs:296-307`), and `Combat.EnemyAction` only lets a hero
  intercept with `canBlock && shield != null` (`Combat.cs:861-871`). A front-row
  class with a shield blocks automatically; a back-row class starts with
  `canBlock = false` and never has it refreshed, so it never blocks.

`EnchantItems()` (`Hero.cs:636-705`) uses the equipped weapon, armor and shield.
A `null` shield is treated as `MaxLevelEnchant`, so classes without shields are
skipped automatically. A new weapon subtype still needs matching items at levels
5-8 because `Item.GetEnchanted()` calls `First(...)` for the next level.

### 1.6 Spells and mana

`Spell` is a plain code class, not a `ScriptableObject` (`Spell.cs:3-46`):

```csharp
public enum Type
{
    Damage,
    Heal
}

public string name;
public Type type;
public DamageType damageType;
public int mana;
public bool aoe;
```

Current spell data (`Spell.spells`):

| Spell | Type | Damage type | Mana | AoE | Notes |
| --- | --- | --- | ---: | --- | --- |
| `heal` | `Heal` | `Normal` (unused) | 25 | No | Heals `100 + hero.level * 10`, capped at missing HP. |
| `holy bolt` | `Damage` | `Holy` | 25 | No | Single-target damage. |
| `holy wave` | `Damage` | `Holy` | 50 | Yes | Hits every enemy. |

`Spell.Get(name)` uses `First(...)` and throws if the name does not exist.
Adding, renaming or removing a spell therefore affects:

- `ClassInfo.spells`/`ClassInfo.unlockableSpells` initialization;
- `Hero.OnAfterDeserialize()`, which restores `spells` by saved name;
- `Hero.LearnSpells()`, which adds unlockable spells;
- `Combat`, which indexes `player.spells` and builds action buttons.

Mana flow:

- `Hero.InitCommon()` starts a `useMana` class at `100/100`.
- `Hero.SetLevel()` and `Hero.AddExpInternal()` add `+10` max MP per level while
  `mpMax > 0`.
- `Hero.mpp` returns `-1` when `mpMax == 0`; `CharacterCard.Init` uses that to
  hide the MP bar.
- `CharacterScreen`/`AllyScreen` show mana only when `mpMax > 0` and show the
  spell list only when `spells` is non-empty.
- `Combat.Init()` adds a `Mana p.` button when `player.mpMax > 0` and one button
  per entry in `player.spells`.
- `Hero.UseManaPotionIfRequired()` and `Combat.HeroUseManaPotion()` restore MP
  from `Item.Subtype.Mana` items; allies also use them automatically after combat
  (`Game.cs:826-837`).
- Rest, travel and rest-combat restore MP proportionally or fully alongside HP
  (`Game.cs:1260-1428`, `Game.cs:843-850`, `World.cs:662-684`).

Casting in combat (`Combat.HeroAction`):

- A player-selected spell is stored as `Action.Spell + spellIndex`; the spell is
  only cast when `spell.mana <= hero.mp`.
- If the player selects a spell without enough mana, `castSpell` stays `null` and
  the normal ally/AI action path runs instead.
- Mana is deducted before the hit/miss result, with no refund.
- Single-target damage uses `hero.Attack`, subtracts full enemy `def` for
  `DamageType.Normal` and half `def` for other damage types, then doubles the
  damage when `enemy.IsSusceptible(spell.damageType)` is true.
- AoE damage iterates every enemy in the list (the loop does not skip dead units)
  with `hero.Attack - enemy.def / 2` and the same susceptibility doubling.
- Heals pick the lowest-HP hero for the player and the lowest-HP ally for AI.
- `Combat.HeroThink()` provides ally AI for `Damage` and `Heal` spells, including
  mana-potion planning and priorities. A new `Spell.Type` needs new AI branches
  and probably new action handling.

`DamageType` currently has only `Normal` and `Holy` (`DamageType.cs:1-5`).
`Enemy.IsSusceptible()` only recognizes Holy plus `enemy.holyDmgSus`
(`Enemy.cs:29`, `Enemy.cs:46-49`). Other damage types need extra enemy fields,
`IsSusceptible()` logic and an `Arrow2` visual mapping (`Arrow2.cs:30-35`).

Spells are currently combat-only. There is no out-of-combat spell button, spell
cooldown or per-spell save data beyond the spell name.

### 1.7 Current classes

| Class | Enum | Attack | Defense | Dex | Row | Mana | Starter spells | Unlockable |
| --- | ---: | ---: | ---: | ---: | --- | --- | --- | --- |
| Warrior | 0 | 25 | 5 | 10 | Front | No | - | - |
| Archer | 1 | 30 | 3 | 15 | Back | No | - | - |
| Cleric | 2 | 20 | 4 | 8 | Front | Yes | `heal`, `holy bolt` | `holy wave` at level 10 |

All three portraits exist at `Assets/Resources/Portraits/{male|female} {class}.jpg`.

### 1.8 UI surfaces

| Surface | What it reads | Limits |
| --- | --- | --- |
| `Menu.Awake` / `Menu.NewGameOk` | `ClassMethods.all`, `ClassMethods.defaultClass` | Dropdown is exactly `all`, in array order. |
| `Game.NewGame` | `Global.playerClass`, `Player.Init()` | Player choice is not randomized. |
| `Game.SpawnHero` | `Hero.Init(level)` → `GetRandom(race)` | Random allies use `all`; favorites are race-based. |
| `CharacterScreen.Refresh` | `player.clas.AsString()`, MP, `player.spells` | Shows MP only when `mpMax > 0`; spells only when non-empty. |
| `AllyScreen.Refresh` | ally class, MP, spells | Same display rules. |
| `Combat.Init` | `player.mpMax`, `player.spells`, `hero.BackRow` | Builds mana/spell buttons and row positions. |
| `Combat.HeroAction` / `HeroThink` | `hero.spells`, mana, `BackRow`, weapon subtype | Actual casting and AI. |
| `Game.UpdateText` | `player.mpMax`, `player.hp` | Adds `MP: current/max` to the HUD. |
| `Game.SaveGame` | `player.clas.AsString()` | Class name appears in the save summary. |
| `GuildScreen.Recruit` | `hero.clas.AsString()` | Recruit text uses the lowercase class name. |
| `Game.TestImages` | every class in `all` | Logs missing male/female portraits. |
| `Game.GiveAll` | `hero.BackRow` | Gives row-appropriate magic gear and mana elixirs. |

---

## 2. Adding another class — required steps

### 2.1 Append the enum value

Add the class after the last existing value:

```csharp
public enum Class
{
    None = -1,
    Warrior,
    Archer,
    Cleric,
    Paladin // new class, value 3
}
```

Never insert or reorder an existing value: saves store the numbers
(`Warrior = 0`, `Archer = 1`, `Cleric = 2`), so a new class must be appended
after `Cleric`. Renaming an enum member is save-safe because the number is
unchanged, but it breaks source references and desyncs the name if
`ClassInfo.name` is not updated to match.

### 2.2 Add the class to `ClassMethods.all`

Add the new enum value to the array:

```csharp
public static readonly Class[] all =
    new[] { Class.Archer, Class.Cleric, Class.Paladin, Class.Warrior };
```

`all` controls the dropdown order. `defaultClass` remains on Warrior because it is
computed with `all.IndexOf(Class.Warrior)`. Appending the class at the end of the
array is the least disruptive choice, but any position is save-safe.

### 2.3 Add a `ClassInfo` entry

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
- `AsString()` returns `[ERROR class Paladin]`;
- selecting the class crashes in `Hero.InitCommon()`/`Hero.BackRow` because
  `GetInfo()` uses the dictionary indexer.

Do not add `Class.None` to the dictionary. It is intentionally a sentinel.

### 2.4 Base stats and progression

The level-1 stats come straight from `ClassInfo`:

```csharp
attack = classInfo.attack;
defense = classInfo.defense;
dex = classInfo.dex;
```

`hpMax = 100` is shared by every class. `SetLevel()` and `AddExpInternal()` also
use identical growth for every class:

| Stat | Gain per level |
| --- | ---: |
| `hpMax` | +20 |
| `mpMax` | +10, only when `mpMax > 0` |
| `attack` | +5 |
| `defense` | +1 |
| `dex` | +2 |

If the new class needs a different HP pool or growth, extend `ClassInfo` and
change those methods. There is no per-class growth table today.

### 2.5 Mana and spells

For a caster:

```csharp
[Class.Paladin] = new()
{
    name = "paladin",
    attack = 27,
    defense = 6,
    dex = 9,
    useMana = true,
    spells = new Spell[]
    {
        Spell.Get("heal")
    },
    unlockableSpells = new (Spell, int)[]
    {
        (Spell.Get("holy wave"), 12)
    }
}
```

Rules:

- `useMana = true` gives `100/100` MP at creation. Without it, `mpMax` remains 0
  and the MP bar, mana-potion logic and MP HUD are disabled for that class.
- `spells` is copied into each new hero's own `List<Spell>`. Changing the class
  list later does not update heroes that already exist.
- `unlockableSpells` is level-gated and learned by `LearnSpells()` on level-up.
  It is not applied by `InitCommon()` at level 1.
- If a class has **only** unlockable spells and no starter `spells`, initialize
  `spells` to an empty array. `LearnSpells()` evaluates
  `!spells.Contains(x.spell)` inside the `Where` predicate before its body runs.
  The `spells ??= new();` currently inside the loop body runs too late, so a
  `null` list still throws when the first unlock level is reached.
- `Spell.Get(name)` throws if the spell entry does not exist. Add spells to
  `Spell.spells` before referencing them from `ClassInfo`.

A non-caster can leave both `spells` and `unlockableSpells` as `null`. The UI
guards those cases.

### 2.6 Row and starting equipment

Decide `backRow` first because it controls several systems at once:

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

Starting equipment is still chosen by the row flag in `InitCommon()`:

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

`Item.Get(name)` throws if any starting item name is missing from `Item.items`.

The current row model has hard limits:

- front row means melee weapon + shield;
- back row means bow and no shield;
- `CanEquip()`, `BuyItems()`, `GiveAll()` and blocking all use `BackRow`, not a
  separate class field.

A front-row class that should use a bow, or a back-row class that should use
melee, cannot be expressed without extending `ClassInfo` and replacing those
row checks. See section 4.3.

Check the applicable equipment paths for the new class:

| Path | Requirement |
| --- | --- |
| `InitCommon()` | Starting weapon/armor/shield names exist in `Item.items`. |
| `Init(startLevel)` | The starting weapon subtype and armor/shield have items at every requested level. |
| `BuyItems()` | Melee or bow items, armor and shields exist through `MaxLevelCity = 4`/`MaxLevelVillage = 3`. |
| `EnchantItems()` | Weapon, armor and shield subtypes have items at levels 5-8. |
| `GiveAll()` | The class receives usable magic gear after editing the row branch if needed. |
| `CanEquip()` | The row's allowed subtypes match what the class is meant to use. |

### 2.7 Portrait assets

`GetPortrait()` builds the resource name from `AsString()`:

```csharp
string imageName = $"Portraits/{(female ? "female" : "male")} {clas.AsString()}";
```

Add:

```text
Assets/Resources/Portraits/male paladin.jpg
Assets/Resources/Portraits/female paladin.jpg
```

The existing class portraits are 32x44 JPGs imported as Sprites. Let Unity
generate the `.meta` files. `Game.TestImages` (Context Menu on the `Game`
component) checks every class in `ClassMethods.all` and logs missing portraits.

### 2.8 Usually no changes needed elsewhere

These already read `ClassMethods`/`ClassInfo` and pick up the new class:

- `Menu.Awake` / `Menu.NewGameOk`: dropdown from `ClassMethods.all`.
- `Global.playerClass`: already a `Class`.
- `Game.NewGame`: copies `global.playerClass` into the player.
- `Game.SpawnHero`: uses `Hero.Init()` → `ClassMethods.GetRandom(race)`.
- `Game.SaveGame`: writes `clas.AsString()` in the save summary.
- `CharacterScreen` / `AllyScreen`: display class, portrait, MP and spells.
- `GuildScreen.Recruit`: displays the lowercase class name.
- `Combat`: uses `BackRow`, `canBlock`, `Attack`/`Defense`, weapon subtype and
  spells; it does not switch on the `Class` enum directly.
- `Game.TestImages`: checks both class portraits.

The one helper to audit is `Game.GiveAll()` because it branches on the row rather
than the class. Update it if the class uses a new gear family.

---

## 3. Adding a spell or a spell-using class

### 3.1 Add the spell data

Append to `Spell.spells` (`Spell.cs:22-45`):

```csharp
new()
{
    name = "smite",
    type = Spell.Type.Damage,
    damageType = DamageType.Holy,
    mana = 30
}
```

`name` is the save key and the combat log/button text. `mana` is the cost checked
against `hero.mp`. `aoe` is only meaningful for damage spells in the current
combat code.

### 3.2 Assign it to a class

Either make it a starter spell:

```csharp
spells = new Spell[]
{
    Spell.Get("heal"),
    Spell.Get("smite")
}
```

or an unlock:

```csharp
unlockableSpells = new (Spell, int)[]
{
    (Spell.Get("smite"), 8)
}
```

Remember the empty-list caveat from section 2.5.

### 3.3 Combat integration

The combat UI is data-driven for existing spell types:

- `Combat.Init()` adds one button per `player.spells` entry and stores
  `Action.Spell + index` in `actionButtonYs` (`Combat.cs:172-202`).
- `Combat.HeroAction()` maps the selected action back to
  `hero.spells[index]` (`Combat.cs:402-433`).
- `Combat.HeroThink()` decides AI casts for `Damage` and `Heal` spells
  (`Combat.cs:638-747`).

A new `Spell.Type` or a new non-damage effect needs changes in all three places.
A new `Action` value must be appended carefully: the spell range is calculated
as `Action.Spell + index`, and `PickAction()` matches the action against
`actionButtonYs`.

Casting formulas currently are:

- **Single-target damage:** hit chance from `AttackChance(hero.dex, enemy.dex)`,
  damage `hero.Attack - enemy.def` for `Normal`, or
  `hero.Attack - enemy.def / 2` for other damage types; doubled when susceptible.
- **AoE damage:** every living enemy is rolled separately; damage
  `hero.Attack - enemy.def / 2`, doubled when susceptible.
- **Heal:** `100 + hero.level * 10`, capped at missing HP; no hit roll.

Mana is spent before the result and is not refunded on a miss.

### 3.4 Damage types and susceptibility

`DamageType` only has `Normal` and `Holy`. `Enemy.IsSusceptible()` returns true
only for Holy damage on enemies with `holyDmgSus`. To add another damage type:

1. Append to `DamageType`.
2. Add the matching enemy flag and `IsSusceptible()` logic in `Enemy.cs`.
3. Add an `Arrow2` sprite/`SetDamageType()` branch if the projectile should look
   different.
4. Set `damageType` on the spell data.

### 3.5 Mana items and shopping

Mana potions and elixirs already exist in `Item.items`:

- `mana potion`: 100 mana, `Subtype.Mana`;
- `mana elixir`: 200 mana, `Subtype.Mana`;
- `mana herb`: ingredient, 25 mana.

They are in `Item.cityItems`/`Item.villageItems`, and `Hero.BuyItems()` buys them
only when `mpMax > 0`. A new class with `useMana = true` therefore needs no item
changes, but a new mana item must be added to `Item.items` and the shop arrays.

`Hero.WillTakeItem()` only accepts mana consumables when `mpMax > 0`, so a
non-caster does not collect them.

### 3.6 UI and save effects

- `CharacterScreen`/`AllyScreen` list spell names from `Hero.spells`.
- `Combat` builds spell buttons from the player's list; ally AI uses its own list.
- `Hero.OnBeforeSerialize()` writes `savedSpells` from `spells`; `OnAfterDeserialize()`
  restores with `Spell.Get(name)`.
- Renaming or removing a spell that exists in a save throws during load. Keep old
  spell names valid or add a fallback.

Spells are not cast outside combat in the current code. There is no spell
cooldown, spellbook UI or per-spell rank system.

---

## 4. If the class needs a new equipment family

The current weapon subtypes are `Melee` and `Bow`. A class that needs a third
family (for example a spear) requires all of the following. Use this section as a
checklist; the same steps apply to a new armor or shield family if the existing
ones are not acceptable.

### 4.1 Extend `Item` enums and icons

In `Item.cs:25-52`, append the subtype and icon values:

```csharp
public enum Subtype
{
    None,
    Melee,
    Bow,
    Heal,
    Mana,
    Treasure,
    Spear // append
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
    Spear // append; must match a sprite index in GameUI.itemIcons
}
```

Update `Item.GetIcon()` (`Item.cs:90-118`). The current weapon branch treats every
non-Melee weapon as a bow:

```csharp
case Type.Weapon:
    if (subtype == Subtype.Melee)
        return Icon.Sword;
    else
        return Icon.Bow;
```

Add an explicit branch for the new subtype:

```csharp
case Type.Weapon:
    return subtype switch
    {
        Subtype.Bow => Icon.Bow,
        Subtype.Spear => Icon.Spear,
        _ => Icon.Sword
    };
```

If a new `Icon` value is appended, open `Assets/Scenes/Game.unity`, select the
`GameUI` component and add a sprite to `itemIcons` at the same index. The
character, ally, craft, store and shop screens index that array with
`(int)item.GetIcon()`.

### 4.2 Add items at every reachable level

Add the weapon family to `Item.items` for levels 1-8:

- levels 1-4 are bought in cities/villages (`MaxLevelCity = 4`,
  `MaxLevelVillage = 3`);
- levels 5-8 are created by `GetEnchanted()` at the Mage Tower
  (`MaxLevelEnchant = 8`);
- level 1 is used by `InitCommon()` and `Init(startLevel)`.

Follow the existing naming style, for example `spear`, `war spear`, `steel spear`,
`blessed spear`, `magic spear`, `magic spear +1`, `magic spear +2`,
`magic spear +3`.

Add the purchasable levels to `cityItems` and `villageItems`:

```csharp
public static readonly Item[] cityItems = new[]
{
    // ...
    Get("spear"),
    Get("war spear"),
    Get("steel spear"),
    Get("blessed spear"),
    // ...
};
```

`GetEnchanted()` matches `type + subtype + level`, and `BuyItems()` uses
`First(...)`; gaps throw exceptions at runtime.

### 4.3 Extend `ClassInfo` and the row checks

`backRow` alone cannot express "front row, spear, shield". Add explicit data, for
example:

```csharp
public class ClassInfo
{
    public string name;
    public Spell[] spells;
    public (Spell spell, int level)[] unlockableSpells;
    public int attack, defense, dex;
    public bool backRow, useMana;
    public Item.Subtype weaponSubtype = Item.Subtype.Melee;
    public bool canUseShield = true;
    public string startingWeapon;
}
```

Then replace the row-based decisions:

- `Hero.InitCommon()`: use `classInfo.startingWeapon` and
  `classInfo.canUseShield`.
- `Hero.CanEquip()`: compare against `classInfo.weaponSubtype` and
  `classInfo.canUseShield`.
- `Hero.BuyItems()`: use `classInfo.weaponSubtype`; skip shields when
  `!classInfo.canUseShield`.
- `Hero.Init(startLevel)`: upgrade using `classInfo.weaponSubtype` instead of the
  current weapon's subtype if a starting item is not the final family.
- `Game.GiveAll()`: give gear based on the same data, not `!hero.BackRow`.
- `Hero.InitCombat()` / `Combat.HeroAction` / `Combat.EnemyAction`: keep
  `canBlock`/shield logic consistent with the new permission data.

Keep `backRow` for the actual combat row and targeting. Separating "row" from
"gear permission" is the clean way to support a class that does not fit the
current melee/bow split.

### 4.4 Checklist for a new equipment family

1. Append `Item.Subtype` and `Item.Icon`; never reorder existing values.
2. Update `GetIcon()` and add the `GameUI.itemIcons` sprite at the same index.
3. Add items for levels 1-8 with unique `name` values.
4. Add the purchasable levels to `cityItems`/`villageItems`.
5. Make the class's starting weapon and shield use the new family/permission.
6. Update `CanEquip()`, `BuyItems()`, `Init(startLevel)` and `GiveAll()`.
7. Verify shopping, enchanting and old-save loading with the new family.

---

## 5. Save/load compatibility

`Game.SaveGame()` serializes the entire `Game` object with
`JsonUtility.ToJson(this)`; `LoadGame()` uses `FromJsonOverwrite`
(`Game.cs:1661-1679`). `player` and team members are `[SerializeReference]`.
There is no versioning or migration.

Rules for a new class, spell or equipment family:

1. **Append `Class` values.** `Class.None = -1` keeps `Warrior = 0`,
   `Archer = 1` and `Cleric = 2`. Appending new classes preserves all existing
   save values.
2. **Do not rename `Hero.clas`, `mp`, `mpMax`, `savedSpells` or the class
   dictionary keys.** Renaming `Hero.clas` silently loses the class on old saves.
3. **`ClassInfo` is code-only.** The dictionary is not serialized. New heroes read
   it in `InitCommon()`; existing saves keep their serialized `attack`,
   `defense`, `dex`, `mp` and `mpMax`. Live reads (`AsString()`, `BackRow`) and
   future `LearnSpells()` unlocks come from the current build, so renaming or
   changing `backRow` affects old saves, while changing base stats does not.
4. **Spells are saved by name.** `Hero.OnBeforeSerialize()` writes
   `savedSpells`; `OnAfterDeserialize()` calls `Spell.Get(name)`, which throws for
   an unknown name. Renaming or deleting a spell breaks saves that contain it.
5. **New `Hero` fields need defaults.** Old saves deserialize new fields as `0`,
   `false` or `null`; initialize them in `InitCommon()` and guard their use. This
   matters for future mana/spell fields and for adding mana to an existing class.
6. **Items are saved by name.** `ItemSlot` writes `item.name` and restores with
   `Item.Get(name)`. Renaming an existing item breaks saves; adding items or
   subtypes is safe.
7. **`[NonSerialized]` combat state** (`canBlock`, `potionTimer`, `potionsUsed`,
   `poison`, `confused`) is rebuilt by `InitCombat()`.
8. **`Class.None` must not appear in a save as a hero class.** `GetInfo()` throws
   for it, and `GetRandom()` only uses it as a local "no favorite" sentinel.
9. **Unknown class numbers fail loudly later.** A save containing a class value
   removed from the build still loads, but `AsString()` shows `[ERROR class N]`
   and `BackRow`/`GetInfo()` throw when the hero is used. Do not rely on old
   builds understanding newer class numbers.
10. **Changing starter spells does not retrofit existing heroes.** Their
    `Hero.spells` list is restored from their save. New starters/unlocks only
    affect heroes created or leveled after the change, unless a migration pass is
    added.

---

## 6. Balance and documentation

- `stats.txt` currently has no class table. Add base stats, MP and growth data
  there when balancing a new class.
- `readme.txt` documents player-facing shortcuts and mechanics. Update it if the
  class adds a new key, combat option or other player-visible mechanic; mana
  potion use (`M` in combat) is already documented.
- Space and naming are part of the UI: class names are lowercase in
  `ClassInfo.name`, then uppercased by the menu and used after articles in the
  guild/character screens.
- All content remains in code arrays (`ClassMethods.classes`, `Spell.spells`,
  `Item.items`, `Item.cityItems`, `Item.villageItems`); there are no
  ScriptableObjects.
- Keep class metadata in one place. Adding a class in `all` plus `classes` is the
  whole data-only path; anything that branches on `backRow` or a spell type is
  code, not data.

---

## 7. Common failure modes

| Symptom | Likely cause | Check |
| --- | --- | --- |
| Class appears in the menu but selecting it crashes. | No `ClassInfo` entry. | `ClassMethods.classes` has a key for the new enum value. |
| `KeyNotFoundException` in `BackRow`/`InitCommon`. | Same missing dictionary entry; `GetInfo()` uses the indexer. | Add the entry before adding the class to `all`. |
| `Missing 'Portraits/male ...'` in the Console. | No portrait or a name mismatch. | `AsString()` matches the file name; run `Game.TestImages`. |
| Ally shopping throws `InvalidOperationException`/`First`. | Missing item level for the selected subtype. | Add all levels 1-4 (city/village) and 5-8 (enchant). |
| Enchanting throws. | New subtype has no levels 5-8. | Check `GetEnchanted()`/`EnchantItems()`. |
| New class has spells but no mana bar. | `useMana` is false or `mpMax` stayed 0. | Set `useMana = true` and initialize MP in `InitCommon()`. |
| Level 10+ unlockable spell throws on a class with no starters. | `spells` is `null` when `LearnSpells()` evaluates `spells.Contains`. | Set `spells = new Spell[0]` or add a starter spell/guard. |
| Loading an old save throws in `OnAfterDeserialize`. | A spell or item name was renamed/removed. | Keep names stable or add a fallback lookup. |
| Warrior/Archer behavior changed after adding a class. | `backRow` or shared `ClassInfo` fields were changed globally. | Regression-test all existing classes, not only the new one. |

---

## 8. Verification checklist for any new class

There is no automated gameplay test or CLI run of the game. Do the following in
the Unity Editor after the compile/static checks pass.

1. Compile in Unity and check the Console for errors.
2. Open `Assets/Scenes/Menu.unity`, open New Game, confirm the new class appears
   in the Class dropdown and is selectable.
3. Start a new game. Confirm base stats, row, portrait, starting weapon/armor/
   shield and blocking.
4. If the class is a caster, confirm `100/100` MP, the MP HUD, the `Mana p.`
   combat button and the spell buttons; cast each starter spell and verify the
   mana cost and effect.
5. Level the class to the first unlockable spell level; confirm the spell is
   learned once, appears in the character screen and can be cast.
6. Open the character screen (`C`) and an ally screen (`1`-`3`); confirm class
   text, portrait, stats, MP, spell list and Equip availability.
7. Shop in a city/village and let an ally of the class receive gold; confirm the
   correct weapon/shield progression, mana-potion purchases and no `First()`
   exceptions.
8. Enchant at the Mage Tower through level 8; confirm the class's weapon/armor
   (and shield if used) upgrade correctly.
9. Fight a battle: verify front/back row placement, enemy targeting, shield
   blocking, melee/bow animation, spell projectiles and ally AI casting.
10. Recruit from the guild (`G` in a city) until the class appears; confirm the
    recruit text uses the correct lowercase name.
11. Save, exit to menu, continue, and confirm the class, stats, MP, spells and
    equipment persist.
12. Load an old save created before the class was added; existing Warrior,
    Archer and Cleric heroes must keep their class, stats and gear.
13. Run the `Game` component's Context Menu `Test images`; there must be no
    `Missing 'Portraits/...'` errors.
14. Run `Give all`; the new class must receive usable magic equipment, mana
    elixirs if it has mana, and any newly learned spells.
15. Regression-test Warrior and Archer behavior, including Archer back-row
    positioning and bow animation, and Cleric's current spells/mana.
