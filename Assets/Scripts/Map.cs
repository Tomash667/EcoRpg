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
    private Vector2 currentPos, travelPos;
    private Vector2Int travelPt;
    private bool inTravel;

    public void Build()
    {
        Tile[] map = Global.World.map;
        Transform tiles = transform.Find("Tiles");
        for (int y = 0; y < World.sizeY; ++y)
        {
            for (int x = 0; x < World.sizeX; ++x)
            {
                GameObject tile = Instantiate(tilePrefab, tiles);
                RectTransform rectTransform = tile.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new(gridOrigin.x + tileSize * x, gridOrigin.y - tileSize * y);
                Image image = tile.GetComponent<Image>();
                image.sprite = sprites[(int)map[x + y * World.sizeX].type];
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
        Vector2Int currentPt = Global.World.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;
        cursor2.SetActive(false);
        arrow.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (inTravel)
            return;

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
                Global.Game.Travel(targetPt, !Input.GetKey(KeyCode.LeftShift));
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

            UpdateText(world, world.currentPt, targetPt);
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

    public void BeginTravel(Vector2Int pt)
    {
        travelPt = pt;
        travelPos = new(gridOrigin.x + tileSize * travelPt.x, gridOrigin.y - tileSize * travelPt.y);
        cursor2.GetComponent<RectTransform>().anchoredPosition = travelPos;
        cursor2.SetActive(true);
        inTravel = true;
    }

    public void UpdateTravel()
    {
        World world = Global.World;
        Vector2Int currentPt = world.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;

        arrow.gameObject.SetActive(true);
        arrow.SetPosition(currentPos, travelPos);

        UpdateText(world, currentPt, travelPt);
    }

    private void UpdateText(World world, Vector2Int currentPt, Vector2Int targetPt)
    {
        int dist = World.CalculateDistance(currentPt, targetPt);
        int days = world.CalculateTravelDays(targetPt);
        string daysText;
        if (days == 0)
            daysText = "less then day";
        else if (days == 1)
            daysText = "1 day";
        else
            daysText = $"{days} days";
        Tile tile = world.map[targetPt.x + targetPt.y * World.sizeX];
        text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}\nTarget: {tile.Name.ToUpper1()}\nDistance: {dist}km\nTravel time: {daysText}";
        text.gameObject.SetActive(true);
    }

    public void EndTravel()
    {
        Vector2Int currentPt = Global.World.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;
        cursor2.SetActive(false);
        arrow.gameObject.SetActive(false);
        inTravel = false;
    }

    public void UpdateMap(Vector2Int pos)
    {
        int index = pos.x + pos.y * World.sizeX;
        Tile tile = Global.World.map[index];
        Transform tiles = transform.Find("Tiles");
        Image image = tiles.GetChild(index).GetComponent<Image>();
        image.sprite = sprites[(int)tile.type];
    }

    public void Regenerate()
    {
        foreach (Transform child in transform.Find("Tiles"))
            Destroy(child.gameObject);
        Build();
    }
}
