public static class Extensions
{
    public static string ToUpper1(this string str)
    {
        return char.ToUpper(str[0]) + str[1..];
    }
}
