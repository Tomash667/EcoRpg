using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Player : Hero
{
    [NonSerialized]
    public List<Property> properties;
    public List<string> savedProperties;
    public int goldWaiting, energy;
    [NonSerialized]
    public int goldReceived;

    public new void Init()
    {
        properties = new();
        level = 1;
        exp = 0;
        hpMax = 100;
        hp = hpMax;
        energy = 100;
        gold = 50;
        attack = 25;
        defense = 5;
        dex = 10;
    }

    public void AddGold(int value)
    {
        gold += value;
        goldReceived += value;
    }

    public override void OnBeforeSerialize()
    {
        base.OnBeforeSerialize();
        savedProperties = properties?.Select(x => x.name).ToList();
    }

    public override void OnAfterDeserialize()
    {
        base.OnAfterDeserialize();
        properties = savedProperties?.Select(x => Property.Get(x)).ToList();
    }
}
