public class Effect
{
    public enum Type
    {
        None,
        Poison,
        Confused,
        Guard
    }

    public Type type;
    public int timer, value;
}
