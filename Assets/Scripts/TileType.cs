public enum TileType
{
    None = -1,
    Plains,
    Forest,
    Mountains,
    City,
    Dungeon,
    Cave,
    Sawmill,
    Mine,
    Sewers,
    Village,
    Swamp,
    House,
    Mansion,
    MageTower,
    DarkDimension,
    Lake,
    Farm,
    EnchantedForest
}

public enum TileImage
{
    Plains,
    Forest,
    Mountains,
    City,
    Dungeon,
    ForestDungeon,
    Cave,
    Sawmill,
    Mine,
    Village,
    Swamp,
    SwampDungeon,
    MageTower,
    Lake,
    Farm,
    EnchantedForest
}

public static class TileTypeMethods
{
    public static string AsString(this TileType tileType)
    {
        return tileType switch
        {
            TileType.Plains => "plains",
            TileType.Forest => "forest",
            TileType.Mountains => "mountains",
            TileType.City => "city",
            TileType.Dungeon => "dungeon",
            TileType.Cave => "cave",
            TileType.Sawmill => "sawmill",
            TileType.Mine => "mine",
            TileType.Sewers => "sewers",
            TileType.Village => "village",
            TileType.Swamp => "swamp",
            TileType.House => "house",
            TileType.Mansion => "mansion",
            TileType.MageTower => "mage tower",
            TileType.DarkDimension => "dark dimension",
            TileType.Lake => "lake",
            TileType.Farm => "farm",
            TileType.EnchantedForest => "enchanted forest",
            _ => $"[ERROR tileType {tileType}]"
        };
    }

    public static bool IsSmall(this TileType tileType)
    {
        return tileType == TileType.Cave
            || tileType == TileType.Dungeon
            || tileType == TileType.Mine
            || tileType == TileType.Sewers
            || tileType == TileType.House
            || tileType == TileType.Mansion
            || tileType == TileType.MageTower;
    }

    public static bool IsClearable(this TileType tileType)
    {
        return tileType == TileType.Cave
            || tileType == TileType.Mine
            || tileType == TileType.Sawmill
            || tileType == TileType.Sewers
            || tileType == TileType.Forest
            || tileType == TileType.Mountains
            || tileType == TileType.Farm
            || tileType == TileType.EnchantedForest;
    }

    public static bool IsSafe(this TileType tileType)
    {
        return tileType == TileType.City || tileType == TileType.Village || tileType == TileType.House || tileType == TileType.Mansion;
    }

    public static bool IsBlocked(this TileType tileType)
    {
        return tileType == TileType.Lake;
    }

    public static int GetEnemiesCount(this TileType tileType)
    {
        return tileType switch
        {
            TileType.Forest or TileType.Mountains or TileType.EnchantedForest => 20,
            _ => 10
        };
    }
}
