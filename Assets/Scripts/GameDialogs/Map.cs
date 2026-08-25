using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Map : GameDialog
{
    private const float tileSize = 50f;
    private const float borderSize = 6f;
    private static readonly Vector2 gridOrigin = new(-tileSize * World.sizeX / 2 + tileSize / 2, tileSize * World.sizeY / 2 - tileSize / 2);
    private static readonly Vector2 gridSize = new(tileSize * World.sizeX, tileSize * World.sizeY);
    private static readonly Vector2 contentSize = new(gridSize.x + borderSize * 2, gridSize.y + borderSize * 2);

    public GameObject tilePrefab, mapIconPrefab;
    public Sprite[] sprites;

    private Arrow arrow;
    private GameObject flag, outlineTarget, outlineQuest;
    private TMP_Text sideText, buttonText;
    private ScrollRect scrollRect;
    private Transform tilesContainer, iconsContainer;
    private RectTransform rectTransform;
    private List<Vector2Int> path;
    private Vector2 currentPos, travelPos, viewportSize, moveViewPos, lastCheckedPos;
    private Vector2Int travelPt;
    private float zoom = 1f;
    private int travelStep;
    private bool inTravel, moveView;

    public void Init()
    {
        sideText = transform.Find("Text").GetComponent<TMP_Text>();
        buttonText = transform.Find("BtClose/Text").GetComponent<TMP_Text>();
        scrollRect = transform.Find("MapView").GetComponent<ScrollRect>();
        rectTransform = scrollRect.GetComponent<RectTransform>();
        viewportSize = transform.Find("MapView/Viewport").GetComponent<RectTransform>().rect.size;
        Transform mapContent = transform.Find("MapView/Viewport/Content");
        mapContent.GetComponent<RectTransform>().sizeDelta = contentSize;
        flag = mapContent.Find("Flag").gameObject;
        outlineTarget = mapContent.Find("OutlineTarget").gameObject;
        outlineQuest = mapContent.Find("OutlineQuest").gameObject;
        arrow = mapContent.Find("Arrow").GetComponent<Arrow>();
        tilesContainer = mapContent.Find("Tiles");
        iconsContainer = mapContent.Find("Icons");
    }

    public void Build()
    {
        // this is called before GameDialog members are set!
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

    public override void Show()
    {
        World world = game.world;
        currentPos = PtToPos(world.currentPt);
        flag.GetComponent<RectTransform>().anchoredPosition = currentPos;
        outlineTarget.SetActive(false);
        Quest quest = game.activeQuests.FirstOrDefault(x => x.tracked && x.location != -1 && world.GetLocation(x.location).discovered);
        if (quest != null)
        {
            outlineQuest.GetComponent<RectTransform>().anchoredPosition = PtToPos(World.IndexToPoint(quest.location));
            outlineQuest.SetActive(true);
        }
        else
            outlineQuest.SetActive(false);
        arrow.gameObject.SetActive(false);
        CenterOnPlayer();
        ui.ShowDialog(gameObject);
    }

    private void Update()
    {
        if (Input.mouseScrollDelta != Vector2.zero && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
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
            if (Input.GetKeyDown(GameUI.escKey))
                game.world.cancelTravel = true;
            return;
        }

        if (Input.GetMouseButtonDown(1) && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
        {
            moveView = true;
            moveViewPos = Input.mousePosition;
            outlineTarget.SetActive(false);
            arrow.gameObject.SetActive(false);
            sideText.text = $"Rations: {game.team.CountItem(Item.Get("ration"))}";
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

        World world = game.world;

        if ((targetPt == world.currentPt || path != null) && Input.GetMouseButtonDown(0))
            game.Travel(targetPt, !Input.GetKey(KeyCode.LeftShift));

#if UNITY_EDITOR
        if (targetPt != world.currentPt && path != null && Input.GetKeyDown(KeyCode.T))
        {
            game.Teleport(targetPt);
            currentPos = PtToPos(world.currentPt);
            flag.GetComponent<RectTransform>().anchoredPosition = currentPos;
            arrow.gameObject.SetActive(false);
        }
#endif

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
            outlineTarget.SetActive(false);
            sideText.text = $"Rations: {game.team.CountItem(Item.Get("ration"))}";
        }
        else
        {
            Tile tile = world.map[targetPt.x + targetPt.y * World.sizeX];
            outlineTarget.GetComponent<RectTransform>().anchoredPosition = PtToPos(targetPt);
            outlineTarget.SetActive(true);
            if (targetPt == world.currentPt)
            {
                // same position as current
                arrow.gameObject.SetActive(false);
                sideText.text = $"Rations: {game.team.CountItem(Item.Get("ration"))}\nTarget: {tile.Name.ToUpper1()}";
            }
            else
            {
                // new position
                if (path == null)
                {
                    // blocked
                    arrow.gameObject.SetActive(false);
                    sideText.text = $"Rations: {game.team.CountItem(Item.Get("ration"))}\nTarget: {tile.Name.ToUpper1()}";
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
        travelPos = PtToPos(pt);
        outlineTarget.GetComponent<RectTransform>().anchoredPosition = travelPos;
        outlineTarget.SetActive(true);
        buttonText.text = "Stop";
        inTravel = true;
        travelStep = 0;
    }

    public void UpdateTravel()
    {
        World world = game.world;
        Vector2 startPos = PtToPos(world.currentPt);
        Vector2 targetPos = startPos + new Vector2(tileSize * world.travelDir.x, -tileSize * world.travelDir.y);
        currentPos = Vector2.Lerp(startPos, targetPos, world.travelDelta);
        flag.GetComponent<RectTransform>().anchoredPosition = currentPos;
        CenterOnPlayer();

        if (world.travelStep >= travelStep)
        {
            if (path.Count > 2)
            {
                path.RemoveAt(0);
                arrow.Progress();
            }
            else
                arrow.gameObject.SetActive(false);
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
        str = $"Rations: {game.team.CountItem(Item.Get("ration"))}\nTarget: {tile.Name.ToUpper1()} [{World.CalculateIndex(targetPt.x, targetPt.y, 0)}]\nDistance: {dist}km\n" +
            $"Travel time: {daysText}\nLevels: {tile.levels}";
#else
        str = $"Rations: {game.team.CountItem(Item.Get("ration"))}\nTarget: {tile.Name.ToUpper1()}\nDistance: {dist}km\nTravel time: {daysText}";
#endif
        sideText.text = str;
        sideText.gameObject.SetActive(true);
    }

    public void EndTravel()
    {
        currentPos = PtToPos(game.world.currentPt);
        flag.GetComponent<RectTransform>().anchoredPosition = currentPos;
        outlineTarget.SetActive(false);
        arrow.gameObject.SetActive(false);
        buttonText.text = "Cancel";
        inTravel = false;
    }

    public void UpdateMap(Vector2Int pt)
    {
        int index = pt.x + pt.y * World.sizeX;
        Tile tile = game.world.map[index];
        Image image = tilesContainer.GetChild(index).GetComponent<Image>();
        image.sprite = sprites[(int)tile.image];
        image.color = tile.discovered ? Color.white : Color.gray;
        Quest quest = game.activeQuests.FirstOrDefault(x => x.tracked && x.location == index);
        if (quest != null)
        {
            outlineQuest.GetComponent<RectTransform>().anchoredPosition = PtToPos(World.IndexToPoint(quest.location));
            outlineQuest.SetActive(true);
        }
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
        // Player position inside content
        Vector2 playerPos = new(currentPos.x - gridOrigin.x, -(currentPos.y - gridOrigin.y));
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
            game.world.cancelTravel = true;
        else
            ui.CloseDialog();
    }

    private Vector2 PtToPos(Vector2Int pt)
    {
        return new(gridOrigin.x + tileSize * pt.x, gridOrigin.y - tileSize * pt.y);
    }

    public void AddIcon(Vector2Int pt)
    {
        GameObject mapIcon = Instantiate(mapIconPrefab, iconsContainer);
        mapIcon.GetComponent<RectTransform>().anchoredPosition = PtToPos(pt);
        mapIcon.GetComponent<MapIcon>().pt = pt;
    }

    public void RemoveIcon(Vector2Int pt)
    {
        MapIcon mapIcon = iconsContainer.GetComponentsInChildren<MapIcon>().FirstOrDefault(x => x.pt == pt);
        if (mapIcon != null)
            Destroy(mapIcon.gameObject);
    }
}
