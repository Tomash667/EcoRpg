using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Player : Hero
{
    public List<Property> properties;
    public int goldWaiting, energy;
    [NonSerialized]
    public int goldReceived;

    public override string him => "you";

    public new void Init()
    {
        properties = new();
        InitCommon();
        energy = 100;
        gold = 100;
    }

    public void AddGold(int value)
    {
        gold += value;
        goldReceived += value;
    }

    public bool HaveProperty(string name)
    {
        return properties.Any(x => x.name == name);
    }

    public bool HavePropertyUpgrade(string propertyName, string upgradeName)
    {
        Property property = properties.FirstOrDefault(x => x.name == propertyName);
        if (property == null || property.upgrades == null)
            return false;
        return property.upgrades.FirstOrDefault(x => x.name == upgradeName)?.active ?? false;
    }
}
