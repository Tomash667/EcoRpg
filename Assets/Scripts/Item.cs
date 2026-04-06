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
        Tool,
        Other
    }

    public enum Subtype
    {
        None,
        Melee,
        Bow
    }

    public const int MaxLevelCity = 4;
    public const int MaxLevelVillage = 3;

    public string name, desc;
    public Type type;
    public Subtype subtype;
    public int level, power, value;

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
            subtype = Subtype.Melee,
            level = 1,
            power = 5,
            value = 25
        },
        new()
        {
            name = "axe",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 2,
            power = 10,
            value = 100
        },
        new()
        {
            name = "sword",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 3,
            power = 15,
            value = 400
        },
        new()
        {
            name = "two handed sword",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 4,
            power = 20,
            value = 1500
        },
        new()
        {
            name = "magic sword",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 5,
            power = 25,
            value = 5000
        },

        new()
        {
            name = "short bow",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 1,
            power = 10,
            value = 50
        },
        new()
        {
            name = "long bow",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 2,
            power = 20,
            value = 200
        },
        new()
        {
            name = "composite bow",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 3,
            power = 30,
            value = 800
        },
        new()
        {
            name = "elven bow",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 4,
            power = 40,
            value = 3000
        },
        new()
        {
            name = "magic bow",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 5,
            power = 50,
            value = 10000
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
            name = "magic armor",
            type = Type.Armor,
            level = 5,
            power = 10,
            value = 5000
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
            name = "dwarven shield",
            type = Type.Shield,
            level = 4,
            power = 4,
            value = 1500
        },
        new()
        {
            name = "magic shield",
            type = Type.Shield,
            level = 5,
            power = 5,
            value = 5000
        },
        new()
        {
            name = "herb",
            desc = "25 heal",
            type = Type.Usable,
            power = 25,
            value = 5
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
            name = "elixir",
            desc = "200 heal",
            type = Type.Usable,
            power = 200,
            value = 20
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
            name = "silver nugget",
            desc = "treasure",
            type = Type.Other,
            value = 25
        },
        new()
        {
            name = "gold nugget",
            desc = "treasure",
            type = Type.Other,
            value = 50
        },
        new()
        {
            name = "alchemy set",
            desc = "allows crafting potions anywhere",
            type = Type.Tool,
            value = 100
        }
    };

    public static readonly Item[] cityItems = new[]
    {
        Get("club"),
        Get("axe"),
        Get("sword"),
        Get("two handed sword"),
        Get("short bow"),
        Get("long bow"),
        Get("composite bow"),
        Get("elven bow"),
        Get("leather armor"),
        Get("chainmail"),
        Get("breastplate"),
        Get("plate armor"),
        Get("wooden shield"),
        Get("iron shield"),
        Get("steel shield"),
        Get("dwarven shield"),
        Get("rations"),
        Get("potion"),
        Get("elixir"),
        Get("tent"),
        Get("pickaxe"),
        Get("alchemy set")
    };


    public static readonly Item[] villageItems = new[]
    {
        Get("club"),
        Get("axe"),
        Get("sword"),
        Get("short bow"),
        Get("long bow"),
        Get("composite bow"),
        Get("leather armor"),
        Get("chainmail"),
        Get("breastplate"),
        Get("wooden shield"),
        Get("iron shield"),
        Get("steel shield"),
        Get("rations"),
        Get("potion"),
        Get("tent"),
        Get("pickaxe")
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
