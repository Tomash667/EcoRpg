using UnityEngine;

public class StoreItemsScreen : GameDialog
{
    public CharacterScreen characterScreen;

    protected override void Refresh()
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
                            game.team.PayForItem(player, itemSlot.item, itemSlot.count);
                        property.AddStoredItem(itemSlot.item, itemSlot.count);
                        player.RemoveItem(itemSlot, itemSlot.count);
                        Refresh();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to store?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            if (itemSlot.team)
                                game.team.PayForItem(player, itemSlot.item, count);
                            property.AddStoredItem(itemSlot.item, count);
                            player.RemoveItem(itemSlot, count);
                            Refresh();
                            return true;
                        });
                    }
                    else
                    {
                        if (itemSlot.team)
                            game.team.PayForItem(player, itemSlot.item);
                        property.AddStoredItem(itemSlot.item);
                        player.RemoveItem(itemSlot);
                        Refresh();
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
                        player.AddItem(itemSlot.item, count);
                        property.RemoveStoredItem(itemSlot, count);
                        Refresh();
                        return true;
                    });
                }
                else
                {
                    player.AddItem(itemSlot.item);
                    property.RemoveStoredItem(itemSlot);
                    Refresh();
                }
            });
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }
}
