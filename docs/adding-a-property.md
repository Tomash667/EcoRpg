# Adding a New Property or Property Upgrade

This document describes how the current `Property` (building) and
`Property.Upgrade` systems work, what has to change to add a new one, and which
parts of the code are still hard-coded around the existing buildings.

It is a design/implementation guide, not a record of an implemented feature. No
gameplay code is changed by this document.

Line numbers are from the 2026-11-09 checkout and will drift. Each reference also
names the symbol so it can be found after a refactor. The project has no test
suite and no CLI gameplay runner; verification is compiling plus playing in the
Unity Editor (see section 6).

See also: `docs/adding-a-hero-class.md` for the equivalent class guide.

---

## 0. At a glance

| Question | Short answer |
| --- | --- |
| Where is property data? | `Property.properties` static array in `Assets/Scripts/Property.cs:314`. There are no ScriptableObjects. |
| Where are owned properties? | `Player.properties` (`Player.cs:8`). |
| Where are properties for sale? | `Game.properties` (`Game.cs:34`). |
| How is an upgrade represented? | Nested serializable class `Property.Upgrade` (`Property.cs:15-26`). |
| Can a numeric-only upgrade be added with data only? | Yes, if it only uses `value`, `income` and `upkeep`. |
| Can a behaviour upgrade be added with data only? | No. Its effect is a string comparison in `Game`, `PropertiesScreen` and/or the screen that uses it. |
| Can a city property be added with data only? | It will appear and earn, but it cannot be managed locally or assigned a worker until the lookup methods are extended. |
| Can a map property be added with data only? | Only by reusing an existing `TileType` and an existing tile, and even then some per-type switches still apply. |
| Biggest risk | Property identity is a display string (`name`), and many behaviours compare that string. |

---

## 1. Current model

### 1.1 Types and ownership

| Type / field | Location | Role |
| --- | --- | --- |
| `Property` | `Property.cs:6` | Runtime building instance; also the static template type. |
| `Property.Upgrade` | `Property.cs:15-26` | Serializable upgrade entry. |
| `Property.Event` | `Property.cs:8-13` | Runtime event (`Buff`, `Infested`). |
| `Property.Status` | `Property.cs:28-34` | Lifecycle state. |
| `Property.DescStatus` | `Property.cs:36-43` | Selects a `ToString` format for the UI. |
| `Property.Image` | `Property.cs:45-51` | Icon index into `GameUI.propertyIcons`. |
| `Property.properties` | `Property.cs:314-727` | Static templates (16 entries, 24 upgrade entries). |
| `Player.properties` | `Player.cs:8` | Properties the player owns. |
| `Game.properties` | `Game.cs:34` | Properties currently for sale/buildable. |
| `Worker.locationIndex` | `Worker.cs:8` | Assigned property, stored as an `int` location, not a reference. |
| `Team.freshHorses` | `Team.cs:13` | Temporary Stables travel bonus. |

A property is copied from a template into `Game.properties` at new-game time
(`Game.cs:1633-1647`). Buying or building it moves the same instance from
`Game.properties` to `Player.properties` (`PropertiesScreen.cs:137-138`).
Selling moves it back (`PropertiesScreen.cs:83-85`).

### 1.2 `Property.Status` is overloaded

| Value | In a template / `Game.properties` | In `Player.properties` |
| --- | --- | --- |
| `None` | Hidden from the Properties screen. | Not expected; does not earn. |
| `Cleared` | Available to build (`PropertiesScreen.cs:118`). | Not expected. |
| `Building` | Not expected. | Under construction; `buildTime` counts down. |
| `Active` | Available to buy. | Operational; earns income and can have events/workers. |

This works because the same enum value means "for sale" in one list and
"operational" in the other. It also means a new property template must use
exactly `Active` (buy) or `Cleared` (build), and that any code comparing status
must know which list it is looking at. `Game.RegenerateWorld` relies on the
numeric ordering with `copy.status >= Property.Status.Building`
(`Game.cs:2226`), so do not reorder the enum.

### 1.3 Lifecycle

1. **New game.** `Game.NewGame` copies every `Property.properties` entry, calls
   `locationIndexFunc` when present, and adds the copy to `Game.properties`
   (`Game.cs:1633-1647`). A missing location only logs an editor warning
   (`Game.cs:1639-1642`).
2. **Listing.** `PropertiesScreen.Refresh` shows properties whose status is not
   `None` and whose `cityIndex` is `-1` or the current city
   (`PropertiesScreen.cs:59-60`).
3. **Buying.** Cost is `property.Value` (not the raw `value` field), gold is
   deducted, the instance moves to `Player.properties`, and the tile's clear
   timer/quest state is updated (`PropertiesScreen.cs:122-165`).
4. **Building.** Cost is `property.BuildPrice`; status becomes `Building`; the
   tile timer is reset and a map icon is added (`PropertiesScreen.cs:139-145`).
5. **Completion.** `Game.OnNewDay` decrements `buildTime`; at zero the status
   becomes `Active`, the tile type is changed, the map icon is removed and a
   notification is added (`Game.cs:1489-1516`).
6. **Income.** `Game.OnNewDay` sums `Profit` for all active properties, then
   subtracts `hiredWorkers.Count * 2` (`Game.cs:1457-1487`).
7. **Efficiency and events.** Every 10 days, unmanaged income properties lose
   efficiency and may get a `Buff` or `Infested` event
   (`Game.cs:1541-1595`).
8. **Selling.** The instance goes back to `Game.properties`, the player receives
   `Value / 2`, events are cleared, and any assigned worker is unassigned
   (`PropertiesScreen.cs:81-105`).

### 1.4 Computed values

| Member | Location | Formula / behaviour |
| --- | --- | --- |
| `Value` | `Property.cs:64-71` | `value * (0.9 + 0.2 * efficiency / 100)`, rounded. Used as the **buy price** and the **sell base**. |
| `Income` | `Property.cs:72-94` | `income`, times 1.5 for `Buff`, 0 for `Infested`, times 1.1 for producers when the player owns an `Office` property, times `EfficiencyMod`. |
| `Upkeep` | `Property.cs:95-109` | `upkeep`, halved while `Infested`; minus `upkeepDiscount` when the player owns a non-infested `Sawmill`. |
| `Profit` | `Property.cs:110` | `Income - Upkeep`. |
| `BuildPrice` | `Property.cs:111-120` | `buildPrice`, minus `buildPriceDiscount` when the player owns a non-infested `Sawmill`. |
| `Name` | `Property.cs:121-130` | `name`, plus the city name when `multi` is true. |
| `Desc` | `Property.cs:131` | Replaces the literal `PROFIT` and `UPKEEP` placeholders. **`UPKEEP` uses the raw `upkeep` field, not the computed `Upkeep`.** |
| `Efficiency` | `Property.cs:132-151` | Maps the 1-100 number to `disaster` ... `amazing`. |
| `EventChances` | `Property.cs:174-193` | Buff/infest percentages derived from efficiency. |

Consequences that matter when adding content:

- A template with the default `efficiency = 0` sells and buys for **90 %** of
  its `value` field (`Value`), not the raw number. Most non-producer templates
  (House, Mansion, Horses, Inn, Office) are in this situation.
- A producer's `income` is not the final income: events, the Office property and
  efficiency all modify it.
- `Desc` can show a different upkeep from the `Upkeep` row in the details panel
  when the Sawmill discount is active.

### 1.5 `Property.Upgrade`

