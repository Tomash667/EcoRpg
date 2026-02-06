using System.IO;
using UnityEngine;

public class Global : MonoBehaviour
{
#if UNITY_EDITOR
    public const KeyCode escKey = KeyCode.Q;
#else
    public const KeyCode escKey = KeyCode.Escape;
#endif

    public static string SavePath => Application.persistentDataPath + "/save.json";

    public static Global Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<Global>();
                if (instance == null)
                {
                    GameObject obj = new();
                    DontDestroyOnLoad(obj);
                    instance = obj.AddComponent<Global>();
                    if (File.Exists(SavePath))
                        instance.loadGame = true;
                    else
                        instance.playerName = "Tomi";
                }
            }
            return instance;
        }
    }

    private static Global instance;

    public string playerName;
    public bool loadGame;
}
