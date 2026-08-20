using UnityEngine;

public class GiveItemsScreen : GameDialog
{
    public AllyScreen allyScreen;
    public CharacterScreen characterScreen;

    protected override void Refresh()
    {
        Hero ally = allyScreen.Ally;

        // player items
        characterScreen.PopulateInventory(gameObject,
            (itemEntry, item) => itemEntry.Init(item.ToString(Price.None)),
            (itemEntry, itemSlot) =>
            {
                if (ally.WillTakeItem(itemSlot.item))
                {
                    itemEntry.Init(itemSlot.ToString(Price.None), "Give", () =>
                    {
                        if (itemSlot.item.type == Item.Type.Weapon || itemSlot.item.type == Item.Type.Armor || itemSlot.item.type == Item.Type.Shield
                            || !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl)))
                        {
                            if (itemSlot.team)
                                game.team.PayForItem(ally, itemSlot.item);
                            else
                                ally.IncreaseAffectionFromValue(itemSlot.item, 1);
                            ally.GiveItem(itemSlot.item);
                            player.RemoveItem(itemSlot);
                            Refresh();
                            game.UpdateText();
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            if (itemSlot.team)
                                game.team.PayForItem(ally, itemSlot.item, itemSlot.count);
                            else
                                ally.IncreaseAffectionFromValue(itemSlot.item, itemSlot.count);
                            ally.GiveItem(itemSlot.item, itemSlot.count);
                            player.RemoveItem(itemSlot, itemSlot.count);
                            Refresh();
                            game.UpdateText();
                        }
                        else
                        {
                            ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} give to {ally.name}?", count =>
                            {
                                if (count <= 0)
                                    return true;
                                count = Mathf.Min(count, itemSlot.count);
                                if (itemSlot.team)
                                    game.team.PayForItem(ally, itemSlot.item, count);
                                else
                                    ally.IncreaseAffectionFromValue(itemSlot.item, count);
                                ally.GiveItem(itemSlot.item, count);
                                player.RemoveItem(itemSlot, count);
                                Refresh();
                                game.UpdateText();
                                return true;
                            });
                        }
                    });
                }
                else
                    itemEntry.Init(itemSlot.ToString(Price.None));
            }
        );

        // ally items
        allyScreen.PopulateInventory(gameObject);
    }
}
