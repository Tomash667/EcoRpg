using System.Linq;
using UnityEngine;

public class Enemy
{
    public enum Pronoun
    {
        He,
        She,
        It
    }

    public enum AttackType
    {
        Normal,
        Ranged,
        Poison,
        LifeSteal,
        Confuse
    }

    public string name, ally;
    public Vector2Int gold, attacks = new(1, 1);
    public (TileType tileType, int difficulty)[] locations;
    public (Item item, float chance)[] drops;
    public int level, hp, attack, def, dex, difficulty;
    public AttackType attackType;
    public Pronoun pronoun;
    public bool blocks, fireball, firebreath, darkbolt, summon;

    public string him
    {
        get
        {
            return pronoun switch
            {
                Pronoun.He => "him",
                Pronoun.She => "her",
                Pronoun.It => "it",
                _ => ""
            };
        }
    }
    public string Portrait => $"Portraits/{name}";

    public static Enemy Get(string name)
    {
        return enemies.First(x => x.name == name);
    }

    public static Enemy TryGet(string name)
    {
        return enemies.FirstOrDefault(x => x.name == name);
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
        return enemies.RandomItem(x => x.difficulty == difficulty);
    }

    public static Enemy[] enemies = new Enemy[]
    {
        new()
        {
            name = "bandit",
            locations = new[] { (TileType.City, 1), (TileType.Village, 1) },
            difficulty = 1,
            level = 0,
            hp = 60,
            attack = 15,
            def = 3,
            dex = 6,
            gold = new(10, 15),
            pronoun = Pronoun.He
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
            drops = new[] { (Item.Get("meat"), 0.5f), (Item.Get("trophy"), 0.1f) },
            pronoun = Pronoun.It
        },
        new()
        {
            name = "wolf",
            locations = new[] { (TileType.Forest, 1) },
            difficulty = 1,
            level = 0,
            hp = 60,
            attack = 15,
            def = 3,
            dex = 6,
            drops = new[] { (Item.Get("meat"), 0.5f), (Item.Get("trophy"), 0.1f) },
            pronoun = Pronoun.It
        },
        new()
        {
            name = "elf",
            locations = new[] { (TileType.Forest, 1), (TileType.Sawmill, 1) },
            difficulty = 1,
            level = 1,
            hp = 80,
            attack = 20,
            def = 3,
            dex = 9,
            gold = new(20, 30),
            attackType = AttackType.Ranged,
            pronoun = Pronoun.She
        },
        new()
        {
            name = "orc",
            locations = new[] { (TileType.Forest, 2), (TileType.Sawmill, 1) },
            difficulty = 1,
            level = 2,
            hp = 110,
            attack = 27,
            def = 5,
            dex = 8,
            gold = new(30, 45),
            pronoun = Pronoun.He
        },
        new()
        {
            name = "dryad",
            locations = new[] { (TileType.Forest, 2) },
            difficulty = 1,
            level = 3,
            hp = 120,
            attack = 30,
            def = 5,
            dex = 13,
            gold = new(40, 60),
            pronoun = Pronoun.She
        },
        new()
        {
            name = "giant spider",
            locations = new[] { (TileType.Forest, 3), (TileType.Cave, 2), (TileType.Mine, 2) },
            difficulty = 2,
            level = 5,
            hp = 160,
            attack = 40,
            def = 8,
            dex = 16,
            drops = new[] { (Item.Get("trophy"), 0.75f) },
            attackType = AttackType.Poison,
            pronoun = Pronoun.It
        },
        new()
        {
            name = "tentacle monster",
            locations = new[] { (TileType.Forest, 3) },
            difficulty = 2,
            level = 6,
            hp = 180,
            attack = 45,
            attacks = new(1, 2),
            def = 9,
            dex = 18,
            drops = new[] { (Item.Get("meat"), 2.5f), (Item.Get("trophy"), 0.8f) },
            pronoun = Pronoun.It
        },
        new()
        {
            name = "harpy",
            locations = new[] { (TileType.Mountains, 1) },
            difficulty = 1,
            level = 2,
            hp = 100,
            attack = 25,
            def = 4,
            dex = 11,
            gold = new(30, 45),
            pronoun = Pronoun.She
        },
        new()
        {
            name = "minotaur",
            locations = new[] { (TileType.Mountains, 2) },
            difficulty = 2,
            level = 4,
            hp = 160,
            attack = 40,
            def = 6,
            dex = 12,
            gold = new(50, 75),
            pronoun = Pronoun.He
        },
        new()
        {
            name = "small dragon",
            locations = new[] { (TileType.Mountains, 3) },
            difficulty = 3,
            level = 7,
            hp = 200,
            attack = 50,
            def = 10,
            dex = 20,
            gold = new(80, 120),
            drops = new[] { (Item.Get("meat"), 1.5f) },
            pronoun = Pronoun.It,
            fireball = true
        },
        new()
        {
            name = "bear",
            locations = new[] { (TileType.Cave, 1), (TileType.Mine, 1) },
            difficulty = 1,
            level = 3,
            hp = 130,
            attack = 32,
            def = 6,
            dex = 10,
            drops = new[] { (Item.Get("meat"), 1.5f), (Item.Get("trophy"), 0.5f) },
            pronoun = Pronoun.It
        },
        new()
        {
            name = "purple worm",
            locations = new[] { (TileType.Cave, 3), (TileType.Mine, 3) },
            difficulty = 3,
            level = 8,
            hp = 220,
            attack = 55,
            def = 11,
            dex = 22,
            drops = new[] { (Item.Get("meat"), 3.25f), (Item.Get("trophy"), 1.05f) },
            pronoun = Pronoun.It
        },
        new()
        {
            name = "skeleton",
            ally = "zombie",
            locations = new[] { (TileType.Dungeon, 1) },
            difficulty = 1,
            level = 3,
            hp = 120,
            attack = 30,
            def = 6,
            dex = 12,
            gold = new(40, 60),
            pronoun = Pronoun.It
        },
        new()
        {
            name = "zombie",
            ally = "skeleton",
            locations = new[] { (TileType.Dungeon, 1) },
            difficulty = 1,
            level = 3,
            hp = 130,
            attack = 32,
            def = 6,
            dex = 8,
            gold = new(40, 60),
            pronoun = Pronoun.It,
            blocks = true
        },
        new()
        {
            name = "mummy",
            ally = "vampire",
            locations = new[] { (TileType.Dungeon, 2) },
            difficulty = 2,
            level = 5,
            hp = 180,
            attack = 45,
            def = 9,
            dex = 10,
            gold = new(60, 90),
            pronoun = Pronoun.It,
            blocks = true
        },
        new()
        {
            name = "vampire",
            ally = "mummy",
            locations = new[] { (TileType.Dungeon, 2) },
            difficulty = 2,
            level = 6,
            hp = 180,
            attack = 45,
            def = 8,
            dex = 20,
            gold = new(70, 105),
            attackType = AttackType.LifeSteal,
            pronoun = Pronoun.He
        },
        new()
        {
            name = "demon",
            locations = new[] { (TileType.Dungeon, 3) },
            difficulty = 3,
            level = 9,
            hp = 240,
            attack = 60,
            def = 12,
            dex = 24,
            gold = new(100, 150),
            pronoun = Pronoun.He,
            fireball = true
        },
        new()
        {
            name = "lich",
            locations = new[] { (TileType.Dungeon, 3) },
            difficulty = 3,
            level = 10,
            hp = 240,
            attack = 70,
            def = 12,
            dex = 26,
            gold = new(110, 165),
            pronoun = Pronoun.He,
            darkbolt = true,
            summon = true
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
            pronoun = Pronoun.He,
            blocks = true
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
            pronoun = Pronoun.It,
            firebreath = true
        },
        new()
        {
            name = "giant crocodile",
            locations = new[] { (TileType.Swamp, 2) },
            difficulty = 2,
            level = 6,
            hp = 180,
            attack = 45,
            def = 8,
            dex = 18,
            drops = new[] { (Item.Get("meat"), 2.5f), (Item.Get("trophy"), 0.8f) },
            pronoun = Pronoun.It
        },
        new()
        {
            name = "hydra",
            locations = new[] { (TileType.Swamp, 3) },
            difficulty = 3,
            level = 9,
            hp = 240,
            attack = 55,
            attacks = new(2, 3),
            def = 11,
            dex = 24,
            drops = new[] { (Item.Get("meat"), 3.5f), (Item.Get("trophy"), 1.2f) },
            attackType = AttackType.Poison,
            pronoun = Pronoun.It
        },
        new()
        {
            name = "skittering horror",
            locations = new[] { (TileType.DarkDimension, 4) },
            level = 13,
            hp = 320,
            attack = 80,
            def = 15,
            dex = 40,
            gold = new(140, 210),
            attackType = AttackType.LifeSteal,
            pronoun = Pronoun.It
        },
        new()
        {
            name = "hulking horror",
            locations = new[] { (TileType.DarkDimension, 4) },
            level = 13,
            hp = 400,
            attack = 80,
            def = 18,
            dex = 32,
            gold = new(140, 210),
            pronoun = Pronoun.It,
            blocks = true
        },
        new()
        {
            name = "deadly horror",
            locations = new[] { (TileType.DarkDimension, 4) },
            level = 13,
            hp = 320,
            attack = 100,
            def = 15,
            dex = 32,
            gold = new(140, 210),
            attackType = AttackType.Poison,
            pronoun = Pronoun.It
        },
        new()
        {
            name = "nameless horror",
            level = 20,
            hp = 460,
            attack = 115,
            def = 22,
            dex = 46,
            gold = new(420, 630),
            attackType = AttackType.Confuse,
            pronoun = Pronoun.It
        }
    };
}
