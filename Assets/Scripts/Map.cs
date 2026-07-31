using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Map : MonoBehaviour
{
    private const float tileSize = 50f;
    private const float borderSize = 6f;
    private static readonly Vector2 gridOrigin = new(-tileSize * World.sizeX / 2 + tileSize / 2, tileSize * World.sizeY / 2 - tileSize / 2);
    private static readonly Vector2 gridSize = new(tileSize * World.sizeX, tileSize * World.sizeY);
    private static readonly Vector2 contentSize = new(gridSize.x + borderSize * 2, gridSize.y + borderSize * 2);

    public GameObject tilePrefab;
    public Sprite[] sprites;

    private Arrow arrow;
    private GameObject cursor, cursor2;
    private TMP_Text text, buttonText;
    private ScrollRect scrollRect;
    private Transform tilesContainer;
    private RectTransform rectTransform;
    private List<Vector2Int> path;
    private Vector2 currentPos, travelPos, viewportSize, moveViewPos, lastCheckedPos;
    private Vector2Int travelPt;
    private float zoom = 1f;
    private int travelStep;
    private bool inTravel, moveView;

    public void Init()
    {
        text = transform.Find("Text").GetComponent<TMP_Text>();
        buttonText = transform.Find("BtClose/Text").GetComponent<TMP_Text>();
        scrollRect = transform.Find("MapView").GetComponent<ScrollRect>();
        rectTransform = scrollRect.GetComponent<RectTransform>();
        viewportSize = transform.Find("MapView/Viewport").GetComponent<RectTransform>().rect.size;
        Transform mapContent = transform.Find("MapView/Viewport/Content");
        mapContent.GetComponent<RectTransform>().sizeDelta = contentSize;
        cursor = mapContent.Find("Cursor").gameObject;
        cursor2 = mapContent.Find("Cursor2").gameObject;
        arrow = mapContent.Find("Arrow").GetComponent<Arrow>();
        tilesContainer = mapContent.Find("Tiles");
    }

    public void Build()
    {
        Tile[] map = Global.World.map;
        tilesContainer = transform.Find("MapView/Viewport/Content/Tiles");
        for (int y = 0; y < World.sizeY; ++y)
        {
            for (int x = 0; x < World.sizeX; ++x)
            {
                Tile tile = map[x + y * World.sizeX];
                GameObject tileObj = Instantiate(tilePrefab, tilesContainer);
                RectTransform rectTransform = tileObj.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new(gridOrigin.x + tileSize * x, gridOrigin.y - tileSize * y);
                Image image = tileObj.GetComponent<Image>();
                image.sprite = sprites[(int)tile.image];
                image.color = tile.discovered ? Color.white : Color.gray;
            }
        }
    }

    public void Show()
    {
        Vector2Int currentPt = Global.World.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;
        cursor2.SetActive(false);
        arrow.gameObject.SetActive(false);
        CenterOnPlayer();
    }

    private void Update()
    {
        if (Input.mouseScrollDelta != Vector2.zero)
        {
            float newZoom = Mathf.Clamp(zoom + Input.mouseScrollDelta.y / 10, 0.7f, 1f);
            if (newZoom != zoom)
            {
                zoom = newZoom;
                tilesContainer.parent.localScale = new(zoom, zoom, zoom);
                CenterOnPlayer();
            }
        }

        if (inTravel)
        {
            CenterOnPlayer();
            if (Input.GetKeyDown(GameUI.escKey))
                Global.World.cancelTravel = true;
            return;
        }

        if (Input.GetMouseButtonDown(1) && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
        {
            moveView = true;
            moveViewPos = Input.mousePosition;
            cursor2.SetActive(false);
            arrow.gameObject.SetActive(false);
            text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}";
            lastCheckedPos.x = -1;
        }

        if (moveView)
        {
            if (!Input.GetMouseButton(1))
                moveView = false;
            else
            {
                Vector2 newPos = Input.mousePosition;
                if (moveViewPos != newPos)
                {
                    Vector2 dif = newPos - moveViewPos;
                    Vector2 scrollRange = contentSize - viewportSize;
                    Vector2 scrollPos = scrollRect.normalizedPosition;
                    scrollPos -= new Vector2(dif.x / scrollRange.x, dif.y / scrollRange.y);
                    scrollPos.x = Mathf.Clamp01(scrollPos.x);
                    scrollPos.y = Mathf.Clamp01(scrollPos.y);
                    scrollRect.normalizedPosition = scrollPos;
                    moveViewPos = newPos;
                }
                return;
            }
        }

        if (!GetTile(Input.mousePosition, out Vector2Int targetPt))
            targetPt.x = -1;

        World world = Global.World;

        if ((targetPt == world.currentPt || path != null) && Input.GetMouseButtonDown(0))
            Global.Game.Travel(targetPt, !Input.GetKey(KeyCode.LeftShift));

        if (targetPt == lastCheckedPos)
            return;

        lastCheckedPos = targetPt;
        if (targetPt.x == -1)
            path = null;
        else
            path = world.FindPath(world.currentPt, targetPt);

        if (targetPt.x == -1)
        {
            // outside map
            arrow.gameObject.SetActive(false);
            cursor2.SetActive(false);
            text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}";
        }
        else
        {
            Tile tile = world.map[targetPt.x + targetPt.y * World.sizeX];
            if (targetPt == world.currentPt)
            {
                // same position as current
                arrow.gameObject.SetActive(false);
                cursor2.SetActive(false);
                text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}\nTarget: {tile.Name.ToUpper1()}";
            }
            else
            {
                // new position
                Vector2 targetPos = new(gridOrigin.x + tileSize * targetPt.x, gridOrigin.y - tileSize * targetPt.y);
                cursor2.GetComponent<RectTransform>().anchoredPosition = targetPos;
                cursor2.SetActive(true);
                if (path == null)
                {
                    // blocked
                    arrow.gameObject.SetActive(false);
                    text.text = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}\nTarget: {tile.Name.ToUpper1()}";
                }
                else
                {
                    // ok
                    arrow.gameObject.SetActive(true);
                    arrow.SetPath(path, gridOrigin, tileSize);
                    UpdateText(world, targetPt);
                }
            }
        }
    }

    private bool GetTile(Vector2 pos, out Vector2Int pt)
    {
        // check if pos is inside map
        if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, pos))
        {
            pt = new(-1, -1);
            return false;
        }

        // transform to local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tilesContainer.parent.transform as RectTransform,
            pos,
            null,
            out Vector2 localPos
        );
        localPos += contentSize * 0.5f;

        Vector2 offset = Vector2.zero;
        float dx = localPos.x - borderSize + offset.x;
        float dy = gridSize.y - localPos.y + borderSize - offset.y;
        pt = new Vector2Int(Mathf.FloorToInt(dx / tileSize), Mathf.FloorToInt(dy / tileSize));
        return World.IsInBounds(pt);
    }

    public void BeginTravel(Vector2Int pt)
    {
        travelPt = pt;
        travelPos = new(gridOrigin.x + tileSize * travelPt.x, gridOrigin.y - tileSize * travelPt.y);
        cursor2.GetComponent<RectTransform>().anchoredPosition = travelPos;
        cursor2.SetActive(true);
        buttonText.text = "Stop";
        inTravel = true;
        travelStep = 0;
    }

    public void UpdateTravel()
    {
        World world = Global.World;
        Vector2Int currentPt = world.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;

        if (world.travelStep > travelStep)
        {
            path.RemoveAt(0);
            arrow.Progress();
            ++travelStep;
        }

        UpdateText(world, travelPt);
    }

    private void UpdateText(World world, Vector2Int targetPt)
    {
        int dist = World.CalculateDistance(path);
        int days = world.CalculateTravelDays(path);
        string daysText;
        if (days == 0)
            daysText = "less then day";
        else if (days == 1)
            daysText = "1 day";
        else
            daysText = $"{days} days";
        Tile tile = world.map[targetPt.x + targetPt.y * World.sizeX];
        string str;
#if UNITY_EDITOR
        str = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}\nTarget: {tile.Name.ToUpper1()} [{World.CalculateIndex(targetPt.x, targetPt.y, 0)}]\nDistance: {dist}km\n" +
            $"Travel time: {daysText}\nLevels: {tile.levels}";
