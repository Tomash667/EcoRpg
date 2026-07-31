using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    private RectTransform head, line;
    private int step;

    private void Awake()
    {
        head = transform.Find("Head").GetComponent<RectTransform>();
        line = transform.Find("Line").GetComponent<RectTransform>();
    }

    public void SetPath(List<Vector2Int> path, Vector2 offset, float tileSize)
    {
        int segments = transform.childCount - 1;
        int reqSegments = path.Count - 1;
        if (segments < reqSegments)
        {
            for (int i = segments; i < reqSegments; ++i)
                Instantiate(line, transform);
        }
        for (int i = 0; i < reqSegments; ++i)
            transform.GetChild(i + 1).gameObject.SetActive(true);
        for (int i = reqSegments; i < segments; ++i)
            transform.GetChild(i + 1).gameObject.SetActive(false);

        for (int i = 1; i < path.Count; ++i)
        {
            Vector2Int from = path[i - 1];
            Vector2Int to = path[i];
            Vector2 fromPos = new(offset.x + from.x * tileSize, offset.y - from.y * tileSize);
            Vector2 toPos = new(offset.x + to.x * tileSize, offset.y - to.y * tileSize);
            Vector2 dir = (toPos - fromPos).normalized;
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, dir);
            Vector2 mid = fromPos + (toPos - fromPos) / 2;
            float dist = (toPos - fromPos).magnitude;
            if (i + 1 == path.Count)
            {
                head.anchoredPosition = toPos - dir * 20;
                head.rotation = rot;
                dist -= 10;
                mid -= dir * 5;
            }
            else
                dist += 2;
            RectTransform rectTransform = transform.GetChild(i).transform as RectTransform;
            rectTransform.anchoredPosition = mid;
            rectTransform.rotation = rot;
            rectTransform.localScale = new(1f, dist / 100f);
        }

        step = 1;
    }

    public void Progress()
    {
        transform.GetChild(step).gameObject.SetActive(false);
        ++step;
    }
}
