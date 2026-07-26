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

    private const int MaxAllies = 2;
    private const int MaxGuildRank = 4;

    private static readonly string[] GuildRanks = new[] { "None", "Copper", "Silver", "Gold", "Diamond" };

    public World world;
    public Player player;
    public List<Hero> allies;
    public List<Quest> availableQuests, activeQuests;
    public List<Property> properties;
    public List<ItemSlot> storedItems;
    public List<Notification> notifications;
    public List<string> gardenPlants;
    public DragonStatus dragonStatus;
    public float guildProgress;
    public int day, hour, minute, guildRank;

    private GameUI ui;
    private GameObject shopScreen, characterScreen, journalScreen, allyScreen, giveAllyItemsScreen, storeItemsScreen, activeInventory, propertiesScreen, guildScreen, gardenScreen, craftScreen;
    private Map map;
    private Combat combatScreen;
    private TMP_Text text;
    private Hero activeAlly;
    private Property selectedProperty;
    private readonly StringBuilder sb = new();
    private System.Action<bool> choiceAction;
    private string lastAction;
    private bool inChoice, traveled;

    public IEnumerable<Hero> Team
    {
        get
        {
            yield return player;
            foreach (Hero ally in allies)
                yield return ally;
        }
    }

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
            if (world.level == 0)
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
                if (Input.GetKeyDown(KeyCode.H) && (player.HaveProperty("House") || player.HaveProperty("Mansion")))
                    EnterHouse();
                break;
            case TileType.Village:
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.S))
                    Shop();
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
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
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
            AddTeamGold(gold);
            player.AddItem(item);
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
            int count = (Utility.Rand % 4) switch
            {
                1 or 2 => 2,
                3 => 3,
                _ => 1,
            };

            if (tile.boss)
            {
                if (tile.defeatedEnemies >= 10 && world.level + 1 == tile.levels)
                {
                    enemy = Enemy.Get("dragon");
                    count = 1;
                }
                else
                    enemy = Enemy.Get("dragon-man");
            }

            combatScreen.Init(enemy, count);
            ui.lockDialog = true;
            ui.ShowDialog(combatScreen.gameObject);
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
                    if (dodged.Count == Team.Count() && dodged.Count > 1)
                        lastAction += " Everone jump away from a pit.";
                    else
                        lastAction += $" {Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away from a pit.";
                }

                if (hit.Count > 0)
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
                    if (dodged.Count == Team.Count() && dodged.Count > 1)
                        lastAction += " Everone jump away from an explosion.";
                    else
                        lastAction += $" {Utility.PrettyList(dodged.Select(x => x.nameYou)).ToUpper1()} {Utility.S("jump", dodged.Count == 1 && dodged[0] != player)} away from an explosion.";
                }

                if (hit.Count > 0)
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
        else if (c == 9 && tile.type == TileType.Forest)
        {
            // 1-3 herbs (~1.5)
            int count = (Utility.Rand % 4) switch
            {
                1 or 2 => 2,
                3 => 3,
                _ => 1,
            };
            Item herb = tile.GetHerb();
            player.AddItem(herb, count);
            lastAction = $"You explore the {tile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.";
        }
        else if (c == 9 && (tile.type == TileType.Mountains || (tile.type == TileType.Cave && tile.mine && !tile.clear)) && tile.difficulty >= 2)
        {
            // 1-4 silver/gold nuggets (~3.16)
            if (player.HaveItem("pickaxe"))
            {
                int count = (Utility.Rand % 6) switch
                {
                    1 or 2 => 2,
                    3 or 4 => 3,
                    5 => 4,
                    _ => 1,
                };
                count += player.GetSkill(Skill.Mining) / 25;
                Item nugget = Item.Get(tile.difficulty == 2 ? "silver nugget" : "gold nugget");
                player.AddItem(nugget, count);
                lastAction = $"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b>. You mine <b>{Utility.Plural(nugget.name, count)}</b>.";
                lastAction += player.Train(Skill.Mining, 0.25f);
            }
            else
                lastAction = $"You explore the {tile.Name} and find small <b>{(tile.difficulty == 2 ? "silver" : "gold")} vein</b> but you don't have a pickaxe...";
        }
        else
            lastAction = $"You explore the {tile.Name} but find nothing interesting.";

        if (isSmall)
            AddTime(minutes: 30);
        else
            AddTime(hours: 1);
        UpdateText();
    }

    public void PostCombat(Combat.Result result, Enemy enemy, int count)
    {
        Tile tile = world.CurrentTile;

        ui.lockDialog = false;
        ui.CloseDialog();

        if (result == Combat.Result.Win)
        {
            foreach (Quest quest in activeQuests)
            {
                if (quest.type == Quest.Type.Defeat)
                {
                    if (quest.enemy == enemy)
                        quest.count += count;
                }
                else if (quest.type == Quest.Type.Clear)
                {
                    if (quest.location == world.CurrentLocationIndex)
                        quest.count += count;
                }
            }

            // gold & items
            List<string> items = new();
            int gold = 0;
            if (enemy.gold != Vector2Int.zero)
            {
                for (int i = 0; i < count; ++i)
                    gold += enemy.gold.Random();
                gold = Utility.Round(gold);
            }
            if (enemy.drops != null)
            {
                foreach (var (item, chance) in enemy.drops)
                {
                    int itemCount = (int)chance;
                    itemCount *= count;
                    float itemChance = chance - itemCount;
                    if (itemChance > 0)
                    {
                        for (int i = 0; i < count; ++i)
                        {
                            if (Utility.Random() < itemChance)
                                ++itemCount;
                        }
                    }
                    if (itemCount > 0)
                    {
                        items.Add(Utility.Plural(item.name, itemCount));
                        player.AddItem(item, itemCount);
                    }
                }
            }
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
            if (enemy.name == "dragon")
            {
                dragonStatus = DragonStatus.Defeated;
                lastAction = "With a final blow, the dragon falls. Its roar fades into silence, and the cavern grows still. The beast is slain—its hoard and your legend now yours to claim. " +
                    $"You found {pickups}.";
                Quest quest = activeQuests.FirstOrDefault(x => x.type == Quest.Type.Unique);
                if (quest != null)
                    RemoveQuest(quest);
                tile.clear = true;
                tile.timer = 0;
            }
            else if (pickups != null)
                lastAction = $"You win fight with <b>{Utility.PluralText(enemy.name, count)}</b> ({pickups} found).";
            else
                lastAction = $"You win fight with <b>{Utility.PluralText(enemy.name, count)}</b>.";
            AddTeamGold(gold);

            // exp
            float ratio;
            if (allies.Count == 0)
                ratio = 1f;
            else
                ratio = 1f / (allies.Count + 1);
            if (player.AddExp(enemy.level, ratio * count))
                lastAction += $" You are now level {player.level}.";
            foreach (Hero ally in allies)
            {
                if (ally.AddExp(enemy.level, ratio * count))
                    lastAction += $" {ally.name} is now level {ally.level}.";
            }

            // quest
            if (world.level == tile.foundLevel)
                tile.defeatedEnemies += count;
            if (tile.timer == 0 && !tile.clear)
                tile.timer = 3;

            if (!tile.boss && tile.type.IsClearable() && tile.defeatedEnemies >= 10)
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
                else if (tile.type == TileType.Mine || tile.type == TileType.Sawmill)
                {
                    tile.timer = 0;
                    lastAction += " You <b>cleared</b> this place.";
                    Property property = properties.FirstOrDefault(x => x.locationIndex == world.CurrentLocationIndex);
                    property?.RemoveEvent("Infested");
                }
                else
                    tile.timer = 30;
            }
        }
        else
        {
            if (result == Combat.Result.Escape)
                lastAction = $"You run away from {Utility.PluralText(enemy.name, count)}.";
            else
                lastAction = $"You run away <color=red>defeated</color> from {Utility.PluralText(enemy.name, count)}.";

            if (enemy.name == "dragon")
                tile.defeatedEnemies -= 5;
        }

        // heal after combat
        if (player.hp < 1)
            player.hp = 1;
        foreach (Hero ally in allies)
        {
            if (ally.hp < 1)
                ally.hp = 1;
            ally.ApplyHealing();
        }

        if (tile.type.IsSmall())
            AddTime(minutes: 30);
        else
            AddTime(hours: 1);
        UpdateText();
    }

    public void Rest()
    {
        lastAction = string.Empty;
        OnRest();
        lastAction += " It's a new day.";
        UpdateText();
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
            int basePay, payMod = 1;
            Skill skill;
            switch (location)
            {
            case TileType.Sawmill:
                basePay = 30;
                skill = Skill.Woodcraft;
                break;
            case TileType.Mine:
                basePay = 20 + world.CurrentTile.difficulty * 10;
                skill = Skill.Mining;
                break;
            default:
                basePay = 20;
                skill = Skill.None;
                break;
            }
            // double pay if owned
            if (player.properties.Any(x => x.locationIndex == world.CurrentLocationIndex))
                payMod = 2;
            foreach (Hero hero in Team)
            {
                int payment = basePay;
                if (skill != Skill.None)
                    payment += hero.GetSkill(skill) / 10;
                payment *= payMod;
                if (hero == player)
                {
                    player.AddGold(payment);
                    lastAction = $"You earned <color=#FFD700>{payment}</color> gold from working.";
                    if (skill != Skill.None)
                        lastAction += player.Train(skill);
                }
                else
                {
                    hero.gold += payment;
                    if (skill != Skill.None)
                        hero.Train(skill);
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
        if (enter)
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
        if (player.HaveProperty("House"))
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

        lastAction = "You exit to the city.";
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
        OnChangeLocation();
    }

    private void OnChangeLocation()
    {
        Tile tile = world.CurrentTile;
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
            itemEntry.Init(item.ToString(false), "Buy", () =>
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
                itemEntry.Init(player.weapon.ToString(true), "Unequip", () =>
                {
                    player.AddItem(player.weapon);
                    player.weapon = null;
                    RefreshPlayerScreen();
                });
            }
            else
                itemEntry.Init(player.weapon.ToString(true));
        }

        if (player.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeInventory == characterScreen)
            {
                itemEntry.Init(player.armor.ToString(true), "Unequip", () =>
                {
                    player.AddItem(player.armor);
                    player.armor = null;
                    RefreshPlayerScreen();
                });
            }
            else
                itemEntry.Init(player.armor.ToString(true));
        }

        if (player.shield != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeInventory == characterScreen)
            {
                itemEntry.Init(player.shield.ToString(true), "Unequip", () =>
                {
                    player.AddItem(player.shield);
                    player.shield = null;
                    RefreshPlayerScreen();
                });
            }
            else
                itemEntry.Init(player.shield.ToString(true));
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
                    itemEntry.Init2(itemSlot.ToString(true), "Equip", () =>
                    {
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
                    }, "Drop", Drop);
                }
                else if (itemSlot.item.type == Item.Type.Usable)
                {
                    itemEntry.Init2(itemSlot.ToString(true), "Use", () =>
                    {
                        player.hp = Mathf.Min(player.hp + itemSlot.item.power, player.hpMax);
                        player.RemoveItem(itemSlot);
                        RefreshPlayerScreen();
                        UpdateText();
                    }, "Drop", Drop);
                }
                else if (itemSlot.item.type == Item.Type.Tool)
                    itemEntry.Init2(itemSlot.ToString(true), "Use", Craft, "Drop", Drop);
                else
                    itemEntry.Init2(itemSlot.ToString(true), null, null, "Drop", Drop);
            }
            else if (activeInventory == shopScreen)
            {
                itemEntry.Init(itemSlot.ToString(true), "Sell", () =>
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
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
                            player.AddGold(itemSlot.item.value * count / 2);
                            player.RemoveItem(itemSlot, count);
                            RefreshPlayerItems();
                            UpdateText();
                            return true;
                        });
                    }
                    else
                    {
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
                    itemEntry.Init(itemSlot.ToString(true), "Give", () =>
                    {
                        if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor || itemSlot.item.type == Item.Type.Shield
                            || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl)))
                        {
                            activeAlly.GiveItem(itemSlot.item);
                            player.RemoveItem(itemSlot);
                            RefreshPlayerItems();
                            RefreshAllyScreen();
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            activeAlly.GiveItem(itemSlot.item, itemSlot.count);
                            player.RemoveItem(itemSlot, itemSlot.count);
                            RefreshPlayerItems();
                            RefreshAllyScreen();
                        }
                        else
                        {
                            ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} give to {activeAlly.name}?", count =>
                            {
                                if (count <= 0)
                                    return true;
                                count = Mathf.Min(count, itemSlot.count);
                                activeAlly.GiveItem(itemSlot.item, count);
                                player.RemoveItem(itemSlot, count);
                                RefreshPlayerItems();
                                RefreshAllyScreen();
                                return true;
                            });
                        }
                    });
                }
                else
                    itemEntry.Init(itemSlot.ToString(true));
            }
            else
            {
                itemEntry.Init(itemSlot.ToString(true), "Store", () =>
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        AddStoredItem(itemSlot.item, itemSlot.count);
                        player.RemoveItem(itemSlot, itemSlot.count);
                        RefreshPlayerItems();
                        RefreshStoredItems();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to store?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            AddStoredItem(itemSlot.item, count);
                            player.RemoveItem(itemSlot, count);
                            RefreshPlayerItems();
                            RefreshStoredItems();
                            return true;
                        });
                    }
                    else
                    {
                        AddStoredItem(itemSlot.item);
                        player.RemoveItem(itemSlot);
                        RefreshPlayerItems();
                        RefreshStoredItems();
                    }
                });
            }
        }
    }

    private void RefreshPlayerScreen()
    {
        TMP_Text charText = characterScreen.transform.Find("Text").GetComponent<TMP_Text>();
        sb.Clear();
        sb.Append($"{player.GenderSign}{player.name}\n" +
            $"Level: {player.level} {player.clas.AsString()} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}\n");
        if (player.skills.Count > 0)
        {
            sb.Append("Skills:\n");
            foreach (var skill in player.skills.Select(kvp => (name: kvp.Key.AsString().ToUpper1(), kvp.Value.level)).OrderBy(x => x.name))
                sb.Append($"  {skill.name}: {skill.level}\n");
        }
        if (player.rested > 0)
            sb.Append($"Effects:\n  Well rested ({Utility.Plural("day", player.rested, true)})");
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
            $"Gold: {activeAlly.gold}\n");
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
            itemEntry.Init(activeAlly.weapon.ToString(true));
        }

        if (activeAlly.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(activeAlly.armor.ToString(true));
        }

        if (activeAlly.shield != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(activeAlly.shield.ToString(true));
        }

        if ((activeAlly.weapon != null || activeAlly.armor != null || activeAlly.shield != null) && activeAlly.items.Count > 0)
            Instantiate(ui.lineSeparatorPrefab, content);

        foreach (ItemSlot itemSlot in activeAlly.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(true));
        }
    }

    private void UpdateText()
    {
        sb.Clear();
        string name = world.CurrentTile.Name.ToUpper1();
        if (world.level != 0)
            name += $", level {world.level + 1}";
        sb.Append($"{name}   Day: {day} {hour}:{minute:00}   Health: {player.hp}/{player.hpMax}   Energy: {player.energy}/100   Gold: {player.gold}");
        if (player.goldReceived != 0)
        {
            sb.Append($"({player.goldReceived:+0;-0})");
            player.goldReceived = 0;
        }
        sb.Append('\n');
        foreach (Hero ally in allies)
            sb.Append($"{ally.name} ({ally.HpP}%)   ");
        Quest activeQuest = activeQuests.FirstOrDefault(x => x.tracked);
        if (activeQuest != null)
            sb.Append($"Quest: {activeQuest.Text}\n");
        else
            sb.Append('\n');
        if (!string.IsNullOrEmpty(lastAction))
        {
            sb.Append('\n');
            sb.Append(lastAction);
        }
        lastAction = null;
        text.text = sb.ToString();
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

    private void OnRest()
    {
        void FullRest()
        {
            player.hp = player.hpMax;
            player.energy = 100;
            foreach (Hero ally in allies)
                ally.hp = ally.hpMax;
        }

        ++day;
        hour = 8;
        minute = 0;
        TileType location = world.Location;
        if ((location == TileType.City && player.HaveProperty("House")) || location == TileType.House)
        {
            FullRest();
            lastAction += "You rest in your house.";
        }
        else if ((location == TileType.City && player.HaveProperty("Mansion")) || location == TileType.Mansion)
        {
            FullRest();
            foreach (Hero hero in Team)
                hero.rested = 11;
            lastAction += "You rest in your mansion.";
        }
        else if (location == TileType.City && player.HaveProperty("Inn"))
        {
            FullRest();
            lastAction += "You rest in your inn.";
        }
        else if ((location == TileType.City || location == TileType.Village) && player.gold > 0)
        {
            FullRest();
            player.AddGold(-1);
            foreach (Hero ally in allies)
                --ally.gold;
            lastAction += "You rest in an inn (<color=#FFD700>-1</color> gold).";
        }
        else if (location == TileType.Sawmill || location == TileType.Mine)
        {
            FullRest();
            lastAction += "You rest in a barracks.";
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
            if (eaten > 0)
            {
                if (eaten == count)
                {
                    energy += 25;
                    player.hp = player.hpMax;
                    foreach (Hero ally in allies)
                        ally.hp = ally.hpMax;
                }
                else
                {
                    float ratio = (float)eaten / count;
                    energy += (int)(ratio * 25);
                    player.hp = Mathf.Min(player.hp + (int)(ratio * player.hpMax), player.hpMax);
                    foreach (Hero ally in allies)
                        ally.hp = Mathf.Min(ally.hp + (int)(ratio * ally.hpMax), ally.hpMax);
                }
                lastAction += $"You rest {where} and eat rations.";
            }
            else
                lastAction += $"You rest {where}.";
            player.energy = Mathf.Min(player.energy + energy, 100);
        }

        OnNewDay();

        if (player.goldWaiting != 0 && location.IsSafe())
        {
            lastAction += player.goldWaiting > 0
                ? $" You receive <color=#FFD700>{player.goldWaiting}</color> gold from your properties."
                : $" You pay <color=#FFD700>{-player.goldWaiting}</color> gold for your properties.";
            player.AddGold(player.goldWaiting);
            player.goldWaiting = 0;
        }

        ui.CloseDialogs(x => x == propertiesScreen || x == guildScreen || x == characterScreen || x == craftScreen);
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
                                AddNotification($"{p.name} infestation has been cleared by {(even.state == 0 ? "adventurers" : "guards")}.");
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
                world.GetLocation(property.locationIndex).SetType(TileType.Mine);
                map.UpdateMap(World.IndexToPoint(property.locationIndex));
                if (world.CurrentLocationIndex == property.locationIndex)
                    OnChangeLocation();
                AddNotification($"The construction of {property.name.ToLower()} has been completed.");
            }
        }

        if (availableQuests != null)
        {
            foreach (Quest quest in availableQuests)
                --quest.timer;
        }

        // grow garden plants
        foreach (var plant in gardenPlants.GroupBy(x => x).Select(x => (name: x.Key, count: x.Count())))
        {
            switch (plant.name)
            {
            case "Vegetables":
                AddStoredItem(Item.Get("rations"), plant.count);
                break;
            case "Herbs":
                AddStoredItem(Item.Get("herb"), plant.count);
                break;
            case "Rare herbs":
                AddStoredItem(Item.Get("rare herb"), plant.count);
                break;
            }
        }

        // end effects
        foreach (Hero hero in Team)
        {
            if (hero.rested > 0)
                --hero.rested;
        }

        world.Update();

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
                    property.events.Add(new Property.Event { name = "Buff", timer = 30 });
                    string str;
                    if (property.name == "Sawmill")
                        str = "Your sawmill production increased thanks to good weather.";
                    else if (property.name == "Inn")
                        str = "Your inn income increased thanks to festival.";
                    else if (Utility.Rand % 2 == 0)
                        str = $"Your {property.name.ToLower()} production increased thanks to good ore quality.";
                    else
                        str = $"Your {property.name.ToLower()} production increased thanks to new ore veins.";
                    AddNotification(str);
                    break;
                }
                else if (c == 2 && property.locationIndex != -1 && !property.HaveUpgrade("Extra guards"))
                {
                    // 5%
                    property.events.Add(new Property.Event { name = "Infested", timer = -1 });
                    AddNotification($"{property.name} has been taken over by monsters! Hire adventurers or deal with it yourself.");
                    Tile tile = world.GetLocation(property.locationIndex);
                    tile.clear = false;
                    tile.defeatedEnemies = 0;
                    break;
                }
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

                int canRemove = Mathf.Min(perHero, counts[hero], count);

                for (int i = 0; i < canRemove; i++)
                    hero.RemoveItem(item);

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
        storedItems = new();
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
        notifications = new();
        properties = new()
        {
            new()
            {
                name = "House",
                desc = "don't pay for inn",
                value = 500,
                status = Property.Status.Active,
                locationIndex = -1,
                upgrades = new Property.Upgrade[]
                {
                    new()
                    {
                        name = "Alchemy lab",
                        desc = "+25 alchemy",
                        value = 100
                    },
                    new()
                    {
                        name = "Garden",
                        desc = "Grow food or herbs, +1 upkeep",
                        value = 100,
                        upkeep = 1
                    }
                }
            },
            new()
            {
                name = "Mansion",
                desc = "don't pay for inn, better rest, UPKEEP upkeep",
                value = 10000,
                upkeep = 5,
                status = Property.Status.Active,
                locationIndex = -1,
                upgrades = new Property.Upgrade[]
                {
                    new()
                    {
                        name = "Alchemy lab",
                        desc = "+25 alchemy",
                        value = 100
                    },
                    new()
                    {
                        name = "Garden",
                        desc = "Grow food or herbs, +2 upkeep",
                        value = 500,
                        upkeep = 2
                    }
                }
            },
            new()
            {
                name = "Horses",
                desc = "+25% travel speel",
                value = 500,
                status = Property.Status.Active,
                locationIndex = -1
            },
            new()
            {
                name = "Sawmill",
                desc = "PROFIT gold/day, reduce mines upkeep and build cost",
                value = 5000,
                infestedCost = 500,
                income = 10,
                upkeep = 5,
                status = Property.Status.Active,
                locationIndex = world.FindLocationIndex(x => x.type == TileType.Sawmill),
                upgrades = new Property.Upgrade[]
                {
                    new()
                    {
                        name = "Extra guards",
                        desc = "Prevents monster invasion, +1 upkeep",
                        value = 1000,
                        upkeep = 1
                    },
                    new()
                    {
                        name = "Water-powered saws",
                        desc = "+5 income",
                        value = 1500,
                        income = 5
                    }
                }
            },
            new()
            {
                name = "Iron mine",
                desc = "PROFIT gold/day",
                value = 10000,
                infestedCost = 750,
                income = 20,
                upkeep = 10,
                upkeepDiscount = 2,
                status = Property.Status.Active,
                locationIndex = world.FindLocationIndex(x => x.type == TileType.Mine),
                upgrades = new Property.Upgrade[]
                {
                    new()
                    {
                        name = "Extra guards",
                        desc = "Prevents monster invasion, +2 upkeep",
                        value = 2000,
                        upkeep = 2
                    },
                    new()
                    {
                        name = "Deep shaft expansion",
                        desc = "+10 income",
                        value = 3000,
                        income = 10
                    }
                }
            },
            new()
            {
                name = "Silver mine",
                desc = "PROFIT gold/day",
                value = 25000,
                infestedCost = 1500,
                income = 35,
                upkeep = 10,
                upkeepDiscount = 2,
                buildPrice = 6000,
                buildPriceDiscount = 500,
                buildTime = 20,
                locationIndex = world.FindLocationIndex(x => x.hidden == TileType.Cave && x.mine && x.difficulty == 2),
                upgrades = new Property.Upgrade[]
                {
                    new()
                    {
                        name = "Extra guards",
                        desc = "Prevents monster invasion, +2 upkeep",
                        value = 2000,
                        upkeep = 2
                    },
                    new()
                    {
                        name = "Deep shaft expansion",
                        desc = "+15 income",
                        value = 4000,
                        income = 15
                    }
                }
            },
            new()
            {
                name = "Gold mine",
                desc = "PROFIT gold/day",
                value = 50000,
                infestedCost = 2000,
                income = 60,
                upkeep = 10,
                upkeepDiscount = 2,
                buildPrice = 7500,
                buildPriceDiscount = 500,
                buildTime = 30,
                locationIndex = world.FindLocationIndex(x => x.hidden == TileType.Cave && x.mine && x.difficulty == 3),
                upgrades = new Property.Upgrade[]
                {
                    new()
                    {
                        name = "Extra guards",
                        desc = "Prevents monster invasion, +2 upkeep",
                        value = 2000,
                        upkeep = 2
                    },
                    new()
                    {
                        name = "Deep shaft expansion",
                        desc = "+20 income",
                        value = 5000,
                        income = 20
                    }
                }
            },
            new()
            {
                name = "Inn",
                desc = "PROFIT gold/day, free rest",
                value = 5000,
                income = 10,
                upkeep = 5,
                status = Property.Status.Active,
                locationIndex = -1
            },
        };
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
        bool inCity = location == TileType.City;
        bool inVillage = location == TileType.Village;
        Transform buttons = transform.Find("Buttons");
        buttons.Find("BtShop").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtWork").gameObject.SetActive(inCity || inVillage);
        buttons.Find("BtGuild").gameObject.SetActive(inCity);
        buttons.Find("BtProperties").gameObject.SetActive(inCity);
        buttons.Find("BtSewers").gameObject.SetActive(inCity);
        Transform houseButton = buttons.Find("BtHouse");
        if (inCity && (player.HaveProperty("House") || player.HaveProperty("Mansion")))
        {
            houseButton.gameObject.SetActive(true);
            houseButton.GetComponentInChildren<TMP_Text>().text = player.HaveProperty("House") ? "House" : "Mansion";
        }
        else
            houseButton.gameObject.SetActive(false);
        buttons.Find("BtTravel").gameObject.SetActive(world.level == 0);
        buttons.Find("BtGoUp").gameObject.SetActive(world.level != 0);
        buttons.Find("BtGoDown").gameObject.SetActive(world.level < world.CurrentTile.foundLevel);

        Transform btJournal = buttons.Find("BtJournal");
        int notificationsAvailable = notifications.Count(x => x.status == Notification.Status.Available);
        if (notificationsAvailable > 0)
        {
            btJournal.Find("Text1").gameObject.SetActive(false);
            btJournal.Find("Text2").gameObject.SetActive(true);
            btJournal.Find("Image").gameObject.SetActive(true);
            TMP_Text counter = btJournal.Find("Counter").GetComponent<TMP_Text>();
            counter.text = notificationsAvailable.ToString();
            counter.gameObject.SetActive(true);
            btJournal.Find("").gameObject.SetActive(true);
        }
        else
        {
            btJournal.Find("Text1").gameObject.SetActive(true);
            btJournal.Find("Text2").gameObject.SetActive(false);
            btJournal.Find("Image").gameObject.SetActive(false);
            btJournal.Find("Counter").gameObject.SetActive(false);
        }

        buttons.Find("BtForage").gameObject.SetActive(location == TileType.Forest);

        buttons.Find("BtCity").gameObject.SetActive(location == TileType.Sewers || location == TileType.House || location == TileType.Mansion);

        buttons.Find("BtWork2").gameObject.SetActive(location == TileType.Sawmill || location == TileType.Mine);

        buttons.Find("BtStorage").gameObject.SetActive(location == TileType.House || location == TileType.Mansion);
        buttons.Find("BtCook").gameObject.SetActive(location == TileType.House || location == TileType.Mansion);
        buttons.Find("BtCraft").gameObject.SetActive((location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab"))
            || (location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Alchemy lab")));
        buttons.Find("BtGarden").gameObject.SetActive((location == TileType.House && player.HavePropertyUpgrade("House", "Garden"))
            || (location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Garden")));

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
        if (allies.Count >= MaxAllies)
        {
            ui.ShowDialog("Your team is full.");
            return;
        }

        Hero hero = SpawnHero();
        string className = hero.clas.AsString();
        ui.ShowConfirm($"You meet <b>{hero.name}</b> and talk with {hero.him} about adventurers. {hero.He} is {Utility.A(className)} <b>{className}</b>. Do you want to recruit {hero.him}?", yes =>
        {
            if (yes)
            {
                lastAction = $"You recruit {hero.name} to your team.";
                allies.Add(hero);
                UpdateButtons();
            }
            AddTime(minutes: 30);
            if (ui.TopDialog == guildScreen)
                RefreshGuild();
            UpdateText();
        });
    }

    private Hero SpawnHero()
    {
        Hero hero = new();
        hero.Init();
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
            activeAlly.gold += count;
            if (world.Location.IsSafe())
                activeAlly.BuyItems();
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

        Property[] propertiesToBuy = properties.Where(x => x.status != Property.Status.None).OrderBy(x => x.value).ThenBy(x => x.name).ToArray();

        // player properties
        if (player.properties.Count > 0)
        {
            ui.AddTextHeader("Your properties:", content);
            foreach (Property property in player.properties.OrderBy(x => x.value).ThenBy(x => x.name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
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
                        lastAction = $"You sell {property.name.ToLower()} for <color=#FFD700>{property.value / 2}</color> gold.";
                        if (property.name == "House" || property.name == "Mansion")
                        {
                            UpdateButtons();
                            gardenPlants.Clear();
                        }
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
                    ui.ShowDialog($"You need {cost} gold to {(build ? "build" : "buy")} {property.name.ToLower()}.");
                    return;
                }

                if ((property.name == "House" && player.HaveProperty("Mansion")) || (property.name == "Mansion" && player.HaveProperty("House")))
                {
                    ui.ShowDialog("You can't own both house and mansion. It's a law!");
                    return;
                }

                player.AddGold(-cost);
                player.properties.Add(property);
                properties.Remove(property);
                if (build)
                {
                    lastAction = $"You pay <color=#FFD700>{cost}</color> gold to build {property.name.ToLower()}.";
                    property.status = Property.Status.Building;
                    world.GetLocation(property.locationIndex).timer = 0; // prevent resetting
                }
                else
                {
                    lastAction = $"You buy {property.name.ToLower()} for <color=#FFD700>{cost}</color> gold.";

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
                    gardenPlants.Clear();
                    int size = property.name == "House" ? 2 : 6;
                    for (int i = 0; i < size; ++i)
                        gardenPlants.Add(string.Empty);
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
            str = $"<b>{selectedProperty.name}</b>\n{Utility.Plural("day", selectedProperty.buildTime, true)} left to end of construction";
        else
        {
            Property.Event even = selectedProperty.events.FirstOrDefault(x => x.name == "Infested");
            if (even == null)
                str = $"<b>{selectedProperty.name}</b>\n";
            else if (even.timer == -1)
                str = $"<b>{selectedProperty.name} (infested)</b>\n";
            else
                str = $"<b>{selectedProperty.name} ({Utility.Plural("day", even.timer, true)} to clear)</b>\n";
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
        guildScreen.transform.Find("BtCook").GetComponent<Button>().interactable = guildRank != 0;
        guildScreen.transform.Find("BtCraft").GetComponent<Button>().interactable = guildRank != 0;

        availableQuests ??= new();

        // remove old quests
        availableQuests.RemoveAll(x => x.timer <= 0);

        // add new quests
        if (availableQuests.Count != 6)
        {
            int[] questsByDifficulty = new int[4];
            foreach (Quest quest in availableQuests)
                questsByDifficulty[quest.difficulty]++;

            for (int difficulty = 1; difficulty <= 3; ++difficulty)
            {
                while (questsByDifficulty[difficulty] < 2)
                {
                    Quest quest = GenerateQuest(difficulty);
                    availableQuests.Add(quest);
                    ++questsByDifficulty[difficulty];
                }
            }

            availableQuests.Sort((a, b) =>
            {
                int result = a.difficulty.CompareTo(b.difficulty);
                if (result != 0)
                    return result;
                return a.timer.CompareTo(b.timer);
            });
        }

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
                    availableQuests.Remove(quest);
                    lastAction = $"You accepted quest '{quest.Title}'.";
                    if (quest.type == Quest.Type.Artifact)
                    {
                        Tile tile = world.GetLocation(quest.location);
                        tile.defeatedEnemies = 0;
                        tile.foundTreasure = false;
                    }
                    else if (quest.type == Quest.Type.Clear)
                    {
                        Tile tile = world.GetLocation(quest.location);
                        tile.defeatedEnemies = 0;
                        tile.clear = false;
                    }
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
            foreach (Property prop in infestedProperties)
            {
                Property property = prop;
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                int days = world.CalculateTravelDaysNonTeam(World.IndexToPoint(property.locationIndex));
                itemEntry.Init($"Clear {property.name.ToLower()} ({Utility.Plural("day", days, true)}, {property.infestedCost} gold)", "Pay", () =>
                {
                    if (player.gold < property.infestedCost)
                    {
                        ui.ShowDialog($"You need {property.infestedCost} gold to pay adventurers to clear the {property.name.ToLower()}.");
                        return;
                    }

                    player.AddGold(-property.infestedCost);
                    prop.events.First(e => e.name == "Infested").timer = days;
                    lastAction = $"You pay <color=#FFD700>{property.infestedCost}</color> gold to adventurers to clear the {property.name.ToLower()}. " +
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
                switch (Utility.Rand % 10)
                {
                case 0:
                    // 10%
                    quest.type = Quest.Type.Clear;
                    quest.locationDifficulty = Utility.Random(1, 3);
                    quest.max = 10;
                    switch (quest.locationDifficulty)
                    {
                    case 1:
                        quest.location = world.FindLocationIndex(x => x.type == TileType.City, 1);
                        quest.difficultyMod = 1f;
                        quest.difficultyMod = 0.5f;
                        break;
                    case 2:
                        if (player.HaveProperty("Sawmill"))
                        {
                            // don't generate random quest if player owned
                            quest.locationDifficulty = 1;
                            quest.location = world.FindLocationIndex(x => x.type == TileType.City, 1);
                            quest.difficultyMod = 0.5f;
                        }
                        else
                        {
                            quest.location = world.FindLocationIndex(x => x.type == TileType.Sawmill);
                            quest.difficultyMod = 0.75f;
                        }
                        break;
                    case 3:
                        if (player.HaveProperty("Iron mine"))
                        {
                            // don't generate random quest if player owned
                            quest.locationDifficulty = 1;
                            quest.location = world.FindLocationIndex(x => x.type == TileType.City, 1);
                            quest.difficultyMod = 0.5f;
                        }
                        else
                        {
                            quest.location = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == 1);
                            quest.difficultyMod = 1f;
                        }
                        break;
                    }
                    break;
                case 1:
                    // 10%
                    quest.type = Quest.Type.Gather;
                    quest.item = Item.Get("herb");
                    quest.max = 20;
                    quest.locationDifficulty = 1;
                    quest.difficultyMod = 0.25f;
                    break;
                case 2:
                case 3:
                    // 20%
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.hidden == TileType.Dungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.difficultyMod = 1f;
                    quest.max = 1;
                    break;
                case 4:
                    // 10%
                    quest.type = Quest.Type.Clear;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Cave || x.hidden == TileType.Cave) && !x.mine && x.difficulty == 1);
                    quest.locationDifficulty = 3;
                    quest.difficultyMod = 1f;
                    quest.max = 10;
                    break;
                default:
                    // 50%
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
                switch (Utility.Rand % 10)
                {
                case 0:
                case 1:
                    // 20%
                    quest.type = Quest.Type.Clear;
                    quest.locationDifficulty = difficulty == 2 ? 5 : 8;
                    quest.max = 10;
                    if (!allowMine || Utility.Rand % 2 == 0)
                        quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Cave || x.hidden == TileType.Cave) && !x.mine && !x.boss && x.difficulty == difficulty);
                    else
                        quest.location = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == difficulty);
                    quest.difficultyMod = 1f;
                    break;
                case 2:
                case 3:
                    // 20%
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.hidden == TileType.Dungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.difficultyMod = 1f;
                    quest.max = 1;
                    break;
                case 4:
                    // 10%
                    if (difficulty == 3)
                        goto case default;
                    quest.type = Quest.Type.Gather;
                    quest.item = Item.Get("rare herb");
                    quest.max = 20;
                    quest.locationDifficulty = 2;
                    quest.difficultyMod = 0.25f;
                    break;
                default:
                    // 50%
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
                return quest;
        }
    }

    private void FinishQuest(Quest quest)
    {
        int reward = quest.Reward;
        lastAction = $"You received <color=#FFD700>{reward}</color> gold for quest '{quest.Title}'.";
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
                }
            }
        }
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
        RemoveQuest(quest);
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

    public void Forage()
    {
        if (player.energy < 10)
        {
            lastAction = "You are too tired to explore.";
            UpdateText();
            return;
        }

        // 1-4 herbs (~3.16)
        int count = (Utility.Rand % 6) switch
        {
            1 or 2 => 2,
            3 or 4 => 3,
            5 => 4,
            _ => 1,
        };

        Tile tile = world.CurrentTile;
        Item herb = tile.GetHerb();
        player.energy -= 10;
        player.AddItem(herb, count);
        lastAction = $"You forage in the {tile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.";
        AddTime(hours: 1);
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
        }

        // potions
        int alchemy = player.GetSkill(Skill.Alchemy);
        if (world.Location == TileType.House || world.Location == TileType.Mansion)
            alchemy += 25;
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
            lastAction = $"You created {Utility.Plural(recipe.result.name, count + extra)}.";
            lastAction += player.Train(Skill.Alchemy, recipe.trainMod * count);
            AddTime(minutes: count * 5);
            if (ui.IsOpen(craftScreen))
                RefreshCraft();
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
        }
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
        foreach (Hero ally in allies)
        {
            ally.gold += share;
            if (world.Location.IsSafe())
                ally.BuyItems();
        }

        gold -= share * allies.Count;
        player.AddGold(gold);
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
        map.Regenerate();
    }

    [ContextMenu("Reveal world")]
    private void RevealWorld()
    {
        world.RevealAllHiddenLocations();
    }

    [ContextMenu("Refresh quests")]
    private void RefreshQuests()
    {
        availableQuests.Clear();
        if (ui.CurrentDialog == guildScreen)
            RefreshGuild();
    }

    [ContextMenu("Give all")]
    private void GiveAll()
    {
        while (allies.Count < MaxAllies)
            allies.Add(SpawnHero());

        foreach (Hero hero in Team)
        {
            while (hero.level < 10)
                hero.AddExp(hero.level, 4f);
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

    public void JoinGuild()
    {
        guildRank = 1;
        lastAction = "You register as an adventurer.";
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
        Transform content = storeItemsScreen.transform.Find("StoredItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (ItemSlot itemSlot in storedItems)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(false), "Take", () =>
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
        }
    }

    private void AddStoredItem(Item item, int count = 1)
    {
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
            storedItems.Remove(itemSlot);
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

        string[] choices = new string[] { "---", "Vegetables (10 gold)", "Herbs (10 herbs)", "Rare herbs (10 herbs)" };

        for (int i = 0; i < gardenPlants.Count; ++i)
        {
            int index = i;
            string plant = gardenPlants[i];
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
                        gardenPlants[index] = "Vegetables";
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
                        gardenPlants[index] = "Herbs";
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
                        gardenPlants[index] = "Rare herbs";
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
}