#else
        str = $"Rations: {Global.Game.CountTeamItem(Item.Get("rations"))}\nTarget: {tile.Name.ToUpper1()}\nDistance: {dist}km\nTravel time: {daysText}";
#endif
        text.text = str;
        text.gameObject.SetActive(true);
    }

    public void EndTravel()
    {
        Vector2Int currentPt = Global.World.currentPt;
        currentPos = new(gridOrigin.x + tileSize * currentPt.x, gridOrigin.y - tileSize * currentPt.y);
        cursor.GetComponent<RectTransform>().anchoredPosition = currentPos;
        cursor2.SetActive(false);
        arrow.gameObject.SetActive(false);
        buttonText.text = "Cancel";
        inTravel = false;
    }

    public void UpdateMap(Vector2Int pos)
    {
        int index = pos.x + pos.y * World.sizeX;
        Tile tile = Global.World.map[index];
        Image image = tilesContainer.GetChild(index).GetComponent<Image>();
        image.sprite = sprites[(int)tile.image];
        image.color = tile.discovered ? Color.white : Color.gray;
    }

    public void Regenerate()
    {
        foreach (Transform child in tilesContainer)
            Destroy(child.gameObject);
        Build();
        Show();
    }

    private void CenterOnPlayer()
    {
        World world = Global.World;

        // Player position inside content
        Vector2 playerPos = new(tileSize * world.currentPt.x, tileSize * world.currentPt.y);
        Vector2 desiredPos = playerPos - (viewportSize * 0.5f);
        Vector2 scrollRange = contentSize - viewportSize;

        // Convert to normalized position (0–1)
        Vector2 normalized = new(desiredPos.x / scrollRange.x, desiredPos.y / scrollRange.y);

        // Flip Y because ScrollRect uses bottom-left origin
        normalized.y = 1f - normalized.y;

        normalized.x = Mathf.Clamp01(normalized.x);
        normalized.y = Mathf.Clamp01(normalized.y);

        scrollRect.normalizedPosition = normalized;
    }

    public void Cancel()
    {
        if (inTravel)
            Global.World.cancelTravel = true;
        else
            Global.UI.CloseDialog();
    }
}
