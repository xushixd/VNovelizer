using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class RouteMapEditor : EditorWindow
{
    private readonly List<ChapterEntry> chapters = new List<ChapterEntry>();
    private int chapterIndex = -1;
    private RouteMapChapter graph;
    private Vector2 leftScroll;
    private Vector2 graphScroll = new Vector2(40f, 40f);
    private string selectedNodeId = "";
    private bool dragging;
    private string dragNodeId = "";
    private Vector2 dragOffset;
    private string status = "";

    private const float NodeW = 168f;
    private const float NodeH = 72f;
    private const float JunctionSize = 22f;
    private static readonly Color Bg = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color NodeFill = new Color(0.22f, 0.28f, 0.36f);
    private static readonly Color NodeSelected = new Color(0.28f, 0.42f, 0.58f);
    private static readonly Color EdgeColor = new Color(0.89f, 0.76f, 0.48f, 0.9f);
    private readonly Vector3[] curvePoints = new Vector3[24];

    private class ChapterEntry
    {
        public string scriptName;
        public string path;
        public ChapterData data;
    }

    [MenuItem("VNovelizer/路线图编辑器 (Route Map Editor)", false, 27)]
    public static void ShowWindow()
    {
        var wnd = GetWindow<RouteMapEditor>();
        wnd.titleContent = new GUIContent("路线图编辑器");
        wnd.minSize = new Vector2(980, 560);
    }

    private void OnEnable()
    {
        RefreshChapters();
    }

    private void OnGUI()
    {
        Rect body = new Rect(8, 8, position.width - 16, position.height - 16);
        const float leftWidth = 260f;
        const float gap = 10f;
        const float toolbarHeight = 56f;

        Rect toolbar = new Rect(body.x, body.y, leftWidth, toolbarHeight);
        Rect chapterList = new Rect(body.x, toolbar.yMax + gap, leftWidth, body.height - toolbarHeight - gap);
        Rect graphRect = new Rect(toolbar.xMax + gap, body.y, body.width - leftWidth - gap, body.height);

        DrawPanel(toolbar);
        DrawPanel(chapterList);
        DrawPanel(graphRect);

        DrawToolbar(toolbar);
        DrawChapterList(chapterList);
        DrawGraph(graphRect);
    }

    private static void DrawPanel(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));
        Handles.color = new Color(0.28f, 0.28f, 0.28f);
        Handles.DrawAAPolyLine(2f,
            new Vector3(rect.xMin, rect.yMin),
            new Vector3(rect.xMax, rect.yMin),
            new Vector3(rect.xMax, rect.yMax),
            new Vector3(rect.xMin, rect.yMax),
            new Vector3(rect.xMin, rect.yMin));
    }

    private void DrawToolbar(Rect rect)
    {
        float buttonWidth = (rect.width - 36f) * 0.5f;
        float y = rect.y + 12f;
        if (GUI.Button(new Rect(rect.x + 12f, y, buttonWidth, 32f), "刷新"))
            RefreshChapters();
        EditorGUI.BeginDisabledGroup(GetCurrent() == null);
        if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, y, buttonWidth, 32f), "初始化"))
            InitFromSegments();
        EditorGUI.EndDisabledGroup();
    }

    private void DrawChapterList(Rect rect)
    {
        GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 20f), "章节列表", EditorStyles.boldLabel);

        Rect listRect = new Rect(rect.x + 8f, rect.y + 36f, rect.width - 16f, rect.height - 44f);
        Rect view = new Rect(0, 0, listRect.width - 16f, Mathf.Max(listRect.height, chapters.Count * 32f + 8f));
        leftScroll = GUI.BeginScrollView(listRect, leftScroll, view);

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterEntry entry = chapters[i];
            string label = entry.data != null && !string.IsNullOrEmpty(entry.data.title)
                ? entry.data.title
                : entry.scriptName;
            Rect row = new Rect(0, i * 32f, view.width, 28f);
            if (GUI.Toggle(row, chapterIndex == i, label, "Button") && chapterIndex != i)
                SelectChapter(i);
        }

        if (chapters.Count == 0)
            GUI.Label(new Rect(4, 4, view.width - 8f, 40f), string.IsNullOrEmpty(status) ? "没有章节" : status);

        GUI.EndScrollView();
    }

    private void DrawGraph(Rect canvas)
    {
        EditorGUI.DrawRect(canvas, Bg);

        if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
        {
            GUI.Label(new Rect(canvas.center.x - 80f, canvas.center.y - 10f, 160f, 20f), "路线图", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Vector2 size = GraphSize();
        graphScroll = GUI.BeginScrollView(canvas, graphScroll, new Rect(0, 0, size.x, size.y));

        Handles.BeginGUI();
        if (graph.edges != null)
        {
            for (int i = 0; i < graph.edges.Count; i++)
            {
                RouteMapEdge edge = graph.edges[i];
                if (edge == null) continue;
                RouteMapNode from = graph.FindNode(edge.fromId);
                RouteMapNode to = graph.FindNode(edge.toId);
                if (from == null || to == null) continue;
                Vector2 a = NodeRight(from);
                Vector2 b = NodeLeft(to);
                Vector2 c1;
                Vector2 c2;
                RouteMapGraph.GetCubicControls(a, b, out c1, out c2);
                RouteMapGraph.SampleCubic(a, c1, c2, b, curvePoints);
                Handles.color = EdgeColor;
                Handles.DrawAAPolyLine(3f, curvePoints);
            }
        }
        Handles.EndGUI();

        Event e = Event.current;
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            RouteMapNode node = graph.nodes[i];
            if (node == null) continue;
            Rect rect = NodeRect(node);
            bool selected = node.id == selectedNodeId;
            EditorGUI.DrawRect(rect, selected ? NodeSelected : NodeFill);
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16),
                string.IsNullOrEmpty(node.title) ? node.id : node.title);

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                selectedNodeId = node.id;
                dragging = true;
                dragNodeId = node.id;
                dragOffset = e.mousePosition - node.position;
                e.Use();
                Repaint();
            }
        }

        if (dragging && e.type == EventType.MouseDrag && e.button == 0)
        {
            RouteMapNode node = graph.FindNode(dragNodeId);
            if (node != null)
            {
                node.position = e.mousePosition - dragOffset;
                node.position.x = Mathf.Max(0f, node.position.x);
                node.position.y = Mathf.Max(0f, node.position.y);
                e.Use();
                Repaint();
            }
        }

        if (e.type == EventType.MouseUp && e.button == 0 && dragging)
        {
            dragging = false;
            SaveCurrent();
        }

        GUI.EndScrollView();
    }

    private void RefreshChapters()
    {
        chapters.Clear();
        string folder = "Assets/Resources/VNovelizerRes/VNScripts";
        if (VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.VNScriptResPath))
            folder = "Assets/Resources/" + VNProjectConfig.Instance.VNScriptResPath;

        if (!Directory.Exists(folder))
        {
            status = "找不到剧本目录: " + folder;
            return;
        }

        string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].EndsWith(".graph.json")) continue;
            string text = File.ReadAllText(files[i]);
            ChapterData data = ScriptParser.TryParseChapter(text);
            if (data == null) continue;
            chapters.Add(new ChapterEntry
            {
                scriptName = Path.GetFileNameWithoutExtension(files[i]),
                path = files[i],
                data = data
            });
        }

        if (chapters.Count == 0)
            status = "没有可识别的 Chapter JSON。";
        else if (chapterIndex < 0 || chapterIndex >= chapters.Count)
            SelectChapter(0);
        else
            LoadGraphForCurrent();
    }

    private void SelectChapter(int index)
    {
        chapterIndex = index;
        selectedNodeId = "";
        LoadGraphForCurrent();
    }

    private ChapterEntry GetCurrent()
    {
        if (chapterIndex < 0 || chapterIndex >= chapters.Count) return null;
        return chapters[chapterIndex];
    }

    private void LoadGraphForCurrent()
    {
        ChapterEntry entry = GetCurrent();
        graph = null;
        if (entry == null || entry.data == null) return;

        string assetPath = RouteMapGraph.GraphAssetPath(entry.data.id);
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        if (asset != null)
            graph = RouteMapGraph.FromJson(asset.text);
        if (graph == null)
            graph = RouteMapGraph.CreateFromChapter(entry.data, entry.scriptName);
        status = entry.data.title + " / " + (graph.nodes != null ? graph.nodes.Count : 0) + " 个节点";
    }

    private void InitFromSegments()
    {
        ChapterEntry entry = GetCurrent();
        if (entry == null || entry.data == null) return;
        graph = RouteMapGraph.CreateFromChapter(entry.data, entry.scriptName);
        SaveCurrent();
        status = "已从 Segment 初始化 " + graph.nodes.Count + " 个节点";
    }

    private void SaveCurrent()
    {
        ChapterEntry entry = GetCurrent();
        if (entry == null || graph == null) return;
        if (string.IsNullOrEmpty(graph.id))
            graph.id = entry.data != null ? entry.data.id : entry.scriptName;
        if (string.IsNullOrEmpty(graph.scriptName))
            graph.scriptName = entry.scriptName;

        if (!Directory.Exists(RouteMapGraph.DefaultFolder))
            Directory.CreateDirectory(RouteMapGraph.DefaultFolder);

        string path = RouteMapGraph.GraphAssetPath(graph.id);
        File.WriteAllText(path, RouteMapGraph.ToJson(graph));
        AssetDatabase.Refresh();
        status = "已保存 " + path;
    }

    private RouteMapNode FindSelected()
    {
        return graph != null ? graph.FindNode(selectedNodeId) : null;
    }

    private static Rect NodeRect(RouteMapNode node)
    {
        float w = node.kind == RouteMapNodeKind.Junction ? JunctionSize : NodeW;
        float h = node.kind == RouteMapNodeKind.Junction ? JunctionSize : NodeH;
        return new Rect(node.position.x, node.position.y, w, h);
    }

    private static Vector2 NodeCenter(RouteMapNode node)
    {
        Rect r = NodeRect(node);
        return r.center;
    }

    private Vector2 GraphSize()
    {
        float maxX = 1200f;
        float maxY = 800f;
        if (graph != null && graph.nodes != null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] == null) continue;
                Rect r = NodeRect(graph.nodes[i]);
                maxX = Mathf.Max(maxX, r.xMax + 120f);
                maxY = Mathf.Max(maxY, r.yMax + 120f);
            }
        }
        return new Vector2(maxX, maxY);
    }

    private static Vector2 NodeRight(RouteMapNode node)
    {
        Rect r = NodeRect(node);
        return new Vector2(r.xMax, r.center.y);
    }

    private static Vector2 NodeLeft(RouteMapNode node)
    {
        Rect r = NodeRect(node);
        return new Vector2(r.xMin, r.center.y);
    }
}

