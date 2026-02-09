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

    public bool HaveItem(string name)
    {
        return FindItem(name) != null;
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

    public virtual void OnBeforeSerialize()
    {
        weaponName = weapon?.name;
        armorName = armor?.name;
    }

    public virtual void OnAfterDeserialize()
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
        switch (item.type)
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

    public void BuyItems()
    {
        // buy rations/potions
        Item rations = Item.Get("rations"), potion = Item.Get("potion");
        int rationsCount = CountItem(rations), potionsCount = CountItem(potion);
        while ((rationsCount < 5 && gold >= rations.value) || (potionsCount < 5 && gold >= potion.value))
        {
            if (rationsCount < 5)
            {
                gold -= rations.value;
                AddItem(rations);
                ++rationsCount;
            }

            if (potionsCount < 5 && gold >= potion.value)
            {
                gold -= potion.value;
                AddItem(potion);
                ++potionsCount;
            }
        }

        // buy weapon/armor
        int weaponLevel = weapon?.level ?? 0, armorLevel = armor?.level ?? 0;
        bool boughtWeapon = false, boughtArmor = false;
        while (weaponLevel < Item.MaxLevel || armorLevel < Item.MaxLevel)
        {
            if (weaponLevel < armorLevel || (weaponLevel == armorLevel && Utility.Rand % 2 == 0))
            {
                Item nextWeapon = Item.items.First(x => x.type == Item.Type.Weapon && x.level == weaponLevel + 1);

                // include resell of old weapon
                int tmpGold = gold;
                if (weapon != null)
                {
                    if (boughtWeapon)
                        tmpGold += weapon.value;
                    else
                        tmpGold += weapon.value / 2;
                }

                if (tmpGold >= nextWeapon.value)
                {
                    // buy
                    gold = tmpGold - nextWeapon.value;
                    weapon = nextWeapon;
                    ++weaponLevel;
                    boughtWeapon = true;
                }
                else
                    break; // can't afford
            }
            else
            {
                Item nextArmor = Item.items.First(x => x.type == Item.Type.Armor && x.level == armorLevel + 1);

                // include resell of old armor
                int tmpGold = gold;
                if (armor != null)
                {
                    if (boughtArmor)
                        tmpGold += armor.value;
                    else
                        tmpGold += armor.value / 2;
                }

                if (tmpGold >= nextArmor.value)
                {
                    // buy
                    gold = tmpGold - nextArmor.value;
                    armor = nextArmor;
                    ++armorLevel;
                    boughtArmor = true;
                }
                else
                    break; // can't afford
            }
        }
    }
}
