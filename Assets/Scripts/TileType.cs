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
    Swamp
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
    Swamp
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
            _ => $"[ERROR tileType {tileType}]"
        };
    }

    public static bool IsSmall(this TileType tileType)
    {
        return tileType == TileType.Cave || tileType == TileType.Dungeon || tileType == TileType.Mine || tileType == TileType.Sewers;
    }

    public static bool IsClearable(this TileType tileType)
    {
        return tileType == TileType.Cave || tileType == TileType.Mine || tileType == TileType.Sawmill || tileType == TileType.Sewers;
    }
}
