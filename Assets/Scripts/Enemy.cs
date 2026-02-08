using System.Linq;

public class Enemy
{
    public string name, location;
    public int level, hp, attack, def, gold;

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
            gold = 15
        },
        new()
        {
            name = "orc",
            location = "Forest",
            level = 1,
            hp = 75,
            attack = 18,
            def = 3,
            gold = 30
        },
        new()
        {
            name = "minotaur",
            location = "Mountains",
            level = 2,
            hp = 100,
            attack = 25,
            def = 5,
            gold = 50
        }
    };
}
