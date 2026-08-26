using UnityEngine;

public abstract class GameDialog : MonoBehaviour
{
    public static Game game;
    public static GameUI ui;
    public static Player player;
    public static TextBuilder text;

    public virtual bool Autoclose => true;
    public bool IsOpen => ui.IsOpen(gameObject);

    public virtual void Show()
    {
        Refresh();
        ui.ShowDialog(gameObject);
        AfterShow();
    }

    public virtual void Refresh()
    {
    }

    protected virtual void AfterShow()
    {
    }

    public void RefreshIfOpen()
    {
        if (ui.IsOpen(gameObject))
            Refresh();
    }

    public virtual void Restore()
    {
        Refresh();
    }

    public void AddTimeAndRefresh(int hours = 0, int minutes = 0, System.Action callback = null)
    {
        game.AddTime(hours, minutes);
        if (ui.IsOpen(gameObject))
        {
            game.AddText(text.Flush());
            if (callback != null)
                callback.Invoke();
            else
                Refresh();
        }
        game.UpdateText();
    }
}
