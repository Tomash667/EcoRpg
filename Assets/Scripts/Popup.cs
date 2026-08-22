using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Popup : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text text;
    private CanvasGroup canvasGroup;
    private float timer;
    private int state;

    public void Show(string str)
    {
        text.text = str;
        state = 1;
        timer = 0;
        canvasGroup.alpha = 0;
        gameObject.SetActive(true);
    }

    private void Awake()
    {
        text = transform.GetChild(0).GetComponent<TMP_Text>();
        canvasGroup = transform.GetComponent<CanvasGroup>();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        switch (state)
        {
        case 1:
            timer += Time.deltaTime * 4;
            if (timer >= 1f)
            {
                timer = 0;
                state = 2;
                canvasGroup.alpha = 1;
            }
            else
                canvasGroup.alpha = timer;
            break;
        case 2:
            timer += Time.deltaTime;
            if (timer >= 10f)
            {
                timer = 1f;
                state = 3;
            }
            break;
        case 3:
            timer -= Time.deltaTime * 4;
            if (timer <= 0f)
            {
                state = 0;
                canvasGroup.alpha = 0;
                gameObject.SetActive(false);
            }
            else
                canvasGroup.alpha = timer;
            break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (state == 1)
            state = 3;
        else if (state == 2)
        {
            state = 3;
            timer = 1;
        }
    }
}
