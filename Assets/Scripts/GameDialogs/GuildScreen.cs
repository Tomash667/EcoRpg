using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuildScreen : GameDialog
{
    private const int MaxGuildRank = 4;

    private static readonly string[] GuildRanks = new[] { "None", "Copper", "Silver", "Gold", "Diamond" };

    public CraftScreen craftScreen;

    private void Update()
    {
        if (player.guildRank != 0)
        {
            if (Input.GetKeyDown(KeyCode.C))
                craftScreen.Show();
            if (Input.GetKeyDown(KeyCode.K))
                game.Cook();
            if (Input.GetKeyDown(KeyCode.R))
                Recruit();
            if (Input.GetKeyDown(KeyCode.T))
                Train();
        }
    }

    protected override void Refresh()
    {
        // text
        string guildText = game.Text.Flush();
        if (guildText != string.Empty)
            guildText += "\n\n";
        int guildRank = player.guildRank;
        guildText += $"Your rank: {GuildRanks[guildRank]}";
        transform.Find("Text").GetComponent<TMP_Text>().text = guildText;

        // enable buttons if player joined guild
        transform.Find("BtJoin").GetComponent<Button>().interactable = guildRank == 0;
        transform.Find("BtRecruit").GetComponent<Button>().interactable = guildRank != 0;
        transform.Find("BtTrain").GetComponent<Button>().interactable = guildRank != 0;
        transform.Find("BtCook").GetComponent<Button>().interactable = guildRank != 0;
        transform.Find("BtCraft").GetComponent<Button>().interactable = guildRank != 0;

        // populate list with quests
        Transform content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        bool haveItems = false;
        int acceptedQuestCount = game.activeQuests.Count(x => !x.IsUnique);
        if (acceptedQuestCount > 0)
        {
            ui.AddTextHeader($"Accepted quests ({acceptedQuestCount}/{guildRank}):", content);
            foreach (Quest quest in game.activeQuests.Where(x => !x.IsUnique))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                if (quest.IsDone())
                    itemEntry.Init2(quest.TextReward, "Finish", () => FinishQuest(quest), "Cancel", () => CancelQuest(quest));
                else
                    itemEntry.Init2(quest.TextReward, null, null, "Cancel", () => CancelQuest(quest));
            }
            haveItems = true;
        }

        if (game.availableQuests.Any(x => x.difficulty <= guildRank))
        {
            if (haveItems)
                Instantiate(ui.lineSeparatorPrefab, content);
            ui.AddTextHeader("Available quests:", content);
            haveItems = true;
        }

        bool unavailable = false;
        foreach (Quest quest in game.availableQuests)
        {
            if (!unavailable && quest.difficulty > guildRank)
            {
                unavailable = true;
                if (haveItems)
                    Instantiate(ui.lineSeparatorPrefab, content);
                ui.AddTextHeader("Unavailable quests:", content);
                haveItems = true;
            }

            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (acceptedQuestCount < guildRank && !unavailable)
                itemEntry.Init(quest.TitleReward, "Pick", () => AcceptQuest(quest));
            else
                itemEntry.Init(quest.TitleReward);
        }

        // add player paid quests
        Property[] infestedProperties = player.properties.Where(p => p.events.Any(e => e.name == "Infested" && e.timer == -1)).ToArray();
        if (infestedProperties.Length > 0)
        {
            if (haveItems)
                Instantiate(ui.lineSeparatorPrefab, content);
            ui.AddTextHeader("Quests to offer:", content);
            foreach (Property property in infestedProperties)
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                int days = game.world.CalculateTravelDaysNonTeam(World.IndexToPoint(property.locationIndex));
                itemEntry.Init($"Clear {property.Name.ToLower()} ({Utility.Plural("day", days, true)}, {property.infestedCost} gold)", "Pay", () => PayToClear(property));
            }
        }
    }

    public void Join()
    {
        player.guildRank = 1;
        game.Text.Set("You fill out form and register as adventurer. From this day forward, you are free to accept quests, earn rewards, and carve your own path through the dungeons. " +
            "May your courage be greater than the dangers ahead, and your pack always heavy with treasure.");
        game.AddTime(minutes: 15);
        RefreshIfOpen();
        game.UpdateText();
    }

    private void AcceptQuest(Quest quest)
    {
        game.activeQuests.Add(quest);
        if (!game.activeQuests.Any(x => x.tracked))
            quest.tracked = true;
        if (quest.type == Quest.Type.Clear)
        {
            // if player already defeat some enemies, update counter
            Tile tile = game.world.GetLocation(quest.location);
            quest.count = tile.defeatedEnemies;
        }
        game.availableQuests.Remove(quest);
        game.Text.Set($"You accepted quest '{quest.Title}'.");
        game.AddTime(minutes: 15);
        RefreshIfOpen();
        game.UpdateText();
    }

    private void FinishQuest(Quest quest)
    {
        TextBuilder text = game.Text;
        int reward = quest.Reward;
        text.Set($"You received <color=#FFD700>{reward}</color> gold for quest '{quest.Title}'.");

        bool promoted = false;
        if (player.guildRank != MaxGuildRank)
        {
            float value = quest.difficultyMod;
            if (quest.difficulty + 1 == player.guildRank)
                value /= 4;
            else if (quest.difficulty < player.guildRank)
                value = 0;

            if (value > 0)
            {
                player.guildProgress += value;
                if (player.guildProgress >= 1f + player.guildRank)
                {
                    ++player.guildRank;
                    player.guildProgress = 0;
                    text.Append($"You were promoted to <b>{GuildRanks[player.guildRank]}</b> rank.");
                    game.team.ChangeAffection(5, text);
                    promoted = true;
                }
            }
        }

        if (!promoted)
            game.team.ChangeAffection(1, text);

        game.team.AddGold(reward);
        quest.Finish();
        game.RemoveQuest(quest);
        game.AddTime(minutes: 15);
        RefreshIfOpen();
        game.UpdateText();
    }

    private void CancelQuest(Quest quest)
    {
        TextBuilder text = game.Text;
        text.Set($"You canceled quest '{quest.Title}'.");
        player.guildProgress -= quest.difficultyMod;
        if (player.guildRank > 1 && player.guildProgress < -player.guildRank)
        {
            --player.guildRank;
            player.guildProgress = 0;
            text.Append($"You are degraded to <b>{GuildRanks[player.guildRank]}</b> rank.");
            game.team.ChangeAffection(-5, text);
        }
        else
            game.team.ChangeAffection(-1, text);
        game.RemoveQuest(quest);

        // readd quest if it can be completed
        bool canBeCompleted = true;
        if (quest.type == Quest.Type.Artifact)
        {
            Tile tile = game.world.GetLocation(quest.location);
            canBeCompleted = !tile.foundTreasure;
        }
        else if (quest.type == Quest.Type.Clear)
        {
            Tile tile = game.world.GetLocation(quest.location);
            canBeCompleted = !tile.clear;
        }
        if (canBeCompleted)
        {
            quest.timer = 5;
            game.availableQuests.Add(quest);
            game.SortQuests();
        }

        game.AddTime(minutes: 15);
        RefreshIfOpen();
        game.UpdateText();
    }

    private void PayToClear(Property property)
    {
        if (player.gold < property.infestedCost)
        {
            ui.ShowDialog($"You need {property.infestedCost} gold to pay adventurers to clear the {property.Name.ToLower()}.");
            return;
        }

        int days = game.world.CalculateTravelDaysNonTeam(World.IndexToPoint(property.locationIndex));
        player.AddGold(-property.infestedCost);
        property.events.First(e => e.name == "Infested").timer = days;
        game.Text.Set($"You pay <color=#FFD700>{property.infestedCost}</color> gold to adventurers to clear the {property.Name.ToLower()}. " +
            $"It will take them {Utility.Plural("day", days, true)}.");
        game.AddTime(minutes: 15);
        RefreshIfOpen();
        game.UpdateText();
    }

    public void Train()
    {
        TextBuilder text = game.Text;
        if (game.hour > 16)
            text.Set("It's too late to train.");
        else if (player.energy < 50)
            text.Set("You are too tired to train.");
        else
        {
            player.energy -= 50;
            text.Set("You train fighting.");
            List<Hero> levelups = null;
            foreach (Hero hero in game.team.heroes)
            {
                if (hero.AddExp(100))
                {
                    levelups ??= new();
                    levelups.Add(hero);
                }
            }

            if (levelups != null)
            {
                foreach (var group in levelups.GroupBy(x => x.level))
                {
                    string isAre = group.Count() > 1 || group.First() is Player ? "are" : "is";
                    text.Append($"{Utility.PrettyList(group.Select(x => x.nameYou)).ToUpper1()} {isAre} now level {group.Key}.");
                }
            }

            game.AddTime(hours: 8);
            RefreshIfOpen();
        }
        game.UpdateText();
    }

    public void Recruit()
    {
        if (game.team.heroes.Count >= Team.MaxSize)
        {
            ui.ShowDialog("Your team is full.");
            return;
        }

        int level = Mathf.Max(Utility.Random(-3, 1) + player.guildRank, 0);
        Hero hero = game.SpawnHero(level);
        // Novice(0) -> Apprentice(2) -> Journeyman(4) -> Adept(8) -> Expert(12) -> Master(16) -> Grandmaster(20)
        string levelName = (level / 2) switch
        {
            1 => "apprentice",
            2 => "journeyman",
            _ => "novice",
        };

        string skill;
        if (hero.skills.Count > 0)
            skill = $" and knows {hero.skills.First().Key.AsString()}";
        else
            skill = string.Empty;

        ui.ShowConfirm($"You meet <b>{hero.name}</b> and talk with {hero.him} about adventurers. " +
            $"{hero.He} is {Utility.A(levelName)} <b>{levelName} {hero.clas.AsString()}</b>{skill}. Do you want to recruit {hero.him}?", yes =>
            {
                int chance = 100 + (player.level - hero.level) * 5;
                if (Utility.Rand % 100 < chance)
                {
                    if (yes)
                    {
                        game.Text.Set($"You recruit {hero.name} to your team.");
                        game.team.heroes.Add(hero);
                        hero.BuyItems();
                        game.UpdateButtons();
                    }
                }
                else
                    game.Text.Set($"You <b>failed</b> to convince {hero.name} to join your team.");

                game.AddTime(minutes: 30);
                RefreshIfOpen();
                game.UpdateText();
            });
    }
}
