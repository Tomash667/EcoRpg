using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public GameObject itemEntryPrefab, lineSeparatorPrefab;

    private GameObject currentDialog, prevDialog;

    public bool HasDialog => currentDialog != null;

    private void Update()
    {
        if (currentDialog != null)
        {
            if (Input.GetKeyDown(Global.escKey))
                CloseDialog();
        }
    }

    public void ShowDialog(string text)
    {
        prevDialog = currentDialog;
        currentDialog = transform.Find("DialogPanel").gameObject;
        currentDialog.transform.Find("OkDialog").GetChild(0).GetComponent<TMP_Text>().text = text;
        currentDialog.SetActive(true);
    }

    public void ShowDialog(GameObject dialog)
    {
        prevDialog = currentDialog;
        currentDialog = dialog;
        dialog.SetActive(true);
    }

    public void CloseDialog()
    {
        currentDialog.SetActive(false);
        if (prevDialog != null)
        {
            currentDialog = prevDialog;
            prevDialog = null;
        }
        else
            currentDialog = null;
    }
}
