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
    private Button prevChapterBtn;
    private Button nextChapterBtn;
    private TextMeshProUGUI notesText;
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI chapterTitleText;
    private ScrollRect mapScroll;
    private RectTransform mapContent;
    private RectTransform pathLayer;
    private RectTransform nodeLayer;

    private RouteMapDataContainer container;
    private GlobalData globalData;
    private TMP_FontAsset font;
    private int chapterIndex;
    private readonly List<RouteMapNodeView> nodeViews = new List<RouteMapNodeView>();

    private static readonly Color PathUnlocked = new Color(0.89f, 0.76f, 0.48f, 0.95f);
    private static readonly Color PathLocked = new Color(0.32f, 0.28f, 0.24f, 0.55f);

    protected override void Awake()
    {
        EnsureHierarchy();
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
            existing.ShowMe();
            return;
        }

        string path = VNProjectConfig.Instance != null
            ? VNProjectConfig.Instance.UI_RouteMapPath
            : "VNovelizerRes/VNPrefabs/UI/RouteMap";
        if (Resources.Load<GameObject>(path + "/RouteMapPanel") != null)
        {
            ui.ShowPanel<RouteMapPanel>("RouteMapPanel", path, E_UI_Layer.Middle, null);
            return;
        }

        if (ui == null || ui.canvas == null)
        {
            Debug.LogError("[RouteMapPanel] 无法显示路线图：UIManager / Canvas 未就绪");
            return;
        }

        GameObject go = new GameObject("RouteMapPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RouteMapPanel));
        go.transform.SetParent(ui.canvas, false);
        RouteMapPanel panel = go.GetComponent<RouteMapPanel>();
        ui.panelDic["RouteMapPanel"] = panel;
        panel.ShowMe();
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
    }

    private void OnDestroy()
    {
        if (backBtn != null) backBtn.onClick.RemoveListener(Close);
        if (prevChapterBtn != null) prevChapterBtn.onClick.RemoveListener(OnPrevChapter);
        if (nextChapterBtn != null) nextChapterBtn.onClick.RemoveListener(OnNextChapter);
        EventCenter.GetInstance().RemoveEventListener<string>("RouteNodeUnlocked", OnRouteUnlocked);
    }

    protected override void OnButtonClick(string ButtonName)
    {
        if (ButtonName == "BackBtn" || ButtonName == "RM_CloseBtn")
            Close();
    }

    private void Close()
    {
        UIManager.GetInstance().HidePanel("RouteMapPanel");
    }

    private void CacheControls()
    {
        backBtn = GetControl<Button>("BackBtn");
        prevChapterBtn = GetControl<Button>("PrevChapterBtn");
        nextChapterBtn = GetControl<Button>("NextChapterBtn");
        notesText = GetNamed<TextMeshProUGUI>("NotesText");
        progressText = GetNamed<TextMeshProUGUI>("ProgressText");
        chapterTitleText = GetNamed<TextMeshProUGUI>("ChapterTitleText");
        mapScroll = GetControl<ScrollRect>("MapScroll");
        if (mapScroll != null)
            mapContent = mapScroll.content;
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
        if (prevChapterBtn != null) prevChapterBtn.onClick.AddListener(OnPrevChapter);
        if (nextChapterBtn != null) nextChapterBtn.onClick.AddListener(OnNextChapter);
    }

    private void Refresh()
    {
        LoadData();
        if (container == null || container.chapters == null || container.chapters.Count == 0)
        {
            if (notesText != null) notesText.text = "还没有配置路线图。\n请打开 VNovelizer / 路线图编辑器。";
            if (progressText != null) progressText.text = "当前进度  0%";
            ClearMap();
            return;
        }

        chapterIndex = Mathf.Clamp(chapterIndex, 0, container.chapters.Count - 1);
        RouteMapChapter chapter = container.chapters[chapterIndex];
        if (chapterTitleText != null)
            chapterTitleText.text = string.IsNullOrEmpty(chapter.title) ? chapter.id : chapter.title;
        if (notesText != null)
            notesText.text = chapter.notes ?? "";
        if (prevChapterBtn != null)
            prevChapterBtn.gameObject.SetActive(container.chapters.Count > 1);
        if (nextChapterBtn != null)
            nextChapterBtn.gameObject.SetActive(container.chapters.Count > 1);

        BuildMap(chapter);
        UpdateProgress(chapter);
    }

    private void LoadData()
    {
        if (VNProjectConfig.Instance == null) return;

        string path = VNProjectConfig.Instance.RouteMap_DataPath + "/RouteMapDataContainer";
        container = ResourcesManager.GetInstance().Load<RouteMapDataContainer>(path);
        if (container == null)
            container = Resources.Load<RouteMapDataContainer>(path);

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

        mapContent.sizeDelta = new Vector2(maxX, maxY);

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

                bool unlocked = IsNodeUnlocked(from) && IsNodeUnlocked(to);
                CreatePath(from, to, unlocked);
            }
        }

        if (chapter.nodes != null)
        {
            for (int i = 0; i < chapter.nodes.Count; i++)
            {
                RouteMapNode node = chapter.nodes[i];
                if (node == null) continue;
                CreateNode(node, IsNodeUnlocked(node));
            }
        }
    }

    private void CreatePath(RouteMapNode from, RouteMapNode to, bool unlocked)
    {
        GameObject go = new GameObject("Path_" + from.id + "_" + to.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(RouteMapPathGraphic));
        go.transform.SetParent(pathLayer, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 mid = (from.position + to.position) * 0.5f;
        Vector2 dir = to.position - from.position;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        if (normal.sqrMagnitude > 0.001f) normal.Normalize();
        float bend = (from.pathBend + to.pathBend) * 0.5f;
        Vector2 control = mid + normal * bend;

        RouteMapPathGraphic graphic = go.GetComponent<RouteMapPathGraphic>();
        graphic.raycastTarget = false;
        graphic.SetPath(from.position, control, to.position, unlocked ? 5f : 3.2f, unlocked ? PathUnlocked : PathLocked);
    }

    private void CreateNode(RouteMapNode node, bool unlocked)
    {
        GameObject go = new GameObject("Node_" + node.id, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(nodeLayer, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        RouteMapNodeView view = go.AddComponent<RouteMapNodeView>();
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

    private bool IsNodeUnlocked(RouteMapNode node)
    {
        if (node == null) return false;
        if (node.startUnlocked) return true;
        if (string.IsNullOrEmpty(node.id)) return false;
        if (GlobalDataManager.GetInstance() != null && GlobalDataManager.GetInstance().IsRouteUnlocked(node.id))
            return true;
        if (globalData != null && globalData.UnlockedScenes != null && globalData.UnlockedScenes.Contains(node.id))
            return true;
        return false;
    }

    private void UpdateProgress(RouteMapChapter chapter)
    {
        int total = 0;
        int unlocked = 0;
        if (chapter.nodes != null)
        {
            for (int i = 0; i < chapter.nodes.Count; i++)
            {
                RouteMapNode node = chapter.nodes[i];
                if (node == null || node.kind != RouteMapNodeKind.Event) continue;
                total++;
                if (IsNodeUnlocked(node)) unlocked++;
            }
        }

        int percent = total == 0 ? 0 : Mathf.RoundToInt(unlocked * 100f / total);
        if (progressText != null)
            progressText.text = $"当前进度  {percent}%";
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

    private void OnPrevChapter()
    {
        if (container == null || container.chapters == null || container.chapters.Count == 0) return;
        chapterIndex = (chapterIndex - 1 + container.chapters.Count) % container.chapters.Count;
        Refresh();
    }

    private void OnNextChapter()
    {
        if (container == null || container.chapters == null || container.chapters.Count == 0) return;
        chapterIndex = (chapterIndex + 1) % container.chapters.Count;
        Refresh();
    }

    private void OnRouteUnlocked(string nodeId)
    {
        Refresh();
    }

    private void EnsureHierarchy()
    {
        RectTransform root = transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.06f, 0.05f, 0.97f);
        bg.raycastTarget = true;

        font = Resources.Load<TMP_FontAsset>("VNovelizerRes/Fonts/TMPFonts/SiYuan-Black-Normal SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        if (transform.Find("BackBtn") == null)
            CreateTextButton("BackBtn", "返回", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(160f, 56f), TextAlignmentOptions.Left);

        if (transform.Find("ChapterTitleText") == null)
            CreateLabel("ChapterTitleText", "", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(400f, 48f), 28, TextAlignmentOptions.Center);

        if (transform.Find("NotesText") == null)
            CreateLabel("NotesText", "", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(360f, 120f), 22, TextAlignmentOptions.TopRight);

        if (transform.Find("ProgressText") == null)
            CreateLabel("ProgressText", "当前进度  0%", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 24f), new Vector2(280f, 40f), 22, TextAlignmentOptions.BottomRight);

        if (transform.Find("PrevChapterBtn") == null)
            CreateTextButton("PrevChapterBtn", "上一章", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 24f), new Vector2(140f, 48f), TextAlignmentOptions.Left);

        if (transform.Find("NextChapterBtn") == null)
            CreateTextButton("NextChapterBtn", "下一章", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(190f, 24f), new Vector2(140f, 48f), TextAlignmentOptions.Left);

        if (transform.Find("MapScroll") == null)
        {
            GameObject scrollObj = new GameObject("MapScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObj.transform.SetParent(transform, false);
            RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 80f);
            scrollRt.offsetMax = new Vector2(0f, -150f);
            Image scrollImage = scrollObj.GetComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0.01f);

            GameObject contentObj = new GameObject("MapContent", typeof(RectTransform));
            contentObj.transform.SetParent(scrollObj.transform, false);
            mapContent = contentObj.GetComponent<RectTransform>();
            mapContent.anchorMin = new Vector2(0f, 0f);
            mapContent.anchorMax = new Vector2(0f, 0f);
            mapContent.pivot = new Vector2(0f, 0f);
            mapContent.anchoredPosition = Vector2.zero;
            mapContent.sizeDelta = new Vector2(2400f, 900f);

            GameObject pathObj = new GameObject("PathLayer", typeof(RectTransform));
            pathObj.transform.SetParent(mapContent, false);
            pathLayer = pathObj.GetComponent<RectTransform>();
            Stretch(pathLayer);

            GameObject nodeObj = new GameObject("NodeLayer", typeof(RectTransform));
            nodeObj.transform.SetParent(mapContent, false);
            nodeLayer = nodeObj.GetComponent<RectTransform>();
            Stretch(nodeLayer);

            ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
            scroll.content = mapContent;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = scrollRt;
            mapScroll = scroll;
        }
        else
        {
            mapScroll = transform.Find("MapScroll").GetComponent<ScrollRect>();
            mapContent = mapScroll != null ? mapScroll.content : null;
            if (mapContent != null)
            {
                Transform existingPath = mapContent.Find("PathLayer");
                pathLayer = existingPath != null ? existingPath as RectTransform : CreateLayer(mapContent, "PathLayer");
                Transform existingNode = mapContent.Find("NodeLayer");
                nodeLayer = existingNode != null ? existingNode as RectTransform : CreateLayer(mapContent, "NodeLayer");
            }
        }
    }

    private static RectTransform CreateLayer(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Stretch(rt);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private void CreateTextButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(anchorMin.x, anchorMax.y > 0.5f ? 1f : 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.02f);

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(go.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        Stretch(textRt);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.color = new Color(0.93f, 0.86f, 0.72f);
        tmp.alignment = align;
        tmp.raycastTarget = false;
    }

    private void CreateLabel(string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(anchorMin.x > 0.5f ? 1f : (anchorMin.x < 0.1f ? 0f : 0.5f), anchorMax.y > 0.5f ? 1f : 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.86f, 0.80f, 0.68f);
        tmp.alignment = align;
        tmp.raycastTarget = false;
    }
}