```csharp
[Serializable]
public class Upgrade
{
    public string name, desc;
    public int value, income, upkeep;
    public bool active;
}
```

- `value` is the purchase cost **and** is added to the property's `value` on
  purchase (`PropertiesScreen.cs:266`). Selling the property therefore refunds
  half of the upgrade value too.
- `income` and `upkeep` are added once to the property's raw fields
  (`PropertiesScreen.cs:267-268`). They are not recomputed from the upgrade
  list, so applying an upgrade twice would double them; `active` is what
  prevents that.
- `desc` is display-only. There is no `PROFIT`/`UPKEEP` substitution in
  `Upgrade.ToString`; it prints `name (desc, value gold)`
  (`Property.cs:22-25`).
- There are no prerequisites, levels, repeatable upgrades, undo, or mutually
  exclusive upgrades. Every inactive upgrade is listed and can be bought in any
  order, sorted by name (`PropertiesScreen.cs:248-252`).
- `active` is serialized. `Copy()` resets it to `false`
  (`Property.cs:247-254`); `Update()` preserves it by matching the upgrade name
  (`Property.cs:284-295`).

### 1.6 Current property templates

All entries are in `Property.properties` (`Property.cs:314-727`).

| Name | City | Multi | Status | Value | Income | Upkeep | Build price / time | Location | Upgrades |
| --- | ---: | --- | --- | ---: | ---: | ---: | --- | --- | --- |
| House | 0 | yes | Active | 500 | 0 | 0 | - | city only | Alchemy lab, Garden |
| House | 1 | yes | Active | 400 | 0 | 0 | - | city only | Alchemy lab, Garden |
| House | 2 | yes | Active | 400 | 0 | 0 | - | city only | Alchemy lab, Garden |
| Mansion | 0 | no | Active | 10000 | 0 | 5 | - | city only | Alchemy lab, Garden, Stables, Office |
| Horses | -1 | no | Active | 500 | 0 | 0 | - | global | - |
| Sawmill | 0 | no | Active | 5000 | 10 | 5 | - | `TileType.Sawmill` | Extra guards, Water-powered saws |
| Iron mine | 0 | no | Active | 10000 | 20 | 10 | - | first `TileType.Mine` | Extra guards, Deep shaft expansion |
| Silver mine | 0 | no | Active | 25000 | 35 | 10 | 6000 / 20 | hidden cave, difficulty 2, `mine` | Extra guards, Deep shaft expansion |
| Gold mine | 0 | no | Active | 50000 | 60 | 10 | 7500 / 30 | hidden cave, difficulty 3, `mine` | Extra guards, Deep shaft expansion |
| Inn | 0 | yes | Active | 5000 | 10 | 5 | - | city only | - |
| Inn | 1 | yes | Active | 4000 | 9 | 5 | - | city only | - |
| Inn | 2 | yes | Active | 4000 | 9 | 5 | - | city only | - |
| Farm | 0 | yes | Active | 5000 | 10 | 5 | - | `TileType.Farm`, difficulty 1 | Extra guards, Advanced tools |
| Farm | 1 | yes | Active | 5000 | 10 | 5 | - | `TileType.Farm`, difficulty 2 | Extra guards, Advanced tools |
| Farm | 2 | yes | Cleared | 5000 | 10 | 5 | 2500 / 15 | plains near city 2 | Extra guards, Advanced tools |
| Office | 0 | no | Active | 5000 | 0 | 5 | - | city only | - |

Notes:

- `Office` appears twice in the file: once as a **Mansion upgrade** and once as
  a **standalone city property**. `HaveProperty("Office")` means the property;
  `HavePropertyUpgrade("Mansion", "Office")` means the upgrade.
- The Sawmill/Iron/Silver/Gold/Farm entries are the only `producer = true`
  templates. `producer` currently only enables the Office +10 % income bonus; it
  does not gate income, events or worker assignment.
- House, Inn and Farm are duplicated per city. Mansion exists only for city 0.

### 1.7 Current upgrade names and effects

There are eight unique upgrade names and 24 entries.

| Upgrade | Numeric fields | Behaviour |
| --- | --- | --- |
| Alchemy lab | `value = 100` | +25 alchemy while inside a House/Mansion that has it (`CraftScreen.cs:24-25`, `Game.cs:2124-2129`). Shows the Craft button (`Game.cs:1773-1774`). |
| Garden | `value = 100/500`, `upkeep = 1/2` | Enables the Garden screen/button (`Game.cs:211-212`, `Game.cs:1775-1776`). Garden slots are created only for House/Mansion (`PropertiesScreen.cs:167-175`). |
| Stables | `value = 1000`, `upkeep = 3` | Sets `team.freshHorses` on purchase (`PropertiesScreen.cs:281-282`), on entering a city/village (`Game.cs:1158-1159`) and on resting in a Mansion (`Game.cs:1280-1281`). |
| Office | `value = 2000`, `upkeep = 1` | Enables remote property management from a Mansion (`PropertiesScreen.cs:18-19`, `Game.cs:209`, `Game.cs:1690`). |
| Extra guards | `value = 500-4000`, `upkeep = 1/2` | Prevents new infestations (`Game.cs:1578`); resolves an existing infestation after a travel delay (`PropertiesScreen.cs:270-280`). |
| Water-powered saws | `value = 1500`, `income = 5` | Numeric only. |
| Deep shaft expansion | `value = 3000-5000`, `income = 10/20` | Numeric only. |
| Advanced tools | `value = 1500-3000`, `income = 5` | Numeric only. |

---

## 2. Adding a new `Property`

### 2.1 Choose the property category first

| Category | Existing examples | New `TileType`? | Has a map location? | Manageable on site? | Main extra work |
| --- | --- | --- | --- | --- | --- |
| City service | Inn, Office | No | No (`locationIndex = -1`) | No (Inn is special-cased) | `GetPropertyHere`, `GetLocationIndex`, `GetProperty`, `OnRest` if it rests |
| Residence / interior | House, Mansion | Yes, plus a sublocation | No | Yes, inside the building | `World.sublocations`, `EnterHouse`, `GetPropertyInside`, storage/garden, background |
| Map producer | Sawmill, Iron mine, Farm | Usually yes (or reuse) | Yes | Yes | `TileType`, `Tile`, map/background sprites, spawn, `DoWork`, `GetPropertyHere`, build completion |
| Global passive | Horses | No | No | No | Only if it changes a global formula (`World` travel speed) |

The cheapest useful addition is a **city service** with numeric-only upgrades.
The most expensive is a new **interior** with its own screen and sublocation.

### 2.2 Minimum data-only city property

The following entry can be appended to `Property.properties` without touching
any other file. It will appear in the Properties screen and earn income.

```csharp
new()
{
    name = "Bakery",
    desc = "PROFIT gold/day",
    value = 6000,
    income = 12,
    upkeep = 6,
    efficiency = 50,
    status = Status.Active,
    cityIndex = 0,
    multi = true,
    upgrades = new Upgrade[]
    {
        new()
        {
            name = "Ovens",
            desc = "+4 income",
            value = 1200,
            income = 4
        },
        new()
        {
            name = "Extra guards",
            desc = "Prevents monster invasion, +1 upkeep",
            value = 800,
            upkeep = 1
        }
    }
}
```

What works automatically:

- The property is copied into `Game.properties` at new game and listed by
  `PropertiesScreen`.
