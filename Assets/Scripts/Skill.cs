using System;

public enum Skill
{
    None = -1,
    Alchemy,
    Mining,
    Woodcraft,
    Management,
    Forage
}

public class SkillEntry
{
    public int level;
    public float train;
}

[Serializable]
public class SavedSkillEntry
{
    public Skill skill;
    public int level;
    public float train;
}

public static class SkillMethods
{
    public static string AsString(this Skill skill)
    {
        return skill switch
        {
            Skill.Alchemy => "alchemy",
            Skill.Mining => "mining",
            Skill.Woodcraft => "woodcraft",
            Skill.Management => "management",
            Skill.Forage => "forage",
            _ => $"[ERROR skill {skill}]"
        };
    }
}
