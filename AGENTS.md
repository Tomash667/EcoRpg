# AGENTS.md

EcoRpg ("economic", not ecologic) — a 2D economic RPG built with Unity 6 (exact version in `ProjectSettings/ProjectVersion.txt`). Single C# assembly, no asmdefs, no test suite, no CI. Developed and run exclusively inside the Unity Editor on Windows.

## Running / verifying changes

- The only real verification is compiling and playing in the Unity Editor. There are no CLI build/test/lint commands; the Unity Test Framework package is installed but no test assemblies exist — don't invent test commands.
- Two scenes: `Assets/Scenes/Menu.unity` (new/load game) → `Assets/Scenes/Game.unity` (all gameplay).
- `Game.cs` and `Menu.cs` import `UnityEditor` unconditionally, so player builds do not compile. Don't "fix" this unless asked; editor-only is the norm.
- Editor-only helpers live behind `#if UNITY_EDITOR`: F4 exits Play Mode, holding 8/9 while exploring forces encounter outcomes, and the `Game` component has `[ContextMenu]` dev helpers (e.g. "Test combat", `TestImages`).

## Architecture

- `Global` (singleton, DontDestroyOnLoad) exposes static accessors used everywhere: `Global.Game`, `Global.World`, `Global.Player`, `Global.UI`, `Global.Location`. It owns save slots and the `loadGame` flag consumed by the Game scene.
- `Game` (`Game.cs`, ~2.5k lines) is the hub MonoBehaviour holding world, player, team, quests, properties, workers, and the clock, plus most gameplay logic. It re-renders via `UpdateText()` / `UpdateButtons()`. Put gameplay logic here; keep `Scripts/GameDialogs/*` screens as thin UI — they are `GameDialog` subclasses reading static `GameDialog.game/ui/player/text`, assigned in `Game.OnEnable`.
- Scene wiring is hardcoded by name: `Game.Awake()` resolves screens via `transform.Find("Shop")`, `transform.Find("Combat")`, etc. Renaming GameObjects in the Game scene breaks the game; new UI needs a matching hierarchy path plus a code lookup.
- All game data is code, not assets (no ScriptableObjects): `Enemy.enemies`, `Item.items` / `cityItems` / `villageItems`, `Recipe.recipes`, `Property.properties` are static arrays. Add content there.
- Enum convention: each gameplay enum gets a static extension class (`Class`/`ClassMethods`, `Race`/`RaceMethods`, `Skill`/`SkillMethods`, `TileType`/`TileTypeMethods`) with an `all` array, an `AsString()` switch, and `GetRandom()`. Follow it.
- Portraits are `Assets/Resources/Portraits/*.jpg` loaded by name (`Portraits/{enemy.name}`, `Portraits/male {class}`, `Portraits/female {class}`); a missing file logs an error at runtime.
- Narrative/UI text is built with `TextBuilder` (`Game.Text`) into TMP labels; grammar helpers live in `Utility` (e.g. `Utility.Plural`).

## Save/load — be careful

- `SaveGame()` runs `JsonUtility.ToJson(this)` on the entire `Game` object; loading uses `FromJsonOverwrite`. `player` and team members are `[SerializeReference]`, so renaming serialized classes or fields silently breaks existing saves — there is no versioning or migration.
- `[NonSerialized]` fields (e.g. `Player.goldReceived`) do not persist; rebuild runtime-only state after load.
- Saves live in `Application.persistentDataPath/saves/N.sav` — first line is a human-readable summary, the rest is JSON; the slot index is in `save.json`. The root `saves.lnk` shortcut opens the saves folder.

## Conventions / gotchas

- Input uses legacy Input Manager polling (`Input.GetKeyDown`) even though the Input System package is installed — match the existing `Input` API.
- Player-facing shortcuts and mechanics are documented in `readme.txt`; update it when adding or changing keys/mechanics.
- `.editorconfig` disables naming-style diagnostics (IDE1006): naming is deliberately nonstandard (`player.clas`, `nameYou`, lowercase members). Never mass-rename or "fix" naming.
- `Work/` (Paint.NET `.pdn` sources and PNG art) is gitignored — art sources stay local; only `Assets/` content is tracked.
- Design reference data: `stats.txt` (enemy stats per level, location difficulty progression) and `todo.txt` (roadmap / known bugs). Consult these when balancing or picking work.
