using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    private GameUI ui;
    private GameObject newGameDialog, loadGameDialog;

    private void Awake()
    {
        ui = GetComponent<GameUI>();
        newGameDialog = transform.Find("NewGameDialog").gameObject;
        loadGameDialog = transform.Find("LoadGameDialog").gameObject;
        if (!Global.Instance.CanContinueGame)
            transform.Find("BtContinue").GetComponent<Button>().interactable = false;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif

        if (ui.CurrentDialog == newGameDialog)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                NewGameOk();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                Continue();
            if (Input.GetKeyDown(KeyCode.Alpha2))
                NewGame();
            if (Input.GetKeyDown(KeyCode.Alpha3))
                LoadGame();
            if (Input.GetKeyDown(KeyCode.Alpha4))
                Quit();
        }
    }

    public void Continue()
    {
        Global.Instance.ContinueGame();
    }

    public void NewGame()
    {
        TMP_InputField input = newGameDialog.GetComponentInChildren<TMP_InputField>();
        input.text = "";
        ui.ShowDialog(newGameDialog);
        input.ActivateInputField();
    }

    public void LoadGame()
    {
        RefreshSavesList();
        ui.ShowDialog(loadGameDialog);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void NewGameOk()
    {
        string name = newGameDialog.GetComponentInChildren<TMP_InputField>().text.Trim();
        if (name.Length > 0)
        {
            Global global = Global.Instance;
            global.playerName = name;
            global.playerFemale = newGameDialog.GetComponentInChildren<Toggle>().isOn;
            global.NewGame();
        }
    }

    private void RefreshSavesList()
    {
        Transform content = loadGameDialog.transform.Find("Panel/List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        Global global = Global.Instance;
        (int index, string text)[] saves = global.GetSaves();
        foreach ((int index, string text) in saves)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init2(text, "Load", () => global.LoadSave(index), "Delete", () =>
            {
                ui.ShowConfirm("Are you sure you want to delete this save?", () =>
                {
                    global.DeleteSave(index);
                    if (!global.CanContinueGame)
                        transform.Find("BtContinue").GetComponent<Button>().interactable = false;
                    RefreshSavesList();
                });
            });
        }
    }
}
