using UnityEngine;

public abstract class GameDialog : MonoBehaviour
{
    public static Game game;
    public static Player player;

    public virtual void Show()
    {
        Refresh();
        game.UI.ShowDialog(gameObject);
        AfterShow();
    }

    protected virtual void Refresh()
    {
    }

    protected virtual void AfterShow()
    {
    }

    public void RefreshIfOpen()
    {
        if (game.UI.IsOpen(gameObject))
            Refresh();
    }
}
