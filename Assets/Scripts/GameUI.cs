using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
#if UNITY_EDITOR
    public const KeyCode escKey = KeyCode.F1;
#else
    public const KeyCode escKey = KeyCode.Escape;
#endif

    public GameObject itemEntryPrefab, lineSeparatorPrefab;

    private List<GameObject> dialogs;
    private Func<int, bool> inputFunc;
    private Action confirmAction;
    private GameObject okDialog, confirmDialog, inputDialog;

    public bool HasDialog => dialogs.Count > 0;
    public GameObject CurrentDialog => dialogs.Count > 0 ? dialogs[^1] : null;

    private void Awake()
    {
        dialogs = new();
        okDialog = transform.Find("OkDialog").gameObject;
        confirmDialog = transform.FindGameObject("ConfirmDialog");
        inputDialog = transform.FindGameObject("InputDialog");
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
            else if (currentDialog == confirmDialog || currentDialog == inputDialog)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    ConfirmDialog();
            }

            if (Input.GetKeyDown(escKey))
                CloseDialog();
        }
    }

    public void ShowDialog(string text)
    {
        okDialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        okDialog.transform.SetAsLastSibling();
        okDialog.SetActive(true);
        dialogs.Add(okDialog);
    }

    public void ShowDialog(GameObject dialog)
    {
        dialog.SetActive(true);
        dialogs.Add(dialog);
    }

    public void ShowInput(string text, Func<int, bool> func)
    {
        inputFunc = func;
        inputDialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        TMP_InputField input = inputDialog.transform.GetChild(0).GetChild(1).GetComponent<TMP_InputField>();
        input.text = string.Empty;
        inputDialog.transform.SetAsLastSibling();
        inputDialog.SetActive(true);
        input.ActivateInputField();
        dialogs.Add(inputDialog);
    }

    public void ShowConfirm(string text, Action action)
    {
        confirmAction = action;
        confirmDialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        confirmDialog.transform.SetAsLastSibling();
        confirmDialog.SetActive(true);
        dialogs.Add(confirmDialog);
    }

    public void ConfirmDialog()
    {
        GameObject currentDialog = dialogs[^1];
        if (currentDialog == inputDialog)
        {
            TMP_InputField input = inputDialog.transform.GetChild(0).GetChild(1).GetComponent<TMP_InputField>();
            string text = input.text.Trim();
            if (int.TryParse(text, out int value) && inputFunc(value))
                CloseDialog();
        }
        else
        {
            CloseDialog();
            confirmAction();
        }
    }

    public void CloseDialog()
    {
        dialogs[^1].SetActive(false);
        dialogs.RemoveAt(dialogs.Count - 1);
    }
}
