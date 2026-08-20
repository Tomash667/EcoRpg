using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Properties : GameDialog
{
    private Property selectedProperty;
    private bool manageProperty;

    public Property SelectedProperty => selectedProperty;

    private void Update()
    {
        bool canManage = manageProperty || ((game.world.Location == TileType.City || game.world.Location == TileType.Mansion)
            && selectedProperty != null && selectedProperty.income > 0 && player.HavePropertyUpgrade("Mansion", "Office"));
        if (Input.GetKeyDown(KeyCode.M) && canManage)
            Manage();
        if (Input.GetKeyDown(KeyCode.P))
            game.ManageWorkers();
    }

    public override void Show()
    {
        if (!game.CloseManagePeopleIfOpen())
            selectedProperty = null;
        manageProperty = false;
        Refresh();
        RefreshDetails();
        transform.Find("List").gameObject.SetActive(true);
        transform.Find("BtManage").GetComponent<Button>().interactable = false;
        ui.ShowDialog(gameObject);
    }

    public void ShowManage()
    {
        selectedProperty = game.GetPropertyHere();
        manageProperty = true;
        RefreshDetails();
        transform.Find("List").gameObject.SetActive(false);
        transform.Find("BtManage").GetComponent<Button>().interactable = true;
        ui.ShowDialog(gameObject);
    }

    protected override void Refresh()
    {
        transform.Find("Text").GetComponent<TMP_Text>().text = game.Text.Flush();

        ItemEntryList list = transform.Find("List").GetComponent<ItemEntryList>();
        list.Clear();
        Transform content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        int cityIndex = game.world.CityIndex;
        Property[] propertiesToBuy = game.properties.Where(x => x.status != Property.Status.None && (x.cityIndex == -1 || x.cityIndex == cityIndex))
            .OrderBy(x => x.value).ThenBy(x => x.Name).ToArray();

        // player properties
        if (player.properties.Count > 0)
        {
            ui.AddTextHeader("Your properties:", content);
            foreach (Property property in player.properties.OrderBy(x => x.value).ThenBy(x => x.Name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.SetImage(ui.propertyIcons[(int)property.GetImage()]);
                itemEntry.data = property;
                itemEntry.canSelect = true;
                if (property == selectedProperty)
                    list.Select(itemEntry);

                if (property.status == Property.Status.Building)
                    itemEntry.Init(property.ToString(Property.DescStatus.Building));
                else if (property.HaveEvent("Infested"))
                    itemEntry.Init(property.ToString(Property.DescStatus.Infested));
                else
                {
                    itemEntry.Init(property.ToString(Property.DescStatus.Sell), "Sell", () =>
                    {
                        game.properties.Add(property);
                        player.AddGold(property.value / 2);
                        player.properties.Remove(property);
                        int locationIndex = game.GetLocationIndex(property);
                        Worker worker = game.hiredWorkers.FirstOrDefault(x => x.locationIndex == locationIndex);
                        if (worker != null)
                            worker.locationIndex = -1;
                        property.events.Clear();
                        game.Text.Set($"You sell {property.Name.ToLower()} for <color=#FFD700>{property.value / 2}</color> gold.");
                        if (property.name == "House" || property.name == "Mansion" || (property.name == "Inn" && property.cityIndex == game.world.CityIndex))
                            game.UpdateButtons();
                        if (property.name == "Horses" || property.name == "Mansion")
                            game.freshHorses = 0;
                        game.AddTime(minutes: 30);
                        if (IsOpen)
                        {
                            if (selectedProperty == property)
                            {
                                selectedProperty = null;
                                RefreshDetails();
                            }
                            Refresh();
                        }
                        game.UpdateText();
                    });
                }
            }

            if (propertiesToBuy.Length > 0)
                Instantiate(ui.lineSeparatorPrefab, content);
        }

        // available properties
        if (propertiesToBuy.Length > 0)
            ui.AddTextHeader("Available properties:", content);
        foreach (Property property in propertiesToBuy)
        {
            bool build = property.status == Property.Status.Cleared;
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(property.ToString(build ? Property.DescStatus.Build : Property.DescStatus.Buy), build ? "Build" : "Buy", () =>
            {
                int cost = build ? property.BuildPrice : property.value;
                if (player.gold < cost)
                {
                    ui.ShowDialog($"You need {cost} gold to {(build ? "build" : "buy")} {property.Name.ToLower()}.");
                    return;
                }

                if ((property.name == "House" && player.HaveProperty("Mansion", cityIndex: cityIndex))
                    || (property.name == "Mansion" && player.HaveProperty("House", cityIndex: cityIndex)))
                {
                    ui.ShowDialog("You can't own both a house and a mansion. It's a law!");
                    return;
                }

                player.AddGold(-cost);
                player.properties.Add(property);
                game.properties.Remove(property);
                if (build)
                {
                    game.Text.Set($"You pay <color=#FFD700>{cost}</color> gold to build {property.Name.ToLower()}.");
                    property.status = Property.Status.Building;
                    game.world.GetLocation(property.locationIndex).timer = 0; // prevent resetting
                }
                else
                {
                    game.Text.Set($"You buy {property.Name.ToLower()} for <color=#FFD700>{cost}</color> gold.");

                    // remove quests assigned to this location
                    if (property.locationIndex != -1)
                    {
                        Quest quest = game.activeQuests.FirstOrDefault(x => x.type == Quest.Type.Clear && x.location == property.locationIndex);
                        if (quest != null)
                        {
                            game.Text.Append($"Quest '{quest.Title}' is reassigned to other party.");
                            game.RemoveQuest(quest);
                        }
                        game.availableQuests.RemoveAll(x => x.type == Quest.Type.Clear && x.location == property.locationIndex);
                    }
                }

                if (property.name == "House" || property.name == "Mansion")
                {
                    game.UpdateButtons();
                    property.storedItems = new();
                    int size = property.name == "House" ? 2 : 6;
                    property.gardenPlants = new();
                    for (int i = 0; i < size; ++i)
                        property.gardenPlants.Add(string.Empty);
                }
                else if (property.name == "Inn")
                {
                    if (property.cityIndex == 1)
                    {
                        if (game.spiderStatus == Game.SpiderStatus.Accepted)
                        {
                            Quest quest = game.activeQuests.First(x => x.type == Quest.Type.UniqueSpider);
                            game.Text.Append($"Quest '{quest.Title}' is canceled.");
                            game.RemoveQuest(quest);
                        }
                        game.spiderStatus = Game.SpiderStatus.Skipped;
                    }
                    game.UpdateButtons();
                }

                game.AddTime(minutes: 30);
                if (IsOpen)
                {
                    selectedProperty = property;
                    Refresh();
                    RefreshDetails();
                }
                game.UpdateText();
            });
            itemEntry.SetImage(ui.propertyIcons[(int)property.GetImage()]);
        }
    }

    public void RefreshDetails()
    {
        if (manageProperty)
            transform.Find("Text").GetComponent<TMP_Text>().text = game.Text.Flush();
        else
        {
            ItemEntryList list = transform.Find("List").GetComponent<ItemEntryList>();
            selectedProperty = list.GetSelectedData() as Property;
            bool canManage = (game.world.Location == TileType.City || game.world.Location == TileType.Mansion)
                && selectedProperty != null && selectedProperty.income > 0 && player.HavePropertyUpgrade("Mansion", "Office");
            transform.Find("BtManage").GetComponent<Button>().interactable = canManage;
        }

        string str;
        if (selectedProperty == null)
            str = string.Empty;
        else if (selectedProperty.status == Property.Status.Building)
            str = $"<b>{selectedProperty.Name}</b>\n{Utility.Plural("day", selectedProperty.buildTime, true)} left to end of construction";
        else
        {
            str = $"<b>{selectedProperty.Name}</b>\n";
            Property.Event even = selectedProperty.events.FirstOrDefault();
            if (even != null)
            {
                if (even.timer == -1)
                    str += $"Events: {even.name}\n";
                else
                    str += $"Events: {even.name} ({even.timer})\n";
            }
            str += $"Income:{selectedProperty.Income}  Upkeep:{selectedProperty.Upkeep}  Profit:{selectedProperty.Profit}\n";
            if (selectedProperty.income > 0)
            {
                int locationIndex = game.GetLocationIndex(selectedProperty);
                Worker manager = game.hiredWorkers.FirstOrDefault(x => locationIndex != -1 && x.locationIndex == locationIndex);
                str += $"Efficiency: {selectedProperty.Efficiency} ({selectedProperty.efficiency})\nManager: {(manager == null ? "(none)" : manager.ToStringShort())}\n";
            }
            str += "Upgrades: ";
            if (selectedProperty.upgrades != null && selectedProperty.upgrades.Any(x => x.active))
                str += string.Join(", ", selectedProperty.upgrades.Where(x => x.active).Select(x => x.name).OrderBy(x => x));
            else
                str += "(none)";
        }
        transform.Find("Text2").GetComponent<TMP_Text>().text = str;

        Transform content = transform.Find("Upgrades/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (selectedProperty != null && selectedProperty.status == Property.Status.Active && selectedProperty.upgrades != null && selectedProperty.upgrades.Any(x => !x.active))
        {
            ui.AddTextHeader("Available upgrades:", content);
            foreach (Property.Upgrade upgrade in selectedProperty.upgrades.Where(x => !x.active).OrderBy(x => x.name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.Init(upgrade.ToString(), "Buy", () =>
                {
                    if (player.gold < upgrade.value)
                    {
                        ui.ShowDialog($"You need {upgrade.value} gold to buy {upgrade.name.ToLower()}.");
                        return;
                    }

                    player.AddGold(-upgrade.value);
                    upgrade.active = true;
                    if (upgrade.upkeep > 0 && selectedProperty.upkeep == 0)
                        selectedProperty.desc += ", UPKEEP upkeep";
                    selectedProperty.value += upgrade.value;
                    selectedProperty.income += upgrade.income;
                    selectedProperty.upkeep += upgrade.upkeep;
                    game.Text.Set($"You buy {upgrade.name.ToLower()} for <color=#FFD700>{upgrade.value}</color> gold.");
                    if (upgrade.name == "Extra guards")
                    {
                        Property.Event even = selectedProperty.events.FirstOrDefault(e => e.name == "Infested" && e.timer == -1);
                        if (even != null)
                        {
                            int days = game.world.CalculateTravelDaysNonTeam(World.IndexToPoint(selectedProperty.locationIndex));
                            even.timer = days;
                            even.state = 1;
                            game.Text.Append($"They will take care of monsters infestation in {Utility.Plural("day", days, true)}.");
                        }
                    }
                    else if (upgrade.name == "Stables")
                        game.freshHorses = 10;
                    game.AddTime(minutes: 30);
                    if (IsOpen)
                    {
                        if (!manageProperty)
                            Refresh();
                        RefreshDetails();
                    }
                    game.UpdateText();
                });
            }
        }
    }

    public void Manage()
    {
        TextBuilder text = game.Text;
        if (game.hour > 16)
            text.Set("It's too late to manage.");
        else if (player.energy < 25)
            text.Set("You are too tired to manage.");
        else if (!game.world.CurrentTile.clear && game.world.Location.IsClearable())
            text.Set($"You can't manage while monsters occupy the {game.world.CurrentTile.Name}.");
        else
        {
            DoManage(selectedProperty, text, !manageProperty);
            game.AddTime(hours: 8);
            if (IsOpen)
            {
                if (!manageProperty)
                    Refresh();
                RefreshDetails();
            }
        }
        game.UpdateText();
    }

    public void DoManage(Property property, TextBuilder text, bool remote)
    {
        player.energy -= 25;
        (Hero bestAlly, int bestValue) = game.team.GetSkill(Skill.Management);
        if (remote)
            bestValue = bestValue * 3 / 4;
        float trainMod;
        if (bestAlly == null || bestAlly == player)
        {
            text?.Set($"You manage the {property.name.ToLower()}.");
            trainMod = 1f;
        }
        else
        {
            text?.Set($"You and {bestAlly.name} manage the {property.name.ToLower()}.");
            int skill = player.GetSkill(Skill.Management);
            if (remote)
                skill = skill * 3 / 4;
            trainMod = 1f + 0.01f * (bestValue - skill);
        }

        int newEfficiency = CalculateEfficiencyChange(bestValue, property.efficiency);
        if (text != null)
        {
            if (newEfficiency > property.efficiency)
                text.Append($"Efficiency increased by {newEfficiency - property.efficiency}.");
            else if (newEfficiency < property.efficiency)
                text.Append($"Efficiency decreased by {property.efficiency - newEfficiency}.");
        }
        property.efficiency = newEfficiency;
        property.lastManaged = game.day;

        player.Train(Skill.Management, text, trainMod);
    }

    public static int CalculateEfficiencyChange(int skill, int efficiency)
    {
        int targetEfficiency = skill + 25 + Utility.Random(-10, 10);
        if (targetEfficiency == efficiency)
            return efficiency;

        int difference = targetEfficiency - efficiency;
        int maxStep = Mathf.Max(Mathf.RoundToInt(Mathf.Abs(difference) * 0.2f), 1);
        int step = Utility.Random(1, maxStep);
        if (difference < 0)
            step = -step;
        return Mathf.Clamp(efficiency + step, 1, 100);
    }
}
