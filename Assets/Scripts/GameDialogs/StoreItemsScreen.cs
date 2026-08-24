using UnityEngine;

public class StoreItemsScreen : GameDialog
{
    public CharacterScreen characterScreen;

    public override void Refresh()
    {
        Property property = game.GetPropertyInside();

        // player items
        characterScreen.PopulateInventory(gameObject,
            (itemEntry, item) => itemEntry.Init(item.ToString(Price.None)),
            (itemEntry, itemSlot) =>
            {
                itemEntry.Init(itemSlot.ToString(Price.None), "Store", () =>
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        if (itemSlot.team)
                        {
                            tb.Append($"You take {Utility.Plural(itemSlot.item.name, itemSlot.count)} for yourself.");
                            game.team.PayForItem(player, itemSlot.item, itemSlot.count);
                        }
                        tb.Append($"You store {Utility.Plural(itemSlot.item.name, itemSlot.count)}.");
                        game.AddText(tb.Flush());
                        property.AddStoredItem(itemSlot.item, itemSlot.count);
                        player.RemoveItem(itemSlot, itemSlot.count);
                        Refresh();
                        game.UpdateText();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to store?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            if (itemSlot.team)
                            {
                                tb.Append($"You take {Utility.Plural(itemSlot.item.name, count)} for yourself.");
                                game.team.PayForItem(player, itemSlot.item, count);
                            }
                            tb.Append($"You store {Utility.Plural(itemSlot.item.name, count)}.");
                            game.AddText(tb.Flush());
                            property.AddStoredItem(itemSlot.item, count);
                            player.RemoveItem(itemSlot, count);
                            Refresh();
                            game.UpdateText();
                            return true;
                        });
                    }
                    else
                    {
                        if (itemSlot.team)
                        {
                            tb.Append($"You take {Utility.A(itemSlot.item.name)} for yourself.");
                            game.team.PayForItem(player, itemSlot.item);
                        }
                        tb.Append($"You store {Utility.A(itemSlot.item.name)}.");
                        game.AddText(tb.Flush());
                        property.AddStoredItem(itemSlot.item);
                        player.RemoveItem(itemSlot);
                        Refresh();
                        game.UpdateText();
                    }
                });
            }
        );

        // stored items
        Transform content = transform.Find("StoredItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (ItemSlot itemSlot in property.storedItems)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(Price.None), "Take", () =>
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    game.AddText($"You take {Utility.Plural(itemSlot.item.name, itemSlot.count)}.");
                    player.AddItem(itemSlot.item, itemSlot.count);
                    property.RemoveStoredItem(itemSlot, itemSlot.count);
                    Refresh();
                }
                else if (Input.GetKey(KeyCode.LeftControl))
                {
                    ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to take?", count =>
                    {
                        if (count <= 0)
                            return true;
                        count = Mathf.Min(count, itemSlot.count);
                        game.AddText($"You take {Utility.Plural(itemSlot.item.name, count)}.");
                        player.AddItem(itemSlot.item, count);
                        property.RemoveStoredItem(itemSlot, count);
                        Refresh();
                        return true;
                    });
                }
                else
                {
                    game.AddText($"You take {Utility.A(itemSlot.item.name)}.");
                    player.AddItem(itemSlot.item);
                    property.RemoveStoredItem(itemSlot);
                    Refresh();
                }
            });
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }
}
