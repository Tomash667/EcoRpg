using System.Linq;

public class Property
{
    public enum Status
    {
        Buy,
        Sell,
        Build,
        Building,
        Infested
    }

    public string name, desc;
    public int value, income, buildPrice, buildTime;

    public string ToString(Status status)
    {
        switch (status)
        {
        default:
        case Status.Buy:
            return $"{name} ({desc}, {value} gold)";
        case Status.Sell:
            return $"{name} ({desc}, {value / 2} gold)";
        case Status.Build:
            return $"{name} ({buildTime} days to build, {desc}, {buildPrice} gold)";
        case Status.Building:
            {
                Game game = Global.Game;
                int days = name == "Silver mine" ? game.silverMineTimer : game.goldMineTimer;
                return $"{name} ({Utility.Plural("day", days)} left, {desc})";
            }
        case Status.Infested:
            return $"{name} (<s>{desc}, {value / 2} gold</s>)";
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
