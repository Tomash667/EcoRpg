using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Player : ISerializationCallbackReceiver
{
    public List<ItemSlot> items = new();
    public Item weapon, armor;
    public string name, weaponName, armorName;
    public int level, exp, hp, hpMax, attack, defense, energy, gold;

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
    public int Defense
    {
        get
        {
            int value = defense;
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
        defense = 5;
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
            defense++;
            return true;
        }
        else
            return false;
    }

    public ItemSlot FindItem(string name)
    {
        return items.FirstOrDefault(x => x.item.name == name);
    }

    public void AddItem(Item item, int count = 1)
    {
        ItemSlot itemSlot = items.FirstOrDefault(x => x.item == item);
        if (itemSlot != null)
            itemSlot.count += count;
        else
            items.Add(new() { item = item, count = count });
    }

    public void RemoveItem(ItemSlot itemSlot, int count = 1)
    {
        itemSlot.count -= count;
        if (itemSlot.count <= 0)
            items.Remove(itemSlot);
    }

    public void OnBeforeSerialize()
    {
        weaponName = weapon?.name;
        armorName = armor?.name;
    }

    public void OnAfterDeserialize()
    {
        if (!string.IsNullOrEmpty(weaponName))
            weapon = Item.Get(weaponName);
        if (!string.IsNullOrEmpty(armorName))
            armor = Item.Get(armorName);
    }
}