- It can be bought, sold, earns `Income`, pays `Upkeep`, gets efficiency decay
  and Buff/Infested events, and can receive a worker through the People screen.
- Both upgrades appear and apply their numeric effects.
- Save/load works because the instance and its upgrades are serialized.

What does **not** work automatically:

- There is no `M` shortcut to manage it in the city. `Game.GetPropertyHere`
  returns only the Inn for `TileType.City`/`Village` (`Game.cs:2618-2627`), and
  `Game.GetLocationIndex` returns `-1` for it (`Game.cs:2486-2492`), so
  `Worker.locationIndex` cannot point at it.
- It cannot be managed remotely unless the player has the Mansion `Office`
  upgrade, because `PropertiesScreen` only enables `BtManage` under that
  condition (`PropertiesScreen.cs:18-19`, `208-209`).
- It has no interior, rest bonus, storage, garden, craft bonus, or local work
  option. Those are all hard-coded to House/Mansion/Inn.

To make it fully usable, see section 2.7.

### 2.3 City, `cityIndex` and `multi`

- `cityIndex` is an index into `World.cityMapping`, which has exactly three
  entries: city 0, village 1 and village 2 (`World.cs:147-152`). A new city
  requires world-generation and naming changes; do not invent a new index
  without adding it there.
- `cityIndex = -1` means "available everywhere" in
  `PropertiesScreen.Refresh` (`PropertiesScreen.cs:59-60`). It is used by
  Horses, not by buildings.
- `multi = true` only appends the city name to the display name
  (`Property.cs:121-130`). It does **not** create per-city instances,
  enforce uniqueness, or change the save format. You still need one template
  entry per city, as House/Inn/Farm demonstrate.
- `Name` calls `Global.World.GetCityTile(cityIndex)` when `multi` is true. A
  `multi` template with an invalid `cityIndex` throws at display time.

### 2.4 Map-location properties and `locationIndexFunc`

`locationIndexFunc` is a `Func<World, int>` on the template
(`Property.cs:58`). It is executed once per new game and again by
`RegenerateProperties`/`RegenerateWorld`, not on load.

Rules:

- Return a valid index from `World.FindLocationIndex` or
  `World.FindRandomLocationIndex`. `-1` means the property cannot be bought or
  built and only logs an editor warning (`Game.cs:1639-1642`,
  `Game.cs:2339-2342`).
- The predicate must identify a **unique** tile. `FindLocationIndex` returns the
  first match (`World.cs:826-834`). Two templates with the same predicate will
  share one location and fight over it.
- The tile must already exist or be spawned by `World.Init`. Existing spawns are
  one Sawmill, one Mine, two Farms and hidden caves
  (`World.cs:203-226`). Adding a new map property normally means adding a
  `SpawnLocation`/`SpawnHiddenLocations` call.
- `Property.Copy()` does not copy `locationIndex` (`Property.cs:240-271`), so
  every code path that creates a runtime copy must set it. Today that is
  `Game.NewGame` (`Game.cs:1635-1645`), `Game.RegenerateProperties`
  (`Game.cs:2335-2345`) and `Game.RegenerateWorld` (`Game.cs:2210-2247`).
- `Property.Update()` does not copy `locationIndexFunc` (delegates are not
  serialized) and does not copy `locationIndex`.
- For a hidden cave mine, `RegenerateWorld` also rewrites the tile type/image
  (`Game.cs:2226-2237`); a new property on a hidden location needs equivalent
  handling.

### 2.5 Adding a new `TileType` (example: Quarry)

Only do this if the property needs a distinct map tile and background. If an
existing tile type is acceptable, skip to 2.6.

Append the enum values; never insert in the middle, because `TileType` and
`TileImage` are serialized as numbers in saves.

1. **`TileType`** (`TileType.cs:1-22`): append `Quarry` after
   `EnchantedForest`. This becomes value 18.
2. **`TileImage`** (`TileType.cs:24-42`): append `Quarry` after
   `EnchantedForest`. This becomes value 16.
3. **`TileTypeMethods.AsString`** (`TileType.cs:46-70`): add
   `TileType.Quarry => "quarry"`.
4. **Classification** (`TileType.cs:72-108`):
   - `IsProperty`: add `|| tileType == TileType.Quarry` so `Game` treats it as
     a building (`Game.cs:1125`, `Game.cs:2568`, `Game.cs:2273`).
   - `IsClearable`: add it if monsters can occupy it.
   - `IsSafe`: usually leave false.
   - `IsSmall`: usually leave false.
5. **`Tile.CanSkipTime`** (`Tile.cs:15-23`): add
   `TileType.Quarry => clear` if the player can skip time there.
6. **`Tile.GetName`** (`Tile.cs:25-75`): add a case if the name depends on
   difficulty; otherwise the default `AsString()` is used.
7. **`Tile.SetType`** (`Tile.cs:77-130`): add a case that sets
   `image = TileImage.Quarry`.
8. **`World.Init`** (`World.cs:203-226`): spawn the tile, for example
   `SpawnLocation(cityPos, TileType.Mountains, TileType.Quarry)`.
9. **Map sprite** (`GameDialogs/Map.cs:62`, `303`): `sprites[(int)tile.image]`
   indexes the `Map` component's `sprites` array. The scene currently has 16
   entries for the 16 `TileImage` values. Add a sprite at index 16.
10. **Background sprite** (`GameUI.cs:186-189`): `backgrounds[(int)tile.type]`
    indexes the `GameUI` component's `backgrounds` array. The scene currently
    has 18 entries for `TileType` 0-17. Add a sprite at index 18.
11. **`Game.Update` key handling** (`Game.cs:161-227`): add a `case
    TileType.Quarry:` with the desired keys (at minimum `W` work and `M`
    manage, matching Sawmill/Mine/Farm at `Game.cs:215-222`).
12. **`Game.UpdateButtons`** (`Game.cs:1769`): add the tile to `BtWork2`.
13. **`Game.GetPropertyHere`** (`Game.cs:2618-2627`): add the tile to the
    `Sawmill or Mine or Farm` arm.
14. **`Game.OnRest`** (`Game.cs:1299-1304`): add the tile to the barracks arm if
    resting there should restore the party.
15. **`Game.DoWork`** (`Game.cs:897-958`): add a `case` with payment and skill.
16. **`Game.OnNewDay` build completion** (`Game.cs:1496-1504`): add the tile to
    the non-Farm branch, or replace the hard-coded `Farm`/`Mine` choice with a
    template field.
17. **Quests / enemies** (optional): `Game.GenerateQuest`
    (`Game.cs:1809-1961`) filters by tile type and property fields; `Enemy`
    location lists (`Enemy.cs:103-253`) decide where enemies spawn.
18. **`readme.txt`**: document any new key.
19. **`stats.txt`**: add balance data if it is a producer.

### 2.6 Map property that reuses an existing tile type

If the new property is, for example, a second Farm, you can skip the enum and
sprite work, but you still must:

- Give it a unique `locationIndexFunc` predicate (difficulty, hidden state, or a
  new spawned tile).
- Add a `SpawnLocation`/`SpawnHiddenLocations` call if no free tile exists.
- Add it to `Game.GetPropertyHere` if it uses a new tile type. If it reuses
  `TileType.Farm`, the existing arm already returns any property at the current
  location (`Game.cs:2623`), so no change is needed there.
- Accept that `Game.DoWork` payment and skill come from the **tile type**, not
  the property (`Game.cs:903-925`). Two properties on the same tile type share
  the same work formula.
