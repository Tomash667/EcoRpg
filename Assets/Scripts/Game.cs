using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public enum DragonStatus
    {
        None,
        Defeated,
        Win
    }

    public enum SpiderStatus
    {
        None,
        Accepted,
        Defeated,
        Rewarded,
        Skipped
    }

    public World world;
    [SerializeReference]
    public Player player;
    public Team team;
    public List<Quest> availableQuests, activeQuests;
    public List<Property> properties;
    public List<Notification> notifications;
    public List<Worker> availableWorkers, hiredWorkers;
    public DragonStatus dragonStatus;
    public SpiderStatus spiderStatus;
    public int day, hour, minute;

    private GameUI ui;
    private RectTransform[] alliesHealthRect;
    private AllyScreen allyScreen;
    private CharacterScreen characterScreen;
    private Combat combatScreen;
    private CraftScreen craftScreen;
    private GardenScreen gardenScreen;
    private GiveItemsScreen giveItemsScreen;
    private GuildScreen guildScreen;
    private Journal journal;
    private Map map;
    private PropertiesScreen propertiesScreen;
    private ShopScreen shopScreen;
    private TMP_Text mainText;
    private readonly StringBuilder sb = new();
    private readonly TextBuilder text = new();
    private System.Action<bool> choiceAction;
    private string lastTestCombat;
    private float restCombatHeal;
    private int restCombatEnergy;
    private bool inChoice, traveled, restCombat;

    public GameUI UI => ui;
    public TextBuilder Text => text;

    private void Awake()
    {
        ui = GetComponent<GameUI>();
        mainText = transform.Find("Text").GetComponent<TMP_Text>();
        shopScreen = transform.Find("Shop").GetComponent<ShopScreen>();
        characterScreen = transform.Find("Character").GetComponent<CharacterScreen>();
        journal = transform.Find("Journal").GetComponent<Journal>();
        allyScreen = transform.Find("Ally").GetComponent<AllyScreen>();
        giveItemsScreen = transform.Find("GiveItems").GetComponent<GiveItemsScreen>();
        propertiesScreen = transform.Find("Properties").GetComponent<PropertiesScreen>();
        guildScreen = transform.Find("Guild").GetComponent<GuildScreen>();
        gardenScreen = transform.Find("Garden").GetComponent<GardenScreen>();
        craftScreen = transform.Find("Craft").GetComponent<CraftScreen>();
        combatScreen = transform.Find("Combat").GetComponent<Combat>();
        combatScreen.Init();
        map = transform.Find("Map").GetComponent<Map>();
        map.Init();
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

    private void OnEnable()
    {
        GameDialog.game = this;
        GameDialog.ui = ui;
        GameDialog.player = player;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif

        if (ui.HasDialog)
            return;

        if (inChoice)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                PickChoice(true);
            if (Input.GetKeyDown(GameUI.escKey))
                PickChoice(false);
        }
        else
        {
            if (team.heroes.Count >= 2 && Input.GetKeyDown(KeyCode.Alpha1))
                allyScreen.Show(0);
            if (team.heroes.Count >= 3 && Input.GetKeyDown(KeyCode.Alpha2))
                allyScreen.Show(1);
            if (Input.GetKeyDown(KeyCode.C))
                characterScreen.Show();
            if (Input.GetKeyDown(KeyCode.J))
                journal.Show();
            if (Input.GetKeyDown(KeyCode.E))
                Explore();
            if (Input.GetKeyDown(KeyCode.R))
                Rest();
            if (world.level == 0 && world.Location != TileType.DarkDimension)
            {
                if (Input.GetKeyDown(KeyCode.T))
                    map.Show();
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
                    guildScreen.Show();
                if (Input.GetKeyDown(KeyCode.P))
                    propertiesScreen.Show();
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.S))
                    shopScreen.Show();
                if (Input.GetKeyDown(KeyCode.X))
                    EnterSewers();
                if (Input.GetKeyDown(KeyCode.H) && (player.HaveProperty("House", cityIndex: world.CityIndex) || player.HaveProperty("Mansion", cityIndex: world.CityIndex)))
                    EnterHouse();
                if (Input.GetKeyDown(KeyCode.M) && player.HaveProperty("Inn", cityIndex: world.CityIndex))
                    propertiesScreen.ShowManage();
                break;
            case TileType.Village:
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.S))
                    shopScreen.Show();
                if (Input.GetKeyDown(KeyCode.P))
                    propertiesScreen.Show();
                if (Input.GetKeyDown(KeyCode.H) && (player.HaveProperty("House", cityIndex: world.CityIndex) || player.HaveProperty("Mansion", cityIndex: world.CityIndex)))
                    EnterHouse();
                if (Input.GetKeyDown(KeyCode.M) && player.HaveProperty("Inn", cityIndex: world.CityIndex))
                    propertiesScreen.ShowManage();
                break;
            case TileType.Forest:
                if (Input.GetKeyDown(KeyCode.F))
                    Forage();
                break;
            case TileType.Sewers:
                if (Input.GetKeyDown(KeyCode.X))
                    ExitToCity();
                break;
            case TileType.House:
            case TileType.Mansion:
                if (Input.GetKeyDown(KeyCode.X))
                    ExitToCity();
                if (Input.GetKeyDown(KeyCode.C) && ((world.Location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab", cityIndex: world.CityIndex))
                    || (world.Location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Alchemy lab", cityIndex: world.CityIndex))))
                    craftScreen.Show();
                if (Input.GetKeyDown(KeyCode.K))
                    Cook();
                if (Input.GetKeyDown(KeyCode.M) && world.Location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Office"))
                    propertiesScreen.Show();
                if (Input.GetKeyDown(KeyCode.G) && ((world.Location == TileType.House && player.HavePropertyUpgrade("House", "Garden", cityIndex: world.CityIndex))
                    || (world.Location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Garden", cityIndex: world.CityIndex))))
                    gardenScreen.Show();
                break;
            case TileType.Sawmill:
            case TileType.Mine:
            case TileType.Farm:
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.M) && player.HaveProperty(world.CurrentLocationIndex))
                    propertiesScreen.ShowManage();
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
        text.Clear();

        if (player.energy < (isSmall ? 5 : 10))
        {
            text.Append("You are too tired to explore.");
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
            text.Append($"You explore the {tile.Name} and find <b>treasure room</b>. Inside chest you find <color=#FFD700>{gold}</color> gold and <b>{item.name}</b>.");
            Quest quest = activeQuests.FirstOrDefault(x => x.type == Quest.Type.Artifact && x.location == world.CurrentLocationIndex);
            if (quest != null)
            {
                quest.count = 1;
                text.Append("You also find an <b>artifact</b>.");
            }
            else
            {
                quest = availableQuests.FirstOrDefault(x => x.type == Quest.Type.Artifact && x.location == world.CurrentLocationIndex);
                if (quest != null)
                    availableQuests.Remove(quest);
            }
            team.AddGold(gold);
            player.AddItem(item, team: team.heroes.Count > 1);
            tile.foundTreasure = true;
        }
        else if (world.level + 1 < tile.levels && tile.foundLevel == world.level && tile.defeatedEnemies >= 10)
        {
            text.Append($"You find stairs leading to level {world.level + 2}.");
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
            text.Append($"You explore the {tile.Name} and find old camp. You pick up <b>{Utility.Plural("rations", count)}</b>.");
        }
        else if (c == 8 && tile.type == TileType.Dungeon && (!tile.foundTreasure || Utility.Rand % 2 == 0))
        {
            // trap
            Hero target = team.heroes.RandomItem();
            text.Append(target == player
                ? $"You explore the {tile.Name} and step on a <color=red>trap</color>."
                : $"You explore the {tile.Name} and {target.name} step on a <color=red>trap</color>.");
            if (tile.difficulty == 1)
            {
                if (Combat.AttackChance(10, target.dex))
                {
                    target.hp -= Utility.Random(20, 25);
                    if (target.hp < 1)
                        target.hp = 1;
                    text.Append($"A shooting arrow hits {target.him}.");
                    if (target != player)
                        target.ApplyHealing();
                }
                else
                    text.Append($"{target.He} {Utility.S("dodge", target != player)} a shooting arrow.");
            }
            else if (tile.difficulty == 2)
            {
                List<Hero> dodged = new(), hit = new();
                foreach (Hero hero in team.heroes)
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
                        text.Append($"{Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away " +
                            $"but {Utility.PrettyList(hit.Select(x => x.nameYou))} {Utility.S("fall", hit.Count == 1 && hit[0] != player)} into a pit.");
                    }
                    else
                    {
                        if (dodged.Count == team.heroes.Count && dodged.Count > 1)
                            text.Append("Everone jump away from a pit.");
                        else
                            text.Append($"{Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away from a pit.");
                    }
                }
                else
                {
                    if (hit.Count == team.heroes.Count && hit.Count > 1)
                        text.Append("Everyone fall into a pit.");
                    else
                        text.Append($"{Utility.PrettyList(hit.Select(x => x.nameYou)).ToUpper1()} {Utility.S("fall", hit.Count == 1 && hit[0] != player)} into a pit.");
                }
            }
            else
            {
                List<Hero> dodged = new(), hit = new();
                foreach (Hero hero in team.heroes)
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
                        text.Append($"{Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away " +
                            $"but {Utility.PrettyList(hit.Select(x => x.nameYou))} {Utility.S("are", hit.Count == 1 && hit[0] != player, "is")} caught in an explosion.");
                    }
                    else
                    {
                        if (dodged.Count == team.heroes.Count && dodged.Count > 1)
                            text.Append("Everone jump away from an explosion.");
                        else
                            text.Append($"{Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away from an explosion.");
                    }
                }
                else
                {
                    if (hit.Count == team.heroes.Count && hit.Count > 1)
                        text.Append("Everyone are caught in an explosion.");
                    else
                        text.Append($"{Utility.PrettyList(hit.Select(x => x.nameYou)).ToUpper1()} {Utility.S("are", hit.Count == 1 && hit[0] != player, "is")} caught in an explosion.");
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
            team.AddGold(gold);
            player.AddItem(Item.Get(item), count);
            text.Append($"You explore the {tile.Name} and find chest. Inside you find <b>{Utility.Plural(item, count)}</b> and <color=#FFD700>{gold}</color> gold.");
        }
        else if (c == 9 && tile.type == TileType.Forest && tile.depleted < 4)
        {
            // herbs/rare herbs
            (Hero bestHero, int bestValue) = team.GetSkill(Skill.Forage);
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
                text.Append($"You explore the {tile.Name} and with {bestHero.name} help find <b>{Utility.Plural(herb.name, count)}</b>.");
                player.Train(Skill.Forage, text, 0.25f * trainMod);
                bestHero.Train(Skill.Forage, null, 0.25f);
            }
            else
            {
                text.Append($"You explore the {tile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.");
                player.Train(Skill.Forage, text, 0.25f);
            }
        }
        else if (c == 9 && ((tile.type == TileType.Mountains && tile.depleted == 0) || (tile.type == TileType.Cave && tile.mine && tile.depleted < 4)) && tile.difficulty >= 2)
        {
            // silver/gold nuggets
            if (team.HaveItem("pickaxe"))
            {
                (Hero bestHero, int bestValue) = team.GetSkill(Skill.Mining);
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
                player.AddItem(nugget, count, team.heroes.Count > 1);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Mining));
                    text.Append($"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. " +
                        $"You and {bestHero.name} mine <b>{Utility.Plural(nugget.name, count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, null, 0.25f);
                }
                else
                {
                    text.Append($"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. You mine <b>{Utility.Plural(nugget.name, count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f);
                }
            }
            else
                text.Append($"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b> but you don't have a pickaxe...");
        }
        else if (c == 9 && tile.type == TileType.Cave && !tile.mine && !tile.boss && tile.depleted < tile.difficulty + 2)
        {
            // magic crystals
            if (team.HaveItem("pickaxe"))
            {
                (Hero bestHero, int bestValue) = team.GetSkill(Skill.Mining);
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
                    text.Append($"You explore the {tile.Name} and find small <b>magic crystals cluster</b>. You and {bestHero.name} mine <b>{Utility.Plural("magic crystal", count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, null, 0.25f);
                }
                else
                {
                    text.Append($"You explore the {tile.Name} and find small <b>magic crystals cluster</b>. You mine <b>{Utility.Plural("magic crystal", count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f);
                }
            }
            else
                text.Append($"You explore the {tile.Name} and find small <b>magic crystals cluster</b> but you don't have a pickaxe...");
        }
        else
            text.Append($"You explore the {tile.Name} but find nothing interesting.");

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
        if (tile.boss && !restCombat && tile.difficulty == 3)
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
        else if (tile.boss && !restCombat && tile.difficulty == 2 && tile.defeatedEnemies >= 10 && spiderStatus < SpiderStatus.Defeated)
        {
            enemy = Enemy.Get("spider queen");
            enemyList.Add(enemy);
            enemy = Enemy.Get("giant spider");
            for (int i = 0; i < 2; ++i)
                enemyList.Add(enemy);
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
            combatScreen.Init(enemyList, text.Flush(), true);
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
                gold += enemy.extraGold;
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
                player.AddItem(itemSlot.item, itemSlot.count, team.heroes.Count > 1 && itemSlot.item.subtype == Item.Subtype.Treasure);
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
                text.Set("With a final blow, the dragon falls. Its roar fades into silence, and the cavern grows still. The beast is slain—its hoard and your legend now yours to claim. " +
                    $"You found {pickups}.");
                Quest quest = activeQuests.FirstOrDefault(x => x.type == Quest.Type.UniqueDragon);
                if (quest != null)
                    RemoveQuest(quest);
                tile.clear = true;
                tile.timer = 0;
                team.ChangeAffection(10, text);
                foreach (Hero hero in team.heroes)
                    hero.winToday = true;
            }
            else
            {
                if (enemyList.Any(x => x.name == "spider queen"))
                {
                    tile.clear = true;
                    tile.timer = 0;
                    if (spiderStatus == SpiderStatus.Accepted)
                    {
                        spiderStatus = SpiderStatus.Defeated;
                        Quest quest = activeQuests.First(x => x.type == Quest.Type.UniqueSpider);
                        quest.location = world.cityMapping[1];
                        quest.count = 1;
                    }
                    else
                        spiderStatus = SpiderStatus.Skipped;
                }

                if (pickups != null)
                    text.Set($"You win a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> ({pickups} found).");
                else
                    text.Set($"You win a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b>.");
                team.ChangeAffection(1, text, hero =>
                {
                    if (hero.winToday)
                        return false;
                    hero.winToday = true;
                    return true;
                });
            }
            team.AddGold(gold);

            // exp
            List<Hero> levelups = null;
            float ratio;
            if (team.heroes.Count == 1)
                ratio = 1f;
            else
                ratio = 1f / team.heroes.Count;
            foreach (Hero hero in team.heroes)
            {
                if (hero.AddExp(enemyList, ratio))
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
                    text.Append($"{Utility.PrettyList(group.Select(x => x.nameYou)).ToUpper1()} {isAre} now level {group.Key}.");
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
                            text.Append("You can build a <b>mine</b> here.");
                        }
                    }
                }
                else if (tile.type == TileType.Mine || tile.type == TileType.Sawmill || tile.type == TileType.Farm)
                {
                    tile.timer = 0;
                    text.Append("You <b>cleared</b> this place.");
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
                text.Set($"You run away from {Utility.PrettyGroup(enemyList.Select(x => x.name))}.");
                team.ChangeAffection(-1, text);
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
                    goldTaken = team.RemoveGold(Utility.Round(goldTaken));
                if (rationsTaken > 0)
                    rationsTaken = team.RemoveItem(Item.Get("rations"), rationsTaken);

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
                    text.Set($"You <color=red>lost</color> a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b>.");
                else
                    text.Set($"You <color=red>lost</color> a fight with <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> ({lost}).");

                team.ChangeAffection(-5, text);
            }

            if (enemyList.Any(x => x.name == "dragon" || x.name == "spider queen"))
                tile.defeatedEnemies -= 5;
            if (tile.type == TileType.DarkDimension && enemyList.Any(x => x.name == "nameless horror"))
                tile.defeatedEnemies = 0;
        }

        // heal after combat
        foreach (Hero hero in team.heroes)
        {
            hero.bored = 0;
            if (hero.hp < 1)
                hero.hp = 1;
            if (hero is not Player)
                hero.ApplyHealing();
        }

        if (restCombat)
        {
            if (result == Combat.Result.Win)
            {
                if (restCombatHeal != 0)
                {
                    foreach (Hero hero in team.heroes)
                        hero.hp = Mathf.Min(hero.hp + (int)(restCombatHeal * hero.hpMax), hero.hpMax);
                }
                player.energy = Mathf.Min(player.energy + restCombatEnergy, 100);
                text.Append("You finish your rest.");
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
        text.Clear();
        if (OnRest())
        {
            text.Append("It's a new day.");
            UpdateText();
        }
    }

    public void Work()
    {
        if (hour > 16)
            text.Set("It's too late to work.");
        else if (player.energy < 50)
            text.Set("You are too tired to work.");
        else if (!world.CurrentTile.clear && world.Location.IsClearable())
            text.Set($"You can't work while monsters occupy the {world.CurrentTile.Name}.");
        else
        {
            DoWork();
            AddTime(hours: 8);
        }
        UpdateText();
    }

    public int DoWork(bool skipTime = false)
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
            (bestHero, skillValue) = team.GetSkill(skill);
            payment += skillValue / 10;
        }
        else
        {
            bestHero = null;
            skillValue = 0;
        }
        // double pay if owned
        if (player.HaveProperty(world.CurrentLocationIndex))
            payment *= 2;
        // give payment & train all team members
        if (!skipTime)
            text.Set($"You earned <color=#FFD700>{payment}</color> gold from working.");
        foreach (Hero hero in team.heroes)
        {
            float trainMod;
            if (skill != Skill.None && bestHero != null && bestHero != hero)
                trainMod = 1f + 0.01f * (skillValue - hero.GetSkill(skill));
            else
                trainMod = 1f;

            hero.AddGold(payment);
            if (skill != Skill.None)
                player.Train(skill, hero == player && !skipTime ? text : null, trainMod);
        }
        return payment;
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
            text.Set("You are too tired to travel.");
            UpdateText();
            return;
        }

        player.energy -= 5;
        text.Set("You enter the sewers.");
        world.sublocation = 1;
        AddTime(minutes: 30);
        OnChangeLocation();
    }

    public void EnterHouse()
    {
        if (player.HaveProperty("House", cityIndex: world.CityIndex))
        {
            text.Set("You enter your house.");
            world.sublocation = 2;
        }
        else
        {
            text.Set("You enter your mansion.");
            world.sublocation = 3;
        }
        AddTime(minutes: 5);
        OnChangeLocation();
    }

    public void ExitToCity()
    {
        if (world.sublocation == 1 && player.energy < 5)
        {
            text.Set("You are too tired to travel.");
            UpdateText();
            return;
        }

        text.Set($"You exit to the {(world.RealLocation == TileType.Village ? "village" : "city")}.");
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
        text.Set($"You go upstairs to level {world.level + 1}.");
        AddTime(minutes: 30);
        UpdateText();
        UpdateButtons();
    }

    public void GoDown()
    {
        ++world.level;
        text.Set($"You go downstairs to level {world.level + 1}.");
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
            text.Set("You return to the city as a hero. The Adventurer’s Guild erupts in cheers, mugs raised high in your honor. " +
                "Songs of your victory begin to spread, and your name will not be forgotten.");
        }
        else if (tile.type == TileType.Village && world.CityIndex == 1 && spiderStatus == SpiderStatus.Defeated)
        {
            spiderStatus = SpiderStatus.Rewarded;
            RemoveQuest(activeQuests.First(x => x.type == Quest.Type.UniqueSpider));
            Property inn = properties.First(x => x.name == "Inn" && x.cityIndex == 1);
            player.properties.Add(inn);
            properties.Remove(inn);
            team.PayForProperty(player, inn.value / 2);
            text.Set($"You travel to the {tile.Name}. Inn owner is thankful for defeating the spider queen and hands over the deed to <b>inn</b>.");
        }
        else
            text.Set($"You travel to the {tile.Name}.");

        if (tile.boss)
        {
            if (tile.difficulty == 3)
                text.Append("There are <b>dragon engravings</b> near entrance.");
            else
                text.Append("There are more <b>cobwebs</b> then in an usual cave.");
        }
        else if (tile.mine && tile.type == TileType.Cave)
            text.Append($"There are <b>{(tile.difficulty == 2 ? "silver" : "gold")} veins</b> inside this cave.");

        Property property = player.properties.FirstOrDefault(x => x.status == Property.Status.Building && x.locationIndex == world.CurrentLocationIndex);
        if (property != null)
            text.Append($"{property.name} is being build here.");
        if ((tile.type == TileType.City || tile.type == TileType.Village) && team.heroes.Skip(1).Any(x => (x.affection <= -25 && !x.complained) || x.affection <= -50))
        {
            Hero[] complainers = team.heroes.Skip(1).Where(x => x.affection <= -25 && !x.complained).ToArray();
            Hero[] quitters = team.heroes.Skip(1).Where(x => x.affection <= -50 && x.complained).ToArray();
            if (complainers.Length > 0)
            {
                foreach (Hero hero in complainers)
                    hero.complained = true;
                text.Append($"{Utility.PrettyList(complainers.Select(x => x.name))} <b>{Utility.S("complain", complainers.Length == 1)}</b> about your lidership.");
            }
            if (quitters.Length > 0)
            {
                foreach (Hero hero in quitters)
                    team.heroes.Remove(hero);
                team.CancelOutDebts();
                text.Append($"{Utility.PrettyList(quitters.Select(x => x.name))} <color=red>{Utility.S("leave", quitters.Length == 1)}</color> your party.");
            }
        }
        OnChangeLocation();
    }

    private void OnChangeLocation()
    {
        team.CheckBoredAllies(text);

        Tile tile = world.CurrentTile;

        if ((tile.type == TileType.City || tile.type == TileType.Village) && player.HaveProperty("Horses") && player.HavePropertyUpgrade("Mansion", "Stables", world.CityIndex))
            team.freshHorses = 10;

        if (tile.type.IsSafe())
        {
            if (player.goldWaiting != 0)
            {
                text.Append(player.goldWaiting > 0
                    ? $"You receive <color=#FFD700>{player.goldWaiting}</color> gold from your properties."
                    : $"You pay <color=#FFD700>{-player.goldWaiting}</color> gold for your properties.");
                player.AddGold(player.goldWaiting);
                player.goldWaiting = 0;
            }

            foreach (Hero ally in team.heroes.Skip(1))
                ally.BuyItems();

            foreach (Notification notification in notifications.Where(x => x.status == Notification.Status.Waiting))
                notification.status = Notification.Status.Available;
        }
        else if (tile.type == TileType.MageTower)
        {
            foreach (Hero ally in team.heroes.Skip(1))
                ally.EnchantItems();
        }

        ui.UpdateBackground((int)tile.type);
        UpdateButtons();
        UpdateText();
        traveled = false;
    }

    public void UpdateText()
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
        string lastAction = text.Flush();
        if (!string.IsNullOrEmpty(lastAction))
        {
            sb.Append('\n');
            sb.Append(lastAction);
        }
        mainText.text = sb.ToString();

        // allies health
        for (int i = 1; i < team.heroes.Count; ++i)
        {
            float hp = team.heroes[i].hpp;
            if (hp < 0)
                hp = 0;
            else if (hp > 0 && hp < 0.01f)
                hp = 0.01f;
            alliesHealthRect[i - 1].sizeDelta = new Vector2(156f * hp, 5f);
        }
    }

    public void AddTime(int hours = 0, int minutes = 0)
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
            text.Append("It's a new day.");
            OnRest();
        }
    }

    public bool OnRest(bool skipTime = false)
    {
        void FullRest()
        {
            player.energy = 100;
            foreach (Hero hero in team.heroes)
                hero.hp = hero.hpMax;
        }

        TileType location = world.Location;
        int cityIndex = world.CityIndex;
        if (((location == TileType.City || location == TileType.Village) && player.HaveProperty("House", cityIndex: cityIndex)) || location == TileType.House)
        {
            FullRest();
            if (!skipTime)
                text.Append("You rest in your house.");
        }
        else if (((location == TileType.City || location == TileType.Village) && player.HaveProperty("Mansion", cityIndex: cityIndex)) || location == TileType.Mansion)
        {
            FullRest();
            foreach (Hero hero in team.heroes)
                hero.rested = 11;
            if (player.HaveProperty("Horses") && player.HavePropertyUpgrade("Mansion", "Stables", cityIndex: cityIndex))
                team.freshHorses = 11;
            if (!skipTime)
                text.Append("You rest in your mansion.");
        }
        else if ((location == TileType.City || location == TileType.Village) && player.HaveProperty("Inn", cityIndex: cityIndex))
        {
            FullRest();
            if (!skipTime)
                text.Append("You rest in your inn.");
        }
        else if ((location == TileType.City || location == TileType.Village) && player.gold > 0)
        {
            FullRest();
            foreach (Hero hero in team.heroes)
                hero.AddGold(-1);
            if (!skipTime)
                text.Append("You rest in an inn (<color=#FFD700>-1</color> gold).");
        }
        else if (location == TileType.Sawmill || location == TileType.Mine || location == TileType.Farm)
        {
            FullRest();
            if (!skipTime)
                text.Append("You rest in a barracks.");
        }
        else if (location == TileType.MageTower)
        {
            FullRest();
            if (!skipTime)
                text.Append("You rest in a guest room.");
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
            int count = team.heroes.Count;
            int eaten = team.RemoveItem(rations, count);
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
                if (!skipTime)
                    text.Append($"You rest {where} and eat rations.");
            }
            else
            {
                if (!skipTime)
                    text.Append($"You rest {where}.");
                heal = 0;
            }

            bool attacked = false;
            if (!skipTime && !world.CurrentTile.clear)
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
                    foreach (Hero hero in team.heroes)
                        hero.hp = Mathf.Min(hero.hp + (int)(heal * hero.hpMax), hero.hpMax);
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
                    foreach (Hero hero in team.heroes)
                        hero.hp = hero.hpMax;
                }
                else if (heal > 0)
                {
                    foreach (Hero hero in team.heroes)
                        hero.hp = Mathf.Min(hero.hp + (int)(heal * hero.hpMax), hero.hpMax);
                }
                player.energy = Mathf.Min(player.energy + energy, 100);
            }
        }

        ++day;
        hour = 8;
        minute = 0;

        OnNewDay();

        if (!skipTime)
        {
            team.CheckBoredAllies(text);

            if (player.goldWaiting != 0 && location.IsSafe())
            {
                text.Append(player.goldWaiting > 0
                    ? $"You receive <color=#FFD700>{player.goldWaiting}</color> gold from your properties."
                    : $"You pay <color=#FFD700>{-player.goldWaiting}</color> gold for your properties.");
                player.AddGold(player.goldWaiting);
                player.goldWaiting = 0;
            }
        }

        ui.CloseDialogs(dialog =>
        {
            if (dialog.TryGetComponent(out GameDialog gameDialog))
                return gameDialog.Autoclose;
            return false;
        });

        return true;
    }

    public void OnNewDay()
    {
        player.goldWaiting += player.properties
            .Where(p => p.status == Property.Status.Active)
            .Sum(p =>
            {
                int profit = p.Profit;
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
                    }
                    else
                    {
                        --even.timer;
                        if (even.timer == 0)
                            p.events.Clear();
                    }
                }
                return profit;
            });
        player.goldWaiting -= hiredWorkers.Count * 2;

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
                    text.Append($"{property.name} has been built.");
                }
                AddNotification($"The construction of {property.Name.ToLower()} has been completed.");
            }
        }

        UpdateQuests();

        if (day % 10 == 0)
            GenerateWorkers();

        // grow garden plants
        foreach (Property property in player.properties.Where(x => x.gardenPlants != null && x.gardenPlants.Count > 0))
        {
            foreach (var plant in property.gardenPlants.GroupBy(x => x).Select(x => (name: x.Key, count: x.Count())))
            {
                switch (plant.name)
                {
                case "Vegetables":
                    property.AddStoredItem(Item.Get("rations"), plant.count);
                    break;
                case "Herbs":
                    property.AddStoredItem(Item.Get("herb"), plant.count);
                    break;
                case "Rare herbs":
                    property.AddStoredItem(Item.Get("rare herb"), plant.count);
                    break;
                }
            }
        }

        team.OnNewDay();

        UpdateWorkers();

        world.Update();

        if (day % 10 == 0)
        {
            // decrease efficiency if none manage property
            foreach (Property property in player.properties.Where(x => x.income > 0 && x.status == Property.Status.Active && (day - x.lastManaged) >= 10))
            {
                int maxDecrease = 1 + property.efficiency / 20;
                property.efficiency -= Utility.Random(1, maxDecrease);
                if (property.efficiency < 1)
                    property.efficiency = 1;
            }

            // property events
            foreach (Property property in player.properties.Where(x => x.income > 0 && x.status == Property.Status.Active))
            {
                if (property.events != null && property.events.Count > 0)
                    continue;

                (int buffChance, int infestChance) = property.EventChances;
                int c = Utility.Rand % 100;
                if (c < buffChance)
                {
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
                else if (c < buffChance + infestChance && property.locationIndex != -1 && !property.HaveUpgrade("Extra guards"))
                {
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

    private void NewGame()
    {
        Global global = Global.Instance;
        player = new() { name = global.playerName, clas = global.playerClass, race = global.playerRace, female = global.playerFemale };
        player.Init();
        team = new() { heroes = new() { player } };
        world = new();
        world.Init();
        map.Build();
        day = 1;
        hour = 8;
        activeQuests = new()
        {
            new()
            {
                type = Quest.Type.UniqueDragon,
                location = -1
            }
        };
        availableQuests = new();
        availableWorkers = new();
        hiredWorkers = new();
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
        GenerateWorkers();
        text.Set("You are an adventurer seeking glory and gold. Rumors speak of a dragon lurking deep within a forgotten cave beyond the wilds. " +
            "Find its lair, face the beast, and carve your name into legend.");
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

    public void UpdateButtons()
    {
        TileType location = world.Location;
        int cityIndex = world.CityIndex;
        bool inCity = location == TileType.City;
        bool inVillage = location == TileType.Village;
        Transform buttons = transform.Find("Buttons");
        buttons.Find("BtShop").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtWork").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtGuild").gameObject.SetActive(inCity);
        buttons.Find("BtProperties").gameObject.SetActive(inCity || inVillage || (location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Office")));
        buttons.Find("BtSewers").gameObject.SetActive(inCity);
        buttons.Find("BtRecruit").gameObject.SetActive(inCity || inVillage);
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
        buttons.Find("BtSkipTime").gameObject.SetActive(world.CurrentTile.CanSkipTime());

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

        button = buttons.Find("BtManage");
        Property property = GetPropertyHere();
        if (property == null)
            button.gameObject.SetActive(false);
        else
        {
            string propertyNameShort;
            if (location == TileType.City || location == TileType.Village)
                propertyNameShort = "inn";
            else
                propertyNameShort = location.AsString();
            button.GetComponentInChildren<TMP_Text>().text = $"Manage {propertyNameShort}";
            button.gameObject.SetActive(true);
        }

        buttons.Find("BtQuest").gameObject.SetActive(cityIndex == 1 && spiderStatus == SpiderStatus.None);

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
        if (team.heroes.Count < 2)
            btAlly.SetActive(false);
        else
        {
            btAlly.GetComponentInChildren<TMP_Text>().text = team.heroes[1].name;
            btAlly.SetActive(true);
        }
        btAlly = buttons.Find("BtAlly2").gameObject;
        if (team.heroes.Count < 3)
            btAlly.SetActive(false);
        else
        {
            btAlly.GetComponentInChildren<TMP_Text>().text = team.heroes[2].name;
            btAlly.SetActive(true);
        }
    }

    public Hero SpawnHero(int level = 0)
    {
        Hero hero = new();
        hero.Init(level);
        hero.name = GetUniqueName(hero.female);
        return hero;
    }

    private string GetUniqueName(bool female)
    {
        string[] names = female ? Names.femaleNames : Names.maleNames;
        while (true)
        {
            string name = names.RandomItem();
            if (!team.heroes.Select(x => x.name).Union(hiredWorkers.Select(x => x.name)).Union(availableWorkers.Select(x => x.name)).Contains(name))
                return name;
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
                    quest.location = -1;
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
                    quest.location = -1;
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
                    quest.location = -1;
                    quest.locationDifficulty = 2;
                    quest.difficultyMod = 0.25f;
                    break;
                default:
                    // 37.5%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 2 + difficulty);
                    quest.location = -1;
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
                    tile.clear = false;
                }
                return quest;
            }
        }
    }

    public void RemoveQuest(Quest quest)
    {
        bool isTracked = quest.tracked;
        activeQuests.Remove(quest);
        if (isTracked)
        {
            quest = activeQuests.FirstOrDefault(x => x.type != Quest.Type.UniqueDragon);
            if (quest != null)
                quest.tracked = true;
        }
    }

    // forage or prospect
    public void Forage()
    {
        Tile tile = world.CurrentTile;

        if (tile.type == TileType.Cave && !team.HaveItem("pickaxe"))
        {
            text.Set("You need a pickaxe for that.");
            UpdateText();
            return;
        }

        int energy = tile.type == TileType.Forest ? 10 : 5;
        if (player.energy < energy)
        {
            text.Set($"You are too tired to {(tile.type == TileType.Forest ? "forage" : "prospect")}.");
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
                text.Set($"You forage in the {tile.Name} but find nothing of value.");
            else
            {
                // herbs/rare herbs
                (Hero bestHero, int bestValue) = team.GetSkill(Skill.Forage);
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
                    text.Set($"You forage the {tile.Name} and with {bestHero.name} help find <b>{Utility.Plural(herb.name, count)}</b>.");
                    if (Utility.Rand % 100 < bestValue)
                    {
                        text.Append($"You also find some edible {(Utility.Rand % 2 == 0 ? "fruits" : "vegetables")} (<b>+1 rations</b>).");
                        player.AddItem(Item.Get("rations"));
                    }
                    player.Train(Skill.Forage, text, 0.25f * trainMod);
                    bestHero.Train(Skill.Forage, null, 0.25f);
                }
                else
                {
                    text.Set($"You forage the {tile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.");
                    if (Utility.Rand % 100 < bestValue)
                    {
                        text.Append($"You also find some edible {(Utility.Rand % 2 == 0 ? "fruits" : "vegetables")} (<b>+1 rations</b>).");
                        player.AddItem(Item.Get("rations"));
                    }
                    player.Train(Skill.Forage, text, 0.25f);
                }
            }
            AddTime(hours: 1);
        }
        else
        {
            if (tile.boss || (tile.mine && tile.depleted >= 4) || (!tile.mine && tile.depleted >= tile.difficulty + 2))
                text.Set($"You prospect the {tile.Name} but find nothing of value.");
            else if (tile.mine)
            {
                // silver/gold nuggets
                (Hero bestHero, int bestValue) = team.GetSkill(Skill.Mining);
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
                player.AddItem(nugget, count, team.heroes.Count > 1);
                if (bestHero != null && bestHero != player)
                {
                    float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Mining));
                    text.Set($"You prospect the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. " +
                        $"You and {bestHero.name} mine <b>{Utility.Plural(nugget.name, count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, null, 0.25f);
                }
                else
                {
                    text.Set($"You prospect the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. You mine <b>{Utility.Plural(nugget.name, count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f);
                }
            }
            else
            {
                // magic crystals
                (Hero bestHero, int bestValue) = team.GetSkill(Skill.Mining);
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
                    text.Set($"You prospect the {tile.Name} and find small <b>magic crystals cluster</b>. You and {bestHero.name} mine <b>{Utility.Plural("magic crystal", count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f * trainMod);
                    bestHero.Train(Skill.Mining, null, 0.25f);
                }
                else
                {
                    text.Set($"You prospect the {tile.Name} and find small <b>magic crystals cluster</b>. You mine <b>{Utility.Plural("magic crystal", count)}</b>.");
                    player.Train(Skill.Mining, text, 0.25f);
                }
            }
            AddTime(minutes: 30);
        }

        UpdateText();
    }

    public void DoAlchemy(Hero hero, Item item)
    {
        (Hero bestHero, int alchemy) = team.GetSkill(Skill.Alchemy);
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
        int extra = (int)(count * CraftScreen.GetAlchemyCountBonus(alchemy));
        int totalCount = count + extra;
        player.AddItem(recipe.result, totalCount);
        if (hero == bestHero)
        {
            text.Set($"{hero.name} creates {Utility.Plural(recipe.result.name, totalCount)} and gives {(totalCount == 1 ? "it" : "them")} to you.");
            hero.Train(Skill.Alchemy, null, recipe.trainMod * count);
        }
        else
        {
            text.Set($"{hero.name} and {bestHero.nameYou} create {Utility.Plural(recipe.result.name, totalCount)}. You receive {(totalCount == 1 ? "it" : "them")}.");
            float trainMod = 1f + 0.01f * (alchemy - bonus - hero.GetSkill(Skill.Alchemy));
            hero.Train(Skill.Alchemy, null, recipe.trainMod * trainMod * count);
            bestHero.Train(Skill.Alchemy, bestHero == player ? text : null, recipe.trainMod * count);
        }

        giveItemsScreen.RefreshIfOpen();
        UpdateText();
    }

    public void Choice(string str, System.Action<bool> action)
    {
        choiceAction = action;
        text.Set(str);
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
        // remember properties assigned to workers
        Dictionary<Worker, Property> workerPropertyMap = new();
        foreach (Worker worker in hiredWorkers)
        {
            Property property = GetProperty(worker.locationIndex);
            if (property != null)
                workerPropertyMap[worker] = property;
        }

        world.Init();

        // reassign property locationIndex, update map with built properties
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

        // reassign workers properties
        foreach ((Worker worker, Property property) in workerPropertyMap)
            worker.locationIndex = GetLocationIndex(property);

        // remove quests for locations
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
        guildScreen.RefreshIfOpen();
    }

    [ContextMenu("Give all")]
    private void GiveAll()
    {
        while (team.heroes.Count < Team.MaxSize)
            team.heroes.Add(SpawnHero());

        foreach (Hero hero in team.heroes)
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
            text.Set($"You cooked {count} pieces of meat into rations.");
            AddTime(minutes: count * 5);
            guildScreen.RefreshIfOpen();
            UpdateText();
            return true;
        });
    }

    public void EnterPortal()
    {
        if (dragonStatus == DragonStatus.None)
        {
            text.Set("The portal is sealed by dragon seal.");
            UpdateText();
            return;
        }

        if (world.sublocation != 4)
        {
            text.Set("You enter the portal and arrive in dark dimension.");
            world.sublocation = 4;
        }
        else
        {
            text.Set("You enter the portal and arrive back in mage tower.");
            world.sublocation = 0;
        }
        OnChangeLocation();
        AddTime(minutes: 15);
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

        if (enemyList.Count > Team.MaxSize)
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

    public Property GetPropertyInside()
    {
        TileType location = world.Location;
        if (location == TileType.House)
            return player.properties.First(x => x.name == "House" && x.cityIndex == world.CityIndex);
        else if (location == TileType.Mansion)
            return player.properties.First(x => x.name == "Mansion" && x.cityIndex == world.CityIndex);
        else
            return null;
    }

    public int GetLocationIndex(Property property)
    {
        if (property.name == "Inn")
            return world.cityMapping[property.cityIndex];
        else
            return property.locationIndex;
    }

    public Property GetProperty(int locationIndex)
    {
        if (locationIndex == -1)
            return null;
        Property property = player.properties.FirstOrDefault(x => x.locationIndex == locationIndex);
        if (property == null)
        {
            int cityIndex = world.cityMapping.IndexOf(locationIndex);
            if (cityIndex != -1)
                property = player.properties.FirstOrDefault(x => x.name == "Inn" && x.cityIndex == cityIndex);
        }
        return property;
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
        if (availableQuests.Count < 9)
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
                    if (Utility.Rand % 3 == 0)
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

    public void SortQuests()
    {
        availableQuests.Sort((a, b) =>
        {
            int result = a.difficulty.CompareTo(b.difficulty);
            if (result != 0)
                return result;
            return a.timer.CompareTo(b.timer);
        });
    }







    public Property GetPropertyHere()
    {
        return world.Location switch
        {
            TileType.City or TileType.Village => player.properties.FirstOrDefault(x => x.name == "Inn" && x.cityIndex == world.CityIndex),
            TileType.Sawmill or TileType.Mine or TileType.Farm => player.properties.FirstOrDefault(x => x.locationIndex == world.CurrentLocationIndex),
            _ => null
        };
    }

    private void GenerateWorkers()
    {
        availableWorkers.RemoveAll(x => Utility.Rand % 2 == 0);

        for (int cityIndex = 0; cityIndex < 3; ++cityIndex)
        {
            int count = Utility.Random(1, 2);
            if (cityIndex == 0)
                ++count;

            int currentCount = availableWorkers.Count(x => x.locationIndex == cityIndex);
            while (currentCount < count)
            {
                Worker worker = new()
                {
                    female = Utility.Rand % 2 == 0,
                    skill = RandomSkill(),
                    locationIndex = cityIndex
                };
                worker.name = GetUniqueName(worker.female);
                availableWorkers.Add(worker);
                ++currentCount;
            }
        }
    }

    private int RandomSkill()
    {
        float t = Mathf.Pow(Random.value, 2f);
        return 25 + Mathf.RoundToInt(t * 15f) * 5;
    }

    private void UpdateWorkers()
    {
        int prevDay = day - 1;
        foreach (Worker worker in hiredWorkers.Where(x => x.locationIndex != -1))
        {
            Property property = GetProperty(worker.locationIndex);
            if (property.lastManaged != prevDay)
            {
                property.efficiency = PropertiesScreen.CalculateEfficiencyChange(worker.skill, property.efficiency);
                property.lastManaged = prevDay;
                worker.Train();
            }
        }
    }

    public void ViewQuest()
    {
        Choice("You look at quest board.\n<i>I hate spiders! If you kill the spider queen I will give you my inn. Or you can buy it and I'll hire someone using that money.</i>\n" +
            "Do you accept the quest?", yes =>
        {
            if (yes)
            {
                spiderStatus = SpiderStatus.Accepted;
                Quest quest = new()
                {
                    type = Quest.Type.UniqueSpider,
                    location = world.FindLocationIndex(x => x.difficulty == 2 && x.boss)
                };
                if (!activeQuests.Any(x => x.tracked))
                    quest.tracked = true;
                activeQuests.Add(quest);
                UpdateButtons();
                text.Set("You accepted the quest to defeat the spider queen.");
            }
            else
                text.Clear();
            UpdateText();
        });
    }
}
