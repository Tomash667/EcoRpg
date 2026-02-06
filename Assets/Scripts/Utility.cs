public static class Utility
{
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
}
