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
}
