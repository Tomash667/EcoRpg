using System;

[Serializable]
public class Tile
{
    public string name;
    public TileType type, hidden;
    public int difficulty, defeatedEnemies, timer;
    public bool mine, boss, foundTreasure;

    public string Name
    {
        get
        {
            switch (type)
            {
            case TileType.Forest:
                switch (difficulty)
                {
                case 1:
                    return "forest";
                case 2:
                    return "deep forest";
                case 3:
                    return "ancient forest";
                }
                break;
            case TileType.Mountains:
                switch (difficulty)
                {
                case 1:
                    return "hills";
                case 2:
                    return "mountains";
                case 3:
                    return "high peaks";
                }
                break;
            case TileType.Mine:
                switch (difficulty)
                {
                case 1:
                    return "iron mine";
                case 2:
                    return "silver mine";
                case 3:
                    return "gold mine";
                }
                break;
            case TileType.Cave:
            case TileType.Dungeon:
            case TileType.ForestDungeon:
                return name;
            }

            return type.AsString();
        }
    }
}
