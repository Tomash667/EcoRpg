using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        SpawnLocation(center, TileType.Forest, TileType.Sawmill);
        SpawnLocation(center, TileType.Mountains, TileType.Mine);
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

    public static int CalculateDistance(Vector2Int a, Vector2Int b)
    {
        Vector2Int dist = a - b;
        int distX = Mathf.Abs(dist.x);
        int distY = Mathf.Abs(dist.y);
        int diagonalDist = Mathf.Min(distX, distY);
        int straightDist = Mathf.Max(distX, distY) - diagonalDist;
        return diagonalDist * 15 + straightDist * 10;
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
        List<Vector2Int> validPoints = new();

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
                    validPoints.Add(new Vector2Int(x, minY));

                if (IsInBounds(x, maxY) && pred(new Vector2Int(x, maxY)))
                    validPoints.Add(new Vector2Int(x, maxY));
            }

            // left & right columns (skip corners, already checked)
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                if (IsInBounds(minX, y) && pred(new Vector2Int(minX, y)))
                    validPoints.Add(new Vector2Int(minX, y));

                if (IsInBounds(maxX, y) && pred(new Vector2Int(maxX, y)))
                    validPoints.Add(new Vector2Int(maxX, y));
            }

            if (validPoints.Count > 0)
                return validPoints.RandomItem();
        }

        return new Vector2Int(-1, -1);
    }

    // Team move slower, need to forage for food
    private float RationsToSpeed(int rations, int teamSize)
    {
        if (rations <= 0)
            return 1.25f;
        else if (rations < teamSize)
            return 1.25f + 1.25f * rations / teamSize;
        else
            return 2.5f;
    }

    public int CalculateTravelDays(Vector2Int pt)
    {
        Game game = Global.Game;
        Vector2Int currentTmpPt = currentPt;
        int dist = CalculateDistance(currentPt, pt);
        int rations = game.CountTeamItem(Item.Get("rations"));
        int teamSize = game.Team.Count();
        float speed = RationsToSpeed(rations, teamSize);
        float travelDist = 0;
        int days = 0, hour = game.hour;
        int energy = game.player.energy;
        bool haveTent = game.player.HaveItem("Tent");
        bool energyTick = false;

        void NextDay()
        {
            hour = 8;
            ++days;
            rations -= teamSize;
            speed = RationsToSpeed(rations, teamSize);
            energy = Mathf.Min(energy + (haveTent ? 100 : 75), 100);
        }

        while (dist > 0)
        {
            Vector2Int dir = (pt - currentTmpPt).Normalized();
            Vector2Int nextPt = currentTmpPt + dir;
            bool isDiagonal = dir.x != 0 && dir.y != 0;

            while (true)
            {
                if (energy < 5)
                    NextDay();

                travelDist += speed;
                if (energyTick)
                {
                    energy -= 5;
                    energyTick = false;
                }
                else
                    energyTick = true;
                ++hour;
                if (hour == 24)
                    NextDay();
                if (travelDist >= (isDiagonal ? 15 : 10))
                {
                    currentTmpPt = nextPt;
                    travelDist -= isDiagonal ? 15 : 10;
                    dist -= isDiagonal ? 15 : 10;
                    break;
                }
            }
        }

        return days;
    }

    public IEnumerator Travel(Vector2Int pt)
    {
        Game game = Global.Game;
        Item rationsItem = Item.Get("rations");
        int dist = CalculateDistance(currentPt, pt);
        int rations = game.CountTeamItem(rationsItem);
        int teamSize = game.Team.Count();
        float speed = RationsToSpeed(rations, teamSize);
        float travelDist = 0;
        bool haveTent = game.player.HaveItem("Tent");
        bool energyTick = false;

        void NextDay()
        {
            game.hour = 8;
            ++game.day;
            game.OnNewDay();
            game.RemoveTeamItem(rationsItem, teamSize);
            rations -= teamSize;
            speed = RationsToSpeed(rations, teamSize);
            foreach (Hero hero in game.Team)
                hero.hp = hero.hpMax;
            game.player.energy = Mathf.Min(game.player.energy + (haveTent ? 100 : 75), 100);
        }

        while (dist > 0)
        {
            Vector2Int dir = (pt - currentPt).Normalized();
            Vector2Int nextPt = currentPt + dir;
            bool isDiagonal = dir.x != 0 && dir.y != 0;

            while (true)
            {
                if (game.player.energy < 5)
                {
                    NextDay();
                    game.UpdateTravel();
                    yield return new WaitForSeconds(0.1f);
                }

                travelDist += speed;
                if (energyTick)
                {
                    game.player.energy -= 5;
                    energyTick = false;
                }
                else
                    energyTick = true;
                ++game.hour;
                if (game.hour == 24)
                    NextDay();
                if (travelDist >= (isDiagonal ? 15 : 10))
                {
                    currentPt = nextPt;
                    location = map[pt.x + pt.y * sizeX];
                    travelDist -= isDiagonal ? 15 : 10;
                    dist -= isDiagonal ? 15 : 10;
                    if (currentPt != pt)
                    {
                        game.UpdateTravel();
                        yield return new WaitForSeconds(0.1f);
                    }
                    break;
                }

                game.UpdateTravel();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
