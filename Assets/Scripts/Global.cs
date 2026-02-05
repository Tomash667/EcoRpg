using UnityEngine;

public static class Global
{
#if UNITY_EDITOR
    public const KeyCode escKey = KeyCode.Q;
#else
    public const KeyCode escKey = KeyCode.Escape;
#endif

    public static string SavePath => Application.persistentDataPath + "/save.json";

    public static string playerName;
    public static bool loadGame;
}
