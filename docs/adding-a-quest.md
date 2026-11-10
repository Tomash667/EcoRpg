# Adding a New Quest or Quest Type

This document describes how the current quest system works, what has to change to
add a new quest (or a new kind of quest), and which parts of the code are still
hard-coded around the six existing `Quest.Type` values.

It is a design/implementation guide, not a record of an implemented feature. No
gameplay code is changed by this document.

Line numbers are from the 2026-11-10 checkout and will drift. Each reference also
names the symbol or the surrounding method so it can be found after a refactor.
The project has no test suite and no CLI gameplay runner; verification is
compiling plus playing in the Unity Editor (see section 6).

See also:

- `docs/adding-a-property.md` - properties, infestations and the property clear
  quests that quest generation depends on;
- `docs/adding-a-hero-class.md` - the `ClassInfo` dictionary pattern proposed here
  for quest type metadata.

---

## 0. At a glance

| Question | Short answer |
| --- | --- |
| Where is quest data? | There is no quest data table. Quests are generated in code by `Game.GenerateQuest` (`Game.cs:1809-1961`). Enemy and item content still comes from `Enemy.enemies` and `Item.items`. |
| Where are quests stored? | `Game.availableQuests` and `Game.activeQuests` (`Game.cs:33`). Both are serialized `List<Quest>`. |
| What is a quest? | A single flat `[Serializable]` class, `Quest` (`Quest.cs:5`), with a nested `Quest.Type` enum (`Quest.cs:7-15`). There are no subclasses, ScriptableObjects or quest assets. |
| How many quests exist at once? | Six at new game (two per difficulty 1-3). The board refills toward three per difficulty, nine total (`Game.cs:2275-2334`). The player can hold `guildRank` non-unique quests (`GuildScreen.cs:54`, `90`). |
| Can a new quest be added with data only? | A new **target** for `Defeat`, `Gather`, `Clear` or `Artifact` mostly can. A new **`Quest.Type`** cannot: its text, reward, similarity, completion and progress hooks are code switches. |
| Where is the quest board? | `GuildScreen` in a city guild. There is one extra hand-written board in village 1 for the spider quest (`Game.cs:1749`, `Game.cs:2406-2429`). |
| Biggest risk | A new `Quest.Type` compiles but silently produces a blank title, zero reward, `IsDone() == true` and no progress because every switch has a `_ =>` default. Nothing makes the compiler or the game complain. |
| Second biggest risk | Quest generation and world state mutate each other. A new location-based type must audit `GenerateQuest`, `World.Update`, `PostCombat`, `RegenerateWorld` and the property screens (section 5). |
| Save compatibility | `Quest.Type` is stored as an int. Append values; never reorder. `Enemy`/`Item` references are persisted by name only for `Defeat`/`Gather` (`Quest.cs:74-88`). |

---

## 1. Current model

### 1.1 Containers and lifecycle

| Member | Location | Role |
| --- | --- | --- |
| `Quest` | `Quest.cs:5` | One quest instance, also the save record. |
| `Quest.Type` | `Quest.cs:7-15` | `Defeat`, `Clear`, `Gather`, `Artifact`, `UniqueDragon`, `UniqueSpider`. |
| `Game.availableQuests` | `Game.cs:33` | Quests offered by the guild, not accepted yet. |
| `Game.activeQuests` | `Game.cs:33` | Accepted quests, including the two story quests. |
| `Game.GenerateQuest` | `Game.cs:1809-1961` | The only generator for repeatable quests. |
| `Game.GenerateInitialQuests` | `Game.cs:2275-2284` | Adds two quests of each difficulty at new game. |
| `Game.UpdateQuests` | `Game.cs:2286-2334` | Ages available quests, removes expired ones and refills the board. |
| `Game.RemoveQuest` | `Game.cs:1963-1973` | Removes an active quest and re-selects a tracked quest. |
| `GuildScreen` | `GameDialogs/GuildScreen.cs` | Board UI: accept, finish and cancel. |
| `Journal` | `GameDialogs/Journal.cs` | Lists active quests and lets the player choose the tracked one. |
| `Map` | `GameDialogs/Map.cs` | Draws a single outline for the tracked quest. |
| `Game.UpdateText` | `Game.cs:1200-1237` | Shows the tracked quest in the HUD (`Game.cs:1215-1217`). |

Lifecycle in order:

1. **New game.** `Game.NewGame` creates one active `UniqueDragon` quest with
   `location = -1`, then initializes empty lists and calls
   `GenerateInitialQuests` (`Game.cs:1620-1648`).
2. **Generation.** `GenerateInitialQuests` adds two quests for each difficulty
   1-3 (`Game.cs:2277-2281`).
3. **Offering.** `GuildScreen.Refresh` splits quests into accepted, available and
   unavailable groups by `difficulty <= guildRank` (`GuildScreen.cs:69-94`).
4. **Accepting.** `GuildScreen.AcceptQuest` moves the quest from
   `availableQuests` to `activeQuests`, auto-tracks it when nothing is tracked,
   and seeds `Clear` progress from `tile.defeatedEnemies`
   (`GuildScreen.cs:121-135`).
5. **Progress.** Only three places mutate `count`:
   - `Game.PostCombat` for `Defeat` and `Clear` (`Game.cs:608-617`);
   - `World.Update` when a cleared tile decays and a `Clear` quest is active
     (`World.cs:883-906`, `World.cs:918-935`);
   - `GuildScreen.AcceptQuest` for the initial `Clear` seed
     (`GuildScreen.cs:126-131`).
   `Gather` has no `count`; its progress is the player's current inventory
   (`Quest.cs:104-110`).
6. **Finishing.** `GuildScreen.FinishQuest` pays `quest.Reward`, applies guild
   progress, calls `quest.Finish()` (which removes `Gather` items) and removes
   the quest (`GuildScreen.cs:137-172`).
7. **Cancelling.** `GuildScreen.CancelQuest` subtracts guild progress, removes
   the quest and may put it back on the board with a five-day timer
   (`GuildScreen.cs:174-209`).
8. **Expiry.** `UpdateQuests` decrements the timer of **available** quests only.
   An expired `Clear` quest on a property tile marks that tile cleared
   (`Game.cs:2289-2307`). Active quests never expire.
9. **Story quests.** `UniqueDragon` and `UniqueSpider` are not generated by
   `GenerateQuest`; they are created in `NewGame` and `Game.ViewQuest` and are
   advanced by `dragonStatus`/`spiderStatus` (`Game.cs:37-38`).

### 1.2 `Quest` fields and save format

```csharp
[Serializable]
public class Quest : ISerializationCallbackReceiver
{
    public enum Type { Defeat, Clear, Gather, Artifact, UniqueDragon, UniqueSpider }

    public Enemy enemy;
    public Item item;
    public Type type;
    public string enemyName, itemName;
    public float difficultyMod;
    public int location, count, max, locationDifficulty, difficulty, timer;
    public bool tracked;
}
```

| Field | Used by | Meaning and caveats |
| --- | --- | --- |
| `enemy` | `Defeat` | Target enemy. Not `[Serializable]`; restored from `enemyName` on load. |
| `item` | `Gather` | Target item. Not `[Serializable]`; restored from `itemName` on load. |
| `enemyName` / `itemName` | save only | Shadow strings written in `OnBeforeSerialize` and read in `OnAfterDeserialize` (`Quest.cs:74-88`). |
| `type` | all | Serialized as an enum number. Append only. |
| `difficultyMod` | all | Guild progress contribution, not a reward multiplier. Values today: `0.1-0.5` for `Defeat`, `0.25` for `Gather`, `0.5` or `1` for `Clear`/`Artifact` (`Game.cs:1830-1944`). |
| `location` | `Clear`, `Artifact`, `Unique*` | World index (`World.CalculateIndex` / `World.IndexToPoint`), `-1` for "anywhere" or "not found yet". |
| `count` | `Defeat`, `Clear`, `Artifact`, `UniqueSpider` | Current progress. `Gather` ignores it. |
| `max` | all except `UniqueSpider` | Target count. `IsDone` uses `count >= max` for every non-`Gather` type, so `max = 0` is instantly complete. |
| `locationDifficulty` | reward only | A second "difficulty" number. Today it is derived from `infestedCost / 250` for property quests, `1-8` for caves/dungeons, or `difficulty` for `Defeat`. |
| `difficulty` | generation, guild gate, reward | The 1-3 tier. `GuildScreen` only lets the player pick a quest with `difficulty <= guildRank` (`GuildScreen.cs:69-94`). |
| `timer` | available quests | Days left on the board. Reused as a five-day re-offer delay after cancelling (`GuildScreen.cs:203`). Active quests never tick it. |
| `tracked` | UI | Exactly one quest is normally tracked. `tracked` is serialized. |

There are no runtime-only (`[NonSerialized]`) quest fields. Every field above is
part of the save. `Quest` does not implement equality, an id, a giver, a chain,
requirements, a reward override or a description.

### 1.3 Existing types

| Type | How it is generated | Target | Progress source | Completion | Base reward |
| --- | --- | --- | --- | --- | --- |
| `Defeat` | `Game.cs:1870-1876`, `1935-1944` | `enemy` | `PostCombat` counts enemies with that name | `count >= max` | `25 * (enemy.level + locationDifficulty) * max` |
| `Clear` | `Game.cs:1818-1841`, `1885-1904` | `location` | `PostCombat` counts every enemy at that location; `World.Update` can decay it | `count >= max` | `locationDifficulty * 250` |
| `Gather` | `Game.cs:1842-1850`, `1914-1932` | `item` | Current inventory (`Global.Player.CountItem`) | `CountItem(item) >= max` | `locationDifficulty * 250` |
| `Artifact` | `Game.cs:1851-1859`, `1905-1913` | `location` | `Game.Explore` sets `count = 1` when the treasure room is found (`Game.cs:272-283`) | `count >= 1` | `locationDifficulty * 500` |
| `UniqueDragon` | `Game.NewGame` (`Game.cs:1620-1627`) | `location` set on first entry to the boss cave (`Game.cs:1115-1116`) | Story status `dragonStatus` | Removed on dragon kill (`Game.cs:669-671`) | 0; reward is story progression/portal access |
| `UniqueSpider` | `Game.ViewQuest` (`Game.cs:2406-2429`) | Spider queen cave, then village 1 | Story status `spiderStatus`; `count` set to 1 on kill (`Game.cs:687-690`) | Removed on returning to the village (`Game.cs:1095-1104`) | 0; reward is the Inn in city 1 |

