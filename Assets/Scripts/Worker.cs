using System;

[Serializable]
public class Worker
{
    public string name;
    public float train;
    public int skill, locationIndex;
    public bool female;

    public int Cost => 250 + 10 * skill;

    public string ToStringHire()
    {
        return $"{name} ({skill} management skill, {Cost} gold and 2 upkeep)";
    }

    public string ToStringShort()
    {
        return $"{name} ({skill})";
    }

    public string ToStringHired(string location)
    {
        return location != null ? $"{name} ({location}, {skill} management skill)" : $"{name} ({skill} management skill)";
    }

    public void Train()
    {
        if (skill >= 100)
            return;

        float required = Hero.CalculateRequiredSkillTrain(skill);
        train += 5f;
        while (train >= required && skill != 100)
        {
            ++skill;
            train -= required;
            required = Hero.CalculateRequiredSkillTrain(skill);
        }
    }
}
