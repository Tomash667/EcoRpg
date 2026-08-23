using UnityEngine;

public class ShopScreen : GameDialog
{
    public CharacterScreen characterScreen;

    public override void Show()
    {
        RefreshShopItems();
        base.Show();
    }

    public override void Refresh()
    {
        characterScreen.PopulateInventory(gameObject,
            (itemEntry, item) => itemEntry.Init(item.ToString(Price.Sell)),
            (itemEntry, itemSlot) =>
            {
                itemEntry.Init(itemSlot.ToString(Price.Sell), "Sell", () =>
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        int gold = itemSlot.item.value * itemSlot.count / 2;
                        if (itemSlot.team)
                            game.team.AddGold(gold);
                        else
                            player.AddGold(gold);
                        game.AddText($"You sell {Utility.P(itemSlot.item.name, itemSlot.count)} for {gold} gold.");
                        player.RemoveItem(itemSlot, itemSlot.count);
                        Refresh();
                        game.UpdateText();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to sell for {itemSlot.item.value / 2} gold each?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            int gold = itemSlot.item.value * count / 2;
                            if (itemSlot.team)
                                game.team.AddGold(gold);
                            else
                                player.AddGold(gold);
                            game.AddText($"You sell {Utility.P(itemSlot.item.name, count)} for {gold} gold.");
                            player.RemoveItem(itemSlot, count);
                            Refresh();
                            game.UpdateText();
                            return true;
                        });
                    }
                    else
                    {
                        int gold = itemSlot.item.value / 2;
                        if (itemSlot.team)
                            game.team.AddGold(gold);
                        else
                            player.AddGold(gold);
                        game.AddText($"You sell {Utility.A(itemSlot.item.name)} for {gold} gold.");
                        player.RemoveItem(itemSlot);
                        Refresh();
                        game.UpdateText();
                    }
                });
            }
        );
    }

    private void RefreshShopItems()
    {
        Transform content = transform.Find("ShopItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);
        Item[] availableItems = (game.world.Location == TileType.City ? Item.cityItems : Item.villageItems);
        foreach (Item item in availableItems)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(item.ToString(Price.Buy), "Buy", () =>
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    ui.ShowInput($"How many {Utility.Plural(item.name)} to buy for {item.value} gold each?", count =>
                    {
                        if (count <= 0)
                            return true;
                        int price = count * item.value;
                        if (player.gold >= price)
                        {
                            game.AddText($"You buy {Utility.P(item.name, count)} for {price} gold.");
                            player.AddItem(item, count);
                            player.AddGold(-price);
                            Refresh();
                            game.UpdateText();
                            return true;
                        }
                        else
                        {
                            ui.ShowDialog($"You need {price} gold to buy {Utility.Plural(item.name, count)}.");
                            return false;
                        }
                    });
                }
                else if (player.gold >= item.value)
                {
                    game.AddText($"You buy {Utility.A(item.name)} for {item.value} gold.");
                    player.AddItem(item);
                    player.AddGold(-item.value);
                    Refresh();
                    game.UpdateText();
                }
                else
                    ui.ShowDialog($"You need {item.value} gold to buy {item.name}.");
            });
            itemEntry.SetImage(ui.itemIcons[(int)item.GetIcon()]);
        }
    }
}
