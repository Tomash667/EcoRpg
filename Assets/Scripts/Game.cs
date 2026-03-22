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
    public enum MineStatus
    {
        None,
        Cleared,
        Building,
        Done
    }

    private const int MaxAllies = 2;

    private static readonly string[] GuildRanks = new[] { "Copper", "Silver", "Gold" };

    public World world;
    public Player player;
    public SerializableDictionary<string, int> propertyEvents;
    public List<Hero> allies;
    public List<Quest> availableQuests;
    public List<string> notifications;
    [SerializeReference]
    public Quest activeQuest;
    public MineStatus silverMineStatus, goldMineStatus;
    public int day, hour, minute, guildRank, guildProgress, silverMineTimer, goldMineTimer;
    public bool dragonDefeated;

    private GameUI ui;
    private GameObject shop, character, allyScreen, giveAllyItems, activeInventory, properiesScreen, guildScreen;
    private Map map;
    private TMP_Text text;
    private Hero activeAlly;
    private readonly StringBuilder sb = new();
    private System.Action<bool> choiceAction;
    private string lastAction;
    private bool inChoice;

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
        properiesScreen = transform.Find("Properties").gameObject;
        guildScreen = transform.Find("Guild").gameObject;
        map = transform.Find("Map").GetComponent<Map>();

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
                if (Input.GetKeyDown(KeyCode.C))
                    Craft();
                if (activeQuest != null && activeQuest.IsDone() && Input.GetKeyDown(KeyCode.F))
                    FinishQuest();
                if (Input.GetKeyDown(KeyCode.R))
                    Recruit();
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
        if ((tile.type == TileType.Sewers || tile.type == TileType.Sawmill || tile.type == TileType.Mine)
            && !(activeQuest != null && activeQuest.type == Quest.Type.Clear && activeQuest.location == world.CurrentLocationIndex && activeQuest.count < activeQuest.max))
            chance = 0;
        if (tile.boss && dragonDefeated)
            chance = 0;
        if (tile.type == TileType.Plains)
            chance = 0;
        if (tile.foundTreasure && chance > 0)
            chance = 4;
        if (tile.type == TileType.Cave && tile.defeatedEnemies >= 10)
            chance = 0;
        player.energy -= 5;

        int c = Random.Range(0, 10);

#if UNITY_EDITOR
        if (Input.GetKey(KeyCode.Alpha9))
            c = 9;
