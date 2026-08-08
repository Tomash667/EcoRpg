using System;
using System.Linq;
using UnityEngine;

public enum Price
{
    None,
    Buy,
    Sell,
    Enchant
}

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
        Bow,
        Ingredient,
        Treasure
    }

    public enum Icon
    {
        Sword,
        Bow,
        Shield,
        Armor,
        Potion,
        Tool,
        Herb,
        Food,
        Treasure
    }

    public const int MaxLevelCity = 4;
    public const int MaxLevelVillage = 3;
    public const int MaxLevelEnchant = 8;

    public string name, desc;
    public Type type;
    public Subtype subtype;
    public int level, power, value;

    public bool CanEnchant()
    {
        return (type == Type.Weapon || type == Type.Armor || type == Type.Shield) && level < MaxLevelEnchant;
    }

    public Item GetEnchanted()
    {
        int nextLevel;
        if (level < 5)
            nextLevel = 5;
        else
            nextLevel = level + 1;
        return items.First(x => x.type == type && x.subtype == subtype && x.level == nextLevel);
    }

    public int GetEnchantCost()
    {
        return level switch
        {
            5 => 10000,
            6 => 20000,
            7 => 30000,
            _ => 10000
        };
    }

    public Icon GetIcon()
    {
        switch (type)
        {
        case Type.Weapon:
            if (subtype == Subtype.Melee)
                return Icon.Sword;
            else
                return Icon.Bow;
        case Type.Shield:
            return Icon.Shield;
        case Type.Armor:
            return Icon.Armor;
        case Type.Usable:
            if (subtype == Subtype.Ingredient)
                return Icon.Herb;
            else
                return Icon.Potion;
        case Type.Tool:
            return Icon.Tool;
        default:
            if (subtype == Subtype.Ingredient)
                return Icon.Herb;
            else if (subtype == Subtype.Treasure)
                return Icon.Treasure;
            else
                return Icon.Food;
        }
    }

    public string ToString(Price price, bool team = false)
    {
        string priceText = price switch
        {
            Price.Buy => $", {value} gold",
            Price.Sell => $", {value / 2} gold",
            Price.Enchant => $", {GetEnchantCost()} gold to enchant",
            _ => string.Empty
        };
        string itemDesc = type switch
        {
            Type.Weapon => $"{power} attack",
            Type.Armor or Type.Shield => $"{power} defense",
            _ => desc
        };
        return team
            ? $"{name.ToUpper1()} (<i>team item</i>, {itemDesc}{priceText})"
            : $"{name.ToUpper1()} ({itemDesc}{priceText})";
    }

    public static Item Get(string name)
    {
        return items.First(x => x.name == name);
    }

    public static Item TryGet(string name)
    {
        return items.FirstOrDefault(x => x.name == name);
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
            name = "magic sword +1",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 6,
            power = 30,
            value = 15000
        },
        new()
        {
            name = "magic sword +2",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 7,
            power = 35,
            value = 35000
        },
        new()
        {
            name = "magic sword +3",
            type = Type.Weapon,
            subtype = Subtype.Melee,
            level = 8,
            power = 40,
            value = 65000
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
            name = "magic bow +1",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 6,
            power = 60,
            value = 20000
        },
        new()
        {
            name = "magic bow +2",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 7,
            power = 70,
            value = 40000
        },
        new()
        {
            name = "magic bow +3",
            type = Type.Weapon,
            subtype = Subtype.Bow,
            level = 8,
            power = 80,
            value = 70000
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
            name = "magic armor +1",
            type = Type.Armor,
            level = 6,
            power = 12,
            value = 15000
        },
        new()
        {
            name = "magic armor +2",
            type = Type.Armor,
            level = 7,
            power = 14,
            value = 35000
        },
        new()
        {
            name = "magic armor +3",
            type = Type.Armor,
            level = 8,
            power = 16,
            value = 65000
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
            name = "magic shield +1",
            type = Type.Shield,
            level = 6,
            power = 6,
            value = 15000
        },
        new()
        {
            name = "magic shield +2",
            type = Type.Shield,
            level = 7,
            power = 7,
            value = 35000
        },
        new()
        {
            name = "magic shield +3",
            type = Type.Shield,
            level = 8,
            power = 8,
            value = 65000
        },
        new()
        {
            name = "herb",
            desc = "25 heal",
            type = Type.Usable,
            subtype = Subtype.Ingredient,
            power = 25,
            value = 5
        },
        new()
        {
            name = "rare herb",
            desc = "50 heal",
            type = Type.Usable,
            subtype = Subtype.Ingredient,
            power = 50,
            value = 10
        },
        new()
        {
            name = "magic crystal",
            desc = "quest item",
            type = Type.Other,
            subtype = Subtype.Ingredient,
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
            name = "meat",
            desc = "can be cooked",
            type = Type.Other,
            value = 4
        },
        new()
        {
            name = "tent",
            desc = "better rest outside",
            type = Type.Tool,
            value = 100
        },
        new()
        {
            name = "pickaxe",
            desc = "miner's tool",
            type = Type.Tool,
            value = 25
        },
        new()
        {
            name = "silver nugget",
            desc = "treasure",
            type = Type.Other,
            subtype = Subtype.Treasure,
            value = 25
        },
        new()
        {
            name = "gold nugget",
            desc = "treasure",
            type = Type.Other,
            subtype = Subtype.Treasure,
            value = 50
        },
        new()
        {
            name = "trophy",
            desc = "treasure",
            type = Type.Other,
            subtype = Subtype.Treasure,
            value = 200
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
    public bool team;

    public void OnBeforeSerialize()
    {
        name = item.name;
    }

    public void OnAfterDeserialize()
    {
        item = Item.Get(name);
    }

    public string ToString(Price price)
    {
        if (count == 1)
            return item.ToString(price, team);
        else
            return $"{count}x {item.ToString(price, team)}";
    }

    public string ToStringShort()
    {
        if (count == 1)
            return item.name;
        else
            return $"{count}x {item.name}";
    }
}
