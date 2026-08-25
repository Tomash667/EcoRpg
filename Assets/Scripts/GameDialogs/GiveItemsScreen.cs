using UnityEngine;

public class GiveItemsScreen : GameDialog
{
    public AllyScreen allyScreen;
    public CharacterScreen characterScreen;

    public override void Refresh()
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
                            text.Append($"You give {ally.name} {Utility.A(itemSlot.item.name)}.");
                            if (itemSlot.team)
                            {
                                text.Append($"{ally.He} takes it for {ally.himself}.");
                                game.team.PayForItem(ally, itemSlot.item);
                            }
                            else
                                ally.IncreaseAffectionFromValue(itemSlot.item, 1, text);
                            ally.GiveItem(itemSlot.item, 1, text);
                            game.AddText(text.Flush());
                            player.RemoveItem(itemSlot);
                            RefreshIfOpen();
                            game.UpdateText();
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            text.Append($"You give {ally.name} {Utility.Plural(itemSlot.item.name, itemSlot.count)}.");
                            if (itemSlot.team)
                            {
                                text.Append($"{ally.He} takes {(itemSlot.count > 1 ? "them" : "it")} for {ally.himself}.");
                                game.team.PayForItem(ally, itemSlot.item, itemSlot.count);
                            }
                            else
                                ally.IncreaseAffectionFromValue(itemSlot.item, itemSlot.count, text);
                            ally.GiveItem(itemSlot.item, itemSlot.count, text);
                            game.AddText(text.Flush());
                            player.RemoveItem(itemSlot, itemSlot.count);
                            RefreshIfOpen();
                            game.UpdateText();
                        }
                        else
                        {
                            ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} give to {ally.name}?", count =>
                            {
                                if (count <= 0)
                                    return true;
                                count = Mathf.Min(count, itemSlot.count);
                                text.Append($"You give {ally.name} {Utility.Plural(itemSlot.item.name, count)}.");
                                if (itemSlot.team)
                                {
                                    text.Append($"{ally.He} takes {(count > 1 ? "them" : "it")} for {ally.himself}.");
                                    game.team.PayForItem(ally, itemSlot.item, count);
                                }
                                else
                                    ally.IncreaseAffectionFromValue(itemSlot.item, count, text);
                                ally.GiveItem(itemSlot.item, count, text);
                                game.AddText(text.Flush());
                                player.RemoveItem(itemSlot, count);
                                RefreshIfOpen();
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
