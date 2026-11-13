using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Combat : GameDialog
{
    public enum Result
    {
        None,
        WinWait,
        Win,
        DefeatWait,
        Defeat,
        Escape
    }

    private enum Action
    {
        None,
        Attack,
        Escape,
        Heal,
        Mana,
        Spell
    }

    private class Unit
    {
        public Enemy enemy;
        public CharacterCard card;
        public int hp, cooldown, cooldown2;
        public bool canBlock, summoned;

        public float hpp => ((float)hp) / enemy.hp;
    }

    private class DelayedText
    {
        public string text;
        public float delay;
    }

    public const int MaxEnemies = 5;

    public GameObject characterCardPrefab, arrowPrefab, buttonPrefab;

    private const float alliesY = -125f;
    private const float enemiesY = 200f;
    private const float spaceBetweenCards = 180f;

    private readonly List<string> textParts = new();
    private readonly List<DelayedText> delayedTexts = new();
    private readonly List<Unit> enemies = new();
    private readonly List<Hero> hitHeroes = new();
    private List<Hero> heroes;
    private List<Enemy> enemyList;
    private List<object> order = new();
    private List<(Action, float)> actionButtonYs;
    private TMP_Text textLog;
    private GameObject arrow;
    private Unit playerTarget;
    private Result result;
    private Action playerAction;
    private float timer;
    private int combatIndex, attacks, effectTick, maxAlliesCount, maxEnemiesCount;

    public override bool Autoclose => false;

    public void Init()
    {
        textLog = transform.GetChild(0).GetComponent<TMP_Text>();
        arrow = transform.Find("Arrow").gameObject;
    }

    public void Init(List<Enemy> enemyList, string startAction, bool restCombat)
    {
        heroes = game.team.heroes;
        this.enemyList = enemyList;

        Transform container = transform.GetChild(1);
        foreach (Transform child in container)
            Destroy(child.gameObject);

        order.Clear();
        enemies.Clear();
        textParts.Clear();
        delayedTexts.Clear();

        int index = 0;
        bool multiRow = heroes.Any(x => x.BackRow != heroes[0].BackRow);
        maxAlliesCount = heroes.Count;
        foreach (Hero hero in heroes)
        {
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(hero.name, hero.hpp, hero.mpp, false, hero.GetPortrait());
            RectTransform transform = card.GetComponent<RectTransform>();
            Vector2 pos = GetCardPosition(index, true);
            if (multiRow && !hero.BackRow)
                pos.y += 25;
            transform.anchoredPosition = pos;
            order.Add(hero);
            card.owner = hero;
            card.slot = index;
            hero.InitCombat();
            hero.card = card;
            ++index;
        }

        bool summoned = enemyList[0].name == "spider queen";
        multiRow = enemyList.Any(x => x.blocks != enemyList[0].blocks);
        maxEnemiesCount = enemyList.Count;
        for (int i = 0; i < enemyList.Count; ++i)
        {
            Enemy enemy = enemyList[i];
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(enemy.name, 1f, -1f, true, Resources.Load<Sprite>(enemy.Portrait));
            RectTransform transform = card.GetComponent<RectTransform>();
            Vector2 pos = GetCardPosition(i, false);
            if (multiRow && enemy.blocks)
                pos.y -= 25;
            transform.anchoredPosition = pos;
            Unit unit = new() { enemy = enemy, card = card, hp = enemy.hp, canBlock = enemy.blocks, summoned = summoned && enemy.name == "giant spider" };
            enemies.Add(unit);
            order.Add(unit);
            card.owner = unit;
            card.slot = i;
        }

        order = order.Select(x =>
        {
            int dex;
            if (x is Hero hero)
                dex = hero.dex;
            else
                dex = (x as Unit).enemy.dex;
            dex += Utility.Rand % 5;
            return (x, dex);
        }).OrderByDescending(x => x.dex).Select(x => x.x).ToList();
        combatIndex = 0;
        result = 0;
        timer = 0.5f;
        arrow.SetActive(false);
        playerAction = Action.None;
        effectTick = 0;
        playerTarget = null;

        // create buttons
        Transform buttons = transform.Find("Buttons");
        foreach (Transform child in buttons)
            Destroy(child.gameObject);

        List<RectTransform> buttonsRect = new();
        actionButtonYs = new();

        GameObject obj = Instantiate(buttonPrefab, buttons);
        obj.GetComponentInChildren<TMP_Text>().text = "Healing p.";
        obj.GetComponent<Button>().onClick.AddListener(() => PickAction(Action.Heal));
        buttonsRect.Add(obj.transform as RectTransform);
        actionButtonYs.Add((Action.Heal, 0));

        if (player.mpMax > 0)
        {
            obj = Instantiate(buttonPrefab, buttons);
            obj.GetComponentInChildren<TMP_Text>().text = "Mana p.";
            obj.GetComponent<Button>().onClick.AddListener(() => PickAction(Action.Mana));
            buttonsRect.Add(obj.transform as RectTransform);
            actionButtonYs.Add((Action.Mana, 0));
        }

        if (player.spells != null)
        {
            index = 0;
            foreach (Spell spell in player.spells)
            {
                Action action = Action.Spell + index;
                obj = Instantiate(buttonPrefab, buttons);
                obj.GetComponentInChildren<TMP_Text>().text = spell.name.ToUpper1();
                obj.GetComponent<Button>().onClick.AddListener(() => PickAction(action));
                buttonsRect.Add(obj.transform as RectTransform);
                actionButtonYs.Add((action, 0));
                if (spell.requireShield && player.shield == null)
                    obj.GetComponent<Button>().interactable = false;
                ++index;
            }
        }

        obj = Instantiate(buttonPrefab, buttons);
        obj.GetComponentInChildren<TMP_Text>().text = "Escape";
        obj.GetComponent<Button>().onClick.AddListener(() => PickAction(Action.Escape));
        buttonsRect.Add(obj.transform as RectTransform);
        actionButtonYs.Add((Action.Escape, 0));

        // position buttons
        const float buttonsGap = 35f;
        float totalWidth = (buttonsRect.Count - 1) * buttonsGap;
        float y = totalWidth * 0.5f;
        for (int i = 0; i < buttonsRect.Count; ++i)
        {
            buttonsRect[i].anchoredPosition = new(-540, y);
            actionButtonYs[i] = (actionButtonYs[i].Item1, y);
            y -= buttonsGap;
        }

        transform.parent.Find("Buttons").gameObject.SetActive(false);
        text.Clear();
        string str;
        if (restCombat)
            str = startAction + $" Suddenly <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> {Utility.S("attack", enemyList.Count == 1)} you.";
        else
            str = $"You {startAction} the {game.world.CurrentTile.Name} and <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> {Utility.S("attack", enemyList.Count == 1)} you.";
        game.AddText(str);
        AppendText(str);
    }

    public override void UpdateDialog()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
            Time.timeScale = 0.25f;
        if (Input.GetKeyUp(KeyCode.Space))
            Time.timeScale = 1f;
#endif

        if (Input.GetKeyDown(KeyCode.H))
            PickAction(Action.Heal);
        if (player.mpMax > 0 && Input.GetKeyDown(KeyCode.M))
            PickAction(Action.Mana);
        if (Input.GetKeyDown(KeyCode.E))
            PickAction(Action.Escape);

        delayedTexts.RemoveAll(delayedText =>
        {
            delayedText.delay -= Time.deltaTime;
            if (delayedText.delay <= 0)
            {
                AppendText(delayedText.text);
                return true;
            }
            else
                return false;
        });

        timer -= Time.deltaTime;
        if (timer > 0)
            return;

        if (result != Result.None)
        {
            switch (result)
            {
            case Result.DefeatWait:
                AppendText("You lost!");
                result = Result.Defeat;
                timer = 1f;
                foreach (Unit enemy in enemies)
                {
                    if (enemy.hp > 1)
                        enemy.card.Approach();
                }
                break;
            case Result.WinWait:
                AppendText("You win!");
                result = Result.Win;
                timer = 1f;
                foreach (Hero hero2 in heroes)
                    hero2.card.Approach();
                break;
            default:
                transform.parent.Find("Buttons").gameObject.SetActive(true);
                game.PostCombat(result, enemyList);
                break;
            }
            return;
        }

        object obj = order[combatIndex];
        bool nextUnit;
        if (obj is Hero hero)
            nextUnit = HeroAction(hero);
        else
            nextUnit = EnemyAction(obj as Unit);

        if (nextUnit || combatIndex == order.Count)
        {
            ++combatIndex;
            if (combatIndex >= order.Count)
                combatIndex = 0;
            effectTick = 0;
        }
    }

    private bool HeroAction(Hero hero)
    {
        Spell castSpell = null;

        if (effectTick == 0)
        {
            effectTick = 1;
            if (!hero.BackRow)
                hero.canBlock = true;
            if (hero.potionTimer > 0)
            {
                --hero.potionTimer;
                if (hero == player)
                    transform.Find("Buttons").GetChild(0).GetComponentInChildren<TMP_Text>().text = $"Healing p. ({hero.potionTimer})";
            }
            hero.effects.RemoveAll(e =>
            {
                if (e.timer > 0 && e.type != Effect.Type.Confused)
                {
                    --e.timer;
                    if (e.timer == 0)
                    {
                        hero.card.RemoveEffect(e.type);
                        return true;
                    }
                }
                return false;
            });
        }

        // escape
        if (playerAction == Action.Escape && hero == player)
        {
            result = Result.Escape;
            timer = 1f;
            foreach (Hero hero2 in heroes)
                hero2.card.Escape();
            PickAction(Action.None);
            return false;
        }

        if (hero.hp > 0)
        {
            Effect effect;

            // poison damage
            if (effectTick < 2 && (effect = hero.GetEffect(Effect.Type.Poison)) != null)
            {
                timer = 0.3f;
                hero.hp -= effect.value;
                hero.card.SetHp(hero.hpp);
                hero.card.PoisonDamage();
                if (hero.hp <= 0)
                {
                    AppendText($"{hero.NameYou} {hero.S("take")} {effect.value} poison damage and {hero.isAre} defeated.");
                    hero.effects.Clear();
                    hero.card.RemoveAllEffects();
                    if (heroes.All(x => x.hp <= 0))
                    {
                        result = Result.DefeatWait;
                        timer = 0.5f;
                    }
                }
                else
                    AppendText($"{hero.NameYou} {hero.S("take")} {effect.value} poison damage.");
                effectTick = 2;
                return false;
            }

            // confused
            if (effectTick < 4 && (effect = hero.GetEffect(Effect.Type.Confused)) != null)
            {
                // show confused image over player
                if (effectTick < 3)
                {
                    timer = 0.3f;
                    hero.card.Confused();
                    effectTick = 3;
                    return false;
                }

                // chance to attack allly or do nothing
                --effect.timer;
                if (effect.timer == 0)
                    player.effects.Remove(effect);

                if (Utility.Rand % 2 == 0)
                {
                    // attack ally
                    Hero targetHero = heroes.RandomItem(x => x.hp > 0);
                    if (targetHero == hero)
                    {
                        AppendText($"{hero.NameYou} {hero.S("don't", "doesn't")} know what to do.");
                        if (effect.timer == 0)
                            hero.card.RemoveEffect(Effect.Type.Confused, 0.1f);
                        timer = 0.1f;
                        return true;
                    }

                    if (AttackChance(hero.dex, targetHero.dex))
                    {
                        int dmg = Mathf.Max(hero.Attack - targetHero.Defense, 0);
                        targetHero.card.Damage();
                        targetHero.card.SetHp(targetHero.hpp, 0.15f);
                        if (targetHero.hp <= 0)
                        {
                            targetHero.effects.Clear();
                            targetHero.card.RemoveAllEffects(0.15f);
                            AppendText($"{hero.NameYou} {hero.S("hit")} {targetHero.name} for {dmg} damage and {hero.S("defeat")} {targetHero.him}.", 0.15f);
                        }
                        else
                            AppendText($"{hero.NameYou} {hero.S("hit")} {targetHero.name} for {dmg} damage.", 0.15f);
                    }
                    else
                    {
                        AppendText($"{hero.NameYou} {hero.S("miss", "misses")} {targetHero.nameYou}.", 0.15f);
                        targetHero.card.Dodge();
                    }

                    if (hero.weapon != null && hero.weapon.subtype == Item.Subtype.Bow)
                    {
                        Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                        arrow.Shoot(hero.card.position, targetHero.card.position);
                    }
                    else
                        hero.card.Attack();

                    if (effect.timer == 0)
                        hero.card.RemoveEffect(Effect.Type.Confused, 0.5f);
                    timer = 0.5f;
                    return true;
                }

                if (effect.timer == 0)
                    hero.card.RemoveEffect(Effect.Type.Confused);
                effectTick = 4;
            }

            // player selected action
            if (hero == player && playerAction != Action.None)
            {
                ItemSlot potion;
                switch (playerAction)
                {
                case Action.Heal:
                    if (hero.potionTimer == 0 && (potion = hero.FindHealingPotion()) != null)
                    {
                        HeroUseHealingPotion(hero, potion);
                        PickAction(Action.None);
                        return true;
                    }
                    break;
                case Action.Mana:
                    if ((potion = hero.FindManaPotion()) != null)
                    {
                        HeroUseManaPotion(hero, potion);
                        PickAction(Action.None);
                        return true;
                    }
                    break;
                default:
                    {
                        int index = playerAction - Action.Spell;
                        Spell spell = hero.spells[index];
                        if (spell.mana <= hero.mp)
                            castSpell = spell;
                    }
                    break;
                }
            }

            // ai auto action
            Unit target = null;
            Hero heroTarget = null;
            if (castSpell == null)
            {
                (Action action, object newTarget) = HeroThink(hero);
                switch (action)
                {
                case Action.Heal:
                    HeroUseHealingPotion(hero);
                    return true;
                case Action.Mana:
                    HeroUseManaPotion(hero);
                    return true;
                case Action.Attack:
                    target = (Unit)newTarget;
                    break;
                default:
                    {
                        int spellIndex = action - Action.Spell;
                        castSpell = hero.spells[spellIndex];
                        if (castSpell.type == Spell.Type.Heal)
                            heroTarget = (Hero)newTarget;
                        else
                            target = (Unit)newTarget;
                    }
                    break;
                }
            }
            else
            {
                if (castSpell.type == Spell.Type.Heal)
                    heroTarget = heroes.OrderBy(x => x.hpp).First();
                else if (!castSpell.aoe && castSpell.type != Spell.Type.Buff)
                {
                    if (playerTarget != null && playerTarget.hp > 0)
                        target = playerTarget;
                    else
                        target = enemies.RandomItem(x => x.hp > 0 && x.enemy.IsSusceptible(castSpell.damageType)) ?? enemies.RandomItem(x => x.hp > 0);
                }
            }

            timer = 0.5f;
            bool isBlocking = false;
            if (target != null && !target.enemy.blocks)
            {
                // front row enemies can block attack once per round
                Unit blockingEnemy = enemies.RandomItem(x => x.hp > 0 && x.canBlock);
                if (blockingEnemy != null)
                {
                    target = blockingEnemy;
                    target.canBlock = false;
                    isBlocking = true;
                }
            }

            if (castSpell != null)
            {
                hero.mp -= castSpell.mana;
                hero.card.SetMp(hero.mpp, 0.15f);
                if (hero is Player && playerAction >= Action.Spell)
                {
                    arrow.SetActive(false);
                    playerAction = Action.None;
                }

                if (castSpell.type == Spell.Type.Heal)
                {
                    int heal = 100 + hero.level * 10;
                    int missingHp = heroTarget.hpMax - heroTarget.hp;
                    heal = Mathf.Min(heal, missingHp);
                    heroTarget.hp += heal;
                    heroTarget.RemoveEffect(Effect.Type.Poison);

                    AppendText($"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {(hero == heroTarget ? hero.himself : heroTarget.name)} and {hero.S("heal")} {heal} damage.", 0.15f);
                    hero.card.Cast();
                    heroTarget.card.Heal();
                    heroTarget.card.RemoveEffect(Effect.Type.Poison, 0.15f);
                    heroTarget.card.SetHp(heroTarget.hpp, 0.15f);

                    return true;
                }
                else if (castSpell.type == Spell.Type.Buff)
                {
                    Effect existingEffect = heroTarget.GetEffect(castSpell.effectType);
                    if (existingEffect != null)
                    {
                        if (castSpell.duration > existingEffect.timer)
                            existingEffect.timer = castSpell.duration;
                    }
                    else
                        hero.effects.Add(new Effect { type = castSpell.effectType, timer = castSpell.duration });

                    AppendText($"{hero.NameYou} {hero.S("guard")} against incoming attacks.");
                    hero.card.Cast();
                    hero.card.BlockImage();
                    hero.card.AddEffect(Effect.Type.Guard);

                    return true;
                }
                else if (castSpell.aoe)
                {
                    hero.card.Cast();
                    foreach (Unit unit in enemies.Where(x => x.hp > 0))
                    {
                        if (AttackChance(hero.dex, unit.enemy.dex))
                        {
                            int dmg = hero.Attack - unit.enemy.def / 2;
                            if (unit.enemy.IsSusceptible(castSpell.damageType))
                                dmg *= 2;
                            dmg = Mathf.Max(dmg, 0);
                            unit.hp -= dmg;
                            unit.card.Damage();
                            unit.card.SetHp(unit.hpp, 0.15f);
                            if (unit.hp <= 0)
                            {
                                AppendText($"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {unit.enemy.name} for {dmg} damage and {hero.S("defeat")} {unit.enemy.him}.", 0.15f);
                                if (enemies.All(x => x.hp <= 0))
                                    result = Result.WinWait;
                            }
                            else
                                AppendText($"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {unit.enemy.name} for {dmg} damage.", 0.15f);
                        }
                        else
                        {
                            AppendText($"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {unit.enemy.name} but {hero.S("miss", "misses")}.", 0.15f);
                            unit.card.Dodge();
                        }

                        Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                        arrow.Shoot(hero.card.position, unit.card.position);
                        arrow.SetDamageType(castSpell.damageType);
                    }
                    return true;
                }
            }

            if (AttackChance(hero.dex, target.enemy.dex))
            {
                int dmg = hero.Attack;
                if (castSpell != null && castSpell.damageType != DamageType.Normal)
                {
                    dmg -= target.enemy.def / 2;
                    if (target.enemy.IsSusceptible(castSpell.damageType))
                        dmg *= 2;
                }
                else
                    dmg -= target.enemy.def;
                dmg = Mathf.Max(dmg, 0);
                target.hp -= dmg;
                if (isBlocking)
                    target.card.Block();
                target.card.Damage();
                target.card.SetHp(target.hpp, 0.15f);

                if (target.hp <= 0)
                {
                    AppendText(castSpell != null
                        ? $"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {target.enemy.name} for {dmg} damage and {hero.S("defeat")} {target.enemy.him}."
                        : $"{hero.NameYou} {hero.S("hit")} {target.enemy.name} for {dmg} damage and {hero.S("defeat")} {target.enemy.him}.", 0.15f);
                    if (enemies.All(x => x.hp <= 0))
                        result = Result.WinWait;
                }
                else
                {
                    AppendText(castSpell != null
                        ? $"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {target.enemy.name} for {dmg} damage."
                        : $"{hero.NameYou} {hero.S("hit")} {target.enemy.name} for {dmg} damage.", 0.15f);
                }
            }
            else
            {
                AppendText(castSpell != null
                    ? $"{hero.NameYou} {hero.S("cast")} {castSpell.name} at {target.enemy.name} but {hero.S("miss", "misses")}."
                    : $"{hero.NameYou} {hero.S("miss", "misses")} {target.enemy.name}.", 0.15f);
                if (isBlocking)
                    target.card.Block();
                else
                    target.card.Dodge();
            }

            if (castSpell != null)
            {
                hero.card.Cast();
                Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                arrow.Shoot(hero.card.position, target.card.position);
                arrow.SetDamageType(castSpell.damageType);
            }
            else if (hero.weapon != null && hero.weapon.subtype == Item.Subtype.Bow)
            {
                Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                arrow.Shoot(hero.card.position, target.card.position);
            }
            else
                hero.card.Attack();
        }
        else if (hero.potionTimer == 0)
        {
            // defeated heroes automatically drink potion
            ItemSlot potion;
            if ((potion = hero.FindHealingPotion()) != null && hero.hp + potion.item.power > 0)
            {
                HeroUseHealingPotion(hero, potion);
                if (hero == player && playerAction == Action.Heal)
                    PickAction(Action.None);
            }
        }

        return true;
    }

    /*
    AI priority
    1. heal yourself
        1a. healing spell if have mana
        1b. healing potion
        1c. healing spell after drinking mana
    2. heal others
        2a. now
        2b. after drinking mana
    3. if spell is better then normal attacks
        3a. drink mana if required
        3b. cast spell
    4. normal attack
    */
    private (Action action, object target) HeroThink(Hero hero)
    {
        Action action = Action.None;
        object target = null;
        int actionPriority = 10;

        void AddWithPriority(Action newAction, int newPriority, object newTarget)
        {
            if (newPriority > actionPriority)
            {
                action = newAction;
                actionPriority = newPriority;
                target = newTarget;
            }
        }

        if (HeroShouldUsePotion(hero))
            AddWithPriority(Action.Heal, 100, null);

        if (hero.spells != null)
        {
            int possibleMana = hero.mp;
            if (hero.mp != hero.mpMax)
            {
                ItemSlot potion = hero.FindManaPotion();
                if (potion != null)
                    possibleMana = Mathf.Min(hero.mp + potion.item.value, hero.mpMax);
            }

            for (int i = 0; i < hero.spells.Count; ++i)
            {
                Spell spell = hero.spells[i];
                if (spell.mana > possibleMana || (spell.requireShield && hero.shield == null))
                    continue;

                if (spell.type == Spell.Type.Damage)
                {
                    if (spell.aoe)
                    {
                        int prio = 0;
                        foreach (Unit unit in enemies.Where(x => x.hp > 0))
                        {
                            if (unit.enemy.IsSusceptible(spell.damageType))
                                prio += 20;
                            else
                                prio += 10;
                        }

                        if (hero.hp >= spell.mana)
                            AddWithPriority(Action.Spell + i, prio, null);
                        else
                            AddWithPriority(Action.Mana, prio * 3 / 4, null);
                    }
                    else
                    {
                        if (enemies.Any(x => x.hp > 0 && x.enemy.IsSusceptible(spell.damageType)))
                        {
                            if (hero.mp >= spell.mana)
                            {
                                Unit newTarget;
                                if (playerTarget != null && playerTarget.hp > 0 && playerTarget.enemy.holyDmgSus && (hero == player || Utility.Rand % 2 == 0))
                                    newTarget = playerTarget;
                                else
                                    newTarget = enemies.RandomItem(x => x.hp > 0 && x.enemy.holyDmgSus);
                                AddWithPriority(Action.Spell + i, 20, newTarget);
                            }
                            else
                                AddWithPriority(Action.Mana, 15, null);
                        }
                    }
                }
                else if (spell.type == Spell.Type.Heal)
                {
                    if (hero.hpp < 0.5f)
                    {
                        // heal yourself
                        if (hero.mp >= spell.mana)
                            AddWithPriority(Action.Spell + i, 150, hero);
                        else
                            AddWithPriority(Action.Mana, 75, null);
                    }
                    else
                    {
                        // heal ally
                        Hero targetAlly = heroes.Where(x => x.hpp < 0.5f).OrderBy(x => x.hpp).FirstOrDefault();
                        if (targetAlly != null)
                        {
                            if (hero.mp >= spell.mana)
                                AddWithPriority(Action.Spell + i, 50, targetAlly);
                            else
                                AddWithPriority(Action.Mana, 25, null);
                        }
                    }
                }
                else if (spell.type == Spell.Type.Buff)
                {
                    if (!hero.HaveEffect(spell.effectType))
                    {
                        // when to use??
                    }
                }
            }
        }

        // attack if nothing better to do
        // also sometimes attack even if there is better option
        if (actionPriority <= 10 || (actionPriority < 100 && Utility.Rand % (actionPriority + 10) < 10))
        {
            action = Action.Attack;
            if (playerTarget != null && playerTarget.hp > 0 && (hero == player || Utility.Rand % 2 == 0))
                target = playerTarget;
            else
                target = enemies.RandomItem(x => x.hp > 0);
        }

        return (action, target);
    }

    private bool HeroShouldUsePotion(Hero hero)
    {
        if (hero.hpp > 0.5f
            || hero.potionTimer != 0
            || heroes.Count(x => x.hp > 0) > 1) // if there are other heroes attack and heal when defeated
            return false;

        ItemSlot potion = hero.FindHealingPotion();
        if (potion == null)
            return false;

        // if can defeat enemy with one hit, do it
        if (enemies.Count(x => x.hp > 0) == 1)
        {
            Unit target = enemies.First(x => x.hp > 0);
            if (CalculateAvgDamage(hero, target) >= target.hp)
                return false;
        }

        // if enemies can defeat hero in one turn, heal
        // unless healing doesn't matter
        int enemiesDmg = enemies.Where(x => x.hp > 0).Sum(x => CalculateAvgDamage(x, hero));
        if (enemiesDmg >= hero.hp && Mathf.Min(hero.hp + potion.item.power, hero.hpMax) > enemiesDmg)
            return true;

        return false;
    }

    private void HeroUseHealingPotion(Hero hero, ItemSlot potion = null)
    {
        potion ??= hero.FindHealingPotion();
        int prevHp = hero.hp;
        hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
        hero.RemoveItem(potion);
        hero.potionsUsed++;
        hero.potionTimer = hero.potionsUsed + 1;
        hero.RemoveEffect(Effect.Type.Poison);
        AppendText($"{hero.NameYou} {hero.S("use")} {potion.item.name} and {hero.S("get")} healed for {hero.hp - prevHp}.", 0.15f);
        hero.card.Heal();
        hero.card.SetHp(hero.hpp, 0.15f);
        hero.card.RemoveEffect(Effect.Type.Poison, 0.15f);
        if (hero == player)
            transform.Find("Buttons").GetChild(0).GetComponentInChildren<TMP_Text>().text = $"Healing p. ({hero.potionTimer})";
        timer = 0.5f;
    }

    private void HeroUseManaPotion(Hero hero, ItemSlot potion = null)
    {
        potion ??= hero.FindManaPotion();
        int prevMp = hero.mp;
        hero.mp = Mathf.Min(hero.mp + potion.item.power, hero.mpMax);
        hero.RemoveItem(potion);
        AppendText($"{hero.NameYou} {hero.S("use")} {potion.item.name} and {hero.S("restore")} {hero.mp - prevMp} mana.", 0.15f);
        hero.card.RestoreMana();
        hero.card.SetMp(hero.mpp, 0.15f);
        timer = 0.5f;
    }

    private bool EnemyAction(Unit me)
    {
        if (me.hp <= 0)
        {
            if (me.summoned)
            {
                AppendText(me.enemy.name == "mummy" ? $"{me.enemy.name.ToUpper1()} crumbles into dust." : $"{me.enemy.name.ToUpper1()} skitters away.", 0.15f);
                if (me == playerTarget)
                {
                    me.card.SetColor(Color.white);
                    playerTarget = null;
                }
                me.card.Unsummon();
                order.Remove(me);
                enemies.Remove(me);
                timer = 0.5f;
                return false;
            }
            return true;
        }

        timer = 0.5f;
        if (effectTick == 0)
        {
            effectTick = 1;
            attacks = me.enemy.attacks.Random();
            hitHeroes.Clear();
            me.canBlock = me.enemy.blocks;
            if (me.cooldown > 0)
                --me.cooldown;
            if (me.cooldown2 > 0)
                --me.cooldown2;
        }

        if ((me.enemy.summonMummy || me.enemy.summonSpider) && me.cooldown2 == 0 && enemies.Count < MaxEnemies && Utility.Rand % 3 != 0)
        {
            EnemySummon(me);
            return true;
        }

        if (me.enemy.firebreath && me.cooldown == 0 && Utility.Rand % 2 == 0)
        {
            EnemyFirebreath(me);
            return true;
        }

        string spellName = null;
        if ((me.enemy.fireball || me.enemy.darkbolt) && me.cooldown == 0 && Utility.Rand % 2 == 0)
        {
            spellName = me.enemy.fireball ? "fireball" : "darkbolt";
            me.cooldown = 2;
        }
        Hero hero = heroes.RandomItem(x => x.hp > 0 && !hitHeroes.Contains(x));
        bool isBlocking = false;
        if (hero == null)
            return true;
        else if (hero.BackRow)
        {
            // front row heroes can block attack once per round
            Hero blockingHero = heroes.RandomItem(x => x.hp > 0 && x.canBlock && x.shield != null);
            if (blockingHero != null)
            {
                hero = blockingHero;
                hero.canBlock = false;
                isBlocking = true;
            }
        }

        if (AttackChance(me.enemy.dex, hero.dex))
        {
            hitHeroes.Add(hero);

            int def = hero.Defense;
            if (spellName != null)
                def /= 2;
            int dmg = Mathf.Max(me.enemy.attack - def, 0);
            hero.hp -= dmg;

            if (isBlocking)
                hero.card.Block();
            hero.card.Damage();
            hero.card.SetHp(hero.hpp, 0.15f);

            if (hero.hp <= 0)
            {
                hero.effects.Clear();
                hero.card.RemoveAllEffects(0.15f);
                AppendText(spellName != null
                    ? $"{me.enemy.name.ToUpper1()} shoots {spellName} at {hero.nameYou} for {dmg} damage and defeats {hero.him}."
                    : $"{me.enemy.name.ToUpper1()} hits {hero.nameYou} for {dmg} damage and defeats {hero.him}.", 0.15f);
                if (heroes.All(x => x.hp <= 0))
                    result = Result.DefeatWait;
            }
            else
            {
                string str = spellName != null ? $"{me.enemy.name.ToUpper1()} shoots {spellName} at {hero.nameYou} for {dmg} damage." : $"{me.enemy.name.ToUpper1()} hits {hero.nameYou} for {dmg} damage.";
                if (me.enemy.attackType == Enemy.AttackType.Poison && dmg > 0)
                {
                    int poison = dmg / 5;
                    if (poison == 0)
                        poison = 1;
                    Effect effect = hero.GetEffect(Effect.Type.Poison);
                    if (effect == null)
                    {
                        str += $" {hero.NameYou} {hero.isAre} poisoned.";
                        hero.card.AddEffect(Effect.Type.Poison, 0.15f);
                        hero.effects.Add(new Effect { type = Effect.Type.Poison, value = poison });
                    }
                    else
                        effect.value += poison;
                }
                else if (me.enemy.attackType == Enemy.AttackType.Confuse)
                {
                    Effect effect = hero.GetEffect(Effect.Type.Confused);
                    if (effect == null)
                    {
                        str += $" {hero.NameYou} {hero.isAre} confused.";
                        hero.effects.Add(new Effect { type = Effect.Type.Confused, timer = 2 });
                        hero.card.AddEffect(Effect.Type.Confused, 0.15f);
                    }
                    else
                        effect.timer = 2;
                }

                AppendText(str, 0.15f);
            }

            if (me.enemy.attackType == Enemy.AttackType.LifeSteal && me.hp < me.enemy.hp && dmg >= 2)
            {
                int heal = dmg / 2;
                int prev = me.hp;
                me.hp = Mathf.Min(me.hp + heal, me.enemy.hp);
                heal = me.hp - prev;
                AppendText($"{me.enemy.name.ToUpper1()} is healed for {heal}.", 0.15f);
                me.card.Heal();
                me.card.SetHp(me.hpp, 0.15f);
            }
        }
        else
        {
            AppendText(spellName != null
                ? $"{me.enemy.name.ToUpper1()} shoots {spellName} at {hero.nameYou} but misses."
                : $"{me.enemy.name.ToUpper1()} misses {hero.nameYou}.", 0.15f);
            if (isBlocking)
                hero.card.Block();
            else
                hero.card.Dodge();
        }

        if (spellName != null)
        {
            me.card.Cast();
            Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
            arrow.Shoot(me.card.position, hero.card.position);
            if (spellName == "fireball")
                arrow.SetFire();
            else
                arrow.SetDark();
        }
        else if (me.enemy.attackType == Enemy.AttackType.Ranged)
        {
            Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
            arrow.Shoot(me.card.position, hero.card.position);
        }
        else
            me.card.Attack();

        --attacks;
        return attacks <= 0;
    }

    private void EnemyFirebreath(Unit me)
    {
        me.card.Cast();
        me.cooldown = 2;
        foreach (Hero hero in heroes.Where(x => x.hp > 0))
        {
            if (AttackChance(me.enemy.dex, hero.dex))
            {
                int def = hero.Defense / 2;
                int dmg = Mathf.Max(me.enemy.attack - def, 0);
                hero.hp -= dmg;
                hero.card.Damage();
                hero.card.SetHp(hero.hpp, 0.15f);
                if (hero.hp <= 0)
                {
                    hero.effects.Clear();
                    hero.card.RemoveAllEffects(0.15f);
                    AppendText($"{me.enemy.name.ToUpper1()} breaths fire at {hero.nameYou} for {dmg} damage and defeats {hero.him}.", 0.15f);
                    if (heroes.All(x => x.hp <= 0))
                        result = Result.DefeatWait;
                }
                else
                    AppendText($"{me.enemy.name.ToUpper1()} breaths fire at {hero.nameYou} for {dmg} damage.", 0.15f);
            }
            else
            {
                AppendText($"{me.enemy.name.ToUpper1()} breaths fire at {hero.nameYou} but misses.", 0.15f);
                hero.card.Dodge();
            }

            Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
            arrow.Shoot(me.card.position, hero.card.position);
            arrow.SetFire();
        }
    }

    private void EnemySummon(Unit me)
    {
        Enemy enemy = Enemy.Get(me.enemy.summonMummy ? "mummy" : "giant spider");
        CharacterCard card = Instantiate(characterCardPrefab, transform.GetChild(1)).GetComponent<CharacterCard>();
        card.Init(enemy.name, 1f, -1f, true, Resources.Load<Sprite>(enemy.Portrait));
        RectTransform rectTransform = card.GetComponent<RectTransform>();
        int emptySlot = -1;
        if (enemies.Count < maxEnemiesCount)
        {
            for (int i = 0; i < maxEnemiesCount; ++i)
            {
                if (!enemies.Any(x => x.card.slot == i))
                    emptySlot = i;
            }
        }
        else
        {
            ++maxEnemiesCount;
            for (int i = 0; i < maxEnemiesCount - 1; ++i)
                enemies[i].card.Move(GetCardPosition(i, false));
            emptySlot = maxEnemiesCount - 1;
        }
        Vector2 pos = GetCardPosition(emptySlot, false);
        if (enemy.blocks && enemies.Any(x => x.enemy.blocks != enemy.blocks))
            pos.y -= 25;
        rectTransform.anchoredPosition = pos;
        Unit summoned = new() { enemy = enemy, card = card, hp = enemy.hp, canBlock = enemy.blocks, summoned = true };
        card.owner = summoned;
        int myOrder = order.IndexOf(me);
        order.Insert(myOrder + 1, summoned);
        card.Summon();
        enemies.Add(summoned);
        AppendText($"{me.enemy.name.ToUpper1()} summons {enemy.name}.", 0.15f);
        timer = 0.5f;
        me.cooldown2 = 3;
        me.card.Cast();
    }

    public static bool AttackChance(int myDex, int targetDex)
    {
        int chance = 75 + (myDex - targetDex) * 5;
        if (chance < 10)
            chance = 10;
        return Utility.Random(0, 100) < chance;
    }

    private float AttackChanceFloat(int myDex, int targetDex)
    {
        int chance = 75 + (myDex - targetDex) * 5;
        chance = Mathf.Clamp(chance, 10, 100);
        return ((float)chance) / 100;
    }

    private int CalculateAvgDamage(Hero hero, Unit target)
    {
        float hitRatio = AttackChanceFloat(hero.dex, target.enemy.dex);
        int dmg = Mathf.Min(hero.Attack - target.enemy.def, 0);
        return Mathf.RoundToInt(hitRatio * dmg);
    }

    private int CalculateAvgDamage(Unit me, Hero hero)
    {
        float hitRatio = AttackChanceFloat(me.enemy.dex, hero.dex);
        int def = hero.Defense;
        if ((me.enemy.fireball || me.enemy.firebreath || me.enemy.darkbolt) && me.cooldown <= 1)
            def /= 2;
        int dmg = Mathf.Min(me.enemy.attack - def, 0);
        return Mathf.RoundToInt(hitRatio * dmg);
    }

    private void AppendText(string str, float delay = 0)
    {
        if (delay > 0)
            delayedTexts.Add(new() { text = str, delay = delay });
        else
        {
            textParts.Add(str);
            if (textParts.Count > 5)
                textParts.RemoveAt(0);
            textLog.text = string.Join('\n', textParts);
            game.UpdateText();
        }
    }

    private void PickAction(Action newAction)
    {
        if (playerAction == newAction || newAction == Action.None)
        {
            playerAction = Action.None;
            arrow.SetActive(false);
        }
        else
        {
            playerAction = newAction;
            RectTransform rectTransform = arrow.transform as RectTransform;
            rectTransform.anchoredPosition = new(rectTransform.anchoredPosition.x, actionButtonYs.First(x => x.Item1 == newAction).Item2);
            arrow.SetActive(true);
        }
    }

    public void SelectCard(object owner)
    {
        if (owner is Unit unit)
        {
            playerTarget?.card.SetColor(Color.white);
            playerTarget = unit;
            playerTarget.card.SetColor(Color.blue);
        }
    }

    public Vector2 GetCardPosition(int index, bool ally)
    {
        int maxCount;
        float y;
        if (ally)
        {
            maxCount = maxAlliesCount;
            y = alliesY;
        }
        else
        {
            maxCount = maxEnemiesCount;
            y = enemiesY;
        }

        int shift = (index + 1) / 2;
        int dir = index % 2 == 1 ? 1 : -1;
        float x = spaceBetweenCards * shift * dir;
        if (maxCount % 2 == 0)
            x -= spaceBetweenCards / 2;

        return new(x, y);
    }
}
