using System;
using System.Linq;
using UnityEngine;

public class Item
{
    public enum Type
    {
        Weapon,
        Armor,
        Usable,
        Other
    }

    public const int MaxLevel = 3;

    public string name, desc;
    public Type type;
    public int level, power, value;

    public string ToString(bool sellPrice)
    {
        int price = sellPrice ? value / 2 : value;
        return type switch
        {
            Type.Weapon => $"{name.ToUpper1()} ({power} attack, {price} gold)",
            Type.Armor => $"{name.ToUpper1()} ({power} defense, {price} gold)",
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
            name = "plate armor",
            type = Type.Armor,
            level = 3,
            power = 6,
            value = 400
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
