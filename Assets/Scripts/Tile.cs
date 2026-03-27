using System;

[Serializable]
public class Tile
{
    public string name;
    public TileImage image;
    public TileType type, hidden;
    public int difficulty, defeatedEnemies, timer;
    public bool mine, boss, foundTreasure, clear;

    public string Name => GetName(type);
    public string RealName => GetName(hidden == TileType.None ? type : hidden);

    public string GetName(TileType tileType)
    {
        switch (tileType)
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
            return name;
        }

        return type.AsString();
    }

    public void SetType(TileType newType)
    {
        switch (newType)
        {
        case TileType.Plains:
            image = TileImage.Plains;
            break;
        case TileType.Forest:
            image = TileImage.Forest;
            break;
        case TileType.Mountains:
            image = TileImage.Mountains;
            break;
        case TileType.City:
            image = TileImage.City;
            break;
        case TileType.Dungeon:
            if (type == TileType.Forest)
                image = TileImage.ForestDungeon;
            else
                image = TileImage.Dungeon;
            break;
        case TileType.Cave:
            image = TileImage.Cave;
            break;
        case TileType.Sawmill:
            image = TileImage.Sawmill;
            break;
        case TileType.Mine:
            image = TileImage.Mine;
            break;
        case TileType.Village:
            image = TileImage.Village;
            break;
        }
        type = newType;
    }
}
