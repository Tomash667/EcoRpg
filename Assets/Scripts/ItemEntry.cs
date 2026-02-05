using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemEntry : MonoBehaviour
{
    public void Init(string text, string buttonText, UnityAction action)
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = text;
        Button button = transform.GetChild(1).GetComponent<Button>();
        button.onClick.AddListener(action);
        button.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText;
    }
}
