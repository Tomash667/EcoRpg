using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class World
{
    public const int sizeX = 20, sizeY = 10;

    public Tile[] map;
    public Tile sewers;
    public Vector2Int currentPt;
    public bool isInside;

    public Tile CurrentTile => isInside ? sewers : map[currentPt.x + currentPt.y * sizeX];
    public int CurrentLocationIndex => CalculateIndex(currentPt.x, currentPt.y, isInside);
    public TileType Location => CurrentTile.type;

    public static int CalculateIndex(int x, int y, bool inside)
    {
        return x + y * sizeX + (inside ? (sizeX * sizeY) : 0);
    }

    public void Init()
    {
        map = new Tile[sizeX * sizeY];
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

                int difficulty;
                if (x < 8)
                    difficulty = 1;
                else if (x < 14)
                    difficulty = 2;
                else
                    difficulty = 3;

                Tile tile = new() { hidden = TileType.None, difficulty = difficulty };
                tile.SetType(tileType);
                map[x + y * sizeX] = tile;
            }
        }

        Vector2Int cityPos = new(2, sizeY / 2);
        map[cityPos.x + cityPos.y * sizeX].SetType(TileType.Plains);
        map[cityPos.x - 1 + cityPos.y * sizeX].SetType(TileType.Plains);
        map[cityPos.x + 1 + cityPos.y * sizeX].SetType(TileType.Plains);
        map[cityPos.x + (cityPos.y - 1) * sizeX].SetType(TileType.Plains);
        map[cityPos.x + (cityPos.y + 1) * sizeX].SetType(TileType.Plains);

        Dictionary<TileType, int> influence = new();
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                void AddInfluence(int x, int y, int value)
                {
                    if (IsInBounds(x, y))
                    {
                        TileType tileType = map[x + y * sizeX].type;
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
                map[x + y * sizeX].SetType(influence.WeightedRandom());
            }
        }

        map[cityPos.x + cityPos.y * sizeX].SetType(TileType.City);

        sewers = new Tile
        {
            type = TileType.Sewers,
            hidden = TileType.None,
            difficulty = 1,
            clear = true
        };

        SpawnLocation(cityPos, TileType.Forest, TileType.Sawmill);
        SpawnLocation(cityPos, TileType.Mountains, TileType.Mine);
        SpawnHiddenLocations(0, 7, 2, TileType.Mountains, TileType.Cave, Names.cave1.ToList());
        List<Tile> spawned = SpawnHiddenLocations(8, 13, 2, TileType.Mountains, TileType.Cave, Names.cave2.ToList());
        spawned[0].mine = true;
        spawned = SpawnHiddenLocations(14, 19, 2, TileType.Mountains, TileType.Cave, Names.cave3.ToList());
        spawned[0].boss = true;
        spawned[1].mine = true;
        List<string> dungeon1 = Names.dungeon1.ToList();
        List<string> dungeon2 = Names.dungeon2.ToList();
        List<string> dungeon3 = Names.dungeon3.ToList();
        SpawnHiddenLocations(0, 7, 2, TileType.Forest, TileType.Dungeon, dungeon1);
        SpawnHiddenLocations(8, 13, 2, TileType.Forest, TileType.Dungeon, dungeon2);
        SpawnHiddenLocations(14, 19, 2, TileType.Forest, TileType.Dungeon, dungeon3);
        SpawnHiddenLocations(0, 7, 2, TileType.Plains, TileType.Dungeon, dungeon1);
        SpawnHiddenLocations(8, 13, 2, TileType.Plains, TileType.Dungeon, dungeon2);
        SpawnHiddenLocations(14, 19, 2, TileType.Plains, TileType.Dungeon, dungeon3);

        RevealHiddenLocations(cityPos, false);
        currentPt = cityPos;
    }

    private void SpawnLocation(Vector2Int wantedPos, TileType wantedTile, TileType targetTile)
    {
        Vector2Int targetPos = FindMatchingTile(wantedPos, pos => map[pos.x + pos.y * sizeX].type == wantedTile);
        Tile tile = map[targetPos.x + targetPos.y * sizeX];
        tile.SetType(targetTile);
        tile.clear = true;
    }

    private List<Tile> SpawnHiddenLocations(int xMin, int xMax, int count, TileType wantedTile, TileType targetTile, List<string> names)
    {
        List<Tile> validTiles = new();
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = xMin; x <= xMax; ++x)
            {
                Tile tile = map[x + y * sizeX];
                if (tile.type == wantedTile)
                    validTiles.Add(tile);
            }
        }

        List<Tile> spawned = new();
        while (count > 0 && validTiles.Count > 0)
        {
            Tile tile = validTiles.RandomItemPop();
            tile.name = names.RandomItemPop();
            tile.hidden = targetTile;
            spawned.Add(tile);
            --count;
        }

        return spawned;
    }

    public static bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < sizeX && y >= 0 && y < sizeY;
    }

    public static Vector2Int IndexToPoint(int index)
    {
        return new(index % sizeX, index / sizeX);
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

        if (isInside && game.minute + 30 >= 60)
        {
            ++hour;
            if (hour == 24)
                NextDay();
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
            game.minute = 0;
            ++game.day;
            game.OnNewDay();
            game.RemoveTeamItem(rationsItem, teamSize);
            rations -= teamSize;
            speed = RationsToSpeed(rations, teamSize);
            foreach (Hero hero in game.Team)
                hero.hp = hero.hpMax;
            game.player.energy = Mathf.Min(game.player.energy + (haveTent ? 100 : 75), 100);
        }

        if (isInside)
        {
            isInside = false;
            game.minute += 30;
            if (game.minute >= 60)
            {
                game.minute -= 60;
                ++game.hour;
                if (game.hour == 24)
                    NextDay();
            }
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
                    RevealHiddenLocations(nextPt, true);
                    currentPt = nextPt;
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

    private void RevealHiddenLocations(Vector2Int pos, bool updateMap)
    {
        for (int y = pos.y - 1; y <= pos.y + 1; ++y)
        {
            for (int x = pos.x - 1; x <= pos.x + 1; ++x)
            {
                if (IsInBounds(x, y))
                {
                    Tile tile = map[x + y * sizeX];
                    if (tile.type != tile.hidden && tile.hidden != TileType.None)
                    {
                        tile.SetType(tile.hidden);
                        tile.hidden = TileType.None;
                        if (updateMap)
                            Global.Game.RevealLocation(new(x, y));
                    }
                }
            }
        }
    }

    public void RevealAllHiddenLocations()
    {
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                Tile tile = map[x + y * sizeX];
                if (tile.type != tile.hidden && tile.hidden != TileType.None)
                {
                    tile.SetType(tile.hidden);
                    tile.hidden = TileType.None;
                    Global.Game.RevealLocation(new(x, y));
                }
            }
        }
    }

    public Tile GetLocation(int index)
    {
        if (index >= sizeX * sizeY)
            return sewers;
        else
            return map[index];
    }

    public int FindLocationIndex(Func<Tile, bool> pred, bool inside = false)
    {
        for (int index = 0; index < sizeX * sizeY; ++index)
        {
            if (pred(map[index]))
            {
                if (inside)
                    return index + sizeX * sizeY;
                else
                    return index;
            }
        }
        return -1;
    }

    public Tile FindLocation(Func<Tile, bool> pred)
    {
        int index = FindLocationIndex(pred);
        if (index != -1)
            return map[index];
        else
            return null;
    }

    public int FindRandomLocationIndex(Func<Tile, bool> pred)
    {
        List<int> choices = new();
        for (int index = 0; index < sizeX * sizeY; ++index)
        {
            if (pred(map[index]))
                choices.Add(index);
        }
        if (choices.Count > 0)
            return choices.RandomItem();
        return -1;
    }

    public void Update()
    {
        foreach (Tile tile in map.Where(x => x.timer > 0))
        {
            tile.timer--;
            if (tile.timer == 0)
            {
                tile.clear = false;
                tile.defeatedEnemies = 0;
                if (tile.mine)
                {
                    if (tile.difficulty == 2)
                        Global.Game.silverMineStatus = Game.MineStatus.None;
                    else
                        Global.Game.goldMineStatus = Game.MineStatus.None;
                }
            }
        }
    }
}
