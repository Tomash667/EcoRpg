using UnityEngine;

public class Garden : GameDialog
{
    protected override void Refresh()
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
            DropdownEntry dropdownEntry = Instantiate(game.UI.dropdownEntryPrefab, content).GetComponent<DropdownEntry>();
            dropdownEntry.Init($"Plot {i + 1}: {plant}", "Change", choices, x =>
            {
                switch (x)
                {
                case 1:
                    // vegetables
                    if (plant == "Vegetables")
                        game.UI.ShowDialog("Vegetables are already planted here.");
                    else if (player.gold < 10)
                        game.UI.ShowDialog("You need 10 gold.");
                    else
                    {
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
                        game.UI.ShowDialog("Herbs are already planted here.");
                    else if (player.CountItem(herb) < 10)
                        game.UI.ShowDialog("You need 10 herbs.");
                    else
                    {
                        player.RemoveItem(herb, 10);
                        property.gardenPlants[index] = "Herbs";
                        Refresh();
                    }
                    break;
                case 3:
                    // rare herbs
                    Item rareHerb = Item.Get("rare herb");
                    if (plant == "Rare herbs")
                        game.UI.ShowDialog("Rare herbs are already planted here.");
                    else if (player.CountItem(rareHerb) < 10)
                        game.UI.ShowDialog("You need 10 rare herbs.");
                    else
                    {
                        player.RemoveItem(rareHerb, 10);
                        property.gardenPlants[index] = "Rare herbs";
                        Refresh();
                    }
                    break;
                }
            });
        }
    }
}
