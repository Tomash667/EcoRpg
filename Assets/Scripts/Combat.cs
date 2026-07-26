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
        public int hp;
    }

    public GameObject characterCardPrefab, arrowPrefab;

    private readonly Vector2[] teamPos = new Vector2[] { new(0, -125), new(-200, -125), new(200, -125) };
    private readonly Vector2[] enemyPos = new Vector2[] { new(0, 200), new(-200, 200), new(200, 200) };

    private readonly List<string> textParts = new();
    private readonly List<Unit> enemies = new();
    private List<Enemy> enemyList;
    private List<int> order = new();
    private Game game;
    private TMP_Text text;
    private GameObject arrow;
    private Result result;
    private Action action;
    private float timer;
    private int combatIndex;

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
            card.Init(hero.name, hero.hpp, false);
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

        for (int i = 0; i < enemyList.Count; ++i)
        {
            Enemy enemy = enemyList[i];
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(enemy.name, 1f, true);
            RectTransform transform = card.GetComponent<RectTransform>();
            transform.anchoredPosition = enemyPos[i];
            order.Add(i);
            card.index = i;
            enemies.Add(new Unit { enemy = enemy, card = card, hp = enemy.hp });
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

        transform.parent.Find("Buttons").gameObject.SetActive(false);
        AppendText($"You explore the {Global.World.CurrentTile.Name} and <b>{Utility.PrettyGroup(enemyList.Select(x => x.name))}</b> {Utility.S("attack", enemyList.Count == 1)} you.");
    }

    private void Update()
    {
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
        if (unitIndex < 0)
            HeroAction(unitIndex);
        else
            EnemyAction(unitIndex);

        ++combatIndex;
        if (combatIndex == order.Count)
            combatIndex = 0;
    }

    private void HeroAction(int unitIndex)
    {
        Hero hero = unitIndex == -1 ? game.player : game.allies[-unitIndex - 2];
        if (!hero.BackRow)
            hero.canBlock = true;

        if (hero is Player && action != Action.None)
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
                return;
            }
            else if (action == Action.Heal)
            {
                ItemSlot potion;
                if ((hero.hp > 0 || hero.potionTimer == 0) && (potion = hero.FindHealingItem()) != null && hero.hp + potion.item.power > 0)
                {
                    HeroUsePotion(hero, potion);
                    arrow.SetActive(false);
                    action = Action.None;
                    return;
                }
            }
        }

        if (hero.hp > 0)
        {
            timer = 0.5f;
            Unit target = enemies.RandomItem(x => x.hp > 0);
            if (AttackChance(hero.dex, target.enemy.dex))
            {
                int dmg = Mathf.Max(hero.Attack - target.enemy.def, 0);
                target.hp -= dmg;
                float hpp = ((float)target.hp) / target.enemy.hp;
                target.card.Damage(hpp);
                if (target.hp <= 0)
                {
                    AppendText(hero is Player
                        ? $"You hit {target.enemy.name} for {dmg} damage and defeat {target.enemy.him}."
                        : $"{hero.name} hits {target.enemy.name} for {dmg} damage and defeat {target.enemy.him}.");
                    if (enemies.All(x => x.hp <= 0))
                    {
                        AppendText("You win!");
                        timer = 1;
                        result = Result.Win;
                    }
                }
                else
                {
                    AppendText(hero is Player
                        ? $"You hit {target.enemy.name} for {dmg} damage."
                        : $"{hero.name} hits {target.enemy.name} for {dmg} damage.");
                }
            }
            else
            {
                AppendText(hero is Player
                    ? $"You miss {target.enemy.name}."
                    : $"{hero.name} misses {target.enemy.name}.");
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
        else
            hero.potionTimer--;
    }

    private void HeroUsePotion(Hero hero, ItemSlot potion)
    {
        int prevHp = hero.hp;
        hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
        hero.RemoveItem(potion);
        hero.potionsUsed++;
        AppendText(hero is Player
            ? $"You use {potion.item.name} and get healed for {hero.hp - prevHp}."
            : $"{hero.name} use {potion.item.name} and get healed for {hero.hp - prevHp}.");
        hero.card.Heal(hero.hpp);
        timer = 0.5f;
    }

    private void EnemyAction(int unitIndex)
    {
        Unit me = enemies[unitIndex];
        if (me.hp <= 0)
            return;

        Hero hero = game.Team.RandomItem(x => x.hp > 0);
        bool isBlocking = false;
        if (hero.BackRow)
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

        timer = 0.5f;
        if (AttackChance(me.enemy.dex, hero.dex))
        {
            int dmg = Mathf.Max(me.enemy.attack - hero.Defense, 0);
            hero.hp -= dmg;
            if (hero.hp <= 0)
            {
                hero.potionTimer = hero.potionsUsed;
                AppendText($"{me.enemy.name.ToUpper1()} hits {(hero is Player ? "you" : hero.name)} for {dmg} damage and defeats {hero.him}.");
                if (game.Team.All(x => x.hp <= 0))
                {
                    // lost
                    AppendText("You lost!");
                    result = Result.Defeat;
                    timer = 0.5f;
                }
            }
            else
                AppendText($"{me.enemy.name.ToUpper1()} hits {(hero is Player ? "you" : hero.name)} for {dmg} damage.");

            if (isBlocking)
                hero.card.Block(hero.hpp);
            else
                hero.card.Damage(hero.hpp);
        }
        else
        {
            AppendText($"{me.enemy.name.ToUpper1()} misses {(hero is Player ? "you" : hero.name)}.");
            if (isBlocking)
                hero.card.Block();
            else
                hero.card.Dodge();
        }
        me.card.Attack();
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
        if (textParts.Count > 4)
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
