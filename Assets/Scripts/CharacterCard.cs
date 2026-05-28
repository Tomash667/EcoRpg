using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCard : MonoBehaviour
{
    private enum Action
    {
        None,
        Attack,
        Dodge
    }

    public Sprite enemySprite;
    public int index;

    private Action action;
    private float timer, prevPos;
    private int actionState, dir;

    public void Init(string text, float hp, bool enemy)
    {
        dir = enemy ? -1 : 1;
        transform.GetChild(2).GetComponent<TMP_Text>().text = text;
        if (hp != 1)
            SetHp(hp);
        if (enemy)
            transform.GetChild(0).GetComponent<Image>().sprite = enemySprite;
    }

    public void SetHp(float hp)
    {
        if (hp < 0)
            hp = 0;
        transform.GetChild(0).GetComponent<Image>().color = hp > 0 ? Color.white : new(0.25f, 0, 0);
        RectTransform rectTransform = transform.GetChild(3).GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(141.87f * hp, 6.0632f);
    }

    private void Update()
    {
        switch (action)
        {
        case Action.Attack:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.15f)
                    actionState = 1;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos, prevPos + 50f * dir, timer / 0.15f));
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                    action = Action.None;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos + 50f * dir, prevPos, (timer - 0.15f) / 0.2f));
            }
            break;
        case Action.Dodge:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.1f)
                    actionState = 1;
            }
            else if (actionState == 1)
            {
                if (timer >= 0.25f)
                    actionState = 2;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos, prevPos - 50f * dir, (timer - 0.1f) / 0.15f));
            }
            else if (actionState == 2)
            {
                if (timer >= 0.45f)
                    action = Action.None;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos - 50f * dir, prevPos, (timer - 0.25f) / 0.2f));
            }
            break;
        }
    }

    public void Attack()
    {
        action = Action.Attack;
        actionState = 0;
        timer = 0;
        prevPos = (transform as RectTransform).anchoredPosition.y;
    }

    public void Dodge()
    {
        action = Action.Dodge;
        actionState = 0;
        timer = 0;
        prevPos = (transform as RectTransform).anchoredPosition.y;
    }
}
