using System.Linq;
using TMPro;
using UnityEngine;

public class CraftScreen : GameDialog
{
    public override void Refresh()
    {
        // text
        TextBuilder text = game.Text;
        transform.Find("Text").GetComponent<TMP_Text>().text = text.Flush();

        // ingredients
        Transform content = transform.Find("Ingredients/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (ItemSlot itemSlot in player.items.Where(x => x.item.subtype == Item.Subtype.Ingredient))
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToStringShort());
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }

        // potions
        (Hero bestHero, int alchemy) = game.team.GetSkill(Skill.Alchemy);
        int bonus = 0;
        World world = game.world;
        if ((world.Location == TileType.House && player.HavePropertyUpgrade("House", "Alchemy lab", world.CityIndex))
            || (world.Location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Alchemy lab", world.CityIndex)))
        {
            bonus = 25;
            alchemy += 25;
        }
        content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        void Brew(Recipe recipe, int count)
        {
            player.RemoveItem(recipe.ingredient, count * 2);
            int extra = (int)(count * GetAlchemyCountBonus(alchemy));
            int batches = count / 5;
            if (count % 5 != 0)
                ++batches;
            player.AddItem(recipe.result, count + extra);
            if (bestHero == null || bestHero == player)
            {
                text.Set($"You created {Utility.Plural(recipe.result.name, count + extra)}.");
                player.Train(Skill.Alchemy, text, recipe.trainMod * count);
            }
            else
            {
                text.Set($"You and {bestHero.name} created {Utility.Plural(recipe.result.name, count + extra)}.");
                if (batches == 1)
                {
                    float trainMod = 1f + 0.01f * (alchemy - bonus - player.GetSkill(Skill.Alchemy));
                    bestHero.Train(Skill.Alchemy, null, recipe.trainMod * count);
                    player.Train(Skill.Alchemy, text, recipe.trainMod * trainMod * count);
                }
                else
                {
                    int prevSkill = player.GetSkill(Skill.Alchemy);
                    for (int i = 0; i < batches; ++i)
                    {
                        int currentCount = Mathf.Min(count, 5);
                        count -= currentCount;
                        float trainMod = 1f + 0.01f * (bestHero.GetSkill(Skill.Alchemy) - player.GetSkill(Skill.Alchemy));
                        bestHero.Train(Skill.Alchemy, null, recipe.trainMod * currentCount);
                        player.Train(Skill.Alchemy, null, recipe.trainMod * trainMod * currentCount);
                    }
                    player.CheckSkillIncrease(Skill.Alchemy, prevSkill, text);
                }
            }

            game.AddTime(minutes: batches * 30);
            RefreshIfOpen();
            game.UpdateText();
        }

        foreach (Recipe recipe in Recipe.GetAvailable(alchemy))
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(recipe.ToString(player.CountItem(recipe.result)), "Brew", () =>
            {
                int possible = player.CountItem(recipe.ingredient) / recipe.ingredientCount;
                if (possible == 0)
                {
                    ui.ShowDialog($"You need {Utility.Plural(recipe.ingredient.name, recipe.ingredientCount)} to brew {recipe.result.name}.");
                    return;
                }

                if (Input.GetKey(KeyCode.LeftShift))
                    Brew(recipe, possible);
                else if (Input.GetKey(KeyCode.LeftControl))
                {
                    ui.ShowInput($"How many {Utility.Plural(recipe.result.name)} to brew (1-{possible})?", count =>
                    {
                        if (count <= 0)
                            return true;
                        Brew(recipe, Mathf.Min(count, possible));
                        return true;
                    });
                }
                else
                    Brew(recipe, 1);
            });
            itemEntry.SetImage(ui.itemIcons[(int)recipe.result.GetIcon()]);
        }
    }

    public static float GetAlchemyCountBonus(int skill)
    {
        if (skill >= 100)
            return 1;
        else if (skill >= 75)
            return 0.5f;
        else if (skill >= 50)
            return 0.25f;
        else if (skill >= 25)
            return 0.1f;
        else
            return 0;
    }
}
