using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GardenScreen : GameDialog
{
    private static readonly Item[] plantChoices = new[]
    {
        null, // empty
        Item.Get("ration"), // vegetables
        Item.Get("herb"),
        Item.Get("mana herb"),
        Item.Get("rare herb")
    };

    private static readonly string[] plantChoicesString = plantChoices.Select(plant =>
    {
        if (plant == null)
            return "---";

        if (plant.name == "ration")
            return "Vegetables (10 gold)";

        return $"{Utility.Plural(plant.name.ToUpper1())} (10 herbs)";
    }).ToArray();

    public override void UpdateDialog()
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

        for (int i = 0; i < property.gardenPlants.Count; ++i)
        {
            int index = i;
            string currentPlant = property.gardenPlants[i];
            if (currentPlant == "")
                currentPlant = "Empty";
            else if (currentPlant == "ration")
                currentPlant = "Vegetables";
            else
                currentPlant = Utility.Plural(currentPlant.ToUpper1());

            DropdownEntry dropdownEntry = Instantiate(ui.dropdownEntryPrefab, content).GetComponent<DropdownEntry>();
            dropdownEntry.Init($"Plot {i + 1}: {currentPlant}", "Change", plantChoicesString, x =>
            {
                Item newPlant = plantChoices[x];
                if (newPlant == null)
                    return; // empty - do nothing

                if (newPlant.name == "ration")
                {
                    // vegetables
                    if (property.gardenPlants[index] == "ration")
                        ui.ShowDialog("Vegetables are already planted here.");
                    else if (player.gold < 10)
                        ui.ShowDialog("You need 10 gold.");
                    else
                    {
                        game.AddText("You plant vegetables.");
                        player.AddGold(-10);
                        property.gardenPlants[index] = "ration";
                        Refresh();
                        game.UpdateText();
                    }
                }
                else
                {
                    // some herb
                    if (property.gardenPlants[index] == newPlant.name)
                        ui.ShowDialog($"{Utility.Plural(newPlant.name.ToUpper1())} are already planted here.");
                    else if (player.CountItem(newPlant) < 10)
                        ui.ShowDialog($"You need 10 {Utility.Plural(newPlant.name)}.");
                    else
                    {
                        game.AddText($"You plant {Utility.Plural(newPlant.name)}.");
                        player.RemoveItem(newPlant, 10);
                        property.gardenPlants[index] = newPlant.name;
                        Refresh();
                    }
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
            Item item = Item.Get(plant.name);
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
}
