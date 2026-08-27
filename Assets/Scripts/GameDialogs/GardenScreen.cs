using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GardenScreen : GameDialog
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
            Work();
    }

    public override void Refresh()
    {
        Transform content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        Property property = game.GetPropertyInside();
        string[] choices = new string[] { "---", "Vegetables (10 gold)", "Herbs (10 herbs)", "Rare herbs (10 herbs)" };

        for (int i = 0; i < property.gardenPlants.Count; ++i)
        {
            int index = i;
            string plant = property.gardenPlants[i];
            if (plant == "")
                plant = "Empty";
            DropdownEntry dropdownEntry = Instantiate(ui.dropdownEntryPrefab, content).GetComponent<DropdownEntry>();
            dropdownEntry.Init($"Plot {i + 1}: {plant}", "Change", choices, x =>
            {
                switch (x)
                {
                case 1:
                    // vegetables
                    if (plant == "Vegetables")
                        ui.ShowDialog("Vegetables are already planted here.");
                    else if (player.gold < 10)
                        ui.ShowDialog("You need 10 gold.");
                    else
                    {
                        game.AddText("You plant vegetables.");
                        player.AddGold(-10);
                        property.gardenPlants[index] = "Vegetables";
                        Refresh();
                        game.UpdateText();
                    }
                    break;
                case 2:
                    // herbs
                    Item herb = Item.Get("herb");
                    if (plant == "Herbs")
                        ui.ShowDialog("Herbs are already planted here.");
                    else if (player.CountItem(herb) < 10)
                        ui.ShowDialog("You need 10 herbs.");
                    else
                    {
                        game.AddText("You plant herbs.");
                        player.RemoveItem(herb, 10);
                        property.gardenPlants[index] = "Herbs";
                        Refresh();
                    }
                    break;
                case 3:
                    // rare herbs
                    Item rareHerb = Item.Get("rare herb");
                    if (plant == "Rare herbs")
                        ui.ShowDialog("Rare herbs are already planted here.");
                    else if (player.CountItem(rareHerb) < 10)
                        ui.ShowDialog("You need 10 rare herbs.");
                    else
                    {
                        game.AddText("You plant rare herbs.");
                        player.RemoveItem(rareHerb, 10);
                        property.gardenPlants[index] = "Rare herbs";
                        Refresh();
                    }
                    break;
                }
            });
        }
    }

    public void Work()
    {
        Property property = game.GetPropertyInside();
        if (property.farmedToday)
            ui.ShowDialog("You already farmed today.");
        else if (!property.gardenPlants.Any(x => !string.IsNullOrEmpty(x)))
            ui.ShowDialog("You need to plant something first.");
        else if (game.hour > 16)
            ui.ShowDialog("It's too late to work.");
        else if (player.energy < 50)
            ui.ShowDialog("You are too tired to work.");
        else
        {
            DoWork(property, text, null);
            ui.CloseDialog();
            game.AddTime(hours: 8);
            game.UpdateText();
        }
    }

    public void DoWork(Property property, TextBuilder text, List<ItemSlot> produceList)
    {
        (Hero bestHero, int bestValue) = game.team.GetSkill(Skill.Farming);
        float mod;
        if (bestValue >= 100)
            mod = 2;
        else if (bestValue >= 75)
            mod = 1.5f;
        else if (bestValue >= 50)
            mod = 1.25f;
        else
            mod = 1f;

        List<string> items = new();
        foreach (var plant in property.gardenPlants.Where(x => !string.IsNullOrEmpty(x)).GroupBy(x => x).Select(x => (name: x.Key, count: x.Count())))
        {
            int count = (int)(mod * plant.count);
            Item item = PlantNameToItem(plant.name);
            player.AddItem(item, count);
            if (produceList != null)
            {
                ItemSlot itemSlot = produceList.FirstOrDefault(x => x.item == item);
                if (itemSlot != null)
                    itemSlot.count += count;
                else
                    produceList.Add(new() { item = item, count = count });
            }
            else
                items.Add(Utility.Plural(item.name, count));
        }

        if (bestHero == null || bestHero == player)
        {
            text?.Set($"You work in garden and produce {Utility.PrettyList(items)}."); // article
            player.Train(Skill.Farming, text);
        }
        else
        {
            text?.Set($"You and {bestHero.name} work in garden and produce {Utility.PrettyList(items)}.");
            bestHero.Train(Skill.Farming, null);
            float trainMod = 1f + 0.01f * (bestValue - player.GetSkill(Skill.Farming));
            player.Train(Skill.Farming, text, trainMod);
        }
        player.energy -= 50;
        property.farmedToday = true;
    }

    public static Item PlantNameToItem(string name)
    {
        return name switch
        {
            "Vegetables" => Item.Get("ration"),
            "Herbs" => Item.Get("herb"),
            "Rare herbs" => Item.Get("rare herb"),
            _ => null
        };
    }
}
