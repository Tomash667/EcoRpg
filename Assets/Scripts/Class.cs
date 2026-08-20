public enum Class
{
    Warrior,
    Archer
}

public static class ClassMethods
{
    public static readonly Class[] all = new[] { Class.Archer, Class.Warrior };

    public static readonly int defaultClass = all.IndexOf(Class.Warrior);

    public static string AsString(this Class clas)
    {
        return clas switch
        {
            Class.Warrior => "warrior",
            Class.Archer => "archer",
            _ => $"[ERROR class {clas}]"
        };
    }

    public static Class GetRandom(Race race)
    {
        return race switch
        {
            Race.Elf => Utility.Rand % 3 != 0 ? Class.Archer : Class.Warrior,
            Race.Dwarf => Utility.Rand % 3 != 0 ? Class.Warrior : Class.Archer,
            _ => all.RandomItem()
        };
    }
}
