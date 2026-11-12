using System.Linq;

public class Spell
{
    public enum Type
    {
        Damage,
        Heal,
        Buff
    }

    public string name;
    public Type type;
    public DamageType damageType;
    public Effect.Type effectType;
    public int mana, duration;
    public bool aoe;

    public static Spell Get(string name)
    {
        return spells.First(x => x.name == name);
    }

    public static readonly Spell[] spells = new Spell[]
    {
        new()
        {
            name = "heal",
            type = Type.Heal,
            mana = 25
        },
        new()
        {
            name = "holy bolt",
            type = Type.Damage,
            damageType = DamageType.Holy,
            mana = 25
        },
        new()
        {
            name = "holy wave",
            type = Type.Damage,
            damageType = DamageType.Holy,
            mana = 50,
            aoe = true
        },
        new()
        {
            name = "guard",
            type = Type.Buff,
            effectType = Effect.Type.Guard,
            duration = 2
        }
    };
}
