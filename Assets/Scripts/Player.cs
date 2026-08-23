using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Player : Hero
{
    public List<Property> properties;
    public float guildProgress;
    public int goldWaiting, energy, guildRank;

    [NonSerialized]
    public int goldReceived;

    public override string nameYou => "you";
    public override string nameYour => "your";
    public override string NameYou => "You";
    public override string He => "You";
    public override string him => "you";
    public override string isAre => "are";

    public void Init()
    {
        properties = new();
        InitCommon();
        energy = 100;
        gold = 25;
    }

    public override void AddGold(int value)
    {
        gold += value;
        goldReceived += value;
        if (value > 0 && owedGold > 0 && gold > MinGold)
            Global.Game.team.PayOwedGold(this);
    }

    public bool HaveProperty(string name, bool isActive = false, int cityIndex = -1)
    {
        Property property = properties.FirstOrDefault(x => x.name == name && (cityIndex == -1 || x.cityIndex == cityIndex));
        if (property == null)
            return false;

        if (isActive)
            return !property.events.Any(x => x.name == "Infested");
        else
            return true;
    }

    public bool HaveProperty(int locationIndex)
    {
        return properties.Any(x => x.locationIndex == locationIndex);
    }

    public bool HavePropertyUpgrade(string propertyName, string upgradeName, int cityIndex = -1)
    {
        Property property = properties.FirstOrDefault(x => x.name == propertyName && (cityIndex == -1 || x.cityIndex == cityIndex));
        if (property == null || property.upgrades == null)
            return false;
        return property.upgrades.FirstOrDefault(x => x.name == upgradeName)?.active ?? false;
    }

    public void TurnItemsNonTeam()
    {
        ItemSlot[] teamItems = items.Where(x => x.team).ToArray();
        foreach (ItemSlot teamItem in teamItems)
        {
            ItemSlot itemSlot = items.FirstOrDefault(x => x.item == teamItem.item && !x.team);
            if (itemSlot != null)
            {
                itemSlot.count += teamItem.count;
                items.Remove(teamItem);
            }
            else
                teamItem.team = false;
        }
    }
}
