using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterCard : MonoBehaviour, IPointerClickHandler
{
    private enum Action
    {
        None,
        Attack,
        Dodge,
        Damage,
        Block,
        BlockDamage,
        Escape,
        Heal,
        PoisonDamage,
        Confused,
        Summon,
        Unsummon
    }

    public Sprite[] effectSprites;
    public GameObject effectPrefab;
    public int index;

    private Action action;
    private Effect addEffect, removeEffect;
    private float timer, prevPos, nextHp;
    private int actionState, dir;

    public Vector2 position => (transform as RectTransform).anchoredPosition;

    public void Init(string text, float hp, bool enemy, Sprite sprite)
    {
        dir = enemy ? -1 : 1;
        transform.GetChild(1).GetChild(0).GetComponent<TMP_Text>().text = text;
        if (hp != 1)
            SetHp(hp);
        transform.GetChild(0).GetComponent<Image>().sprite = sprite;
    }

    public void SetHp(float hp)
    {
        if (hp < 0)
            hp = 0;
        else if (hp > 0 && hp < 0.01f)
            hp = 0.01f;
        transform.GetChild(0).GetComponent<Image>().color = hp > 0 ? Color.white : new(0.25f, 0, 0);
        RectTransform rectTransform = transform.GetChild(2).GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(141.87f * hp, 6.0632f);
        if (hp <= 0)
        {
            // remove all effects when defeated
            Transform effects = transform.GetChild(6);
            for (int i = 0; i < effects.childCount; ++i)
                Destroy(effects.GetChild(i).gameObject);
        }
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
                {
                    if (nextHp > 0)
                    {
                        // heal on attack
                        transform.GetChild(5).gameObject.SetActive(true);
                        SetHp(nextHp);
                    }
                    actionState = 1;
                }
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos, prevPos + 50f * dir, timer / 0.15f));
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    action = Action.None;
                    if (nextHp > 0)
                    {
                        // heal on attack
                        transform.GetChild(5).gameObject.SetActive(false);
                    }
                }
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
        case Action.Damage:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.15f)
                {
                    transform.GetChild(4).gameObject.SetActive(true);
                    SetHp(nextHp);
                    if (addEffect != Effect.None)
                        AddEffect();
                    actionState = 1;
                }
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    transform.GetChild(4).gameObject.SetActive(false);
                    action = Action.None;
                }
            }
            break;
        case Action.Block:
        case Action.BlockDamage:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.15f)
                {
                    actionState = 1;
                    transform.GetChild(3).gameObject.SetActive(true);
                    if (action == Action.BlockDamage)
                    {
                        transform.GetChild(4).gameObject.SetActive(true);
                        SetHp(nextHp);
                        if (addEffect != Effect.None)
                            AddEffect();
                    }
                }
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos, prevPos + 25f * dir, timer / 0.15f));
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    if (action == Action.BlockDamage)
                        transform.GetChild(4).gameObject.SetActive(false);
                    transform.GetChild(3).gameObject.SetActive(false);
                    action = Action.None;
                }
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos + 25f * dir, prevPos, (timer - 0.15f) / 0.2f));
            }
            break;
        case Action.Escape:
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y - 600f * dir * Time.deltaTime);
            }
            break;
        case Action.Heal:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.15f)
                {
                    transform.GetChild(5).gameObject.SetActive(true);
                    if (removeEffect != Effect.None)
                        RemoveEffect();
                    SetHp(nextHp);
                    actionState = 1;
                }
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    transform.GetChild(5).gameObject.SetActive(false);
                    action = Action.None;
                }
            }
            break;
        case Action.PoisonDamage:
        case Action.Confused:
            timer += Time.deltaTime;
            if (timer >= 0.2f)
            {
                transform.GetChild(action == Action.PoisonDamage ? 7 : 8).gameObject.SetActive(false);
                if (removeEffect != Effect.None)
                    RemoveEffect();
                action = Action.None;
            }
            break;
        case Action.Summon:
            timer += Time.deltaTime;
            GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(timer / 0.3f);
            if (timer >= 0.3f)
                action = Action.None;
            break;
        case Action.Unsummon:
            timer += Time.deltaTime;
            GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(1f - timer / 0.3f);
            if (timer >= 0.3f)
            {
                action = Action.None;
                Destroy(gameObject);
            }
            break;
        }
    }

    public void Attack(float nextHp = -1f)
    {
        this.nextHp = nextHp;
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

    public void Damage(float nextHp)
    {
        this.nextHp = nextHp;
        action = Action.Damage;
        actionState = 0;
        timer = 0;
    }

    public void PoisonDamage(float nextHp)
    {
        SetHp(nextHp);
        action = Action.PoisonDamage;
        transform.GetChild(7).gameObject.SetActive(true);
        actionState = 0;
        timer = 0;
    }

    public void Block(float nextHp = Mathf.NegativeInfinity)
    {
        if (nextHp == Mathf.NegativeInfinity)
            action = Action.Block;
        else
        {
            this.nextHp = nextHp;
            action = Action.BlockDamage;
        }
        actionState = 0;
        timer = 0;
        prevPos = (transform as RectTransform).anchoredPosition.y;
    }

    public void Escape()
    {
        action = Action.Escape;
    }

    public void Heal(float nextHp)
    {
        this.nextHp = nextHp;
        action = Action.Heal;
        actionState = 0;
        timer = 0;
    }

    public void AddEffect(Effect effect)
    {
        addEffect = effect;
    }

    private void AddEffect()
    {
        Transform effects = transform.GetChild(6);
        for (int i = 0; i < effects.childCount; ++i)
        {
            if (effects.GetChild(i).GetComponent<EffectIcon>().effect == addEffect)
            {
                // already added
                addEffect = Effect.None;
                return;
            }
        }
        GameObject obj = Instantiate(effectPrefab, effects);
        obj.GetComponent<EffectIcon>().effect = addEffect;
        obj.GetComponent<Image>().sprite = effectSprites[(int)addEffect - 1];
        addEffect = Effect.None;
    }

    public void RemoveEffect(Effect effect)
    {
        removeEffect = effect;
    }

    private void RemoveEffect()
    {
        Transform effects = transform.GetChild(6);
        for (int i = 0; i < effects.childCount; ++i)
        {
            Transform child = effects.GetChild(i);
            if (child.GetComponent<EffectIcon>().effect == removeEffect)
                Destroy(child.gameObject);
        }
        removeEffect = Effect.None;
    }

    public void Confused()
    {
        action = Action.Confused;
        transform.GetChild(8).gameObject.SetActive(true);
        timer = 0;
    }

    public void Summon()
    {
        action = Action.Summon;
        transform.GetComponent<CanvasGroup>().alpha = 0;
        timer = 0;
    }

    public void Unsummon()
    {
        action = Action.Unsummon;
        timer = 0;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            transform.parent.parent.GetComponent<Combat>().SelectCard(index);
    }

    public void SetColor(Color color)
    {
        GetComponent<Image>().color = color;
    }
}
