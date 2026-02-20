using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public static int Random(this Vector2Int range)
    {
        return Utility.Random(range.x, range.y);
    }

    public static T WeightedRandom<T>(this Dictionary<T, int> values)
    {
        int total = values.Values.Sum();
        int c = Utility.Rand % total;
        int k = 0;
        foreach ((T t, int weight) in values)
        {
            k += weight;
            if (c < k)
                return t;
        }
        return values.First().Key;
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

    public static GameObject FindGameObject(this Transform transform, string name)
    {
        Transform result = transform.Find(name);
        if (result != null)
            return result.gameObject;
        return null;
    }
}
