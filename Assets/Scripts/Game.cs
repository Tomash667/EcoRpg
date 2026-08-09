using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public enum DragonStatus
    {
        None,
        Defeated,
        Win
    }

    public const int MaxTeamSize = 3;
    private const int MaxGuildRank = 4;

    private static readonly string[] GuildRanks = new[] { "None", "Copper", "Silver", "Gold", "Diamond" };

    public World world;
    public Player player;
    public List<Hero> allies;
    public List<Quest> availableQuests, activeQuests;
    public List<Property> properties;
    public List<Notification> notifications;
    public DragonStatus dragonStatus;
    public float guildProgress;
    public int day, hour, minute, guildRank, freshHorses;

    private GameUI ui;
    private GameObject shopScreen, characterScreen, journalScreen, allyScreen, giveAllyItemsScreen, storeItemsScreen, activeInventory, propertiesScreen, guildScreen, gardenScreen, craftScreen,
        enchantItemsScreen;
    private RectTransform[] alliesHealthRect;
    private Map map;
    private Combat combatScreen;
    private TMP_Text text;
    private Hero activeAlly;
    private Property selectedProperty;
    private readonly StringBuilder sb = new();
    private System.Action<bool> choiceAction;
    private string lastAction, lastTestCombat;
    private float restCombatHeal;
    private int restCombatEnergy;
    private bool inChoice, traveled, restCombat;

    public IEnumerable<Hero> Team
    {
        get
        {
            yield return player;
            foreach (Hero ally in allies)
                yield return ally;
        }
    }
    public GameUI UI => ui;

    private void Awake()
    {
        ui = GetComponent<GameUI>();
        text = transform.Find("Text").GetComponent<TMP_Text>();
        shopScreen = transform.Find("Shop").gameObject;
        characterScreen = transform.Find("Character").gameObject;
        journalScreen = transform.Find("Journal").gameObject;
        allyScreen = transform.Find("Ally").gameObject;
        giveAllyItemsScreen = transform.Find("GiveItems").gameObject;
        storeItemsScreen = transform.Find("StoreItems").gameObject;
        propertiesScreen = transform.Find("Properties").gameObject;
        guildScreen = transform.Find("Guild").gameObject;
        gardenScreen = transform.Find("Garden").gameObject;
        craftScreen = transform.Find("Craft").gameObject;
        combatScreen = transform.Find("Combat").GetComponent<Combat>();
        combatScreen.Init();
        map = transform.Find("Map").GetComponent<Map>();
        map.Init();
        enchantItemsScreen = transform.Find("EnchantItems").gameObject;
        alliesHealthRect = new[] { transform.Find("Buttons/BtAlly/Health") as RectTransform, transform.Find("Buttons/BtAlly2/Health") as RectTransform };

        Global global = Global.Instance;
        global.game = this;
        if (global.loadGame)
            LoadGame();
        else
            NewGame();
        UpdateText();
        UpdateButtons();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif

        if (ui.HasDialog)
        {
            if (ui.CurrentDialog == guildScreen)
            {
                if (guildRank != 0)
                {
                    if (Input.GetKeyDown(KeyCode.C))
                        Craft();
                    if (Input.GetKeyDown(KeyCode.K))
                        Cook();
                    if (Input.GetKeyDown(KeyCode.R))
                        Recruit();
                    if (Input.GetKeyDown(KeyCode.T))
                        Train();
                }
            }
            return;
        }

        if (inChoice)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                PickChoice(true);
            if (Input.GetKeyDown(GameUI.escKey))
                PickChoice(false);
        }
        else
        {
            if (allies.Count >= 1 && Input.GetKeyDown(KeyCode.Alpha1))
                Ally(0);
            if (allies.Count >= 2 && Input.GetKeyDown(KeyCode.Alpha2))
                Ally(1);
            if (Input.GetKeyDown(KeyCode.C))
                Character();
            if (Input.GetKeyDown(KeyCode.J))
                Journal();
            if (Input.GetKeyDown(KeyCode.E))
                Explore();
            if (Input.GetKeyDown(KeyCode.R))
                Rest();
            if (world.level == 0 && world.Location != TileType.DarkDimension)
            {
                if (Input.GetKeyDown(KeyCode.T))
                    Travel();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.U))
                    GoUp();
            }
            if (world.level < world.CurrentTile.foundLevel)
            {
                if (Input.GetKeyDown(KeyCode.D))
                    GoDown();
            }

            switch (world.CurrentTile.type)
            {
            case TileType.City:
                if (Input.GetKeyDown(KeyCode.G))
                    Guild();
                if (Input.GetKeyDown(KeyCode.P))
                    ManageProperties();
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.S))
                    Shop();
                if (Input.GetKeyDown(KeyCode.X))
                    EnterSewers();
                if (Input.GetKeyDown(KeyCode.H) && (player.HaveProperty("House", cityIndex: world.CityIndex) || player.HaveProperty("Mansion", cityIndex: world.CityIndex)))
                    EnterHouse();
                break;
            case TileType.Village:
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.S))
                    Shop();
                if (Input.GetKeyDown(KeyCode.P))
                    ManageProperties();
                if (Input.GetKeyDown(KeyCode.H) && (player.HaveProperty("House", cityIndex: world.CityIndex) || player.HaveProperty("Mansion", cityIndex: world.CityIndex)))
                    EnterHouse();
                break;
            case TileType.Forest:
                if (Input.GetKeyDown(KeyCode.F))
                    Forage();
                break;
            case TileType.Sewers:
            case TileType.House:
            case TileType.Mansion:
                if (Input.GetKeyDown(KeyCode.X))
                    ExitToCity();
                break;
            case TileType.Sawmill:
            case TileType.Mine:
            case TileType.Farm:
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                break;
            case TileType.Cave:
                if (Input.GetKeyDown(KeyCode.P))
                    Forage();
                break;
            }
        }
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    public void Explore()
    {
        Tile tile = world.CurrentTile;
        bool isSmall = tile.type.IsSmall();

        if (player.energy < (isSmall ? 5 : 10))
        {
            lastAction = "You are too tired to explore.";
            UpdateText();
            return;
        }

        int chance = 7;
        if (tile.type == TileType.Plains || tile.clear)
            chance = 0;
        if (tile.foundTreasure && chance > 0)
            chance = 4;
        player.energy -= 5;

        int c = Random.Range(0, 10);

#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.Alpha8))
            c = 8;
        if (Input.GetKey(KeyCode.Alpha9))
            c = 9;
#endif

        Enemy enemy;
        if (tile.type == TileType.Dungeon && !tile.foundTreasure && tile.defeatedEnemies >= 10 && world.level + 1 == tile.levels)
        {
            int level = tile.difficulty + 2;
            Item item = Item.items.RandomItem(x => x.level == level);
            int gold = Utility.Round(Utility.Random(level * 100, level * 200));
            lastAction = $"You explore the {tile.Name} and find <b>treasure room</b>. Inside chest you find <color=#FFD700>{gold}</color> gold and <b>{item.name}</b>.";
            Quest quest = activeQuests.FirstOrDefault(x => x.type == Quest.Type.Artifact && x.location == world.CurrentLocationIndex);
            if (quest != null)
            {
                quest.count = 1;
                lastAction += $" You also find an <b>artifact</b>.";
            }
            else
            {
                quest = availableQuests.FirstOrDefault(x => x.type == Quest.Type.Artifact && x.location == world.CurrentLocationIndex);
                if (quest != null)
                    availableQuests.Remove(quest);
            }
            AddTeamGold(gold);
            player.AddItem(item, team: allies.Count > 0);
            tile.foundTreasure = true;
        }
        else if (world.level + 1 < tile.levels && tile.foundLevel == world.level && tile.defeatedEnemies >= 10)
        {
            lastAction = $"You find stairs leading to level {world.level + 2}.";
            ++tile.foundLevel;
            tile.defeatedEnemies = 0;
            UpdateButtons();
        }
        else if (c < chance && (enemy = Enemy.GetRandom(tile.type, tile.difficulty)) != null)
        {
            StartCombat(enemy, "explore");
            return;
        }
        else if (c == 8 && (tile.type == TileType.Forest || tile.type == TileType.Mountains || tile.type == TileType.Plains))
        {
            // old camp
            int count = Utility.Random(1, 4);
            player.AddItem(Item.Get("rations"), count);
            lastAction = $"You explore the {tile.Name} and find old camp. You pick up <b>{Utility.Plural("rations", count)}</b>.";
        }
        else if (c == 8 && tile.type == TileType.Dungeon && (!tile.foundTreasure || Utility.Rand % 2 == 0))
        {
            // trap
            Hero target = Team.RandomItem();
            lastAction = target == player
                ? $"You explore the {tile.Name} and step on a <color=red>trap</color>."
                : $"You explore the {tile.Name} and {target.name} step on a <color=red>trap</color>.";
            if (tile.difficulty == 1)
            {
                if (Combat.AttackChance(10, target.dex))
                {
                    target.hp -= Utility.Random(20, 25);
                    if (target.hp < 1)
                        target.hp = 1;
                    if (target == player)
                        lastAction += " A shooting arrow hits you.";
                    else
                    {
                        target.ApplyHealing();
                        lastAction += $" A shooting arrow hits {target.him}.";
                    }
                }
                else
                    lastAction += target == player ? " You dodge a shooting arrow." : $" {target.He} dodges a shooting arrow.";
            }
            else if (tile.difficulty == 2)
            {
                List<Hero> dodged = new(), hit = new();
                foreach (Hero hero in Team)
                {
                    if (Combat.AttackChance(15, hero.dex))
                    {
                        hero.hp -= Utility.Random(25, 30);
                        if (hero.hp < 1)
                            hero.hp = 1;
                        if (hero != player)
                            hero.ApplyHealing();
                        hit.Add(hero);
                    }
                    else
                        dodged.Add(hero);
                }

                if (dodged.Count > 0)
                {
                    if (hit.Count > 0)
                    {
                        lastAction += $" {Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away " +
                            $"but {Utility.PrettyList(hit.Select(x => x.nameYou))} {Utility.S("fall", hit.Count == 1 && hit[0] != player)} into a pit.";
                    }
                    else
                    {
                        if (dodged.Count == Team.Count() && dodged.Count > 1)
                            lastAction += " Everone jump away from a pit.";
                        else
                            lastAction += $" {Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away from a pit.";
                    }
                }
                else
                {
                    if (hit.Count == Team.Count() && hit.Count > 1)
                        lastAction += " Everyone fall into a pit.";
                    else
                        lastAction += $" {Utility.PrettyList(hit.Select(x => x.nameYou)).ToUpper1()} {Utility.S("fall", hit.Count == 1 && hit[0] != player)} into a pit.";
                }
            }
            else
            {
                List<Hero> dodged = new(), hit = new();
                foreach (Hero hero in Team)
                {
                    if (Combat.AttackChance(20, hero.dex))
                    {
                        hero.hp -= Utility.Random(30, 40);
                        if (hero.hp < 1)
                            hero.hp = 1;
                        if (hero != player)
                            hero.ApplyHealing();
                        hit.Add(hero);
                    }
                    else
                        dodged.Add(hero);
                }

                if (dodged.Count > 0)
                {
                    if (hit.Count > 0)
                    {
                        lastAction += $" {Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away " +
                            $"but {Utility.PrettyList(hit.Select(x => x.nameYou))} {Utility.S("are", hit.Count == 1 && hit[0] != player, "is")} caught in an explosion.";
                    }
                    else
                    {
                        if (dodged.Count == Team.Count() && dodged.Count > 1)
                            lastAction += " Everone jump away from an explosion.";
                        else
                            lastAction += $" {Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away from an explosion.";
                    }
                }
                else
                {
                    if (hit.Count == Team.Count() && hit.Count > 1)
                        lastAction += " Everyone are caught in an explosion.";
                    else
                        lastAction += $" {Utility.PrettyList(hit.Select(x => x.nameYou)).ToUpper1()} {Utility.S("are", hit.Count == 1 && hit[0] != player, "is")} caught in an explosion.";
                }
            }
        }
        else if (c == 9 && tile.type == TileType.Dungeon && (!tile.foundTreasure || Utility.Rand % 2 == 0))
        {
            // lesser treasure
            string item = tile.difficulty switch
            {
                2 => Utility.Rand % 2 == 0 ? "potion" : "elixir",
                3 => "elixir",
                _ => "potion",
            };
            int gold = Utility.Round(Utility.Random(100 * tile.difficulty, 200 * tile.difficulty));
            int count = Utility.Random(1, 2);
            AddTeamGold(gold);
            player.AddItem(Item.Get(item), count);
            lastAction = $"You explore the {tile.Name} and find chest. Inside you find <b>{Utility.Plural(item, count)}</b> and <color=#FFD700>{gold}</color> gold.";
        }
        else if (c == 9 && tile.type == TileType.Forest && tile.depleted < 4)
        {
            // herbs/rare herbs
            (Hero bestHero, int bestValue) = GetTeamSkill(Skill.Forage);
            int count = (Utility.Rand % 6) switch
            {
                1 or 2 => 2,
                3 or 4 => 3,
                5 => 4,
                _ => 1,
            };
            count += bestValue / 25 - tile.depleted;
            if (count < 1)
                count = 1;
            tile.depleted++;
            Item herb = tile.GetHerb();
            player.AddItem(herb, count);
            if (bestHero != null && bestHero != player)
            {
                float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Forage));
                lastAction = $"You explore the {tile.Name} and with {bestHero.name} help find <b>{Utility.Plural(herb.name, count)}</b>.";
                lastAction += player.Train(Skill.Forage, 0.25f * trainMod);
                bestHero.Train(Skill.Forage, 0.25f);
            }
            else
            {
                lastAction = $"You explore the {tile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.";
                lastAction += player.Train(Skill.Forage, 0.25f);
            }
        }
        else if (c == 9 && ((tile.type == TileType.Mountains && tile.depleted == 0) || (tile.type == TileType.Cave && tile.mine && tile.depleted < 4)) && tile.difficulty >= 2)
        {
            // silver/gold nuggets
            if (HaveTeamItem("pickaxe"))
            {
                (Hero bestHero, int bestValue) = GetTeamSkill(Skill.Mining);
                int count = (Utility.Rand % 6) switch
                {
                    1 or 2 => 2,
                    3 or 4 => 3,
                    5 => 4,
                    _ => 1,
                };
                count += bestValue / 25 - tile.depleted;
                if (count < 1)
                    count = 1;
                tile.depleted++;
                Item nugget = Item.Get(tile.difficulty == 2 ? "silver nugget" : "gold nugget");
                player.AddItem(nugget, count, allies.Count > 0);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Mining));
                    lastAction = $"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. " +
                        $"You and {bestHero.name} mine <b>{Utility.Plural(nugget.name, count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, 0.25f);
                }
                else
                {
                    lastAction = $"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. You mine <b>{Utility.Plural(nugget.name, count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f);
                }
            }
            else
                lastAction = $"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b> but you don't have a pickaxe...";
        }
        else if (c == 9 && tile.type == TileType.Cave && !tile.mine && !tile.boss && tile.depleted < tile.difficulty + 2)
        {
            // magic crystals
            if (HaveTeamItem("pickaxe"))
            {
                (Hero bestHero, int bestValue) = GetTeamSkill(Skill.Mining);
                int count = (Utility.Rand % 4) switch
                {
                    1 or 2 => 2,
                    3 => 3,
                    _ => 1,
                };
                count += tile.difficulty - tile.depleted - 1 + bestValue / 25;
                if (count < 1)
                    count = 1;
                tile.depleted++;
                player.AddItem(Item.Get("magic crystal"), count);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Mining));
                    lastAction = $"You explore the {tile.Name} and find small <b>magic crystals cluster</b>. You and {bestHero.name} mine <b>{Utility.Plural("magic crystal", count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, 0.25f);
                }
                else
                {
                    lastAction = $"You explore the {tile.Name} and find small <b>magic crystals cluster</b>. You mine <b>{Utility.Plural("magic crystal", count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f);
                }
            }
            else
                lastAction = $"You explore the {tile.Name} and find small <b>magic crystals cluster</b> but you don't have a pickaxe...";
        }
        else
            lastAction = $"You explore the {tile.Name} but find nothing interesting.";

        if (isSmall)
            AddTime(minutes: 30);
        else
            AddTime(hours: 1);
        UpdateText();
    }

    public void StartCombat(Enemy enemy, string startAction)
    {
        int count = (Utility.Rand % 4) switch
        {
            1 or 2 => 2,
            3 => 3,
            _ => 1,
        };

        List<Enemy> enemyList = new();
        Tile tile = world.CurrentTile;
        if (tile.boss && !restCombat)
        {
            if (tile.defeatedEnemies >= 10 && world.level + 1 == tile.levels)
            {
                enemy = Enemy.Get("dragon");
                enemyList.Add(enemy);
                enemy = Enemy.Get("dragon-man");
                for (int i = 0; i < 2; ++i)
                    enemyList.Add(enemy);
            }
            else
            {
                enemy = Enemy.Get("dragon-man");
                for (int i = 0; i < count; ++i)
                    enemyList.Add(enemy);
            }
        }
        else if (tile.type == TileType.DarkDimension)
        {
            if (tile.defeatedEnemies >= 13)
            {
                enemyList.Add(Enemy.Get("nameless horror"));
                for (int i = 0; i < 2; ++i)
                    enemyList.Add(Enemy.GetRandom(TileType.DarkDimension, 4));
            }
            else
            {
                enemyList.Add(enemy);
                for (int i = 1; i < count; ++i)
                    enemyList.Add(Enemy.GetRandom(TileType.DarkDimension, 4));
            }
        }
        else if (enemy.ally == null)
        {
            for (int i = 0; i < count; ++i)
                enemyList.Add(enemy);
        }
        else
        {
            enemyList.Add(enemy);
            Enemy ally = Enemy.Get(enemy.ally);
            for (int i = 1; i < count; ++i)
                enemyList.Add(Utility.Rand % 2 == 0 ? ally : enemy);
        }

        if (restCombat)
            combatScreen.Init(enemyList, lastAction, true);
        else
            combatScreen.Init(enemyList, startAction, false);
        ui.lockDialog = true;
        ui.ShowDialog(combatScreen.gameObject);
    }

    public void PostCombat(Combat.Result result, List<Enemy> enemyList)
    {
        Tile tile = world.CurrentTile;

        ui.lockDialog = false;
        ui.CloseDialog();

        if (result == Combat.Result.Win)
        {
            foreach (Quest quest in activeQuests)
            {
                if (quest.type == Quest.Type.Defeat)
                    quest.count += enemyList.Count(x => x == quest.enemy);
                else if (quest.type == Quest.Type.Clear)
                {
                    if (quest.location == world.CurrentLocationIndex)
                        quest.count += enemyList.Count;
                }
            }

            // gold & items
            List<string> items = new();
            int gold = 0;
            List<ItemSlot> drops = new();
            foreach (Enemy enemy in enemyList)
            {
                if (enemy.gold != Vector2Int.zero)
                    gold += enemy.gold.Random();
                if (enemy.drops != null)
                {
                    foreach (var (item, chance) in enemy.drops)
                    {
                        int itemCount = (int)chance;
                        float itemChance = chance - itemCount;
                        if (itemChance > 0 && Utility.Random() < itemChance)
                            ++itemCount;
                        if (itemCount > 0)
                        {
                            ItemSlot itemSlot = drops.FirstOrDefault(x => x.item == item);
                            if (itemSlot != null)
                                itemSlot.count += itemCount;
                            else
                                drops.Add(new ItemSlot { item = item, count = itemCount });
                        }
                    }
                }
            }
            foreach (ItemSlot itemSlot in drops)
            {
                items.Add(Utility.Plural(itemSlot.item.name, itemSlot.count));
                player.AddItem(itemSlot.item, itemSlot.count, allies.Count > 0 && itemSlot.item.subtype == Item.Subtype.Treasure);
            }
            gold = Utility.Round(gold);
            string pickups;
            if (items.Count > 0)
            {
                if (gold > 0)
                    items.Add($"<color=#FFD700>{gold}</color> gold");
                pickups = Utility.PrettyList(items);
            }
            else if (gold > 0)
                pickups = $"<color=#FFD700>{gold}</color> gold";
            else
                pickups = null;
            if (enemyList.Any(x => x.name == "dragon"))
            {
                dragonStatus = DragonStatus.Defeated;
                lastAction = "With a final blow, the dragon falls. Its roar fades into silence, and the cavern grows still. The beast is slain—its hoard and your legend now yours to claim. " +
                    $"You found {pickups}.";
                Quest quest = activeQuests.FirstOrDefault(x => x.type == Quest.Type.Unique);
                if (quest != null)
                    RemoveQuest(quest);
                tile.clear = true;
                tile.timer = 0;
                ChangeTeamAffection(10);
                foreach (Hero ally in allies)
                    ally.winToday = true;
            }
            else
            {
                if (pickups != null)
                    lastAction = $"You win a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> ({pickups} found).";
                else
                    lastAction = $"You win a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b>.";
                ChangeTeamAffection(1, ally =>
                {
                    if (ally.winToday)
                        return false;
                    ally.winToday = true;
                    return true;
                });
            }
            AddTeamGold(gold);

            // exp
            List<Hero> levelups = null;
            float ratio;
            if (allies.Count == 0)
                ratio = 1f;
            else
                ratio = 1f / (allies.Count + 1);
            if (player.AddExp(enemyList, ratio))
            {
                levelups ??= new();
                levelups.Add(player);
            }
            foreach (Hero ally in allies)
            {
                if (ally.AddExp(enemyList, ratio))
                {
                    levelups ??= new();
                    levelups.Add(ally);
                }
            }
            if (levelups != null)
            {
                foreach (var group in levelups.GroupBy(x => x.level))
                {
                    string isAre = group.Count() > 1 || group.First() is Player ? "are" : "is";
                    lastAction += $" {Utility.PrettyList(group.Select(x => x.nameYou)).ToUpper1()} {isAre} now level {group.Key}.";
                }
            }

            // quest
            if (world.level == tile.foundLevel)
                tile.defeatedEnemies += enemyList.Count;
            if (tile.timer == 0 && !tile.clear)
                tile.timer = 3;
            if (tile.type == TileType.DarkDimension && enemyList.Any(x => x.name == "nameless horror"))
                tile.defeatedEnemies = 0;

            if (!tile.boss && tile.type.IsClearable() && tile.defeatedEnemies >= ((tile.type == TileType.Forest || tile.type == TileType.Mountains) ? 20 : 10))
            {
                tile.clear = true;
                if (tile.type == TileType.Cave)
                {
                    tile.timer = 30;
                    if (tile.mine)
                    {
                        Property property = properties.FirstOrDefault(x => x.locationIndex == world.CurrentLocationIndex);
                        if (property != null)
                        {
                            property.status = Property.Status.Cleared;
                            lastAction += " You can build a <b>mine</b> here.";
                        }
                    }
                }
                else if (tile.type == TileType.Mine || tile.type == TileType.Sawmill || tile.type == TileType.Farm)
                {
                    tile.timer = 0;
                    lastAction += " You <b>cleared</b> this place.";
                    Property property = player.properties.FirstOrDefault(x => x.locationIndex == world.CurrentLocationIndex);
                    property?.RemoveEvent("Infested");
                }
                else
                    tile.timer = 30;

                Quest quest = availableQuests.FirstOrDefault(x => x.type == Quest.Type.Clear && x.location == world.CurrentLocationIndex);
                if (quest != null)
                    availableQuests.Remove(quest);
            }
        }
        else
        {
            if (result == Combat.Result.Escape)
            {
                lastAction = $"You run away from {Utility.PrettyGroup(enemyList.Select(x => x.name))}.";
                ChangeTeamAffection(-1);
            }
            else
            {
                int goldTaken = 0;
                int rationsTaken = 0;
                foreach (Enemy enemy in enemyList)
                {
                    if (enemy.gold != Vector2Int.zero)
                        goldTaken += Utility.Random(enemy.gold.x * 2, enemy.gold.y * 2);
                    else
                        rationsTaken += (enemy.level / 3 + 1) * 2;
                }

                if (goldTaken > 0)
                    goldTaken = RemoveTeamGold(goldTaken);
                if (rationsTaken > 0)
                    rationsTaken = RemoveTeamItem(Item.Get("rations"), rationsTaken);

                string lost = null;
                if (goldTaken > 0)
                {
                    if (rationsTaken > 0)
                        lost = $"<color=#FFD700>{goldTaken}</color> gold and {rationsTaken} rations lost";
                    else
                        lost = $"<color=#FFD700>{goldTaken}</color> gold lost";
                }
                else
                    lost = $"{rationsTaken} rations lost";

                if (lost == null)
                    lastAction = $"You <color=red>lost</color> a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b>.";
                else
                    lastAction = $"You <color=red>lost</color> a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> ({lost}).";

                ChangeTeamAffection(-5);
            }

            if (enemyList.Any(x => x.name == "dragon"))
                tile.defeatedEnemies -= 5;
            if (tile.type == TileType.DarkDimension && enemyList.Any(x => x.name == "nameless horror"))
                tile.defeatedEnemies = 0;
        }

        // heal after combat
        player.bored = 0;
        if (player.hp < 1)
            player.hp = 1;
        foreach (Hero ally in allies)
        {
            ally.bored = 0;
            if (ally.hp < 1)
                ally.hp = 1;
            ally.ApplyHealing();
        }

        if (restCombat)
        {
            if (result == Combat.Result.Win)
            {
                if (restCombatHeal != 0)
                {
                    player.hp = Mathf.Min(player.hp + (int)(restCombatHeal * player.hpMax), player.hpMax);
                    foreach (Hero ally in allies)
                        ally.hp = Mathf.Min(ally.hp + (int)(restCombatHeal * ally.hpMax), ally.hpMax);
                }
                player.energy = Mathf.Min(player.energy + restCombatEnergy, 100);
                lastAction += " You finish your rest.";
            }

            if (hour < 8)
                hour = 8;
            else
            {
                ++day;
                hour = 8;
            }
            OnNewDay();
            restCombat = false;
        }
        else
        {
            if (tile.type.IsSmall())
                AddTime(minutes: 30);
            else
                AddTime(hours: 1);
        }

        UpdateText();
    }

    public void Rest()
    {
        lastAction = string.Empty;
        if (OnRest())
        {
            lastAction += " It's a new day.";
            UpdateText();
        }
    }

    public void Work()
    {
        if (hour > 16)
            lastAction = "It's too late to work.";
        else if (player.energy < 50)
            lastAction = "You are too tired to work.";
        else if (!world.CurrentTile.clear && world.Location.IsClearable())
            lastAction = $"You can't work while monsters occupy the {world.CurrentTile.Name}.";
        else
        {
            player.energy -= 50;
            TileType location = world.Location;
            int payment;
            Skill skill;
            switch (location)
            {
            case TileType.Sawmill:
                payment = 30;
                skill = Skill.Woodcraft;
                break;
            case TileType.Mine:
                payment = 20 + world.CurrentTile.difficulty * 10;
                skill = Skill.Mining;
                break;
            case TileType.City:
                payment = 20;
                skill = Skill.None;
                break;
            default:
                payment = 15;
                skill = Skill.None;
                break;
            }
            // ally with a skill can help & train others
            Hero bestHero;
            int skillValue;
            if (skill != Skill.None)
            {
                (bestHero, skillValue) = GetTeamSkill(skill);
                payment += skillValue / 10;
            }
            else
            {
                bestHero = null;
                skillValue = 0;
            }
            // double pay if owned
            if (player.properties.Any(x => x.locationIndex == world.CurrentLocationIndex))
                payment *= 2;
            // give payment & train all team members
            lastAction = $"You earned <color=#FFD700>{payment}</color> gold from working.";
            foreach (Hero hero in Team)
            {
                float trainMod;
                if (skill != Skill.None && bestHero != null && bestHero != hero)
                    trainMod = 1f + 0.01f * (skillValue - hero.GetSkill(skill));
                else
                    trainMod = 1f;

                hero.AddGold(payment);
                if (skill != Skill.None)
                {
                    string str = player.Train(skill, trainMod);
                    if (hero == player)
                        lastAction += str;
                }
            }
            AddTime(hours: 8);
            if (location.IsSafe())
            {
                foreach (Hero ally in allies)
                    ally.BuyItems();
            }
        }
        UpdateText();
    }

    public void Travel()
    {
        ui.ShowDialog(map.gameObject);
        map.Show();
    }

    public void Travel(Vector2Int pt, bool enter)
    {
        if (pt == world.currentPt)
        {
            if (enter)
            {
                if (traveled)
                    OnEnterLocation();
                ui.CloseDialog();
            }
            return;
        }

        StartCoroutine(TravelLoop(pt, enter));
    }

    private IEnumerator TravelLoop(Vector2Int pt, bool enter)
    {
        traveled = true;
        ui.lockDialog = true;
        map.BeginTravel(pt);
        yield return world.Travel(pt);
        map.EndTravel();
        ui.lockDialog = false;
        if (enter && !world.cancelTravel)
        {
            OnEnterLocation();
            ui.CloseDialog();
        }
    }

    public void UpdateTravel()
    {
        map.UpdateTravel();
        UpdateText();
    }

    public void RevealLocation(Vector2Int pos)
    {
        map.UpdateMap(pos);
    }

    public void EnterSewers()
    {
        if (player.energy < 5)
        {
            lastAction = "You are too tired to travel.";
            UpdateText();
            return;
        }

        player.energy -= 5;
        lastAction = "You enter the sewers.";
        world.sublocation = 1;
        AddTime(minutes: 30);
        OnChangeLocation();
    }

    public void EnterHouse()
    {
        if (player.HaveProperty("House", cityIndex: world.CityIndex))
        {
            lastAction = "You enter your house.";
            world.sublocation = 2;
        }
        else
        {
            lastAction = "You enter your mansion.";
            world.sublocation = 3;
        }
        AddTime(minutes: 5);
        OnChangeLocation();
    }

    public void ExitToCity()
    {
        if (world.sublocation == 1 && player.energy < 5)
        {
            lastAction = "You are too tired to travel.";
            UpdateText();
            return;
        }

        lastAction = $"You exit to the {(world.RealLocation == TileType.Village ? "village" : "city")}.";
        if (world.sublocation == 1)
        {
            player.energy -= 5;
            AddTime(minutes: 30);
        }
        else
            AddTime(minutes: 5);
        world.sublocation = 0;
        OnChangeLocation();
    }

    public void GoUp()
    {
        --world.level;
        lastAction = $"You go upstairs to level {world.level + 1}.";
        AddTime(minutes: 30);
        UpdateText();
        UpdateButtons();
    }

    public void GoDown()
    {
        ++world.level;
        lastAction = $"You go downstairs to level {world.level + 1}.";
        AddTime(minutes: 30);
        UpdateText();
        UpdateButtons();
    }

    private void OnEnterLocation()
    {
        Tile tile = world.CurrentTile;
        if (tile.type == TileType.City && dragonStatus == DragonStatus.Defeated)
        {
            dragonStatus = DragonStatus.Win;
            lastAction = "You return to the city as a hero. The Adventurer’s Guild erupts in cheers, mugs raised high in your honor. " +
                "Songs of your victory begin to spread, and your name will not be forgotten.";
        }
        else
            lastAction = $"You travel to the {tile.Name}.";
        if (tile.boss)
            lastAction += " There are <b>dragon engravings</b> near entrance.";
        else if (tile.mine && tile.type == TileType.Cave)
            lastAction += $" There are <b>{(tile.difficulty == 2 ? "silver" : "gold")} veins</b> inside this cave.";
        Property property = player.properties.FirstOrDefault(x => x.status == Property.Status.Building && x.locationIndex == world.CurrentLocationIndex);
        if (property != null)
            lastAction += $" {property.name} is being build here.";
        if ((tile.type == TileType.City || tile.type == TileType.Village) && allies.Any(x => (x.affection <= -25 && !x.complained) || x.affection <= -50))
        {
            Hero[] complainers = allies.Where(x => x.affection <= -25 && !x.complained).ToArray();
            Hero[] quitters = allies.Where(x => x.affection <= -50 && x.complained).ToArray();
            if (complainers.Length > 0)
            {
                foreach (Hero hero in complainers)
                    hero.complained = true;
                lastAction += $" {Utility.PrettyList(complainers.Select(x => x.name))} <b>{Utility.S("complain", complainers.Length == 1)}</b> about your lidership.";
            }
            if (quitters.Length > 0)
            {
                foreach (Hero hero in quitters)
                    allies.Remove(hero);
                CancelOutDebts();
                lastAction += $" {Utility.PrettyList(quitters.Select(x => x.name))} <color=red>{Utility.S("leave", quitters.Length == 1)}</color> your party.";
            }
        }
        OnChangeLocation();
    }

    private void OnChangeLocation()
    {
        CheckBoredAllies();

        Tile tile = world.CurrentTile;

        if ((tile.type == TileType.City || tile.type == TileType.Village) && player.HaveProperty("Horses") && player.HavePropertyUpgrade("Mansion", "Stables", world.CityIndex))
            freshHorses = 10;

        if (tile.type.IsSafe())
        {
            if (player.goldWaiting != 0)
            {
                lastAction += player.goldWaiting > 0
                    ? $" You receive <color=#FFD700>{player.goldWaiting}</color> gold from your properties."
                    : $" You pay <color=#FFD700>{-player.goldWaiting}</color> gold for your properties.";
                player.AddGold(player.goldWaiting);
                player.goldWaiting = 0;
            }

            foreach (Hero ally in allies)
                ally.BuyItems();

            foreach (Notification notification in notifications.Where(x => x.status == Notification.Status.Waiting))
                notification.status = Notification.Status.Available;
        }
        else if (tile.type == TileType.MageTower)
        {
            foreach (Hero ally in allies)
                ally.EnchantItems();
        }

        ui.UpdateBackground((int)tile.type);
        UpdateButtons();
        UpdateText();
        traveled = false;
    }

    public void Shop()
    {
        activeInventory = shopScreen;
        RefreshShopItems();
        RefreshPlayerItems();
        ui.ShowDialog(shopScreen);
    }

    public void Character()
    {
        activeInventory = characterScreen;
        RefreshPlayerScreen();
        ui.ShowDialog(characterScreen);
    }

    public void Ally(int index)
    {
        activeAlly = allies[index];
        RefreshAllyScreen();
        ui.ShowDialog(allyScreen);
    }

    private void RefreshShopItems()
    {
        Transform content = shopScreen.transform.Find("ShopItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);
        Item[] availableItems = (world.Location == TileType.City ? Item.cityItems : Item.villageItems);
        foreach (Item item in availableItems)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(item.ToString(Price.Buy), "Buy", () =>
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    ui.ShowInput($"How many {Utility.Plural(item.name)} to buy for {item.value} gold each?", count =>
                    {
                        if (count <= 0)
                            return true;
                        int price = count * item.value;
                        if (player.gold >= price)
                        {
                            player.AddItem(item, count);
                            player.AddGold(-price);
                            RefreshPlayerItems();
                            UpdateText();
                            return true;
                        }
                        else
                        {
                            ui.ShowDialog($"You need {price} gold to buy {Utility.Plural(item.name, count)}.");
                            return false;
                        }
                    });
                }
                else if (player.gold >= item.value)
                {
                    player.AddItem(item);
                    player.AddGold(-item.value);
                    RefreshPlayerItems();
                    UpdateText();
                }
                else
                    ui.ShowDialog($"You need {item.value} gold to buy {item.name}.");
            });
            itemEntry.SetImage(ui.itemIcons[(int)item.GetIcon()]);
        }
    }

    private void RefreshPlayerItems()
    {
        Transform content = activeInventory.transform.Find("PlayerItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (player.weapon != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeInventory == characterScreen)
            {
                itemEntry.Init(player.weapon.ToString(Price.None), "Unequip", () =>
                {
                    player.AddItem(player.weapon);
                    player.weapon = null;
                    RefreshPlayerScreen();
                });
            }
            else if (activeInventory == enchantItemsScreen && player.weapon.level < Item.MaxLevelEnchant)
            {
                itemEntry.Init(player.weapon.ToString(Price.Enchant), "Enchant", () =>
                {
                    int cost = player.weapon.GetEnchantCost();
                    if (player.gold < cost)
                        ui.ShowDialog($"You need {cost} gold to enchant {player.weapon.name}.");
                    else
                    {
                        player.weapon = player.weapon.GetEnchanted();
                        player.AddGold(-cost);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                });
            }
            else
                itemEntry.Init(player.weapon.ToString(activeInventory == shopScreen ? Price.Sell : Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)player.weapon.GetIcon()]);
        }

        if (player.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeInventory == characterScreen)
            {
                itemEntry.Init(player.armor.ToString(Price.None), "Unequip", () =>
                {
                    player.AddItem(player.armor);
                    player.armor = null;
                    RefreshPlayerScreen();
                });
            }
            else if (activeInventory == enchantItemsScreen && player.armor.level < Item.MaxLevelEnchant)
            {
                itemEntry.Init(player.armor.ToString(Price.Enchant), "Enchant", () =>
                {
                    int cost = player.armor.GetEnchantCost();
                    if (player.gold < cost)
                        ui.ShowDialog($"You need {cost} gold to enchant {player.armor.name}.");
                    else
                    {
                        player.armor = player.armor.GetEnchanted();
                        player.AddGold(-cost);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                });
            }
            else
                itemEntry.Init(player.armor.ToString(activeInventory == shopScreen ? Price.Sell : Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)player.armor.GetIcon()]);
        }

        if (player.shield != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeInventory == characterScreen)
            {
                itemEntry.Init(player.shield.ToString(Price.None), "Unequip", () =>
                {
                    player.AddItem(player.shield);
                    player.shield = null;
                    RefreshPlayerScreen();
                });
            }
            else if (activeInventory == enchantItemsScreen && player.shield.level < Item.MaxLevelEnchant)
            {
                itemEntry.Init(player.shield.ToString(Price.Enchant), "Enchant", () =>
                {
                    int cost = player.shield.GetEnchantCost();
                    if (player.gold < cost)
                        ui.ShowDialog($"You need {cost} gold to enchant {player.shield.name}.");
                    else
                    {
                        player.shield = player.shield.GetEnchanted();
                        player.AddGold(-cost);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                });
            }
            else
                itemEntry.Init(player.shield.ToString(activeInventory == shopScreen ? Price.Sell : Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)player.shield.GetIcon()]);
        }

        if ((player.weapon != null || player.armor != null || player.shield != null) && player.items.Count > 0)
            Instantiate(ui.lineSeparatorPrefab, content);

        foreach (ItemSlot itemSlot in player.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeInventory == characterScreen)
            {
                void Drop()
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        player.RemoveItem(itemSlot, itemSlot.count);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to drop away?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            player.RemoveItem(itemSlot, count);
                            RefreshPlayerItems();
                            UpdateText();
                            return true;
                        });
                    }
                    else
                    {
                        player.RemoveItem(itemSlot);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                }

                if (player.CanEquip(itemSlot.item))
                {
                    itemEntry.Init2(itemSlot.ToString(Price.None), "Equip", () =>
                    {
                        if (itemSlot.team)
                            PayForTeamItem(player, itemSlot.item);

                        switch (itemSlot.item.type)
                        {
                        case Item.Type.Weapon:
                            if (player.weapon != null)
                                player.AddItem(player.weapon);
                            player.weapon = itemSlot.item;
                            break;
                        case Item.Type.Armor:
                            if (player.armor != null)
                                player.AddItem(player.armor);
                            player.armor = itemSlot.item;
                            break;
                        case Item.Type.Shield:
                            if (player.shield != null)
                                player.AddItem(player.shield);
                            player.shield = itemSlot.item;
                            break;
                        }
                        player.RemoveItem(itemSlot);
                        RefreshPlayerScreen();
                        UpdateText();
                    }, "Drop", Drop);
                }
                else if (itemSlot.item.type == Item.Type.Usable)
                {
                    itemEntry.Init2(itemSlot.ToString(Price.None), "Use", () =>
                    {
                        player.hp = Mathf.Min(player.hp + itemSlot.item.power, player.hpMax);
                        player.RemoveItem(itemSlot);
                        RefreshPlayerScreen();
                        UpdateText();
                    }, "Drop", Drop);
                }
                else if (itemSlot.item.type == Item.Type.Tool && itemSlot.item.name == "alchemy set")
                    itemEntry.Init2(itemSlot.ToString(Price.None), "Use", Craft, "Drop", Drop);
                else
                    itemEntry.Init2(itemSlot.ToString(Price.None), null, null, "Drop", Drop);
            }
            else if (activeInventory == shopScreen)
            {
                itemEntry.Init(itemSlot.ToString(Price.Sell), "Sell", () =>
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        if (itemSlot.team)
                            AddTeamGold(itemSlot.item.value * itemSlot.count / 2);
                        else
                            player.AddGold(itemSlot.item.value * itemSlot.count / 2);
                        player.RemoveItem(itemSlot, itemSlot.count);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to sell for {itemSlot.item.value / 2} gold each?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            if (itemSlot.team)
                                AddTeamGold(itemSlot.item.value * count / 2);
                            else
                                player.AddGold(itemSlot.item.value * count / 2);
                            player.RemoveItem(itemSlot, count);
                            RefreshPlayerItems();
                            UpdateText();
                            return true;
                        });
                    }
                    else
                    {
                        if (itemSlot.team)
                            AddTeamGold(itemSlot.item.value / 2);
                        else
                            player.AddGold(itemSlot.item.value / 2);
                        player.RemoveItem(itemSlot);
                        RefreshPlayerItems();
                        UpdateText();
                    }
                });
            }
            else if (activeInventory == giveAllyItemsScreen)
            {
                if (activeAlly.WillTakeItem(itemSlot.item))
                {
                    itemEntry.Init(itemSlot.ToString(Price.None), "Give", () =>
                    {
                        if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor || itemSlot.item.type == Item.Type.Shield
                            || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl)))
                        {
                            if (itemSlot.team)
                                PayForTeamItem(activeAlly, itemSlot.item);
                            else
                                IncreaseAffectionFromValue(activeAlly, itemSlot.item, 1);
                            activeAlly.GiveItem(itemSlot.item);
                            player.RemoveItem(itemSlot);
                            RefreshPlayerItems();
                            RefreshAllyScreen();
                            UpdateText();
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            if (itemSlot.team)
                                PayForTeamItem(activeAlly, itemSlot.item, itemSlot.count);
                            else
                                IncreaseAffectionFromValue(activeAlly, itemSlot.item, itemSlot.count);
                            activeAlly.GiveItem(itemSlot.item, itemSlot.count);
                            player.RemoveItem(itemSlot, itemSlot.count);
                            RefreshPlayerItems();
                            RefreshAllyScreen();
                            UpdateText();
                        }
                        else
                        {
                            ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} give to {activeAlly.name}?", count =>
                            {
                                if (count <= 0)
                                    return true;
                                count = Mathf.Min(count, itemSlot.count);
                                if (itemSlot.team)
                                    PayForTeamItem(activeAlly, itemSlot.item, count);
                                else
                                    IncreaseAffectionFromValue(activeAlly, itemSlot.item, count);
                                activeAlly.GiveItem(itemSlot.item, count);
                                player.RemoveItem(itemSlot, count);
                                RefreshPlayerItems();
                                RefreshAllyScreen();
                                UpdateText();
                                return true;
                            });
                        }
                    });
                }
                else
                    itemEntry.Init(itemSlot.ToString(Price.None));
            }
            else if (activeInventory == storeItemsScreen)
            {
                itemEntry.Init(itemSlot.ToString(Price.None), "Store", () =>
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        if (itemSlot.team)
                            PayForTeamItem(player, itemSlot.item, itemSlot.count);
                        AddStoredItem(itemSlot.item, itemSlot.count);
                        player.RemoveItem(itemSlot, itemSlot.count);
                        RefreshPlayerItems();
                        RefreshStoredItems();
                        UpdateText();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to store?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            if (itemSlot.team)
                                PayForTeamItem(player, itemSlot.item, count);
                            AddStoredItem(itemSlot.item, count);
                            player.RemoveItem(itemSlot, count);
                            RefreshPlayerItems();
                            RefreshStoredItems();
                            UpdateText();
                            return true;
                        });
                    }
                    else
                    {
                        if (itemSlot.team)
                            PayForTeamItem(player, itemSlot.item);
                        AddStoredItem(itemSlot.item);
                        player.RemoveItem(itemSlot);
                        RefreshPlayerItems();
                        RefreshStoredItems();
                        UpdateText();
                    }
                });
            }
            else if (activeInventory == enchantItemsScreen)
            {
                if (itemSlot.item.CanEnchant())
                {
                    itemEntry.Init(itemSlot.ToString(Price.Enchant), "Enchant", () =>
                    {
                        int cost = itemSlot.item.GetEnchantCost();
                        if (player.gold < cost)
                            ui.ShowDialog($"You need {cost} gold to enchant {itemSlot.item.name}.");
                        else
                        {
                            Item item = itemSlot.item;
                            if (itemSlot.team)
                                PayForTeamItem(player, itemSlot.item);
                            player.RemoveItem(itemSlot);
                            player.AddItem(item.GetEnchanted());
                            player.AddGold(-cost);
                            RefreshPlayerItems();
                            UpdateText();
                        }
                    });
                }
                else
                    itemEntry.Init(itemSlot.ToString(Price.None));
            }
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }

    private void RefreshPlayerScreen()
    {
        TMP_Text charText = characterScreen.transform.Find("Text").GetComponent<TMP_Text>();
        sb.Clear();
        sb.Append($"{player.GenderSign}{player.name}\n" +
            $"Level: {player.level} {player.clas.AsString()} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}\n" +
            $"Health: {player.hp}/{player.hpMax}\n");
        if (player.owedGold > 0)
            sb.Append($"Owed gold: {player.owedGold}\n");
        if (player.skills.Count > 0)
        {
            sb.Append("Skills:\n");
            foreach (var skill in player.skills.Select(kvp => (name: kvp.Key.AsString().ToUpper1(), kvp.Value.level)).OrderBy(x => x.name))
                sb.Append($"  {skill.name}: {skill.level}\n");
        }
        if (player.rested > 0 || freshHorses > 0)
        {
            sb.Append("Effects:\n");
            if (player.rested > 0)
                sb.Append($"  Well rested ({Utility.Plural("day", player.rested, true)})\n");
            if (freshHorses > 0)
                sb.Append($"  Fresh horses ({Utility.Plural("day", freshHorses, true)})\n");
        }
        charText.text = sb.ToString();

        RefreshPlayerItems();
    }

    private void RefreshAllyScreen()
    {
        TMP_Text charText = allyScreen.transform.Find("Text").GetComponent<TMP_Text>();
        sb.Clear();
        sb.Append($"{activeAlly.GenderSign}{activeAlly.name}\n" +
            $"Level: {activeAlly.level} {activeAlly.clas.AsString()} ({activeAlly.ExpP}%)\n" +
            $"Attack: {activeAlly.Attack}\n" +
            $"Defense: {activeAlly.Defense}\n" +
            $"Health: {activeAlly.hp}/{activeAlly.hpMax}\n" +
            $"Gold: {activeAlly.gold}");
        if (activeAlly.owedGold > 0)
            sb.Append($" (owes {activeAlly.owedGold} gold)\n");
        else
            sb.Append('\n');
        sb.Append($"Affection: {activeAlly.affection}\n");
        if (activeAlly.skills.Count > 0)
        {
            sb.Append("Skills:\n");
            foreach (var skill in activeAlly.skills.Select(kvp => (name: kvp.Key.AsString().ToUpper1(), kvp.Value.level)).OrderBy(x => x.name))
                sb.Append($"  {skill.name}: {skill.level}\n");
        }
        if (activeAlly.rested > 0)
            sb.Append($"Effects:\n  Well rested ({Utility.Plural("day", activeAlly.rested, true)})");
        charText.text = sb.ToString();

        RefreshAllyItems(allyScreen);
        if (activeInventory == giveAllyItemsScreen)
            RefreshAllyItems(giveAllyItemsScreen);
    }

    private void RefreshAllyItems(GameObject dialog)
    {
        Transform content = dialog.transform.Find("AllyItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (activeAlly.weapon != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(activeAlly.weapon.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)activeAlly.weapon.GetIcon()]);
        }

        if (activeAlly.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(activeAlly.armor.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)activeAlly.armor.GetIcon()]);
        }

        if (activeAlly.shield != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(activeAlly.shield.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)activeAlly.shield.GetIcon()]);
        }

        if ((activeAlly.weapon != null || activeAlly.armor != null || activeAlly.shield != null) && activeAlly.items.Count > 0)
            Instantiate(ui.lineSeparatorPrefab, content);

        foreach (ItemSlot itemSlot in activeAlly.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }

    private void UpdateText()
    {
        sb.Clear();
        string name = world.CurrentTile.Name.ToUpper1();
        if (world.level != 0)
            name += $", level {world.level + 1}";
        sb.Append($"{name}   Day: {day} {hour}:{minute:00}   HP: {player.hp}/{player.hpMax}   Energy: {player.energy}/100   Gold: {player.gold}");
        if (player.goldReceived != 0)
        {
            sb.Append($"({player.goldReceived:+0;-0})");
            player.goldReceived = 0;
        }
        Quest activeQuest = activeQuests.FirstOrDefault(x => x.tracked);
        if (activeQuest != null)
            sb.Append($"\nQuest: {activeQuest.Text}\n");
        if (!string.IsNullOrEmpty(lastAction))
        {
            sb.Append('\n');
            sb.Append(lastAction);
        }
        lastAction = null;
        text.text = sb.ToString();

        // allies health
        for (int i = 0; i < allies.Count; ++i)
        {
            float hp = allies[i].hpp;
            if (hp < 0)
                hp = 0;
            else if (hp > 0 && hp < 0.01f)
                hp = 0.01f;
            alliesHealthRect[i].sizeDelta = new Vector2(156f * hp, 5f);
        }
    }

    private void AddTime(int hours = 0, int minutes = 0)
    {
        minute += minutes;
        if (minute >= 60)
        {
            hour += minute / 60;
            minute %= 60;
        }
        hour += hours;
        if (hour >= 24)
        {
            lastAction += " It's a new day. ";
            OnRest();
        }
    }

    private bool OnRest()
    {
        void FullRest()
        {
            player.hp = player.hpMax;
            player.energy = 100;
            foreach (Hero ally in allies)
                ally.hp = ally.hpMax;
        }

        TileType location = world.Location;
        int cityIndex = world.CityIndex;
        if (((location == TileType.City || location == TileType.Village) && player.HaveProperty("House", cityIndex: cityIndex)) || location == TileType.House)
        {
            FullRest();
            lastAction += "You rest in your house.";
        }
        else if (((location == TileType.City || location == TileType.Village) && player.HaveProperty("Mansion", cityIndex: cityIndex)) || location == TileType.Mansion)
        {
            FullRest();
            foreach (Hero hero in Team)
                hero.rested = 11;
            if (player.HaveProperty("Horses") && player.HavePropertyUpgrade("Mansion", "Stables", cityIndex: cityIndex))
                freshHorses = 11;
            lastAction += "You rest in your mansion.";
        }
        else if ((location == TileType.City || location == TileType.Village) && player.HaveProperty("Inn", cityIndex: cityIndex))
        {
            FullRest();
            lastAction += "You rest in your inn.";
        }
        else if ((location == TileType.City || location == TileType.Village) && player.gold > 0)
        {
            FullRest();
            player.AddGold(-1);
            foreach (Hero ally in allies)
                ally.AddGold(-1);
            lastAction += "You rest in an inn (<color=#FFD700>-1</color> gold).";
        }
        else if (location == TileType.Sawmill || location == TileType.Mine || location == TileType.Farm)
        {
            FullRest();
            lastAction += "You rest in a barracks.";
        }
        else if (location == TileType.MageTower)
        {
            FullRest();
            lastAction += "You rest in a guest room.";
        }
        else
        {
            string where;
            int energy;
            if (player.HaveItem("tent"))
            {
                where = "in a tent";
                energy = 75;
            }
            else
            {
                if (location == TileType.City || location == TileType.Village)
                    where = "on a street";
                else if (location == TileType.Plains || location == TileType.Forest)
                    where = "on a grass";
                else
                    where = "on a ground";
                energy = 50;
            }

            Item rations = Item.Get("rations");
            int count = 1 + allies.Count;
            int eaten = RemoveTeamItem(rations, count);
            float heal;
            if (eaten > 0)
            {
                if (eaten == count)
                {
                    energy += 25;
                    heal = 1;
                }
                else
                {
                    float ratio = (float)eaten / count;
                    energy += (int)(ratio * 25);
                    heal = ratio;
                }
                lastAction += $"You rest {where} and eat rations.";
            }
            else
            {
                lastAction += $"You rest {where}.";
                heal = 0;
            }

            bool attacked = false;
            if (!world.CurrentTile.clear)
            {
                int attackChance = location switch
                {
                    TileType.Forest or TileType.Mountains or TileType.Swamp or TileType.Sewers => 5,
                    TileType.Cave or TileType.Dungeon => 10,
                    TileType.DarkDimension => 20,
                    _ => 0,
                };
                attacked = Utility.Rand % 100 < attackChance;
#if UNITY_EDITOR
                if (attackChance > 0 && Input.GetKey(KeyCode.Alpha9))
                    attacked = true;
#endif
            }

            if (attacked)
            {
                hour += Utility.Random(3, 5);
                minute = 0;
                if (hour >= 24)
                {
                    ++day;
                    hour -= 24;
                }
                heal /= 2;
                energy /= 2;
                if (heal != 0)
                {
                    player.hp = Mathf.Min(player.hp + (int)(heal * player.hpMax), player.hpMax);
                    foreach (Hero ally in allies)
                        ally.hp = Mathf.Min(ally.hp + (int)(heal * ally.hpMax), ally.hpMax);
                }
                player.energy = Mathf.Min(player.energy + energy, 100);
                Tile tile = world.CurrentTile;
                Enemy enemy = Enemy.GetRandom(tile.type, tile.difficulty);
                restCombat = true;
                restCombatHeal = heal;
                restCombatEnergy = energy;
                StartCombat(enemy, null);
                return false;
            }
            else
            {
                if (heal == 1)
                {
                    player.hp = player.hpMax;
                    foreach (Hero ally in allies)
                        ally.hp = ally.hpMax;
                }
                else if (heal > 0)
                {
                    player.hp = Mathf.Min(player.hp + (int)(heal * player.hpMax), player.hpMax);
                    foreach (Hero ally in allies)
                        ally.hp = Mathf.Min(ally.hp + (int)(heal * ally.hpMax), ally.hpMax);
                }
                player.energy = Mathf.Min(player.energy + energy, 100);
            }
        }

        ++day;
        hour = 8;
        minute = 0;

        OnNewDay();
        CheckBoredAllies();

        if (player.goldWaiting != 0 && location.IsSafe())
        {
            lastAction += player.goldWaiting > 0
                ? $" You receive <color=#FFD700>{player.goldWaiting}</color> gold from your properties."
                : $" You pay <color=#FFD700>{-player.goldWaiting}</color> gold for your properties.";
            player.AddGold(player.goldWaiting);
            player.goldWaiting = 0;
        }

        ui.CloseDialogs(x => x == propertiesScreen || x == guildScreen || x == characterScreen || x == craftScreen);
        return true;
    }

    private void CheckBoredAllies()
    {
        List<(Hero ally, int count)> changes = null;
        foreach (Hero ally in allies)
        {
            if (ally.bored >= 30)
            {
                int count = ally.bored / 30;
                ally.bored -= count * 30;
                ally.affection -= count;
                changes ??= new();
                changes.Add((ally, count));
            }
        }

        if (changes != null)
        {
            foreach (var group in changes.GroupBy(x => x.count))
                lastAction += $" {Utility.PrettyList(group.Select(x => x.ally.name))} {(group.Count() == 1 ? "is" : "are")} bored (-{group.Key} affection).";
        }
    }

    public void OnNewDay()
    {
        player.goldWaiting += player.properties
            .Where(p => p.status == Property.Status.Active)
            .Sum(p =>
            {
                if (p.events.Count != 0)
                {
                    Property.Event even = p.events[0];
                    if (even.name == "Infested")
                    {
                        if (even.timer != -1)
                        {
                            --even.timer;
                            if (even.timer == 0)
                            {
                                world.GetLocation(p.locationIndex).clear = true;
                                AddNotification($"{p.Name} infestation has been cleared by {(even.state == 0 ? "adventurers" : "guards")}.");
                                p.events.Clear();
                            }
                        }
                        return -p.upkeep / 2;
                    }
                    else
                    {
                        --even.timer;
                        if (even.timer == 0)
                            p.events.Clear();
                        return p.income * 3 / 2 - p.upkeep;
                    }
                }
                else
                    return p.income - p.upkeep;
            });

        foreach (Property property in player.properties.Where(x => x.status == Property.Status.Building))
        {
            --property.buildTime;
            if (property.buildTime == 0)
            {
                property.status = Property.Status.Active;
                Tile tile = world.GetLocation(property.locationIndex);
                if (property.name == "Farm")
                {
                    tile.SetType(TileType.Farm);
                    tile.name = world.GetCityTile(2).name.Split(' ')[0] + " farm";
                    tile.difficulty = 3;
                    tile.clear = true;
                }
                else
                    tile.SetType(TileType.Mine);
                map.UpdateMap(World.IndexToPoint(property.locationIndex));
                if (world.CurrentLocationIndex == property.locationIndex)
                {
                    ui.UpdateBackground((int)tile.type);
                    UpdateButtons();
                    lastAction += $" {property.name} has been built.";
                }
                AddNotification($"The construction of {property.Name.ToLower()} has been completed.");
            }
        }

        UpdateQuests();

        // grow garden plants
        foreach (Property property in player.properties.Where(x => x.gardenPlants != null && x.gardenPlants.Count > 0))
        {
            foreach (var plant in property.gardenPlants.GroupBy(x => x).Select(x => (name: x.Key, count: x.Count())))
            {
                switch (plant.name)
                {
                case "Vegetables":
                    AddStoredItem(Item.Get("rations"), plant.count, property.storedItems);
                    break;
                case "Herbs":
                    AddStoredItem(Item.Get("herb"), plant.count, property.storedItems);
                    break;
                case "Rare herbs":
                    AddStoredItem(Item.Get("rare herb"), plant.count, property.storedItems);
                    break;
                }
            }
        }

        // update heroes
        foreach (Hero hero in Team)
        {
            if (hero.rested > 0)
                --hero.rested;
            ++hero.bored;
            hero.winToday = false;
            hero.lastGift = 0;
        }

        if (allies.Count == 0)
            player.affection = 0;
        else
            player.affection = allies.Max(x => x.affection);

        if (freshHorses > 0)
            --freshHorses;

        world.Update();

        // property events
        if (day % 10 == 0)
        {
            foreach (Property property in player.properties.Where(x => x.income > 0 && x.status == Property.Status.Active))
            {
                if (property.events != null && property.events.Count > 0)
                    continue;

                int c = Utility.Rand % 20;
                if (c < 2)
                {
                    // 10%
                    if (property.lastEvent != "Buff")
                    {
                        property.events.Add(new Property.Event { name = "Buff", timer = 30 });
                        property.lastEvent = "Buff";
                        string str;
                        string propName = property.Name.ToLower();
                        if (property.name == "Sawmill" || property.name == "Farm")
                            str = $"Your {propName} production increased thanks to good weather.";
                        else if (property.name == "Inn")
                            str = $"Your {propName} income increased thanks to festival.";
                        else if (Utility.Rand % 2 == 0)
                            str = $"Your {propName} production increased thanks to good ore quality.";
                        else
                            str = $"Your {propName} production increased thanks to new ore veins.";
                        AddNotification(str);
                        break;
                    }
                    else
                        property.lastEvent = null;
                }
                else if (c == 2 && property.locationIndex != -1 && !property.HaveUpgrade("Extra guards"))
                {
                    // 5%
                    if (property.lastEvent != "Infested")
                    {
                        property.events.Add(new Property.Event { name = "Infested", timer = -1 });
                        property.lastEvent = "Infested";
                        AddNotification($"{property.Name} has been taken over by monsters! Hire adventurers or deal with it yourself.");
                        Tile tile = world.GetLocation(property.locationIndex);
                        tile.clear = false;
                        tile.defeatedEnemies = 0;
                        break;
                    }
                    else
                        property.lastEvent = null;
                }
                else
                    property.lastEvent = null;
            }
        }
    }

    private void AddNotification(string str)
    {
        Notification notification = new() { text = str, day = day, status = Notification.Status.Waiting };
        notifications.Add(notification);
        if (!world.isTraveling && world.Location.IsSafe())
        {
            notification.status = Notification.Status.Available;
            UpdateButtons();
        }
    }

    public int CountTeamItem(Item item)
    {
        return Team.Sum(x => x.CountItem(item));
    }

    public int RemoveTeamGold(int count)
    {
        int removed = 0;

        while (count > 0)
        {
            Hero[] available = Team.Where(x => x.gold > 0).ToArray();
            if (available.Length == 0)
                break; // nothing left to remove

            int perHero = Mathf.Max(1, count / available.Length);
            foreach (Hero hero in available)
            {
                if (count <= 0)
                    break;

                int canRemove = Mathf.Min(perHero, hero.gold);
                hero.AddGold(-canRemove);
                count -= canRemove;
                removed += canRemove;
            }
        }

        return removed;
    }

    public int RemoveTeamItem(Item item, int count)
    {
        int removed = 0;

        // Cache counts so we don't call CountItem repeatedly
        Dictionary<Hero, int> counts = new();
        foreach (Hero hero in Team)
            counts[hero] = hero.CountItem(item);

        while (count > 0)
        {
            // Heroes that still have items
            var available = counts
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key)
                .ToList();

            if (available.Count == 0)
                break; // nothing left to remove

            int perHero = Mathf.Max(1, count / available.Count);
            foreach (var hero in available)
            {
                if (count <= 0)
                    break;

                int canRemove = Mathf.Min(perHero, counts[hero]);
                hero.RemoveItem(item, canRemove);
                counts[hero] -= canRemove;
                count -= canRemove;
                removed += canRemove;
            }
        }

        return removed;
    }

    private void NewGame()
    {
        Global global = Global.Instance;
        player = new() { name = global.playerName, clas = global.playerClass, female = global.playerFemale };
        player.Init();
        allies = new();
        world = new();
        world.Init();
        map.Build();
        day = 1;
        hour = 8;
        activeQuests = new()
        {
            new()
            {
                type = Quest.Type.Unique
            }
        };
        availableQuests = new();
        notifications = new();
        properties = new();
        foreach (Property property in Property.properties)
        {
            Property copy = property.Copy();
            if (property.locationIndexFunc != null)
            {
                copy.locationIndex = property.locationIndexFunc(world);
#if UNITY_EDITOR
                if (copy.locationIndex == -1)
                    Debug.LogWarning($"Failed to find location index for '{property.Name}'.");
#endif
            }
            else
                copy.locationIndex = -1;
            properties.Add(copy);
        }
        GenerateInitialQuests();
        lastAction = "You are an adventurer seeking glory and gold. Rumors speak of a dragon lurking deep within a forgotten cave beyond the wilds. " +
            "Find its lair, face the beast, and carve your name into legend.";
    }

    private void SaveGame()
    {
        string json = JsonUtility.ToJson(this);
        Global.Instance.SaveGame($"{player.name}, Day {day}, Level {player.level} {player.clas.AsString()}, Gold {player.gold}", json);
    }

    private void LoadGame()
    {
        Global global = Global.Instance;
        global.loadGame = false;
        string json = global.GetSaveData();
        JsonUtility.FromJsonOverwrite(json, this);
        map.Build();
        ui.UpdateBackground((int)world.Location);
    }

    public void ExitToMenu()
    {
        SaveGame();
        SceneManager.LoadScene("Menu");
    }

    private void UpdateButtons()
    {
        TileType location = world.Location;
        int cityIndex = world.CityIndex;
        bool inCity = location == TileType.City;
        bool inVillage = location == TileType.Village;
        Transform buttons = transform.Find("Buttons");
        buttons.Find("BtShop").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtWork").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtGuild").gameObject.SetActive(inCity);
        buttons.Find("BtProperties").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtSewers").gameObject.SetActive(inCity);
        Transform button = buttons.Find("BtHouse");
        if ((inCity || inVillage) && player.HaveProperty("Mansion", cityIndex: cityIndex))
        {
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = "Mansion";
        }
        else if ((inCity || inVillage) && player.HaveProperty("House", cityIndex: cityIndex))
        {
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = "House";
        }
        else
            button.gameObject.SetActive(false);
        buttons.Find("BtTravel").gameObject.SetActive(world.level == 0 && location != TileType.DarkDimension);
        buttons.Find("BtGoUp").gameObject.SetActive(world.level != 0);
        buttons.Find("BtGoDown").gameObject.SetActive(world.level < world.CurrentTile.foundLevel);
        buttons.Find("BtEnchantItems").gameObject.SetActive(location == TileType.MageTower);
        buttons.Find("BtEnterPortal").gameObject.SetActive(location == TileType.MageTower);
        buttons.Find("BtEnterPortal2").gameObject.SetActive(location == TileType.DarkDimension);

        button = buttons.Find("BtJournal");
        int notificationsAvailable = notifications.Count(x => x.status == Notification.Status.Available);
        if (notificationsAvailable > 0)
        {
            button.Find("Text1").gameObject.SetActive(false);
            button.Find("Text2").gameObject.SetActive(true);
            button.Find("Image").gameObject.SetActive(true);
            TMP_Text counter = button.Find("Counter").GetComponent<TMP_Text>();
            counter.text = notificationsAvailable.ToString();
            counter.gameObject.SetActive(true);
            button.Find("").gameObject.SetActive(true);
        }
        else
        {
            button.Find("Text1").gameObject.SetActive(true);
            button.Find("Text2").gameObject.SetActive(false);
            button.Find("Image").gameObject.SetActive(false);
            button.Find("Counter").gameObject.SetActive(false);
        }

        button = buttons.Find("BtForage");
        if (location == TileType.Forest || location == TileType.Cave)
        {
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = location == TileType.Forest ? "Forage" : "Prospect";
        }
        else
            button.gameObject.SetActive(false);

        button = buttons.Find("BtCity");
        if (location == TileType.Sewers || location == TileType.House || location == TileType.Mansion)
        {
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = world.RealLocation == TileType.Village ? "Exit to village" : "Exit to city";
        }
        else
            button.gameObject.SetActive(false);

        buttons.Find("BtWork2").gameObject.SetActive(location == TileType.Sawmill || location == TileType.Mine || location == TileType.Farm);

        buttons.Find("BtStorage").gameObject.SetActive(location == TileType.House || location == TileType.Mansion);
        buttons.Find("BtCook").gameObject.SetActive(location == TileType.House || location == TileType.Mansion);
        buttons.Find("BtCraft").gameObject.SetActive((location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab", cityIndex: cityIndex))
            || (location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Alchemy lab", cityIndex: cityIndex)));
        buttons.Find("BtGarden").gameObject.SetActive((location == TileType.House && player.HavePropertyUpgrade("House", "Garden", cityIndex: cityIndex))
            || (location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Garden", cityIndex: cityIndex)));

        GameObject btAlly = buttons.Find("BtAlly").gameObject;
        if (allies.Count < 1)
            btAlly.SetActive(false);
        else
        {
            btAlly.GetComponentInChildren<TMP_Text>().text = allies[0].name;
            btAlly.SetActive(true);
        }
        btAlly = buttons.Find("BtAlly2").gameObject;
        if (allies.Count < 2)
            btAlly.SetActive(false);
        else
        {
            btAlly.GetComponentInChildren<TMP_Text>().text = allies[1].name;
            btAlly.SetActive(true);
        }
    }

    public void Recruit()
    {
        if (allies.Count + 1 >= MaxTeamSize)
        {
            ui.ShowDialog("Your team is full.");
            return;
        }

        int level = Mathf.Max(Utility.Random(-3, 1) + guildRank, 0);
        Hero hero = SpawnHero(level);
        // Novice(0) -> Apprentice(2) -> Journeyman(4) -> Adept(8) -> Expert(12) -> Master(16) -> Grandmaster(20)
        string levelName = (level / 2) switch
        {
            1 => "apprentice",
            2 => "journeyman",
            _ => "novice",
        };

        string skill;
        if (hero.skills.Count > 0)
            skill = $" and knows {hero.skills.First().Key.AsString()}";
        else
            skill = string.Empty;

        ui.ShowConfirm($"You meet <b>{hero.name}</b> and talk with {hero.him} about adventurers. " +
            $"{hero.He} is {Utility.A(levelName)} <b>{levelName} {hero.clas.AsString()}</b>{skill}. Do you want to recruit {hero.him}?", yes =>
        {
            int chance = 100 + (player.level - hero.level) * 5;
            if (Utility.Rand % 100 < chance)
            {
                if (yes)
                {
                    lastAction = $"You recruit {hero.name} to your team.";
                    allies.Add(hero);
                    hero.BuyItems();
                    UpdateButtons();
                }
            }
            else
                lastAction = $"You <b>failed</b> to convince {hero.name} to join your team.";

            AddTime(minutes: 30);
            if (ui.TopDialog == guildScreen)
                RefreshGuild();
            UpdateText();
        });
    }

    private Hero SpawnHero(int level = 0)
    {
        Hero hero = new();
        hero.Init(level);
        while (true)
        {
            if (!Team.Any(x => x.name == hero.name))
                break;
            hero.name = (hero.female ? Names.femaleNames : Names.maleNames).RandomItem();
        }
        return hero;
    }

    public void RemoveAlly()
    {
        if (!world.Location.IsSafe())
        {
            ui.ShowDialog("You can only remove your allies in city or village.");
            return;
        }

        ui.ShowConfirm($"Are you sure you want to remove {activeAlly.name} from your team?", () =>
        {
            lastAction = $"{activeAlly.name} is sad and leave.";
            allies.Remove(activeAlly);
            CancelOutDebts();
            UpdateButtons();
            UpdateText();
            ui.CloseDialog();
        });
    }

    public void GiveAllyItems()
    {
        activeInventory = giveAllyItemsScreen;
        ui.ShowDialog(giveAllyItemsScreen);
        RefreshPlayerItems();
        RefreshAllyItems(giveAllyItemsScreen);
    }

    public void GiveAllyGold()
    {
        ui.ShowInput($"How much gold give to {activeAlly.name}?", count =>
        {
            count = Mathf.Min(count, player.gold);
            if (count <= 0)
                return true;
            player.AddGold(-count);
            activeAlly.AddGold(count);
            IncreaseAffectionFromValue(activeAlly, count);
            if (world.Location.IsSafe())
                activeAlly.BuyItems();
            else if (world.Location == TileType.MageTower)
                activeAlly.EnchantItems();
            RefreshAllyScreen();
            UpdateText();
            return true;
        });
    }

    public void ManageProperties()
    {
        selectedProperty = null;
        RefreshProperties();
        RefreshPropertyDetails();
        ui.ShowDialog(propertiesScreen);
    }

    private void RefreshProperties()
    {
        propertiesScreen.transform.Find("Text").GetComponent<TMP_Text>().text = lastAction ?? string.Empty;
        lastAction = null;

        ItemEntryList list = propertiesScreen.transform.Find("List").GetComponent<ItemEntryList>();
        list.Clear();
        Transform content = propertiesScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        int cityIndex = world.CityIndex;
        Property[] propertiesToBuy = properties.Where(x => x.status != Property.Status.None && (x.cityIndex == -1 || x.cityIndex == cityIndex))
            .OrderBy(x => x.value).ThenBy(x => x.Name).ToArray();

        // player properties
        if (player.properties.Count > 0)
        {
            ui.AddTextHeader("Your properties:", content);
            foreach (Property property in player.properties.OrderBy(x => x.value).ThenBy(x => x.Name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.SetImage(ui.propertyIcons[(int)property.GetImage()]);
                itemEntry.data = property;
                itemEntry.canSelect = true;
                if (property == selectedProperty)
                    list.Select(itemEntry);

                if (property.status == Property.Status.Building)
                    itemEntry.Init(property.ToString(Property.DescStatus.Building));
                else if (property.HaveEvent("Infested"))
                    itemEntry.Init(property.ToString(Property.DescStatus.Infested));
                else
                {
                    itemEntry.Init(property.ToString(Property.DescStatus.Sell), "Sell", () =>
                    {
                        properties.Add(property);
                        player.AddGold(property.value / 2);
                        player.properties.Remove(property);
                        property.events.Clear();
                        lastAction = $"You sell {property.Name.ToLower()} for <color=#FFD700>{property.value / 2}</color> gold.";
                        if (property.name == "House" || property.name == "Mansion")
                            UpdateButtons();
                        if (property.name == "Horses" || property.name == "Mansion")
                            freshHorses = 0;
                        AddTime(minutes: 30);
                        if (ui.CurrentDialog == propertiesScreen)
                        {
                            if (selectedProperty == property)
                            {
                                selectedProperty = null;
                                RefreshPropertyDetails();
                            }
                            RefreshProperties();
                        }
                        UpdateText();
                    });
                }
            }

            if (propertiesToBuy.Length > 0)
                Instantiate(ui.lineSeparatorPrefab, content);
        }

        // available properties
        if (propertiesToBuy.Length > 0)
            ui.AddTextHeader("Available properties:", content);
        foreach (Property property in propertiesToBuy)
        {
            bool build = property.status == Property.Status.Cleared;
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(property.ToString(build ? Property.DescStatus.Build : Property.DescStatus.Buy), build ? "Build" : "Buy", () =>
            {
                int cost = build ? property.BuildPrice : property.value;
                if (player.gold < cost)
                {
                    ui.ShowDialog($"You need {cost} gold to {(build ? "build" : "buy")} {property.Name.ToLower()}.");
                    return;
                }

                if ((property.name == "House" && player.HaveProperty("Mansion", cityIndex: cityIndex))
                    || (property.name == "Mansion" && player.HaveProperty("House", cityIndex: cityIndex)))
                {
                    ui.ShowDialog("You can't own both a house and a mansion. It's a law!");
                    return;
                }

                player.AddGold(-cost);
                player.properties.Add(property);
                properties.Remove(property);
                if (build)
                {
                    lastAction = $"You pay <color=#FFD700>{cost}</color> gold to build {property.Name.ToLower()}.";
                    property.status = Property.Status.Building;
                    world.GetLocation(property.locationIndex).timer = 0; // prevent resetting
                }
                else
                {
                    lastAction = $"You buy {property.Name.ToLower()} for <color=#FFD700>{cost}</color> gold.";

                    // remove quests assigned to this location
                    if (property.locationIndex != -1)
                    {
                        Quest quest = activeQuests.FirstOrDefault(x => x.type == Quest.Type.Clear && x.location == property.locationIndex);
                        if (quest != null)
                        {
                            lastAction += $" Quest '{quest.Title}' is reassigned to other party.";
                            RemoveQuest(quest);
                        }
                        availableQuests.RemoveAll(x => x.type == Quest.Type.Clear && x.location == property.locationIndex);
                    }
                }

                if (property.name == "House" || property.name == "Mansion")
                {
                    UpdateButtons();
                    property.storedItems = new();
                    int size = property.name == "House" ? 2 : 6;
                    property.gardenPlants = new();
                    for (int i = 0; i < size; ++i)
                        property.gardenPlants.Add(string.Empty);
                }
                AddTime(minutes: 30);
                if (ui.CurrentDialog == propertiesScreen)
                {
                    selectedProperty = property;
                    RefreshProperties();
                    RefreshPropertyDetails();
                }
                UpdateText();
            });
            itemEntry.SetImage(ui.propertyIcons[(int)property.GetImage()]);
        }
    }

    public void RefreshPropertyDetails()
    {
        ItemEntryList list = propertiesScreen.transform.Find("List").GetComponent<ItemEntryList>();
        selectedProperty = list.GetSelectedData() as Property;

        string str;
        if (selectedProperty == null)
            str = string.Empty;
        else if (selectedProperty.status == Property.Status.Building)
            str = $"<b>{selectedProperty.Name}</b>\n{Utility.Plural("day", selectedProperty.buildTime, true)} left to end of construction";
        else
        {
            str = $"<b>{selectedProperty.Name}</b>\n";
            Property.Event even = selectedProperty.events.FirstOrDefault();
            if (even != null)
            {
                if (even.timer == -1)
                    str += $"Events: {even.name}\n";
                else
                    str += $"Events: {even.name} ({even.timer})\n";
            }
            str += $"Income:{selectedProperty.Income}  Upkeep:{selectedProperty.Upkeep}  Profit:{selectedProperty.Profit}\nUpgrades: ";
            if (selectedProperty.upgrades != null && selectedProperty.upgrades.Any(x => x.active))
                str += string.Join(", ", selectedProperty.upgrades.Where(x => x.active).Select(x => x.name).OrderBy(x => x));
            else
                str += "(none)";
        }
        propertiesScreen.transform.Find("Text2").GetComponent<TMP_Text>().text = str;

        Transform content = propertiesScreen.transform.Find("Upgrades/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (selectedProperty != null && selectedProperty.status == Property.Status.Active && selectedProperty.upgrades != null && selectedProperty.upgrades.Any(x => !x.active))
        {
            ui.AddTextHeader("Available upgrades:", content);
            foreach (Property.Upgrade upgrade in selectedProperty.upgrades.Where(x => !x.active).OrderBy(x => x.name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.Init(upgrade.ToString(), "Buy", () =>
                {
                    if (player.gold < upgrade.value)
                    {
                        ui.ShowDialog($"You need {upgrade.value} gold to buy {upgrade.name.ToLower()}.");
                        return;
                    }

                    player.AddGold(-upgrade.value);
                    upgrade.active = true;
                    selectedProperty.value += upgrade.value;
                    selectedProperty.income += upgrade.income;
                    selectedProperty.upkeep += upgrade.upkeep;
                    lastAction = $"You buy {upgrade.name.ToLower()} for <color=#FFD700>{upgrade.value}</color> gold.";
                    if (upgrade.name == "Extra guards")
                    {
                        Property.Event even = selectedProperty.events.FirstOrDefault(e => e.name == "Infested" && e.timer == -1);
                        if (even != null)
                        {
                            int days = world.CalculateTravelDaysNonTeam(World.IndexToPoint(selectedProperty.locationIndex));
                            even.timer = days;
                            even.state = 1;
                            lastAction += $" They will take care of monsters infestation in {Utility.Plural("day", days, true)}.";
                        }
                    }
                    else if (upgrade.name == "Stables")
                        freshHorses = 10;
                    AddTime(minutes: 30);
                    if (ui.CurrentDialog == propertiesScreen)
                    {
                        RefreshProperties();
                        RefreshPropertyDetails();
                    }
                    UpdateText();
                });
            }
        }
    }

    public void Guild()
    {
        RefreshGuild();
        ui.ShowDialog(guildScreen);
    }

    private void RefreshGuild()
    {
        string guildText;
        if (!string.IsNullOrEmpty(lastAction))
        {
            guildText = lastAction;
            guildText += "\n\n";
        }
        else
            guildText = string.Empty;
        guildText += $"Your rank: {GuildRanks[guildRank]}";
        guildScreen.transform.Find("Text").GetComponent<TMP_Text>().text = guildText;

        guildScreen.transform.Find("BtJoin").GetComponent<Button>().interactable = guildRank == 0;
        guildScreen.transform.Find("BtRecruit").GetComponent<Button>().interactable = guildRank != 0;
        guildScreen.transform.Find("BtTrain").GetComponent<Button>().interactable = guildRank != 0;
        guildScreen.transform.Find("BtCook").GetComponent<Button>().interactable = guildRank != 0;
        guildScreen.transform.Find("BtCraft").GetComponent<Button>().interactable = guildRank != 0;

        // populate list with quests
        Transform content = guildScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        int acceptedQuests = activeQuests.Count(x => x.type != Quest.Type.Unique);
        if (acceptedQuests != 0)
        {
            ui.AddTextHeader($"Accepted quests ({acceptedQuests}/{guildRank}):", content);
            foreach (Quest quest in activeQuests.Where(x => x.type != Quest.Type.Unique))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                if (quest.IsDone())
                    itemEntry.Init2(quest.TextReward, "Finish", () => FinishQuest(quest), "Cancel", () => CancelQuest(quest));
                else
                    itemEntry.Init2(quest.TextReward, null, null, "Cancel", () => CancelQuest(quest));
            }
            Instantiate(ui.lineSeparatorPrefab, content);
        }

        if (guildRank != 0)
            ui.AddTextHeader("Available quests:", content);

        bool unavailable = false;
        foreach (Quest quest in availableQuests)
        {
            if (!unavailable && quest.difficulty > guildRank)
            {
                unavailable = true;
                Instantiate(ui.lineSeparatorPrefab, content);
                ui.AddTextHeader("Unavailable quests:", content);
            }

            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (acceptedQuests < guildRank && !unavailable)
            {
                itemEntry.Init(quest.TitleReward, "Pick", () =>
                {
                    activeQuests.Add(quest);
                    if (!activeQuests.Any(x => x.tracked))
                        quest.tracked = true;
                    if (quest.type == Quest.Type.Clear)
                    {
                        // if player already defeat some enemies, update counter
                        Tile tile = world.GetLocation(quest.location);
                        quest.count = tile.defeatedEnemies;
                    }
                    availableQuests.Remove(quest);
                    lastAction = $"You accepted quest '{quest.Title}'.";
                    AddTime(minutes: 15);
                    if (ui.CurrentDialog == guildScreen)
                        RefreshGuild();
                    UpdateText();
                });
            }
            else
                itemEntry.Init(quest.TitleReward);
        }

        // add player paid quests
        Property[] infestedProperties = player.properties.Where(p => p.events.Any(e => e.name == "Infested" && e.timer == -1)).ToArray();
        if (infestedProperties.Length > 0)
        {
            Instantiate(ui.lineSeparatorPrefab, content);
            ui.AddTextHeader("Quests to offer:", content);
            foreach (Property prop in infestedProperties)
            {
                Property property = prop;
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                int days = world.CalculateTravelDaysNonTeam(World.IndexToPoint(property.locationIndex));
                itemEntry.Init($"Clear {property.Name.ToLower()} ({Utility.Plural("day", days, true)}, {property.infestedCost} gold)", "Pay", () =>
                {
                    if (player.gold < property.infestedCost)
                    {
                        ui.ShowDialog($"You need {property.infestedCost} gold to pay adventurers to clear the {property.Name.ToLower()}.");
                        return;
                    }

                    player.AddGold(-property.infestedCost);
                    prop.events.First(e => e.name == "Infested").timer = days;
                    lastAction = $"You pay <color=#FFD700>{property.infestedCost}</color> gold to adventurers to clear the {property.Name.ToLower()}. " +
                        $"It will take them {Utility.Plural("day", days, true)}.";
                    AddTime(minutes: 15);
                    if (ui.CurrentDialog == guildScreen)
                        RefreshGuild();
                    UpdateText();
                });
            }
        }
    }

    private Quest GenerateQuest(int difficulty)
    {
        Quest quest = new() { difficulty = difficulty, timer = Utility.Random(5, 20) };
        while (true)
        {
            if (difficulty == 1)
            {
                switch (Utility.Rand % 8)
                {
                case 0:
                    // 12.5%
                    quest.type = Quest.Type.Clear;
                    quest.locationDifficulty = Utility.Random(1, 3);
                    quest.max = 10;
                    Property[] propertiesToClear = properties.Where(x => x.status == Property.Status.Active && x.infestedDifficulty == difficulty && !x.HaveEvent("Infested")).ToArray();
                    int choice = Utility.Rand % (propertiesToClear.Length + 1);
                    if (choice == propertiesToClear.Length)
                    {
                        // sewers
                        quest.location = world.FindLocationIndex(x => x.type == TileType.City, 1);
                        quest.locationDifficulty = 1;
                        quest.difficultyMod = 0.5f;
                        break;
                    }
                    else
                    {
                        // property that player don't own
                        Property property = propertiesToClear[choice];
                        quest.location = property.locationIndex;
                        quest.locationDifficulty = property.infestedCost / 250;
                        quest.difficultyMod = property.infestedDifficultyMod;
                    }
                    break;
                case 1:
                    // 12.5%
                    quest.type = Quest.Type.Gather;
                    quest.item = Item.Get(Utility.Rand % 2 == 0 ? "herb" : "magic crystal");
                    quest.max = 20;
                    quest.locationDifficulty = 1;
                    quest.difficultyMod = 0.25f;
                    break;
                case 2:
                case 3:
                    // 25%
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.hidden == TileType.Dungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.difficultyMod = 1f;
                    quest.max = 1;
                    break;
                case 4:
                    // 12.5%
                    quest.type = Quest.Type.Clear;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Cave || x.hidden == TileType.Cave) && !x.mine && x.difficulty == 1);
                    quest.locationDifficulty = 3;
                    quest.difficultyMod = 1f;
                    quest.max = 10;
                    break;
                default:
                    // 37.5%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 3);
                    quest.locationDifficulty = difficulty;
                    quest.difficultyMod = Mathf.Lerp(0.1f, 0.5f, quest.enemy.level / 3f);
                    break;
                }
            }
            else
            {
                string mineName = (difficulty == 2 ? "Silver mine" : "Gold mine");
                bool allowMine = properties.Any(x => x.name == mineName && x.status == Property.Status.Active);
                switch (Utility.Rand % 8)
                {
                case 0:
                case 1:
                    // 25%
                    quest.type = Quest.Type.Clear;
                    quest.max = 10;
                    Property[] propertiesToClear = properties.Where(x => x.status == Property.Status.Active && x.infestedDifficulty == difficulty && !x.HaveEvent("Infested")).ToArray();
                    if (propertiesToClear.Length == 0 || Utility.Rand % 2 == 0)
                    {
                        quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Cave || x.hidden == TileType.Cave) && !x.mine && !x.boss && x.difficulty == difficulty);
                        quest.locationDifficulty = difficulty == 2 ? 5 : 8;
                        quest.difficultyMod = 1f;
                    }
                    else
                    {
                        Property property = propertiesToClear.RandomItem();
                        quest.location = property.locationIndex;
                        quest.locationDifficulty = property.infestedCost / 250;
                        quest.difficultyMod = property.infestedDifficultyMod;
                    }
                    break;
                case 2:
                case 3:
                    // 25%
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.hidden == TileType.Dungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.difficultyMod = 1f;
                    quest.max = 1;
                    break;
                case 4:
                    // 12.5%
                    if (difficulty == 3)
                        goto case default;
                    quest.type = Quest.Type.Gather;
                    if (Utility.Rand % 2 == 0)
                    {
                        quest.item = Item.Get("rare herb");
                        quest.max = 20;
                    }
                    else
                    {
                        quest.item = Item.Get("magic crystal");
                        quest.max = 50;
                    }
                    quest.locationDifficulty = 2;
                    quest.difficultyMod = 0.25f;
                    break;
                default:
                    // 37.5%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 2 + difficulty);
                    quest.locationDifficulty = difficulty;
                    if (difficulty == 2)
                        quest.difficultyMod = Mathf.Lerp(0.25f, 0.5f, (quest.enemy.level - 4) / 2f);
                    else
                        quest.difficultyMod = Mathf.Lerp(0.25f, 0.5f, (quest.enemy.level - 7) / 2f);
                    break;
                }
            }

            if (availableQuests.All(x => !x.IsSimilar(quest)) && activeQuests.All(x => !x.IsSimilar(quest)))
            {
                if (quest.type == Quest.Type.Artifact || quest.type == Quest.Type.Clear)
                {
                    Tile tile = world.GetLocation(quest.location);
                    tile.defeatedEnemies = 0;
                    tile.timer = 0;
                    tile.foundTreasure = false;
                }
                return quest;
            }
        }
    }

    private void FinishQuest(Quest quest)
    {
        int reward = quest.Reward;
        lastAction = $"You received <color=#FFD700>{reward}</color> gold for quest '{quest.Title}'.";
        bool promoted = false;
        if (guildRank != MaxGuildRank)
        {
            float value = quest.difficultyMod;
            if (quest.difficulty + 1 == guildRank)
                value /= 4;
            else if (quest.difficulty < guildRank)
                value = 0;

            if (value > 0)
            {
                guildProgress += value;
                if (guildProgress >= 1f + guildRank)
                {
                    ++guildRank;
                    guildProgress = 0;
                    lastAction += $" You were promoted to <b>{GuildRanks[guildRank]}</b> rank.";
                    ChangeTeamAffection(5);
                    promoted = true;
                }
            }
        }
        if (!promoted)
            ChangeTeamAffection(1);
        AddTeamGold(reward);
        quest.Finish();
        RemoveQuest(quest);
        AddTime(minutes: 15);
        if (ui.CurrentDialog == guildScreen)
            RefreshGuild();
        UpdateText();
    }

    private void CancelQuest(Quest quest)
    {
        lastAction = $"You canceled quest '{quest.Title}'.";
        guildProgress -= quest.difficultyMod;
        if (guildRank > 1 && guildProgress < -guildRank)
        {
            --guildRank;
            guildProgress = 0;
            lastAction += $" You are degraded to <b>{GuildRanks[guildRank]}</b> rank.";
            ChangeTeamAffection(-5);
        }
        else
            ChangeTeamAffection(-1);
        RemoveQuest(quest);

        // readd quest if it can be completed
        bool canBeCompleted = true;
        if (quest.type == Quest.Type.Artifact)
        {
            Tile tile = world.GetLocation(quest.location);
            canBeCompleted = !tile.foundTreasure;
        }
        else if (quest.type == Quest.Type.Clear)
        {
            Tile tile = world.GetLocation(quest.location);
            canBeCompleted = !tile.clear;
        }
        if (canBeCompleted)
        {
            quest.timer = 5;
            availableQuests.Add(quest);
            SortQuests();
        }

        AddTime(minutes: 15);
        if (ui.CurrentDialog == guildScreen)
            RefreshGuild();
        UpdateText();
    }

    private void RemoveQuest(Quest quest)
    {
        bool isTracked = quest.tracked;
        activeQuests.Remove(quest);
        if (isTracked)
        {
            quest = activeQuests.FirstOrDefault(x => x.type != Quest.Type.Unique);
            if (quest != null)
                quest.tracked = true;
        }
    }

    // forage or prospect
    public void Forage()
    {
        Tile tile = world.CurrentTile;

        if (tile.type == TileType.Cave && !HaveTeamItem("pickaxe"))
        {
            lastAction = "You need a pickaxe for that.";
            UpdateText();
            return;
        }

        int energy = tile.type == TileType.Forest ? 10 : 5;
        if (player.energy < energy)
        {
            lastAction = $"You are too tired to {(tile.type == TileType.Forest ? "forage" : "prospect")}.";
            UpdateText();
            return;
        }

        player.energy -= energy;

        Enemy enemy;
        if (!tile.clear && Utility.Rand % 10 == 0 && (enemy = Enemy.GetRandom(tile.type, tile.difficulty)) != null)
        {
            StartCombat(enemy, tile.type == TileType.Forest ? "forage in" : "prospect");
            return;
        }
        else if (tile.type == TileType.Forest)
        {
            if (tile.depleted >= 4)
                lastAction = $"You forage in the {tile.Name} but find nothing of value.";
            else
            {
                // herbs/rare herbs
                (Hero bestHero, int bestValue) = GetTeamSkill(Skill.Forage);
                int count = (Utility.Rand % 6) switch
                {
                    1 or 2 => 2,
                    3 or 4 => 3,
                    5 => 4,
                    _ => 1,
                };
                count += bestValue / 25 - tile.depleted;
                if (count < 1)
                    count = 1;
                tile.depleted++;
                Item herb = tile.GetHerb();
                player.AddItem(herb, count);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Forage));
                    lastAction = $"You forage the {tile.Name} and with {bestHero.name} help find <b>{Utility.Plural(herb.name, count)}</b>.";
                    if (Utility.Rand % 100 < bestValue)
                    {
                        lastAction += $" You also find some edible {(Utility.Rand % 2 == 0 ? "fruits" : "vegetables")} (<b>+1 rations</b>).";
                        player.AddItem(Item.Get("rations"));
                    }
                    lastAction += player.Train(Skill.Forage, 0.25f * trainMod);
                    bestHero.Train(Skill.Forage, 0.25f);
                }
                else
                {
                    lastAction = $"You forage the {tile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.";
                    if (Utility.Rand % 100 < bestValue)
                    {
                        lastAction += $" You also find some edible {(Utility.Rand % 2 == 0 ? "fruits" : "vegetables")} (<b>+1 rations</b>).";
                        player.AddItem(Item.Get("rations"));
                    }
                    lastAction += player.Train(Skill.Forage, 0.25f);
                }
            }
            AddTime(hours: 1);
        }
        else
        {
            if (tile.boss || (tile.mine && tile.depleted >= 4) || (!tile.mine && tile.depleted >= tile.difficulty + 2))
                lastAction = $"You prospect the {tile.Name} but find nothing of value.";
            else if (tile.mine)
            {
                // silver/gold nuggets
                (Hero bestHero, int bestValue) = GetTeamSkill(Skill.Mining);
                int count = (Utility.Rand % 6) switch
                {
                    1 or 2 => 2,
                    3 or 4 => 3,
                    5 => 4,
                    _ => 1,
                };
                count += bestValue / 25 - tile.depleted;
                if (count < 1)
                    count = 1;
                tile.depleted++;
                Item nugget = Item.Get(tile.difficulty == 2 ? "silver nugget" : "gold nugget");
                player.AddItem(nugget, count, allies.Count > 0);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Mining));
                    lastAction = $"You prospect the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. " +
                        $"You and {bestHero.name} mine <b>{Utility.Plural(nugget.name, count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, 0.25f);
                }
                else
                {
                    lastAction = $"You prospect the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. You mine <b>{Utility.Plural(nugget.name, count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f);
                }
            }
            else
            {
                // magic crystals
                (Hero bestHero, int bestValue) = GetTeamSkill(Skill.Mining);
                int count = (Utility.Rand % 4) switch
                {
                    1 or 2 => 2,
                    3 => 3,
                    _ => 1,
                };
                count += tile.difficulty - tile.depleted - 1 + bestValue / 25;
                if (count < 1)
                    count = 1;
                tile.depleted++;
                player.AddItem(Item.Get("magic crystal"), count);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Mining));
                    lastAction = $"You prospect the {tile.Name} and find small <b>magic crystals cluster</b>. You and {bestHero.name} mine <b>{Utility.Plural("magic crystal", count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, 0.25f);
                }
                else
                {
                    lastAction = $"You prospect the {tile.Name} and find small <b>magic crystals cluster</b>. You mine <b>{Utility.Plural("magic crystal", count)}</b>.";
                    lastAction += player.Train(Skill.Mining, 0.25f);
                }
            }
            AddTime(minutes: 30);
        }

        UpdateText();
    }

    public void Craft()
    {
        RefreshCraft();
        ui.ShowDialog(craftScreen);
    }

    private void RefreshCraft()
    {
        // text
        craftScreen.transform.Find("Text").GetComponent<TMP_Text>().text = lastAction ?? string.Empty;

        // ingredients
        Transform content = craftScreen.transform.Find("Ingredients/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (ItemSlot itemSlot in player.items.Where(x => x.item.subtype == Item.Subtype.Ingredient))
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToStringShort());
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }

        // potions
        (Hero bestHero, int alchemy) = GetTeamSkill(Skill.Alchemy);
        int bonus = 0;
        if ((world.Location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab", world.CityIndex))
            || (world.Location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Alchemy lab", world.CityIndex)))
        {
            bonus = 25;
            alchemy += 25;
        }
        content = craftScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        void Brew(Recipe recipe, int count)
        {
            player.RemoveItem(recipe.ingredient, count * 2);
            float mod;
            if (alchemy >= 100)
                mod = 1;
            else if (alchemy >= 75)
                mod = 0.5f;
            else if (alchemy >= 50)
                mod = 0.25f;
            else if (alchemy >= 25)
                mod = 0.1f;
            else
                mod = 0;
            int extra = (int)(count * mod);
            player.AddItem(recipe.result, count + extra);
            float trainMod;
            if (bestHero == null || bestHero is Player)
            {
                lastAction = $"You created {Utility.Plural(recipe.result.name, count + extra)}.";
                trainMod = 1f;
            }
            else
            {
                lastAction = $"You and {bestHero.name} created {Utility.Plural(recipe.result.name, count + extra)}.";
                trainMod = 1f + 0.01f * (alchemy - bonus - player.GetSkill(Skill.Alchemy));
                bestHero.Train(Skill.Alchemy, recipe.trainMod * count);
            }
            lastAction += player.Train(Skill.Alchemy, recipe.trainMod * trainMod * count);
            AddTime(minutes: count * 5);
            if (ui.IsOpen(craftScreen))
                RefreshCraft();
            if (ui.IsOpen(characterScreen))
                RefreshPlayerScreen();
            UpdateText();
        }

        foreach (Recipe recipe in Recipe.GetAvailable(alchemy))
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(recipe.ToString(player.CountItem(recipe.result)), "Brew", () =>
            {
                int possible = player.CountItem(recipe.ingredient) / recipe.ingredientCount;
                if (possible == 0)
                {
                    ui.ShowDialog($"You need {Utility.Plural(recipe.ingredient.name, recipe.ingredientCount)} to brew {recipe.result.name}.");
                    return;
                }

                if (Input.GetKey(KeyCode.LeftShift))
                    Brew(recipe, possible);
                else if (Input.GetKey(KeyCode.LeftControl))
                {
                    ui.ShowInput($"How many {Utility.Plural(recipe.result.name)} to brew (1-{possible})?", count =>
                    {
                        if (count <= 0)
                            return true;
                        Brew(recipe, Mathf.Min(count, possible));
                        return true;
                    });
                }
                else
                    Brew(recipe, 1);
            });
            itemEntry.SetImage(ui.itemIcons[(int)recipe.result.GetIcon()]);
        }
    }

    public void DoAlchemy(Hero hero, Item item)
    {
        (Hero bestHero, int alchemy) = GetTeamSkill(Skill.Alchemy);
        int bonus = 0;
        if ((world.Location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab", world.CityIndex))
            || (world.Location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Alchemy lab", world.CityIndex)))
        {
            bonus = 25;
            alchemy += 25;
        }

        int ingredientCount = hero.CountItem(item);
        Recipe recipe = Recipe.recipes.FirstOrDefault(x => alchemy >= x.requiredSkill && x.ingredient == item && ingredientCount >= x.ingredientCount);
        if (recipe == null)
            return;

        int count = ingredientCount / recipe.ingredientCount;
        hero.RemoveItem(recipe.ingredient, count * 2);
        float mod;
        if (alchemy >= 100)
            mod = 1;
        else if (alchemy >= 75)
            mod = 0.5f;
        else if (alchemy >= 50)
            mod = 0.25f;
        else if (alchemy >= 25)
            mod = 0.1f;
        else
            mod = 0;
        int extra = (int)(count * mod);
        int totalCount = count + extra;
        player.AddItem(recipe.result, totalCount);
        if (hero == bestHero)
        {
            lastAction = $"{hero.name} creates {Utility.Plural(recipe.result.name, totalCount)} and gives {(totalCount == 1 ? "it" : "them")} to you.";
            hero.Train(Skill.Alchemy, recipe.trainMod * count);
        }
        else
        {
            lastAction = $"{hero.name} and {bestHero.nameYou} create {Utility.Plural(recipe.result.name, totalCount)}. You receive {(totalCount == 1 ? "it" : "them")}.";
            float trainMod = 1f + 0.01f * (alchemy - bonus - hero.GetSkill(Skill.Alchemy));
            hero.Train(Skill.Alchemy, recipe.trainMod * trainMod * count);
            string str = bestHero.Train(Skill.Alchemy, recipe.trainMod * count);
            if (bestHero is Player)
                lastAction += str;
        }

        if (ui.IsOpen(giveAllyItemsScreen))
        {
            RefreshPlayerItems();
            RefreshAllyScreen();
        }
        UpdateText();
    }

    private void AddTeamGold(int gold)
    {
        if (gold <= 0)
            return;

        if (allies.Count == 0)
        {
            player.AddGold(gold);
            return;
        }

        int share = gold / (allies.Count + 1);
        int extraGold = gold - share * (allies.Count + 1);
        foreach (Hero hero in Team)
        {
            int goldReceived = share;
            if (extraGold > 0)
            {
                ++goldReceived;
                --extraGold;
            }
            hero.AddGold(goldReceived);
            if (hero is not Player)
            {
                if (world.Location.IsSafe())
                    hero.BuyItems();
                else if (world.Location == TileType.MageTower)
                    hero.EnchantItems();
            }
        }
    }

    public void Choice(string str, System.Action<bool> action)
    {
        choiceAction = action;
        lastAction = str;
        UpdateText();
        transform.Find("Buttons").gameObject.SetActive(false);
        transform.Find("ChoiceButtons").gameObject.SetActive(true);
        inChoice = true;
    }

    public void PickChoice(bool choice)
    {
        inChoice = false;
        transform.Find("Buttons").gameObject.SetActive(true);
        transform.Find("ChoiceButtons").gameObject.SetActive(false);
        choiceAction(choice);
    }

    [ContextMenu("Regenerate world")]
    private void RegenerateWorld()
    {
        world.Init();
        foreach (Property property in Property.properties)
        {
            if (property.locationIndexFunc != null)
            {
                Property copy = properties.Union(player.properties).FirstOrDefault(x => x.name == property.name && x.cityIndex == property.cityIndex);
                if (copy != null)
                {
                    copy.locationIndex = property.locationIndexFunc(world);
#if UNITY_EDITOR
                    if (copy.locationIndex == -1)
                    {
                        Debug.LogWarning($"Failed to find location index for '{property.Name}'.");
                        continue;
                    }
#endif
                    Tile tile = world.GetLocation(copy.locationIndex);
                    if (tile.type == TileType.Mountains && copy.status >= Property.Status.Building)
                    {
                        tile.type = TileType.Cave;
                        tile.hidden = TileType.None;
                        tile.image = TileImage.Cave;
                        tile.clear = true;
                    }
                    if (tile.type == TileType.Cave && copy.status == Property.Status.Active)
                    {
                        tile.type = TileType.Mine;
                        tile.image = TileImage.Mine;
                    }
                    if (tile.type == TileType.Plains && copy.status == Property.Status.Active)
                    {
                        tile.type = TileType.Farm;
                        tile.image = TileImage.Farm;
                        tile.name = world.GetCityTile(2).name.Split(' ')[0] + " farm";
                        tile.difficulty = 3;
                        tile.clear = true;
                    }
                }
            }
        }
        activeQuests.RemoveAll(x => x.type == Quest.Type.Artifact || x.type == Quest.Type.Clear);
        availableQuests.RemoveAll(x => x.type == Quest.Type.Artifact || x.type == Quest.Type.Clear);
        map.Regenerate();
    }

    [ContextMenu("Reveal world")]
    private void RevealWorld()
    {
        world.RevealAllAreas();
    }

    [ContextMenu("Refresh quests")]
    private void RefreshQuests()
    {
        availableQuests.Clear();
        GenerateInitialQuests();
        if (ui.CurrentDialog == guildScreen)
            RefreshGuild();
    }

    [ContextMenu("Give all")]
    private void GiveAll()
    {
        while (allies.Count + 1 < MaxTeamSize)
            allies.Add(SpawnHero());

        foreach (Hero hero in Team)
        {
            if (hero.level < 10)
                hero.SetLevel(10);
            if (hero.clas == Class.Warrior)
            {
                hero.weapon = Item.Get("magic sword");
                hero.shield = Item.Get("magic shield");
            }
            else
                hero.weapon = Item.Get("magic bow");
            hero.armor = Item.Get("magic armor");
            hero.AddItem(Item.Get("elixir"), 100);
            hero.AddItem(Item.Get("rations"), 1000 - hero.CountItem(Item.Get("rations")));
            hero.gold = Mathf.Max(hero.gold, 100000);
            hero.owedGold = 0;
        }

        player.AddItemIfMissing("tent");
        player.AddItemIfMissing("pickaxe");

        if (!player.HaveProperty("Horses"))
        {
            Property property = properties.First(x => x.name == "Horses");
            player.properties.Add(property);
            properties.Remove(property);
        }

        UpdateText();
        UpdateButtons();
    }

    [ContextMenu("Regenerate properties")]
    private void RegenerateProperties()
    {
        foreach (Property property in Property.properties)
        {
            Property copy = properties.Union(player.properties).FirstOrDefault(x => x.name == property.name && x.cityIndex == property.cityIndex);
            if (copy != null)
                copy.Update(property);
            else
            {
                copy = property.Copy();
                if (property.locationIndexFunc != null)
                {
                    copy.locationIndex = property.locationIndexFunc(world);
#if UNITY_EDITOR
                    if (copy.locationIndex == -1)
                        Debug.LogWarning($"Failed to find location index for '{property.Name}'.");
#endif
                }
                else
                    copy.locationIndex = -1;
                properties.Add(copy);
            }
        }
    }

    public void JoinGuild()
    {
        guildRank = 1;
        lastAction = "You fill out form and register as adventurer. From this day forward, you are free to accept quests, earn rewards, and carve your own path through the dungeons. " +
            "May your courage be greater than the dangers ahead, and your pack always heavy with treasure.";
        AddTime(minutes: 15);
        if (ui.CurrentDialog == guildScreen)
            RefreshGuild();
        UpdateText();
    }

    public void StoreItems()
    {
        activeInventory = storeItemsScreen;
        ui.ShowDialog(storeItemsScreen);
        RefreshPlayerItems();
        RefreshStoredItems();
    }

    private void RefreshStoredItems()
    {
        List<ItemSlot> storedItems = GetPropertyInside().storedItems;

        Transform content = storeItemsScreen.transform.Find("StoredItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (ItemSlot itemSlot in storedItems)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(Price.None), "Take", () =>
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    player.AddItem(itemSlot.item, itemSlot.count);
                    RemoveStoredItem(itemSlot, itemSlot.count);
                    RefreshPlayerItems();
                    RefreshStoredItems();
                }
                else if (Input.GetKey(KeyCode.LeftControl))
                {
                    ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to take?", count =>
                    {
                        if (count <= 0)
                            return true;
                        count = Mathf.Min(count, itemSlot.count);
                        player.AddItem(itemSlot.item, count);
                        RemoveStoredItem(itemSlot, count);
                        RefreshPlayerItems();
                        RefreshStoredItems();
                        return true;
                    });
                }
                else
                {
                    player.AddItem(itemSlot.item);
                    RemoveStoredItem(itemSlot);
                    RefreshPlayerItems();
                    RefreshStoredItems();
                }
            });
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }

    private void AddStoredItem(Item item, int count = 1, List<ItemSlot> storedItems = null)
    {
        storedItems ??= GetPropertyInside().storedItems;
        ItemSlot itemSlot = storedItems.FirstOrDefault(x => x.item == item);
        if (itemSlot != null)
            itemSlot.count += count;
        else
            storedItems.Add(new() { item = item, count = count });
    }

    private void RemoveStoredItem(ItemSlot itemSlot, int count = 1)
    {
        itemSlot.count -= count;
        if (itemSlot.count <= 0)
            GetPropertyInside().storedItems.Remove(itemSlot);
    }

    public void Garden()
    {
        ui.ShowDialog(gardenScreen);
        RefreshGarden();
    }

    private void RefreshGarden()
    {
        Transform content = gardenScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        Property property = GetPropertyInside();
        string[] choices = new string[] { "---", "Vegetables (10 gold)", "Herbs (10 herbs)", "Rare herbs (10 herbs)" };

        for (int i = 0; i < property.gardenPlants.Count; ++i)
        {
            int index = i;
            string plant = property.gardenPlants[i];
            if (plant == "")
                plant = "Empty";
            DropdownEntry dropdownEntry = Instantiate(ui.dropdownEntryPrefab, content).GetComponent<DropdownEntry>();
            dropdownEntry.Init($"Plot {i + 1}: {plant}", "Change", choices, x =>
            {
                switch (x)
                {
                case 1:
                    // vegetables
                    if (plant == "Vegetables")
                        ui.ShowDialog("Vegetables are already planted here.");
                    else if (player.gold < 10)
                        ui.ShowDialog("You need 10 gold.");
                    else
                    {
                        player.AddGold(-10);
                        property.gardenPlants[index] = "Vegetables";
                        RefreshGarden();
                        UpdateText();
                    }
                    break;
                case 2:
                    // herbs
                    Item herb = Item.Get("herb");
                    if (plant == "Herbs")
                        ui.ShowDialog("Herbs are already planted here.");
                    else if (player.CountItem(herb) < 10)
                        ui.ShowDialog("You need 10 herbs.");
                    else
                    {
                        player.RemoveItem(herb, 10);
                        property.gardenPlants[index] = "Herbs";
                        RefreshGarden();
                    }
                    break;
                case 3:
                    // rare herbs
                    Item rareHerb = Item.Get("rare herb");
                    if (plant == "Rare herbs")
                        ui.ShowDialog("Rare herbs are already planted here.");
                    else if (player.CountItem(rareHerb) < 10)
                        ui.ShowDialog("You need 10 rare herbs.");
                    else
                    {
                        player.RemoveItem(rareHerb, 10);
                        property.gardenPlants[index] = "Rare herbs";
                        RefreshGarden();
                    }
                    break;
                }
            });
        }
    }

    public void Cook()
    {
        Item meat = Item.Get("meat");
        int meatCount = player.CountItem(meat);
        ui.ShowInput($"How many meat you want to cook? You have {meatCount} pieces of meat.", count =>
        {
            if (count <= 0)
                return true;
            if (count > meatCount)
            {
                ui.ShowDialog($"You don't have {count} pieces of meat.");
                return false;
            }
            Item rations = Item.Get("rations");
            player.RemoveItem(meat, count);
            player.AddItem(rations, count);
            lastAction = $"You cooked {count} pieces of meat into rations.";
            AddTime(minutes: count * 5);
            if (ui.TopDialog == guildScreen)
                RefreshGuild();
            UpdateText();
            return true;
        });
    }

    public void SetText(string txt)
    {
        lastAction = txt;
        UpdateText();
    }

    public void Journal()
    {
        RefreshJournal();
        ui.ShowDialog(journalScreen);
    }

    private void RefreshJournal()
    {
        bool notificationChanges = false;

        // notifications
        sb.Clear();
        if (notifications.Any(x => x.status != Notification.Status.Waiting))
        {
            foreach (Notification notification in notifications.Where(x => x.status != Notification.Status.Waiting))
            {
                if (notification.status == Notification.Status.Available)
                    sb.Append("<b>");
                sb.Append($"Day {notification.day} - {notification.text}");
                if (notification.status == Notification.Status.Available)
                {
                    sb.Append("</b>");
                    notification.status = Notification.Status.Read;
                    notificationChanges = true;
                }
                sb.Append("\n");
            }
        }
        else
            sb.Append("...");
        journalScreen.transform.Find("Notifications/Viewport/Content/Text").GetComponent<TMP_Text>().text = sb.ToString();
        StartCoroutine(MoveScrollRectToPos(journalScreen.transform.Find("Notifications").GetComponent<ScrollRect>(), 0f));

        // active quests
        Transform content = journalScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (Quest quest in activeQuests)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (quest.tracked)
                itemEntry.Init(quest.TextReward);
            else
            {
                itemEntry.Init(quest.TextReward, "Track", () =>
                {
                    Quest prevQuest = activeQuests.FirstOrDefault(x => x.tracked);
                    if (prevQuest != null)
                        prevQuest.tracked = false;
                    quest.tracked = true;
                    RefreshJournal();
                    UpdateText();
                });
            }
        }

        if (notificationChanges)
            UpdateButtons();
    }

    private IEnumerator MoveScrollRectToPos(ScrollRect scrollRect, float pos)
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = pos;
    }

    public void EnchantItems()
    {
        activeInventory = enchantItemsScreen;
        RefreshPlayerItems();
        ui.ShowDialog(enchantItemsScreen);
    }

    public void EnterPortal()
    {
        if (dragonStatus == DragonStatus.None)
        {
            lastAction = "The portal is sealed by dragon seal.";
            UpdateText();
            return;
        }

        if (world.sublocation != 4)
        {
            lastAction = "You enter the portal and arrive in dark dimension.";
            world.sublocation = 4;
        }
        else
        {
            lastAction = "You enter the portal and arrive back in mage tower.";
            world.sublocation = 0;
        }
        OnChangeLocation();
        AddTime(minutes: 15);
    }

    private void ChangeAffection(Hero ally, int value)
    {
        if (value > 0)
        {
            if (ally.affection == 100)
                return;
        }
        else
        {
            if (ally.affection == -100)
                return;
        }

        ally.affection = Mathf.Clamp(ally.affection + value, -100, 100);
    }

    private void IncreaseAffectionFromValue(Hero ally, Item item, int count)
    {
        if (item.type == Item.Type.Usable && item.subtype == Item.Subtype.Ingredient)
            return;
        IncreaseAffectionFromValue(ally, item.value * count);
    }

    private void IncreaseAffectionFromValue(Hero ally, int value)
    {
        int affectionGain = ally.ValueToAffectionGain(value);
        int actualAffectionGain = affectionGain - ally.lastGift;
        if (actualAffectionGain > 0)
        {
            ally.lastGift = affectionGain;
            ChangeAffection(ally, actualAffectionGain);
        }
    }

    private void ChangeTeamAffection(int value, System.Func<Hero, bool> pred = null)
    {
        List<(Hero ally, int change)> changes = null;
        foreach (Hero ally in allies)
        {
            if (pred != null && !pred(ally))
                continue;
            if (value > 0)
            {
                if (ally.affection == 100)
                    continue;
            }
            else
            {
                if (ally.affection == -100)
                    continue;
            }

            int prev = ally.affection;
            ally.affection = Mathf.Clamp(ally.affection + value, -100, 100);
            int actualChange = ally.affection - prev;
            changes ??= new();
            changes.Add((ally, actualChange));
        }

        if (changes != null)
        {
            foreach (var group in changes.GroupBy(x => x.change))
                lastAction += $" {Utility.PrettyList(group.Select(x => x.ally.name))} affection {(value > 0 ? "increased" : "decreased")} ({group.Key:+0;-#}).";
        }
    }

    [ContextMenu("Test images")]
    private void TestImages()
    {
        foreach (Class clas in ClassMethods.all)
        {
            string path = $"Portraits/male {clas.AsString()}";
            if (Resources.Load<Sprite>(path) == null)
                Debug.LogError($"Missing '{path}'.");

            path = $"Portraits/female {clas.AsString()}";
            if (Resources.Load<Sprite>(path) == null)
                Debug.LogError($"Missing '{path}'.");
        }

        foreach (Enemy enemy in Enemy.enemies)
        {
            string path = $"Portraits/{enemy.name}";
            if (Resources.Load<Sprite>(path) == null)
                Debug.LogError($"Missing '{path}'.");
        }
    }

    [ContextMenu("Test combat")]
    private void TestCombat()
    {
        ui.ShowInput("Test combat vs:", TestCombat, lastTestCombat);
    }

    private bool TestCombat(string enemiesStr)
    {
        string[] parts = enemiesStr.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
        if (parts.Length == 0)
            return false;

        List<Enemy> enemyList = new();
        foreach (string part in parts)
        {
            string[] innerParts = part.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string name;
            if (innerParts.Length > 1 && int.TryParse(innerParts[0], out int count))
                name = string.Join(' ', innerParts.Skip(1));
            else
            {
                name = part;
                count = 1;
            }

            Enemy enemy = Enemy.TryGet(name);
            if (enemy == null)
            {
                ui.ShowDialog($"Invalid enemy '{name}'.");
                return false;
            }
            for (int i = 0; i < count; ++i)
                enemyList.Add(enemy);
        }

        if (enemyList.Count == 0)
        {
            ui.ShowDialog("Empty list.");
            return false;
        }

        if (enemyList.Count > MaxTeamSize)
        {
            ui.ShowDialog("Too many enemies.");
            return false;
        }

        ui.CloseDialog();
        lastTestCombat = enemiesStr;
        combatScreen.Init(enemyList, "explore", false);
        ui.lockDialog = true;
        ui.ShowDialog(combatScreen.gameObject);
        return true;
    }

    private Property GetPropertyInside()
    {
        TileType location = world.Location;
        if (location == TileType.House)
            return player.properties.First(x => x.name == "House" && x.cityIndex == world.CityIndex);
        else if (location == TileType.Mansion)
            return player.properties.First(x => x.name == "Mansion" && x.cityIndex == world.CityIndex);
        else
            return null;
    }

    private (Hero hero, int value) GetTeamSkill(Skill skill)
    {
        Hero bestHero = null;
        int bestValue = 0;
        foreach (Hero hero in Team)
        {
            int value = hero.GetSkill(skill);
            if (value > bestValue)
            {
                bestValue = value;
                bestHero = hero;
            }
        }
        return (bestHero, bestValue);
    }

    private bool HaveTeamItem(string itemName)
    {
        Item item = Item.Get(itemName);
        return Team.Any(x => x.HaveItem(item));
    }

    private void PayForTeamItem(Hero hero, Item item, int count = 1)
    {
        int cost = (item.value * count / 2) * allies.Count / (allies.Count + 1);
        hero.owedGold += cost;
        CancelOutDebts();
        if (hero.owedGold > 0)
            PayOwedGold(hero);
    }

    public void PayOwedGold(Hero hero)
    {
        int availableGold = hero.gold - Hero.MinGold;
        if (availableGold <= 0)
            return;

        if (availableGold >= hero.owedGold)
        {
            // pay all
            int goldPerHero = hero.owedGold / allies.Count;
            int extraGold = hero.owedGold - goldPerHero * allies.Count;
            hero.AddGold(-hero.owedGold);
            hero.owedGold = 0;
            foreach (Hero hero2 in Team)
            {
                if (hero2 == hero)
                    continue;
                int goldReceived = goldPerHero;
                if (extraGold > 0)
                {
                    ++goldReceived;
                    --extraGold;
                }
                hero2.AddGold(goldReceived);
            }
        }
        else
        {
            // pay partial
            int goldPerHero = availableGold / allies.Count;
            if (goldPerHero == 0)
                return;
            hero.AddGold(-goldPerHero * allies.Count);
            hero.owedGold -= goldPerHero * allies.Count;
            foreach (Hero hero2 in Team)
            {
                if (hero2 != hero)
                    hero2.AddGold(goldPerHero);
            }
        }
    }

    private void CancelOutDebts()
    {
        if (allies.Count == 0)
        {
            player.TurnItemsNonTeam();
            player.owedGold = 0;
            return;
        }

        int minDebt = Team.Min(x => x.owedGold);
        if (minDebt > 0)
        {
            foreach (Hero hero in Team)
                hero.owedGold -= minDebt;
        }
    }

    [ContextMenu("Give item")]
    private void GiveItem()
    {
        ui.ShowInput("Give item:", GiveItem);
    }

    private bool GiveItem(string str)
    {
        bool team;
        if (str.StartsWith("team "))
        {
            team = true;
            str = str["team ".Length..];
        }
        else
            team = false;

        string[] parts = str.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (int.TryParse(parts[0], out int count))
            str = string.Join(' ', parts.Skip(1));
        else
            count = 1;

        Item item = Item.TryGet(str);
        if (item == null)
        {
            ui.ShowDialog($"Invalid item '{str}'.");
            return false;
        }

        if (count > 0)
            player.AddItem(item, count, team);
        return true;
    }

    private void GenerateInitialQuests()
    {
        for (int difficulty = 1; difficulty <= 3; ++difficulty)
        {
            for (int i = 0; i < 2; ++i)
                availableQuests.Add(GenerateQuest(difficulty));
        }

        SortQuests();
    }

    private void UpdateQuests()
    {
        // remove old quests
        availableQuests.RemoveAll(quest =>
        {
            --quest.timer;
            return quest.timer <= 0;
        });

        // add new quests
        if (availableQuests.Count != 6)
        {
            int[] questsByDifficulty = new int[4];
            foreach (Quest quest in availableQuests)
                questsByDifficulty[quest.difficulty]++;

            bool addedQuests = false;
            for (int difficulty = 1; difficulty <= 3; ++difficulty)
            {
                int missingQuests = 3 - questsByDifficulty[difficulty];
                for (int i = 0; i < missingQuests; ++i)
                {
                    if (Utility.Rand % 4 == 0)
                    {
                        Quest quest = GenerateQuest(difficulty);
                        availableQuests.Add(quest);
                        addedQuests = true;
                    }
                }
            }

            if (addedQuests)
                SortQuests();
        }
    }

    private void SortQuests()
    {
        availableQuests.Sort((a, b) =>
        {
            int result = a.difficulty.CompareTo(b.difficulty);
            if (result != 0)
                return result;
            return a.timer.CompareTo(b.timer);
        });
    }

    public void Train()
    {
        if (hour > 16)
            lastAction = "It's too late to train.";
        else if (player.energy < 50)
            lastAction = "You are too tired to train.";
        else
        {
            player.energy -= 50;
            lastAction = "You train fighting.";
            List<Hero> levelups = null;
            foreach (Hero hero in Team)
            {
                if (hero.AddExp(100))
                {
                    levelups ??= new();
                    levelups.Add(hero);
                }
            }

            if (levelups != null)
            {
                foreach (var group in levelups.GroupBy(x => x.level))
                {
                    string isAre = group.Count() > 1 || group.First() is Player ? "are" : "is";
                    lastAction += $" {Utility.PrettyList(group.Select(x => x.nameYou)).ToUpper1()} {isAre} now level {group.Key}.";
                }
            }

            AddTime(hours: 8);
            if (ui.CurrentDialog == guildScreen)
                RefreshGuild();
        }
        UpdateText();
    }
}
