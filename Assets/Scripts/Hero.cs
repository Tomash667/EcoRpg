using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Hero : ISerializationCallbackReceiver
{
    public Dictionary<Skill, SkillEntry> skills;
    public List<ItemSlot> items = new();
    public List<SavedSkillEntry> savedSkills;
    public Item weapon, armor, shield;
    public string name, weaponName, armorName, shieldName;
    public Class clas;
    public int level, exp, hp, hpMax, attack, defense, dex, gold, rested, affection, bored, lastGift;
    public bool female, winToday, loseToday, questToday;

    [NonSerialized]
    public CharacterCard card;
    [NonSerialized]
    public int potionTimer, potionsUsed;
    [NonSerialized]
    public bool canBlock;

    public int Attack
    {
        get
        {
            int value = attack;
            if (weapon != null)
                value += weapon.power;
            float affectionMod = 1f + 0.05f * (affection / 25);
            value = (int)(value * affectionMod);
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
            if (shield != null)
                value += shield.power;
            float affectionMod = 1f + 0.05f * (affection / 25);
            value = (int)(value * affectionMod);
            return value;
        }
    }
    public int ExpP => exp / 10;
    public float hpp => ((float)hp) / hpMax;
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
    public bool BackRow => clas == Class.Archer;
    public char GenderSign => female ? '♀' : '♂';
    public virtual string nameYou => name;
    public string He => female ? "She" : "He";
    public virtual string him => female ? "her" : "him";

    public void Init(int startLevel = 0)
    {
        female = Utility.Rand % 2 == 0;
        name = (female ? Names.femaleNames : Names.maleNames).RandomItem();
        clas = ClassMethods.all.RandomItem();
        InitCommon();
        if (clas == Class.Warrior)
        {
            weapon = Item.Get("club");
            shield = Item.Get("wooden shield");
        }
        else
            weapon = Item.Get("short bow");
        armor = Item.Get("leather armor");
        if (startLevel > 0)
        {
            SetLevel(startLevel);
            int equipmentLevel = 1 + level / 2;
            int weaponLevel = equipmentLevel, armorLevel = equipmentLevel;
            if (level % 2 == 1)
            {
                if (Utility.Rand % 2 == 0)
                    ++weaponLevel;
                else
                    ++armorLevel;
            }
            if (weaponLevel != 1)
            {
                weapon = Item.items.First(x => x.type == Item.Type.Weapon && x.subtype == weapon.subtype && x.level == weaponLevel);
                if (shield != null)
                    shield = Item.items.First(x => x.type == Item.Type.Shield && x.level == weaponLevel);
            }
            if (armorLevel != 1)
                armor = Item.items.First(x => x.type == Item.Type.Armor && x.level == armorLevel);
        }
        AddItem(Item.Get("potion"));
        AddItem(Item.Get("rations"), 3);
    }

    protected void InitCommon()
    {
        skills = new();
        level = 1;
        hpMax = 100;
        hp = hpMax;
        if (clas == Class.Warrior)
        {
            attack = 25;
            defense = 5;
            dex = 10;
        }
        else
        {
            attack = 30;
            defense = 3;
            dex = 15;
        }
    }

    public void InitCombat()
    {
        potionTimer = 0;
        potionsUsed = 0;
        if (clas == Class.Warrior)
            canBlock = true;
        else
            canBlock = false;
    }

    public void SetLevel(int newLevel)
    {
        int dif = newLevel - level;
        float hpRatio = hpp;
        hpMax += dif * 20;
        hp = (int)(hpRatio * hpMax);
        attack += dif * 5;
        defense += dif;
        dex += dif * 2;
        level = newLevel;
    }

    public bool AddExp(List<Enemy> enemyList, float mod)
    {
        if (rested > 0)
            mod *= 1.1f;
        int newExp = 0;
        foreach (Enemy enemy in enemyList)
            newExp += GetExpReward(enemy.level);
        newExp = (int)(newExp * mod);
        exp += newExp;
        if (exp >= 1000)
        {
            exp -= 1000;
            ++level;
            float hpRatio = hpp;
            hpMax += 20;
            hp = (int)(hpRatio * hpMax);
            attack += 5;
            defense++;
            dex += 2;
            return true;
        }
        else
            return false;
    }

    private int GetExpReward(int enemyLevel)
    {
        int dif = level - enemyLevel;
        if (dif < 0)
            return 250 - dif * 50;
        return dif switch
        {
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

    public ItemSlot FindHealingItem()
    {
        return items.Where(x => x.item.type == Item.Type.Usable).OrderByDescending(x => x.item.power).FirstOrDefault();
    }

    public bool HaveItem(Item item)
    {
        return items.FirstOrDefault(x => x.item == item) != null;
    }

    public bool HaveItem(string name)
    {
        return items.FirstOrDefault(x => x.item.name == name) != null;
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

    public void AddItemIfMissing(string name)
    {
        Item item = Item.Get(name);
        if (!HaveItem(item))
            AddItem(item);
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
        shieldName = shield?.name;
        savedSkills = skills?.Select(kvp => new SavedSkillEntry { skill = kvp.Key, level = kvp.Value.level, train = kvp.Value.train }).ToList();
    }

    public virtual void OnAfterDeserialize()
    {
        if (!string.IsNullOrEmpty(weaponName))
            weapon = Item.Get(weaponName);
        if (!string.IsNullOrEmpty(armorName))
            armor = Item.Get(armorName);
        if (!string.IsNullOrEmpty(shieldName))
            shield = Item.Get(shieldName);
        skills = savedSkills?.ToDictionary(x => x.skill, x => new SkillEntry { level = x.level, train = x.train });
    }

    public bool WillTakeItem(Item item)
    {
        return item.type switch
        {
            Item.Type.Weapon => CanEquip(item) && (weapon == null || weapon.power < item.power),
            Item.Type.Armor => CanEquip(item) && (armor == null || armor.power < item.power),
            Item.Type.Shield => CanEquip(item) && (shield == null || shield.power < item.power),
            Item.Type.Usable => true,
            Item.Type.Other => item.name == "rations",
            _ => false
        };
    }

    public bool CanEquip(Item item)
    {
        switch (item.type)
        {
        case Item.Type.Weapon:
            if (clas == Class.Warrior)
                return item.subtype == Item.Subtype.Melee;
            else
                return item.subtype == Item.Subtype.Bow;
        case Item.Type.Shield:
            return clas == Class.Warrior;
        case Item.Type.Armor:
            return true;
        default:
            return false;
        }
    }

    public void GiveItem(Item item, int count = 1)
    {
        switch (item.type)
        {
        case Item.Type.Weapon:
            if (weapon != null)
            {
                if (Global.Location.IsSafe())
                    gold += weapon.value / 2;
                else
                    AddItem(weapon);
            }
            weapon = item;
            break;
        case Item.Type.Armor:
            if (armor != null)
            {
                if (Global.Location.IsSafe())
                    gold += armor.value / 2;
                else
                    AddItem(armor);
            }
            armor = item;
            break;
        case Item.Type.Shield:
            if (shield != null)
            {
                if (Global.Location.IsSafe())
                    gold += shield.value / 2;
                else
                    AddItem(shield);
            }
            shield = item;
            break;
        default:
            AddItem(item, count);
            break;
        }
    }

    public void BuyItems()
    {
        bool isCity = Global.World.Location != TileType.Village;

        // sell old items
        items.RemoveAll(x =>
        {
            if (x.item.type == Item.Type.Weapon || x.item.type == Item.Type.Armor || x.item.type == Item.Type.Shield)
            {
                gold += x.item.value * x.count / 2;
                return true;
            }
            return false;
        });

        // buy rations/potions
        Item rations = Item.Get("rations"), potion = Item.Get(hpMax >= 200 ? "elixir" : "potion"), elixir = Item.Get("elixir");
        Item healingItem = (hpMax >= 200 && isCity) ? elixir : potion;
        int rationsCount = CountItem(rations), potionsCount = CountItem(potion), elixirCount = CountItem(elixir);
        int healingItemCount = potionsCount + elixirCount;
        int requiredRations = 5 + level / 2;
        while ((rationsCount < requiredRations && gold >= rations.value) || (healingItemCount < 5 && gold >= healingItem.value))
        {
            if (rationsCount < requiredRations)
            {
                gold -= rations.value;
                AddItem(rations);
                ++rationsCount;
            }

            if (healingItemCount < 5 && gold >= healingItem.value)
            {
                gold -= healingItem.value;
                AddItem(healingItem);
                ++healingItemCount;
            }
        }

        // buy weapon/armor/shield
        int maxLevel = isCity ? Item.MaxLevelCity : Item.MaxLevelVillage;
        int weaponLevel = weapon?.level ?? 0, armorLevel = armor?.level ?? 0, shieldLevel = shield?.level ?? 0;
        bool boughtWeapon = false, boughtArmor = false, boughtShield = false;
        Item.Subtype weaponSubtype;
        if (clas == Class.Warrior)
            weaponSubtype = Item.Subtype.Melee;
        else
        {
            weaponSubtype = Item.Subtype.Bow;
            shieldLevel = maxLevel;
        }

        while (weaponLevel < maxLevel || armorLevel < maxLevel || shieldLevel < maxLevel)
        {
            int minLevel = Mathf.Min(weaponLevel, armorLevel, shieldLevel);
            List<Item.Type> typesToBuy = new();
            if (weaponLevel == minLevel)
                typesToBuy.Add(Item.Type.Weapon);
            if (armorLevel == minLevel)
                typesToBuy.Add(Item.Type.Armor);
            if (shieldLevel == minLevel)
                typesToBuy.Add(Item.Type.Shield);

            switch (typesToBuy.RandomItem())
            {
            case Item.Type.Weapon:
                {
                    Item nextWeapon = Item.items.First(x => x.type == Item.Type.Weapon && x.subtype == weaponSubtype && x.level == weaponLevel + 1);

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
                        return; // can't afford
                }
                break;
            case Item.Type.Armor:
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
                        return; // can't afford
                }
                break;
            case Item.Type.Shield:
                {
                    Item nextShield = Item.items.First(x => x.type == Item.Type.Shield && x.level == shieldLevel + 1);

                    // include resell of old shield
                    int tmpGold = gold;
                    if (shield != null)
                    {
                        if (boughtShield)
                            tmpGold += shield.value;
                        else
                            tmpGold += shield.value / 2;
                    }

                    if (tmpGold >= nextShield.value)
                    {
                        // buy
                        gold = tmpGold - nextShield.value;
                        shield = nextShield;
                        ++shieldLevel;
                        boughtShield = true;
                    }
                    else
                        return; // can't afford
                }
                break;
            }
        }
    }

    public void EnchantItems()
    {
        int weaponLevel = weapon?.level ?? Item.MaxLevelEnchant,
            armorLevel = armor?.level ?? Item.MaxLevelEnchant,
            shieldLevel = shield?.level ?? Item.MaxLevelEnchant;
        while (weaponLevel < Item.MaxLevelEnchant || armorLevel < Item.MaxLevelEnchant || shieldLevel < Item.MaxLevelEnchant)
        {
            int minLevel = Mathf.Min(weaponLevel, armorLevel, shieldLevel);
            List<Item.Type> typesToBuy = new();
            if (weaponLevel == minLevel)
                typesToBuy.Add(Item.Type.Weapon);
            if (armorLevel == minLevel)
                typesToBuy.Add(Item.Type.Armor);
            if (shieldLevel == minLevel)
                typesToBuy.Add(Item.Type.Shield);

            switch (typesToBuy.RandomItem())
            {
            case Item.Type.Weapon:
                {
                    int cost = weapon.GetEnchantCost();
                    if (gold >= cost)
                    {
                        // enchant
                        gold -= cost;
                        weapon = weapon.GetEnchanted();
                        weaponLevel = weapon.level;
                    }
                    else
                        return; // can't afford
                }
                break;
            case Item.Type.Armor:
                {
                    int cost = armor.GetEnchantCost();
                    if (gold >= cost)
                    {
                        // enchant
                        gold -= cost;
                        armor = armor.GetEnchanted();
                        armorLevel = armor.level;
                    }
                    else
                        return; // can't afford
                }
                break;
            case Item.Type.Shield:
                {
                    int cost = shield.GetEnchantCost();
                    if (gold >= cost)
                    {
                        // enchant
                        gold -= cost;
                        shield = shield.GetEnchanted();
                        shieldLevel = shield.level;
                    }
                    else
                        return; // can't afford
                }
                break;
            }
        }
    }

    public void ApplyHealing()
    {
        if (hpp < 0.5f)
        {
            ItemSlot potion = FindHealingItem();
            if (potion != null)
            {
                hp = Mathf.Min(hp + potion.item.power, hpMax);
                RemoveItem(potion);
            }
        }
    }

    public int GetSkill(Skill skill)
    {
        if (skills.TryGetValue(skill, out SkillEntry skillEntry))
            return skillEntry.level;
        else
            return 0;
    }

    private SkillEntry GetSkillEntry(Skill skill)
    {
        if (!skills.TryGetValue(skill, out SkillEntry skillEntry))
        {
            skillEntry = new();
            skills[skill] = skillEntry;
        }
        return skillEntry;
    }

    public string Train(Skill skill, float mod = 1f)
    {
        SkillEntry skillEntry = GetSkillEntry(skill);
        if (skillEntry.level >= 100)
            return string.Empty;

        bool increased = false;
        float required = CalculateRequiredSkillTrain(skillEntry.level);
        skillEntry.train += 5f * mod;
        while (skillEntry.train >= required && skillEntry.level != 100)
        {
            ++skillEntry.level;
            skillEntry.train -= required;
            required = CalculateRequiredSkillTrain(skillEntry.level);
            increased = true;
        }

        if (increased)
            return $" Your {skill.AsString()} skill increased to {skillEntry.level}.";
        else
            return string.Empty;
    }

    private float CalculateRequiredSkillTrain(int value)
    {
        float valueFloat = (float)value;
        return (value / 20) switch
        {
            0 => 1f + (1.25f - 1f) * (valueFloat / 20),
            1 => 1.25f + (1.666666666f - 1.25f) * ((valueFloat - 20) / 20),
            2 => 1.666666666f + (2.5f - 1.666666666f) * ((valueFloat - 40) / 20),
            3 => 2.5f + (5f - 2.5f) * ((valueFloat - 60) / 20),
            _ => 5f
        };
    }

    public int ValueToAffectionGain(int value)
    {
        if (value >= 25000)
            return 5;
        else if (value >= 5000)
            return 4;
        else if (value >= 1000)
            return 3;
        else if (value >= 250)
            return 2;
        else if (value >= 50)
            return 1;
        else
            return 0;
    }
}
