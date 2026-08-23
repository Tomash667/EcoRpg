public class EnchantItemsScreen : GameDialog
{
    public CharacterScreen characterScreen;

    public override void Refresh()
    {
        characterScreen.PopulateInventory(gameObject,
            (itemEntry, item) =>
            {
                if (item.level < Item.MaxLevelEnchant)
                {
                    itemEntry.Init(item.ToString(Price.Enchant), "Enchant", () =>
                    {
                        int cost = item.GetEnchantCost();
                        if (player.gold < cost)
                            ui.ShowDialog($"You need {cost} gold to enchant {item.name}.");
                        else
                        {
                            Item newItem = item.GetEnchanted();
                            switch (newItem.type)
                            {
                            case Item.Type.Weapon:
                                player.weapon = newItem;
                                break;
                            case Item.Type.Shield:
                                player.shield = newItem;
                                break;
                            case Item.Type.Armor:
                                player.armor = newItem;
                                break;
                            }
                            player.AddGold(-cost);
                            Refresh();
                            game.UpdateText();
                        }
                    });
                }
                else
                    itemEntry.Init(item.ToString(Price.None));
            },
            (itemEntry, itemSlot) =>
            {
                if (itemSlot.item.CanEnchant())
                {
                    itemEntry.Init(itemSlot.ToString(Price.Enchant), "Enchant", () =>
                    {
                        int cost = itemSlot.item.GetEnchantCost();
                        if (player.gold < cost)
                            ui.ShowDialog($"You need {cost} gold to enchant {itemSlot.item.name}.");
                        else
                        {
                            Item item = itemSlot.item;
                            if (itemSlot.team)
                                game.team.PayForItem(player, itemSlot.item);
                            player.RemoveItem(itemSlot);
                            player.AddItem(item.GetEnchanted());
                            player.AddGold(-cost);
                            Refresh();
                            game.UpdateText();
                        }
                    });
                }
                else
                    itemEntry.Init(itemSlot.ToString(Price.None));
            }
        );
    }
}
