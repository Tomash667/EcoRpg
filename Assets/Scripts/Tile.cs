using System;

[Serializable]
public class Tile
{
    public TileType type, hidden;
    public int difficulty;
    public bool mine, boss;
}
