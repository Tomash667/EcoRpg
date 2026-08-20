public enum Race
{
    None,
    Human,
    Elf,
    Dwarf
}

public static class RaceMethods
{
    public static readonly Race[] all = new Race[] { Race.Dwarf, Race.Elf, Race.Human };

    public static readonly Race[] random = new Race[] { Race.Human, Race.Human, Race.Dwarf, Race.Elf };

    public static readonly int defaultRace = all.IndexOf(Race.Human);

    public static string AsString(this Race race)
    {
        return race switch
        {
            Race.Human => "human",
            Race.Elf => "elf",
            Race.Dwarf => "dwarf",
            _ => $"[ERROR race {race}]"
        };
    }
}
