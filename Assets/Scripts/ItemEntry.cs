using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemEntry : MonoBehaviour
{
    public void Init(Item item, UnityAction action)
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = item.ToString();
        Button button = transform.GetChild(1).GetComponent<Button>();
        button.onClick.AddListener(action);
        button.transform.GetChild(0).GetComponent<TMP_Text>().text = "Buy";
    }

    public void Init(ItemSlot itemSlot, UnityAction action)
    {
        transform.GetChild(0).GetComponent<TMP_Text>().text = itemSlot.ToString();
        Button button = transform.GetChild(1).GetComponent<Button>();
        button.onClick.AddListener(action);
        button.transform.GetChild(0).GetComponent<TMP_Text>().text = "Sell";
    }
}
