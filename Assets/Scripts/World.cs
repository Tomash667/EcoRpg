using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class World
{
    public const int sizeX = 25, sizeY = 15;
    public const int sublocationOffset = sizeX * sizeY;

    public Tile[] map;
    public Tile[] sublocations;
    public Vector2Int currentPt;
    public int sublocation; // 0-none, 1-sewers, 2-house, 3-mansion, 4-dark dimension
    public int level;
    [NonSerialized]
    public bool isTraveling;

    public Tile CurrentTile => sublocation == 0 ? map[currentPt.x + currentPt.y * sizeX] : sublocations[sublocation];
    public int CurrentLocationIndex => CalculateIndex(currentPt.x, currentPt.y, sublocation);
    public TileType Location => CurrentTile.type;

    public static int CalculateIndex(int x, int y, int z)
    {
        return x + y * sizeX + z * sublocationOffset;
    }

    public void Init()
    {
        Tile tile;
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
                if (x < 9)
                    difficulty = 1;
                else if (x < 17)
                    difficulty = 2;
                else
                    difficulty = 3;

                tile = new() { hidden = TileType.None, difficulty = difficulty };
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

        Vector2Int villagePos = new(Utility.Random(15, 19), Utility.Rand % 2 == 0 ? Utility.Random(2, 6) : Utility.Random(8, 12));
        map[villagePos.x + villagePos.y * sizeX].SetType(TileType.Plains);

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

        // city
        map[cityPos.x + cityPos.y * sizeX].SetType(TileType.City);

        // village
        tile = map[villagePos.x + villagePos.y * sizeX];
        tile.SetType(TileType.Village);
        tile.difficulty = 1;

        sublocations = new Tile[]
        {
            new(),
            new()
            {
                type = TileType.Sewers,
                hidden = TileType.None,
                difficulty = 1,
                clear = true
            },
            new()
            {
                type = TileType.House,
                hidden = TileType.None,
                difficulty = 0,
                clear = true
            },
            new()
            {
                type = TileType.Mansion,
                hidden = TileType.None,
                difficulty = 0,
                clear = true
            },
            new()
            {
                type = TileType.DarkDimension,
                hidden = TileType.None,
                difficulty = 4,
                clear = false
            }
        };

        List<string> dungeon1 = Names.dungeon1.ToList();
        List<string> dungeon2 = Names.dungeon2.ToList();
        List<string> dungeon3 = Names.dungeon3.ToList();
        List<Tile> spawnedDungeon1 = new(), spawnedDungeon2 = new(), spawnedDungeon3 = new();

        // swamps & swamp dungeons
        List<Tile> spawned = SpawnBlob(9, 16, Utility.Random(5, 7), TileType.Plains, TileType.Forest, TileType.Swamp);
        Tile dungeon = spawned.RandomItem();
        SpawnHiddenLocation(dungeon, TileType.Dungeon, dungeon2);
        spawnedDungeon2.Add(dungeon);
        spawned = SpawnBlob(17, 24, Utility.Random(5, 7), TileType.Plains, TileType.Forest, TileType.Swamp);
        dungeon = spawned.RandomItem();
        SpawnHiddenLocation(dungeon, TileType.Dungeon, dungeon3);
        spawnedDungeon3.Add(dungeon);

        // sawmill & mine
        SpawnLocation(cityPos, TileType.Forest, TileType.Sawmill);
        SpawnLocation(cityPos, TileType.Mountains, TileType.Mine);

        // caves with potential mines or boxx
        SpawnHiddenLocations(0, 8, 2, TileType.Mountains, TileType.Cave, Names.cave1.ToList());
        spawned = SpawnHiddenLocations(9, 16, 3, TileType.Mountains, TileType.Cave, Names.cave2.ToList());
        spawned[0].mine = true;
        spawned = SpawnHiddenLocations(17, 24, 3, TileType.Mountains, TileType.Cave, Names.cave3.ToList());
        spawned[0].boss = true;
        spawned[0].levels = 2;
        spawned[1].mine = true;

        // dungeons
        spawnedDungeon1.AddRange(SpawnHiddenLocations(0, 8, 2, TileType.Forest, TileType.Dungeon, dungeon1));
        spawnedDungeon2.AddRange(SpawnHiddenLocations(9, 16, 2, TileType.Forest, TileType.Dungeon, dungeon2));
        spawnedDungeon3.AddRange(SpawnHiddenLocations(17, 24, 2, TileType.Forest, TileType.Dungeon, dungeon3));
        spawnedDungeon1.AddRange(SpawnHiddenLocations(0, 8, 2, TileType.Plains, TileType.Dungeon, dungeon1));
        spawnedDungeon2.AddRange(SpawnHiddenLocations(9, 16, 2, TileType.Plains, TileType.Dungeon, dungeon2));
        spawnedDungeon3.AddRange(SpawnHiddenLocations(17, 24, 2, TileType.Plains, TileType.Dungeon, dungeon3));

        // set dungeon levels
        // -- difficulty 1 (50% level 1, 50% level 2)
        spawnedDungeon1.Shuffle();
        for (int i = 0; i < 2; ++i)
        {
            spawnedDungeon1[i].levels = 1;
            spawnedDungeon1[2 + i].levels = 2;
        }
        // -- difficulty 2 (20% level 1, 60% level 2, 20% level 3)
        spawnedDungeon2.Shuffle();
        spawnedDungeon2[0].levels = 1;
        spawnedDungeon2[1].levels = 3;
        for (int i = 2; i < 5; ++i)
            spawnedDungeon2[i].levels = 2;
        // -- difficulty 3 (40% level 2, 60% level 3)
        spawnedDungeon3.Shuffle();
        for (int i = 0; i < 2; ++i)
            spawnedDungeon3[i].levels = 2;
        for (int i = 2; i < 5; ++i)
            spawnedDungeon3[i].levels = 3;

        // mage tower
        SpawnHiddenLocations(20, 24, 1, TileType.Plains, TileType.MageTower, null);

        RevealHiddenLocations(cityPos, false);
        currentPt = cityPos;
        level = 0;
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
            if (names != null)
                tile.name = names.RandomItemPop();
            tile.hidden = targetTile;
            spawned.Add(tile);
            --count;
        }

        return spawned;
    }

    private void SpawnHiddenLocation(Tile tile, TileType targetTile, List<string> names)
    {
        tile.name = names.RandomItemPop();
        tile.hidden = targetTile;
    }

    private List<Tile> SpawnBlob(int xMin, int xMax, int count, TileType wantedTile, TileType optionalTile, TileType targetTile)
    {
        Tile tile;
        List<Vector2Int> validTiles = new();
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = xMin; x <= xMax; ++x)
            {
                tile = map[x + y * sizeX];
                if (tile.type == wantedTile)
                    validTiles.Add(new(x, y));
            }
        }

        if (validTiles == null)
            return null;

        void CheckTiles(int x, int y)
        {
            if (x >= xMin && x <= xMax && IsInBounds(x, y) && (map[x + y * sizeX].type == wantedTile || map[x + y * sizeX].type == optionalTile))
                validTiles.Add(new(x, y));
        }

        List<Tile> spawned = new();
        Vector2Int pt = validTiles.RandomItem();
        validTiles.Clear();
        tile = map[pt.x + pt.y * sizeX];
        tile.SetType(targetTile);
        spawned.Add(tile);
        --count;
        CheckTiles(pt.x - 1, pt.y);
        CheckTiles(pt.x + 1, pt.y);
        CheckTiles(pt.x, pt.y - 1);
        CheckTiles(pt.x, pt.y + 1);

        while (count > 0 && validTiles.Count > 0)
        {
            pt = validTiles.RandomItemPop();
            tile = map[pt.x + pt.y * sizeX];
            tile.SetType(targetTile);
            spawned.Add(tile);
            --count;
            CheckTiles(pt.x - 1, pt.y);
            CheckTiles(pt.x + 1, pt.y);
            CheckTiles(pt.x, pt.y - 1);
            CheckTiles(pt.x, pt.y + 1);
        }

        return spawned;
    }

    public static bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < sizeX && y >= 0 && y < sizeY;
    }

    public static bool IsInBounds(Vector2Int pt)
    {
        return pt.x >= 0 && pt.x < sizeX && pt.y >= 0 && pt.y < sizeY;
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
        List<(Vector2Int pt, int dist)> validPoints = new();

        void CheckPoint(int x, int y)
        {
            Vector2Int pt = new(x, y);
            if (IsInBounds(pt) && pred(pt))
                validPoints.Add((pt, CalculateDistance(pt, startPos)));
        }

        for (int r = 1; r <= maxRadius; r++)
        {
            int minX = startPos.x - r;
            int maxX = startPos.x + r;
            int minY = startPos.y - r;
            int maxY = startPos.y + r;

            // top & bottom rows
            for (int x = minX; x <= maxX; x++)
            {
                CheckPoint(x, minY);
                CheckPoint(x, maxY);
            }

            // left & right columns (skip corners, already checked)
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                CheckPoint(minX, y);
                CheckPoint(maxX, y);
            }

            if (validPoints.Count > 0)
            {
                int minDist = validPoints.Min(x => x.dist);
                return validPoints.RandomItem(x => x.dist == minDist).pt;
            }
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
        float speedMod = game.player.HaveProperty("Horses") ? 1.25f : 1f;
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

        if (sublocation == 1 && game.minute + 30 >= 60)
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

                travelDist += speed * speedMod;
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

    public int CalculateTravelDaysNonTeam(Vector2Int pt)
    {
        Game game = Global.Game;
        Vector2Int currentTmpPt = currentPt;
        int dist = CalculateDistance(currentPt, pt);
        float speed = 2.5f * 1.25f; // rations + horse
        float travelDist = 0;
        int days = 0, hour = game.hour;

        void NextDay()
        {
            hour = 8;
            ++days;
        }

        while (dist > 0)
        {
            Vector2Int dir = (pt - currentTmpPt).Normalized();
            Vector2Int nextPt = currentTmpPt + dir;
            bool isDiagonal = dir.x != 0 && dir.y != 0;

            while (true)
            {
                travelDist += speed;
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

        if (days < 1)
            days = 1;
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
        float speedMod = game.player.HaveProperty("Horses") ? 1.25f : 1f;
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

        isTraveling = true;

        if (sublocation != 0)
        {
            if (sublocation == 1)
                game.minute += 30;
            else
                game.minute += 5;
            sublocation = 0;
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

                travelDist += speed * speedMod;
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
                    game.UpdateTravel();
                    if (currentPt != pt)
                        yield return new WaitForSeconds(0.1f);
                    break;
                }

                game.UpdateTravel();
                yield return new WaitForSeconds(0.1f);
            }
        }

        isTraveling = false;
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
        if (index >= sublocationOffset)
            return sublocations[index / sublocationOffset];
        else
            return map[index];
    }

    public int FindLocationIndex(Func<Tile, bool> pred, int sublocation = 0)
    {
        for (int index = 0; index < sizeX * sizeY; ++index)
        {
            if (pred(map[index]))
                return index + sublocation * sublocationOffset;
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
                int index = Array.IndexOf(map, tile);
                Quest quest = Global.Game.activeQuests.FirstOrDefault(x => x.type == Quest.Type.Clear && x.location == index);
                if (tile.clear)
                {
                    tile.clear = false;
                    tile.defeatedEnemies = 0;
                    if (quest != null)
                        quest.count = 0;
                    if (tile.mine)
                    {
                        Property property = Global.Game.properties.FirstOrDefault(x => x.locationIndex == index);
                        if (property != null)
                            property.status = Property.Status.None;
                    }
                }
                else
                {
                    --tile.defeatedEnemies;
                    if (quest != null && quest.count > 0)
                        --quest.count;
                    if (tile.defeatedEnemies != 0)
                        tile.timer = 3;
                }
            }
        }

        Vector2Int cityPos = new(2, sizeY / 2);
        for (int i = 0; i < sublocations.Length; ++i)
        {
            Tile tile = sublocations[i];
            if (tile.timer <= 0)
                continue;
            tile.timer--;
            if (tile.timer == 0)
            {
                int index = CalculateIndex(cityPos.x, cityPos.y, i);
                Quest quest = Global.Game.activeQuests.FirstOrDefault(x => x.type == Quest.Type.Clear && x.location == index);
                if (tile.clear)
                {
                    tile.clear = false;
                    tile.defeatedEnemies = 0;
                    if (quest != null)
                        quest.count = 0;
                }
                else
                {
                    --tile.defeatedEnemies;
                    if (quest != null && quest.count > 0)
                        --quest.count;
                    if (tile.defeatedEnemies != 0)
                        tile.timer = 3;
                }
            }

        }
    }
}
