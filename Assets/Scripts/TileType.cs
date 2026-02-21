public enum TileType
{
    Plains,
    Forest,
    Mountains,
    City,
    Dungeon,
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
            TileType.Dungeon => "dungeon",
            TileType.Cave => "cave",
            TileType.Sawmill => "sawmill",
            TileType.Mine => "mine",
            TileType.Sewers => "sewers",
            _ => $"[ERROR tileType {tileType}]"
        };
    }
}
