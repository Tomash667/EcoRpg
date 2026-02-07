using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public GameObject itemEntryPrefab, lineSeparatorPrefab;

    private readonly List<GameObject> dialogs = new();
    private Func<int, bool> inputFunc;
    private GameObject okDialog, inputDialog;

    public bool HasDialog => dialogs.Count > 0;

    private void Awake()
    {
        okDialog = transform.Find("OkDialog").gameObject;
        inputDialog = transform.Find("InputDialog").gameObject;
    }

    private void Update()
    {
        if (HasDialog)
        {
            GameObject currentDialog = dialogs[^1];
            if (currentDialog == okDialog)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    CloseDialog();
            }
            else if (currentDialog == inputDialog)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    ConfirmDialog();
            }

            if (Input.GetKeyDown(Global.escKey))
                CloseDialog();
        }
    }

    public void ShowDialog(string text)
    {
        GameObject dialog = transform.Find("OkDialog").gameObject;
        dialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        dialog.transform.SetAsLastSibling();
        dialog.SetActive(true);
        dialogs.Add(dialog);
    }

    public void ShowDialog(GameObject dialog)
    {
        dialog.SetActive(true);
        dialogs.Add(dialog);
    }

    public void ShowInput(string text, Func<int, bool> func)
    {
        inputFunc = func;
        GameObject dialog = transform.Find("InputDialog").gameObject;
        dialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        TMP_InputField input = dialog.transform.GetChild(0).GetChild(1).GetComponent<TMP_InputField>();
        input.text = string.Empty;
        dialog.transform.SetAsLastSibling();
        dialog.SetActive(true);
        input.ActivateInputField();
        dialogs.Add(dialog);
    }

    public void ConfirmDialog()
    {
        GameObject dialog = transform.Find("InputDialog").gameObject;
        TMP_InputField input = dialog.transform.GetChild(0).GetChild(1).GetComponent<TMP_InputField>();
        string text = input.text.Trim();
        if (int.TryParse(text, out int value) && inputFunc(value))
            CloseDialog();
    }

    public void CloseDialog()
    {
        dialogs[^1].SetActive(false);
        dialogs.RemoveAt(dialogs.Count - 1);
    }
}
