using UnityEngine;

public static class Utility
{
    private static readonly string[] counter = new string[]
    {
        "one",
        "two",
        "three",
        "four"
    };

    public static int Rand => Random.Range(0, int.MaxValue);

    public static string Plural(string word)
    {
        if (word == "rations")
            return word;
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
