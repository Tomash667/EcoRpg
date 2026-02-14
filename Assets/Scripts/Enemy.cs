using System.Linq;
using UnityEngine;

public class Enemy
{
    public string name, location;
    public Vector2Int gold;
    public int level, hp, attack, def, dex;
    public bool quest = true;

    public static Enemy Get(string name)
    {
        return enemies.First(x => x.name == name);
    }

    public static Enemy[] enemies = new Enemy[]
    {
        new()
        {
            name = "bandit",
            location = "City",
            level = 0,
            hp = 60,
            attack = 15,
            def = 3,
            dex = 6,
            gold = new(10, 15)
        },
        new()
        {
            name = "giant rat",
            location = "Sewers",
            level = 0,
            hp = 60,
            attack = 15,
            def = 3,
            dex = 6,
            quest = false
        },
        new()
        {
            name = "orc",
            location = "Forest",
            level = 1,
            hp = 90,
            attack = 22,
            def = 4,
            dex = 6,
            gold = new(20, 30)
        },
        new()
        {
            name = "elf",
            location = "Forest",
            level = 1,
            hp = 80,
            attack = 20,
            def = 3,
            dex = 9,
            gold = new(20, 30)
        },
        new()
        {
            name = "minotaur",
            location = "Mountains",
            level = 2,
            hp = 110,
            attack = 27,
            def = 5,
            dex = 8,
            gold = new(30, 45)
        },
        new()
        {
            name = "harpy",
            location = "Mountains",
            level = 2,
            hp = 100,
            attack = 25,
            def = 4,
            dex = 11,
            gold = new(30, 45)
        },
        new()
        {
            name = "demon",
            location = "Dungeon",
            level = 3,
            hp = 120,
            attack = 30,
            def = 6,
            dex = 12,
            gold = new(40, 60)
        },
        new()
        {
            name = "vampire",
            location = "Dungeon",
            level = 3,
            hp = 125,
            attack = 30,
            def = 5,
            dex = 13,
            gold = new(40, 60)
        },
        new()
        {
            name = "dragon",
            location = "Cave",
            level = 10,
            hp = 250,
            attack = 62,
            def = 12,
            dex = 24,
            gold = new(10000, 10000),
            quest = false
        }
    };
}
