using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 主界面路线图：查看已走过的剧情节点和尚未开启的分支。
/// </summary>
public class RouteMapPanel : BasePanel
{
    private Button backBtn;
    private Transform chapterList;
    private GameObject chapterItemPrefab;
    private ScrollRect mapScroll;
    private RectTransform mapContent;
    private RectTransform pathLayer;
    private RectTransform nodeLayer;
    private GameObject nodePrefab;

    private List<RouteMapChapter> chapters = new List<RouteMapChapter>();
    private GlobalData globalData;
    private TMP_FontAsset font;
    private int chapterIndex;
    private readonly List<RouteMapNodeView> nodeViews = new List<RouteMapNodeView>();
    private float mapZoom = 1f;
    private float pinchDistance = -1f;

    private static readonly Color PathUnlocked = new Color(0.89f, 0.76f, 0.48f, 0.95f);
    private static readonly Color PathLocked = new Color(0.32f, 0.28f, 0.24f, 0.55f);
    private const float MinZoom = 0.4f;
    private const float MaxZoom = 2.5f;

    protected override void Awake()
    {
        base.Awake();
        CacheControls();
        BindEvents();
        EventCenter.GetInstance().AddEventListener<string>("RouteNodeUnlocked", OnRouteUnlocked);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Refresh();
    }

    public static void ShowFromMenu()
    {
        UIManager ui = UIManager.GetInstance();
        if (ui != null && ui.canvas == null)
            ui.Init();

        RouteMapPanel existing = ui != null ? ui.GetPanel<RouteMapPanel>("RouteMapPanel") : null;
        if (existing != null)
        {
            HideMainMenu();
            existing.ShowMe();
            return;
        }

        string path = VNProjectConfig.Instance != null
            ? VNProjectConfig.Instance.UI_RouteMapPath
            : "VNovelizerRes/VNPrefabs/UI/RouteMap";
        if (Resources.Load<GameObject>(path + "/RouteMapPanel") != null)
        {
            HideMainMenu();
            ui.ShowPanel<RouteMapPanel>("RouteMapPanel", path, E_UI_Layer.Top, null);
            return;
        }

        Debug.LogError("[RouteMapPanel] 找不到 RouteMapPanel 预制件: " + path);
    }

