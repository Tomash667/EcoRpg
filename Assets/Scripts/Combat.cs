using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Combat : MonoBehaviour
{
    public GameObject characterCardPrefab;

    private readonly Vector2[] teamPos = new Vector2[] { new(0, 200), new(-200, 200), new(200, 200) };
    private readonly Vector2[] enemyPos = new Vector2[] { new(0, -200), new(-200, -200), new(200, -200) };

    private List<int> order = new(), enemyHp = new();
    private Game game;
    private Enemy enemy;
    private float timer;
    private int combatIndex, combatResult;

    public void Init(Enemy enemy, int count)
    {
        game = Global.Game;
        this.enemy = enemy;

        Transform container = transform.GetChild(0);
        foreach (Transform child in container)
            Destroy(child.gameObject);

        order.Clear();
        enemyHp.Clear();

        int index = 0;
        foreach(Hero hero in game.Team)
        {
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(hero.name, hero.hpp, false);
            RectTransform transform = card.GetComponent<RectTransform>();
            transform.anchoredPosition = teamPos[index];
            ++index;
            order.Add(-index);
            hero.InitCombat();
        }

        for (int i = 0; i < count; ++i)
        {
            CharacterCard card = Instantiate(characterCardPrefab, container).GetComponent<CharacterCard>();
            card.Init(enemy.name, 1f, true);
            RectTransform transform = card.GetComponent<RectTransform>();
            transform.anchoredPosition = enemyPos[i];
            order.Add(i);
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
        combatResult = 0;
        timer = 0.5f;

        transform.parent.Find("Buttons").gameObject.SetActive(false);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0)
            return;

        int unitIndex = order[combatIndex];
        if (unitIndex < 0)
        {
            Hero hero = unitIndex == -1 ? game.player : game.allies[-unitIndex - 2];
            if (!hero.backRow)
                hero.canBlock = true;
            if (hero.wasteTurn)
                hero.wasteTurn = false;
            else if (hero.hp > 0)
            {
                int enemyIndex = enemyHp.Select((hp, index) => (hp, index)).RandomItem(x => x.hp > 0).index;
                if (AttackChance(hero.dex, enemy.dex))
                {
                    enemyHp[enemyIndex] -= Mathf.Max(hero.Attack - enemy.def, 0);
                    if (enemyHp.All(x => x <= 0))
                        combatResult = 1;
                }
                timer = 0.5f;
            }
        }
        else if (enemyHp[unitIndex] > 0)
        {
            Hero hero = game.Team.RandomItem(x => x.hp > 0);
            if (hero.backRow)
            {
                // front row heroes can block attack once per round
                Hero blockingHero = game.Team.RandomItem(x => x.hp > 0 && x.canBlock);
                if (blockingHero != null)
                {
                    hero = blockingHero;
                    hero.canBlock = false;
                }
            }

            if (AttackChance(enemy.dex, hero.dex))
            {
                hero.hp -= Mathf.Max(enemy.attack - hero.Defense, 0);
                if (hero.hp <= 0)
                {
                    ItemSlot potion;
                    if (!hero.wasteTurn && (potion = hero.FindHealingItem()) != null && hero.hp + potion.item.power > 0)
                    {
                        // hero use potion and waste turn
                        hero.hp = Mathf.Min(hero.hp + potion.item.power, hero.hpMax);
                        hero.RemoveItem(potion);
                        hero.wasteTurn = true;
                    }
                    else if (game.Team.All(x => x.hp <= 0))
                    {
                        // lost
                        combatResult = 2;
                    }
                }
            }
        }

        ++combatIndex;
        if (combatIndex == order.Count)
            combatIndex = 0;
    }

    private bool AttackChance(int myDex, int targetDex)
    {
        int chance = 75 + (myDex - targetDex) * 5;
        if (chance < 10)
            chance = 10;
        return Utility.Random(0, 100) < chance;
    }
}
