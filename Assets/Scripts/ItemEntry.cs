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

        Button button2 = transform.GetChild(2).GetComponent<Button>();
        button2.gameObject.SetActive(false);
        ((RectTransform)button.transform).sizeDelta = new(100, 42);
    }

    public void Init2(string text, string buttonText = null, UnityAction action = null, string buttonText2 = null, UnityAction action2 = null)
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = text;

        Button button = transform.GetChild(2).GetComponent<Button>();
        if (buttonText == null)
            button.gameObject.SetActive(false);
        else
        {
            button.onClick.AddListener(action);
            button.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText;
        }

        Button button2 = transform.GetChild(1).GetComponent<Button>();
        if (buttonText2 == null)
        {
            button2.gameObject.SetActive(false);
        }
        else
        {
            button2.onClick.AddListener(action2);
            button2.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText2;
        }
    }
}