    public override void ShowMe()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public override void HideMe()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
        HandleMapZoom();
    }

    private void OnDestroy()
    {
        if (backBtn != null) backBtn.onClick.RemoveListener(Close);
        EventCenter.GetInstance().RemoveEventListener<string>("RouteNodeUnlocked", OnRouteUnlocked);
    }

    protected override void OnButtonClick(string ButtonName)
    {
        if (ButtonName == "BackBtn" || ButtonName == "CloseBtn" || ButtonName == "RM_CloseBtn")
            Close();
    }

    private void Close()
    {
        UIManager.GetInstance().HidePanel("RouteMapPanel");
        ShowMainMenu();
    }

    private static void HideMainMenu()
    {
        MainMenuPanel menu = UIManager.GetInstance() != null
            ? UIManager.GetInstance().GetPanel<MainMenuPanel>("MainMenuPanel")
            : Object.FindFirstObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
        if (menu != null)
            menu.gameObject.SetActive(false);
    }

    private static void ShowMainMenu()
    {
        MainMenuPanel menu = UIManager.GetInstance() != null
            ? UIManager.GetInstance().GetPanel<MainMenuPanel>("MainMenuPanel")
            : Object.FindFirstObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
        if (menu != null)
            menu.gameObject.SetActive(true);
    }

    private void CacheControls()
    {
        backBtn = GetControl<Button>("CloseBtn");
        if (backBtn == null) backBtn = GetControl<Button>("BackBtn");
        chapterList = FindDeep(transform, "ChapterList");
        Transform item = FindDeep(transform, "ChapterItem");
        if (item != null) chapterItemPrefab = item.gameObject;
        mapScroll = GetControl<ScrollRect>("MapScroll");
        if (mapScroll != null)
        {
            mapContent = mapScroll.content;
            mapScroll.scrollSensitivity = 0f;
        }
        if (mapContent != null)
        {
            Transform existingPath = mapContent.Find("PathLayer");
            pathLayer = existingPath as RectTransform;
            Transform existingNode = mapContent.Find("NodeLayer");
            nodeLayer = existingNode as RectTransform;
        }
        string nodePath = VNProjectConfig.Instance != null
            ? VNProjectConfig.Instance.UI_RouteMapPath + "/RouteMapNode"
            : "VNovelizerRes/VNPrefabs/UI/RouteMap/RouteMapNode";
        nodePrefab = Resources.Load<GameObject>(nodePath);
    }

    private T GetNamed<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        if (t == null) t = FindDeep(transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void BindEvents()
    {
        if (backBtn != null) backBtn.onClick.AddListener(Close);
    }

    private void Refresh()
    {
        LoadData();
        if (chapters == null || chapters.Count == 0)
        {
            RebuildChapterList();
            ClearMap();
            return;
        }

        chapterIndex = Mathf.Clamp(chapterIndex, 0, chapters.Count - 1);
        RebuildChapterList();
        BuildMap(chapters[chapterIndex]);
    }

    private void LoadData()
    {
        if (VNProjectConfig.Instance == null) return;

        chapters = RouteMapGraph.LoadAllFromResources();

        if (globalData == null && GlobalDataManager.GetInstance() != null)
            globalData = GlobalDataManager.GetInstance().GetGlobalData();

        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>("VNovelizerRes/Fonts/TMPFonts/SiYuan-Black-Normal SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;
        }
    }

    private void BuildMap(RouteMapChapter chapter)
    {
        if (mapContent == null || pathLayer == null || nodeLayer == null) return;

        ClearMap();

        float maxX = 1600f;
        float maxY = 900f;
        if (chapter.nodes != null)
        {
            for (int i = 0; i < chapter.nodes.Count; i++)
            {
                RouteMapNode node = chapter.nodes[i];
                if (node == null) continue;
                maxX = Mathf.Max(maxX, node.position.x + 240f);
                maxY = Mathf.Max(maxY, node.position.y + 180f);
            }
        }

        mapContent.pivot = new Vector2(0f, 1f);
        mapContent.anchorMin = new Vector2(0f, 1f);
        mapContent.anchorMax = new Vector2(0f, 1f);
        mapContent.sizeDelta = new Vector2(maxX, maxY);
        mapContent.anchoredPosition = Vector2.zero;
        ResetMapZoom();
        if (pathLayer != null) pathLayer.sizeDelta = mapContent.sizeDelta;
        if (nodeLayer != null) nodeLayer.sizeDelta = mapContent.sizeDelta;

        Dictionary<string, RouteMapNode> lookup = new Dictionary<string, RouteMapNode>();
        if (chapter.nodes != null)
        {
            for (int i = 0; i < chapter.nodes.Count; i++)
            {
                RouteMapNode node = chapter.nodes[i];
                if (node == null || string.IsNullOrEmpty(node.id)) continue;
                lookup[node.id] = node;
            }
        }

        if (chapter.edges != null)
        {
            for (int i = 0; i < chapter.edges.Count; i++)
            {
                RouteMapEdge edge = chapter.edges[i];
                if (edge == null) continue;
                RouteMapNode from;
                RouteMapNode to;
                if (!lookup.TryGetValue(edge.fromId, out from) || !lookup.TryGetValue(edge.toId, out to))
                    continue;

                bool fromVisible = ShouldShowNode(from);
                bool toVisible = ShouldShowNode(to);
                if (!fromVisible || !toVisible) continue;
                bool unlocked = IsNodeUnlocked(from) && IsNodeUnlocked(to);
                CreatePath(from, to, unlocked);
            }
        }

        if (chapter.nodes != null)
        {
            for (int i = 0; i < chapter.nodes.Count; i++)
            {
                RouteMapNode node = chapter.nodes[i];
                if (node == null || !ShouldShowNode(node)) continue;
                CreateNode(node, IsNodeUnlocked(node));
            }
        }
    }

    private void CreatePath(RouteMapNode from, RouteMapNode to, bool unlocked)
    {
        GameObject go = new GameObject("Path_" + from.id + "_" + to.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(RouteMapPathGraphic));
        go.transform.SetParent(pathLayer, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = mapContent != null ? mapContent.sizeDelta : new Vector2(2400f, 900f);

        Vector2 start = EdgePoint(from, true);
        Vector2 end = EdgePoint(to, false);
        Vector2 c1;
        Vector2 c2;
        RouteMapGraph.GetCubicControls(start, end, out c1, out c2);

        RouteMapPathGraphic graphic = go.GetComponent<RouteMapPathGraphic>();
        graphic.raycastTarget = false;
        graphic.SetPath(start, c1, c2, end, unlocked ? 5f : 3.2f, unlocked ? PathUnlocked : PathLocked);
    }

    private static Vector2 EdgePoint(RouteMapNode node, bool outgoing)
    {
        bool junction = node != null && node.kind == RouteMapNodeKind.Junction;
        float width = junction ? 22f : 168f;
        float height = junction ? 22f : 72f;
        Vector2 topLeft = node != null ? node.position : Vector2.zero;
        float x = outgoing ? topLeft.x + width : topLeft.x;
        float y = -(topLeft.y + height * 0.5f);
        return new Vector2(x, y);
    }

    private void RebuildChapterList()
    {
        if (chapterList == null) return;

        for (int i = chapterList.childCount - 1; i >= 0; i--)
        {
            Transform child = chapterList.GetChild(i);
            if (chapterItemPrefab != null && child.gameObject == chapterItemPrefab)
            {
                child.gameObject.SetActive(false);
                continue;
            }
            Destroy(child.gameObject);
        }

        if (chapterItemPrefab == null) return;
        chapterItemPrefab.SetActive(false);

        for (int i = 0; i < chapters.Count; i++)
        {
            RouteMapChapter chapter = chapters[i];
            if (chapter == null) continue;
            GameObject item = Instantiate(chapterItemPrefab, chapterList);
            item.name = "ChapterItem_" + chapter.id;
            item.SetActive(true);
            TMP_Text label = item.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = string.IsNullOrEmpty(chapter.title) ? chapter.id : chapter.title;

            Button button = item.GetComponent<Button>();
            if (button == null) button = item.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                int captured = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectChapter(captured));
            }
        }
    }

    private void SelectChapter(int index)
    {
        if (chapters == null || index < 0 || index >= chapters.Count) return;
        chapterIndex = index;
        BuildMap(chapters[chapterIndex]);
    }

    private void CreateNode(RouteMapNode node, bool unlocked)
    {
        GameObject go;
        if (nodePrefab != null)
        {
            go = Instantiate(nodePrefab, nodeLayer);
            go.name = "Node_" + node.id;
        }
        else
        {
            go = new GameObject("Node_" + node.id, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(nodeLayer, false);
            go.AddComponent<RouteMapNodeView>();
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(node.position.x, -node.position.y);

        RouteMapNodeView view = go.GetComponent<RouteMapNodeView>();
        if (view == null) view = go.AddComponent<RouteMapNodeView>();
        view.Init(node, unlocked, font, OnNodeClicked);
        nodeViews.Add(view);
    }

    private void OnNodeClicked(RouteMapNode node, bool unlocked)
    {
        if (node == null || node.kind == RouteMapNodeKind.Junction) return;
        if (!unlocked)
        {
            Debug.Log($"[RouteMap] 节点未解锁: {node.id}");
            return;
        }

        if (string.IsNullOrEmpty(node.scriptName))
            return;

        MainMenuPanel mainMenu = UIManager.GetInstance().GetPanel<MainMenuPanel>("MainMenuPanel");
        bool wasMainMenuVisible = mainMenu != null && mainMenu.gameObject.activeSelf;
        HideMe();
        if (mainMenu != null)
            mainMenu.gameObject.SetActive(false);

        VNManager.GetInstance().StartSceneReplay(node.scriptName, node.startLineID, node.endLineID, wasMainMenuVisible);
    }

    private bool IsDeveloperMode()
    {
        return VNProjectConfig.Instance != null && VNProjectConfig.Instance.RouteMapDeveloperMode;
    }

    private RouteMapUnvisitedMode UnvisitedMode()
    {
        return VNProjectConfig.Instance != null
            ? VNProjectConfig.Instance.RouteMapUnvisitedMode
            : RouteMapUnvisitedMode.Hide;
    }

    private bool ShouldShowNode(RouteMapNode node)
    {
        if (node == null) return false;
        if (IsDeveloperMode() || IsNodeUnlocked(node)) return true;
        return UnvisitedMode() == RouteMapUnvisitedMode.Lock;
    }

    private bool IsNodeUnlocked(RouteMapNode node)
    {
        if (node == null) return false;
        if (IsDeveloperMode()) return true;
        if (node.startUnlocked) return true;
        if (string.IsNullOrEmpty(node.id)) return false;
        if (GlobalDataManager.GetInstance() != null && GlobalDataManager.GetInstance().IsRouteUnlocked(node.id))
            return true;
        if (globalData != null && globalData.UnlockedScenes != null && globalData.UnlockedScenes.Contains(node.id))
            return true;
        return false;
    }

    private void HandleMapZoom()
    {
        if (mapContent == null || mapScroll == null) return;

        RectTransform viewport = mapScroll.viewport != null
            ? mapScroll.viewport
            : mapScroll.transform as RectTransform;
        Camera cam = GetCanvasCamera();

        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.touches.Count >= 2
            && touch.touches[0].press.isPressed
            && touch.touches[1].press.isPressed)
        {
            Vector2 a = touch.touches[0].position.ReadValue();
            Vector2 b = touch.touches[1].position.ReadValue();
            Vector2 mid = (a + b) * 0.5f;
            if (RectTransformUtility.RectangleContainsScreenPoint(viewport, mid, cam))
            {
                float dist = Vector2.Distance(a, b);
                if (pinchDistance > 1f && dist > 1f)
                    ZoomAt(mid, dist / pinchDistance);
                pinchDistance = dist;
                mapScroll.enabled = false;
                return;
            }
        }

        if (pinchDistance > 0f)
        {
            pinchDistance = -1f;
            mapScroll.enabled = true;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;
        Vector2 pointer = mouse.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, pointer, cam))
            return;
        if (Mathf.Abs(scroll) > 10f) scroll /= 120f;
        ZoomAt(pointer, Mathf.Pow(1.12f, Mathf.Clamp(scroll, -3f, 3f)));
    }

    private void ZoomAt(Vector2 screenPos, float factor)
    {
        if (mapContent == null || Mathf.Approximately(factor, 1f)) return;

        float next = Mathf.Clamp(mapZoom * factor, MinZoom, MaxZoom);
        if (Mathf.Approximately(next, mapZoom)) return;

        RectTransform parent = mapContent.parent as RectTransform;
        if (parent == null) return;

        Vector2 pointer;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screenPos, GetCanvasCamera(), out pointer))
            return;

        Vector2 fromPivot = (pointer - mapContent.anchoredPosition) / mapZoom;
        mapZoom = next;
        mapContent.localScale = new Vector3(mapZoom, mapZoom, 1f);
        mapContent.anchoredPosition = pointer - fromPivot * mapZoom;
    }

    private void ResetMapZoom()
    {
        mapZoom = 1f;
        pinchDistance = -1f;
        if (mapContent != null)
            mapContent.localScale = Vector3.one;
        if (mapScroll != null)
            mapScroll.enabled = true;
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera;
    }

    private void ClearMap()
    {
        nodeViews.Clear();
        ClearChildren(pathLayer);
        ClearChildren(nodeLayer);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void OnRouteUnlocked(string nodeId)
    {
        Refresh();
    }
}
