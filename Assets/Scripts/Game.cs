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

    private static readonly string[] GuildRanks = new[] { "None", "Copper", "Silver", "Gold" };

    public World world;
    public Player player;
    public List<Hero> allies;
    public List<Quest> availableQuests;
    public List<Property> properties;
    public List<ItemSlot> storedItems;
    public List<string> notifications;
    [SerializeReference]
    public Quest activeQuest;
    public DragonStatus dragonStatus;
    public int day, hour, minute, guildRank, guildProgress;

    private GameUI ui;
    private GameObject shop, character, allyScreen, giveAllyItems, storeItemsScreen, activeInventory, propertiesScreen, guildScreen;
    private Map map;
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
        shop = transform.Find("Shop").gameObject;
        character = transform.Find("Character").gameObject;
        allyScreen = transform.Find("Ally").gameObject;
        giveAllyItems = transform.Find("GiveItems").gameObject;
        storeItemsScreen = transform.Find("StoreItems").gameObject;
        propertiesScreen = transform.Find("Properties").gameObject;
        guildScreen = transform.Find("Guild").gameObject;
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
                    if (activeQuest != null && activeQuest.IsDone() && Input.GetKeyDown(KeyCode.F))
                        FinishQuest();
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
            if (Input.GetKeyDown(KeyCode.E))
                Explore();
            if (Input.GetKeyDown(KeyCode.R))
                Rest();
            if (Input.GetKeyDown(KeyCode.T))
                Travel();

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
                if (Input.GetKeyDown(KeyCode.H) && player.HaveProperty("House"))
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
                if (Input.GetKeyDown(KeyCode.X))
                    ExitToCity();
                break;
            case TileType.Sawmill:
            case TileType.Mine:
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                break;
            case TileType.House:
                if (Input.GetKeyDown(KeyCode.X))
                    ExitToCity();
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
        if (tile.type == TileType.Dungeon && !tile.foundTreasure && tile.defeatedEnemies >= 10)
        {
            int level = tile.difficulty + 2;
            Item item = Item.items.RandomItem(x => x.level == level);
            int gold = Utility.Round(Utility.Random(level * 100, level * 200));
            lastAction = $"You explore the {tile.Name} and find <b>treasure room</b>. Inside chest you find <color=#FFD700>{gold}</color> gold and <b>{item.name}</b>.";
            if (activeQuest != null && activeQuest.type == Quest.Type.Artifact && activeQuest.location == world.CurrentLocationIndex)
            {
                activeQuest.count = 1;
                lastAction += $" You also find an <b>artifact</b>.";
            }
            AddTeamGold(gold);
            player.AddItem(item);
            tile.foundTreasure = true;
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
                if (tile.defeatedEnemies >= 10)
                {
                    enemy = Enemy.Get("dragon");
                    count = 1;
                }
                else
                    enemy = Enemy.Get("dragon-man");
            }

            lastAction = $"You explore the {tile.Name} and <b>{Utility.PluralText(enemy.name, count)}</b> attack you.";
            if (Combat(enemy, count))
            {
                if (activeQuest != null)
                {
                    if (activeQuest.type == Quest.Type.Defeat)
                    {
                        if (activeQuest.enemy == enemy)
                            activeQuest.count += count;
                    }
                    else if (activeQuest.type == Quest.Type.Clear)
                    {
                        if (activeQuest.location == world.CurrentLocationIndex)
                            activeQuest.count += count;
                    }
                }

                // gold
                int gold = 0;
                for (int i = 0; i < count; ++i)
                    gold += enemy.gold.Random();
                if (enemy.name == "dragon")
                {
                    dragonStatus = DragonStatus.Defeated;
                    lastAction += " With a final blow, the dragon falls. Its roar fades into silence, and the cavern grows still. The beast is slain—its hoard and your legend now yours to claim. " +
                        $"You found <color=#FFD700>{gold}</color> gold.";
                    tile.clear = true;
                }
                else if (gold > 0)
                    lastAction += $" You win (<color=#FFD700>{gold}</color> gold found).";
                else
                    lastAction += $" You win.";
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
                tile.defeatedEnemies += count;
                if (tile.defeatedEnemies >= 10 && !tile.boss && tile.type.IsClearable())
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
                        lastAction += " You <b>cleared</b> this place.";
                        Property property = properties.FirstOrDefault(x => x.locationIndex == world.CurrentLocationIndex);
                        property?.RemoveEvent("Infested");
                    }
                }
            }
            else
            {
                lastAction += " You run away <color=red>defeated<color>.";
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
            if (AttackChance(10, target.dex))
            {
                target.hp -= Mathf.Max(15 + tile.difficulty * 5 + Utility.Random(0, 5), 0);
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
            Item herb = Item.Get("herb");
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
        lastAction = "You enter your house.";
        world.sublocation = 2;
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
        }

        ui.UpdateBackground((int)tile.type);
        UpdateButtons();
        UpdateText();
        traveled = false;
    }

    public void Shop()
    {
        activeInventory = shop;
        RefreshShopItems();
        RefreshPlayerItems();
        ui.ShowDialog(shop);
    }

    public void Character()
    {
        activeInventory = character;
        RefreshPlayerScreen();
        ui.ShowDialog(character);
    }

    public void Ally(int index)
    {
        activeAlly = allies[index];
        RefreshAllyScreen();
        ui.ShowDialog(allyScreen);
    }

    private void RefreshShopItems()
    {
        Transform content = shop.transform.Find("ShopItems/Viewport/Content");
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
            if (activeInventory == character)
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
            if (activeInventory == character)
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
            if (activeInventory == character)
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
            if (activeInventory == character)
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
                else
                    itemEntry.Init2(itemSlot.ToString(true), null, null, "Drop", Drop);
            }
            else if (activeInventory == shop)
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
            else if (activeInventory == giveAllyItems)
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
        TMP_Text charText = character.transform.Find("Text").GetComponent<TMP_Text>();
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
        charText.text = sb.ToString();

        RefreshAllyItems(allyScreen);
        if (activeInventory == giveAllyItems)
            RefreshAllyItems(giveAllyItems);
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
        sb.Append($"{world.CurrentTile.Name.ToUpper1()}   Day: {day} {hour}:{minute:00}   Health: {player.hp}/{player.hpMax}   Energy: {player.energy}/100   Gold: {player.gold}");
        if (player.goldReceived != 0)
        {
            sb.Append($"({player.goldReceived:+0;-0})");
            player.goldReceived = 0;
        }
        sb.Append('\n');
        foreach (Hero ally in allies)
            sb.Append($"{ally.name} ({ally.HpP}%)   ");
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

    private bool Combat(Enemy enemy, int enemyCount)
    {
        List<int> order = new() { -1 };
        List<int> enemyHp = new();
        player.InitCombat();
        int index = -2;
        foreach (Hero ally in allies)
        {
            order.Add(index);
            ally.InitCombat();
            --index;
        }
        for (int i = 0; i < enemyCount; ++i)
        {
            order.Add(i);
            enemyHp.Add(enemy.hp);
        }
        order = order.Select(x =>
        {
            int dex;
            if (x == -1)
                dex = player.dex;
            else if (x < -1)
                dex = allies[-x - 2].dex;
            else
                dex = enemy.dex;
            dex += Utility.Rand % 5;
            return (x, dex);
        }).OrderByDescending(x => x.dex).Select(x => x.x).ToList();
        index = 0;

        while (true)
        {
            int unitIndex = order[index];
            if (unitIndex < 0)
            {
                Hero hero = unitIndex == -1 ? player : allies[-unitIndex - 2];
                if (!hero.backRow)
                    hero.canBlock = true;
                if (hero.wasteTurn)
                    hero.wasteTurn = false;
                else if (hero.hp > 0)
                {
                    int enemyIndex = enemyHp.Select((hp, index) => (hp, index)).RandomItem(x => x.hp > 0).index;
                    if (AttackChance(hero.dex, enemy.dex))
                    {
                        enemyHp[enemyIndex] -= Mathf.Max(hero.Attack - enemy.def, 0);
                        if (enemyHp.All(x => x <= 0))
                            return true;
                    }
                }
            }
            else if (enemyHp[unitIndex] > 0)
            {
                Hero hero = Team.RandomItem(x => x.hp > 0);
                if (hero.backRow)
                {
                    // front row heroes can block attack once per round
                    Hero blockingHero = Team.RandomItem(x => x.hp > 0 && x.canBlock);
                    if (blockingHero != null)
                    {
                        hero = blockingHero;
                        hero.canBlock = false;
                    }
                }

                if (AttackChance(enemy.dex, hero.dex))
                {
                    hero.hp -= Mathf.Max(enemy.attack - hero.Defense, 0);
                    if (hero.hp <= 0)
                    {
                        ItemSlot potion;
                        if (!hero.wasteTurn && (potion = hero.FindHealingItem()) != null && hero.hp + potion.item.power > 0)
                        {
                            // hero use potion and waste turn
                            hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
                            hero.RemoveItem(potion);
                            hero.wasteTurn = true;
                        }
                        else if (Team.All(x => x.hp <= 0))
                        {
                            // lost
                            return false;
                        }
                    }
                }
            }

            ++index;
            if (index == order.Count)
                index = 0;
        }
    }

    private bool AttackChance(int myDex, int targetDex)
    {
        int chance = 75 + (myDex - targetDex) * 5;
        if (chance < 10)
            chance = 10;
        return Utility.Random(0, 100) < chance;
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
        ++day;
        hour = 8;
        minute = 0;
        TileType location = world.Location;
        if ((location == TileType.City && player.HaveProperty("House")) || location == TileType.House)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            foreach (Hero ally in allies)
                ally.hp = ally.hpMax;
            lastAction += "You rest in your house.";
        }
        else if ((location == TileType.City || location == TileType.Village) && player.gold > 0)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            player.AddGold(-1);
            foreach (Hero ally in allies)
            {
                ally.hp = ally.hpMax;
                --ally.gold;
            }
            lastAction += "You rest in an inn (<color=#FFD700>-1</color> gold).";
        }
        else if (location == TileType.Sawmill || location == TileType.Mine)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            foreach (Hero ally in allies)
                ally.hp = ally.hpMax;
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

        GameObject topDialog = ui.TopDialog;
        if (topDialog == propertiesScreen || topDialog == guildScreen)
            ui.CloseTopDialog();
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
                        return -p.upkeep / 2;
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
                AddNotification($"The construction of {property.name} has been completed.");
            }
        }

        if (availableQuests != null)
        {
            foreach (Quest quest in availableQuests)
                --quest.timer;
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
                        str = "Your Sawmill production increased thanks to good weather.";
                    else if (Utility.Rand % 2 == 0)
                        str = $"Your {property.name} production increased thanks to good ore quality.";
                    else
                        str = $"Your {property.name} production increased thanks to new ore veins.";
                    AddNotification(str);
                    break;
                }
                else if (c == 2 && !property.HaveUpgrade("Extra guards"))
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
        notifications ??= new();
        notifications.Add(str);
        if (world.Location.IsSafe())
            UpdateButtons();
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
                desc = "PROFIT gold/day",
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
                buildPrice = 6000,
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
                buildPrice = 7500,
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
                    }
                }
            }
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
        buttons.Find("BtHouse").gameObject.SetActive(inCity && player.HaveProperty("House"));

        GameObject btMessages = buttons.Find("BtMessages").gameObject;
        bool showMessages = inCity || inVillage || location == TileType.House;
        btMessages.SetActive(showMessages);
        if (showMessages)
        {
            btMessages.GetComponent<Button>().interactable = notifications.Count > 0;
            btMessages.GetComponentInChildren<TMP_Text>().text = notifications.Count > 0 ? $"Messages ({notifications.Count})" : "Messages";
        }

        buttons.Find("BtForage").gameObject.SetActive(location == TileType.Forest);

        buttons.Find("BtCity").gameObject.SetActive(location == TileType.Sewers || location == TileType.House);

        buttons.Find("BtWork2").gameObject.SetActive(location == TileType.Sawmill || location == TileType.Mine);

        buttons.Find("BtStorage").gameObject.SetActive(location == TileType.House);
        buttons.Find("BtCraft").gameObject.SetActive(location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab"));

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
                UpdateGuild();
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
        activeInventory = giveAllyItems;
        ui.ShowDialog(giveAllyItems);
        RefreshPlayerItems();
        RefreshAllyItems(giveAllyItems);
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
        UpdateProperties();
        UpdatePropertyDetails();
        ui.ShowDialog(propertiesScreen);
    }

    private void UpdateProperties()
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
                        lastAction = $"You sell {property.name} for <color=#FFD700>{property.value / 2}</color> gold.";
                        if (property.name == "House")
                            UpdateButtons();
                        AddTime(minutes: 30);
                        if (ui.CurrentDialog == propertiesScreen)
                        {
                            if (selectedProperty == property)
                            {
                                selectedProperty = null;
                                UpdatePropertyDetails();
                            }
                            UpdateProperties();
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
                int cost = build ? property.buildPrice : property.value;
                if (player.gold < cost)
                {
                    ui.ShowDialog($"You need {cost} gold to {(build ? "build" : "buy")} {property.name}.");
                    return;
                }

                player.AddGold(-cost);
                player.properties.Add(property);
                properties.Remove(property);
                if (build)
                {
                    lastAction = $"You pay <color=#FFD700>{cost}</color> gold to build {property.name}.";
                    property.status = Property.Status.Building;
                    world.GetLocation(property.locationIndex).timer = 0; // prevent resetting
                }
                else
                {
                    lastAction = $"You buy {property.name} for <color=#FFD700>{cost}</color> gold.";

                    // remove quests assigned to this location
                    if (property.locationIndex != -1)
                    {
                        if (activeQuest != null && activeQuest.type == Quest.Type.Clear && activeQuest.location == property.locationIndex)
                        {
                            lastAction += $" Quest '{activeQuest.Title}' is reassigned to other party.";
                            activeQuest = null;
                        }
                        availableQuests.RemoveAll(x => x.type == Quest.Type.Clear && x.location == property.locationIndex);
                    }
                }

                if (property.name == "House")
                    UpdateButtons();
                AddTime(minutes: 30);
                if (ui.CurrentDialog == propertiesScreen)
                {
                    selectedProperty = property;
                    UpdateProperties();
                    UpdatePropertyDetails();
                }
                UpdateText();
            });
        }
    }

    public void UpdatePropertyDetails()
    {
        ItemEntryList list = propertiesScreen.transform.Find("List").GetComponent<ItemEntryList>();
        selectedProperty = list.GetSelectedData() as Property;

        string str;
        if (selectedProperty != null)
        {
            str = $"<b>{selectedProperty.name}</b>\n" +
                $"Income:{selectedProperty.Income}  Upkeep:{selectedProperty.Upkeep}  Profit:{selectedProperty.Profit}\nUpgrades: ";
            if (selectedProperty.upgrades != null && selectedProperty.upgrades.Any(x => x.active))
                str += string.Join(", ", selectedProperty.upgrades.Where(x => x.active).Select(x => x.name).OrderBy(x => x));
            else
                str += "(none)";
        }
        else
            str = string.Empty;
        propertiesScreen.transform.Find("Text2").GetComponent<TMP_Text>().text = str;

        Transform content = propertiesScreen.transform.Find("Upgrades/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (selectedProperty != null && selectedProperty.upgrades != null && selectedProperty.upgrades.Any(x => !x.active))
        {
            ui.AddTextHeader("Available upgrades:", content);
            foreach (Property.Upgrade upgrade in selectedProperty.upgrades.Where(x => !x.active).OrderBy(x => x.name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.Init(upgrade.ToString(), "Buy", () =>
                {
                    if (player.gold < upgrade.value)
                    {
                        ui.ShowDialog($"You need {upgrade.value} gold to buy {upgrade.name}.");
                        return;
                    }

                    player.AddGold(-upgrade.value);
                    upgrade.active = true;
                    selectedProperty.value += upgrade.value;
                    selectedProperty.upkeep += upgrade.upkeep;
                    lastAction = $"You buy {upgrade.name} for <color=#FFD700>{upgrade.value}</color> gold.";
                    if (upgrade.name == "Extra guards")
                    {
                        if (selectedProperty.RemoveEvent("Infested"))
                            lastAction += $" That will take care of monsters infestation.";
                    }
                    AddTime(minutes: 30);
                    if (ui.CurrentDialog == propertiesScreen)
                    {
                        UpdateProperties();
                        UpdatePropertyDetails();
                    }
                    UpdateText();
                });
            }
        }
    }

    public void Guild()
    {
        UpdateGuild();
        ui.ShowDialog(guildScreen);
    }

    private void UpdateGuild()
    {
        string guildText;
        if (!string.IsNullOrEmpty(lastAction))
        {
            guildText = lastAction;
            guildText += "\n\n";
        }
        else
            guildText = string.Empty;

        guildText += $"Your rank: {GuildRanks[guildRank]}\nCurrent quest: {(activeQuest != null ? activeQuest.Text : "none")}";
        guildScreen.transform.Find("Text").GetComponent<TMP_Text>().text = guildText;

        if (guildRank == 0)
        {
            guildScreen.transform.Find("BtFinishQuest").gameObject.SetActive(false);
            guildScreen.transform.Find("BtJoin").gameObject.SetActive(true);
            guildScreen.transform.Find("BtRecruit").GetComponent<Button>().interactable = false;
            guildScreen.transform.Find("BtCraft").GetComponent<Button>().interactable = false;
        }
        else
        {
            Transform finishQuestTransform = guildScreen.transform.Find("BtFinishQuest");
            finishQuestTransform.gameObject.SetActive(true);
            finishQuestTransform.GetComponent<Button>().interactable = activeQuest != null && activeQuest.IsDone();
            guildScreen.transform.Find("BtJoin").gameObject.SetActive(false);
        }
        guildScreen.transform.Find("BtRecruit").GetComponent<Button>().interactable = guildRank != 0;
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
            if (activeQuest == null && !unavailable)
            {
                itemEntry.Init(quest.TitleReward, "Pick", () =>
                {
                    activeQuest = quest;
                    availableQuests.Remove(quest);
                    lastAction = $"You accepted quest '{activeQuest.Title}'.";
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
                        UpdateGuild();
                    UpdateText();
                });
            }
            else
                itemEntry.Init(quest.TitleReward);
        }

        // add player paid quests
        Property[] infestedProperties = player.properties.Where(p => p.events.Any(e => e.name == "Infested")).ToArray();
        if (infestedProperties.Length > 0)
        {
            Instantiate(ui.lineSeparatorPrefab, content);
            foreach (Property prop in infestedProperties)
            {
                Property property = prop;
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.Init($"Clear {property.name.ToLower()} ({property.infestedCost} gold)", "Pay", () =>
                {
                    if (player.gold < property.infestedCost)
                    {
                        ui.ShowDialog($"You need {property.infestedCost} gold to pay adventurers to clear the {property.name.ToLower()}.");
                        return;
                    }

                    player.AddGold(-property.infestedCost);
                    lastAction = $"You pay <color=#FFD700>{property.infestedCost}</color> gold to adventurers to clear the {property.name.ToLower()}.";
                    world.GetLocation(property.locationIndex).clear = true;
                    property.RemoveEvent("Infested");
                    AddTime(minutes: 15);
                    if (ui.CurrentDialog == guildScreen)
                        UpdateGuild();
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
                int c = Utility.Rand % 10;
                switch (c)
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
                        break;
                    case 2:
                        if (player.HaveProperty("Sawmill"))
                        {
                            // don't generate random quest if player owned
                            quest.locationDifficulty = 1;
                            quest.location = world.FindLocationIndex(x => x.type == TileType.City, 1);
                        }
                        else
                            quest.location = world.FindLocationIndex(x => x.type == TileType.Sawmill);
                        break;
                    case 3:
                        if (player.HaveProperty("Iron mine"))
                        {
                            // don't generate random quest if player owned
                            quest.locationDifficulty = 1;
                            quest.location = world.FindLocationIndex(x => x.type == TileType.City, 1);
                        }
                        else
                            quest.location = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == 1);
                        break;
                    }
                    break;
                case 1:
                    // 10%
                    quest.type = Quest.Type.Gather;
                    quest.item = Item.Get("herb");
                    quest.max = 20;
                    break;
                case 2:
                case 3:
                    // 20%
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.hidden == TileType.Dungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.max = 1;
                    break;
                case 4:
                    // 10%
                    quest.type = Quest.Type.Clear;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Cave || x.hidden == TileType.Cave) && !x.mine && x.difficulty == 1);
                    quest.locationDifficulty = 3;
                    quest.max = 10;
                    break;
                default:
                    // 50%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 3);
                    quest.locationDifficulty = difficulty;
                    break;
                }
            }
            else
            {
                string mineName = (difficulty == 2 ? "Silver mine" : "Gold mine");
                bool allowMine = properties.Any(x => x.name == mineName && x.status == Property.Status.Active);
                int c = Utility.Rand % 5;
                if (c < 3)
                {
                    // 60%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 2 + difficulty);
                    quest.locationDifficulty = difficulty;
                }
                else if (c == 3 && (allowMine || difficulty == 2))
                {
                    // 0-20% (if player don't own the mine or cave can be picked)
                    quest.type = Quest.Type.Clear;
                    quest.locationDifficulty = difficulty == 2 ? 5 : 8;
                    quest.max = 10;
                    if (difficulty == 2 && (!allowMine || Utility.Rand % 2 == 0))
                        quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Cave || x.hidden == TileType.Cave) && !x.mine && x.difficulty == 2);
                    else
                        quest.location = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == difficulty);
                }
                else
                {
                    // 20-40% (if clear quest is unavailable)
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.hidden == TileType.Dungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.max = 1;
                }
            }

            if (availableQuests.All(x => !x.IsSimilar(quest)) && (activeQuest == null || !activeQuest.IsSimilar(quest)))
                return quest;
        }
    }

    public void FinishQuest()
    {
        int reward = activeQuest.Reward;
        lastAction = $"You received <color=#FFD700>{reward}</color> gold for quest '{activeQuest.Title}'.";
        if (activeQuest.difficulty == guildRank && guildRank != 3)
        {
            ++guildProgress;
            if (guildProgress == 2)
            {
                ++guildRank;
                guildProgress = 0;
                lastAction += $" You were promoted to <b>{GuildRanks[guildRank]}</b> rank.";
            }
        }
        AddTeamGold(reward);
        activeQuest = null;
        AddTime(minutes: 15);
        if (ui.CurrentDialog == guildScreen)
            UpdateGuild();
        UpdateText();
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
        Item herb = Item.Get("herb");
        player.energy -= 10;
        player.AddItem(herb, count);
        lastAction = $"You forage in the {world.CurrentTile.Name} and find <b>{Utility.Plural(herb.name, count)}</b>.";
        AddTime(hours: 1);
        UpdateText();
    }

    public void Craft()
    {
        Item herb = Item.Get("herb");
        int herbCount = player.CountItem(herb);
        ui.ShowInput($"How many potions do you want to craft? You have {Utility.Plural(herb.name, herbCount)} (2 herbs = 1 potion)", count =>
        {
            if (count <= 0)
                return true;
            if (count * 2 > herbCount)
            {
                ui.ShowDialog($"You don't have {Utility.Plural(herb.name, count * 2)}.");
                return false;
            }
            Item potion = Item.Get("potion");
            player.RemoveItem(herb, count * 2);
            float mod;
            int alchemy = player.GetSkill(Skill.Alchemy);
            if (world.Location == TileType.House)
                alchemy += 25;
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
            player.AddItem(potion, count + extra);
            lastAction = $"You created {Utility.Plural(potion.name, count + extra)}.";
            lastAction += player.Train(Skill.Alchemy, 0.2f * count);
            AddTime(minutes: count * 5);
            if (ui.TopDialog == guildScreen)
                UpdateGuild();
            UpdateText();
            return true;
        });
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
            UpdateGuild();
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
            hero.weapon = Item.Get("magic sword");
            hero.armor = Item.Get("magic armor");
            hero.shield = Item.Get("magic shield");
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

    public void ShowNotification()
    {
        ui.ShowDialog(notifications[0]);
        notifications.RemoveAt(0);
        UpdateButtons();
    }

    public void JoinGuild()
    {
        guildRank = 1;
        lastAction = "You register as an adventurer.";
        AddTime(minutes: 15);
        if (ui.CurrentDialog == guildScreen)
            UpdateGuild();
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
}
