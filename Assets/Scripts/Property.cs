using System.Linq;

public class Property
{
    public string name, desc;
    public int value, income;

    public string ToString(bool sellPrice)
    {
        int price = sellPrice ? value / 2 : value;
        return $"{name} ({desc}, {price} gold)";
    }

    public static Property Get(string name)
    {
        return properties.First(x => x.name == name);
    }

    public static readonly Property[] properties = new Property[]
    {
        new()
        {
            name = "House",
            desc = "don't pay for inn",
            value = 500
        },
        new()
        {
            name = "Sawmill",
            desc = "5 gold/day",
            value = 5000,
            income = 5
        },
        new()
        {
            name = "Mine",
            desc = "10 gold/day",
            value = 10000,
            income = 10
        }
    };
}
