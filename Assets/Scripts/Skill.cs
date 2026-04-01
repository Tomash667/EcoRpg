using System;

public enum Skill
{
    None = -1,
    Alchemy,
    Mining,
    Woodcraft
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
            _ => $"[ERROR skill {skill}]"
        };
    }
}
