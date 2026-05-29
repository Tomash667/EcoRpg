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

    public GameObject characterCardPrefab, arrowPrefab;

    private readonly Vector2[] teamPos = new Vector2[] { new(0, -125), new(-200, -125), new(200, -125) };
    private readonly Vector2[] enemyPos = new Vector2[] { new(0, 200), new(-200, 200), new(200, 200) };

    private readonly List<string> textParts = new();
    private readonly List<CharacterCard> cards = new();
    private readonly List<int> enemyHp = new();
    private List<int> order = new();
    private Game game;
    private Enemy enemy;
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

    public void Init(Enemy enemy, int count)
    {
        game = Global.Game;
        this.enemy = enemy;

        Transform container = transform.GetChild(1);
        foreach (Transform child in container)
            Destroy(child.gameObject);

        order.Clear();
        enemyHp.Clear();
        cards.Clear();
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
            cards.Add(card);
            hero.InitCombat();
        }

        for (int i = 0; i < count; ++i)
        {
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(enemy.name, 1f, true);
            RectTransform transform = card.GetComponent<RectTransform>();
            transform.anchoredPosition = enemyPos[i];
            order.Add(i);
            card.index = i;
            cards.Add(card);
            enemyHp.Add(enemy.hp);
        }

        order = order.Select(x =>
        {
            int dex;
            if (x == -1)
                dex = game.player.dex;
            else if (x < -1)
                dex = game.allies[-x - 2].dex;
            else
                dex = enemy.dex;
            dex += Utility.Rand % 5;
            return (x, dex);
        }).OrderByDescending(x => x.dex).Select(x => x.x).ToList();
        combatIndex = 0;
        result = 0;
        timer = 0.5f;
        arrow.SetActive(false);
        action = Action.None;

        transform.parent.Find("Buttons").gameObject.SetActive(false);
        AppendText($"You explore the {Global.World.CurrentTile.Name} and <b>{Utility.PluralText(enemy.name, count)}</b> attack you.");
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
                foreach (CharacterCard card in cards.Where(x => x.index < 0))
                    card.Escape();
            }
            else
            {
                transform.parent.Find("Buttons").gameObject.SetActive(true);
                game.PostCombat(result, enemy, enemyHp.Count);
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
                foreach (CharacterCard card in cards.Where(x => x.index < 0))
                    card.Escape();
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
            int enemyIndex = enemyHp.Select((hp, index) => (hp, index)).RandomItem(x => x.hp > 0).index;
            CharacterCard targetCard = cards.First(x => x.index == enemyIndex);
            if (AttackChance(hero.dex, enemy.dex))
            {
                int dmg = Mathf.Max(hero.Attack - enemy.def, 0);
                int hp = enemyHp[enemyIndex] -= dmg;
                float hpp = ((float)hp) / enemy.hp;
                targetCard.Damage(hpp);
                if (hp <= 0)
                {
                    // hit hits
                    AppendText(hero is Player
                        ? $"You hit {enemy.name} for {dmg} damage and defeat {enemy.him}."
                        : $"{hero.name} hits {enemy.name} for {dmg} damage and defeat {enemy.him}.");
                    if (enemyHp.All(x => x <= 0))
                    {
                        AppendText("You win!");
                        timer = 1;
                        result = Result.Win;
                    }
                }
                else
                {
                    AppendText(hero is Player
                        ? $"You hit {enemy.name} for {dmg} damage."
                        : $"{hero.name} hits {enemy.name} for {dmg} damage.");
                }
            }
            else
            {
                AppendText(hero is Player
                    ? $"You miss {enemy.name}."
                    : $"{hero.name} misses {enemy.name}.");
                targetCard.Dodge();
            }

            CharacterCard heroCard = GetCard(hero);
            if (hero.weapon != null && hero.weapon.subtype == Item.Subtype.Bow)
            {
                Arrow2 arrow = Instantiate(arrowPrefab, transform).GetComponent<Arrow2>();
                arrow.Shoot(heroCard.position, targetCard.position);
            }
            else
                heroCard.Attack();
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
        // hero use potion
        int prevHp = hero.hp;
        hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
        hero.RemoveItem(potion);
        hero.potionsUsed++;
        AppendText(hero is Player
            ? $"You use {potion.item.name} and get healed for {hero.hp - prevHp}."
            : $"{hero.name} use {potion.item.name} and get healed for {hero.hp - prevHp}.");
        GetCard(hero).Heal(hero.hpp);
        timer = 0.5f;
    }

    private void EnemyAction(int unitIndex)
    {
        if (enemyHp[unitIndex] <= 0)
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
        CharacterCard heroCard = GetCard(hero);
        if (AttackChance(enemy.dex, hero.dex))
        {
            int dmg = Mathf.Max(enemy.attack - hero.Defense, 0);
            hero.hp -= dmg;
            if (hero.hp <= 0)
            {
                hero.potionTimer = hero.potionsUsed;
                AppendText($"{enemy.name.ToUpper1()} hits {(hero is Player ? "you" : hero.name)} for {dmg} damage and defeats {hero.him}.");
                if (game.Team.All(x => x.hp <= 0))
                {
                    // lost
                    AppendText("You lost!");
                    result = Result.Defeat;
                    timer = 0.5f;
                }
            }
            else
                AppendText($"{enemy.name.ToUpper1()} hits {(hero is Player ? "you" : hero.name)} for {dmg} damage.");

            if (isBlocking)
                heroCard.Block(hero.hpp);
            else
                heroCard.Damage(hero.hpp);
        }
        else
        {
            AppendText($"{enemy.name.ToUpper1()} misses {(hero is Player ? "you" : hero.name)}.");
            if (isBlocking)
                heroCard.Block();
            else
                heroCard.Dodge();
        }
        cards.First(x => x.index == unitIndex).Attack();
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

    private CharacterCard GetCard(Hero hero)
    {
        int index = -game.Team.IndexOf(hero) - 1;
        return cards.First(x => x.index == index);
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
