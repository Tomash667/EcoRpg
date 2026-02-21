using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    public const float tileSize = 50f;
    public readonly Vector2 gridOrigin = new(-609f, 263f);

    public GameObject tilePrefab;
    public Sprite[] sprites;

    private Arrow arrow;
    private GameObject cursor, cursor2;
    private TMP_Text text;
    private RectTransform rectTransform;
    private Vector2 currentPos;

    public void Build()
    {
        TileType[] map = Global.Game.world.map;
        Transform tiles = transform.Find("Tiles");
        for (int y = 0; y < World.sizeY; ++y)
        {
            for (int x = 0; x < World.sizeX; ++x)
            {
                GameObject tile = Instantiate(tilePrefab, tiles);
                RectTransform rectTransform = tile.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new(gridOrigin.x + tileSize * x, gridOrigin.y - tileSize * y);
                Image image = tile.GetComponent<Image>();
                image.sprite = sprites[(int)map[x + y * World.sizeX]];
            }
        }

        cursor = transform.Find("Cursor").gameObject;
        cursor2 = transform.Find("Cursor2").gameObject;
        arrow = transform.Find("Arrow").GetComponent<Arrow>();
        text = transform.Find("Text").GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void Show()
    {
        Vector2Int currentPt = Global.Game.world.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;
        cursor2.SetActive(false);
        arrow.gameObject.SetActive(false);
    }

    private void Update()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            Input.mousePosition,
            null,
            out Vector2 localMousePosition
        );

        World world = Global.World;
        Vector2Int targetPt = LocalPosToTile(localMousePosition);
        if (World.IsInBounds(targetPt.x, targetPt.y))
        {
            if (Input.GetMouseButtonDown(0))
            {
                Global.Game.Travel(targetPt);
                return;
            }

            if (targetPt == world.currentPt)
                targetPt.x = -1;
        }
        else
            targetPt.x = -1;

        if (targetPt.x != -1)
        {
            Vector2 targetPos = new(gridOrigin.x + tileSize * targetPt.x, gridOrigin.y - tileSize * targetPt.y);
            cursor2.GetComponent<RectTransform>().anchoredPosition = targetPos;
            cursor2.SetActive(true);

            arrow.gameObject.SetActive(true);
            arrow.SetPosition(currentPos, targetPos);

            int dist = CalculateDistance(world.currentPt, targetPt);
            int days = dist / 40;
            string daysText;
            if (days == 0)
                daysText = "less then day";
            else if (days == 1)
                daysText = "1 day";
            else
                daysText = $"{days} days";
            text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}\nTarget: {world.map[targetPt.x + targetPt.y * World.sizeX]}\nDistance: {dist}km\nTravel time: {daysText}";
            text.gameObject.SetActive(true);
        }
        else
        {
            cursor2.SetActive(false);
            arrow.gameObject.SetActive(false);
            text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}";
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

    private int CalculateDistance(Vector2Int a, Vector2Int b)
    {
        Vector2Int dist = a - b;
        int distX = Mathf.Abs(dist.x);
        int distY = Mathf.Abs(dist.y);
        int diagonalDist = Mathf.Min(distX, distY);
        int straightDist = Mathf.Max(distX, distY) - diagonalDist;
        return diagonalDist * 15 + straightDist * 10;
    }
}
