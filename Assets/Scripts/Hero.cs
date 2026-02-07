using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Hero : ISerializationCallbackReceiver
{
    public List<ItemSlot> items = new();
    public Item weapon, armor;
    public string name, weaponName, armorName;
    public int level, exp, hp, hpMax, attack, defense, gold;
    public bool female;

    [NonSerialized]
    public bool wasteTurn;

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
    public int HpP
    {
        get
        {
            int result = Mathf.FloorToInt(100f * hp / hpMax);
            if (result < 0)
                result = 0;
            else if (result == 0 && hp > 0)
                result = 1;
            return result;
        }
    }
    public char GenderSign => female ? '♀' : '♂';

    public void Init()
    {
        female = Utility.Rand % 2 == 0;
        name = (female ? Names.femaleNames : Names.maleNames).RandomItem();
        level = 1;
        hpMax = 100;
        hp = hpMax;
        attack = 25;
        defense = 5;
        weapon = Item.Get("club");
        armor = Item.Get("leather armor");
        AddItem(Item.Get("potion"));
        AddItem(Item.Get("rations"), 3);
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
