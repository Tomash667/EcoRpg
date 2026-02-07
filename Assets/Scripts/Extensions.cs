using System.Collections.Generic;

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
}
