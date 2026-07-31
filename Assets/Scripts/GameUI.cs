using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : MonoBehaviour
{
#if UNITY_EDITOR
    public const KeyCode escKey = KeyCode.F1;
#else
    public const KeyCode escKey = KeyCode.Escape;
#endif

    public Sprite[] backgrounds;
    public Sprite[] propertyIcons;
    public Sprite[] itemIcons;
    public GameObject itemEntryPrefab, lineSeparatorPrefab, textHeaderPrefab, dropdownEntryPrefab;

    [NonSerialized]
    public bool lockDialog;

    private List<GameObject> dialogs;
    private Func<int, bool> inputFunc;
    private Func<string, bool> inputStrFunc;
    private Action<bool> confirmAction2;
    private Action confirmAction;
    private GameObject okDialog, confirmDialog, inputDialog;

    public bool HasDialog => dialogs.Count > 0;
    public GameObject CurrentDialog => dialogs.Count > 0 ? dialogs[^1] : null;
    public GameObject TopDialog => dialogs.Count > 0 ? dialogs[0] : null;

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
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputDialog.transform.SetAsLastSibling();
        inputDialog.SetActive(true);
        input.ActivateInputField();
        dialogs.Add(inputDialog);
    }


    public void ShowInput(string text, Func<string, bool> func, string def = null)
    {
        inputStrFunc = func;
        inputDialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        TMP_InputField input = inputDialog.transform.GetChild(0).GetChild(1).GetComponent<TMP_InputField>();
        input.text = def ?? string.Empty;
        input.contentType = TMP_InputField.ContentType.Standard;
        inputDialog.transform.SetAsLastSibling();
        inputDialog.SetActive(true);
        input.ActivateInputField();
        dialogs.Add(inputDialog);
    }

    public void ShowConfirm(string text, Action action)
    {
        ShowConfirm(text, x => action());
    }

    public void ShowConfirm(string text, Action<bool> action)
    {
        confirmAction = null;
        confirmAction2 = action;
        confirmDialog.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = text;
        confirmDialog.transform.SetAsLastSibling();
        confirmDialog.SetActive(true);
        dialogs.Add(confirmDialog);
    }

    public void ConfirmDialog()
    {
        if (CurrentDialog == inputDialog)
        {
            TMP_InputField input = inputDialog.transform.GetChild(0).GetChild(1).GetComponent<TMP_InputField>();
            string text = input.text.Trim();
            if (input.contentType == TMP_InputField.ContentType.IntegerNumber)
            {
                if (int.TryParse(text, out int value) && inputFunc(value) && CurrentDialog == inputDialog)
                    CloseDialogInternal();
            }
            else
            {
                if (inputStrFunc(text) && CurrentDialog == inputDialog)
                    CloseDialogInternal();
            }
        }
        else
        {
            CloseDialogInternal();
            if (confirmAction == null)
                confirmAction2(true);
            else
                confirmAction();
        }
    }

    public void CloseDialog()
    {
        if (lockDialog)
            return;
        GameObject currentDialog = dialogs[^1];
        CloseDialogInternal();
        if (currentDialog == confirmDialog)
        {
            if (confirmAction == null)
                confirmAction2(false);
        }
    }

    private void CloseDialogInternal()
    {
        dialogs[^1].SetActive(false);
        dialogs.RemoveAt(dialogs.Count - 1);
    }

    public void CloseDialogs(Func<GameObject, bool> pred)
    {
        dialogs.RemoveAll(x =>
        {
            if (pred(x))
            {
                x.SetActive(false);
                return true;
            }
            else
                return false;
        });
    }

    public void UpdateBackground(int index)
    {
        transform.Find("Background").GetComponent<Image>().sprite = backgrounds[index];
    }

    public void AddTextHeader(string text, Transform parent)
    {
        Instantiate(textHeaderPrefab, parent).transform.GetChild(0).GetComponent<TMP_Text>().text = text;
    }

    public bool IsOpen(GameObject dialog)
    {
        return dialogs.Contains(dialog);
    }
}
