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
                            game.AddText($"You pay {cost} gold to enchant {Utility.A(item.name)} into {Utility.A(newItem.name)}.");
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
                            Item newItem = item.GetEnchanted();
                            if (itemSlot.team)
                            {
                                tb.Append($"You take {Utility.A(itemSlot.item.name)} for yourself.");
                                game.team.PayForItem(player, itemSlot.item);
                            }
                            tb.Append($"You pay {cost} gold to enchant {Utility.A(item.name)} into {Utility.A(newItem.name)}.");
                            game.AddText(tb.Flush());
                            player.RemoveItem(itemSlot);
                            player.AddItem(newItem);
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
