using UnityEngine;

public abstract class GameDialog : MonoBehaviour
{
    public static Game game;
    public static GameUI ui;
    public static Player player;
    public static readonly TextBuilder tb = new();

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
}
