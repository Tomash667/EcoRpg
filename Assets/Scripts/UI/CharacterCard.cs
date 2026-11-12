using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Combat is turn based but cards are animated
// Logic of animation and delays to show/hide images are here
public class CharacterCard : MonoBehaviour, IPointerClickHandler
{
    private enum Action
    {
        None,
        SetHp,
        SetMp,
        Attack,
        Cast,
        Dodge,
        Damage,
        Block,
        Escape,
        Approach,
        Heal,
        Mana,
        PoisonDamage,
        Confused,
        Summon,
        Unsummon,
        Move,
        AddEffect,
        RemoveEffect
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

    private class Subaction
    {
        public Action action;
        public Effect.Type effect;
        public Vector2 fromPos, toPos;
        public float timer, prevPos, value;
        public int state;
    }

    private const float BarWidth = 141.87f;
    private const float BarHeight = 6f;

    public Sprite[] effectSprites;
    public Sprite healSprite, manaSprite;
    public GameObject effectPrefab;
    public object owner;
    public int slot;

    private readonly List<Subaction> subactions = new();
    private int dir;

    public Vector2 position => (transform as RectTransform).anchoredPosition;

    public void Init(string text, float hp, float mp, bool enemy, Sprite sprite)
    {
        dir = enemy ? -1 : 1;
        transform.GetChild((int)ChildIndex.TextBackground).GetChild(0).GetComponent<TMP_Text>().text = text;
        if (hp != 1)
            SetHpInternal(hp);
        if (mp >= 0)
        {
            // if character use mana show mana bar and move health bar and text up
            transform.GetChild((int)ChildIndex.Mana).gameObject.SetActive(true);
            SetMpInternal(mp);

            RectTransform rectTransform = transform.GetChild((int)ChildIndex.Health).transform as RectTransform;
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y + BarHeight);

            rectTransform = transform.GetChild((int)ChildIndex.TextBackground).transform as RectTransform;
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y + BarHeight);
        }
        transform.GetChild((int)ChildIndex.Image).GetComponent<Image>().sprite = sprite;
    }

    public void SetHp(float hp, float delay = 0)
    {
        if (delay == 0)
            SetHpInternal(hp);
        else
            subactions.Add(new Subaction { action = Action.SetHp, value = hp, timer = delay });
    }

    private void SetHpInternal(float hp)
    {
        if (hp < 0)
            hp = 0;
        else if (hp > 0 && hp < 0.01f)
            hp = 0.01f;
        RectTransform rectTransform = transform.GetChild((int)ChildIndex.Health) as RectTransform;
        rectTransform.sizeDelta = new Vector2(BarWidth * hp, BarHeight);
        // turn image red when defeated
        transform.GetChild((int)ChildIndex.Image).GetComponent<Image>().color = hp > 0 ? Color.white : new(0.25f, 0, 0);
    }

    public void SetMp(float mp, float delay = 0)
    {
        if (delay == 0)
            SetMpInternal(mp);
        else
            subactions.Add(new Subaction { action = Action.SetMp, value = mp, timer = delay });
    }

    private void SetMpInternal(float mp)
    {
        RectTransform rectTransform = transform.GetChild((int)ChildIndex.Mana) as RectTransform;
        rectTransform.sizeDelta = new Vector2(BarWidth * mp, BarHeight);
    }

    private void Update()
    {
        subactions.RemoveAll(sub => UpdateSubaction(sub));
    }

    private bool UpdateSubaction(Subaction sub)
    {
        switch (sub.action)
        {
        case Action.SetHp:
            sub.timer -= Time.deltaTime;
            if (sub.timer <= 0)
            {
                SetHpInternal(sub.value);
                return true;
            }
            break;
        case Action.SetMp:
            sub.timer -= Time.deltaTime;
            if (sub.timer <= 0)
            {
                SetMpInternal(sub.value);
                return true;
            }
            break;
        case Action.Attack:
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos, sub.prevPos + 50f * dir, sub.timer / 0.15f));
                if (sub.timer >= 0.15f)
                    sub.state = 1;
            }
            else
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos + 50f * dir, sub.prevPos, (sub.timer - 0.15f) / 0.2f));
                if (sub.timer >= 0.35f)
                    return true;
            }
            break;
        case Action.Cast:
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                // move forward a bit
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos, sub.prevPos + 25f * dir, sub.timer / 0.15f));
                if (sub.timer >= 0.15f)
                    sub.state = 1;
            }
            else if (sub.state == 1)
            {
                // wait
                if (sub.timer >= 0.25f)
                    sub.state = 2;
            }
            else
            {
                // go back to start position
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos + 25f * dir, sub.prevPos, (sub.timer - 0.25f) / 0.15f));
                if (sub.timer >= 0.4f)
                    return true;
            }
            break;
        case Action.Dodge:
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                if (sub.timer >= 0.1f)
                    sub.state = 1;
            }
            else if (sub.state == 1)
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos, sub.prevPos - 50f * dir, (sub.timer - 0.1f) / 0.15f));
                if (sub.timer >= 0.25f)
                    sub.state = 2;
            }
            else
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos - 50f * dir, sub.prevPos, (sub.timer - 0.25f) / 0.2f));
                if (sub.timer >= 0.45f)
                    return true;
            }
            break;
        case Action.Damage:
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                if (sub.timer >= 0.15f)
                {
                    transform.GetChild((int)ChildIndex.Blood).gameObject.SetActive(true);
                    sub.state = 1;
                }
            }
            else
            {
                if (sub.timer >= 0.35f)
                {
                    transform.GetChild((int)ChildIndex.Blood).gameObject.SetActive(false);
                    return true;
                }
            }
            break;
        case Action.Block:
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos, sub.prevPos + 25f * dir, sub.timer / 0.15f));
                if (sub.timer >= 0.15f)
                {
                    sub.state = 1;
                    transform.GetChild((int)ChildIndex.Block).gameObject.SetActive(true);
                }
            }
            else
            {
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, Mathf.Lerp(sub.prevPos + 25f * dir, sub.prevPos, (sub.timer - 0.15f) / 0.2f));
                if (sub.timer >= 0.35f)
                {
                    transform.GetChild((int)ChildIndex.Block).gameObject.SetActive(false);
                    return true;
                }
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
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                if (sub.timer >= 0.15f)
                {
                    transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(true);
                    sub.state = 1;
                }
            }
            else
            {
                if (sub.timer >= 0.35f)
                {
                    transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(false);
                    return true;
                }
            }
            break;
        case Action.Mana:
            sub.timer += Time.deltaTime;
            if (sub.state == 0)
            {
                if (sub.timer >= 0.15f)
                {
                    transform.GetChild((int)ChildIndex.Heal).gameObject.SetActive(true);
                    sub.state = 1;
                }
            }
            else
            {
                if (sub.timer >= 0.35f)
                {
                    GameObject obj = transform.GetChild((int)ChildIndex.Heal).gameObject;
                    obj.SetActive(false);
                    obj.GetComponent<Image>().sprite = healSprite;
                    return true;
                }
            }
            break;
        case Action.PoisonDamage:
        case Action.Confused:
            sub.timer += Time.deltaTime;
            if (sub.timer >= 0.2f)
            {
                transform.GetChild((int)(sub.action == Action.PoisonDamage ? ChildIndex.Poison : ChildIndex.Confused)).gameObject.SetActive(false);
                return true;
            }
            break;
        case Action.Summon:
            sub.timer += Time.deltaTime;
            GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(sub.timer / 0.3f);
            if (sub.timer >= 0.3f)
                return true;
            break;
        case Action.Unsummon:
            sub.timer += Time.deltaTime;
            GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(1f - sub.timer / 0.3f);
            if (sub.timer >= 0.3f)
            {
                Destroy(gameObject);
                return true;
            }
            break;
        case Action.Move:
            {
                sub.timer += Time.deltaTime;
                RectTransform rectTransform = transform as RectTransform;
                rectTransform.anchoredPosition = Vector2.Lerp(sub.fromPos, sub.toPos, sub.timer / 0.3f);
                if (sub.timer >= 0.3f)
                    return true;
            }
            break;
        case Action.AddEffect:
            sub.timer -= Time.deltaTime;
            if (sub.timer <= 0)
            {
                AddEffectInternal(sub.effect);
                return true;
            }
            break;
        case Action.RemoveEffect:
            sub.timer -= Time.deltaTime;
            if (sub.timer <= 0)
            {
                if (sub.effect == Effect.Type.None)
                    RemoveAllEffectsInternal();
                else
                    RemoveEffectInternal(sub.effect);
                return true;
            }
            break;
        }

        return false;
    }

    public void Attack()
    {
        subactions.Add(new Subaction
        {
            action = Action.Attack,
            prevPos = (transform as RectTransform).anchoredPosition.y
        });
    }

    public void Cast()
    {
        subactions.Add(new Subaction
        {
            action = Action.Cast,
            prevPos = (transform as RectTransform).anchoredPosition.y
        });
    }

    public void Dodge()
    {
        subactions.Add(new Subaction
        {
            action = Action.Dodge,
            prevPos = (transform as RectTransform).anchoredPosition.y
        });
    }

    public void Damage()
    {
        subactions.Add(new Subaction { action = Action.Damage });
    }

    public void PoisonDamage()
    {
        subactions.Add(new Subaction { action = Action.PoisonDamage });
        transform.GetChild((int)ChildIndex.Poison).gameObject.SetActive(true);
    }

    public void Block()
    {
        subactions.Add(new Subaction
        {
            action = Action.Block,
            prevPos = (transform as RectTransform).anchoredPosition.y
        });
    }

    public void Escape()
    {
        subactions.Add(new Subaction { action = Action.Escape });
    }

    public void Approach()
    {
        subactions.Add(new Subaction { action = Action.Approach });
    }

    public void Heal()
    {
        subactions.Add(new Subaction { action = Action.Heal });
    }

    public void RestoreMana()
    {
        subactions.Add(new Subaction { action = Action.Mana });
        transform.GetChild((int)ChildIndex.Heal).GetComponent<Image>().sprite = manaSprite;
    }

    public void AddEffect(Effect.Type effect, float delay = 0)
    {
        if (delay == 0)
            AddEffectInternal(effect);
        else
            subactions.Add(new Subaction { action = Action.AddEffect, effect = effect, timer = delay });
    }

    private void AddEffectInternal(Effect.Type effect)
    {
        Transform effects = transform.GetChild((int)ChildIndex.Effects);
        foreach (Transform child in effects)
        {
            if (child.GetComponent<EffectIcon>().effect == effect)
            {
                // already added
                return;
            }
        }

        GameObject obj = Instantiate(effectPrefab, effects);
        obj.GetComponent<EffectIcon>().effect = effect;
        obj.GetComponent<Image>().sprite = effectSprites[(int)effect - 1];
    }

    public void RemoveEffect(Effect.Type effect, float delay = 0)
    {
        if (delay == 0)
            RemoveEffectInternal(effect);
        else
            subactions.Add(new Subaction { action = Action.RemoveEffect, effect = effect, timer = delay });
    }

    private void RemoveEffectInternal(Effect.Type effect)
    {
        Transform effects = transform.GetChild((int)ChildIndex.Effects);
        foreach (Transform child in effects)
        {
            if (child.GetComponent<EffectIcon>().effect == effect)
                Destroy(child.gameObject);
        }
    }

    public void RemoveAllEffects(float delay = 0)
    {
        if (delay == 0)
            RemoveAllEffectsInternal();
        else
            subactions.Add(new Subaction { action = Action.RemoveEffect, effect = Effect.Type.None, timer = delay });
    }

    private void RemoveAllEffectsInternal()
    {
        Transform effects = transform.GetChild((int)ChildIndex.Effects);
        foreach (Transform child in effects)
            Destroy(child.gameObject);
    }

    public void Confused()
    {
        subactions.Add(new Subaction { action = Action.Confused });
        transform.GetChild((int)ChildIndex.Confused).gameObject.SetActive(true);
    }

    public void Summon()
    {
        subactions.Add(new Subaction { action = Action.Summon });
        transform.GetComponent<CanvasGroup>().alpha = 0;
    }

    public void Unsummon()
    {
        subactions.Add(new Subaction { action = Action.Unsummon });
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
        subactions.Add(new Subaction
        {
            action = Action.Move,
            fromPos = position,
            toPos = pos
        });
    }
}
