using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Property
{
    [Serializable]
    public class Event
    {
        public string name;
        public int timer, state;
    }

    [Serializable]
    public class Upgrade
    {
        public string name, desc;
        public int value, income, upkeep;
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

    public enum Image
    {
        Ok,
        Buff,
        Infested,
        Building
    }

    public string name, desc, lastEvent;
    public List<Event> events;
    public List<ItemSlot> storedItems;
    public List<string> gardenPlants;
    public Upgrade[] upgrades;
    public Func<World, int> locationIndexFunc;
    public Status status;
    public float infestedDifficultyMod;
    public int value, infestedCost, infestedDifficulty, income, upkeep, upkeepDiscount, buildPrice, buildPriceDiscount, buildTime, locationIndex, cityIndex;
    public bool multi;

    public int Income
    {
        get
        {
            if (events.Count == 0)
                return income;

            if (events[0].name == "Infested")
                return 0;
            else
                return income * 3 / 2;
        }
    }
    public int Upkeep
    {
        get
        {
            if (events.Count == 0 || events[0].name != "Infested")
            {
                if (upkeepDiscount != 0 && Global.Player.HaveProperty("Sawmill", true))
                    return upkeep - upkeepDiscount;
                else
                    return upkeep;
            }
            else
                return upkeep / 2;
        }
    }
    public int Profit => Income - Upkeep;
    public int BuildPrice
    {
        get
        {
            if (buildPriceDiscount != 0 && Global.Player.HaveProperty("Sawmill", true))
                return buildPrice - buildPriceDiscount;
            else
                return buildPrice;
        }
    }
    public string Name
    {
        get
        {
            if (multi)
                return $"{name} ({Global.World.GetCityTile(cityIndex).Name})";
            else
                return name;
        }
    }
    public string Desc => desc.Replace("PROFIT", Profit.ToString()).Replace("UPKEEP", upkeep.ToString());

    public override string ToString()
    {
        return Name;
    }

    public string ToString(DescStatus status)
    {
        return status switch
        {
            DescStatus.Sell => $"{Name} ({Desc}, {value / 2} gold)",
            DescStatus.Build => $"{Name} ({buildTime} days to build, {Desc}, {BuildPrice} gold)",
            DescStatus.Building => $"{Name} ({Utility.Plural("day", buildTime)} left, {Desc})",
            DescStatus.Infested => $"{Name} (<color=red>{Desc}, {value / 2} gold</color>)",
            _ => $"{Name} ({Desc}, {value} gold)"
        };
    }

    public bool HaveEvent(string eventName)
    {
        return events.Any(x => x.name == eventName);
    }

    public bool HaveUpgrade(string upgradeName)
    {
        if (upgrades == null)
            return false;
        Upgrade upgrade = upgrades.FirstOrDefault(x => x.name == upgradeName);
        return upgrade != null && upgrade.active;
    }

    public bool RemoveEvent(string eventName)
    {
        return events.RemoveFirst(x => x.name == eventName);
    }

    public Image GetImage()
    {
        if (status != Status.Active)
            return Image.Building;
        if (HaveEvent("Infested"))
            return Image.Infested;
        if (HaveEvent("Buff"))
            return Image.Buff;
        return Image.Ok;
    }

    public Property Copy()
    {
        return new()
        {
            name = name,
            desc = desc,
            events = new(),
            upgrades = upgrades?.Select(x => new Upgrade
            {
                name = x.name,
                desc = x.desc,
                value = x.value,
                income = x.income,
                upkeep = x.upkeep
            }).ToArray(),
            status = status,
            cityIndex = cityIndex,
            value = value,
            infestedCost = infestedCost,
            infestedDifficulty = infestedDifficulty,
            infestedDifficultyMod = infestedDifficultyMod,
            income = income,
            upkeep = upkeep,
            upkeepDiscount = upkeepDiscount,
            buildPrice = buildPrice,
            buildPriceDiscount = buildPriceDiscount,
            buildTime = buildTime,
            multi = multi
        };
    }

    public void Update(Property p)
    {
        desc = p.desc;
        infestedCost = p.infestedCost;
        infestedDifficulty = p.infestedDifficulty;
        infestedDifficultyMod = p.infestedDifficultyMod;
        upkeepDiscount = p.upkeepDiscount;
        buildPrice = p.buildPrice;
        buildPriceDiscount = p.buildPriceDiscount;
        if (p.upgrades != null)
        {
            upgrades = p.upgrades.Select(x => new Upgrade
            {
                name = x.name,
                desc = x.desc,
                value = x.value,
                income = x.income,
                upkeep = x.upkeep,
                active = upgrades.Any(y => y.name == x.name && y.active)
            }).ToArray();
        }
    }

    public static readonly Property[] properties = new Property[]
    {
        new()
        {
            name = "House",
            desc = "don't pay for inn",
            value = 500,
            status = Status.Active,
            cityIndex = 0,
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Alchemy lab",
                    desc = "+25 alchemy",
                    value = 100
                },
                new()
                {
                    name = "Garden",
                    desc = "Grow food or herbs, +1 upkeep",
                    value = 100,
                    upkeep = 1
                }
            },
            multi = true
        },
        new()
        {
            name = "House",
            desc = "don't pay for inn",
            value = 400,
            status = Status.Active,
            cityIndex = 1,
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Alchemy lab",
                    desc = "+25 alchemy",
                    value = 100
                },
                new()
                {
                    name = "Garden",
                    desc = "Grow food or herbs, +1 upkeep",
                    value = 100,
                    upkeep = 1
                }
            },
            multi = true
        },
        new()
        {
            name = "House",
            desc = "don't pay for inn",
            value = 400,
            status = Status.Active,
            cityIndex = 2,
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Alchemy lab",
                    desc = "+25 alchemy",
                    value = 100
                },
                new()
                {
                    name = "Garden",
                    desc = "Grow food or herbs, +1 upkeep",
                    value = 100,
                    upkeep = 1
                }
            },
            multi = true
        },
        new()
        {
            name = "Mansion",
            desc = "don't pay for inn, better rest, UPKEEP upkeep",
            value = 10000,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 0,
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Alchemy lab",
                    desc = "+25 alchemy",
                    value = 100
                },
                new()
                {
                    name = "Garden",
                    desc = "Grow food or herbs, +2 upkeep",
                    value = 500,
                    upkeep = 2
                },
                new()
                {
                    name = "Stables",
                    desc = "Increased speed for 10 days after visiting city, +3 upkeep",
                    value = 1000,
                    upkeep = 3
                }
            }
        },
        new()
        {
            name = "Horses",
            desc = "+25% travel speel",
            value = 500,
            status = Status.Active,
            cityIndex = -1
        },
        new()
        {
            name = "Sawmill",
            desc = "PROFIT gold/day, reduce mines upkeep and build cost",
            value = 5000,
            infestedCost = 500,
            infestedDifficulty = 1,
            infestedDifficultyMod = 0.75f,
            income = 10,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 0,
            locationIndexFunc = world => world.FindLocationIndex(x => x.type == TileType.Sawmill),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +1 upkeep",
                    value = 1000,
                    upkeep = 1
                },
                new()
                {
                    name = "Water-powered saws",
                    desc = "+5 income",
                    value = 1500,
                    income = 5
                }
            }
        },
        new()
        {
            name = "Iron mine",
            desc = "PROFIT gold/day",
            value = 10000,
            infestedCost = 750,
            infestedDifficulty = 1,
            infestedDifficultyMod = 1f,
            income = 20,
            upkeep = 10,
            upkeepDiscount = 2,
            status = Status.Active,
            cityIndex = 0,
            locationIndexFunc = world => world.FindLocationIndex(x => x.type == TileType.Mine),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +2 upkeep",
                    value = 2000,
                    upkeep = 2
                },
                new()
                {
                    name = "Deep shaft expansion",
                    desc = "+10 income",
                    value = 3000,
                    income = 10
                }
            }
        },
        new()
        {
            name = "Silver mine",
            desc = "PROFIT gold/day",
            value = 25000,
            infestedCost = 1500,
            infestedDifficulty = 2,
            infestedDifficultyMod = 1f,
            income = 35,
            upkeep = 10,
            upkeepDiscount = 2,
            buildPrice = 6000,
            buildPriceDiscount = 500,
            buildTime = 20,
            cityIndex = 0,
            locationIndexFunc = world => world.FindLocationIndex(x => x.hidden == TileType.Cave && x.mine && x.difficulty == 2),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +2 upkeep",
                    value = 2000,
                    upkeep = 2
                },
                new()
                {
                    name = "Deep shaft expansion",
                    desc = "+15 income",
                    value = 4000,
                    income = 15
                }
            }
        },
        new()
        {
            name = "Gold mine",
            desc = "PROFIT gold/day",
            value = 50000,
            infestedCost = 2000,
            infestedDifficulty = 3,
            infestedDifficultyMod = 1f,
            income = 60,
            upkeep = 10,
            upkeepDiscount = 2,
            buildPrice = 7500,
            buildPriceDiscount = 500,
            buildTime = 30,
            cityIndex = 0,
            locationIndexFunc = world => world.FindLocationIndex(x => x.hidden == TileType.Cave && x.mine && x.difficulty == 3),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +2 upkeep",
                    value = 2000,
                    upkeep = 2
                },
                new()
                {
                    name = "Deep shaft expansion",
                    desc = "+20 income",
                    value = 5000,
                    income = 20
                }
            }
        },
        new()
        {
            name = "Inn",
            desc = "PROFIT gold/day, free rest",
            value = 5000,
            income = 10,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 0,
            multi = true
        },
        new()
        {
            name = "Inn",
            desc = "PROFIT gold/day, free rest",
            value = 4000,
            income = 9,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 1,
            multi = true
        },
        new()
        {
            name = "Inn",
            desc = "PROFIT gold/day, free rest",
            value = 4000,
            income = 9,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 2,
            multi = true
        },
        new()
        {
            name = "Farm",
            desc = "PROFIT gold/day",
            value = 5000,
            infestedCost = 500,
            infestedDifficulty = 1,
            infestedDifficultyMod = 0.75f,
            income = 10,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 0,
            locationIndexFunc = world => world.FindLocationIndex(x => x.type == TileType.Farm && x.difficulty == 1),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +1 upkeep",
                    value = 1000,
                    upkeep = 1
                },
                new()
                {
                    name = "Advanced tools",
                    desc = "+5 income",
                    value = 1500,
                    income = 5
                }
            },
            multi = true
        },
        new()
        {
            name = "Farm",
            desc = "PROFIT gold/day",
            value = 5000,
            infestedCost = 1250,
            infestedDifficulty = 2,
            infestedDifficultyMod = 0.75f,
            income = 10,
            upkeep = 5,
            status = Status.Active,
            cityIndex = 1,
            locationIndexFunc = world => world.FindLocationIndex(x => x.type == TileType.Farm && x.difficulty == 2),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +1 upkeep",
                    value = 2000,
                    upkeep = 2
                },
                new()
                {
                    name = "Advanced tools",
                    desc = "+5 income",
                    value = 1500,
                    income = 5
                }
            },
            multi = true
        },
        new()
        {
            name = "Farm",
            desc = "PROFIT gold/day",
            value = 5000,
            infestedCost = 1250,
            infestedDifficulty = 2,
            infestedDifficultyMod = 0.75f,
            income = 10,
            upkeep = 5,
            buildPrice = 2500,
            buildPriceDiscount = 500,
            buildTime = 15,
            status = Status.Cleared,
            cityIndex = 2,
            locationIndexFunc = world => world.FindLocationIndex(World.IndexToPoint(world.cityMapping[2]), x => x.type == TileType.Plains && x.hidden == TileType.None),
            upgrades = new Upgrade[]
            {
                new()
                {
                    name = "Extra guards",
                    desc = "Prevents monster invasion, +1 upkeep",
                    value = 2000,
                    upkeep = 2
                },
                new()
                {
                    name = "Advanced tools",
                    desc = "+5 income",
                    value = 1500,
                    income = 5
                }
            },
            multi = true
        }
    };
}