- Accept that build completion sets `TileType.Mine` for every non-Farm buildable
  property (`Game.cs:1503-1504`) and uses city 2's name for every Farm
  (`Game.cs:1496-1502`). A new buildable property needs that branch changed.

### 2.7 City / interior property hooks

For a city service such as the Bakery, extend these lookups:

| Need | Current code | Required change |
| --- | --- | --- |
| Local manage (`M`) | `Game.GetPropertyHere` returns Inn only for cities (`Game.cs:2618-2627`) | Return the new property, or a selected property, for the current city. |
| Worker assignment | `Game.GetLocationIndex` special-cases Inn (`Game.cs:2486-2492`); `Worker.locationIndex` is an `int` (`Worker.cs:8`) | Give each city property a unique pseudo-index, or change workers to store a property reference/id. |
| Reverse lookup | `Game.GetProperty(int)` falls back only to Inn (`Game.cs:2494-2506`) | Resolve the new pseudo-index/property. |
| Entering the building | `Game.EnterHouse` chooses House vs Mansion (`Game.cs:1029-1043`); `GetPropertyInside` only House/Mansion (`Game.cs:2475-2484`) | Add a sublocation and a `GetPropertyInside` case. |
| Rest bonus | `Game.OnRest` checks House/Mansion/Inn (`Game.cs:1269-1290`) | Add a branch if the property changes rest. |
| House button | `Game.UpdateButtons` chooses `BtHouse` by House/Mansion (`Game.cs:1693-1705`) | Add a button/label or generalize. |
| Storage/garden | `PropertiesScreen` initializes `storedItems`/`gardenPlants` only for House/Mansion (`PropertiesScreen.cs:167-175`) | Initialize for the new property; otherwise `StoreItemsScreen.cs:76` / `GardenScreen.cs:41` throw. |
| Background | `GameUI.backgrounds[(int)tile.type]` (`GameUI.cs:186-189`) | Add an interior `TileType`/`TileImage` and sprites if it has a unique interior. |
| Remote manage | `PropertiesScreen` requires Mansion Office (`PropertiesScreen.cs:18-19`) | Decide whether the new property is manageable without it. |

For an **interior**, also add a `World.sublocations` entry
(`World.cs:154-186`), update the `sublocation` comment/values (`World.cs:36`),
and handle enter/exit in `Game.EnterHouse`/`Game.ExitToCity`. The current
interior indices are fixed: 0 none, 1 sewers, 2 house, 3 mansion, 4 dark
dimension.

### 2.8 Icons and scene assets

| Array | Used by | Current size | When to extend |
| --- | --- | --- | --- |
| `GameUI.propertyIcons` | `PropertiesScreen.cs:69`, `198` with `(int)property.GetImage()` | 4 (matches `Property.Image`) | New `Property.Image` value. |
| `GameUI.backgrounds` | `GameUI.cs:188` with `(int)tile.type` | 18 (matches `TileType` 0-17) | New `TileType`. |
| `Map.sprites` | `Map.cs:62`, `303` with `(int)tile.image` | 16 (matches `TileImage`) | New `TileImage`. |

The arrays live on components in `Assets/Scenes/Game.unity`; add the sprite at
the same index in the Inspector. A missing entry is an
`IndexOutOfRangeException` at runtime, not a compile error.

### 2.9 Scene and screen wiring

`Game.Awake` resolves screens by hard-coded child name
(`Game.cs:72-86`):

```csharp
propertiesScreen = transform.Find("Properties").GetComponent<PropertiesScreen>();
```

`Game.OnEnable` assigns the static `GameDialog` members
(`Game.cs:119-125`), and `GameDialog` screens read `game`, `ui`, `player` and
`text` statically. A new property screen therefore needs:

1. A GameObject under the `Game` canvas with the exact name used by
   `transform.Find`.
2. A `Game` field and an `Awake` lookup.
3. A `GameDialog` subclass; use `ui.ShowDialog(gameObject)` and `Refresh`.
4. The child paths the screen uses. `PropertiesScreen` currently uses `List`,
   `List/Viewport/Content`, `Text`, `Upgrades/Viewport/Content` and `BtManage`
   (`PropertiesScreen.cs:35-54`, `206-252`).
5. Button GameObjects under `Buttons` if it adds a shortcut
   (`BtProperties`, `BtHouse`, `BtManage`, `BtWork2`, `BtStorage`, `BtCook`,
   `BtCraft`, `BtGarden`; `Game.cs:1686-1776`).

### 2.10 Save/load

- A property instance is serialized as part of `Game`/`Player`; its fields and
  the nested `Upgrade` fields are part of the save.
- Templates are code-only. Changing a template does not rewrite an existing
  save unless `RegenerateProperties` runs.
- `Property.Copy()` resets `active` upgrades and does not copy
  `storedItems`/`gardenPlants`; `Property.Update()` preserves upgrade `active`
  by name but does not sync `value`, `income`, `upkeep` or `buildTime`
  (`Property.cs:240-296`).
- Do not rename `Property` fields, `Upgrade` fields, or the `name` string of an
  existing property. Old saves store the object graph, and gameplay lookups use
  the name string.
- Append enum values (`Property.Status`, `Property.Image`, `TileType`,
  `TileImage`); never reorder them.

### 2.11 Verification checklist for a new property

1. Compile in the Unity Editor; Console has no errors.
2. New game: the property appears in the Properties screen in the intended city.
3. Buy path: gold deducted, property moves to "Your properties", tile clear
   state/quest handling is correct.
4. Build path: `buildTime` counts down, completion changes the tile, map icon is
   removed, notification appears.
5. Income: next day's `goldWaiting` includes the correct profit; worker upkeep
   is subtracted once.
6. Events: after 10 days the property can get Buff/Infested; Extra guards
   prevents infestation.
7. Efficiency: managing it changes efficiency; 10 days without management
   decays it.
8. Worker: assign/unassign from the People screen; worker improves efficiency
   and trains.
9. Upgrades: buy each one; stats/desc update; cannot buy twice; sell refunds
   the upgraded value.
10. Management UI: `M`/`BtManage` works where intended; remote manage works only
    with the correct Mansion Office.
11. Sell: property returns to the sale list, worker is unassigned, special
    side effects (horses/rest/buttons) are correct.
12. Save, exit to menu, continue: property, upgrades, efficiency, worker and
    location persist.
13. Load a save created before the property existed: existing properties still
    load and no lookup throws.
14. New `TileType` only: map sprite, background sprite, tile name, work/rest
    buttons, property icon and quest generation all work.
15. Regression: House, Mansion, Inn, Farm, Sawmill and all three mines still
    behave as before.

---

## 3. Adding a new `Property.Upgrade`

### 3.1 Numeric-only upgrade (data only)

If the upgrade only changes cost, income or upkeep, add it to the template:

```csharp
new()
{
    name = "Ovens",
    desc = "+4 income",
    value = 1200,
    income = 4
}
```

This works because `PropertiesScreen.RefreshDetails` already:

- lists every inactive upgrade (`PropertiesScreen.cs:248-252`);
- checks the gold cost (`PropertiesScreen.cs:256-260`);
- sets `active = true`, adds `value` to the property value, and adds
  `income`/`upkeep` (`PropertiesScreen.cs:262-268`);
- shows active upgrades in the details text (`PropertiesScreen.cs:236-240`).

`Copy()` and `Update()` already map `name`, `desc`, `value`, `income` and
`upkeep` (`Property.cs:247-254`, `286-294`), so no C# change is required.

