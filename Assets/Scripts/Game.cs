using TMPro;
using UnityEditor;
using UnityEngine;

public class Game : MonoBehaviour
{
    private Player player;
    private TMP_Text text;
    private string location, lastAction;
    private int day, hour;

    private void Awake()
    {
        player = new();
        location = "City";
        text = transform.Find("Text").GetComponent<TMP_Text>();
        day = 1;
        hour = 8;
        UpdateText();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif

        if (Input.GetKeyDown(KeyCode.E))
            Explore();
        if (Input.GetKeyDown(KeyCode.W))
            Work();
        if (Input.GetKeyDown(KeyCode.R))
            Rest();
        if (Input.GetKeyDown(KeyCode.T))
            Travel();
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
            player.energy = Mathf.Min(player.energy + 50, 100);
            lastAction = $"You rest on {(location == "City" ? "street" : "grass")}. It's a new day.";
        }
        UpdateText();
    }

    public void Work()
    {
        if (location != "City")
            lastAction = "You can't work here.";
        else if (hour > 16)
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
            AddHour();
        }
        UpdateText();
    }

    private void UpdateText()
    {
        string str = $"{location}   Day: {day} {hour}:00\n" +
            $"Level: {player.level} ({player.ExpP}%)\n" +
            $"Health: {player.hp}/{player.hpMax}   Energy: {player.energy}/100\n" +
            $"Attack: {player.attack}   Defence: {player.defence}\n" +
            $"Gold: {player.gold}";
        if (lastAction != null)
        {
            str += "\n\n";
            str += lastAction;
            lastAction = null;
        }

        text.text = str;
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

        while (true)
        {
            // player attack
            if (Random.Range(0, 100) > 25)
            {
                enemyHp -= player.attack - enemyDef;
                if (enemyHp <= 0)
                    return true;
            }

            // enemy attack
            if (Random.Range(0, 100) > 75)
            {
                player.hp -= Mathf.Max(enemyAttack - player.defence);
                if (player.hp <= 0)
                {
                    player.hp = 1;
                    return false;
                }
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
            if (location == "City" && player.gold > 0)
            {
                player.hp = player.hpMax;
                player.energy = 100;
                --player.gold;
                lastAction += " It's a new day and you rest in inn (-1 gold).";
            }
            else
            {
                player.energy = Mathf.Min(player.energy + 50, 100);
                lastAction += $" It's a new day and you rest on {(location == "City" ? "street" : "grass")}.";
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
}
