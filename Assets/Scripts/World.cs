using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class World
{
    public const int sizeX = 20, sizeY = 10;

    public TileType[] map;
    public TileType location;
    public Vector2Int currentPt;

    public void Init()
    {
        map = new TileType[sizeX * sizeY];
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                TileType tileType = (Utility.Rand % 5) switch
                {
                    2 or 3 => TileType.Forest,
                    4 => TileType.Mountains,
                    _ => TileType.Plains
                };
                map[x + y * sizeX] = tileType;
            }
        }

        Vector2Int center = new(sizeX / 2, sizeY / 2);
        map[center.x + center.y * sizeX] = TileType.Plains;
        map[center.x - 1 + center.y * sizeX] = TileType.Plains;
        map[center.x + 1 + center.y * sizeX] = TileType.Plains;
        map[center.x + (center.y - 1) * sizeX] = TileType.Plains;
        map[center.x + (center.y + 1) * sizeX] = TileType.Plains;

        Dictionary<TileType, int> influence = new();
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                void AddInfluence(int x, int y, int value)
                {
                    if (x >= 0 && y >= 0 && x < sizeX && y < sizeY)
                    {
                        TileType tileType = map[x + y * sizeX];
                        influence[tileType] = influence.GetValueOrDefault(tileType) + value;
                    }
                }

                influence.Clear();
                AddInfluence(x, y, 5);
                AddInfluence(x - 1, y, 3);
                AddInfluence(x + 1, y, 3);
                AddInfluence(x, y - 1, 3);
                AddInfluence(x, y + 1, 3);
                AddInfluence(x - 1, y - 1, 1);
                AddInfluence(x - 1, y + 1, 1);
                AddInfluence(x + 1, y - 1, 1);
                AddInfluence(x + 1, y + 1, 1);
                map[x + y * sizeX] = influence.WeightedRandom();
            }
        }

        map[center.x + center.y * sizeX] = TileType.City;

        SpawnLocation(new Vector2Int(sizeX / 4, sizeY / 2), TileType.Forest, TileType.Dungeon);
        SpawnLocation(new Vector2Int(sizeX * 3 / 4, sizeY / 2), TileType.Mountains, TileType.Cave);

        currentPt = center;
        location = TileType.City;
    }

    private void SpawnLocation(Vector2Int wantedPos, TileType wantedTile, TileType targetTile)
    {
        Vector2Int targetPos = FindMatchingTile(wantedPos, pos => map[pos.x + pos.y * sizeX] == wantedTile);
        map[targetPos.x + targetPos.y * sizeX] = targetTile;
    }

    public static bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < sizeX && y >= 0 && y < sizeY;
    }

    private Vector2Int FindMatchingTile(Vector2Int startPos, Func<Vector2Int, bool> pred)
    {
        // bounds check for startPos
        if (startPos.x < 0 || startPos.x >= sizeX ||
            startPos.y < 0 || startPos.y >= sizeY)
            return new Vector2Int(-1, -1);

        // check start tile first
        if (pred(startPos))
            return startPos;

        int maxRadius = Mathf.Max(sizeX, sizeY);

        for (int r = 1; r <= maxRadius; r++)
        {
            int minX = startPos.x - r;
            int maxX = startPos.x + r;
            int minY = startPos.y - r;
            int maxY = startPos.y + r;

            // top & bottom rows
            for (int x = minX; x <= maxX; x++)
            {
                if (IsInBounds(x, minY) && pred(new Vector2Int(x, minY)))
                    return new Vector2Int(x, minY);

                if (IsInBounds(x, maxY) && pred(new Vector2Int(x, maxY)))
                    return new Vector2Int(x, maxY);
            }

            // left & right columns (skip corners, already checked)
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                if (IsInBounds(minX, y) && pred(new Vector2Int(minX, y)))
                    return new Vector2Int(minX, y);

                if (IsInBounds(maxX, y) && pred(new Vector2Int(maxX, y)))
                    return new Vector2Int(maxX, y);
            }
        }

        return new Vector2Int(-1, -1);
    }
}
