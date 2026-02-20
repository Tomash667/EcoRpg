using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    public const int sizeX = 20, sizeY = 10;
    public const float tileSize = 50f;
    public readonly Vector2 gridOrigin = new(-609f, 263f);

    public GameObject tilePrefab;
    public Sprite[] sprites;

    private Arrow arrow;
    private GameObject cursor, cursor2;
    private RectTransform rectTransform;
    private Vector2 currentPos;
    private Vector2Int currentPt;

    public void Start()
    {
        TileType[] map = new TileType[sizeX * sizeY];
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                TileType tileType = (Utility.Rand % 5) switch
                {
                    2 or 3 => TileType.Forest,
                    4 => TileType.Mountains,
                    _ => TileType.Plains
                };
                map[x + y * sizeX] = tileType;
            }
        }

        Vector2Int center = new(sizeX / 2, sizeY / 2);
        map[center.x + center.y * sizeX] = TileType.Plains;
        map[center.x - 1 + center.y * sizeX] = TileType.Plains;
        map[center.x + 1 + center.y * sizeX] = TileType.Plains;
        map[center.x + (center.y - 1) * sizeX] = TileType.Plains;
        map[center.x + (center.y + 1) * sizeX] = TileType.Plains;

        Dictionary<TileType, int> influence = new();
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                void AddInfluence(int x, int y, int value)
                {
                    if (x >= 0 && y >= 0 && x < sizeX && y < sizeY)
                    {
                        TileType tileType = map[x + y * sizeX];
                        influence[tileType] = influence.GetValueOrDefault(tileType) + value;
                    }
                }

                influence.Clear();
                AddInfluence(x, y, 5);
                AddInfluence(x - 1, y, 3);
                AddInfluence(x + 1, y, 3);
                AddInfluence(x, y - 1, 3);
                AddInfluence(x, y + 1, 3);
                AddInfluence(x - 1, y - 1, 1);
                AddInfluence(x - 1, y + 1, 1);
                AddInfluence(x + 1, y - 1, 1);
                AddInfluence(x + 1, y + 1, 1);
                map[x + y * sizeX] = influence.WeightedRandom();
            }
        }

        map[center.x + center.y * sizeX] = TileType.City;

        Transform tiles = transform.Find("Tiles");
        for (int y = 0; y < sizeY; ++y)
        {
            for (int x = 0; x < sizeX; ++x)
            {
                GameObject tile = Instantiate(tilePrefab, tiles);
                RectTransform rectTransform = tile.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new(gridOrigin.x + tileSize * x, gridOrigin.y - tileSize * y);
                Image image = tile.GetComponent<Image>();
                image.sprite = sprites[(int)map[x + y * sizeX]];
            }
        }

        currentPt = center;
        currentPos = new(gridOrigin.x + tileSize * center.x, gridOrigin.y - tileSize * center.y);
        cursor = transform.Find("Cursor").gameObject;
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;

        cursor2 = transform.Find("Cursor2").gameObject;
        cursor2.SetActive(false);

        arrow = transform.Find("Arrow").GetComponent<Arrow>();
        arrow.gameObject.SetActive(false);

        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            Input.mousePosition,
            null,
            out Vector2 localMousePosition
        );

        Vector2Int targetPt = LocalPosToTile(localMousePosition);
        if (targetPt.x >= 0 && targetPt.y >= 0 && targetPt.x < sizeX && targetPt.y < sizeY && targetPt != currentPt)
        {
            if (Input.GetMouseButtonDown(0))
            {
                currentPt = targetPt;
                currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
                cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;

                cursor2.SetActive(false);
                arrow.gameObject.SetActive(false);
            }
            else
            {
                Vector2 targetPos = new(gridOrigin.x + tileSize * targetPt.x, gridOrigin.y - tileSize * targetPt.y);
                cursor2.GetComponent<RectTransform>().anchoredPosition = targetPos;
                cursor2.SetActive(true);

                arrow.SetPosition(currentPos, targetPos);
                arrow.gameObject.SetActive(true);
            }
        }
        else
        {
            cursor2.SetActive(false);
            arrow.gameObject.SetActive(false);
        }
    }

    private Vector2Int LocalPosToTile(Vector2 localPos)
    {
        float dx = localPos.x - gridOrigin.x + tileSize / 2;
        float dy = gridOrigin.y - localPos.y + tileSize / 2;

        int x = Mathf.FloorToInt(dx / tileSize);
        int y = Mathf.FloorToInt(dy / tileSize);

        return new Vector2Int(x, y);
    }
}
