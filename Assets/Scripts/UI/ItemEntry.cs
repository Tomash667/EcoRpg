using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemEntry : MonoBehaviour, IPointerDownHandler
{
    public object data;
    public bool canSelect;

    private ItemEntryList list;
    private bool selected;

    public void Init(string text, string buttonText = null, UnityAction action = null)
    {
        InitCommon();

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
        InitCommon();

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

    public void SetImage(Sprite sprite)
    {
        Image image = transform.GetChild(3).GetComponent<Image>();
        image.sprite = sprite;
        image.gameObject.SetActive(true);

        RectTransform rectTransform = transform.GetChild(0) as RectTransform;
        rectTransform.anchoredPosition = new(265.77f, -24f);
        rectTransform.sizeDelta = new(483.14f, 48f);
    }

    private void InitCommon()
    {
        list = transform.GetComponentInParent<ItemEntryList>(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (list != null && !selected && canSelect)
            list.Select(this);
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        GetComponent<Image>().enabled = isSelected;
    }
}
