using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCard : MonoBehaviour
{
    public Sprite enemySprite;
    public int index;

    public void Init(string text, float hp, bool enemy)
    {
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
        RectTransform rectTransform = transform.GetChild(3).GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(141.87f * hp, 6.0632f);
    }
}
