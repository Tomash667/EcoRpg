using System;
using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static string ToUpper1(this string str)
    {
        return char.ToUpper(str[0]) + str[1..];
    }

    public static T RandomItem<T>(this IList<T> items)
    {
        return items[Utility.Rand % items.Count];
    }

    public static T RandomItem<T>(this IEnumerable<T> items, Func<T, bool> pred)
    {
        T[] filteredItems = items.Where(pred).ToArray();
        return filteredItems[Utility.Rand % filteredItems.Length];
    }

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            int k = Utility.Rand % n;
            n--;
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
