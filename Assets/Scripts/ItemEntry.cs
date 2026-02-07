using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemEntry : MonoBehaviour
{
    public void Init(string text, string buttonText = null, UnityAction action = null)
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = text;
        Button button = transform.GetChild(1).GetComponent<Button>();
        if (buttonText == null)
            button.gameObject.SetActive(false);
        else
        {
            button.onClick.AddListener(action);
            button.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText;
        }
    }
}
