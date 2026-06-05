using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Property
{
    [Serializable]
    public class Event
    {
        public string name;
        public int timer, state;
    }

    [Serializable]
    public class Upgrade
    {
        public string name, desc;
        public int value, income, upkeep;
        public bool active;

        public override string ToString()
        {
            return $"{name} ({desc}, {value} gold)";
        }
    }

    public enum Status
    {
        None,
        Cleared,
        Building,
        Active
    }

    public enum DescStatus
    {
        Buy,
        Sell,
        Build,
        Building,
        Infested
    }

    public string name, desc;
    public List<Event> events = new();
    public Upgrade[] upgrades;
    public Status status;
    public int value, infestedCost, income, upkeep, upkeepDiscount, buildPrice, buildPriceDiscount, buildTime, locationIndex;

    public int Income
    {
        get
        {
            if (events.Count == 0)
                return income;

            if (events[0].name == "Infested")
                return 0;
            else
                return income * 3 / 2;
        }
    }
    public int Upkeep
    {
        get
        {
            if (events.Count == 0 || events[0].name != "Infested")
            {
                if (upkeepDiscount != 0 && Global.Player.HaveProperty("Sawmill", true))
                    return upkeep - upkeepDiscount;
                else
                    return upkeep;
            }
            else
                return upkeep / 2;
        }
    }
    public int Profit => Income - Upkeep;
    public int BuildPrice
    {
        get
        {
            if (buildPriceDiscount != 0 && Global.Player.HaveProperty("Sawmill", true))
                return buildPrice - buildPriceDiscount;
            else
                return buildPrice;
        }
    }
    public string Desc => desc.Replace("PROFIT", Profit.ToString()).Replace("UPKEEP", upkeep.ToString());

    public string ToString(DescStatus status)
    {
        return status switch
        {
            DescStatus.Sell => $"{name} ({Desc}, {value / 2} gold)",
            DescStatus.Build => $"{name} ({buildTime} days to build, {Desc}, {BuildPrice} gold)",
            DescStatus.Building => $"{name} ({Utility.Plural("day", buildTime)} left, {Desc})",
            DescStatus.Infested => $"{name} (<color=red>{Desc}, {value / 2} gold</color>)",
            _ => $"{name} ({Desc}, {value} gold)"
        };
    }

    public bool HaveEvent(string eventName)
    {
        return events.Any(x => x.name == eventName);
    }

    public bool HaveUpgrade(string upgradeName)
    {
        if (upgrades == null)
            return false;
        Upgrade upgrade = upgrades.FirstOrDefault(x => x.name == upgradeName);
        return upgrade != null && upgrade.active;
    }

    public bool RemoveEvent(string eventName)
    {
        return events.RemoveFirst(x => x.name == eventName);
    }
}
