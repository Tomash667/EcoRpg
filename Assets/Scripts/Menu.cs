using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    private GameObject dialogPanel;

#if UNITY_EDITOR
    private const KeyCode escKey = KeyCode.Q;
#else
    private const KeyCode escKey = KeyCode.Escape;
#endif

    private void Awake()
    {
        dialogPanel = transform.Find("DialogPanel").gameObject;
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
            if (Input.GetKeyDown(escKey))
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
            Global.playerName = name;
            SceneManager.LoadScene("Main");
        }
    }

    public void NewGameCancel()
    {
        dialogPanel.SetActive(false);
    }
}
