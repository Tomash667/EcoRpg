using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    public Player player;
    public Hero ally;
    public string location;
    public int day, hour;

    private GameUI ui;
    private GameObject shop, character;
    private TMP_Text header, text;
    private string lastAction;

    private void Awake()
    {
        ui = GetComponent<GameUI>();
        header = transform.Find("Header").GetComponent<TMP_Text>();
        text = transform.Find("Text").GetComponent<TMP_Text>();
        shop = transform.Find("Shop").gameObject;
        character = transform.Find("Character").gameObject;

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

        if (!ui.HasDialog)
        {
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
            lastAction = location == "City" ? "You explore city and fight with bandits." : "You explore forest and fight with orcs.";
            if (Combat())
            {
                int gold = location == "City" ? 15 : 30;
                player.gold += gold;
                lastAction += $" You win ({gold} gold found).";
                if (player.AddExp(GetExpReward()))
                    lastAction += $" You are now level {player.level}.";
            }
            else
                lastAction += " You run away.";
        }
        else
            lastAction = $"You explore {location.ToLower()} but find nothing interesting.";

        AddHour();
        UpdateText();
    }

    public void Rest()
    {
        ++day;
        hour = 8;
        if (location == "City" && player.gold > 0)
        {
            player.hp = player.hpMax;
            player.energy = 100;
            --player.gold;
            lastAction = "You rest in inn (-1 gold). It's a new day.";
        }
        else
        {
            ItemSlot itemSlot = player.FindItem("rations");
            if (itemSlot != null)
            {
                player.RemoveItem(itemSlot);
                player.hp = player.hpMax;
                player.energy = Mathf.Min(player.energy + 75, 100);
                lastAction = $"You rest on {(location == "City" ? "street" : "grass")} and eat rations. It's a new day.";
            }
            else
            {
                player.energy = Mathf.Min(player.energy + 50, 100);
                lastAction = $"You rest on {(location == "City" ? "street" : "grass")}. It's a new day.";
            }
        }
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
            ++day;
            hour = 8;
            player.hp = player.hpMax;
            player.energy = 100;
            player.gold += 19;
            lastAction = "You earned 20 gold from working. It's a new day and you rest in inn (-1 gold).";
        }
        UpdateText();
    }

    public void Travel()
    {
        if (player.energy < 10)
            lastAction = "You are too tired to travel.";
        else
        {
            player.energy -= 10;
            if (location == "City")
            {
                lastAction = "You travel to forest.";
                location = "Forest";
            }
            else
            {
                lastAction = "You travel to city.";
                location = "City";
            }
            bool inCity = location == "City";
            transform.Find("BtWork").gameObject.SetActive(inCity);
            transform.Find("BtShop").gameObject.SetActive(inCity);
            AddHour();
        }
        UpdateText();
    }

    public void Shop()
    {
        RefreshShopItems();
        RefreshPlayerItems();
        ui.ShowDialog(shop);
    }

    public void Character()
    {
        RefreshInventory();
        ui.ShowDialog(character);
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
                            player.gold -= price;
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
                    player.gold -= item.value;
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
        Transform content = shop.transform.Find("PlayerItems").Find("Viewport").Find("Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);
        foreach (ItemSlot itemSlot in player.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(true), "Sell", () =>
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    player.gold += itemSlot.item.value * itemSlot.count / 2;
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
                        player.gold += itemSlot.item.value * count / 2;
                        player.RemoveItem(itemSlot, count);
                        RefreshPlayerItems();
                        UpdateText();
                        return true;
                    });
                }
                else
                {
                    player.gold += itemSlot.item.value / 2;
                    player.RemoveItem(itemSlot);
                    RefreshPlayerItems();
                    UpdateText();
                }
            });
        }
    }

    private void RefreshInventory()
    {
        TMP_Text charText = character.transform.Find("Text").GetComponent<TMP_Text>();
        charText.text = $"{player.GenderSign}{player.name}\n" +
            $"Level: {player.level} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}";

        Transform content = character.transform.Find("PlayerItems").Find("Viewport").Find("Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (player.weapon != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(player.weapon.ToString(true), "Unequip", () =>
            {
                player.AddItem(player.weapon);
                player.weapon = null;
                RefreshInventory();
            });
        }

        if (player.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(player.armor.ToString(true), "Unequip", () =>
            {
                player.AddItem(player.armor);
                player.armor = null;
                RefreshInventory();
            });
        }

        if (player.weapon != null || player.armor != null)
            Instantiate(ui.lineSeparatorPrefab, content);

        foreach (ItemSlot itemSlot in player.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor)
            {
                itemEntry.Init(itemSlot.ToString(true), "Equip", () =>
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
                    RefreshInventory();
                });
            }
            else if (itemSlot.item.type == Item.Type.Usable)
            {
                itemEntry.Init(itemSlot.ToString(true), "Use", () =>
                {
                    player.hp = Mathf.Min(player.hp + itemSlot.item.power, player.hpMax);
                    player.RemoveItem(itemSlot);
                    RefreshInventory();
                    UpdateText();
                });
            }
            else
                itemEntry.Init(itemSlot.ToString(true), null, null);
        }
    }

    private void UpdateText()
    {
        header.text = $"{location}   Day: {day} {hour}:00   Health: {player.hp}/{player.hpMax}   Energy: {player.energy}/100   Gold: {player.gold}";
        if (lastAction != null)
        {
            text.text = lastAction;
            lastAction = null;
        }
        else
            text.text = string.Empty;
    }

    private bool Combat()
    {
        int enemyHp, enemyAttack, enemyDef;
        if (location == "City")
        {
            enemyHp = 50;
            enemyAttack = 12;
            enemyDef = 2;
        }
        else
        {
            enemyHp = 75;
            enemyAttack = 18;
            enemyDef = 3;
        }

        bool playerTurn = true;
        while (true)
        {
            if (playerTurn)
            {
                // player attack
                if (Random.Range(0, 100) > 25)
                {
                    enemyHp -= player.Attack - enemyDef;
                    if (enemyHp <= 0)
                        return true;
                }
                playerTurn = false;
            }
            else
            {
                // enemy attack
                if (Random.Range(0, 100) > 75)
                {
                    player.hp -= Mathf.Max(enemyAttack - player.Defense);
                    if (player.hp <= 0)
                    {
                        ItemSlot potion = player.FindItem("potion");
                        if (potion != null && player.hp + potion.item.power > 0)
                        {
                            player.hp = Mathf.Min(player.hp + potion.item.power, player.hpMax);
                            player.RemoveItem(potion);
                            continue; // use up player turn on purpose
                        }
                        player.hp = 1;
                        return false;
                    }
                }
                playerTurn = true;
            }
        }
    }

    private void AddHour()
    {
        ++hour;
        if (hour == 24)
        {
            ++day;
            hour = 8;
            lastAction += " It's a new day.";
            if (location == "City" && player.gold > 0)
            {
                player.hp = player.hpMax;
                player.energy = 100;
                --player.gold;
                lastAction += " You rest in inn (-1 gold).";
            }
            else
            {
                ItemSlot itemSlot = player.FindItem("rations");
                if (itemSlot != null)
                {
                    player.RemoveItem(itemSlot);
                    player.hp = player.hpMax;
                    player.energy = Mathf.Min(player.energy + 75, 100);
                    lastAction = $" You rest on {(location == "City" ? "street" : "grass")} and eat rations.";
                }
                else
                {
                    player.energy = Mathf.Min(player.energy + 50, 100);
                    lastAction = $" You rest on {(location == "City" ? "street" : "grass")}.";
                }
            }
        }
    }

    private int GetExpReward()
    {
        int enemyLevel = location == "City" ? 0 : 1;
        return (player.level - enemyLevel) switch
        {
            0 => 250,
            1 => 200,
            2 => 150,
            3 => 100,
            4 => 50,
            5 => 25,
            6 => 10,
            7 => 5,
            8 => 2,
            9 => 1,
            _ => 0
        };
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
    }

    public void ExitToMenu()
    {
        SaveGame();
        SceneManager.LoadScene("Menu");
    }
}