Caveats:

- `value` is both the cost and a permanent increase to the property's sell
  value.
- `upkeep` is added to the raw `upkeep`; if the property starts at 0 upkeep, the
  code also appends `", UPKEEP upkeep"` to the description once
  (`PropertiesScreen.cs:264-265`).
- There is no "already owned" check beyond `active`; the upgrade cannot be
  bought twice because it disappears from the inactive list.
- There is no requirement/level system. If the upgrade should require another
  upgrade, a level, a skill, a worker, or a resource, that logic does not exist.

### 3.2 Behaviour upgrade (code changes required)

A behaviour upgrade is recognised by its `name` string. To add one, you need at
least:

1. **Template data.** Add the upgrade with `name`, `desc`, `value` and any
   numeric fields.
2. **Purchase hook.** Add a branch in `PropertiesScreen.RefreshDetails` next to
   the existing `Extra guards`, `Stables`, `Alchemy lab` and `Garden` branches
   (`PropertiesScreen.cs:270-284`).
3. **Runtime hook.** Add the check where the behaviour matters. For example:
   - travel/rest: `Game.cs:1158-1159`, `1280-1281`;
   - combat/events: `Game.cs:1578`;
   - craft/alchemy: `CraftScreen.cs:24-25`, `Game.cs:2124-2129`;
   - UI visibility: `Game.cs:1690`, `1773-1776`;
   - property details/manage: `PropertiesScreen.cs:18-19`, `208-209`.
4. **Display hook** if the effect is not a simple stat: update `Desc` or the
   details panel. `Upgrade.ToString()` only prints name, desc and value.
5. **Save compatibility.** `active` is serialized; old saves deserialize a new
   upgrade as absent until the template is regenerated, and new `Upgrade`
   fields default to `0`/`false`.

There is no generic effect system. A cleaner alternative is proposed in
section 4.4.

### 3.3 `Copy()` / `Update()` field mapping

When adding a field to `Property.Upgrade`, update **both** mapping sites:

| Field | `Copy()` (`Property.cs:247-254`) | `Update()` (`Property.cs:286-294`) |
| --- | --- | --- |
| `name` | copied | copied |
| `desc` | copied | copied |
| `value` | copied | copied |
| `income` | copied | copied |
| `upkeep` | copied | copied |
| `active` | forced `false` | preserved by matching name |

Forgetting `Copy()` means new games lose the field. Forgetting `Update()` means
`RegenerateProperties` (editor) loses it for existing saves. `Update()` also
reads `upgrades.Any(...)` when the template has upgrades; if the existing
instance's `upgrades` is `null`, this throws (`Property.cs:293`).

### 3.4 Verification checklist for a new upgrade

1. Compile; Console has no errors.
2. New game: the upgrade appears for the intended property only.
3. Buy it: gold deducted, `active` set, stats/desc update, cannot buy again.
4. Effect is visible in the correct place (income next day, travel speed, craft
   bonus, event prevention, UI button, etc.).
5. Sell the property: refund includes the upgrade value; re-buying the property
   starts with the upgrade inactive.
6. Save/load: the active upgrade persists; a save created before the upgrade
   still loads and shows it as inactive.
7. `RegenerateProperties` (editor Context Menu) updates the template data
   without losing `active` or throwing.
8. Regression: existing upgrades (Extra guards, Stables, Office, Alchemy lab,
   Garden, numeric upgrades) still work.

---

## 4. Pain points and hard-coded logic

### 4.1 Top pain points

| # | Pain point | Evidence | Impact when adding content |
| ---: | --- | --- | --- |
| 1 | Property identity is a display string. | `Player.HaveProperty` matches `x.name == name` (`Player.cs:38-48`); dozens of `"House"`, `"Inn"`, `"Farm"` checks. | Renaming a property breaks gameplay and old saves. A new property needs name checks in every behaviour it shares. |
| 2 | `Status` means different things in different lists. | `Property.cs:28-34`; `PropertiesScreen.cs:59`, `118`; `Game.cs:1458`, `2226`. | A new template must pick `Active` vs `Cleared` correctly; adding a status requires auditing comparisons. |
| 3 | City properties have no unique location. | `GetLocationIndex` special-cases only Inn (`Game.cs:2486-2492`); `GetProperty` fallback only Inn (`Game.cs:2494-2506`). | A new city income property cannot be managed locally or assigned a worker. |
| 4 | `GetPropertyHere` / `GetPropertyInside` are hard-coded by tile/name. | `Game.cs:2475-2484`, `2618-2627`. | New interior/city property is invisible to `M`, `BtManage`, storage, garden, rest. |
| 5 | Build completion hard-codes Farm vs Mine. | `Game.cs:1496-1504`; also `Game.cs:2238-2244`. | A new buildable property becomes a Mine tile, and every Farm uses city 2's name. |
| 6 | Upgrade effects are hard-coded by name. | `PropertiesScreen.cs:270-284`; `Game.cs:1578`; `CraftScreen.cs:24-25`; `Game.cs:2124-2129`. | A new behaviour upgrade needs edits in 3-6 files; it is not data-driven. |
| 7 | `Property` getters depend on static `Global.Player`. | `Property.cs:87`, `101`, `115`. | Cannot evaluate a property without a live game/player; NRE before init; hard to test. |
| 8 | `Copy()` and `Update()` duplicate the field list and disagree. | `Property.cs:240-296`. | Adding a field is easy to forget in one of them; `Update()` does not sync value/income/upkeep/buildTime and can NRE. |
| 9 | No save versioning or migration. | `Game.cs:1654-1666`; `AGENTS.md`. | Renaming a field/class/property name silently breaks old saves. |
| 10 | Enum values index scene sprite arrays. | `GameUI.cs:16`, `188`; `Map.cs:62`, `303`; `PropertiesScreen.cs:69`, `198`. | Adding `Property.Image`, `TileType` or `TileImage` without a sprite throws at runtime. |

### 4.2 Detailed hard-coded inventory

#### `Property.cs`

| Line | Code / check | Why it is hard-coded |
| --- | --- | --- |
| 87 | `producer && Global.Player.HaveProperty("Office")` | The Office property bonus is a literal name and a static lookup. |
| 101 | `HaveProperty("Sawmill", true)` | The mine upkeep discount requires a literal Sawmill. |
| 115 | `HaveProperty("Sawmill", true)` | The build-price discount requires a literal Sawmill. |
| 126 | `Global.World.GetCityTile(cityIndex).Name` | Display name depends on a valid global city index. |
| 131 | `desc.Replace("PROFIT", ...).Replace("UPKEEP", upkeep.ToString())` | Text placeholders and raw upkeep; no localization; ignores the Sawmill discount. |
| 205 | `buildTime` in `ToString(Build)` | Build text assumes days. |
| 240-271 | `Copy()` field list | Does not copy `locationIndex`, `locationIndexFunc`, `storedItems`, `gardenPlants`, `farmedToday`, `lastManaged`; resets upgrades. |
| 273-296 | `Update()` field list | Does not sync `value`, `income`, `upkeep`, `buildTime`, `status`, `cityIndex`; `upgrades.Any` can NRE. |
| 314-727 | Template array | Duplicated per-city rows; no shared id; `Office` name used twice. |

#### `Game.cs`

