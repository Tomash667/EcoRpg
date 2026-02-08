using System;
using UnityEngine;

[Serializable]
public class Quest : ISerializationCallbackReceiver
{
    public Enemy enemy;
    public string enemyName;
    public int count, max;

    public int Reward => 25 * (enemy.level + 1) * max;
    public string Text => $"Defeat {count}/{max} {Utility.Plural(enemy.name)}";
    public string Title => $"Defeat {Utility.Plural(enemy.name, max)}";
    public string TitleReward => $"{Title} ({Reward} gold)";

    public void OnBeforeSerialize()
    {
        enemyName = enemy.name;
    }

    public void OnAfterDeserialize()
    {
        enemy = Enemy.Get(enemyName);
    }
}
