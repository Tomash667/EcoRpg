using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class AllyScreen : GameDialog
{
    private Hero ally;
    private readonly StringBuilder sb = new();

    public Hero Ally => ally;

    public void Show(int index)
    {
        ally = game.team.heroes[index + 1];
        base.Show();
    }

    public override void Refresh()
    {
        TMP_Text charText = transform.Find("TextScroll/Viewport/Content/Text").GetComponent<TMP_Text>();
        sb.Clear();
        sb.Append($"{ally.GenderSign}{ally.name}\n" +
            $"Race: {ally.race.AsString()}\n" +
            $"Level: {ally.level} {ally.clas.AsString()} ({ally.ExpP}%)\n" +
            $"Attack: {ally.Attack}\n" +
            $"Defense: {ally.Defense}\n" +
            $"Health: {ally.hp}/{ally.hpMax}\n" +
            $"Gold: {ally.gold}");
        if (ally.owedGold > 0)
            sb.Append($" (owes {ally.owedGold} gold)\n");
        else
            sb.Append('\n');
        sb.Append($"Affection: {ally.affection}\n");
        if (ally.skills.Count > 0)
        {
            sb.Append("Skills:\n");
            foreach (var skill in ally.skills.Select(kvp => (name: kvp.Key.AsString().ToUpper1(), kvp.Value.level)).OrderBy(x => x.name))
                sb.Append($"  {skill.name}: {skill.level}\n");
        }
        if (ally.rested > 0)
            sb.Append($"Effects:\n  Well rested ({Utility.Plural("day", ally.rested, true)})");
        charText.text = sb.ToString();

        PopulateInventory(gameObject);
    }

    public void PopulateInventory(GameObject inventory)
    {
        Transform content = inventory.transform.Find("AllyItems/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (ally.weapon != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(ally.weapon.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)ally.weapon.GetIcon()]);
        }

        if (ally.armor != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(ally.armor.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)ally.armor.GetIcon()]);
        }

        if (ally.shield != null)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(ally.shield.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)ally.shield.GetIcon()]);
        }

        if ((ally.weapon != null || ally.armor != null || ally.shield != null) && ally.items.Count > 0)
            Instantiate(ui.lineSeparatorPrefab, content);

        foreach (ItemSlot itemSlot in ally.items)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            itemEntry.Init(itemSlot.ToString(Price.None));
            itemEntry.SetImage(ui.itemIcons[(int)itemSlot.item.GetIcon()]);
        }
    }

    public void Remove()
    {
        if (!game.world.Location.IsSafe())
        {
            ui.ShowDialog("You can only remove your allies in city or village.");
            return;
        }

        ui.ShowConfirm($"Are you sure you want to remove {ally.name} from your team?", () =>
        {
            game.Text.Set($"{ally.name} is sad and leave.");
            game.team.heroes.Remove(ally);
            game.team.CancelOutDebts();
            game.UpdateButtons();
            game.UpdateText();
            ui.CloseDialog();
        });
    }

    public void GiveGold()
    {
        ui.ShowInput($"How much gold give to {ally.name}?", count =>
        {
            count = Mathf.Min(count, player.gold);
            if (count <= 0)
                return true;
            player.AddGold(-count);
            ally.AddGold(count);
            ally.IncreaseAffectionFromValue(count);
            Refresh();
            game.UpdateText();
            return true;
        });
    }
}
