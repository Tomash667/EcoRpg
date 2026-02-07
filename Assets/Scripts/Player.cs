using System;

[Serializable]
public class Player : Hero
{
    public int energy;

    public new void Init()
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
}
