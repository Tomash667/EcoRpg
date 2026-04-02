using UnityEngine;
using UnityEngine.Events;

public class ItemEntryList : MonoBehaviour
{
    public UnityEvent onSelectionChanged;

    private ItemEntry selected;

    public object GetSelectedData()
    {
        if (selected != null)
            return selected.data;
        else
            return null;
    }

    public void Select(ItemEntry itemEntry)
    {
        if (itemEntry == selected)
            return;

        if (selected != null)
            selected.SetSelected(false);
        selected = itemEntry;
        if (selected != null)
            selected.SetSelected(true);
        onSelectionChanged.Invoke();
    }

    public void Clear()
    {
        selected = null;
    }
}