`Quest.IsUnique` only recognises the last two (`Quest.cs:25`). `GuildScreen`
filters on it when counting accepted quests and when listing the active quests
(`GuildScreen.cs:54-67`).

### 1.4 Generation and refill

`Game.GenerateQuest(int difficulty)` is a single `while (true)` loop containing a
`switch (Utility.Rand % 8)` per difficulty band. The comments in the code are the
only record of the weights:

**Difficulty 1** (`Game.cs:1814-1877`)

| Case | Weight | Type | Notes |
| --- | ---: | --- | --- |
| 0 | 12.5 % | `Clear` | A for-sale property or the city sewers. |
| 1 | 12.5 % | `Gather` | `herb`, `mana herb` or `magic crystal`; `max = 20`. |
| 2, 3 | 25 % | `Artifact` | Random dungeon of the same difficulty. |
| 4 | 12.5 % | `Clear` | Non-mine, non-boss cave of difficulty 1; `max = 10`. |
| 5, 6, 7 | 37.5 % | `Defeat` | Random enemy of the same difficulty; `max = 2-3`. |

**Difficulty 2 and 3** (`Game.cs:1879-1946`)

| Case | Weight | Type | Notes |
| --- | ---: | --- | --- |
| 0, 1 | 25 % | `Clear` | For-sale property or a non-mine, non-boss cave. |
| 2, 3 | 25 % | `Artifact` | Random dungeon of the same difficulty. |
| 4 | 12.5 % | `Gather` | `rare herb` (20) or `magic crystal` (50); difficulty 3 is redirected. |
| 5, 6, 7 | 37.5 % | `Defeat` | Random enemy of the same difficulty; `max = 2` to `2 + difficulty`. |

The `while (true)` loop only returns a quest when `IsSimilar` is false against
every available and active quest (`Game.cs:1948-1959`). For `Artifact` and
`Clear` it also resets the target tile before returning:

```csharp
if (quest.type == Quest.Type.Artifact || quest.type == Quest.Type.Clear)
{
    Tile tile = world.GetLocation(quest.location);
    tile.defeatedEnemies = 0;
    tile.timer = 0;
    tile.foundTreasure = false;
    tile.clear = false;
}
```

That is a side effect on the world, not just on the quest. It can re-open a
cleared tile or erase a treasure flag (section 5.3).

The board refill is equally hard-coded (`Game.cs:2309-2334`): if
`availableQuests.Count < 9`, the game computes three minus the current count per
difficulty and adds a quest for each missing slot only with a `Utility.Rand % 3 == 0`
chance. The board can therefore stay below nine for many days.

### 1.5 Acceptance, tracking and completion

- **Availability.** A quest is pickable only when
  `quest.difficulty <= player.guildRank`, `acceptedQuestCount < guildRank`, and
  it is in the "available" (not "unavailable") section of the list
  (`GuildScreen.cs:69-94`). `acceptedQuestCount` excludes unique quests
  (`GuildScreen.cs:54`).
- **Accepting.** `AcceptQuest` moves the quest to `activeQuests`, makes it
  tracked if nothing else is, and for `Clear` copies the tile's current
  `defeatedEnemies` into `count` (`GuildScreen.cs:121-135`).
- **Tracking.** `Journal` lets the player toggle one quest (`Journal.cs:43-68`).
  `RemoveQuest` re-tracks the first remaining quest that is not a
  `UniqueDragon` (`Game.cs:1963-1973`).
- **Completion.** `GuildScreen` shows a "Finish" button only when
  `quest.IsDone()` is true (`GuildScreen.cs:61-64`). `IsDone` is a type switch
  (`Quest.cs:104-110`), so a new type without a case uses
  `count >= max` by default.
- **Cancelling.** Any active non-unique quest can be cancelled. Cancelling
  subtracts `difficultyMod` from guild progress and can demote the player
  (`GuildScreen.cs:174-188`). It then re-adds the quest to the board if
  `canBeCompleted` is still true. Only `Artifact` and `Clear` get a real check;
  every other type defaults to `true` (`GuildScreen.cs:189-206`).

### 1.6 Rewards and guild progression

```csharp
public int Reward => type switch
{
    Type.Defeat => 25 * (enemy.level + locationDifficulty) * max,
    Type.Gather => locationDifficulty * 250,
    Type.Clear => locationDifficulty * 250,
    Type.Artifact => locationDifficulty * 500,
    _ => 0,
};
```

There is no per-quest reward field. `difficultyMod` does **not** scale the gold
reward; it only feeds `player.guildProgress` in `GuildScreen.FinishQuest`
(`GuildScreen.cs:142-163`):

- if `quest.difficulty + 1 == player.guildRank`, the contribution is divided by
  four;
- if `quest.difficulty < player.guildRank`, the contribution is zero;
- promotion happens when `player.guildProgress >= 1 + player.guildRank`;
- cancelling subtracts the same value and demotes when progress drops below
  `-player.guildRank`.

`UniqueDragon` and `UniqueSpider` have `Reward == 0` and `TextReward` hides the
gold suffix for them (`Quest.cs:71-72`). Their real rewards are delivered outside
the quest system: the spider hands over the Inn (`Game.cs:1095-1104`) and the
dragon unlocks the dark-dimension portal through `dragonStatus`
(`Game.cs:2219-2226`).

### 1.7 Unique story quests

The two unique quests are not a general "story quest" system; they are two
specific enum values plus two specific fields on `Game`:

| Story | Status field | Values | Quest creation | Completion hook | Reward hook |
| --- | --- | --- | --- | --- | --- |
| Dragon | `Game.dragonStatus` (`Game.cs:37`) | `None`, `Found`, `Defeated`, `Win` | `NewGame` (`Game.cs:1620-1627`) | `PostCombat` on `"dragon"` (`Game.cs:664-677`) | `OnEnterLocation` city + `EnterPortal` (`Game.cs:1089-1094`, `2219-2226`) |
| Spider | `Game.spiderStatus` (`Game.cs:38`) | `None`, `Accepted`, `Defeated`, `Rewarded`, `Skipped` | `Game.ViewQuest` (`Game.cs:2406-2429`) | `PostCombat` on `"spider queen"` (`Game.cs:680-693`) | `OnEnterLocation` village 1 (`Game.cs:1095-1104`) |

`BtQuest` is visible only in village 1 while `spiderStatus == None`
(`Game.cs:1749`). The dragon quest starts active and untracked with
`location = -1`; entering the difficulty-3 boss cave sets its location
(`Game.cs:1112-1116`).

### 1.8 UI surfaces

| Surface | What it reads | Limits |
| --- | --- | --- |
| `GuildScreen.Refresh` | `activeQuests`, `availableQuests`, `player.guildRank`, infested properties | Fixed `ItemEntry` rows; a row supports one or two buttons. |
| `Journal.Refresh` | all `activeQuests`, one `tracked` | No description, giver, deadline or reward breakdown. |
| `Game.UpdateText` | `activeQuests.FirstOrDefault(x => x.tracked)` | One line of text in the HUD. |
| `Map.Show` / `Map.UpdateMap` | tracked quest with `location != -1` and a discovered tile | One outline; no multi-location quests. |
| `Game.UpdateButtons` | `BtQuest` condition | Hard-coded to village 1 / spider status. |

---

## 2. Adding a quest without a new type

This is the cheapest change: reuse `Defeat`, `Clear`, `Gather` or `Artifact` and
add a target, or add one hand-authored `Quest` instance. No save format change
and no new UI.

### 2.1 New `Defeat` target (enemy content only)

`GenerateQuest` calls `Enemy.GetRandom(difficulty)`, which selects any enemy with
a matching `Enemy.difficulty` (`Enemy.cs:68-71`). Adding an enemy to
`Enemy.enemies` with `difficulty = 1`, `2` or `3` therefore makes it reachable as
a `Defeat` quest automatically.

Required:

1. Add the enemy entry to `Enemy.enemies` (`Enemy.cs:73` onwards).
2. Give it a portrait at `Assets/Resources/Portraits/{name}.jpg`; a missing file
   logs an error when the quest or combat uses it.
3. Give it `locations`, `level`, `hp`, `attack`, `def`, `dex`, `gold` and the
   other combat fields used by the existing entries.
4. Decide whether the existing `25 * (enemy.level + locationDifficulty) * max`
   reward is acceptable. There is no per-enemy reward override.

No `Quest.cs` or `Game.cs` change is required. The quest text uses
`Utility.Plural(enemy.name)` and the reward uses `enemy.level`, so both work for
new names unless the plural is irregular; add it to `Utility.plurals`
(`Utility.cs:21-30`) if needed.

### 2.2 New `Gather` target (item content plus one generator case)

`Gather` is not "collect after accepting"; `IsDone` reads the player's current
inventory, so items collected before accepting count immediately
(`Quest.cs:104-110`). `Finish` removes `max` items (`Quest.cs:112-116`).

Required:

1. Add the item to `Item.items` (`Item.cs`), with a stable `name` because
   `itemName` is the save key.
2. Add it to `Item.cityItems` / `Item.villageItems` if it should be buyable.
3. Add it to a `GenerateQuest` case, or it will never be requested. There is no
   item flag that marks an item as quest material.

Example for difficulty 1 (`Game.cs:1842-1850`):

