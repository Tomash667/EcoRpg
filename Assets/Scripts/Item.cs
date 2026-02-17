using System;
using System.Linq;
using UnityEngine;

public class Item
{
    public enum Type
    {
        Weapon,
        Armor,
        Shield,
        Usable,
        Other
    }

    public const int MaxLevel = 4;

    public string name, desc;
    public Type type;
    public int level, power, value;
    public bool shop = true;

    public string ToString(bool sellPrice)
    {
        int price = sellPrice ? value / 2 : value;
        return type switch
        {
            Type.Weapon => $"{name.ToUpper1()} ({power} attack, {price} gold)",
            Type.Armor or Type.Shield => $"{name.ToUpper1()} ({power} defense, {price} gold)",
            _ => $"{name.ToUpper1()} ({desc}, {price} gold)"
        };
    }

    public static Item Get(string name)
    {
        return items.First(x => x.name == name);
    }

    public static readonly Item[] items = new Item[]
    {
        new()
        {
            name = "club",
            type = Type.Weapon,
            level = 1,
            power = 5,
            value = 25
        },
        new()
        {
            name = "axe",
            type = Type.Weapon,
            level = 2,
            power = 10,
            value = 100
        },
        new()
        {
            name = "sword",
            type = Type.Weapon,
            level = 3,
            power = 15,
            value = 400
        },
        new()
        {
            name = "two handed sword",
            type = Type.Weapon,
            level = 4,
            power = 20,
            value = 1500
        },
        new()
        {
            name = "leather armor",
            type = Type.Armor,
            level = 1,
            power = 2,
            value = 25
        },
        new()
        {
            name = "chainmail",
            type = Type.Armor,
            level = 2,
            power = 4,
            value = 100
        },
        new()
        {
            name = "breastplate",
            type = Type.Armor,
            level = 3,
            power = 6,
            value = 400
        },
        new()
        {
            name = "plate armor",
            type = Type.Armor,
            level = 4,
            power = 8,
            value = 1500
        },
        new()
        {
            name = "wooden shield",
            type = Type.Shield,
            level = 1,
            power = 1,
            value = 25
        },
        new()
        {
            name = "iron shield",
            type = Type.Shield,
            level = 2,
            power = 2,
            value = 100
        },
        new()
        {
            name = "steel shield",
            type = Type.Shield,
            level = 3,
            power = 3,
            value = 400
        },
        new()
        {
            name = "magic shield",
            type = Type.Shield,
            level = 4,
            power = 4,
            value = 1500
        },
        new()
        {
            name = "herb",
            desc = "25 heal",
            type = Type.Usable,
            power = 25,
            value = 5,
            shop = false
        },
        new()
        {
            name = "potion",
            desc = "100 heal",
            type = Type.Usable,
            power = 100,
            value = 10
        },
        new()
        {
            name = "rations",
            desc = "traveler's food",
            type = Type.Other,
            value = 5
        },
        new()
        {
            name = "tent",
            desc = "better rest outside",
            type = Type.Other,
            value = 100
        },
        new()
        {
            name = "pickaxe",
            desc = "miner's tool",
            type = Type.Other,
            value = 25
        },
        new()
        {
            name = "gold nugget",
            desc = "treasure",
            type = Type.Other,
            value = 50,
            shop = false
        }
    };
}

[Serializable]
public class ItemSlot : ISerializationCallbackReceiver
{
    public Item item;
    public string name;
    public int count;

    public void OnBeforeSerialize()
    {
        name = item.name;
    }

    public void OnAfterDeserialize()
    {
        item = Item.Get(name);
    }

    public string ToString(bool sellPrice)
    {
        if (count == 1)
            return item.ToString(sellPrice);
        else
            return $"{count}x {item.ToString(sellPrice)}";
    }
}
