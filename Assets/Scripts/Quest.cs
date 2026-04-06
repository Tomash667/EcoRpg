using System;
using UnityEngine;

[Serializable]
public class Quest : ISerializationCallbackReceiver
{
    public enum Type
    {
        Defeat,
        Clear,
        Gather,
        Artifact
    }

    public Enemy enemy;
    public Item item;
    public Type type;
    public string enemyName, itemName;
    public int location, count, max, locationDifficulty, difficulty, timer;

    public int Reward
    {
        get
        {
            return type switch
            {
                Type.Defeat => 25 * (enemy.level + locationDifficulty) * max,
                Type.Gather => locationDifficulty * 250,
                Type.Clear => locationDifficulty * 250,
                Type.Artifact => locationDifficulty * 500,
                _ => 0,
            };
        }
    }
    public string Text
    {
        get
        {
            return type switch
            {
                Type.Defeat => $"Defeat {count}/{max} {Utility.Plural(enemy.name)}",
                Type.Clear => $"Clear {GetLocationName()} ({Mathf.Min(100 * count / max, 100)}%)",
                Type.Gather => $"Gather {Global.Player.CountItem(item)}/{max} {Utility.Plural(item.name)}",
                Type.Artifact => count == 0 ? $"Find artifact in {GetLocationName()}" : $"Bring artifact from {GetLocationName()} to guild",
                _ => string.Empty
            };
        }
    }
    public string Title
    {
        get
        {
            return type switch
            {
                Type.Defeat => $"Defeat {Utility.Plural(enemy.name, max)}",
                Type.Clear => $"Clear {GetLocationName()}",
                Type.Gather => $"Gather {Utility.Plural(item.name, max)}",
                Type.Artifact => $"Find artifact in {GetLocationName()}",
                _ => string.Empty
            };
        }
    }
    public string TitleReward => $"{Title} ({Reward} gold)";

    public void OnBeforeSerialize()
    {
        if (type == Type.Defeat)
            enemyName = enemy.name;
        else if (type == Type.Gather)
            itemName = item.name;
    }

    public void OnAfterDeserialize()
    {
        if (type == Type.Defeat)
            enemy = Enemy.Get(enemyName);
        else if (type == Type.Gather)
            item = Item.Get(itemName);
    }

    public bool IsSimilar(Quest quest)
    {
        if (type != quest.type)
            return false;

        return type switch
        {
            Type.Defeat => enemy == quest.enemy,
            Type.Clear or Type.Artifact => location == quest.location,
            Type.Gather => item == quest.item,
            _ => false
        };
    }

    public bool IsDone()
    {
        if (type == Type.Gather)
            return Global.Player.CountItem(item) >= max;
        else
            return count >= max;
    }

    public void Finish()
    {
        if(type == Type.Gather)
            Global.Player.RemoveItem(item, max);
    }

    private string GetLocationName()
    {
        Tile tile = Global.World.GetLocation(location);
        if (tile.type == TileType.City)
            return TileType.Sewers.AsString();
        return tile.RealName;
    }
}
