using System.Collections.Generic;

public static class Utility
{
    private static readonly string[] counter = new string[]
    {
        "one",
        "two",
        "three",
        "four"
    };

    private static readonly Dictionary<string, string> plurals = new()
    {
        ["dragon-man"] = "dragon-men",
        ["elf"] = "elves",
        ["harpy"] = "harpies",
        ["mummy"] = "mummies",
        ["rations"] = "rations",
        ["wolf"] = "wolves"
    };

    public static int Rand => UnityEngine.Random.Range(0, int.MaxValue);

    public static int Random(int a, int b)
    {
        return UnityEngine.Random.Range(a, b + 1);
    }

    public static string Plural(string word)
    {
        if (plurals.TryGetValue(word, out string plural))
            return plural;
        if (word.EndsWith('s') || word.EndsWith("ch"))
            return word + "es";
        return word + 's';
    }

    public static string Plural(string word, int count)
    {
        if (count == 1)
            return word;
        else
            return $"{count} {Plural(word)}";
    }

    public static string PluralText(string word, int count)
    {
        if (count == 1)
            return word;
        else
            return $"{counter[count - 1]} {Plural(word)}";
    }
}
