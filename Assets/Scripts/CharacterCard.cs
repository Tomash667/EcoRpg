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
        Cast,
        Dodge,
        Damage,
        Block,
        BlockDamage,
        Escape,
        Approach,
        Heal,
        Mana,
        PoisonDamage,
        Confused,
        Summon,
        Unsummon,
        Move
    }

    private enum ChildIndex
    {
        Image,
        TextBackground,
        Health,
        Mana,
        Block,
        Blood,
        Heal,
        Effects,
        Poison,
        Confused
    }

    private const float BarWidth = 141.87f;
    private const float BarHeight = 6f;

    public Sprite[] effectSprites;
    public Sprite healSprite, manaSprite;
    public GameObject effectPrefab;
    public object owner;
    public int slot;

    private Vector2 fromPos, toPos;
    private Action action;
    private Effect addEffect, removeEffect;
    private float timer, prevPos, nextValue;
    private int actionState, dir;

    public Vector2 position => (transform as RectTransform).anchoredPosition;

    public void Init(string text, float hp, float mp, bool enemy, Sprite sprite)
    {
        dir = enemy ? -1 : 1;
        transform.GetChild((int)ChildIndex.TextBackground).GetChild(0).GetComponent<TMP_Text>().text = text;
        if (hp != 1)
            SetHp(hp);
        if (mp >= 0)
        {
            // if character use mana show mana bar and move health bar and text up
            transform.GetChild((int)ChildIndex.Mana).gameObject.SetActive(true);
            SetMp(mp);

            RectTransform rectTransform = transform.GetChild((int)ChildIndex.Health).transform as RectTransform;
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y + BarHeight);

            rectTransform = transform.GetChild((int)ChildIndex.TextBackground).transform as RectTransform;
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y + BarHeight);
        }
        transform.GetChild((int)ChildIndex.Image).GetComponent<Image>().sprite = sprite;
    }

    public void SetHp(float hp)
    {
        if (hp < 0)
            hp = 0;
        else if (hp > 0 && hp < 0.01f)
            hp = 0.01f;
        RectTransform rectTransform = transform.GetChild((int)ChildIndex.Health) as RectTransform;
        rectTransform.sizeDelta = new Vector2(BarWidth * hp, BarHeight);

        // turn image red when defeated
        transform.GetChild((int)ChildIndex.Image).GetComponent<Image>().color = hp > 0 ? Color.white : new(0.25f, 0, 0);

        // remove all effects when defeated
        if (hp <= 0)
        {
            Transform effects = transform.GetChild((int)ChildIndex.Effects);
            foreach (Transform child in effects)
                Destroy(child.gameObject);
        }
    }

    public void SetMp(float mp)
    {
        RectTransform rectTransform = transform.GetChild((int)ChildIndex.Mana) as RectTransform;
        rectTransform.sizeDelta = new Vector2(BarWidth * mp, BarHeight);
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
                    if (nextValue > 0)
                    {
                        // heal on attack
                        transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(true);
                        SetHp(nextValue);
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
                    if (nextValue > 0)
                    {
                        // heal on attack
                        transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(false);
                    }
                }
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos + 50f * dir, prevPos, (timer - 0.15f) / 0.2f));
            }
            break;
        case Action.Cast:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                // move forward a bit
                if (timer >= 0.15f)
                    actionState = 1;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos, prevPos + 25f * dir, timer / 0.15f));
            }
            else if (actionState == 1)
            {
                // wait
                if (timer >= 0.25f)
                    actionState = 2;
            }
            else
            {
                // go back to start position
                if (timer >= 0.4f)
                    action = Action.None;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(prevPos + 25f * dir, prevPos, (timer - 0.25f) / 0.15f));
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
                    transform.GetChild((int)ChildIndex.Blood).gameObject.SetActive(true);
                    SetHp(nextValue);
                    if (addEffect != Effect.None)
                        AddEffect();
                    actionState = 1;
                }
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    transform.GetChild((int)ChildIndex.Blood).gameObject.SetActive(false);
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
                    transform.GetChild((int)ChildIndex.Block).gameObject.SetActive(true);
                    if (action == Action.BlockDamage)
                    {
                        transform.GetChild((int)ChildIndex.Blood).gameObject.SetActive(true);
                        SetHp(nextValue);
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
                        transform.GetChild((int)ChildIndex.Blood).gameObject.SetActive(false);
                    transform.GetChild((int)ChildIndex.Block).gameObject.SetActive(false);
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
        case Action.Approach:
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y + 50f * dir * Time.deltaTime);
            }
            break;
        case Action.Heal:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.15f)
                {
                    transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(true);
                    if (removeEffect != Effect.None)
                        RemoveEffect();
                    SetHp(nextValue);
                    actionState = 1;
                }
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(false);
                    action = Action.None;
                }
            }
            break;
        case Action.Mana:
            timer += Time.deltaTime;
            if (actionState == 0)
            {
                if (timer >= 0.15f)
                {
                    transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(true);
                    SetMp(nextValue);
                    actionState = 1;
                }
            }
            else if (actionState == 1)
            {
                if (timer >= 0.35f)
                {
                    GameObject obj = transform.GetChild((int)ChildIndex.Heal).gameObject;
                    obj.SetActive(false);
                    obj.GetComponent<Image>().sprite = healSprite;
                    action = Action.None;
                }
            }
            break;
        case Action.PoisonDamage:
        case Action.Confused:
            timer += Time.deltaTime;
            if (timer >= 0.2f)
            {
                transform.GetChild((int)(action == Action.PoisonDamage ? ChildIndex.Poison : ChildIndex.Confused)).gameObject.SetActive(false);
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
        case Action.Move:
            {
                timer += Time.deltaTime;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = Vector2.Lerp(fromPos, toPos, timer / 0.3f);
                if (timer >= 0.3f)
                    action = Action.None;
            }
            break;
        }
    }

    public void Attack(float nextHp = -1f)
    {
        nextValue = nextHp;
        action = Action.Attack;
        actionState = 0;
        timer = 0;
        prevPos = (transform as RectTransform).anchoredPosition.y;
    }

    public void Cast()
    {
        action = Action.Cast;
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
        nextValue = nextHp;
        action = Action.Damage;
        actionState = 0;
        timer = 0;
    }

    public void PoisonDamage(float nextHp)
    {
        SetHp(nextHp);
        action = Action.PoisonDamage;
        transform.GetChild((int)ChildIndex.Poison).gameObject.SetActive(true);
        actionState = 0;
        timer = 0;
    }

    public void Block(float nextHp = Mathf.NegativeInfinity)
    {
        if (nextHp == Mathf.NegativeInfinity)
            action = Action.Block;
        else
        {
            nextValue = nextHp;
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

    public void Approach()
    {
        action = Action.Approach;
    }

    public void Heal(float nextHp)
    {
        nextValue = nextHp;
        action = Action.Heal;
        actionState = 0;
        timer = 0;
    }

    public void RestoreMana(float nextMp)
    {
        nextValue = nextMp;
        action = Action.Mana;
        actionState = 0;
        timer = 0;
        transform.GetChild((int)ChildIndex.Heal).GetComponent<Image>().sprite = manaSprite;
    }

    public void AddEffect(Effect effect)
    {
        addEffect = effect;
    }

    private void AddEffect()
    {
        Transform effects = transform.GetChild((int)ChildIndex.Effects);
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
        Transform effects = transform.GetChild((int)ChildIndex.Effects);
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
        transform.GetChild((int)ChildIndex.Confused).gameObject.SetActive(true);
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
            transform.parent.parent.GetComponent<Combat>().SelectCard(owner);
    }

    public void SetColor(Color color)
    {
        GetComponent<Image>().color = color;
    }

    public void Move(Vector2 pos)
    {
        action = Action.Move;
        timer = 0;
        fromPos = position;
        toPos = pos;
    }
}
