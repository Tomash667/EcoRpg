using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DropdownEntry : MonoBehaviour
{
    public void Init(string text, string buttonText, string[] choices, UnityAction<int> action)
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = text;

        TMP_Dropdown dropdown = transform.GetChild(1).GetComponent<TMP_Dropdown>();
        foreach (string choice in choices)
            dropdown.options.Add(new TMP_Dropdown.OptionData { text = choice });

        Button button = transform.GetChild(2).GetComponent<Button>();
        button.transform.GetChild(0).GetComponent<TMP_Text>().text = buttonText;
        button.onClick.AddListener(() => action.Invoke(dropdown.value));
    }
}
