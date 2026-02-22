using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Player : Hero
{
    [NonSerialized]
    public List<Property> properties;
    public List<string> savedProperties;
    public int goldWaiting, energy;
    [NonSerialized]
    public int goldReceived;
    public Dictionary<Skill, int> skills;
    public List<SkillEntry> savedSkills;

    public new void Init()
    {
        properties = new();
        skills = new();
        level = 1;
        exp = 0;
        hpMax = 100;
        hp = hpMax;
        energy = 100;
        gold = 50;
        attack = 25;
        defense = 5;
        dex = 10;
    }

    public void AddGold(int value)
    {
        gold += value;
        goldReceived += value;
    }

    public override void OnBeforeSerialize()
    {
        base.OnBeforeSerialize();
        savedProperties = properties?.Select(x => x.name).ToList();
        savedSkills = skills?.Select(kvp => new SkillEntry { skill = kvp.Key, level = kvp.Value }).ToList();
    }

    public override void OnAfterDeserialize()
    {
        base.OnAfterDeserialize();
        properties = savedProperties?.Select(x => Property.Get(x)).ToList();
        skills = savedSkills?.ToDictionary(x => x.skill, x => x.level);
    }

    public bool HaveProperty(string name)
    {
        return properties.Any(x => x.name == name);
    }

    public int GetSkill(Skill skill)
    {
        return skills.GetValueOrDefault(skill);
    }

    public void Train(Skill skill, int value, ref string text)
    {
        int currentLevel = GetSkill(skill);
        int gain = Mathf.Min(value, 100 - currentLevel);
        if (gain > 0)
        {
            skills[skill] = currentLevel + gain;
            text += $" Your {skill.AsString()} skill increased to {currentLevel + gain}.";
        }
    }
}
