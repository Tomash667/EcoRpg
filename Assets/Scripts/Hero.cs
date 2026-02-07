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

    public bool AddExp(int enemyLevel, float mod)
    {
        int newExp = (int)(GetExpReward(enemyLevel) * mod);
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

    private int GetExpReward(int enemyLevel)
    {
        return (level - enemyLevel) switch
        {
            -1 => 300,
            0 => 250,
            1 => 200,
            2 => 150,
            3 => 100,
            4 => 50,
            5 => 25,
            6 => 10,
            7 => 5,
            8 => 2,
            9 => 1,
            _ => 0
        };
    }

    public ItemSlot FindItem(string name)
    {
        return items.FirstOrDefault(x => x.item.name == name);
    }

    public int CountItem(Item item)
    {
        ItemSlot itemSlot = items.FirstOrDefault(x => x.item == item);
        return itemSlot?.count ?? 0;
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

    public void RemoveItem(Item item, int count = 1)
    {
        ItemSlot itemSlot = items.FirstOrDefault(x => x.item == item);
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

    public bool WillTakeItem(Item item)
    {
        return item.type switch
        {
            Item.Type.Weapon => weapon == null || weapon.power < item.power,
            Item.Type.Armor => armor == null || armor.power < item.power,
            _ => true
        };
    }

    public void GiveItem(Item item, int count = 1)
    {
        switch(item.type)
        {
        case Item.Type.Weapon:
            if (weapon != null)
                gold += weapon.value / 2;
            weapon = item;
            break;
        case Item.Type.Armor:
            if (armor != null)
                gold += armor.value / 2;
            armor = item;
            break;
        default:
            AddItem(item, count);
            break;
        }
    }
}
