using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class PeopleScreen : GameDialog
{
    public PropertiesScreen propertiesScreen;

    private bool recruitWorkers;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P) && !recruitWorkers)
            propertiesScreen.Show();
    }

    public override void Show()
    {
        recruitWorkers = false;
        Refresh();
        ui.CloseDialog();
        transform.Find("BtProperties").gameObject.SetActive(true);
        transform.Find("BtClose").gameObject.SetActive(false);
        transform.Find("BtClose2").gameObject.SetActive(true);
        ui.ShowDialog(gameObject);
    }

    public void ShowRecruit()
    {
        recruitWorkers = true;
        Refresh();
        transform.Find("BtProperties").gameObject.SetActive(false);
        transform.Find("BtClose").gameObject.SetActive(true);
        transform.Find("BtClose2").gameObject.SetActive(false);
        ui.ShowDialog(gameObject);
    }

    public override void Refresh()
    {
        // header
        TMP_Text header = transform.Find("Header").GetComponent<TMP_Text>();
        if (recruitWorkers)
            header.text = "Available people:";
        else
            header.text = $"Hired people ({2 * game.hiredWorkers.Count} upkeep):";

        // text
        transform.Find("Text").GetComponent<TMP_Text>().text = game.Text.Flush();

        // list
        Transform content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        if (recruitWorkers)
        {
            int cityIndex = game.world.CityIndex;
            foreach (Worker worker in game.availableWorkers.Where(x => x.locationIndex == cityIndex).OrderBy(x => x.name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                itemEntry.Init(worker.ToStringHire(), "Hire", () =>
                {
                    int cost = worker.Cost;
                    if (player.gold < cost)
                    {
                        ui.ShowDialog($"You need {cost} gold.");
                        return;
                    }

                    game.Text.Set($"You pay <color=#FFD700>{cost}</color> gold to hire {worker.name}.");
                    player.AddGold(-cost);
                    worker.locationIndex = -1;
                    game.hiredWorkers.Add(worker);
                    game.availableWorkers.Remove(worker);
                    game.AddTime(minutes: 15);
                    RefreshIfOpen();
                    game.UpdateText();
                });
            }
        }
        else
        {
            foreach (Worker worker in game.hiredWorkers.OrderBy(x => x.name))
            {
                ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
                Property property = game.GetProperty(worker.locationIndex);
                string actionText;
                UnityAction action;
                Property selectedProperty = propertiesScreen.SelectedProperty;
                if (selectedProperty == null || selectedProperty.income == 0)
                {
                    actionText = null;
                    action = null;
                }
                else if (selectedProperty == property)
                {
                    actionText = "Unassign";
                    action = () =>
                    {
                        game.Text.Set($"You unassign {worker.name} from {selectedProperty.Name}.");
                        worker.locationIndex = -1;
                        Refresh();
                    };
                }
                else
                {
                    actionText = "Assign";
                    action = () =>
                    {
                        int locationIndex = game.GetLocationIndex(selectedProperty);
                        Worker currentWorker = game.hiredWorkers.FirstOrDefault(x => x.locationIndex == locationIndex);
                        if (currentWorker != null)
                        {
                            game.Text.Set($"You unassign {currentWorker.name} and assign {worker.name} to {selectedProperty.Name}.");
                            currentWorker.locationIndex = -1;
                        }
                        else
                            game.Text.Set($"You assign {worker.name} to {selectedProperty.Name}.");
                        worker.locationIndex = locationIndex;
                        Refresh();
                    };
                }

                itemEntry.Init2(worker.ToStringHired(property?.Name), actionText, action, "Fire", () =>
                {
                    game.Text.Set($"You fire {worker.name}.");
                    game.hiredWorkers.Remove(worker);
                    Refresh();
                });
            }
        }
    }
}
