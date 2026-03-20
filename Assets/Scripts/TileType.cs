public enum TileType
{
    None = -1,
    Plains,
    Forest,
    Mountains,
    City,
    Dungeon,
    ForestDungeon,
    Cave,
    Sawmill,
    Mine,
    Sewers
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
            TileType.Dungeon or TileType.ForestDungeon => "dungeon",
            TileType.Cave => "cave",
            TileType.Sawmill => "sawmill",
            TileType.Mine => "mine",
            TileType.Sewers => "sewers",
            _ => $"[ERROR tileType {tileType}]"
        };
    }

    public static bool IsSmall(this TileType tileType)
    {
        return tileType == TileType.Cave || tileType == TileType.Dungeon || tileType == TileType.ForestDungeon || tileType == TileType.Mine || tileType == TileType.Sewers;
    }
}
