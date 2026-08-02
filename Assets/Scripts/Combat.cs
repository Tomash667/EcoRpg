using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class Combat : MonoBehaviour
{
    public enum Result
    {
        None,
        Win,
        Defeat,
        DefeatEscaping,
        Escape
    }

    private enum Action
    {
        None,
        Escape,
        Heal
    }

    private class Unit
    {
        public Enemy enemy;
        public CharacterCard card;
        public int hp, cooldown, cooldown2;
        public bool canBlock, summoned;

        public float hpp => ((float)hp) / enemy.hp;
    }

    public GameObject characterCardPrefab, arrowPrefab;

    private readonly Vector2[] teamPos = new Vector2[] { new(0, -125), new(-200, -125), new(200, -125) };
    private readonly Vector2[] enemyPos = new Vector2[] { new(0, 200), new(-200, 200), new(200, 200) };

    private readonly List<string> textParts = new();
    private readonly List<Unit> enemies = new();
    private readonly List<Hero> hitHeroes = new();
    private List<Enemy> enemyList;
    private List<int> order = new();
    private Game game;
    private TMP_Text text;
    private GameObject arrow;
    private Result result;
    private Action action;
    private float timer;
    private int combatIndex, attacks, effectTick;

    public void Init()
    {
        text = transform.GetChild(0).GetComponent<TMP_Text>();
        arrow = transform.Find("Arrow").gameObject;
    }

    public void Init(List<Enemy> enemyList)
    {
        game = Global.Game;
        this.enemyList = enemyList;

        Transform container = transform.GetChild(1);
        foreach (Transform child in container)
            Destroy(child.gameObject);

        order.Clear();
        enemies.Clear();
        textParts.Clear();

        int index = 0;
        bool multiRow = game.Team.Any(x => x.BackRow != game.player.BackRow);
        foreach (Hero hero in game.Team)
        {
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(hero.name, hero.hpp, false, Resources.Load<Sprite>(hero.Portrait));
            RectTransform transform = card.GetComponent<RectTransform>();
            Vector2 pos = teamPos[index];
            if (multiRow && !hero.BackRow)
                pos.y += 25;
            transform.anchoredPosition = pos;
            ++index;
            order.Add(-index);
            card.index = -index;
            hero.InitCombat();
            hero.card = card;
        }

        multiRow = enemyList.Any(x => x.blocks != enemyList[0].blocks);
        for (int i = 0; i < enemyList.Count; ++i)
        {
            Enemy enemy = enemyList[i];
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(enemy.name, 1f, true, Resources.Load<Sprite>(enemy.Portrait));
            RectTransform transform = card.GetComponent<RectTransform>();
            Vector2 pos = enemyPos[i];
            if (multiRow && enemy.blocks)
                pos.y -= 25;
            transform.anchoredPosition = pos;
            order.Add(i);
            card.index = i;
            enemies.Add(new Unit { enemy = enemy, card = card, hp = enemy.hp, canBlock = enemy.blocks });
        }

        order = order.Select(x =>
        {
            int dex;
            if (x == -1)
                dex = game.player.dex;
            else if (x < -1)
                dex = game.allies[-x - 2].dex;
            else
                dex = enemies[x].enemy.dex;
            dex += Utility.Rand % 5;
            return (x, dex);
        }).OrderByDescending(x => x.dex).Select(x => x.x).ToList();
        combatIndex = 0;
        result = 0;
        timer = 0.5f;
        arrow.SetActive(false);
        action = Action.None;
        effectTick = 0;

        transform.parent.Find("Buttons").gameObject.SetActive(false);
        AppendText($"You explore the {Global.World.CurrentTile.Name} and <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> {Utility.S("attack", enemyList.Count == 1)} you.");
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
            Time.timeScale = 0.25f;
        if (Input.GetKeyUp(KeyCode.Space))
            Time.timeScale = 1f;
#endif

        timer -= Time.deltaTime;
        if (timer > 0)
            return;

        if (result != Result.None)
        {
            if (result == Result.Defeat)
            {
                result = Result.DefeatEscaping;
                timer = 1f;
                foreach (Hero hero in game.Team)
                    hero.card.Escape();
            }
            else
            {
                transform.parent.Find("Buttons").gameObject.SetActive(true);
                game.PostCombat(result, enemyList);
            }
            return;
        }

        int unitIndex = order[combatIndex];
        bool nextUnit;
        if (unitIndex < 0)
            nextUnit = HeroAction(unitIndex);
        else
            nextUnit = EnemyAction(unitIndex);

        if (nextUnit)
        {
            ++combatIndex;
            if (combatIndex == order.Count)
                combatIndex = 0;
            effectTick = 0;
        }
    }

    private bool HeroAction(int unitIndex)
    {
        Hero hero = unitIndex == -1 ? game.player : game.allies[-unitIndex - 2];

        if (effectTick == 0)
        {
            effectTick = 1;
            if (!hero.BackRow)
                hero.canBlock = true;
            if (hero.potionTimer > 0)
                --hero.potionTimer;
        }

        if (hero is Player && action != Action.None && (hero.confused == 0 || Utility.Rand % 2 == 0))
        {
            if (action == Action.Escape)
            {
                // escape from combat
                result = Result.Escape;
                timer = 1f;
                foreach (Hero hero2 in game.Team)
                    hero2.card.Escape();
                arrow.SetActive(false);
                action = Action.None;
                return false;
            }
            else if (action == Action.Heal)
            {
                ItemSlot potion;
                if ((hero.hp > 0 || hero.potionTimer == 0) && (potion = hero.FindHealingItem()) != null && hero.hp + potion.item.power > 0)
                {
                    HeroUsePotion(hero, potion);
                    arrow.SetActive(false);
                    action = Action.None;
                    return true;
                }
            }
        }

        if (hero.hp > 0)
        {
            // poison damage
            if (effectTick < 2 && hero.poison > 0)
            {
                timer = 0.3f;
                hero.hp -= hero.poison;
                hero.card.PoisonDamage(hero.hpp);
                if (hero.hp <= 0)
                {
                    hero.potionTimer = hero.potionsUsed + 1;
                    hero.poison = 0;
                    AppendText($"{hero.NameYou} {hero.S("take")} {hero.poison} poison damage and {hero.isAre} defeated.");
                    if (game.Team.All(x => x.hp <= 0))
                    {
                        // lost
                        AppendText("You lost!");
                        result = Result.Defeat;
                        timer = 0.5f;
                    }
                }
                else
                    AppendText($"{hero.NameYou} {hero.S("take")} {hero.poison} poison damage.");
                effectTick = 2;
                return false;
            }

            // confused
            if (hero.confused > 0)
            {
                if (effectTick < 3)
                {
                    timer = 0.3f;
                    hero.card.Confused();
                    if (hero.confused == 1)
                        hero.card.RemoveEffect(Effect.Confused);
                    effectTick = 3;
                    return false;
                }

                --hero.confused;
                if (Utility.Rand % 2 == 0)
                {
                    // attack ally
                    Hero targetHero = game.Team.RandomItem(x => x.hp > 0);
                    if (targetHero == hero)
                    {
                        AppendText($"{hero.NameYou} {hero.S("don't", "doesn't")} know what to do.");
                        timer = 0.1f;
                        return true;
                    }

                    if (AttackChance(hero.dex, targetHero.dex))
                    {
                        int dmg = Mathf.Max(hero.Attack - targetHero.Defense, 0);
                        targetHero.card.Damage(targetHero.hpp);
                        if (targetHero.hp <= 0)
                            AppendText($"{hero.NameYou} {hero.S("hit")} {targetHero.name} for {dmg} damage and {hero.S("defeat")} {targetHero.him}.");
                        else
                            AppendText($"{hero.NameYou} {hero.S("hit")} {targetHero.name} for {dmg} damage.");
                    }
                    else
                    {
                        AppendText($"{hero.NameYou} {hero.S("miss", "misses")} {targetHero.nameYou}.");
                        targetHero.card.Dodge();
                    }

                    if (hero.weapon != null && hero.weapon.subtype == Item.Subtype.Bow)
                    {
                        Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                        arrow.Shoot(hero.card.position, targetHero.card.position);
                    }
                    else
                        hero.card.Attack();

                    timer = 0.5f;
                    return true;
                }
            }

            timer = 0.5f;
            Unit target = enemies.RandomItem(x => x.hp > 0);
            bool isBlocking = false;
            if (!target.enemy.blocks)
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

            if (AttackChance(hero.dex, target.enemy.dex))
            {
                int dmg = Mathf.Max(hero.Attack - target.enemy.def, 0);
                target.hp -= dmg;
                if (isBlocking)
                    target.card.Block(target.hpp);
                else
                    target.card.Damage(target.hpp);

                if (target.hp <= 0)
                {
                    AppendText($"{hero.NameYou} {hero.S("hit")} {target.enemy.name} for {dmg} damage and {hero.S("defeat")} {target.enemy.him}.");
                    if (enemies.All(x => x.hp <= 0))
                    {
                        AppendText("You win!");
                        timer = 1;
                        result = Result.Win;
                    }
                }
                else
                    AppendText($"{hero.NameYou} {hero.S("hit")} {target.enemy.name} for {dmg} damage.");
            }
            else
            {
                AppendText($"{hero.NameYou} {hero.S("miss", "misses")} {target.enemy.name}.");
                if (isBlocking)
                    target.card.Block();
                else
                    target.card.Dodge();
            }

            if (hero.weapon != null && hero.weapon.subtype == Item.Subtype.Bow)
            {
                Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                arrow.Shoot(hero.card.position, target.card.position);
            }
            else
                hero.card.Attack();
        }
        else if (hero.potionTimer == 0)
        {
            ItemSlot potion;
            if ((potion = hero.FindHealingItem()) != null && hero.hp + potion.item.power > 0)
                HeroUsePotion(hero, potion);
        }

        return true;
    }

    private void HeroUsePotion(Hero hero, ItemSlot potion)
    {
        int prevHp = hero.hp;
        hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
        hero.RemoveItem(potion);
        hero.potionsUsed++;
        hero.poison = 0;
        AppendText($"{hero.NameYou} {hero.S("use")} {potion.item.name} and {hero.S("get")} healed for {hero.hp - prevHp}.");
        hero.card.Heal(hero.hpp);
        hero.card.RemoveEffect(Effect.Poison);
        timer = 0.5f;
    }

    private bool EnemyAction(int unitIndex)
    {
        Unit me = enemies[unitIndex];
        if (me.hp <= 0)
        {
            if (me.summoned)
            {
                AppendText($"{me.enemy.name.ToUpper1()} crumbles into dust.");
                me.card.Unsummon();
                order.Remove(unitIndex);
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

        if (me.enemy.summon && me.cooldown2 == 0 && enemies.Count < Game.MaxTeamSize && Utility.Rand % 3 != 0)
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
        Hero hero = game.Team.RandomItem(x => x.hp > 0 && !hitHeroes.Contains(x));
        bool isBlocking = false;
        if (hero == null)
            return true;
        else if (hero.BackRow)
        {
            // front row heroes can block attack once per round
            Hero blockingHero = game.Team.RandomItem(x => x.hp > 0 && x.canBlock && x.shield != null);
            if (blockingHero != null)
            {
                hero = blockingHero;
                hero.canBlock = false;
                isBlocking = true;
            }
        }

        bool lifesteal = false;
        if (AttackChance(me.enemy.dex, hero.dex))
        {
            hitHeroes.Add(hero);

            int def = hero.Defense;
            if (spellName != null)
                def /= 2;
            int dmg = Mathf.Max(me.enemy.attack - def, 0);
            hero.hp -= dmg;

            if (isBlocking)
                hero.card.Block(hero.hpp);
            else
                hero.card.Damage(hero.hpp);

            if (hero.hp <= 0)
            {
                hero.potionTimer = hero.potionsUsed + 1;
                hero.poison = 0;
                AppendText(spellName != null
                    ? $"{me.enemy.name.ToUpper1()} shoots {spellName} at {hero.nameYou} for {dmg} damage and defeats {hero.him}."
                    : $"{me.enemy.name.ToUpper1()} hits {hero.nameYou} for {dmg} damage and defeats {hero.him}.");
                if (game.Team.All(x => x.hp <= 0))
                {
                    // lost
                    AppendText("You lost!");
                    result = Result.Defeat;
                }
            }
            else
            {
                string str = spellName != null ? $"{me.enemy.name.ToUpper1()} shoots {spellName} at {hero.nameYou} for {dmg} damage." : $"{me.enemy.name.ToUpper1()} hits {hero.nameYou} for {dmg} damage.";
                if (me.enemy.attackType == Enemy.AttackType.Poison && dmg > 0)
                {
                    int poison = dmg / 5;
                    if (poison == 0)
                        poison = 1;
                    if (hero.poison == 0)
                    {
                        str += $" {hero.NameYou} {hero.isAre} poisoned.";
                        hero.card.AddEffect(Effect.Poison);
                    }
                    hero.poison += poison;
                }
                else if (me.enemy.attackType == Enemy.AttackType.Confuse)
                {
                    str += $" {hero.NameYou} {hero.isAre} confused.";
                    hero.confused = 2;
                    hero.card.AddEffect(Effect.Confused);
                }

                AppendText(str);
            }

            if (me.enemy.attackType == Enemy.AttackType.LifeSteal && me.hp < me.enemy.hp && dmg >= 2)
            {
                int heal = dmg / 2;
                int prev = me.hp;
                me.hp = Mathf.Min(me.hp + heal, me.enemy.hp);
                heal = me.hp - prev;
                AppendText($"{me.enemy.name.ToUpper1()} is healed for {heal}.");
                lifesteal = true;
            }
        }
        else
        {
            AppendText(spellName != null
                ? $"{me.enemy.name.ToUpper1()} shoots {spellName} at {hero.nameYou} but misses."
                : $"{me.enemy.name.ToUpper1()} misses {hero.nameYou}.");
            if (isBlocking)
                hero.card.Block();
            else
                hero.card.Dodge();
        }

        if (spellName != null)
        {
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
        else if (lifesteal)
            me.card.Attack(me.hpp);
        else
            me.card.Attack();

        --attacks;
        return attacks <= 0;
    }

    private void EnemyFirebreath(Unit me)
    {
        me.cooldown = 2;
        foreach (Hero hero in game.Team.Where(x => x.hp > 0))
        {
            if (AttackChance(me.enemy.dex, hero.dex))
            {
                int def = hero.Defense / 2;
                int dmg = Mathf.Max(me.enemy.attack - def, 0);
                hero.hp -= dmg;
                hero.card.Damage(hero.hpp);
                if (hero.hp <= 0)
                {
                    hero.potionTimer = hero.potionsUsed + 1;
                    hero.poison = 0;
                    AppendText($"{me.enemy.name.ToUpper1()} breaths fire at {hero.nameYou} for {dmg} damage and defeats {hero.him}.");
                    if (game.Team.All(x => x.hp <= 0))
                    {
                        // lost
                        AppendText("You lost!");
                        result = Result.Defeat;
                    }
                }
                else
                    AppendText($"{me.enemy.name.ToUpper1()} breaths fire at {hero.nameYou} for {dmg} damage.");
            }
            else
            {
                AppendText($"{me.enemy.name.ToUpper1()} breaths fire at {hero.nameYou} but misses.");
                hero.card.Dodge();
            }

            Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
            arrow.Shoot(me.card.position, hero.card.position);
            arrow.SetFire();
        }
    }

    private void EnemySummon(Unit me)
    {
        Enemy enemy = Enemy.Get("mummy");
        CharacterCard card = Instantiate(characterCardPrefab, transform.GetChild(1)).GetComponent<CharacterCard>();
        card.Init(enemy.name, 1f, true, Resources.Load<Sprite>(enemy.Portrait));
        RectTransform rectTransform = card.GetComponent<RectTransform>();
        int i = enemies.Count;
        Vector2 pos = enemyPos[i];
        if (enemy.blocks && enemies.Any(x => x.enemy.blocks != enemy.blocks))
            pos.y -= 25;
        rectTransform.anchoredPosition = pos;
        int myIndex = enemies.IndexOf(me);
        int myOrder = order.IndexOf(myIndex);
        order.Insert(myOrder + 1, i);
        card.index = i;
        card.Summon();
        enemies.Add(new Unit { enemy = enemy, card = card, hp = enemy.hp, canBlock = enemy.blocks, summoned = true });
        AppendText($"{me.enemy.name.ToUpper1()} summons {enemy.name}.");
        timer = 0.5f;
        me.cooldown2 = 3;
    }

    public static bool AttackChance(int myDex, int targetDex)
    {
        int chance = 75 + (myDex - targetDex) * 5;
        if (chance < 10)
            chance = 10;
        return Utility.Random(0, 100) < chance;
    }

    private void AppendText(string str)
    {
        textParts.Add(str);
        if (textParts.Count > 5)
            textParts.RemoveAt(0);
        text.text = string.Join('\n', textParts);
        game.SetText(null);
    }

    public void Escape()
    {
        if (action == Action.Escape)
        {
            action = Action.None;
            arrow.SetActive(false);
        }
        else
        {
            action = Action.Escape;
            RectTransform rectTransform = arrow.transform as RectTransform;
            rectTransform.anchoredPosition = new(rectTransform.anchoredPosition.x, -37.03653f);
            arrow.SetActive(true);
        }
    }

    public void Heal()
    {
        if (action == Action.Heal)
        {
            action = Action.None;
            arrow.SetActive(false);
        }
        else
        {
            action = Action.Heal;
            RectTransform rectTransform = arrow.transform as RectTransform;
            rectTransform.anchoredPosition = new(rectTransform.anchoredPosition.x, 2.96347f);
            arrow.SetActive(true);
        }
    }
}
