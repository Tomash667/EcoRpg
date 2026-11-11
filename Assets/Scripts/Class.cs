using System.Collections.Generic;

public enum Class
{
    None = -1,
    Warrior,
    Archer,
    Cleric
}

public class ClassInfo
{
    public string name;
    public Spell[] spells;
    public (Spell spell, int level)[] unlockableSpells;
    public int attack, defense, dex;
    public bool backRow, useMana;
}

public static class ClassMethods
{
    public static readonly Class[] all = new[] { Class.Archer, Class.Cleric, Class.Warrior };

    public static readonly int defaultClass = all.IndexOf(Class.Warrior);

    public static readonly Dictionary<Class, ClassInfo> classes = new()
    {
        [Class.Warrior] = new()
        {
            name = "warrior",
            attack = 25,
            defense = 5,
            dex = 10
        },
        [Class.Archer] = new()
        {
            name = "archer",
            attack = 30,
            defense = 3,
            dex = 15,
            backRow = true
        },
        [Class.Cleric] = new()
        {
            name = "cleric",
            spells = new Spell[]
            {
                Spell.Get("heal"),
                Spell.Get("holy bolt")
            },
            unlockableSpells = new (Spell, int)[]
            {
                (Spell.Get("holy wave"), 10)
            },
            attack = 20,
            defense = 4,
            dex = 8,
            useMana = true
        }
    };

    public static string AsString(this Class clas)
    {
        if (classes.TryGetValue(clas, out ClassInfo info))
            return info.name;
        else
            return $"[ERROR class {clas}]";
    }

    public static Class GetRandom(Race race)
    {
        Class favorite = race.GetFavoriteClass();
        if (favorite != Class.None && Utility.Rand % all.Length == 0)
            return favorite;
        else
            return all.RandomItem();
    }

    public static ClassInfo GetInfo(this Class clas)
    {
        return classes[clas];
    }
}
