using System.Linq;
using UnityEngine;

public class Enemy
{
    public string name;
    public Vector2Int gold;
    public (TileType tileType, int difficulty)[] locations;
    public int level, hp, attack, def, dex;
    public bool quest = true;

    public static Enemy Get(string name)
    {
        return enemies.First(x => x.name == name);
    }

    public static Enemy GetRandom(TileType tileType, int difficulty)
    {
        Enemy[] matchingEnemies = enemies.Where(x => x.locations != null && x.locations.Any(y => y.tileType == tileType && y.difficulty == difficulty)).ToArray();
        if (matchingEnemies.Length == 0)
            return null;
        return matchingEnemies.RandomItem();
    }

    public static Enemy GetRandom(int difficulty)
    {
        return enemies.RandomItem(x => x.quest && x.locations != null && x.locations.Any(y => y.difficulty == difficulty));
    }

    public static Enemy[] enemies = new Enemy[]
    {
        new()
        {
            name = "bandit",
            locations = new[] { (TileType.City, 1) },
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
            locations = new[] { (TileType.Sewers, 1) },
            level = 0,
            hp = 60,
            attack = 15,
            def = 3,
            dex = 6,
            quest = false
        },
        new()
        {
            name = "wolf",
            locations = new[] { (TileType.Forest, 1) },
            level = 0,
            hp = 60,
            attack = 15,
            def = 3,
            dex = 6
        },
        new()
        {
            name = "elf",
            locations = new[] { (TileType.Forest, 1), (TileType.Sawmill, 1) },
            level = 1,
            hp = 80,
            attack = 20,
            def = 3,
            dex = 9,
            gold = new(20, 30)
        },
        new()
        {
            name = "orc",
            locations = new[] { (TileType.Forest, 2), (TileType.Sawmill, 1) },
            level = 2,
            hp = 110,
            attack = 27,
            def = 5,
            dex = 8,
            gold = new(30, 45)
        },
        new()
        {
            name = "dryad",
            locations = new[] { (TileType.Forest, 2) },
            level = 3,
            hp = 120,
            attack = 30,
            def = 5,
            dex = 13,
            gold = new(40, 60)
        },
        new()
        {
            name = "giant spider",
            locations = new[] { (TileType.Forest, 3), (TileType.Cave, 2), (TileType.Mine, 2) },
            level = 5,
            hp = 160,
            attack = 40,
            def = 8,
            dex = 16
        },
        new()
        {
            name = "tentacle monster",
            locations = new[] { (TileType.Forest, 3) },
            level = 6,
            hp = 180,
            attack = 45,
            def = 9,
            dex = 18
        },
        new()
        {
            name = "harpy",
            locations = new[] { (TileType.Mountains, 1) },
            level = 2,
            hp = 100,
            attack = 25,
            def = 4,
            dex = 11,
            gold = new(30, 45)
        },
        new()
        {
            name = "minotaur",
            locations = new[] { (TileType.Mountains, 2) },
            level = 4,
            hp = 160,
            attack = 40,
            def = 6,
            dex = 12,
            gold = new(50, 75)
        },
        new()
        {
            name = "small dragon",
            locations = new[] { (TileType.Mountains, 3) },
            level = 7,
            hp = 200,
            attack = 50,
            def = 10,
            dex = 20,
            gold = new(80, 120)
        },
        new()
        {
            name = "bear",
            locations = new[] { (TileType.Cave, 1), (TileType.Mine, 1) },
            level = 3,
            hp = 130,
            attack = 32,
            def = 6,
            dex = 10
        },
        new()
        {
            name = "purple worm",
            locations = new[] { (TileType.Cave, 3), (TileType.Mine, 3) },
            level = 8,
            hp = 220,
            attack = 55,
            def = 11,
            dex = 22
        },
        new()
        {
            name = "skeleton",
            locations = new[] { (TileType.Dungeon, 1) },
            level = 3,
            hp = 120,
            attack = 30,
            def = 6,
            dex = 12,
            gold = new(40, 60)
        },
        new()
        {
            name = "zombie",
            locations = new[] { (TileType.Dungeon, 1) },
            level = 3,
            hp = 130,
            attack = 32,
            def = 6,
            dex = 8,
            gold = new(40, 60)
        },
        new()
        {
            name = "mummy",
            locations = new[] { (TileType.Dungeon, 2) },
            level = 5,
            hp = 180,
            attack = 45,
            def = 9,
            dex = 10,
            gold = new(60, 90)
        },
        new()
        {
            name = "vampire",
            locations = new[] { (TileType.Dungeon, 2) },
            level = 6,
            hp = 180,
            attack = 45,
            def = 8,
            dex = 20,
            gold = new(70, 105)
        },
        new()
        {
            name = "demon",
            locations = new[] { (TileType.Dungeon, 3) },
            level = 9,
            hp = 240,
            attack = 60,
            def = 12,
            dex = 24,
            gold = new(100, 150)
        },
        new()
        {
            name = "lich",
            locations = new[] { (TileType.Dungeon, 3) },
            level = 10,
            hp = 240,
            attack = 70,
            def = 12,
            dex = 26,
            gold = new(110, 165)
        },
        new()
        {
            name = "dragon-man",
            level = 8,
            hp = 220,
            attack = 55,
            def = 11,
            dex = 22,
            gold = new(90, 135),
            quest = false
        },
        new()
        {
            name = "dragon",
            level = 15,
            hp = 500,
            attack = 90,
            def = 18,
            dex = 26,
            gold = new(10000, 10000),
            quest = false
        }
    };
}
