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

    public override string nameYou => "you";
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

    public bool HaveProperty(string name, bool isActive = false)
    {
        Property property = properties.FirstOrDefault(x => x.name == name);
        if (property == null)
            return false;

        if (isActive)
            return !property.events.Any(x => x.name == "Infested");
        else
            return true;
    }

    public bool HavePropertyUpgrade(string propertyName, string upgradeName)
    {
        Property property = properties.FirstOrDefault(x => x.name == propertyName);
        if (property == null || property.upgrades == null)
            return false;
        return property.upgrades.FirstOrDefault(x => x.name == upgradeName)?.active ?? false;
    }
}
