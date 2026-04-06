using System.Collections.Generic;
using System.Linq;

public class Recipe
{
    public string name;
    public Item result, ingredient;
    public float trainMod;
    public int ingredientCount, requiredSkill;

    public string ToString(int owned)
    {
        return $"{result.name.ToUpper1()} ({result.desc}, {ingredientCount}x {ingredient.name}, {owned} owned)";
    }

    public static IEnumerable<Recipe> GetAvailable(int skill)
    {
        return recipes.Where(x => skill >= x.requiredSkill);
    }

    public static readonly Recipe[] recipes = new Recipe[]
    {
        new()
        {
            result = Item.Get("potion"),
            ingredient = Item.Get("herb"),
            ingredientCount = 2,
            trainMod = 0.2f
        },
        new()
        {
            result = Item.Get("elixir"),
            ingredient = Item.Get("rare herb"),
            ingredientCount = 2,
            trainMod = 0.4f,
            requiredSkill = 25
        }
    };
}
