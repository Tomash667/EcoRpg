using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Global : MonoBehaviour
{
    [Serializable]
    public struct SaveState
    {
        public int currentIndex;
        public int nextIndex;
    }

    private static string SavesDir => Application.persistentDataPath + "/saves";
    private static string SaveStatePath => Application.persistentDataPath + "/save.json";

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
                    instance.Init();
                }
            }
            return instance;
        }
    }

    private static Global instance;

    public Game game;
    public string playerName;
    public Class playerClass;
    public bool loadGame, playerFemale;

    private SaveState saveState;

    public bool CanContinueGame => saveState.currentIndex != 0 && File.Exists($"{SavesDir}/{saveState.currentIndex}.sav");
    public static Game Game => Instance.game;
    public static World World => Instance.game.world;
    public static Player Player => Instance.game.player;
    public static GameUI UI => Instance.game.UI;
    public static TileType Location => Instance.game.world.Location;

    private void Init()
    {
        if (File.Exists(SaveStatePath))
        {
            string json = File.ReadAllText(SaveStatePath);
            saveState = JsonUtility.FromJson<SaveState>(json);
            if (saveState.currentIndex != 0)
                loadGame = true;
            else
                playerName = "Tomi";
        }
        else
            saveState = new() { nextIndex = 1 };
    }

    private void DoSaveState()
    {
        string json = JsonUtility.ToJson(saveState);
        File.WriteAllText(SaveStatePath, json);
    }

    public void NewGame()
    {
        saveState.currentIndex = saveState.nextIndex;
        ++saveState.nextIndex;
        DoSaveState();
        loadGame = false;
        SceneManager.LoadScene("Game");
    }

    public void ContinueGame()
    {
        if (CanContinueGame)
        {
            loadGame = true;
            SceneManager.LoadScene("Game");
        }
    }

    public (int index, string text)[] GetSaves()
    {
        if (!Directory.Exists(SavesDir))
            return Array.Empty<(int, string)>();

        return Directory.GetFiles(SavesDir, "*.sav")
            .Select(fileName =>
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                if (!int.TryParse(fileNameWithoutExt, out int index) || index < 1)
                    return (0, null);
                string text = File.ReadLines(fileName).First();
                return (index, text);
            })
            .Where(x => x.index != 0)
            .OrderBy(x => x.index)
            .ToArray();
    }

    public void LoadSave(int index)
    {
        saveState.currentIndex = index;
        DoSaveState();
        loadGame = true;
        SceneManager.LoadScene("Game");
    }

    public void DeleteSave(int index)
    {
        File.Delete($"{SavesDir}/{index}.sav");
        if (saveState.currentIndex == index)
        {
            saveState.currentIndex = 0;
            DoSaveState();
        }
    }

    public string GetSaveData()
    {
        string[] lines = File.ReadAllLines($"{SavesDir}/{saveState.currentIndex}.sav");
        return lines[1];
    }

    public void SaveGame(string text, string json)
    {
        if (saveState.currentIndex == 0)
        {
            saveState.currentIndex = saveState.nextIndex;
            ++saveState.nextIndex;
            DoSaveState();
        }
        Directory.CreateDirectory(SavesDir);
        File.WriteAllLines($"{SavesDir}/{saveState.currentIndex}.sav", new string[] { text, json });
    }
}
