using System.Linq;

public class Property
{
    public string name, desc;
    public int value, income, buildPrice, buildTime;

    public string ToString(bool sellPrice, bool build)
    {
        if (build)
        {
            if (sellPrice)
            {
                Game game = Global.Game;
                int days = name == "Silver mine" ? game.silverMineTimer : game.goldMineTimer;
                return $"{name} ({Utility.Plural("day", days)} left, {desc})";
            }
            else
                return $"{name} ({buildTime} days to build, {desc}, {buildPrice} gold)";
        }
        else
        {
            int price = sellPrice ? value / 2 : value;
            return $"{name} ({desc}, {price} gold)";
        }
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
            name = "Iron mine",
            desc = "10 gold/day",
            value = 10000,
            income = 10
        },
        new()
        {
            name = "Silver mine",
            desc = "25 gold/day",
            value = 25000,
            income = 25,
            buildPrice = 6000,
            buildTime = 20
        },
        new()
        {
            name = "Gold mine",
            desc = "50 gold/day",
            value = 50000,
            income = 50,
            buildPrice = 7500,
            buildTime = 30
        }
    };
}
