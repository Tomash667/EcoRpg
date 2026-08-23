using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class CharacterScreen : GameDialog
{
    public CraftScreen craftScreen;

    private readonly StringBuilder sb = new();

    public override void Refresh()
    {
        TMP_Text charText = transform.Find("TextScroll/Viewport/Content/Text").GetComponent<TMP_Text>();
        sb.Clear();
        sb.Append($"{player.GenderSign}{player.name}\n" +
            $"Race: {player.race.AsString()}\n" +
            $"Level: {player.level} {player.clas.AsString()} ({player.ExpP}%)\n" +
            $"Attack: {player.Attack}\n" +
            $"Defense: {player.Defense}\n" +
            $"Health: {player.hp}/{player.hpMax}\n");
        if (player.owedGold > 0)
            sb.Append($"Owed gold: {player.owedGold}\n");
        if (player.skills.Count > 0)
        {
            sb.Append("Skills:\n");
            foreach (var skill in player.skills.Select(kvp => (name: kvp.Key.AsString().ToUpper1(), kvp.Value.level)).OrderBy(x => x.name))
                sb.Append($"  {skill.name}: {skill.level}\n");
        }
        if (player.rested > 0 || game.team.freshHorses > 0)
        {
            sb.Append("Effects:\n");
            if (player.rested > 0)
                sb.Append($"  Well rested ({Utility.Plural("day", player.rested, true)})\n");
            if (game.team.freshHorses > 0)
                sb.Append($"  Fresh horses ({Utility.Plural("day", game.team.freshHorses, true)})\n");
        }
        charText.text = sb.ToString();

        RefreshItems();
    }

    private void RefreshItems()
    {
        PopulateInventory(gameObject,
            (itemEntry, item) =>
            {
                itemEntry.Init(item.ToString(Price.None), "Unequip", () =>
                {
                    game.AddText($"You unequip {Utility.A(item.name)}.");
                    player.AddItem(item);
                    switch (item.type)
                    {
                    case Item.Type.Weapon:
                        player.weapon = null;
                        break;
                    case Item.Type.Shield:
                        player.shield = null;
                        break;
                    case Item.Type.Armor:
                        player.armor = null;
                        break;
                    }
                    Refresh();
                });
            },
            (itemEntry, itemSlot) =>
            {
                void Drop()
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        game.AddText($"You drop {Utility.P(itemSlot.item.name, itemSlot.count)}.");
                        player.RemoveItem(itemSlot, itemSlot.count);
                        RefreshItems();
                        game.UpdateText();
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        ui.ShowInput($"How many {Utility.Plural(itemSlot.item.name)} to drop away?", count =>
                        {
                            if (count <= 0)
                                return true;
                            count = Mathf.Min(count, itemSlot.count);
                            game.AddText($"You drop {Utility.P(itemSlot.item.name, count)}.");
                            player.RemoveItem(itemSlot, count);
                            RefreshItems();
                            game.UpdateText();
                            return true;
                        });
                    }
                    else
                    {
                        game.AddText($"You drop {Utility.A(itemSlot.item.name)}.");
                        player.RemoveItem(itemSlot);
                        RefreshItems();
                        game.UpdateText();
                    }
                }

                if (player.CanEquip(itemSlot.item))
                {
                    itemEntry.Init2(itemSlot.ToString(Price.None), "Equip", () =>
                    {
                        string str = string.Empty;
                        if (itemSlot.team)
                        {
                            str = $"You take {Utility.A(itemSlot.item.name)} for yourself. ";
                            game.team.PayForItem(player, itemSlot.item);
                        }

                        Item prevItem = null;
                        switch (itemSlot.item.type)
                        {
                        case Item.Type.Weapon:
                            prevItem = player.weapon;
                            player.weapon = itemSlot.item;
                            break;
                        case Item.Type.Armor:
                            prevItem = player.armor;
                            player.armor = itemSlot.item;
                            break;
                        case Item.Type.Shield:
                            prevItem = player.shield;
                            player.shield = itemSlot.item;
                            break;
                        }

                        if (prevItem != null)
                        {
                            player.AddItem(prevItem);
                            str += $"You equip {Utility.A(itemSlot.item.name)} and put your old {prevItem.name} into backpack.";
                        }
                        else
                            str += $"You equip {Utility.A(itemSlot.item.name)}.";
                        game.AddText(str);
                        player.RemoveItem(itemSlot);
                        Refresh();
                        game.UpdateText();
                    }, "Drop", Drop);
                }
                else if (itemSlot.item.type == Item.Type.Usable)
                {
                    itemEntry.Init2(itemSlot.ToString(Price.None), "Use", () =>
                    {
                        string str = $"You {(itemSlot.item.drink ? "drink" : "eat")} {Utility.A(itemSlot.item.name)}.";
                        if (player.hp != player.hpMax)
                            str += " Your wounds heal.";
                        game.AddText(str);
                        player.hp = Mathf.Min(player.hp + itemSlot.item.power, player.hpMax);
                        player.RemoveItem(itemSlot);
                        Refresh();
                        game.UpdateText();
                    }, "Drop", Drop);
                }
                else if (itemSlot.item.type == Item.Type.Tool && itemSlot.item.name == "alchemy set")
                    itemEntry.Init2(itemSlot.ToString(Price.None), "Use", craftScreen.Show, "Drop", Drop);
                else
                    itemEntry.Init2(itemSlot.ToString(Price.None), null, null, "Drop", Drop);
            }
        );
    }

    public void PopulateInventory(GameObject inventory, Action<ItemEntry, Item> equippedCallback, Action<ItemEntry, ItemSlot> itemCallback)
    {
        Transform content = inventory.transform.Find("PlayerItems/Viewport/Content");

        // remove existing items
        foreach (Transform child in content)
            Destroy(child.gameObject);

        // add equipped items
        if (player.weapon != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            equippedCallback(itemEntry, player.weapon);
            itemEntry.SetImage(ui.itemIcons[(int)player.weapon.GetIcon()]);
        }

        if (player.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            equippedCallback(itemEntry, player.armor);
            itemEntry.SetImage(ui.itemIcons[(int)player.armor.GetIcon()]);
        }

        if (player.shield != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            equippedCallback(itemEntry, player.shield);
            itemEntry.SetImage(ui.itemIcons[(int)player.shield.GetIcon()]);
        }

        // add separator between equipped and not equipped items
        if ((player.weapon != null || player.armor != null || player.shield != null) && player.items.Count > 0)
            Instantiate(ui.lineSeparatorPrefab, content);

        // add not equipped items
        foreach (ItemSlot itemSlot in player.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemCallback(itemEntry, itemSlot);
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }
}
