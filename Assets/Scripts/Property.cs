using System;

[Serializable]
public class Property
{
    [Serializable]
    public class Upgrade
    {
        public string name, desc;
        public int value;
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
    public Upgrade[] upgrades;
    public Status status;
    public int value, income, buildPrice, buildTime, locationIndex;

    public string ToString(DescStatus status)
    {
        return status switch
        {
            DescStatus.Sell => $"{name} ({desc}, {value / 2} gold)",
            DescStatus.Build => $"{name} ({buildTime} days to build, {desc}, {buildPrice} gold)",
            DescStatus.Building => $"{name} ({Utility.Plural("day", buildTime)} left, {desc})",
            DescStatus.Infested => $"{name} (<s>{desc}, {value / 2} gold</s>)",
            _ => $"{name} ({desc}, {value} gold)"
        };
    }
}