```csharp
case 1:
    quest.type = Quest.Type.Gather;
    quest.item = Item.Get(Utility.Random("herb", "mana herb", "magic crystal", "rare herb"));
    quest.max = 20;
    quest.location = -1;
    quest.locationDifficulty = 1;
    quest.difficultyMod = 0.25f;
    break;
```

Caveats:

- The reward is `locationDifficulty * 250` regardless of item value. A quest for
  an expensive item can pay less than selling it.
- `Finish` calls `Global.Player.RemoveItem(item, max)`, which removes the first
  matching stack, including a `team` stack (`Hero.cs:314-329`). If the item can
  be `Subtype.Treasure` or otherwise team-owned, decide whether that is allowed.
- `OnBeforeSerialize`/`OnAfterDeserialize` only handle `Gather` and `Defeat`
  (`Quest.cs:74-88`). A new reference type needs its own name shadow.

### 2.3 New `Clear` / `Artifact` target

`Clear` and `Artifact` are location-based. The target must be a tile index that
`World.GetLocation` can resolve. Today the generator uses:

- `Clear`: a for-sale property's `locationIndex`, the city sewers, or a
  non-mine/non-boss cave (`Game.cs:1823-1866`, `1885-1904`);
- `Artifact`: a dungeon (`TileType.Dungeon` or a hidden dungeon) of the same
  difficulty (`Game.cs:1855`, `1909`).

To add a new location:

1. Make sure the tile exists at new-game time (`World.Init`, `World.cs:61-273`)
   or is created by a property build. Quest generation runs after `world.Init`
   in `NewGame` (`Game.cs:1615-1648`).
2. Extend the predicate in `GenerateQuest`. If the predicate matches nothing,
   `FindRandomLocationIndex` returns `-1` and the generator will happily return a
   quest with `location == -1`; `Quest.Text` then calls `world.GetLocation(-1)`
   and throws. Guard the result (section 5.3).
3. Keep `max` in sync with how the tile is cleared.
   `TileType.GetEnemiesCount` is 20 for forest, mountains and enchanted forest,
   and 10 for everything else (`TileType.cs:110-117`), while `Clear` quests are
   created with `max = 10` (`Game.cs:1822`, `1866`, `1889`). A new forest or
   mountain `Clear` quest will complete at 10 kills while the tile is still
   infested. Set `max = tile.type.GetEnemiesCount()`.
4. Check `PostCombat` only increments `tile.defeatedEnemies` when
   `world.level == tile.foundLevel` (`Game.cs:734-735`). A multi-level dungeon
   `Clear` quest needs a different progress source or the tile will never clear.
5. `World.Update` decays the quest count when the tile timer expires
   (`World.cs:883-906`, `918-935`). Decide whether that is desired.

### 2.4 One hand-authored quest instance

If the goal is a single scripted offer rather than a whole new type, add a
`Quest` object directly. This is the only "data-only" way to create a quest
because there are no quest assets.

