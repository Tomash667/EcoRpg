using System.Collections;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Journal : MonoBehaviour
{
    private readonly StringBuilder sb = new();

    public void Show()
    {
        Global.UI.ShowDialog(gameObject);
        Refresh();
    }

    private void Refresh()
    {
        Game game = Global.Game;
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
        StartCoroutine(MoveScrollRectToPos(transform.Find("Notifications").GetComponent<ScrollRect>(), 0f));

        // active quests
        Transform content = transform.Find("List/Viewport/Content");
        foreach (Transform child in content)
            Destroy(child.gameObject);

        foreach (Quest quest in game.activeQuests)
        {
            ItemEntry itemEntry = Instantiate(game.UI.itemEntryPrefab, content).GetComponent<ItemEntry>();
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

    private IEnumerator MoveScrollRectToPos(ScrollRect scrollRect, float pos)
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = pos;
    }
}