#endif

        Enemy enemy;
        if ((tile.type == TileType.Dungeon || tile.type == TileType.ForestDungeon) && !tile.foundTreasure && tile.defeatedEnemies >= 10)
        {
            int level = tile.difficulty + 2;
            Item item = Item.items.RandomItem(x => x.level == level);
            int gold = Utility.Round(Utility.Random(level * 100, level * 200));
            lastAction = $"You explore the {tile.Name} and find <b>treasure room</b>. Inside chest you find {gold} gold and {item.name}.";
            if (activeQuest != null && activeQuest.type == Quest.Type.Artifact && activeQuest.location == world.CurrentLocationIndex)
            {
                activeQuest.count = 1;
                lastAction += $" You also find an artifact.";
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

            lastAction = $"You explore the {tile.Name} and {Utility.PluralText(enemy.name, count)} attack you.";
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
                lastAction += $" You win ({gold} gold found).";
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
                if (tile.type == TileType.Cave && tile.defeatedEnemies >= 10)
                {
                    tile.timer = 30;
                    if (tile.mine)
                    {
                        if (tile.difficulty == 2)
                            silverMineStatus = MineStatus.Cleared;
                        else
                            goldMineStatus = MineStatus.Cleared;
                        lastAction += " You can build a <b>mine</b> here.";
                    }
                }
                if (enemy.name == "dragon")
                    dragonDefeated = true;
            }
            else
            {
                lastAction += " You run away defeated.";
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
            lastAction = $"You explore the {tile.Name} and find {Utility.Plural(herb.name, count)}.";
        }
        else if (c == 9 && tile.type == TileType.Mountains)
        {
            if (player.HaveItem("pickaxe"))
            {
                // 1-4 gold nuggets (~3.16)
                int count = (Utility.Rand % 6) switch
                {
                    1 or 2 => 2,
                    3 or 4 => 3,
                    5 => 4,
                    _ => 1,
                };
                Item nugget = Item.Get("gold nugget");
                player.AddItem(nugget, count);
                lastAction = $"You explore the {tile.Name} and find small gold vein. You mine {Utility.Plural(nugget.name, count)}.";
                player.Train(Skill.Mining, 1, ref lastAction);
            }
            else
                lastAction = $"You explore the {tile.Name} and find small gold vein but you don't have a pickaxe...";
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
        else if (activeQuest != null && activeQuest.type == Quest.Type.Clear && activeQuest.location == world.CurrentLocationIndex && activeQuest.count < activeQuest.max)
            lastAction = $"You can't work while monsters occupy the {world.CurrentTile.Name}.";
        else
        {
            player.energy -= 50;
            TileType location = world.Location;
            int payment;
            switch (location)
            {
            case TileType.Sawmill:
                payment = 30 + player.GetSkill(Skill.Woodcraft) / 10;
                if (player.HaveProperty("Sawmill"))
                    payment *= 2;
                break;
            case TileType.Mine:
                switch (world.CurrentTile.difficulty)
                {
                default:
                case 1:
                    payment = 30 + player.GetSkill(Skill.Mining) / 10;
                    if (player.HaveProperty("Iron mine"))
                        payment *= 2;
                    break;
                case 2:
                    payment = 40 + player.GetSkill(Skill.Mining) / 10;
                    if (player.HaveProperty("Silver mine"))
                        payment *= 2;
                    break;
                case 3:
                    payment = 50 + player.GetSkill(Skill.Mining) / 10;
                    if (player.HaveProperty("Gold mine"))
                        payment *= 2;
                    break;
                }
                break;
            default:
                payment = 20;
                break;
            }
            player.AddGold(payment);
            foreach (Hero ally in allies)
                ally.gold += payment;
            lastAction = $"You earned {payment} gold from working.";
            if (location == TileType.Sawmill)
                player.Train(Skill.Woodcraft, 1, ref lastAction);
            else if (location == TileType.Mine)
                player.Train(Skill.Mining, 1, ref lastAction);
            AddTime(hours: 8);
            if (location == TileType.City)
            {
                foreach (Hero ally in allies)
                    ally.BuyItems();
            }
        }
        UpdateText();
    }

    public void Travel()
    {
        map.Show();
        ui.ShowDialog(map.gameObject);
    }

    public void Travel(Vector2Int pt, bool enter)
    {
        if (pt == world.currentPt)
        {
            if (enter)
                ui.CloseDialog();
            return;
        }

        StartCoroutine(TravelLoop(pt, enter));
    }

    private IEnumerator TravelLoop(Vector2Int pt, bool enter)
    {
        ui.lockDialog = true;
        map.BeginTravel(pt);
        yield return world.Travel(pt);
        map.EndTravel();
        ui.lockDialog = false;
        if (enter)
        {
            Tile tile = world.CurrentTile;
            lastAction = $"You travel to the {tile.Name}.";
            if (tile.boss)
                lastAction += " There are <b>dragon engravings</b> near entrance.";
            else if (tile.mine && tile.type == TileType.Cave)
                lastAction += $" There are <b>{(tile.difficulty == 2 ? "silver" : "gold")} veins</b> inside this cave.";
            OnChangeLocation();
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
        world.isInside = true;
        AddTime(minutes: 30);
        OnChangeLocation();
    }

    public void ExitToCity()
    {
        if (player.energy < 5)
        {
            lastAction = "You are too tired to travel.";
            UpdateText();
            return;
        }

        player.energy -= 5;
        lastAction = "You exit to the city.";
        world.isInside = false;
        AddTime(minutes: 30);
        OnChangeLocation();
    }

    private void OnChangeLocation()
    {
        if (world.CurrentTile.type == TileType.City)
        {
            if (player.goldWaiting != 0)
            {
                lastAction += $" You receive {player.goldWaiting} gold from your properties.";
                player.AddGold(player.goldWaiting);
                player.goldWaiting = 0;
            }

            foreach (Hero ally in allies)
                ally.BuyItems();
        }

        UpdateButtons();
        UpdateText();
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
        foreach (Item item in Item.items.Where(x => x.shop))
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

                if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor || itemSlot.item.type == Item.Type.Shield)
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
            else
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
        }
    }

    private void RefreshPlayerScreen()
    {
        TMP_Text charText = character.transform.Find("Text").GetComponent<TMP_Text>();
        sb.Clear();
        sb.Append($"{player.GenderSign}{player.name}\n" +
            $"Level: {player.level} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}\n");
        if (player.skills.Count > 0)
        {
            sb.Append("Skills:\n");
            foreach (var skill in player.skills.Select(kvp => (name: kvp.Key.AsString().ToUpper1(), level: kvp.Value)).OrderBy(x => x.name))
                sb.Append($"  {skill.name}: {skill.level}\n");
        }

        charText.text = sb.ToString();

        RefreshPlayerItems();
    }

    private void RefreshAllyScreen()
    {
        TMP_Text charText = allyScreen.transform.Find("Text").GetComponent<TMP_Text>();
        charText.text = $"{activeAlly.GenderSign}{activeAlly.name}\n" +
            $"Level: {activeAlly.level} ({activeAlly.ExpP}%)\n" +
            $"Attack: {activeAlly.Attack}\n" +
            $"Defense: {activeAlly.Defense}\n" +
            $"Gold: {activeAlly.gold}";

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
        player.wasteTurn = false;
        int index = -2;
        foreach (Hero ally in allies)
        {
            order.Add(index);
            ally.wasteTurn = false;
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
        if (location == TileType.City && player.HaveProperty("House"))
        {
            player.hp = player.hpMax;
            player.energy = 100;
            foreach (Hero ally in allies)
                ally.hp = ally.hpMax;
            lastAction += "You rest in your house.";
        }
        else if (location == TileType.City && player.gold > 0)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            player.AddGold(-1);
            foreach (Hero ally in allies)
            {
                ally.hp = ally.hpMax;
                --ally.gold;
            }
            lastAction += "You rest in an inn (-1 gold).";
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
                if (location == TileType.City)
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

        if (player.goldWaiting > 0 && location == TileType.City)
        {
            lastAction += $" You receive {player.goldWaiting} gold from your properties.";
            player.AddGold(player.goldWaiting);
            player.goldWaiting = 0;
        }

        GameObject topDialog = ui.TopDialog;
        if (topDialog == properiesScreen || topDialog == guildScreen)
            ui.CloseTopDialog();
    }

    public void OnNewDay()
    {
        player.goldWaiting += player.properties.Where(x =>
        {
            if (x.name == "Silver mine")
                return silverMineStatus == MineStatus.Done;
            else if (x.name == "Gold mine")
                return goldMineStatus == MineStatus.Done;
            else
                return true;
        })
        .Sum(x =>
        {
            if (propertyEvents.dictionary.TryGetValue(x.name, out int value))
            {
                if (value > 0)
                {
                    --value;
                    if (value > 0)
                        propertyEvents.dictionary[x.name] = value;
                    else
                        propertyEvents.dictionary.Remove(x.name);
                    return x.income * 3 / 2;
                }
                else
                    return 0;
            }
            else
                return x.income;
        });

        if (silverMineStatus == MineStatus.Building)
        {
            --silverMineTimer;
            if (silverMineTimer == 0)
            {
                silverMineStatus = MineStatus.Done;
                int locationIndex = world.FindLocationIndex(x => x.type == TileType.Cave && x.mine && x.difficulty == 2);
                Tile tile = world.GetLocation(locationIndex);
                tile.type = TileType.Mine;
                tile.hidden = TileType.None;
                map.UpdateMap(World.IndexToPoint(locationIndex));
                AddNotification("The construction of Silver mine has been completed.");
            }
        }

        if (goldMineStatus == MineStatus.Building)
        {
            --goldMineTimer;
            if (goldMineTimer == 0)
            {
                goldMineStatus = MineStatus.Done;
                int locationIndex = world.FindLocationIndex(x => x.type == TileType.Cave && x.mine && x.difficulty == 3);
                Tile tile = world.GetLocation(locationIndex);
                tile.type = TileType.Mine;
                tile.hidden = TileType.None;
                map.UpdateMap(World.IndexToPoint(locationIndex));
                AddNotification("The construction of Gold mine has been completed.");
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
            foreach (Property property in player.properties)
            {
                if (property.income <= 0)
                    continue;

                if (propertyEvents.dictionary.TryGetValue(property.name, out int _))
                    continue;

                if (Utility.Rand % 10 == 0)
                {
                    propertyEvents.dictionary.Add(property.name, 30);
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
            }
        }
    }

    private void AddNotification(string str)
    {
        notifications ??= new();
        notifications.Add(str);
        if (world.Location == TileType.City)
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
        player = new() { name = global.playerName, female = global.playerFemale };
        player.Init();
        allies = new();
        world = new();
        world.Init();
        map.Build();
        day = 1;
        hour = 8;
    }

    private void SaveGame()
    {
        string json = JsonUtility.ToJson(this);
        Global.Instance.SaveGame($"{player.name}, Day {day}, Level {player.level}, Gold {player.gold}", json);
    }

    private void LoadGame()
    {
        Global global = Global.Instance;
        global.loadGame = false;
        string json = global.GetSaveData();
        JsonUtility.FromJsonOverwrite(json, this);
        map.Build();
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
        Transform buttons = transform.Find("Buttons");
        buttons.Find("BtShop").gameObject.SetActive(inCity);
        buttons.Find("BtGuild").gameObject.SetActive(inCity);
        buttons.Find("BtWork").gameObject.SetActive(inCity);
        buttons.Find("BtProperties").gameObject.SetActive(inCity);
        buttons.Find("BtSewers").gameObject.SetActive(inCity);

        GameObject btMessages = buttons.Find("BtMessages").gameObject;
        btMessages.SetActive(inCity);
        if (inCity)
        {
            btMessages.GetComponent<Button>().interactable = notifications.Count > 0;
            btMessages.GetComponentInChildren<TMP_Text>().text = notifications.Count > 0 ? $"Messages ({notifications.Count})" : "Messages";
        }

        buttons.Find("BtForage").gameObject.SetActive(location == TileType.Forest);

        buttons.Find("BtCity").gameObject.SetActive(location == TileType.Sewers);

        buttons.Find("BtWork2").gameObject.SetActive(location == TileType.Sawmill || location == TileType.Mine);

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

        Hero hero = new();
        hero.Init();
        while (true)
        {
            if (!Team.Any(x => x.name == hero.name))
                break;
            hero.name = (hero.female ? Names.femaleNames : Names.maleNames).RandomItem();
        }
        ui.ShowConfirm($"You meet {hero.name} and talk with {hero.him} about adventurers. Do you want to recruit {hero.him}?", yes =>
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

    public void RemoveAlly()
    {
        if (world.Location != TileType.City)
        {
            ui.ShowDialog("You can only remove your allies in city.");
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
            if (world.Location == TileType.City)
                activeAlly.BuyItems();
            RefreshAllyScreen();
            UpdateText();
            return true;
        });
    }

    public void ManageProperties()
    {
        UpdateProperties();
        ui.ShowDialog(properiesScreen);
    }

    private void UpdateProperties()
    {
        properiesScreen.transform.Find("Text").GetComponent<TMP_Text>().text = lastAction ?? string.Empty;
        lastAction = null;

        Transform content = properiesScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        Property[] propertiesToBuy = Property.properties.Except(player.properties)
            .Where(x =>
            {
                if (x.name == "Silver mine")
                    return silverMineStatus != MineStatus.None;
                else if (x.name == "Gold mine")
                    return goldMineStatus != MineStatus.None;
                else
                    return true;
            })
            .ToArray();

        // player properties
        if (player.properties.Count > 0)
        {
            foreach (Property property in player.properties.OrderBy(x => x.value))
            {
                bool build = false;
                if (property.name == "Silver mine")
                    build = silverMineStatus != MineStatus.Done;
                else if (property.name == "Gold mine")
                    build = goldMineStatus != MineStatus.Done;
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                if (build)
                    itemEntry.Init(property.ToString(true, true));
                else
                {
                    itemEntry.Init(property.ToString(true, false), "Sell", () =>
                    {
                        player.AddGold(property.value / 2);
                        player.properties.Remove(property);
                        propertyEvents?.dictionary.Remove(property.name);
                        lastAction = $"You sell {property.name} for {property.value / 2} gold.";
                        AddTime(minutes: 30);
                        if (ui.CurrentDialog == properiesScreen)
                            UpdateProperties();
                        UpdateText();
                    });
                }
            }

            if (propertiesToBuy.Length > 0)
                Instantiate(ui.lineSeparatorPrefab, content);
        }

        // available properties
        foreach (Property property in propertiesToBuy)
        {
            bool build = false;
            if (property.name == "Silver mine")
                build = silverMineStatus == MineStatus.Cleared;
            else if (property.name == "Gold mine")
                build = goldMineStatus == MineStatus.Cleared;
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(property.ToString(false, build), build ? "Build" : "Buy", () =>
            {
                int cost = build ? property.buildPrice : property.value;
                if (player.gold < cost)
                {
                    ui.ShowDialog($"You need {cost} gold to {(build ? "build" : "buy")} {property.name}.");
                    return;
                }

                player.AddGold(-cost);
                player.properties.Add(property);
                if (build)
                {
                    lastAction = $"You pay {cost} gold to build {property.name}.";
                    int difficulty;
                    if (property.name == "Silver mine")
                    {
                        silverMineStatus = MineStatus.Building;
                        silverMineTimer = property.buildTime;
                        difficulty = 2;
                    }
                    else
                    {
                        goldMineStatus = MineStatus.Building;
                        goldMineTimer = property.buildTime;
                        difficulty = 3;
                    }
                    // prevent resetting
                    world.FindLocation(x => x.type == TileType.Cave && x.mine && x.difficulty == difficulty).timer = 0;
                }
                else
                {
                    lastAction = $"You buy {property.name} for {cost} gold.";

                    // remove quests assigned to this location
                    int locationIndex = -1;
                    if (property.name == "Sawmill")
                        locationIndex = world.FindLocationIndex(x => x.type == TileType.Sawmill);
                    else if (property.name == "Iron mine")
                        locationIndex = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == 1);
                    else if (property.name == "Silver mine")
                        locationIndex = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == 2);
                    else if (property.name == "Gold mine")
                        locationIndex = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == 3);
                    if (locationIndex != -1)
                    {
                        if (activeQuest != null && activeQuest.type == Quest.Type.Clear && activeQuest.location == locationIndex)
                        {
                            lastAction += $" Quest '{activeQuest.Title}' is reassigned to other party.";
                            activeQuest = null;
                        }
                        availableQuests.RemoveAll(x => x.type == Quest.Type.Clear && x.location == locationIndex);
                    }
                }

                AddTime(minutes: 30);
                if (ui.CurrentDialog == properiesScreen)
                    UpdateProperties();
                UpdateText();
            });
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
        guildScreen.transform.Find("BtFinishQuest").GetComponent<Button>().interactable = activeQuest != null && activeQuest.IsDone();

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

        Transform content = guildScreen.transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        bool unavailable = false;
        foreach (Quest quest in availableQuests)
        {
            if (!unavailable && quest.difficulty > guildRank + 1)
            {
                unavailable = true;
                Instantiate(ui.lineSeparatorPrefab, content);
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
                    AddTime(minutes: 15);
                    if (ui.CurrentDialog == guildScreen)
                        UpdateGuild();
                    UpdateText();
                });
            }
            else
                itemEntry.Init(quest.TitleReward);
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
                        quest.location = world.FindLocationIndex(x => x.type == TileType.City, true);
                        break;
                    case 2:
                        if (player.HaveProperty("Sawmill"))
                        {
                            // don't generate random quest if player owned
                            quest.locationDifficulty = 1;
                            quest.location = world.FindLocationIndex(x => x.type == TileType.City, true);
                        }
                        else
                            quest.location = world.FindLocationIndex(x => x.type == TileType.Sawmill);
                        break;
                    case 3:
                        if (player.HaveProperty("Iron mine"))
                        {
                            // don't generate random quest if player owned
                            quest.locationDifficulty = 1;
                            quest.location = world.FindLocationIndex(x => x.type == TileType.City, true);
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
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.type == TileType.ForestDungeon) && x.difficulty == difficulty);
                    quest.locationDifficulty = difficulty;
                    quest.max = 1;
                    break;
                default:
                    // 60%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 3);
                    break;
                }
            }
            else
            {
                MineStatus mineStatus = difficulty == 2 ? silverMineStatus : goldMineStatus;
                bool allowMine = (mineStatus == MineStatus.Done && !player.HaveProperty(difficulty == 2 ? "Silver mine" : "Gold mine"));
                int c = Utility.Rand % 5;
                if (c < 3)
                {
                    // 60%
                    quest.type = Quest.Type.Defeat;
                    quest.enemy = Enemy.GetRandom(difficulty);
                    quest.max = Utility.Random(2, 3);
                }
                else if (c == 3 && allowMine)
                {
                    // 20% (only if mine is built and player don't own it)
                    quest.type = Quest.Type.Clear;
                    quest.locationDifficulty = difficulty == 2 ? 5 : 8;
                    quest.max = 10;
                    quest.location = world.FindLocationIndex(x => x.type == TileType.Mine && x.difficulty == difficulty);
                }
                else
                {
                    // 40% (or 20% if mine is built and player don't own it)
                    quest.type = Quest.Type.Artifact;
                    quest.location = world.FindRandomLocationIndex(x => (x.type == TileType.Dungeon || x.type == TileType.ForestDungeon) && x.difficulty == difficulty);
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
        lastAction = $"You received {reward} gold for quest '{activeQuest.Title}'.";
        if (activeQuest.difficulty > guildRank && guildRank != 2)
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
        lastAction = $"You forage in the {world.CurrentTile.Name} and find {Utility.Plural(herb.name, count)}.";
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
            player.Train(Skill.Alchemy, count, ref lastAction);
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
            if (world.Location == TileType.City)
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

    public void ShowNotification()
    {
        ui.ShowDialog(notifications[0]);
        notifications.RemoveAt(0);
        UpdateButtons();
    }
}
