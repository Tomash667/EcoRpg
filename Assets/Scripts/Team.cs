using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Team
{
    public const int MaxSize = 3;

    [SerializeReference]
    public List<Hero> heroes;

    public bool HaveItem(string itemName)
    {
        Item item = Item.Get(itemName);
        return heroes.Any(x => x.HaveItem(item));
    }

    public (Hero hero, int value) GetSkill(Skill skill)
    {
        Hero bestHero = null;
        int bestValue = 0;
        foreach (Hero hero in heroes)
        {
            int value = hero.GetSkill(skill);
            if (value > bestValue)
            {
                bestValue = value;
                bestHero = hero;
            }
        }
        return (bestHero, bestValue);
    }

    public int CountItem(Item item)
    {
        return heroes.Sum(x => x.CountItem(item));
    }

    public void AddGold(int gold)
    {
        if (gold <= 0)
            return;

        if (heroes.Count == 1)
        {
            heroes[0].AddGold(gold);
            return;
        }

        int share = gold / heroes.Count;
        int extraGold = gold - share * heroes.Count;
        foreach (Hero hero in heroes)
        {
            int goldReceived = share;
            if (extraGold > 0)
            {
                ++goldReceived;
                --extraGold;
            }
            hero.AddGold(goldReceived);
            /*if (hero is not Player)
			{
				if (world.Location.IsSafe())
					hero.BuyItems();
				else if (world.Location == TileType.MageTower)
					hero.EnchantItems();
			}*/
        }
    }

    public int RemoveGold(int count)
    {
        int removed = 0;

        while (count > 0)
        {
            Hero[] available = heroes.Where(x => x.gold > 0).ToArray();
            if (available.Length == 0)
                break; // nothing left to remove

            int perHero = Mathf.Max(1, count / available.Length);
            foreach (Hero hero in available)
            {
                if (count <= 0)
                    break;

                int canRemove = Mathf.Min(perHero, hero.gold);
                hero.AddGold(-canRemove);
                count -= canRemove;
                removed += canRemove;
            }
        }

        return removed;
    }

    public int RemoveItem(Item item, int count)
    {
        int removed = 0;

        // Cache counts so we don't call CountItem repeatedly
        Dictionary<Hero, int> counts = new();
        foreach (Hero hero in heroes)
            counts[hero] = hero.CountItem(item);

        while (count > 0)
        {
            // Heroes that still have items
            var available = counts
                .Where(kv => kv.Value > 0)
                .Select(kv => kv.Key)
                .ToList();

            if (available.Count == 0)
                break; // nothing left to remove

            int perHero = Mathf.Max(1, count / available.Count);
            foreach (var hero in available)
            {
                if (count <= 0)
                    break;

                int canRemove = Mathf.Min(perHero, counts[hero]);
                hero.RemoveItem(item, canRemove);
                counts[hero] -= canRemove;
                count -= canRemove;
                removed += canRemove;
            }
        }

        return removed;
    }

    public void CancelOutDebts()
    {
        if (heroes.Count == 1)
        {
            Player player = heroes[0] as Player;
            player.TurnItemsNonTeam();
            player.owedGold = 0;
            return;
        }

        int minDebt = heroes.Min(x => x.owedGold);
        if (minDebt > 0)
        {
            foreach (Hero hero in heroes)
                hero.owedGold -= minDebt;
        }
    }

    public void ChangeAffection(int value, TextBuilder text, Func<Hero, bool> pred = null)
    {
        List<(Hero ally, int change)> changes = null;
        foreach (Hero ally in heroes.Skip(1))
        {
            if (pred != null && !pred(ally))
                continue;

            if (value > 0)
            {
                if (ally.affection == 100)
                    continue;
            }
            else
            {
                if (ally.affection == -100)
                    continue;
            }

            int prev = ally.affection;
            ally.affection = Mathf.Clamp(ally.affection + value, -100, 100);
            int actualChange = ally.affection - prev;

            if (text != null)
            {
                changes ??= new();
                changes.Add((ally, actualChange));
            }
        }

        if (changes != null)
        {
            foreach (var group in changes.GroupBy(x => x.change))
                text.Append($"{Utility.PrettyList(group.Select(x => x.ally.name))} affection {(value > 0 ? "increased" : "decreased")} ({group.Key:+0;-#}).");
        }
    }

    public void CheckBoredAllies(TextBuilder text)
    {
        List<(Hero ally, int count)> changes = null;
        foreach (Hero ally in heroes.Skip(1))
        {
            if (ally.bored >= 30)
            {
                int count = ally.bored / 30;
                ally.bored -= count * 30;
                ally.affection -= count;

                if (text != null)
                {
                    changes ??= new();
                    changes.Add((ally, count));
                }
            }
        }

        if (changes != null)
        {
            foreach (var group in changes.GroupBy(x => x.count))
                text.Append($"{Utility.PrettyList(group.Select(x => x.ally.name))} {(group.Count() == 1 ? "is" : "are")} bored (-{group.Key} affection).");
        }
    }

    public void OnNewDay()
    {
        foreach (Hero hero in heroes)
        {
            if (hero.rested > 0)
                --hero.rested;
            ++hero.bored;
            hero.winToday = false;
            hero.lastGift = 0;
        }

        Player player = heroes[0] as Player;
        if (heroes.Count == 1)
            player.affection = 0;
        else
            player.affection = heroes.Skip(1).Max(x => x.affection);
    }

    public void PayForItem(Hero hero, Item item, int count = 1)
    {
        int cost = (item.value * count / 2) * (heroes.Count - 1) / heroes.Count;
        hero.owedGold += cost;
        CancelOutDebts();
        if (hero.owedGold > 0)
            PayOwedGold(hero);
    }

    public void PayForProperty(Hero hero, int value)
    {
        int cost = value * (heroes.Count - 1) / heroes.Count;
        hero.owedGold += cost;
        CancelOutDebts();
        if (hero.owedGold > 0)
            PayOwedGold(hero);
    }

    public void PayOwedGold(Hero hero)
    {
        int availableGold = hero.gold - Hero.MinGold;
        if (availableGold <= 0)
            return;

        int people = heroes.Count - 1;
        if (availableGold >= hero.owedGold)
        {
            // pay all
            int goldPerHero = hero.owedGold / people;
            int extraGold = hero.owedGold - goldPerHero * people;
            hero.AddGold(-hero.owedGold);
            hero.owedGold = 0;
            foreach (Hero hero2 in heroes)
            {
                if (hero2 == hero)
                    continue;
                int goldReceived = goldPerHero;
                if (extraGold > 0)
                {
                    ++goldReceived;
                    --extraGold;
                }
                hero2.AddGold(goldReceived);
            }
        }
        else
        {
            // pay partial
            int goldPerHero = availableGold / people;
            if (goldPerHero == 0)
                return;
            hero.AddGold(-goldPerHero * people);
            hero.owedGold -= goldPerHero * people;
            foreach (Hero hero2 in heroes)
            {
                if (hero2 != hero)
                    hero2.AddGold(goldPerHero);
            }
        }
    }
}
