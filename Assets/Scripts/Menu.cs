using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    private GameObject dialogPanel;

    private void Awake()
    {
        dialogPanel = transform.Find("DialogPanel").gameObject;
        if (!File.Exists(Global.SavePath))
            transform.Find("BtContinue").GetComponent<Button>().interactable = false;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif

        if (dialogPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                NewGameOk();
            if (Input.GetKeyDown(Global.escKey))
                NewGameCancel();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                Continue();
            if (Input.GetKeyDown(KeyCode.Alpha2))
                NewGame();
            if (Input.GetKeyDown(KeyCode.Alpha3))
                Quit();
        }
    }

    public void Continue()
    {
        if (File.Exists(Global.SavePath))
        {
            Global.Instance.loadGame = true;
            SceneManager.LoadScene("Game");
        }
    }

    public void NewGame()
    {
        TMP_InputField input = dialogPanel.GetComponentInChildren<TMP_InputField>();
        input.text = "";
        dialogPanel.SetActive(true);
        input.ActivateInputField();
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
        string name = dialogPanel.GetComponentInChildren<TMP_InputField>().text.Trim();
        if (name.Length > 0)
        {
            Global global = Global.Instance;
            global.playerName = name;
            global.loadGame = false;
            SceneManager.LoadScene("Game");
        }
    }

    public void NewGameCancel()
    {
        dialogPanel.SetActive(false);
    }
}
