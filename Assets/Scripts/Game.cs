using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public Player player;
    [SerializeReference]
    public Hero ally;
    public List<Quest> availableQuests;
    [SerializeReference]
    public Quest activeQuest;
    public string location;
    public int day, hour;

    private GameUI ui;
    private GameObject shop, character, allyScreen, giveAllyItems, activeInventory, properiesScreen, guilScreend, travelScreen;
    private TMP_Text text;
    private string lastAction;

    private void Awake()
    {
        ui = GetComponent<GameUI>();
        text = transform.Find("Text").GetComponent<TMP_Text>();
        shop = transform.Find("Shop").gameObject;
        character = transform.Find("Character").gameObject;
        allyScreen = transform.Find("Ally").gameObject;
        giveAllyItems = transform.Find("GiveItems").gameObject;
        properiesScreen = transform.Find("Properties").gameObject;
        guilScreend = transform.Find("Guild").gameObject;
        travelScreen = transform.Find("Travel").gameObject;

        Global global = Global.Instance;
        if (global.loadGame)
        {
            global.loadGame = false;
            LoadGame();
        }
        else
        {
            player = new() { name = global.playerName, female = global.playerFemale };
            player.Init();
            location = "City";
            day = 1;
            hour = 8;
        }
        UpdateText();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif

        if (ui.HasDialog)
        {
            if (ui.CurrentDialog == travelScreen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && location != "City")
                    Travel("City");
                if (Input.GetKeyDown(KeyCode.Alpha2) && location != "Forest")
                    Travel("Forest");
                if (Input.GetKeyDown(KeyCode.Alpha3) && location != "Mountains")
                    Travel("Mountains");
                if (Input.GetKeyDown(KeyCode.Alpha4) && location != "Dungeon")
                    Travel("Dungeon");
            }
        }
        else
        {
            if (ally != null && Input.GetKeyDown(KeyCode.A))
                Ally();
            if (Input.GetKeyDown(KeyCode.C))
                Character();
            if (Input.GetKeyDown(KeyCode.E))
                Explore();
            if (Input.GetKeyDown(KeyCode.R))
                Rest();
            if (Input.GetKeyDown(KeyCode.T))
                Travel();
            if (location == "City")
            {
                if (Input.GetKeyDown(KeyCode.G))
                    Guild();
                if (Input.GetKeyDown(KeyCode.P))
                    ManageProperties();
                if (Input.GetKeyDown(KeyCode.W))
                    Work();
                if (Input.GetKeyDown(KeyCode.S))
                    Shop();
            }
        }
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    public void Explore()
    {
        if (player.energy < 10)
        {
            lastAction = "You are too tired to explore.";
            UpdateText();
            return;
        }

        player.energy -= 10;
        if (Random.Range(0, 10) > 3)
        {
            Enemy enemy = Enemy.enemies.RandomItem(x => x.location == location);
            int count = (Utility.Rand % 4) switch
            {
                1 or 2 => 2,
                3 => 3,
                _ => 1,
            };
            lastAction = $"You explore {location.ToLower()} and {Utility.PluralText(enemy.name, count)} attack you.";
            if (Combat(enemy, count))
            {
                if (activeQuest != null && activeQuest.enemy == enemy)
                    activeQuest.count += count;

                int gold = 0;
                for (int i = 0; i < count; ++i)
                    gold += enemy.gold.Random();

                lastAction += $" You win ({gold} gold found).";
                if (ally == null)
                {
                    player.gold += gold;
                    if (player.AddExp(enemy.level, count))
                        lastAction += $" You are now level {player.level}.";
                }
                else
                {
                    int allyGold = gold / 2;
                    ally.gold += allyGold;
                    gold -= allyGold;
                    player.AddGold(gold);
                    if (player.AddExp(enemy.level, 0.5f * count))
                        lastAction += $" You are now level {player.level}.";
                    if (ally.AddExp(enemy.level, 0.5f * count))
                        lastAction += $" {ally.name} is now level {ally.level}.";
                    if (location == "City")
                        ally.BuyItems();
                }
            }
            else
                lastAction += " You run away defeated.";
        }
        else
            lastAction = $"You explore {location.ToLower()} but find nothing interesting.";

        AddHour();
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
        else
        {
            player.AddGold(20);
            if (ally != null)
                ally.gold += 20;
            lastAction = "You earned 20 gold from working. ";
            OnRest();
            ally?.BuyItems();
            lastAction += " It's a new day.";
        }
        UpdateText();
    }

    public void Travel()
    {
        if (player.energy < 10)
        {
            lastAction = "You are too tired to travel.";
            UpdateText();
            return;
        }

        travelScreen.transform.Find("City").GetComponent<Button>().interactable = location != "City";
        travelScreen.transform.Find("Forest").GetComponent<Button>().interactable = location != "Forest";
        travelScreen.transform.Find("Mountains").GetComponent<Button>().interactable = location != "Mountains";
        travelScreen.transform.Find("Dungeon").GetComponent<Button>().interactable = location != "Dungeon";
        ui.ShowDialog(travelScreen);
    }

    public void Travel(string where)
    {
        player.energy -= 10;
        lastAction = $"You travel to {where.ToLower()}.";
        location = where;
        if (location == "City")
        {
            if (player.goldWaiting != 0)
            {
                lastAction += $" You receive {player.goldWaiting} gold from your properties.";
                player.AddGold(player.goldWaiting);
                player.goldWaiting = 0;
            }
            ally?.BuyItems();
        }
        UpdateButtons();
        AddHour();
        UpdateText();
        ui.CloseDialog();
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

    public void Ally()
    {
        RefreshAllyScreen();
        ui.ShowDialog(allyScreen);
    }

    private void RefreshShopItems()
    {
        Transform content = shop.transform.Find("ShopItems").Find("Viewport").Find("Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);
        foreach (Item item in Item.items)
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
                            ui.ShowDialog($"You need {item.value} gold to buy {Utility.Plural(item.name, count)}.");
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
        Transform content = activeInventory.transform.Find("PlayerItems").Find("Viewport").Find("Content");
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

        if (player.weapon != null || player.armor != null)
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

                if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor)
                {
                    itemEntry.Init2(itemSlot.ToString(true), "Equip", () =>
                    {
                        if (itemSlot.item.type == Item.Type.Weapon)
                        {
                            if (player.weapon != null)
                                player.AddItem(player.weapon);
                            player.weapon = itemSlot.item;
                        }
                        else
                        {
                            if (player.armor != null)
                                player.AddItem(player.armor);
                            player.armor = itemSlot.item;
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
                if (ally.WillTakeItem(itemSlot.item))
                {
                    itemEntry.Init(itemSlot.ToString(true), "Give", () =>
                    {
                        if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl)))
                        {
                            ally.GiveItem(itemSlot.item);
                            player.RemoveItem(itemSlot);
                            RefreshPlayerItems();
                            RefreshAllyScreen();
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            ally.GiveItem(itemSlot.item, itemSlot.count);
                            player.RemoveItem(itemSlot, itemSlot.count);
                            RefreshPlayerItems();
                            RefreshAllyScreen();
                        }
                        else
                        {
                            ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} give to {ally.name}?", count =>
                            {
                                if (count <= 0)
                                    return true;
                                count = Mathf.Min(count, itemSlot.count);
                                ally.GiveItem(itemSlot.item, count);
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
        charText.text = $"{player.GenderSign}{player.name}\n" +
            $"Level: {player.level} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}";

        RefreshPlayerItems();
    }

    private void RefreshAllyScreen()
    {
        TMP_Text charText = allyScreen.transform.Find("Text").GetComponent<TMP_Text>();
        charText.text = $"{ally.GenderSign}{ally.name}\n" +
            $"Level: {ally.level} ({ally.ExpP}%)\n" +
            $"Attack: {ally.Attack}\n" +
            $"Defense: {ally.Defense}\n" +
            $"Gold: {ally.gold}";

        RefreshAllyItems(allyScreen);
        if (activeInventory == giveAllyItems)
            RefreshAllyItems(giveAllyItems);
    }

    private void RefreshAllyItems(GameObject dialog)
    {
        Transform content = dialog.transform.Find("AllyItems").Find("Viewport").Find("Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (ally.weapon != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(ally.weapon.ToString(true));
        }

        if (ally.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(ally.armor.ToString(true));
        }

        if (ally.weapon != null || ally.armor != null)
            Instantiate(ui.lineSeparatorPrefab, content);

        foreach (ItemSlot itemSlot in ally.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(true));
        }
    }

    private void UpdateText()
    {
        string str = $"{location}   Day: {day} {hour}:00   Health: {player.hp}/{player.hpMax}   Energy: {player.energy}/100   Gold: {player.gold}";
        if (player.goldReceived != 0)
        {
            str += $"({player.goldReceived:+0;-0})";
            player.goldReceived = 0;
        }
        str += '\n';
        if (ally != null)
            str += $"{ally.name} ({ally.HpP}%)   ";
        if (activeQuest != null)
            str += $"Quest: {activeQuest.Text}\n";
        else
            str += '\n';
        if (lastAction != null)
        {
            str += '\n';
            str += lastAction;
            lastAction = null;
        }
        text.text = str;
    }

    private bool Combat(Enemy enemy, int enemyCount)
    {
        List<int> order = new() { -1 };
        List<int> enemyHp = new();
        player.wasteTurn = false;
        if (ally != null)
        {
            order.Add(-2);
            ally.wasteTurn = false;
        }
        for (int i = 0; i < enemyCount; ++i)
        {
            order.Add(i);
            enemyHp.Add(enemy.hp);
        }
        order.Shuffle();
        int index = 0;

        while (true)
        {
            int unitIndex = order[index];
            if (unitIndex < 0)
            {
                Hero hero = unitIndex == -1 ? player : ally;
                if (hero.wasteTurn)
                    hero.wasteTurn = false;
                else if (hero.hp > 0)
                {
                    int enemyIndex = enemyHp.Select((hp, index) => (hp, index)).RandomItem(x => x.hp > 0).index;
                    if (Random.Range(0, 100) > 25)
                    {
                        enemyHp[enemyIndex] -= hero.Attack - enemy.def;
                        if (enemyHp.All(x => x <= 0))
                            return true;
                    }
                }
            }
            else if (enemyHp[unitIndex] > 0)
            {
                Hero hero;
                if (player.hp > 0)
                {
                    if (ally != null && ally.hp > 0)
                        hero = Utility.Rand % 2 == 0 ? player : ally;
                    else
                        hero = player;
                }
                else if (ally != null && ally.hp > 0)
                    hero = ally;
                else
                    hero = null; // no one to attack?

                if (hero != null && Random.Range(0, 100) > 75)
                {
                    hero.hp -= Mathf.Max(enemy.attack - hero.Defense);
                    if (hero.hp <= 0)
                    {
                        ItemSlot potion = hero.FindItem("potion");
                        if (potion != null && hero.hp + potion.item.power > 0 && !hero.wasteTurn)
                        {
                            // hero use potion and waste turn
                            hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
                            hero.RemoveItem(potion);
                            hero.wasteTurn = true;
                        }
                        else if (player.hp <= 0 && (ally == null || ally.hp <= 0))
                        {
                            // lost
                            player.hp = 1;
                            if (ally != null)
                                ally.hp = 1;
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

    private void AddHour()
    {
        ++hour;
        if (hour == 24)
        {
            lastAction += " It's a new day. ";
            OnRest();
        }
    }

    private void OnRest()
    {
        ++day;
        hour = 8;
        if (location == "City" && player.properties.Any(x => x.name == "House"))
        {
            player.hp = player.hpMax;
            player.energy = 100;
            if (ally != null)
                ally.hp = ally.hpMax;
            lastAction += "You rest in your house.";
        }
        else if (location == "City" && player.gold > 0)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            player.AddGold(-1);
            if (ally != null)
            {
                ally.hp = ally.hpMax;
                --ally.gold;
            }
            lastAction += "You rest in inn (-1 gold).";
        }
        else
        {
            string where;
            int energy;
            if (player.HaveItem("tent"))
            {
                where = "in tent";
                energy = 75;
            }
            else
            {
                where = $"on {(location == "City" ? "street" : "grass")}";
                energy = 50;
            }

            Item rations = Item.Get("rations");
            int count = 1;
            if (ally != null)
                ++count;
            int eaten = RemoveTeamItem(rations, count);
            if (eaten > 0)
            {
                if (eaten == count)
                {
                    energy += 25;
                    player.hp = player.hpMax;
                    if (ally != null)
                        ally.hp = ally.hpMax;
                }
                else
                {
                    energy += 12;
                    player.hp = Mathf.Min(player.hp + player.hpMax / 2, player.hpMax);
                    if (ally != null)
                        ally.hp = Mathf.Min(ally.hp + ally.hpMax / 2, ally.hpMax);
                }
                lastAction = $"You rest {where} and eat rations.";
            }
            else
                lastAction = $"You rest {where}.";
            player.energy = Mathf.Min(player.energy + energy, 100);
        }

        int income = player.properties.Sum(x => x.income);
        if (income > 0)
        {
            if (location == "City")
            {
                lastAction += $" You receive {income} gold from your properties.";
                player.AddGold(income);
            }
            else
                player.goldWaiting += income;
        }
    }

    private int RemoveTeamItem(Item item, int count)
    {
        List<Hero> heroes = new() { player };
        if (ally != null)
            heroes.Add(ally);
        int removed = 0;

        // Cache counts so we don't call CountItem repeatedly
        Dictionary<Hero, int> counts = new();
        foreach (var hero in heroes)
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

    private void SaveGame()
    {
        string json = JsonUtility.ToJson(this);
        File.WriteAllText(Global.SavePath, json);
    }

    private void LoadGame()
    {
        string json = File.ReadAllText(Global.SavePath);
        JsonUtility.FromJsonOverwrite(json, this);
        UpdateButtons();
    }

    public void ExitToMenu()
    {
        SaveGame();
        SceneManager.LoadScene("Menu");
    }

    private void UpdateButtons()
    {
        bool inCity = location == "City";
        transform.Find("BtShop").gameObject.SetActive(inCity);
        transform.Find("BtGuild").gameObject.SetActive(inCity);
        transform.Find("BtWork").gameObject.SetActive(inCity);
        transform.Find("BtRecruit").gameObject.SetActive(inCity);
        transform.Find("BtProperties").gameObject.SetActive(inCity);
        GameObject btAlly = transform.Find("BtAlly").gameObject;
        if (ally == null)
            btAlly.SetActive(false);
        else
        {
            btAlly.GetComponentInChildren<TMP_Text>().text = ally.name;
            btAlly.SetActive(true);
        }
    }

    public void Recruit()
    {
        if (ally != null)
            lastAction = $"You already have an ally, {ally.name}.";
        else
        {
            ally = new Hero();
            ally.Init();
            lastAction = $"You recruit {ally.name} to your team.";
            AddHour();
            UpdateButtons();
        }
        UpdateText();
    }

    public void RemoveAlly()
    {
        ui.ShowConfirm($"Are you sure you want to remove {ally.name} from your team?", () =>
        {
            lastAction = $"{ally.name} is sad and leave.";
            ally = null;
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
        ui.ShowInput($"How much gold give to {ally.name}?", count =>
        {
            count = Mathf.Min(count, player.gold);
            if (count <= 0)
                return true;
            player.AddGold(-count);
            ally.gold += count;
            if (location == "City")
                ally.BuyItems();
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
        Transform content = properiesScreen.transform.Find("List").Find("Viewport").Find("Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        Property[] propertiesToBuy = Property.properties.Except(player.properties).ToArray();

        // player properties
        if (player.properties.Count > 0)
        {
            foreach (Property property in player.properties.OrderBy(x => x.value))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.Init(property.ToString(true), "Sell", () =>
                {
                    player.AddGold(property.value / 2);
                    player.properties.Remove(property);
                    UpdateProperties();
                    UpdateText();
                });
            }

            if (propertiesToBuy.Length > 0)
                Instantiate(ui.lineSeparatorPrefab, content);
        }

        // available properties
        foreach (Property property in propertiesToBuy)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(property.ToString(false), "Buy", () =>
            {
                if (player.gold < property.value)
                    ui.ShowDialog($"You need {property.value} gold to buy {property.name}.");
                else
                {
                    player.AddGold(-property.value);
                    player.properties.Add(property);
                    UpdateProperties();
                    UpdateText();
                }
            });
        }
    }

    public void Guild()
    {
        UpdateGuild();
        ui.ShowDialog(guilScreend);
    }

    private void UpdateGuild()
    {
        string guildText = string.Empty;
        if (activeQuest != null && activeQuest.count >= activeQuest.max)
        {
            int reward = activeQuest.Reward;
            guildText = $"You received {reward} gold for quest '{activeQuest.Title}'.\n\n";
            if (ally != null)
            {
                int allyReward = reward / 2;
                ally.gold += allyReward;
                ally.BuyItems();
                reward -= allyReward;
            }
            player.AddGold(reward);
            activeQuest = null;
            UpdateText();
        }

        guildText += $"Current quest: {(activeQuest != null ? activeQuest.Text : "none")}";
        guilScreend.transform.Find("Text").GetComponent<TMP_Text>().text = guildText;

        availableQuests ??= new();
        while (availableQuests.Count < 3)
        {
            Quest quest = new() { enemy = Enemy.enemies.RandomItem(), max = Utility.Random(2, 3) };
            availableQuests.Add(quest);
        }

        Transform content = guilScreend.transform.Find("List").Find("Viewport").Find("Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (Quest quest in availableQuests)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (activeQuest == null)
            {
                itemEntry.Init(quest.TitleReward, "Pick", () =>
                {
                    activeQuest = quest;
                    availableQuests.Remove(quest);
                    UpdateText();
                    UpdateGuild();
                });
            }
            else
                itemEntry.Init(quest.TitleReward);
        }
    }
}
