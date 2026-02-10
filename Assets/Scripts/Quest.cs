using System;
using UnityEngine;

[Serializable]
public class Quest : ISerializationCallbackReceiver
{
    public enum Type
    {
        Defeat,
        Clear
    }

    public Enemy enemy;
    public Type type;
    public string enemyName, location;
    public int count, max;

    public int Reward
    {
        get
        {
            if (type == Type.Defeat)
                return 25 * (enemy.level + 1) * max;
            else
                return 250;
        }
    }
    public string Text
    {
        get
        {
            if (type == Type.Defeat)
                return $"Defeat {count}/{max} {Utility.Plural(enemy.name)}";
            else
                return $"Clear {location.ToLower()} ({Mathf.Min(100 * count / max, 100)}%)";
        }
    }
    public string Title
    {
        get
        {
            if (type == Type.Defeat)
                return $"Defeat {Utility.Plural(enemy.name, max)}";
            else
                return $"Clear {location.ToLower()}";
        }
    }
    public string TitleReward => $"{Title} ({Reward} gold)";

    public void OnBeforeSerialize()
    {
        if (type == Type.Defeat)
            enemyName = enemy.name;
    }

    public void OnAfterDeserialize()
    {
        if (type == Type.Defeat)
            enemy = Enemy.Get(enemyName);
    }

    public bool IsSimilar(Quest quest)
    {
        if (type != quest.type)
            return false;

        if (type == Type.Defeat)
            return enemy == quest.enemy;
        else
            return location == quest.location;
    }
}