| Line | Code / check | Why it is hard-coded |
| --- | --- | --- |
| 174, 186 | `H` + `HaveProperty("House")`/`"Mansion"` | City/village house shortcut. |
| 176, 188 | `M` + `HaveProperty("Inn")` | City/village inn management shortcut. |
| 209 | `M` + Mansion + `HavePropertyUpgrade("Mansion", "Office")` | Office shortcut; no `cityIndex` argument. |
| 211-212 | `G` + House/Mansion + `HavePropertyUpgrade(..., "Garden")` | Garden shortcut. |
| 215-221 | `case Sawmill/Mine/Farm` | Work/manage shortcuts for map properties. |
| 749-754 | `tile.mine` → property `status = Cleared` | Clearing a cave always offers a mine. |
| 757-762 | `tile.type == Mine/Sawmill/Farm` → `RemoveEvent("Infested")` | Property tile types are a fixed set. |
| 1031, 1038 | House vs Mansion → sublocation 2/3 | Entering a building assumes only these two. |
| 1099-1102 | `properties.First(x => x.name == "Inn" && x.cityIndex == 1)` | Spider quest reward is tied to one Inn. |
| 1128-1130 | building text by `property.name` | Display only, but name-based. |
| 1158-1159 | Horses + Mansion Stables → `freshHorses = 10` | Travel bonus tied to three names. |
| 1269-1290 | Rest branches by House/Mansion/Inn | New rest property needs a new branch. |
| 1457-1487 | Income sum + `hiredWorkers.Count * 2` | Worker upkeep is global, not per property. |
| 1489-1516 | `property.name == "Farm"` else `TileType.Mine` | Build completion is hard-coded. |
| 1524-1533 | `gardenPlants` growth | Assumes the property has a garden list. |
| 1544-1550 | `income > 0` efficiency decay | All income properties share the rule. |
| 1553-1595 | event messages by `Sawmill`/`Farm`/`Inn`; `HaveUpgrade("Extra guards")` | New property gets ore-themed text; new guard upgrade must use the same name. |
| 1690 | `BtProperties` + Mansion Office | No `cityIndex`. |
| 1693-1705 | `BtHouse` label House/Mansion | New residence needs a new label. |
| 1734-1747 | `BtManage` label `"inn"` or `location.AsString()` | City management assumes Inn. |
| 1769 | `BtWork2` Sawmill/Mine/Farm | New map tile needs adding. |
| 1771-1776 | `BtStorage`/`BtCook`/`BtCraft`/`BtGarden` House/Mansion | New interior needs adding. |
| 1881-1882 | `"Silver mine"`/`"Gold mine"` | Quest generation is name-specific. |
| 2124-2129 | Alchemy lab bonus House/Mansion | New property with an alchemy bonus needs a branch. |
| 2313-2318 | GiveAll grants Horses | Dev helper is name-specific. |
| 2475-2484 | `GetPropertyInside` House/Mansion | New interior returns null. |
| 2486-2492 | `GetLocationIndex` Inn → `cityMapping` | New city property gets `-1`. |
| 2494-2506 | `GetProperty` Inn fallback | New city property cannot be resolved from a worker index. |
| 2618-2627 | `GetPropertyHere` fixed switch | New tile/city property is not manageable. |
| 2662-2673 | `UpdateWorkers` assumes `GetProperty` non-null | A removed/reassigned property can NRE. |

#### `Player.cs`

| Line | Code | Why it is hard-coded |
| --- | --- | --- |
| 38-48 | `HaveProperty(string name, bool isActive, int cityIndex)` | Name-based lookup; `isActive` only checks `Infested`. |
| 50-53 | `HaveProperty(int locationIndex)` | Any property at a location matches, regardless of name/status. |
| 55-61 | `HavePropertyUpgrade(propertyName, upgradeName, cityIndex)` | Both levels are name strings. |

#### `PropertiesScreen.cs`

| Line | Code | Why it is hard-coded |
| --- | --- | --- |
| 18-19, 208-209 | Manage requires City/Mansion + Office + income > 0 | No city index; new city property cannot be managed without Office. |
| 59-60 | city filter | `-1` means everywhere. |
| 92-95 | sell side effects House/Mansion/Inn/Horses | New property with a global effect needs a branch. |
| 129-134 | can't own House and Mansion in one city | Hard-coded law. |
| 167-175 | storage/garden slots only House/Mansion | New storage/garden property NREs. |
| 176-189 | Inn city 1 spider quest | Hard-coded quest interaction. |
| 217 | building text | Assumes `buildTime` days. |
| 229 | Income/Upkeep/Profit details | Fine, but uses computed values while `Desc` uses raw. |
| 236-240 | active upgrade list | Fine for numeric upgrades. |
| 248-294 | upgrade purchase | Direct field mutation; no central `ApplyUpgrade`. |
| 264-265 | append `", UPKEEP upkeep"` | Only when base upkeep is 0; one-time formatting hack. |
| 270-284 | Extra guards/Stables/Alchemy lab/Garden branches | Behaviour by upgrade name. |

#### `World.cs`, `TileType.cs`, `Tile.cs`

| Line | Code | Why it is hard-coded |
| --- | --- | --- |
| `World.cs:147-152` | `cityMapping` has 3 entries | Adding a city requires world/naming/UI changes. |
| `World.cs:154-186` | `sublocations` fixed 0-4 | Adding an interior requires a new index and tile. |
| `World.cs:203-210` | spawns one Sawmill, one Mine, two Farms | New map property needs a spawn. |
| `World.cs:520-531`, `646-657` | Horses travel speed | Global passive property by name. |
| `World.cs:893-895` | `Global.Game.properties` status `None` | Uses the for-sale list, not `player.properties`. |
| `TileType.cs:72-108` | `IsSmall`/`IsClearable`/`IsSafe`/`IsProperty` | Fixed type lists. |
| `TileType.cs:24-42` | `TileImage` enum | Indexes `Map.sprites`. |
| `Tile.cs:15-23` | `CanSkipTime` | Fixed type list. |
| `Tile.cs:51-61` | Mine names by difficulty | New tile type needs a name case. |
| `Tile.cs:77-130` | `SetType` switch | New tile type needs an image case. |

#### Other screens and systems

| Location | Code | Why it is hard-coded |
| --- | --- | --- |
| `CraftScreen.cs:24-25` | House/Mansion + Alchemy lab | New alchemy property needs a branch. |
| `GardenScreen.cs:39-41`, `96-99` | `GetPropertyInside` + `gardenPlants` | New garden property NREs if slots are not initialized. |
| `StoreItemsScreen.cs:9`, `76` | `GetPropertyInside` + `storedItems` | New storage property NREs if slots are not initialized. |
| `SkipTimeScreen.cs:26-32`, `73-92` | tile list + Garden/Manage options | New work/rest property needs adding. |
| `PeopleScreen.cs:82`, `106` | `GetProperty(worker.locationIndex)` + `GetLocationIndex` | Worker assignment is location-int based. |
| `Team.cs:13`, `228-229`, `241-248` | `freshHorses`, `PayForProperty` | Stables/travel and ally payment are global. |
| `Hero.cs:21`, `188-189`, `199-200` | `rested` bonus | Mansion rest effect is a Hero field. |
| `GameUI.cs:16`, `188` | `propertyIcons`, `backgrounds` | Enum-indexed scene arrays. |
| `Map.cs:62`, `303` | `sprites[(int)tile.image]` | Enum-indexed scene array. |

### 4.3 Concrete defects and risks found while analysing

These are observations from the current code, not changes made by this
document.

