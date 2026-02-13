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
    private const int MaxAllies = 2;

    public Player player;
    public List<Hero> allies;
    public List<Quest> availableQuests;
    [SerializeReference]
    public Quest activeQuest;
    public string location;
    public int day, hour;

    private GameUI ui;
    private GameObject shop, character, allyScreen, giveAllyItems, activeInventory, properiesScreen, guilScreend, travelScreen;
    private TMP_Text text;
    private Hero activeAlly;
    private readonly StringBuilder sb = new();
    private string lastAction;

    private readonly string[] allLocations = new[] { "City", "Forest", "Mountains", "Dungeon", "Sewers" };

    private IEnumerable<Hero> Team
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
            allies = new();
            location = "City";
            day = 1;
            hour = 8;
        }
        global.player = player;
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
            else if (location == "Forest")
            {
                if (Input.GetKeyDown(KeyCode.F))
                    Forage();
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

        int chance = 3;
        if (location == "Sewers" && !(activeQuest != null && activeQuest.type == Quest.Type.Clear && activeQuest.location == location && activeQuest.count < activeQuest.max))
            chance = 10;
        player.energy -= 10;

        int c = Random.Range(0, 10);
        if (c > chance)
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
                if (activeQuest != null)
                {
                    if (activeQuest.type == Quest.Type.Defeat)
                    {
                        if (activeQuest.enemy == enemy)
                            activeQuest.count += count;
                    }
                    else if (activeQuest.type == Quest.Type.Clear)
                    {
                        if (activeQuest.location == location)
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
            }
            else
                lastAction += " You run away defeated.";
        }
        else if (c == 0 && location == "Forest")
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
            lastAction = $"You explore {location.ToLower()} and find {Utility.Plural(herb.name, count)}.";
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
            player.energy -= 50;
            player.AddGold(20);
            foreach (Hero ally in allies)
                ally.gold += 20;
            lastAction = "You earned 20 gold from working. ";
            AddHour(8);
            foreach (Hero ally in allies)
                ally.BuyItems();
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

        foreach (string loc in allLocations)
            travelScreen.transform.Find(loc).GetComponent<Button>().interactable = location != loc;

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

            foreach (Hero ally in allies)
                ally.BuyItems();
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

        if ((player.weapon != null || player.armor != null) && player.items.Count > 0)
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
                if (activeAlly.WillTakeItem(itemSlot.item))
                {
                    itemEntry.Init(itemSlot.ToString(true), "Give", () =>
                    {
                        if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl)))
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
        charText.text = $"{player.GenderSign}{player.name}\n" +
            $"Level: {player.level} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}";

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

        if ((activeAlly.weapon != null || activeAlly.armor != null) && activeAlly.items.Count > 0)
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
        sb.Append($"{location}   Day: {day} {hour}:00   Health: {player.hp}/{player.hpMax}   Energy: {player.energy}/100   Gold: {player.gold}");
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
        if (lastAction != null)
        {
            sb.Append('\n');
            sb.Append(lastAction);
            lastAction = null;
        }
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
                        enemyHp[enemyIndex] -= hero.Attack - enemy.def;
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
                        else if (Team.All(x => x.hp <= 0))
                        {
                            // lost
                            player.hp = 1;
                            foreach (Hero ally in allies)
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

    private bool AttackChance(int myDex, int targetDex)
    {
        int chance = 75 + (myDex - targetDex) * 5;
        if (chance < 10)
            chance = 10;
        return Utility.Random(0, 100) < chance;
    }

    private void AddHour(int count = 1)
    {
        hour += count;
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
        if (location == "City" && player.properties.Any(x => x.name == "House"))
        {
            player.hp = player.hpMax;
            player.energy = 100;
            foreach (Hero ally in allies)
                ally.hp = ally.hpMax;
            lastAction += "You rest in your house.";
        }
        else if (location == "City" && player.gold > 0)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            player.AddGold(-1);
            foreach (Hero ally in allies)
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
                if (location == "City")
                    where = "on street";
                else if (location == "Forest")
                    where = "on grass";
                else
                    where = "on ground";
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

    private void SaveGame()
    {
        string json = JsonUtility.ToJson(this);
        Global.Instance.SaveGame($"{player.name}, Day {day}, Level {player.level}, Gold {player.gold}", json);
    }

    private void LoadGame()
    {
        string json = Global.Instance.GetSaveData();
        JsonUtility.FromJsonOverwrite(json, this);
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

        transform.Find("BtForage").gameObject.SetActive(location == "Forest");

        GameObject btAlly = transform.Find("BtAlly").gameObject;
        if (allies.Count < 1)
            btAlly.SetActive(false);
        else
        {
            btAlly.GetComponentInChildren<TMP_Text>().text = allies[0].name;
            btAlly.SetActive(true);
        }
        btAlly = transform.Find("BtAlly2").gameObject;
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
            lastAction = "Your team is full.";
        else
        {
            Hero ally = new();
            ally.Init();
            lastAction = $"You recruit {ally.name} to your team.";
            allies.Add(ally);
            AddHour();
            UpdateButtons();
        }
        UpdateText();
    }

    public void RemoveAlly()
    {
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
            if (location == "City")
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
        Transform content = properiesScreen.transform.Find("List/Viewport/Content");
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
        bool doneQuest = false;
        if (activeQuest != null && activeQuest.IsDone())
        {
            int reward = activeQuest.Reward;
            lastAction = $"You received {reward} gold for quest '{activeQuest.Title}'.";
            AddTeamGold(reward);
            activeQuest = null;
            doneQuest = true;
        }
        else
            lastAction = null;

        UpdateGuild();
        if (doneQuest)
            UpdateText();
        ui.ShowDialog(guilScreend);
    }

    private void UpdateGuild()
    {
        string guildText;
        if (lastAction != null)
        {
            guildText = lastAction;
            guildText += "\n\n";
        }
        else
            guildText = string.Empty;

        guildText += $"Current quest: {(activeQuest != null ? activeQuest.Text : "none")}";
        guilScreend.transform.Find("Text").GetComponent<TMP_Text>().text = guildText;

        availableQuests ??= new();
        while (availableQuests.Count < 3)
        {
            Quest quest = new();
            int c = Utility.Rand % 5;
            switch (c)
            {
            case 0:
                quest.type = Quest.Type.Clear;
                quest.location = "Sewers";
                quest.max = 10;
                break;
            case 1:
                quest.type = Quest.Type.Gather;
                quest.item = Item.Get("herb");
                quest.max = 20;
                break;
            default:
                quest.type = Quest.Type.Defeat;
                quest.enemy = Enemy.enemies.RandomItem(x => x.quest);
                quest.max = Utility.Random(2, 3);
                break;
            }

            if (availableQuests.All(x => !x.IsSimilar(quest)) && (activeQuest == null || !activeQuest.IsSimilar(quest)))
                availableQuests.Add(quest);
        }

        Transform content = guilScreend.transform.Find("List/Viewport/Content");
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
        lastAction = $"You forage in {location.ToLower()} and find {Utility.Plural(herb.name, count)}.";
        AddHour();
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
            player.AddItem(potion, count);
            lastAction = $"You created {Utility.Plural(potion.name, count)}.";
            UpdateGuild();
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
            if (location == "City")
                ally.BuyItems();
        }

        gold -= share * allies.Count;
        player.AddGold(gold);
    }
}
