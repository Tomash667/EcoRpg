using System.Collections;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Journal : GameDialog
{
    private readonly StringBuilder sb = new();

    public override void Refresh()
    {
        bool notificationChanges = false;

        // notifications
        sb.Clear();
        if (game.notifications.Any(x => x.status != Notification.Status.Waiting))
        {
            foreach (Notification notification in game.notifications.Where(x => x.status != Notification.Status.Waiting))
            {
                if (notification.status == Notification.Status.Available)
                    sb.Append("<b>");
                sb.Append($"Day {notification.day} - {notification.text}");
                if (notification.status == Notification.Status.Available)
                {
                    sb.Append("</b>");
                    notification.status = Notification.Status.Read;
                    notificationChanges = true;
                }
                sb.Append("\n");
            }
        }
        else
            sb.Append("...");
        transform.Find("Notifications/Viewport/Content/Text").GetComponent<TMP_Text>().text = sb.ToString();

        // active quests
        Transform content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (Quest quest in game.activeQuests)
        {
            ItemEntry itemEntry = Instantiate(ui.itemEntryPrefab, content).GetComponent<ItemEntry>();
            if (quest.tracked)
                itemEntry.Init(quest.TextReward);
            else
            {
                itemEntry.Init(quest.TextReward, "Track", () =>
                {
                    Quest prevQuest = game.activeQuests.FirstOrDefault(x => x.tracked);
                    if (prevQuest != null)
                        prevQuest.tracked = false;
                    quest.tracked = true;
                    Refresh();
                    game.UpdateText();
                });
            }
        }

        if (notificationChanges)
            game.UpdateButtons();
    }

    protected override void AfterShow()
    {
        StartCoroutine(MoveScrollRectToPos(transform.Find("Notifications").GetComponent<ScrollRect>(), 0f));
    }

    private IEnumerator MoveScrollRectToPos(ScrollRect scrollRect, float pos)
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = pos;
    }
}
