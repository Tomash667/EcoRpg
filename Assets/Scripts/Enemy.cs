using System.Linq;
using UnityEngine;

public class Enemy
{
    public string name, location;
    public Vector2Int gold;
    public int level, hp, attack, def;

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
            hp = 50,
            attack = 12,
            def = 2,
            gold = new(10, 15)
        },
        new()
        {
            name = "orc",
            location = "Forest",
            level = 1,
            hp = 75,
            attack = 18,
            def = 3,
            gold = new(20, 30)
        },
        new()
        {
            name = "elf",
            location = "Forest",
            level = 1,
            hp = 75,
            attack = 18,
            def = 3,
            gold = new(20, 30)
        },
        new()
        {
            name = "minotaur",
            location = "Mountains",
            level = 2,
            hp = 100,
            attack = 25,
            def = 5,
            gold = new(30, 45)
        },
        new()
        {
            name = "demon",
            location = "Dungeon",
            level = 3,
            hp = 125,
            attack = 31,
            def = 6,
            gold = new(40, 60)
        }
    };
}
