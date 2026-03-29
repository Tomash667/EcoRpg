using System;

public enum Class
{
    Warrior,
    Archer
}

public static class ClassMethods
{
    public static readonly Class[] all = new[] { Class.Archer, Class.Warrior };

    public static readonly int defaultClass = Array.IndexOf(all, Class.Warrior);

    public static string AsString(this Class clas)
    {
        return clas switch
        {
            Class.Warrior => "warrior",
            Class.Archer => "archer",
            _ => $"[ERROR class {clas}]"
        };
    }
}
