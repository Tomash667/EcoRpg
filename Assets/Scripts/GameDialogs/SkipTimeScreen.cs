using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class SkipTimeScreen : GameDialog
{
    public GardenScreen gardenScreen;
    public PropertiesScreen propertiesScreen;

    private string lastAction;
    private int lastDays = 1;

    public override bool Autoclose => false;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            DoSkipTime();
    }

    public override void Show()
    {
        List<string> options = new();
        TileType location = game.world.Location;
        if (location == TileType.City || location == TileType.Village || location == TileType.Mine || location == TileType.Sawmill || location == TileType.Farm)
            options.Add("Work");
        if ((location == TileType.House && player.HavePropertyUpgrade("House", "Garden", game.world.CityIndex))
            || (location == TileType.Mansion && player.HavePropertyUpgrade("Mansion", "Garden", game.world.CityIndex)))
            options.Add("Work in garden");
        if (game.GetPropertyHere() != null)
            options.Add("Manage");
        if (location == TileType.City)
            options.Add("Train");
        options.Add("Relax");
        TMP_Dropdown dropdown = transform.Find("Dropdown").GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        int index = 0;
        if (!string.IsNullOrEmpty(lastAction))
        {
            index = options.IndexOf(lastAction);
            if (index == -1)
                index = 0;
        }
        dropdown.value = index;
        transform.Find("Input").GetComponent<TMP_InputField>().text = lastDays.ToString();
        ui.ShowDialog(gameObject);
    }

    public void DoSkipTime()
    {
        int days = int.Parse(transform.Find("Input").GetComponent<TMP_InputField>().text);
        if (days < 1 || days > 30)
        {
            ui.ShowDialog("Invalid number of days to skip.");
            return;
        }

        string action = transform.Find("Dropdown").GetComponent<TMP_Dropdown>().captionText.text;
        lastAction = action;
        lastDays = days;

        List<Hero> levelups = null;
        List<ItemSlot> produce = null;
        Dictionary<Skill, int> prevSkills = player.skills.ToDictionary(x => x.Key, x => x.Value.level);
        Tile tile = game.world.CurrentTile;
        Property property = null;
        int skippedDays = 0;
        int payment = 0;
        int prevEfficiency = 0;

        if (action == "Manage")
        {
            property = game.GetPropertyHere();
            prevEfficiency = property.efficiency;
        }
        else if (action == "Work in garden")
        {
            property = game.GetPropertyHere();
            if (!property.gardenPlants.Any(x => !string.IsNullOrEmpty(x)))
            {
                ui.ShowDialog("You need to plant something first.");
                return;
            }
            produce = new();
        }

        // skip first day if tired
        if (((action == "Work" || action == "Work in garden" || action == "Train") && (player.energy < 50 || game.hour > 16))
            || (action == "Manage" && (player.energy < 25 || game.hour > 16))
            || (action == "Work in garden" && property.farmedToday))
        {
            game.OnRest(true);
            --days;
            ++skippedDays;
        }

        while (days > 0)
        {
            switch (action)
            {
            case "Train":
                foreach (Hero hero in game.team.heroes)
                {
                    if (hero.AddExp(100))
                    {
                        levelups ??= new();
                        levelups.Add(hero);
                    }
                }
                break;
            case "Work":
                payment += game.DoWork(true);
                break;
            case "Work in garden":
                gardenScreen.DoWork(property, null, produce);
                break;
            case "Manage":
                propertiesScreen.DoManage(property, null, false);
                break;
            }

            game.OnRest(true);
            --days;
            ++skippedDays;

            if (!tile.CanSkipTime())
                break;
        }

        string verb;
        if (action == "Manage")
            verb = $"managing the {property.name.ToLower()}";
        else if (action == "Work in garden")
            verb = "working in garden";
        else
            verb = action.ToLower() + "ing";
        TextBuilder text = game.Text;
        text.Set($"You spend {Utility.Plural("day", skippedDays)} {verb}.");

        if (payment > 0)
            text.Append($"You earned <color=#FFD700>{payment}</color> gold.");

        if (produce != null)
            text.Append($"You produced {Utility.PrettyList(produce.Select(x => Utility.Plural(x.item.name, x.count)))}.");

        if (levelups != null)
        {
            foreach (var group in levelups.GroupBy(x => x.level))
            {
                string isAre = group.Count() > 1 || group.First() == player ? "are" : "is";
                text.Append($"{Utility.PrettyList(group.Select(x => x.nameYou)).ToUpper1()} {isAre} now level {group.Key}.");
            }
        }

        if (property != null)
        {
            if (property.efficiency > prevEfficiency)
                text.Append($"Efficiency increased by {property.efficiency - prevEfficiency}.");
            else if (property.efficiency < prevEfficiency)
                text.Append($"Efficiency decreased by {prevEfficiency - property.efficiency}.");
        }

        foreach (KeyValuePair<Skill, SkillEntry> sk in player.skills)
        {
            if (!prevSkills.TryGetValue(sk.Key, out int prevValue) || prevValue < sk.Value.level)
                text.Append($"Your {sk.Key.AsString()} skill increased to {sk.Value.level}.");
        }

        game.team.CheckBoredAllies(text);

        if (player.goldWaiting != 0 && tile.type.IsSafe())
        {
            text.Append(player.goldWaiting > 0
                ? $"You receive <color=#FFD700>{player.goldWaiting}</color> gold from your properties."
                : $"You pay <color=#FFD700>{-player.goldWaiting}</color> gold for your properties.");
            player.AddGold(player.goldWaiting);
            player.goldWaiting = 0;
        }

        if (!tile.CanSkipTime())
        {
            text.Append($"Monsters <b>attacked</b> the {tile.Name}.");
            game.UpdateButtons();
        }

        ui.CloseDialog();
        game.UpdateText();
    }
}