1. **`Desc` shows the wrong upkeep.** `Property.Desc` uses the raw `upkeep`
   field (`Property.cs:131`), while the details panel uses the computed
   `Upkeep` (`PropertiesScreen.cs:229`). With a Sawmill, a mine shows 10 upkeep
   in its description but 8 in the stats row.
2. **Non-producer prices are silently 90 %.** `Value` scales by efficiency and
   defaults to `0.9` when `efficiency` is 0 (`Property.cs:64-71`), and
   `PropertiesScreen` uses `Value` as the buy price (`PropertiesScreen.cs:122`).
   House/Mansion/Inn/Horses/Office therefore buy and sell below their `value`
   field. Set `efficiency = 50` for a neutral price, or use `value` directly for
   fixed-price properties.
3. **`Property.Update()` can throw.** When a template gains upgrades, the
   existing instance may have `upgrades == null`, and
   `active = upgrades.Any(...)` (`Property.cs:293`) throws. Guard with
   `upgrades != null` or use `p.upgrades`/an empty array.
4. **`Property.Update()` does not rebalance.** It copies `desc`, infest fields,
   discounts, build price, `multi`, `producer` and upgrades, but not `value`,
   `income`, `upkeep`, `buildTime`, `status` or `cityIndex`
   (`Property.cs:273-296`). Changing those in the template will not affect an
   existing save through `RegenerateProperties`.
5. **`buildTime = 0` never completes.** Completion checks `buildTime == 0`
   after `--buildTime` (`Game.cs:1489-1492`), so a buildable template with 0
   goes to -1 and stays `Building`. A buildable property must have
   `buildTime >= 1`.
6. **Build completion is wrong for new types.** Every non-`Farm` buildable
   property becomes `TileType.Mine` (`Game.cs:1503-1504`), and every `Farm` uses
   `world.GetCityTile(2).name` (`Game.cs:1499`). A new buildable property in
   another city or with another tile type is misnamed/mis-typed.
7. **City properties cannot be workers' targets.** `GetLocationIndex` returns
   `-1` for a new city property (`Game.cs:2486-2492`), and `Worker.locationIndex`
   uses `-1` for "unassigned" (`Worker.cs:8`, `PeopleScreen.cs:70`). The worker
   UI would silently unassign or fail to assign.
8. **Office checks omit `cityIndex`.** `Game.cs:209`, `Game.cs:1690`,
   `PropertiesScreen.cs:19` and `PropertiesScreen.cs:209` call
   `HavePropertyUpgrade("Mansion", "Office")` without a city. Today only city 0
   has a Mansion, so it is latent; adding Mansions to other cities would enable
   remote management everywhere.
9. **`Office` naming collision.** The standalone `Office` property
   (`Property.cs:720-726`) and the Mansion `Office` upgrade
   (`Property.cs:422-427`) share a name but are different systems.
   `HaveProperty("Office")` and `HavePropertyUpgrade("Mansion", "Office")` are
   easy to confuse.
10. **`producer` is narrower than its name.** It only adds the Office +10 %
    bonus (`Property.cs:87`). Income, events, efficiency and workers apply to
    every property with `income > 0` (`Game.cs:1544`, `1553`). A non-producer
    with income still gets the full producer lifecycle.
11. **`multi` does not mean multi-instance.** It only changes the display name
    (`Property.cs:121-130`). Per-city instances are separate template rows.
12. **No upgrade prerequisites or undo.** `todo.txt` lists "undo upgrades" and
    "upgrades take time" as future work; neither exists. All inactive upgrades
    are immediately purchasable.
13. **No save migration.** `Game.SaveGame` serializes the whole object graph
    and `LoadGame` overwrites it (`Game.cs:1654-1666`). Renaming a field, class
    or property name breaks old saves.
14. **Scene arrays are enum-indexed.** `propertyIcons`, `backgrounds` and
    `Map.sprites` are indexed by enum values. Adding an enum value without a
    sprite is a runtime exception, not a compile error.
15. **`World.Update` uses the wrong list for infested mines.**
    `Global.Game.properties` is the for-sale list (`Game.cs:34`); a player-owned
    mine is in `player.properties`, so the `status = None` branch
    (`World.cs:893-895`) only affects an unsold offer. Verify this is intended
    before reusing the pattern for a new property.
16. **`UpdateWorkers` assumes the property still exists.**
    `GetProperty(worker.locationIndex)` can return `null` and is dereferenced
    without a check (`Game.cs:2667-2668`). Selling unassigns the worker, but
    other removal paths (load/regenerate) can leave a stale index.
17. **Worker upkeep is global.** `player.goldWaiting -= hiredWorkers.Count * 2`
    (`Game.cs:1487`) is not per property, even though `Worker.ToStringHire`
    describes "2 upkeep" (`Worker.cs:15`).
18. **Event text assumes a mine/farm/inn.** A new property with income gets
    "good ore quality"/"new ore veins" text (`Game.cs:1569-1572`) unless a new
    branch is added.

### 4.4 Proposed refactor (changes required to make additions safe)

These are recommendations; none are implemented in this checkout.

**Phase 1 - low-risk helpers (no save format change)**

- Add stable `id` fields to `Property` and `Property.Upgrade`, separate from the
  display `name`. Use `id` for lookups and `name` for text.
- Add `Property.HasFeature(PropertyFeature)` with flags such as `Rest`,
  `Storage`, `Garden`, `Craft`, `RemoteManage`, `TravelSpeed`, `AlchemyBonus`,
  `PreventsInfestation`. Replace the name checks in `Game`,
  `PropertiesScreen`, `CraftScreen`, `GardenScreen`, `SkipStore` and `World`.
- Add `Property.ApplyUpgrade(Upgrade)` and move the mutation currently in
  `PropertiesScreen.cs:262-284` there. Add `Upgrade.effect` (enum) or a small
  `UpgradeEffect` object so behaviour upgrades are data, not `if (name == ...)`.
- Add `Property.IsForSale`, `IsOwned`, `IsOperational` helpers so `Status` is
  not compared directly everywhere.
- Add `Property.DisplayUpkeep` or make `Desc` use `Upkeep`.
- Add `Property.InitRuntimeState()` that creates `storedItems`/`gardenPlants`
  when the template says it has storage/garden.
- Add an editor `ValidateProperties` Context Menu that checks unique ids, valid
  `cityIndex`, `locationIndexFunc` results, `buildTime >= 1`, upgrade field
  mapping and sprite-array sizes.

**Phase 2 - save migration**

- Add `saveVersion` to `Game`.
- On load, migrate old property objects by `name + cityIndex` to the new `id`.
- Keep `name` unchanged for old saves; never rename an existing property.
- Add default values for new fields in `Property`/`Upgrade` and guard nulls
  (`upgrades`, `events`, `storedItems`, `gardenPlants`).

**Phase 3 - location model**

- Replace the `int locationIndex` worker assignment with a stable property id
  or a `[SerializeReference] Property` reference. `todo.txt` already lists
  "store worker assigned property as reference".
- Introduce a `PropertyLocation` value: `City(cityIndex)`, `Map(tileIndex)`,
  `Interior(sublocation)`. Use it in `GetLocationIndex`, `GetProperty`,
  `GetPropertyHere` and `GetPropertyInside`.
- Give city properties unique pseudo-indices, or stop using a single
  `cityMapping[cityIndex]` for all city buildings.

**Phase 4 - tile and UI data**

