using UnityEngine;

public class Arrow : MonoBehaviour
{
    private RectTransform head, line;

    private void Awake()
    {
        head = transform.Find("Head").GetComponent<RectTransform>();
        line = transform.Find("Line").GetComponent<RectTransform>();
    }

    public void SetPosition(Vector2 from, Vector2 to)
    {
        Vector2 dir = (to - from).normalized;
        Quaternion rot = Quaternion.LookRotation(Vector3.forward, dir);
        head.anchoredPosition = to - dir * 20;
        head.rotation = rot;

        Vector2 mid = from + (to - from) / 2;
        float dist = (to - from).magnitude;
        dist -= 10;
        mid -= dir * 5;
        line.anchoredPosition = mid;
        line.rotation = rot;
        line.localScale = new(1f, dist / 100f);
    }
}
