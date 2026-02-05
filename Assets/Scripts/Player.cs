using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Player
{
    public List<ItemSlot> items = new();
    public Item weapon, armor;
    public string name;
    public int level, exp, hp, hpMax, attack, defence, energy, gold;

    public int Attack
    {
        get
        {
            int value = attack;
            if (weapon != null)
                value += weapon.power;
            return value;
        }
    }
    public int Defence
    {
        get
        {
            int value = defence;
            if (armor != null)
                value += armor.power;
            return value;
        }
    }
    public int ExpP => exp / 10;

    public Player()
    {
        level = 1;
        exp = 0;
        hpMax = 100;
        hp = hpMax;
        energy = 100;
        gold = 50;
        attack = 25;
        defence = 5;
    }

    public bool AddExp(int newExp)
    {
        exp += newExp;
        if (exp >= 1000)
        {
            exp -= 1000;
            ++level;
            float hpRatio = (float)hp / hpMax;
            hpMax += 20;
            hp = (int)(hpRatio * hpMax);
            attack += 5;
            defence++;
            return true;
        }
        else
            return false;
    }

    public void AddItem(Item item)
    {
        ItemSlot itemSlot = items.FirstOrDefault(x => x.item == item);
        if (itemSlot != null)
            itemSlot.count++;
        else
            items.Add(new() { item = item, count = 1 });
    }

    public void RemoveItem(ItemSlot itemSlot)
    {
        --itemSlot.count;
        if (itemSlot.count == 0)
            items.Remove(itemSlot);
    }
}
