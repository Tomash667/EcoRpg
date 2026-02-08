public static class Utility
{
    private static readonly string[] counter = new string[]
    {
        "one",
        "two",
        "three",
        "four"
    };

    public static int Rand => UnityEngine.Random.Range(0, int.MaxValue);

    public static int Random(int a, int b)
    {
        return UnityEngine.Random.Range(a, b + 1);
    }

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
