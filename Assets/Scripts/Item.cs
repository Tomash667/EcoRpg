using System;

public class Item
{
    public enum Type
    {
        Weapon,
        Armor
    }

    public string name;
    public Type type;
    public int power, value;

    public override string ToString()
    {
        return $"{name.ToUpper1()} ({power} {(type == Type.Weapon ? "attack" : "defence")}, {value} gold)";
    }

    public static readonly Item[] items = new Item[]
    {
        new()
        {
            name = "club",
            type = Type.Weapon,
            power = 5,
            value = 25
        },
        new()
        {
            name = "axe",
            type = Type.Weapon,
            power = 10,
            value = 100
        },
        new()
        {
            name = "sword",
            type = Type.Weapon,
            power = 15,
            value = 400
        },
        new()
        {
            name = "leather armor",
            type = Type.Armor,
            power = 2,
            value = 25
        },
        new()
        {
            name = "chainmail",
            type = Type.Armor,
            power = 4,
            value = 100
        },
        new()
        {
            name = "plate armor",
            type = Type.Armor,
            power = 6,
            value = 400
        }
    };
}

[Serializable]
public class ItemSlot
{
    public Item item;
    public int count;

    public override string ToString()
    {
        if (count == 1)
            return item.ToString();
        else
            return $"{count}x {item}";
    }
}
