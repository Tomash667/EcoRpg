using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class Utility
{
    private static readonly string[] counter = new string[]
    {
        "one",
        "two",
        "three",
        "four",
        "five",
        "six",
        "seven",
        "eight",
        "nine"
    };

    private static readonly Dictionary<string, string> plurals = new()
    {
        ["dragon-man"] = "dragon-men",
        ["elf"] = "elves",
        ["harpy"] = "harpies",
        ["meat"] = "meat",
        ["mummy"] = "mummies",
        ["trophy"] = "trophies",
        ["wolf"] = "wolves"
    };

    public static int Rand => UnityEngine.Random.Range(0, int.MaxValue);

    public static float Random()
    {
        return UnityEngine.Random.Range(0f, 1f);
    }

    public static int Random(int a, int b)
    {
        return UnityEngine.Random.Range(a, b + 1);
    }

    public static int Round(int value)
    {
        if (value < 100)
            return value;
        int digits = (int)Mathf.Log10(value);
        int step = (int)Mathf.Pow(10, digits - 1);
        return Mathf.RoundToInt(value / (float)step) * step;
    }

    public static string A(string word)
    {
        char c = word[0];
        if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' || c == 'A' || c == 'E' || c == 'I' || c == 'O' || c == 'U')
            return "an " + word;
        else
            return "a " + word;
    }

    public static string S(string word, bool addS, string optional = null)
    {
        if (addS)
        {
            if (optional != null)
                return optional;
            else
                return word + 's';
        }
        else
            return word;
    }

    public static string Plural(string word)
    {
        // handle enchanted items: magic sword +1 -> magic swords +1
        int plusPos = word.LastIndexOf('+');
        if (plusPos != -1)
            return Plural(word[..^3]) + word.Substring(word.Length - 3, 3);

        int spacePos = word.LastIndexOf(' ');
        if (spacePos != -1)
        {
            string start = word[0..spacePos];
            string end = word[(spacePos + 1)..];
            return $"{start} {Plural(end)}";
        }
        if (plurals.TryGetValue(word, out string plural))
            return plural;
        if (word.EndsWith('s') || word.EndsWith("ch"))
            return word + "es";
        return word + 's';
    }

    public static string Plural(string word, int count, bool alwaysShowNumber = false)
    {
        if (count == 1)
        {
            if (alwaysShowNumber)
                return $"1 {word}";
            else
                return A(word);
        }
        else
            return $"{count} {Plural(word)}";
    }

    public static string PluralText(string word, int count)
    {
        if (count == 1)
            return A(word);
        else if (count < 10)
            return $"{counter[count - 1]} {Plural(word)}";
        else
            return $"{count} {Plural(word)}";
    }

    public static string PrettyList(IList<string> items)
    {
        if (items.Count == 1)
            return items[0];
        else if (items.Count == 2)
            return $"{items[0]} and {items[1]}";
        else
        {
            StringBuilder sb = new();
            int index = 0;
            foreach (string item in items)
            {
                if (index > 0 && index < items.Count - 1)
                    sb.Append(", ");
                else if (index == items.Count - 1)
                    sb.Append(" and ");

                sb.Append(item);
                ++index;
            }
            return sb.ToString();
        }
    }

    public static string PrettyList(IEnumerable<string> items)
    {
        return PrettyList(items.ToArray());
    }

    public static string PrettyGroup(IEnumerable<string> items)
    {
        var groups = items.GroupBy(x => x).ToArray();
        if (groups.Length == 1)
            return PluralText(groups[0].Key, groups[0].Count());
        else if (groups.Length == 2)
            return $"{PluralText(groups[0].Key, groups[0].Count())} and {PluralText(groups[1].Key, groups[1].Count())}";
        else
        {
            StringBuilder sb = new();
            int index = 0;
            foreach (var group in groups)
            {
                if (index > 0 && index < groups.Length - 1)
                    sb.Append(", ");
                else if (index == groups.Length - 1)
                    sb.Append(" and ");

                sb.Append(PluralText(group.Key, group.Count()));
                ++index;
            }
            return sb.ToString();
        }
    }
}