public static class RouteMapPrefabBuilder
{
    public const string DefaultDataFolder = RouteMapGraph.DefaultFolder;
    public const string DefaultPrefabFolder = "Assets/Resources/VNovelizerRes/VNPrefabs/UI/RouteMap";

    public static void EnsureInProject()
    {
        if (!Directory.Exists(DefaultDataFolder))
            Directory.CreateDirectory(DefaultDataFolder);
        if (!Directory.Exists(DefaultPrefabFolder))
            Directory.CreateDirectory(DefaultPrefabFolder);

        string prefabPath = DefaultPrefabFolder + "/RouteMapPanel.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            string template = "Packages/com.fakecorps.vnovelizer/Runtime/PackageDefault/VNovelizerRes/VNPrefabs/UI/RouteMap/RouteMapPanel.prefab";
            if (AssetDatabase.CopyAsset(template, prefabPath))
                Debug.Log("[RouteMap] 已从模板复制 RouteMapPanel");
            else
                Debug.LogWarning("[RouteMap] 工程里还没有 RouteMapPanel 预制件，请从插件模板拷贝。");
        }

        string skipFolder = "Assets/Resources/VNovelizerRes/VNPrefabs/UI/Video";
        if (!Directory.Exists(skipFolder)) Directory.CreateDirectory(skipFolder);
        string skipPath = skipFolder + "/VideoSkipBtn.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(skipPath) == null)
        {
            GameObject skip = new GameObject("VideoSkipBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            RectTransform skipRt = skip.GetComponent<RectTransform>();
            skipRt.anchorMin = new Vector2(1f, 1f);
            skipRt.anchorMax = new Vector2(1f, 1f);
            skipRt.pivot = new Vector2(1f, 1f);
            skipRt.anchoredPosition = new Vector2(-24f, -24f);
            skipRt.sizeDelta = new Vector2(140f, 48f);
            skip.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.45f);
            GameObject label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(skip.transform, false);
            RectTransform labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "跳过";
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(skip, skipPath);
            Object.DestroyImmediate(skip);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
