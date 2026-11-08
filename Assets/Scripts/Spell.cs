public class Spell
{
    public enum Type
    {
        Damage,
        //Heal
    }

    public string name;
    public Type type;
    public DamageType damageType;
    public int mana;

    public static readonly Spell[] spells = new Spell[]
    {
        /*new()
        {
            name = "heal",
            type = Type.Heal
        },*/
        new()
        {
            name = "holy bolt",
            type = Type.Damage,
            damageType = DamageType.Holy,
            mana = 25
        }
    };
}