For an active quest (for example, a one-off delivery to the player's journal):

```csharp
activeQuests.Add(new Quest
{
    type = Quest.Type.Gather,
    item = Item.Get("magic crystal"),
    itemName = "magic crystal",       // keep the save shadow in sync
    max = 10,
    location = -1,
    locationDifficulty = 1,
    difficulty = 1,
    difficultyMod = 0.5f,
    timer = 10,
    tracked = !activeQuests.Any(x => x.tracked)
});
```

For an offer on the board:

```csharp
availableQuests.Add(new Quest
{
    type = Quest.Type.Defeat,
    enemy = Enemy.Get("bandit"),
    enemyName = "bandit",             // keep the save shadow in sync
    max = 3,
    location = -1,
    locationDifficulty = 1,
    difficulty = 1,
    difficultyMod = 0.4f,
    timer = 15
});
SortQuests();
```

Rules:

- Add after `NewGame` has initialized `availableQuests`/`activeQuests`
  (`Game.cs:1620-1632`); adding earlier throws a null reference.
- `enemy`/`item` are not serialized; set `enemyName`/`itemName` too, or the
  reference is `null` after load. `OnBeforeSerialize` also writes the shadows on
  save, but setting them at creation avoids a null window.
- There is no custom title, description or reward. The quest displays the generic
  `Title`/`Text` for its type and pays the formula reward. If the quest needs a
  unique name, a story description or a fixed reward, you need a new type (or
  new fields on `Quest`).
- `SortQuests()` is required after adding an offer, otherwise the
  "unavailable" grouping in `GuildScreen` can hide a low-difficulty quest that
  appears after a higher one.

### 2.5 Checklist for existing-type content

1. Compile; Console has no errors.
2. New game: the quest appears on the board at the intended difficulty, or the
   active quest appears in the Journal.
3. Accept it: it moves to active quests and becomes tracked if nothing else is.
4. Progress it: kill the right enemy / gather the item / clear the location /
   find the treasure.
5. Finish it: reward, guild progress, item removal (`Gather`) and
   `RemoveQuest` tracking behaviour are correct.
6. Cancel it: guild progress penalty and re-offer/removal behaviour are correct.
7. Let an offer expire: the board removes it and `Clear` property state is
   correct.
8. Save, exit, continue: type, enemy/item, progress, timer and tracking persist.
9. Load an old save: existing quests are unchanged.

---

## 3. Adding a new `Quest.Type`

Use this when the quest needs a new progress source, a new target shape, a new
text/reward rule or a story identity that `Defeat`/`Clear`/`Gather`/`Artifact`
cannot express. The example below is `Hunt`: kill N of a specific enemy in a
specific region. It is deliberately similar to `Defeat`, so the diff is easy to
audit.

### 3.1 Choose the progress source first

| Progress source | Existing example | Hook to reuse or add |
| --- | --- | --- |
| Enemy killed | `Defeat` | `Game.PostCombat` (`Game.cs:608-617`). |
| Enemies killed at a location | `Clear` | `Game.PostCombat` plus `World.Update` decay. |
| Item in inventory | `Gather` | `Quest.IsDone` / `Quest.Finish`. |
| Tile state (`clear`, `foundTreasure`) | `Clear` / `Artifact` | `Game.Explore`, `Game.PostCombat`, `World.Update`. |
| Story flag | `UniqueDragon`, `UniqueSpider` | `Game` status field plus `PostCombat`/`OnEnterLocation`. |
| Time / travel / day | none | New hook: `Game.OnNewDay`, `World.Update` or `Game.Travel`. |
| Property owned / event | none (paid clear is separate) | `Game.OnNewDay` or `PropertiesScreen` purchase path. |
| Skill / level / gold | none | New hook at the point the value changes. |

Write down the source before touching the enum. If the source is "story flag", use
section 3.10 instead of only adding a generated type.

### 3.2 Append the enum value

```csharp
public enum Type
{
    Defeat,        // 0
    Clear,         // 1
    Gather,        // 2
    Artifact,      // 3
    UniqueDragon,  // 4
    UniqueSpider,  // 5
    Hunt           // 6 - append, never insert
}
```

Existing saves store the numeric value. Inserting a new value before an old one
silently changes every old quest's type. Append only. Renaming an enum member is
safe for the save (the number is unchanged) but breaks source references.

`Quest.Type` does **not** follow the repository's enum convention. `AGENTS.md`
asks every gameplay enum to have a `Class`/`ClassMethods`-style extension class
with an `all` array, `AsString()` and `GetRandom()`. `Quest.Type` has none. A new
type should either add `QuestTypeMethods` (recommended) or deliberately document
why not.

### 3.3 Update every `Quest` switch

These members use `type switch` or `if (type == ...)`:

| Member | What happens for an unknown type | Required change for `Hunt` |
| --- | --- | --- |
| `IsUnique` (`Quest.cs:25`) | false | Leave false for a generated hunt, or add it for a story hunt. |
| `Reward` (`Quest.cs:26-39`) | `0` | Add a reward formula. |
| `Text` (`Quest.cs:40-55`) | `string.Empty` | Add the progress line. |
| `Title` (`Quest.cs:56-70`) | `string.Empty` | Add the board/journal title. |
| `OnBeforeSerialize` (`Quest.cs:74-80`) | no shadow written | Add the shadow for any reference field. |
| `OnAfterDeserialize` (`Quest.cs:82-88`) | no reference restored | Add the restore for any reference field. |
| `IsSimilar` (`Quest.cs:90-102`) | `false` | Define what makes two hunts duplicates. |
| `IsDone` (`Quest.cs:104-110`) | `count >= max` | Fine for `Hunt`; change for item/delivery types. |
| `Finish` (`Quest.cs:112-116`) | no side effect | Add item removal/turn-in if needed. |
| `GetLocationName` (`Quest.cs:118-124`) | not called for `Hunt` | Only relevant for location types. |

A complete `Hunt` sketch:

```csharp
public TileType huntTileType;

public bool IsUnique => type == Type.UniqueDragon || type == Type.UniqueSpider;

public int Reward
{
    get
    {
        return type switch
        {
            Type.Defeat => 25 * (enemy.level + locationDifficulty) * max,
            Type.Gather => locationDifficulty * 250,
            Type.Clear => locationDifficulty * 250,
            Type.Artifact => locationDifficulty * 500,
            Type.Hunt => 30 * (enemy.level + locationDifficulty) * max,
            _ => 0,
        };
    }
}

public string Text
{
    get
    {
        return type switch
        {
            Type.Defeat => $"Defeat {count}/{max} {Utility.Plural(enemy.name)}",
            Type.Clear => $"Clear {GetLocationName()} - {Mathf.Min(100 * count / max, 100)}%",
            Type.Gather => $"Gather {Global.Player.CountItem(item)}/{max} {Utility.Plural(item.name)}",
            Type.Artifact => count == 0 ? $"Find artifact in {GetLocationName()}" : $"Bring artifact from {GetLocationName()} to guild",
            Type.Hunt => $"Hunt {count}/{max} {Utility.Plural(enemy.name)} in the {huntTileType.AsString()}",
            Type.UniqueDragon => location == -1 ? "Defeat dragon in far cave" : $"Defeat dragon in {GetLocationName()}",
            Type.UniqueSpider => count == 0 ? $"Defeat spider queen in {GetLocationName()}" : $"Return to {GetLocationName()} and get your reward",
            _ => string.Empty
        };
    }
}

public string Title
{
    get
    {
        return type switch
        {
            Type.Defeat => $"Defeat {Utility.Plural(enemy.name, max)}",
            Type.Clear => $"Clear {GetLocationName()}",
            Type.Gather => $"Gather {Utility.Plural(item.name, max)}",
            Type.Artifact => $"Find artifact in {GetLocationName()}",
            Type.Hunt => $"Hunt {Utility.Plural(enemy.name, max)} in the {huntTileType.AsString()}",
            Type.UniqueDragon or Type.UniqueSpider => Text,
            _ => string.Empty
        };
    }
}

public void OnBeforeSerialize()
{
    if (type == Type.Defeat || type == Type.Hunt)
        enemyName = enemy.name;
    else if (type == Type.Gather)
        itemName = item.name;
}

public void OnAfterDeserialize()
{
    if (type == Type.Defeat || type == Type.Hunt)
        enemy = Enemy.Get(enemyName);
    else if (type == Type.Gather)
        item = Item.Get(itemName);
}

public bool IsSimilar(Quest quest)
{
    if (type != quest.type)
        return false;

    return type switch
    {
        Type.Defeat => enemy == quest.enemy,
        Type.Clear or Type.Artifact => location == quest.location,
        Type.Gather => item == quest.item,
        Type.Hunt => enemy == quest.enemy && huntTileType == quest.huntTileType,
        _ => false
    };
}
```

Notes:

- `IsDone`, `Finish` and `GetLocationName` do not need changes for `Hunt`.
- `Text` for `Hunt` uses `huntTileType.AsString()`; `TileTypeMethods.AsString`
  exists for every current tile type.
- `OnAfterDeserialize` uses `Enemy.Get`, which throws if `enemyName` is empty or
  unknown (`Enemy.cs:46-49`). Use `Enemy.TryGet` if the new type can exist
  without an enemy.
- If the new type needs a second reference (for example a `Property`), add a
  shadow string and restore it too. `Property` is `[Serializable]`, but quest
  identity by object reference is fragile across world regeneration.
- If the new type has text that depends on another entity, keep the lookup
  null-safe. `Quest.Text` is evaluated by `GuildScreen.Refresh`, `Journal` and
  `UpdateText`; a throw there breaks the whole screen.

### 3.4 Decide the fields and their defaults

Add only fields that every quest of that type needs. New `Quest` fields are
save-compatible because `JsonUtility` fills missing fields with `0`, `false` or
`null`, but there is no migration step:

- A new enum field (`TileType huntTileType`) deserializes to `0`
  (`TileType.Plains`) in old saves. That is acceptable only if old saves never
  contain the new type.
- A new reference field deserializes to `null`. Guard it or restore it from a
  name shadow.
- A new `int max`-like field deserializes to `0`; remember `IsDone` treats
  `count >= 0` as done, so set `max` explicitly when creating the quest.
- Do not reuse `locationDifficulty` for a new meaning. It already means
  "reward tier" and is derived from `infestedCost / 250`, cave difficulty or
  enemy difficulty depending on the type.
- Do not reuse `timer` for a deadline on active quests unless you also implement
  active expiry; today it only ages available offers.

### 3.5 Generate it

Add the type to both difficulty bands. The existing weights are encoded by the
switch modulus and comments, so decide whether to replace a slice or widen the
modulus.

Example: widen difficulty 1 from `% 8` to `% 10`, give `Hunt` two slices and
reduce `Defeat` from three slices to three out of ten:

```csharp
switch (Utility.Rand % 10)
{
case 0:
    // 10% Clear property or sewers
    quest.type = Quest.Type.Clear;
    quest.locationDifficulty = Utility.Random(1, 3);
    quest.max = 10;
    // ... existing property/sewers selection ...
    break;
case 1:
    // 10% Gather
    quest.type = Quest.Type.Gather;
    quest.item = Item.Get(Utility.Random("herb", "mana herb", "magic crystal"));
    quest.max = 20;
    quest.location = -1;
    quest.locationDifficulty = 1;
    quest.difficultyMod = 0.25f;
    break;
case 2:
case 3:
    // 20% Artifact
    // ... existing dungeon selection ...
    break;
case 4:
    // 10% Clear cave
    // ... existing cave selection ...
    break;
case 5:
case 6:
    // 20% Hunt
    quest.type = Quest.Type.Hunt;
    quest.huntTileType = Utility.Random(TileType.Forest, TileType.Mountains, TileType.Swamp);
    quest.enemy = Enemy.GetRandom(quest.huntTileType, difficulty);
    if (quest.enemy == null)
        continue; // retry the whole while loop
    quest.max = Utility.Random(3, 5);
    quest.location = -1;
    quest.locationDifficulty = difficulty;
    quest.difficultyMod = 0.4f;
    break;
default:
    // 30% Defeat
    // ... existing defeat selection ...
    break;
}
```

Then do the same in the difficulty 2/3 branch (`Game.cs:1879-1946`). Keep the
comments in sync with the modulus; they are the only documentation of the
distribution.

Do **not** copy the `goto case default` shortcut from the difficulty 3 `Gather`
case (`Game.cs:1916-1917`). It compiles, but `default` is the default literal
and therefore `0`, so it jumps to `case 0`, not to the `default` label. Use
`goto default;` or restructure the branch (section 5.3).

Generation checklist for the new type:

1. Set `quest.difficulty` (it comes from the method argument).
2. Set `quest.type`.
3. Set the target: `enemy`/`item`/`location`/new field.
4. Set `max`.
5. Set `locationDifficulty` and `difficultyMod`.
6. Set `timer` (the generator already does `timer = Random(5, 20)` at the top).
7. Decide whether `IsSimilar` prevents duplicates.
8. If the type is location-based, decide whether generation should reset the
   target tile. Do not copy the existing reset blindly (section 5.3).
9. Guard `FindRandomLocationIndex(...) == -1` and `Enemy.GetRandom(...) == null`.

### 3.6 Hook progress

For `Hunt`, add a branch to `PostCombat` next to `Defeat` and `Clear`:

```csharp
foreach (Quest quest in activeQuests)
{
    if (quest.type == Quest.Type.Defeat)
        quest.count += enemyList.Count(x => x == quest.enemy);
    else if (quest.type == Quest.Type.Clear)
    {
        if (quest.location == world.CurrentLocationIndex)
            quest.count += enemyList.Count;
    }
    else if (quest.type == Quest.Type.Hunt)
    {
        if (quest.huntTileType == tile.type)
            quest.count += enemyList.Count(x => x == quest.enemy);
    }
}
```

`tile` is `world.CurrentTile` (`Game.cs:601`). For hidden locations `tile.type` is
the surface type (for example `Mountains`) and `tile.hidden` is the visible type
(`Cave`). `world.RealLocation` is the overworld tile type. Decide which one
"region" means and be consistent in generation, text and progress.

If the quest should only count kills while it is active, the loop above is
already correct: it only walks `activeQuests`. If it should count kills before
accepting (like `Clear` does), seed `count` in `GuildScreen.AcceptQuest`.

If the progress source is not combat, add the hook where the value changes:

| Source | Where to hook |
| --- | --- |
| Item picked up | `Hero.AddItem` or `Game.PostCombat`/`Game.Explore`/`Game.Forage`/`Game.DoWork` |
| Item crafted | `Game.Craft` / `CraftScreen` |
| Day passed | `Game.OnNewDay` |
| Travel arrival | `Game.OnEnterLocation` / `Game.OnChangeLocation` |
| Property bought/built | `PropertiesScreen` buy/build path |
| Location cleared | `Game.PostCombat` already sets `tile.clear` |
| Story flag | `PostCombat` / `OnEnterLocation` next to the dragon/spider blocks |

Prefer a single helper such as `Game.ProgressQuests(...)` over adding more
branches to every call site. There is no such helper today.

### 3.7 Accept, finish and cancel

`GuildScreen` is mostly type-agnostic, but check these:

- **Accepted cap.** `acceptedQuestCount` excludes `IsUnique` only
  (`GuildScreen.cs:54`). A new generated type counts toward `guildRank`.
- **Accept seeding.** `AcceptQuest` seeds only `Clear` (`GuildScreen.cs:126-131`).
  Add a branch if the new type needs pre-existing progress.
- **Finish.** `FinishQuest` pays the formula reward and calls `quest.Finish()`.
  If the new type needs a turn-in or a story reward, add it to `Quest.Finish`
  or to `FinishQuest` next to the spider Inn transfer.
- **Cancel.** `CancelQuest` re-adds the quest when `canBeCompleted` is true. The
  default for an unknown type is true (`GuildScreen.cs:190-206`). Add a branch
  for a type that should not be re-offered after cancellation.
- **Tracking.** `RemoveQuest` auto-tracks the first remaining quest that is not
  `UniqueDragon` (`Game.cs:1963-1973`). A new story type that should not be
  auto-tracked needs an exclusion here.

### 3.8 UI implications

For a generated type, the existing board and Journal rows work as long as
`Title`, `Text` and `TextReward` are implemented:

- `GuildScreen` uses `quest.TitleReward` for offers and `quest.TextReward` for
  accepted quests (`GuildScreen.cs:62`, `91`).
- `Journal` uses `quest.TextReward` and a track toggle (`Journal.cs:43-68`).
- `Game.UpdateText` uses `quest.Text` for the HUD (`Game.cs:1215-1217`).
- `Map.Show` only outlines the tracked quest when `location != -1` and the tile
  is discovered (`Map.cs:74-81`). `Map.UpdateMap` only handles the single
  tracked quest (`Map.cs:305-310`).

New fields that need to be visible (a deadline, a giver, a region, a chain step)
have no home in the current UI. Adding them means changing `ItemEntry` rows,
`Journal`, or the HUD. There is no detail pane for a quest.

### 3.9 Expiry and tracking decisions

- Available quests use `timer`; active quests do not. A timed quest needs a new
  active-expiry pass in `Game.OnNewDay`/`UpdateQuests` and a visible timer.
- The board refill is `availableQuests.Count < 9` and a one-in-three chance per
  missing slot (`Game.cs:2309-2334`). A new type that is rare in the switch can
  take a long time to appear.
- Tracking is single-slot. A new type with multiple locations or multiple
  objectives cannot be represented on the map without changing `Map`.
- Expired `Clear` offers force `tile.clear = true` on property tiles
  (`Game.cs:2294-2302`). Verify this before adding another location-based type;
  it currently gives away property clearing when an offer expires.

### 3.10 Unique/story quest variant

A unique quest that is not "kill a specific boss and return" needs more than a
new enum value:

1. **Status field.** Add a new enum and field to `Game`, next to
   `DragonStatus`/`SpiderStatus` (`Game.cs:12-27`, `Game.cs:37-38`). Append
   values; do not reorder an existing status enum because it is serialized.
2. **Start.** Create the quest in `Game.NewGame` (`Game.cs:1620-1627`), or in a
   new `ViewQuest`-like method. Set `tracked` only when nothing else is tracked.
3. **Giver/UI.** There is no generic giver. The spider uses `BtQuest`
   (`Game.cs:1749`) and `Game.ViewQuest` (`Game.cs:2406-2429`). A new story
   quest needs its own button condition, dialog text and screen wiring (or a
   generic quest-giver screen).
4. **Progress.** Add a `UniqueX` branch to `PostCombat`, `OnEnterLocation`,
   `OnNewDay` or wherever the story advances. Use `FirstOrDefault` and a null
   guard; the existing `First(...)` calls for the spider can throw if the quest
   and status disagree (`Game.cs:687`, `1098`; `PropertiesScreen.cs:182`).
5. **Completion.** `Quest.IsUnique` must include the new type
   (`Quest.cs:25`) so it is excluded from the guild cap and the normal
   finish/cancel flow. If the quest should still be finishable at the guild,
   decide explicitly; `GuildScreen` filters unique quests out of the accepted
   list (`GuildScreen.cs:54-67`).
6. **Reward.** Unique quests pay `0` through `Reward` and hide the gold suffix
   through `TextReward` (`Quest.cs:71-72`). Deliver the real reward in the
   completion hook (property, item, gold, portal unlock) or add a reward field.
7. **Cleanup.** Add the type to `RegenerateWorld`'s quest removal
   (`Game.cs:2496-2498`) and to `RefreshQuests` if it is location-based
   (`Game.cs:2509-2524`). Otherwise a regenerated world can leave a quest
   pointing at a stale tile.
8. **Save.** The `Game` status field, the quest list and `tracked` are all
   serialized. Load an old save (status defaults to `0`), and load a save made
   mid-quest, before trusting the flow.

### 3.11 Documentation and balance

- `readme.txt` documents player-facing shortcuts and mechanics. Add a new key or
  a new board location there if the quest adds one.
- `stats.txt` holds enemy stats per level and location difficulty progression.
  If the new type uses a new reward formula or region, add the data there.
- `todo.txt` already lists quest plans such as "defeat x enemies in region",
  "rescue", "bandits captured caravan", "timed" and "cancel quest confirm"
  (`todo.txt:25-28`, `147-155`, `253`). Keep implemented and planned features
  separate so the docs do not claim unimplemented behaviour.

### 3.12 Checklist for a new type

1. Append the enum value; do not insert or reorder.
2. Update `Reward`, `Text`, `Title`, `IsSimilar`, `IsDone` and `Finish`.
3. Update `OnBeforeSerialize`/`OnAfterDeserialize` for every reference field.
4. Add fields with sensible defaults and guards.
5. Generate it in both difficulty bands and update the weight comments.
6. Guard `-1` locations and `null` enemies/items.
7. Add the progress hook(s).
8. Check `GuildScreen` accept/finish/cancel/tracking behaviour.
9. Decide active expiry and tracking/map behaviour.
10. Save/load, old save, `RegenerateWorld`, and the whole section 6 checklist.

---

## 4. Save/load compatibility

`Game.SaveGame` writes `JsonUtility.ToJson(this)` and `LoadGame` uses
`JsonUtility.FromJsonOverwrite` (`Game.cs:1654-1672`). There is no versioning or
migration. `Quest` is a `[Serializable]` class inside the `Game` object, so every
public field is part of the save.

Rules for a new quest/type:

1. **Append `Quest.Type` values.** The enum is stored as an int. Inserting
   `Hunt` before `Artifact` would turn old artifact quests into hunts.
2. **Do not rename `Quest`, its fields, or the `Game` list fields.**
   `availableQuests`, `activeQuests`, `dragonStatus` and `spiderStatus` are part
   of the save.
3. **Do not rename an existing enemy or item name.** `Defeat`/`Gather` persist
   `enemyName`/`itemName` and restore with `Enemy.Get`/`Item.Get`, which throw on
   an unknown name (`Enemy.cs:46-49`, `Item.Get`).
4. **New fields default to `0`, `false` or `null`.** There is no initializer run
   on old saves. Guard new references and set `max`/`type` explicitly when
   creating a quest.
5. **New reference types need a name shadow.** `Enemy` and `Item` are not
   `[Serializable]`; that is why `Quest` has `enemyName`/`itemName`. A new
   `Property`, `Worker` or other reference will be `null` after load unless it
   is also stored by a stable string/index and restored.
6. **Location indices are world indices.** `RegenerateWorld` reinitializes the
   map and removes only `Artifact`/`Clear` quests (`Game.cs:2496-2498`). A new
   location-based type must be added there or it will point at a different tile.
7. **`tracked` is serialized.** A save with a tracked quest whose type was
   removed from the build will still load; the switch defaults then produce a
   blank quest rather than a crash. Do not rely on that.
8. **Older builds cannot understand newer types.** Loading a save containing
   enum value 6 into a build without `Hunt` produces a quest with
   `type = (Quest.Type)6`, `Title`/`Text` empty, reward `0`, and
   `IsDone() == count >= max`. This is silent in the UI. Add new content in a
   build that supports it, or add an explicit unknown-type fallback.
9. **No runtime-only quest state exists.** If the new type needs cached text or
   a transient target, mark it `[NonSerialized]` and rebuild it after load, as
   other runtime fields do.

---

## 5. Pain points and hard-coded logic

### 5.1 Top pain points

| # | Pain point | Evidence | Impact when adding content |
| ---: | --- | --- | --- |
| 1 | Quests are generated by one large random switch, not by data. | `Game.GenerateQuest` (`Game.cs:1809-1961`); weights are `% 8` plus comments. | Every new type or target edits the same 150-line method; rebalancing is manual and easy to get wrong. |
| 2 | Every quest member is an open `switch` with a `_ =>` fallback. | `Quest.cs:26-102`. | A new enum value compiles but renders blank, pays 0, is instantly done and is never similar to anything. Nothing fails loudly. |
| 3 | Progress is mutated in unrelated systems. | `PostCombat` (`Game.cs:608-617`), `Explore` (`Game.cs:272-283`), `World.Update` (`World.cs:883-935`), `AcceptQuest` (`GuildScreen.cs:126-131`), `PropertiesScreen` (`PropertiesScreen.cs:150-189`). | A new progress source needs edits in 3-6 files and has no single place to test. |
| 4 | Three overlapping "difficulty" numbers. | `difficulty`, `locationDifficulty`, `difficultyMod` (`Quest.cs:21-22`); assigned at `Game.cs:1821-1943`. | New content must decide three values with no documented contract; reward and guild promotion use different ones. |
| 5 | Generation weights are code comments. | `Game.cs:1816-1944`. | Adding one case changes every probability; comments drift immediately. |
| 6 | Quest state and world state mutate each other. | Generation resets tiles (`Game.cs:1950-1957`); expiry clears properties (`Game.cs:2294-2302`); `OnNewDay` infestation resets `defeatedEnemies` (`Game.cs:1582-1587`); `World.Update` decays counts (`World.cs:883-935`). | A new location type can clear, re-infest or reset another quest's target; a `Clear` quest can complete without clearing the tile. |
| 7 | Story quests are two special cases. | `IsUnique` (`Quest.cs:25`); `DragonStatus`/`SpiderStatus` (`Game.cs:12-27`, `37-38`); literal `"dragon"`/`"spider queen"` in `PostCombat` and `StartCombat`. | A third story quest copies the whole pattern, including a new `Game` status enum and save field. |
| 8 | A single tracked quest and a single map outline. | `tracked` (`Quest.cs:23`); `Map.Show` (`Map.cs:74-81`); `Map.UpdateMap` (`Map.cs:305-310`). | Multi-location, multi-objective or escort quests cannot be shown without UI work. |
| 9 | Guild rank is the only gate and the only active cap. | `GuildScreen.cs:54`, `69-94`. | New quest types with prerequisites, reputation or level requirements have nowhere to live. |
| 10 | No stable quest id or definition. | `Quest` is a flat class (`Quest.cs:5-23`); no `id`, `def`, `giver` or `requirements`. | Duplicate detection, save migration, chains and localization are all string/position based. |
| 11 | References are persisted by name, only for two types. | `Quest.cs:74-88`; `Enemy`/`Item` are not `[Serializable]`. | A new reference field silently becomes `null` after load. |
| 12 | The board UI is fixed to `ItemEntry` rows. | `GuildScreen.cs:60-107`; `Journal.cs:43-68`. | No description, giver, deadline, objective list or reward breakdown; long text can overflow. |
| 13 | There are two parallel "clear" systems. | `Quest` `Clear`; paid property clearing in `GuildScreen.PayToClear` (`GuildScreen.cs:211-225`) and `Property.Event "Infested"` (`Game.cs:1553-1594`). | A new quest that interacts with infestations must update both; they use different state. |
| 14 | No tests or validation. | No test assembly; only F5/`[ContextMenu("Refresh quests")]` (`GuildScreen.cs:28-31`, `Game.cs:2509-2524`). | Every change is verified by manual play; silent quest failures are easy to miss. |
| 15 | Quest text depends on static globals. | `Quest.Text` calls `Global.Player`/`Global.World` (`Quest.cs:40-55`, `118-124`). | Cannot evaluate a quest without a live game; NRE before init or in tests; inventory is queried on every UI refresh. |
| 16 | Integer and location assumptions are unchecked. | `100 * count / max` (`Quest.cs:47`); `max = 0` completes instantly; `GetLocation(-1)` throws (`World.cs:813-819`); `location -1` is valid for "anywhere". | New types must set `max` and validate locations or crash/instant-complete. |
| 17 | `Gather` counts inventory, not quest progress. | `Quest.IsDone` (`Quest.cs:104-110`); `AcceptQuest` only seeds `Clear`. | A new "collect after accepting" quest cannot be expressed; pre-owned items finish it instantly. |
| 18 | No active quest timer. | `UpdateQuests` only ages `availableQuests` (`Game.cs:2289-2307`). | Timed quests from `todo.txt` need a new daily pass and UI. |

### 5.2 Detailed hard-coded inventory

#### `Quest.cs`

| Line | Code / check | Why it is hard-coded |
| --- | --- | --- |
| 7-15 | `enum Type` | Six fixed types. No `all` array, `AsString` or metadata. |
| 25 | `IsUnique => UniqueDragon || UniqueSpider` | Story identity is an enum value, not a field. |
| 30-37 | `Reward` switch with `_ => 0` | Formula per type; no per-quest reward. |
| 44-53 | `Text` switch with `_ => string.Empty` | Text per type; `Gather` reads inventory; `Clear` maps a city to "sewers". |
| 60-67 | `Title` switch with `_ => string.Empty` | Title per type; `UniqueDragon`/`UniqueSpider` reuse `Text`. |
| 71-72 | `TitleReward` / `TextReward` | Unique quests hide the gold suffix. A unique quest with a numeric reward displays nothing. |
| 76-80 | `OnBeforeSerialize` | Only `Defeat` writes `enemyName`; only `Gather` writes `itemName`. |
| 84-88 | `OnAfterDeserialize` | Only those two restore references, with `Get` (throws on unknown). |
| 96-101 | `IsSimilar` switch | `Defeat` compares enemy, `Clear`/`Artifact` compare location, `Gather` compares item, everything else returns false. A new type with a broad target can generate duplicates forever. |
| 106-109 | `IsDone` | `Gather` checks inventory; everything else is `count >= max`, so `max = 0` is done. |
| 114-115 | `Finish` | Only `Gather` removes items. No generic turn-in. |
| 118-124 | `GetLocationName` | Any `TileType.City` becomes "sewers"; hidden tiles use `RealName`. |

#### `Game.cs`

| Line | Code / check | Why it is hard-coded |
| --- | --- | --- |
| 33 | `List<Quest> availableQuests, activeQuests` | Quests live directly on the `Game` MonoBehaviour; no quest system object. |
| 37-38 | `DragonStatus`, `SpiderStatus` | Story state is on `Game`, not on `Quest`. |
| 272-283 | `Explore` artifact quest | `Artifact` is the only type that can be advanced by the treasure room; it updates or removes a quest by exact location. |
| 608-617 | `PostCombat` progress | Only `Defeat` and `Clear` are handled. |
| 664-677 | Dragon kill | Literal `"dragon"`, `dragonStatus`, `RemoveQuest`. |
| 680-693 | Spider kill | Literal `"spider queen"`, `spiderStatus`, `cityMapping[1]`, `count = 1`. |
| 734-735 | `tile.defeatedEnemies += enemyList.Count` | Only when `world.level == tile.foundLevel`; clear quests can complete while the tile is not cleared. |
| 741-769 | Clear condition | `IsClearable`, `GetEnemiesCount`, fixed property branches, removes an available `Clear` quest at the location. |
| 767-769 | `availableQuests.Remove` | Removing the offer does not mark the quest finished; it just disappears. |
| 1089-1104 | City/village entry | Dragon win and spider reward are hard-coded by type and location. |
| 1112-1122 | Boss cave entry | Dragon quest location is set only for `difficulty == 3`; spider uses cobweb text. |
| 1215-1217 | HUD | One tracked quest, raw `Text`. |
| 1582-1587 | Infestation event | `tile.clear = false; tile.defeatedEnemies = 0` can invalidate an active `Clear` quest's world progress. |
| 1620-1628 | `NewGame` | Only the dragon quest is created; all other quests come from the generator. |
| 1809-1961 | `GenerateQuest` | Every type, weight, target and reward tier is code. |
| 1823, 1890 | `properties` filter | Uses the for-sale list (`Game.properties`), not player-owned properties. |
| 1845, 1921-1927 | Item names | `"herb"`, `"mana herb"`, `"magic crystal"`, `"rare herb"` are literals; a renamed item breaks generation. |
| 1863, 1893 | Cave predicates | `!x.mine`, `!x.boss`, `x.difficulty == difficulty`; no general "clearable location" selector. |
| 1881-1882 | `allowMine` | Computed and never used. The intent (allow mine quests when the mine is active) is dead code. |
| 1916-1917 | `goto case default` | `default` is the default literal `0`, so this jumps to `case 0` (Clear), not the default label. See 5.3. |
| 1948-1959 | `while (true)` dedupe | No iteration bound and no `-1` guard; `IsSimilar` is the only exit condition. |
| 1950-1957 | Tile reset | Generating an `Artifact`/`Clear` quest mutates the target tile. |
| 1963-1973 | `RemoveQuest` | Auto-track fallback excludes only `UniqueDragon`. |
| 2294-2302 | Clear offer expiry | Expiring a `Clear` offer on a property tile sets `tile.clear = true`. |
| 2310-2329 | Board refill | `Count < 9`, three per difficulty, `Rand % 3 == 0`. |
| 2336-2345 | `SortQuests` | Sorts by difficulty then timer; `GuildScreen`'s "unavailable" grouping relies on it. |
| 2406-2429 | `ViewQuest` | One hard-coded spider board: text, status, location predicate and tracking. |
| 2496-2498 | `RegenerateWorld` cleanup | Removes only `Artifact` and `Clear`; other location types become stale. |
| 2509-2524 | `RefreshQuests` (editor) | Resets only `Clear` property tiles, then clears the board. |

#### `GameDialogs/GuildScreen.cs`

| Line | Code / check | Why it is hard-coded |
| --- | --- | --- |
| 9-11 | `MaxGuildRank = 4`, rank names | Four ranks; new content cannot add a rank gate. |
| 28-31 | F5 refresh | Editor-only quest debug. |
| 54-67 | Accepted list | Excludes `IsUnique`; fixed "Finish"/"Cancel" buttons. |
| 69-94 | Available/unavailable split | `quest.difficulty <= guildRank`; `acceptedQuestCount < guildRank`. |
| 97-109 | Paid property clearing | A second quest-like system using `Property.Event "Infested"` and `infestedCost`; not a `Quest`. |
| 121-135 | `AcceptQuest` | Seeds only `Clear`; auto-tracks first quest. |
| 137-172 | `FinishQuest` | Reward + guild progress; no type-specific reward or turn-in. |
| 174-209 | `CancelQuest` | Only `Artifact`/`Clear` get a real re-offer check; everything else defaults to true. |
| 211-225 | `PayToClear` | Directly edits the property's `Infested` event; no quest record. |

#### `World.cs`, `PropertiesScreen.cs`, `Map.cs`, `Journal.cs`

| Location | Code / check | Why it is hard-coded |
| --- | --- | --- |
| `World.cs:884`, `919` | `FirstOrDefault(x => x.type == Clear && x.location == index)` | Only `Clear` reacts to world timers. |
| `World.cs:893-895` | `Global.Game.properties` | Uses the for-sale list, not `player.properties`; an owned mine is not updated here. |
| `World.cs:900-904`, `929-933` | `--tile.defeatedEnemies`, `--quest.count` | World respawn and quest progress are coupled one-to-one. |
| `PropertiesScreen.cs:150-159` | Buying a property removes `Clear` quests at its location | Only `Clear`; a new location-bound type stays active. |
| `PropertiesScreen.cs:176-189` | Buying Inn city 1 cancels `UniqueSpider` | Hard-coded story interaction. |
| `PropertiesScreen.cs:303` | `!CurrentTile.clear && Location.IsClearable()` | Clearability is a `TileType` list, not quest data. |
| `Map.cs:74-81` | One tracked quest outline | No multi-quest or multi-location display. |
| `Map.cs:305-310` | `tracked && location == index` | Same single-quest assumption during map updates. |
| `Journal.cs:43-68` | All active quests, one tracked | No description/giver/deadline; one track slot. |

#### `Tile.cs` and `TileType.cs`

| Location | Code / check | Why it is hard-coded |
| --- | --- | --- |
| `Tile.cs:9-10` | `defeatedEnemies`, `timer`, `foundTreasure`, `clear` | Quest progress for `Clear`/`Artifact` is world state, not quest state. |
| `TileType.cs:83-93` | `IsClearable` | Fixed tile-type list; a new clearable region needs an edit. |
| `TileType.cs:110-117` | `GetEnemiesCount` | 20 for forest/mountains/enchanted, 10 otherwise; `Clear` quest `max` is hard-coded to 10. |
| `TileType.cs:105-108` | `IsProperty` | Fixed Sawmill/Mine/Farm list; property quests depend on it. |

### 5.3 Concrete defects and risks found while analysing

These are observations from the current code, not changes made by this document.

1. **A new `Quest.Type` fails silently.** `Reward` returns `0`, `Title`/`Text`
   return an empty string, `IsSimilar` returns false and `IsDone` returns
   `count >= max` (true when `max` is 0). Nothing throws or logs. Always update
   every switch in section 3.3, and consider replacing the `_ =>` defaults with
   an explicit error/log.

2. **`goto case default` jumps to `case 0`, not `default`.** In
   `Game.cs:1916-1917`, the difficulty-3 `Gather` branch uses
   `goto case default;`. In C#, `default` here is the default literal, which is
   `0` for the `int` switch, so it jumps to `case 0` (the `Clear` branch), not
   the `default` label (the `Defeat` branch). A minimal reproducer:

   ```csharp
   int x = 4;
   switch (x)
   {
   case 0: Console.WriteLine("case0"); break;
   case 4: if (true) goto case default; break;
   default: Console.WriteLine("default"); break;
   }
   ```

   prints `case0`. Effect: a difficulty-3 roll of case 4 creates a `Clear`
   quest (usually the sewers) with `difficulty = 3`, `locationDifficulty = 1`
   and `difficultyMod = 0.5`, instead of the intended `Defeat` quest. Replace
   with `goto default;` or restructure the branch.

3. **`location == -1` is dereferenced without a guard.**
   `FindRandomLocationIndex` returns `-1` when no tile matches
   (`World.cs:853-864`). `GenerateQuest` returns the quest anyway; the
   `Artifact`/`Clear` reset at `Game.cs:1950-1957` calls
   `world.GetLocation(-1)`, and `Quest.GetLocationName` does the same when the
   guild board renders. `World.GetLocation(-1)` indexes `map[-1]` and throws
   `IndexOutOfRangeException` (`World.cs:813-819`). The current world usually
   guarantees a cave/dungeon candidate, but a player-built mine, a regenerated
   world or a new narrower predicate can remove the last candidate.

4. **Generation mutates the world.** The `Artifact`/`Clear` reset at
   `Game.cs:1950-1957` sets `defeatedEnemies = 0`, `timer = 0`,
   `foundTreasure = false` and `clear = false` on the target tile. Generating a
   quest can therefore re-open a cleared location, erase an already-found
   treasure, or reset a property's clear state. For a new location type, decide
   explicitly what to reset; ideally move the reset to acceptance, not
   generation.

5. **Expiring a `Clear` offer clears the property for free.**
   `UpdateQuests` (`Game.cs:2294-2302`) sets `tile.clear = true` and
   `tile.timer = 0` when an available `Clear` quest on a property tile expires.
   The property's infestation event is not removed, so the state is
   inconsistent: the tile says cleared, the property still says infested. Verify
   whether this is intended before reusing the pattern.

6. **`allowMine` is dead code.** `Game.cs:1881-1882` computes
   `allowMine = properties.Any(x => x.name == mineName && x.status == Active)`
   and never reads it. A mine-clear quest is therefore never gated by mine
   ownership. Either use it or delete it; a new type should not copy the
   variable.

7. **`Clear` quest `max` does not match `GetEnemiesCount`.** `Clear` quests are
   created with `max = 10` (`Game.cs:1822`, `1866`, `1889`), but
   `TileType.GetEnemiesCount` is 20 for forest, mountains and enchanted forest
   (`TileType.cs:110-117`). A new `Clear` target in those regions completes at
   10 kills while the tile is still infested. Use
   `tile.type.GetEnemiesCount()`.

8. **`Clear` progress can complete without clearing the tile.** `PostCombat`
   increments `tile.defeatedEnemies` only when
   `world.level == tile.foundLevel` (`Game.cs:734-735`), but increments
   `quest.count` whenever the location matches (`Game.cs:612-616`). On a lower
   dungeon level, the quest can reach `max` while the tile's clear condition is
   not met.

9. **Infestation resets world progress but not quest progress.**
   `Game.OnNewDay` sets `tile.clear = false` and `tile.defeatedEnemies = 0`
   when a property becomes infested (`Game.cs:1582-1587`), but an active
   `Clear` quest keeps its `count`. The quest can then be finished while the
   tile is infested, and `World.Update` decays the count from an inconsistent
   base.

10. **`Clear` progress is seeded from old fights.** `AcceptQuest` sets
    `quest.count = tile.defeatedEnemies` (`GuildScreen.cs:130`), so enemies
    killed before accepting count. That may be intended, but it means the player
    can accept a nearly-finished quest. A new location type should decide
    whether to seed.

11. **`Gather` is not "collect after accept".** `IsDone` checks the current
    inventory (`Quest.cs:106-107`), so items gathered before accepting count
    immediately. There is no "collected since accept" counter. A delivery/collect
    quest needs a new field and a hook in `Hero.AddItem` or the loot paths.

12. **`Gather` `Finish` can take team items.** `Player.CountItem`/`RemoveItem`
    operate on `Hero.items`; `RemoveItem` picks the first matching stack
    regardless of its `team` flag (`Hero.cs:270-273`, `314-329`). A new quest
    item that can be team-owned needs a team-aware removal rule.

13. **`GetLocationName` turns every city into "sewers".** `Quest.cs:118-124`
    returns `TileType.Sewers.AsString()` for any `TileType.City`. A new quest
    that targets an Inn, shop or city NPC would display "sewers".

14. **`Text` divides by `max`.** `100 * count / max` (`Quest.cs:47`) throws on
    `max = 0` and truncates with integer division. `IsDone` treats `max = 0` as
    complete. A new type must set `max` and, if it can be 0, guard the text.

15. **`OnBeforeSerialize`/`OnAfterDeserialize` only know two types.** A new
    type with an `Enemy`, `Item`, `Property` or other reference will be `null`
    after load. Add a name/index shadow and restore it, or use a serializable
    surrogate.

16. **Story quests use `First(...)` without a null guard.** `Game.cs:687`,
    `Game.cs:1098` and `PropertiesScreen.cs:182` call `First` for
    `UniqueSpider` when a status says it should exist. If the quest was removed
    or the status and list disagree after a load or a regeneration, the screen
    throws. New story quests should use `FirstOrDefault` and handle the missing
    case.

17. **`RemoveQuest` auto-tracks a new quest unexpectedly.** After removing a
    tracked quest, it tracks the first remaining quest that is not
    `UniqueDragon` (`Game.cs:1963-1973`). A new story or location-less quest can
    become tracked and replace the player's choice. Consider excluding all
    unique/story types, or preserving the previous tracking preference.

18. **`RegenerateWorld` only removes `Artifact`/`Clear`.** A new location-based
    type remains in `availableQuests`/`activeQuests` with a stale tile index
    after `World.Init` (`Game.cs:2496-2498`). Add it to the cleanup or rebuild
    its target.

19. **`PropertiesScreen` only removes `Clear` quests on purchase.** Buying a
    property removes `Clear` offers/actives at that location
    (`PropertiesScreen.cs:150-159`). A new quest type that targets a property
    (delivery, inspection, defense) is not removed and can point at a
    now-owned property.

20. **The board refill can stall.** `UpdateQuests` adds each missing slot only
    with `Utility.Rand % 3 == 0` (`Game.cs:2322`), so a board below nine can stay
    below nine for several in-game days. A new rare type compounds this.

21. **`availableQuests` timers are invisible.** `timer` is decremented but never
    shown in `GuildScreen`, `Journal` or the HUD. A new timed quest needs UI or
    the player cannot see the deadline.

22. **Two clear systems can disagree.** A `Clear` quest, the property
    `Infested` event and `GuildScreen.PayToClear` all mutate the same tile and
    property state. `PayToClear` only sets the event timer
    (`GuildScreen.cs:211-225`); `OnNewDay` clears the event and sets
    `tile.clear = true` (`Game.cs:1469-1475`); `PostCombat` removes the event for
    map property types (`Game.cs:757-763`). A new quest must pick one source of
    truth.

23. **`World.Update` uses the for-sale list for mine state.**
    `Global.Game.properties.FirstOrDefault(...)` (`World.cs:893`) is
    `Game.properties`, not `player.properties`. An owned mine whose tile timer
    expires is not set to `Status.None`; the property stays active. This is
    latent for `Clear` property quests and should be verified before relying on
    it.

24. **New content in an old build silently degrades.** Loading a save with
    `(Quest.Type)6` into a build without `Hunt` yields a blank, 0-reward,
    instantly-done quest. There is no unknown-type banner, log or migration.

### 5.4 Proposed refactor

These are recommendations; none are implemented in this checkout.

**Phase 1 - quest type metadata (no save change)**

- Add `QuestTypeInfo` and a `QuestTypeMethods` static class, following the
  `ClassInfo` pattern in `docs/adding-a-hero-class.md`:
  `name`, `unique`, reward parameters, text/title delegates, `similar` predicate,
  `isDone` predicate, `finish` action.
- Add `QuestTypeMethods.all`, `AsString()` and a `GetRandom()` helper, as
  `AGENTS.md` asks for gameplay enums.
- Replace the `Quest` switches with dictionary lookups. A missing entry should
  throw or log `[ERROR quest type ...]` instead of returning a blank string.
  This turns the silent failure in 5.3 item 1 into an immediate error.
- Add an editor validation method that checks every `Quest.Type` has a
  `QuestTypeInfo`, text, reward and similarity rule.

**Phase 2 - one progress entry point**

- Add `QuestSystem` (or `Game.ProgressQuests`) with events such as
  `OnEnemyDefeated(enemy, location, count)`, `OnItemAdded(item, count)`,
  `OnLocationCleared(location)`, `OnDayPassed()`, `OnPropertyBought(property)`.
- Move the `PostCombat`/`Explore`/`World.Update`/`AcceptQuest` mutations behind
  those events. Quest type handlers decide whether and how to progress.
- Add a `QuestObjective` value (`Kill`, `Collect`, `Reach`, `Clear`, `Talk`,
  `Own`, `StoryFlag`) so a new type can compose objectives instead of adding a
  new switch arm.

**Phase 3 - data-driven generation**

- Add a `QuestDef` array (code, not ScriptableObjects, per `AGENTS.md`) with:
  `type`, `difficulty`, weight per difficulty band, target selector
  (`Func<World, QuestTarget>`), `min`/`max` count, reward parameters and
  `maxActive`.
- Replace the `Utility.Rand % 8` switch with a weighted table. Weights become
  data, and adding a type no longer changes every other probability.
- Add generator validation: a definition that returns `null` is skipped, not
  returned as a `-1` location.
- Move the tile reset out of generation and into an explicit
  `Quest.OnAccept`/`OnGenerate` step so the side effect is deliberate.

**Phase 4 - identity, story and save migration**

- Add a stable `id`/`defId` to `Quest` and `QuestDef`, separate from display
  text.
- Add `saveVersion` to `Game` and migrate old quests by type + fields.
- Replace `DragonStatus`/`SpiderStatus` with a story-state dictionary keyed by
  quest/def id, or keep the fields but move the story checks behind quest
  handlers. Append values; never reorder.
- Add `[SerializeReference]` quest subclasses only if the flat class becomes
  unmanageable; a flat class with objectives is usually easier to migrate.

**Phase 5 - UI and verification**

- Add a quest detail pane (description, giver, objectives, deadline, reward).
- Add multi-quest / multi-location map markers.
- Add an editor debug helper that generates 100 quests of every type, accepts
  one of each, advances days, saves/loads and validates that no quest is blank,
  no location is `-1`, no enemy/item is `null`, and no quest is instantly done.
- Add a validation pass that runs on new game and on load (editor-only) and
  logs every invalid quest instead of letting the UI throw.

### 5.5 What not to do

- Do not insert or reorder `Quest.Type` values; old saves store the number.
- Do not add a `Quest.Type` without updating **all** members in section 3.3.
  The `_ =>` defaults hide the omission.
- Do not copy `goto case default;`; use `goto default;` or restructure.
- Do not assume `location` is valid: `-1` is legal for "anywhere" and is also
  what `FindRandomLocationIndex` returns on failure.
- Do not assume `max > 0`, `enemy != null` or `item != null` in text/title code.
- Do not copy the `Artifact`/`Clear` tile reset into a new type without deciding
  what world state it should change.
- Do not add a reference field without a save shadow; `Enemy` and `Item` are not
  `[Serializable]`.
- Do not rely on `First(...)` for story quests; status and list can disagree.
- Do not rename an existing item or enemy that a `Defeat`/`Gather` quest uses;
  the save stores the name.
- Do not use `Game.properties` when you mean `player.properties`; they are
  different lists and several quest/property paths already mix them.

---

## 6. Verification checklist

There is no automated gameplay test or CLI run of the game. Do the following in
the Unity Editor after the compile/static checks pass.

1. Compile in Unity; Console has no errors.
2. New game: the board contains six quests (two per difficulty) and the dragon
   quest is active with `location = -1`.
3. Open the guild at rank 1, 2, 3 and 4; verify the accepted cap, the
   available/unavailable split and the `Pick`/`Finish`/`Cancel` buttons.
4. Accept, progress, finish and cancel one quest of each existing type. Check
   gold, guild progress, promotion/demotion, item removal (`Gather`) and
   tracking.
5. Let a `Clear` offer expire; verify the board removes it and the property/tile
   state matches the intended rule (see 5.3 item 5).
6. Save and load each type; verify `enemy`/`item` are restored, `count`, `timer`,
   `location`, `tracked` and `type` persist.
7. Load a save made before the new type existed; verify existing quests are
   unchanged and no text/title throws.
8. Open the Journal and the map with each quest tracked; verify the HUD line and
   the map outline, including `location == -1`.
9. For a new type: generate at least 100 quests (F5 in the guild or the
   `Refresh quests` Context Menu) and verify no blank title, no `-1` location,
   no `null` enemy/item and no instantly-done quest.
10. For a new location type: clear/build/buy the target, let it respawn, and run
    `RegenerateWorld`; verify no stale quest points at the wrong tile.
11. For a new story quest: accept it, complete it, return for the reward, load a
    mid-quest save and load a save from before the quest existed; verify the
    status field and quest list stay in sync and no `First(...)` throws.
12. Regression-test `Defeat`, `Clear`, `Gather`, `Artifact`, the dragon and the
    spider, including the Inn reward and the portal unlock.
13. Update `readme.txt` for any new key/board location and `stats.txt` for new
    balance data.

Static checks that can be run from the repository root:

```powershell
dotnet build Assembly-CSharp.csproj --nologo -v:q
git diff --check
```

Expected: build succeeds with 0 warnings/0 errors, and `git diff --check`
reports no whitespace errors.

---

## 7. Reference index

### 7.1 Quest model

- `Quest`: `Assets/Scripts/Quest.cs`
- `Quest.Type`: `Quest.cs:7-15`
- `Quest.Reward`: `Quest.cs:26-39`
- `Quest.Text` / `Title`: `Quest.cs:40-70`
- `Quest.OnBeforeSerialize` / `OnAfterDeserialize`: `Quest.cs:74-88`
- `Quest.IsSimilar`: `Quest.cs:90-102`
- `Quest.IsDone` / `Finish`: `Quest.cs:104-116`
- `Quest.GetLocationName`: `Quest.cs:118-124`
- `Game.availableQuests` / `activeQuests`: `Game.cs:33`

### 7.2 Generation and lifecycle

- `Game.NewGame`: `Game.cs:1609-1652`
- `Game.GenerateQuest`: `Game.cs:1809-1961`
- `Game.RemoveQuest`: `Game.cs:1963-1973`
- `Game.GenerateInitialQuests`: `Game.cs:2275-2284`
- `Game.UpdateQuests`: `Game.cs:2286-2334`
- `Game.SortQuests`: `Game.cs:2336-2345`
- `Game.ViewQuest`: `Game.cs:2406-2429`
- `Game.RefreshQuests` (editor): `Game.cs:2509-2524`
- `Game.RegenerateWorld` (editor): `Game.cs:2436-2501`

### 7.3 Progress and world state

- `Game.PostCombat`: `Game.cs:599-...` (quest progress at `608-617`, clear at `741-769`)
- `Game.Explore` artifact: `Game.cs:266-287`
- `Game.OnNewDay`: `Game.cs:1455-1596`
- `Game.OnEnterLocation`: `Game.cs:1084-1150`
- `Game.StartCombat` boss setup: `Game.cs:525-597`
- `World.Update`: `World.cs:866-938`
- `TileType.GetEnemiesCount`: `TileType.cs:110-117`
- `TileType.IsClearable`: `TileType.cs:83-93`
- `TileType.IsProperty`: `TileType.cs:105-108`
- `Tile`: `Tile.cs:1-142`

### 7.4 UI

- `GuildScreen`: `GameDialogs/GuildScreen.cs`
- `Journal`: `GameDialogs/Journal.cs`
- `Map`: `GameDialogs/Map.cs`
- `Game.UpdateText` HUD: `Game.cs:1200-1237`
- `Game.UpdateButtons` quest buttons: `Game.cs:1680-1789` (`BtQuest` at `1749`)
- `ItemEntry`: `ItemEntry.cs`
- `ItemEntryList`: `ItemEntryList.cs`

### 7.5 Content and save

- `Enemy.enemies`: `Enemy.cs:73`
- `Enemy.Get` / `TryGet` / `GetRandom`: `Enemy.cs:46-71`
- `Item.items`: `Item.cs`
- `Item.Get`: `Item.cs`
- `Game.SaveGame` / `LoadGame`: `Game.cs:1654-1672`
- `AGENTS.md` (save/load rules, enum convention, no ScriptableObjects)

---

## 8. Open decisions

These need a product decision before implementing a new quest type:

1. **Should quests be a flat class or a type hierarchy?** A flat `Quest` with
   optional fields is easy to save and matches the current code; subclasses with
   `[SerializeReference]` are cleaner for many distinct types but need save
   migration and more wiring.
2. **Should quest definitions be code arrays or ScriptableObjects?** The
   repository convention is code arrays (`Enemy.enemies`, `Item.items`,
   `Property.properties`, `ClassMethods.classes`); a `QuestDef` code array fits
   that convention and avoids asset wiring.
3. **Should generation be a weighted table?** The current `% 8` switch makes
   every addition a rebalance. A table makes weights explicit but changes the
   existing distribution; decide whether to preserve the current odds exactly.
4. **What are the three difficulty fields' contracts?** Today `difficulty` is
   the guild tier, `locationDifficulty` is a reward tier derived from several
   sources, and `difficultyMod` is guild progress. Define them or collapse them.
5. **Should `Gather` become "collect after accepting"?** Today pre-owned items
   count. A change needs an accepted-time baseline or a separate progress field.
6. **Should active quests expire?** `todo.txt` lists "timed" quests. Today only
   offers age. A deadline needs a daily pass, UI and a failure penalty.
7. **Should unique/story quests be representable as data?** Today they are two
   enum values plus two `Game` status fields. A third story quest copies that
   pattern.
8. **Should the spider board and paid property clearing become generic quest
   sources?** They are two parallel systems today. Unifying them removes
   duplicate state but is a larger UI/save change.
9. **Should quests have a giver, requirements or reputation?** `Player.guildRank`
   is the only gate; `todo.txt` lists "guild rank benefits" and "quests in
   village, no guild?". Decide whether a new quest type needs those systems
   before adding fields.
10. **Should a unique quest show a reward?** `TextReward` hides gold for unique
    quests (`Quest.cs:72`). If a new story quest pays gold, either add a reward
    field/display or keep the reward out of band.
11. **Should `Clear` completion be tied to `tile.clear` instead of `count`?**
    The two can disagree (5.3 items 7-9). A single source of truth would make
    new clear-type quests much safer.
12. **Should old builds reject newer quest types?** Today they silently render a
    blank, instantly-done quest. A save version or an unknown-type fallback
    would make the incompatibility visible.