- Put the created tile type/image and background index on the template, e.g.
  `PropertyDef.tileType`, `PropertyDef.tileImage`, `PropertyDef.icon`.
- Replace `Game.Awake`'s `transform.Find("...")` screen registry with a serialized
  array or a small screen map, so adding a screen does not require editing
  `Awake`.
- Replace `TileType` classification switches with data flags
  (`CanWork`, `CanRest`, `CanSkipTime`, `IsProperty`, `IsClearable`) where
  practical.

**Phase 5 - validation**

- Add editor-only validation that every `Property.properties` entry has a
  matching icon/background/tile sprite and a unique `locationIndexFunc` result.
- Add a debug Context Menu that buys/builds every property and runs 30 days to
  exercise income, events, efficiency, worker and save/load paths. There is no
  test suite, so editor helpers are the practical safety net.

### 4.5 What not to do

- Do not rename an existing property `name`, `Property` field or `Upgrade`
  field; old saves and string lookups depend on them.
- Do not insert or reorder `Property.Status`, `Property.Image`, `TileType` or
  `TileImage` values.
- Do not add a new `TileType`/`TileImage`/`Property.Image` without extending the
  matching scene array.
- Do not assume `locationIndex` is valid for a city/interior property; it is
  `-1` today.
- Do not assume `storedItems`/`gardenPlants` are non-null; only House/Mansion
  initialise them.

---

## 5. Save/load compatibility summary

`Game.SaveGame` writes `JsonUtility.ToJson(this)` and `LoadGame` uses
`JsonUtility.FromJsonOverwrite` (`Game.cs:1654-1666`). `player` and team members
are `[SerializeReference]` (`Game.cs:30-31`). There is no versioning or
migration.

Rules for a new property/upgrade:

1. **Append enum values.** `Status`, `Image`, `TileType`, `TileImage`.
2. **Do not rename serialized fields or classes.** `Property`, `Upgrade`,
   `Event`, `Player.properties`, `Game.properties`.
3. **Do not rename an existing property's `name`.** Gameplay lookups and many
   UI strings use it.
4. **New fields default to `0`, `false` or `null`.** Initialise them in
   `Copy`, `NewGame` or a runtime-init method; guard null lists.
5. **`Property.Copy()` is not a full clone.** It resets upgrades, omits
   `locationIndex`, storage and garden. Set runtime-only fields explicitly.
6. **`Property.Update()` is a partial template sync.** It preserves upgrade
   `active` and some fields but not `value`/`income`/`upkeep`/`buildTime`.
7. **`locationIndexFunc` is not serialized.** Recompute it on new game and on
   the editor regenerate helpers, not on load.
8. **Test old saves.** Create a save, add the property/upgrade, load the save,
   and confirm no `NullReferenceException` and no missing sprite/index.

---

## 6. Verification checklist (whole feature)

There is no automated gameplay test or CLI run of the game. Do the following in
the Unity Editor after the compile/static checks pass.

1. Compile in Unity; Console has no errors.
2. New game: every intended property appears in the correct city; no
   `Failed to find location index` warning for a map property.
3. Buy and build one of each category (city service, map producer, interior);
   verify gold, status, tile, map icon and notification.
4. Advance 1 day: income/upkeep/worker cost match the details panel.
5. Advance 10+ days: efficiency decay and Buff/Infested events occur; Extra
   guards prevents infestation.
6. Manage each property: local and remote (Mansion Office) paths; efficiency
   changes and management skill trains.
7. Assign/unassign a worker; verify efficiency, training and upkeep.
8. Buy every upgrade; verify effect, cost, one-time purchase and sell refund.
9. Use every shortcut and button the property adds; update `readme.txt` for new
   keys.
10. Save, exit to menu, continue; verify property, upgrades, efficiency, worker
    and location.
11. Load a save made before the change; verify existing properties and upgrades
    still work.
12. Check all sprite arrays: property icon, map tile sprite, background sprite.
13. Regression-test House, Mansion, Inn, Farm, Sawmill, Iron/Silver/Gold mine,
    Horses and Office.

Static checks that can be run from the repository root:

```powershell
dotnet build Assembly-CSharp.csproj --nologo -v:q
git diff --check
```

Expected: build succeeds with 0 warnings/0 errors (the project currently does),
and `git diff --check` reports no whitespace errors.

---

## 7. Reference index

### 7.1 Property templates and upgrades

- `Property.properties`: `Property.cs:314-727`
- `Property.Copy`: `Property.cs:240-271`
- `Property.Update`: `Property.cs:273-296`
- `Property.HaveUpgrade`: `Property.cs:216-222`
- `Property.GetImage`: `Property.cs:229-238`
- `Property.Income`/`Upkeep`/`BuildPrice`: `Property.cs:72-120`
- `Property.Desc`: `Property.cs:131`

### 7.2 Lookup and lifecycle

- `Player.HaveProperty`: `Player.cs:38-53`
- `Player.HavePropertyUpgrade`: `Player.cs:55-61`
- `Game.NewGame`: `Game.cs:1609-1652`
- `Game.OnNewDay`: `Game.cs:1455-1596`
- `Game.UpdateButtons`: `Game.cs:1680-1789`
- `Game.GetPropertyInside`: `Game.cs:2475-2484`
- `Game.GetLocationIndex`: `Game.cs:2486-2492`
- `Game.GetProperty`: `Game.cs:2494-2506`
- `Game.GetPropertyHere`: `Game.cs:2618-2627`
- `Game.UpdateWorkers`: `Game.cs:2662-2673`

### 7.3 UI

- `PropertiesScreen`: `GameDialogs/PropertiesScreen.cs`
- `GardenScreen`: `GameDialogs/GardenScreen.cs`
- `StoreItemsScreen`: `GameDialogs/StoreItemsScreen.cs`
- `SkipTimeScreen`: `GameDialogs/SkipTimeScreen.cs`
- `PeopleScreen`: `GameDialogs/PeopleScreen.cs`
- `CraftScreen`: `GameDialogs/CraftScreen.cs`
- `Map`: `GameDialogs/Map.cs`
- `GameUI`: `GameUI.cs`

### 7.4 World and tiles

- `World.Init` / spawns: `World.cs:61-273`
- `World.cityMapping`: `World.cs:147-152`
- `World.sublocations`: `World.cs:154-186`
- `World.FindLocationIndex`: `World.cs:826-842`
- `World.Update`: `World.cs:866-907`
- `TileType`: `TileType.cs`
- `Tile`: `Tile.cs`

---

## 8. Open decisions

These need a product decision before implementing a new property/upgrade:

1. **Is `value` a base price or the real price?** Today `Value` applies a
   `0.9` factor when efficiency is 0. If fixed prices are intended, use
   `efficiency = 50` for non-producers or change the buy path to use `value`.
2. **Should city properties be individually manageable?** If yes, the worker
   location model must change (section 4.4, phase 3).
3. **Should upgrades be data-driven effects or name-based hooks?** Data-driven
   is the only way to add behaviour without editing 3-6 files.
4. **Should a new property be able to define its own tile type/image/background
   from data?** If yes, add the fields and validation described in phase 4.
5. **Should existing saves be rebalanced when templates change?** Today
   `Property.Update` deliberately/accidentally keeps value/income/upkeep. Decide
   whether `RegenerateProperties` should sync those fields.
6. **Should `Office` (property) and `Office` (Mansion upgrade) be renamed?** A
   rename breaks old saves and string lookups; a migration or an alias is
   needed first.
