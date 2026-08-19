using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class RouteMapEditor : EditorWindow
{
    private RouteMapDataContainer container;
    private int chapterIndex;
    private int nodeIndex = -1;
    private int edgeIndex = -1;
    private Vector2 leftScroll;
    private Vector2 rightScroll;

    [MenuItem("VNovelizer/路线图编辑器 (Route Map Editor)", false, 27)]
    public static void ShowWindow()
    {
        var wnd = GetWindow<RouteMapEditor>();
        wnd.titleContent = new GUIContent("路线图编辑器");
        wnd.minSize = new Vector2(880, 560);
    }

    private void OnEnable()
    {
        LoadContainer();
    }

    private void OnGUI()
    {
        if (container == null)
        {
            EditorGUILayout.HelpBox("未找到 RouteMapDataContainer。", MessageType.Warning);
            if (GUILayout.Button("创建数据容器", GUILayout.Height(32)))
            {
                RouteMapPrefabBuilder.EnsureInProject();
                LoadContainer();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawLeft();
        DrawRight();
        EditorGUILayout.EndHorizontal();

        if (GUI.changed)
            EditorUtility.SetDirty(container);
    }

    private void LoadContainer()
    {
        string resPath = "VNovelizerRes/GalleryContent/RouteMap/RouteMapDataContainer";
        if (VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.RouteMap_DataPath))
            resPath = VNProjectConfig.Instance.RouteMap_DataPath + "/RouteMapDataContainer";

        container = Resources.Load<RouteMapDataContainer>(resPath);
        if (container == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:RouteMapDataContainer");
            if (guids.Length > 0)
                container = AssetDatabase.LoadAssetAtPath<RouteMapDataContainer>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void DrawLeft()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(280));
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        EditorGUILayout.LabelField("章节", EditorStyles.boldLabel);
        if (GUILayout.Button("新建章节"))
        {
            container.chapters.Add(new RouteMapChapter
            {
                id = "chapter_" + (container.chapters.Count + 1).ToString("00"),
                title = "新章节"
            });
            chapterIndex = container.chapters.Count - 1;
            nodeIndex = -1;
            edgeIndex = -1;
        }

        for (int i = 0; i < container.chapters.Count; i++)
        {
            RouteMapChapter chapter = container.chapters[i];
            string label = chapter == null ? "[空]" : (string.IsNullOrEmpty(chapter.title) ? chapter.id : chapter.title);
            if (GUILayout.Toggle(chapterIndex == i, label, "Button"))
            {
                if (chapterIndex != i)
                {
                    chapterIndex = i;
                    nodeIndex = -1;
                    edgeIndex = -1;
                }
            }
        }

        RouteMapChapter current = GetCurrentChapter();
        if (current != null)
        {
            GUILayout.Space(12);
            EditorGUILayout.LabelField("节点", EditorStyles.boldLabel);
            if (GUILayout.Button("新建事件节点"))
                AddNode(current, RouteMapNodeKind.Event);
            if (GUILayout.Button("新建分岔点"))
                AddNode(current, RouteMapNodeKind.Junction);

            if (current.nodes != null)
            {
                for (int i = 0; i < current.nodes.Count; i++)
                {
                    RouteMapNode node = current.nodes[i];
                    string kind = node != null && node.kind == RouteMapNodeKind.Junction ? "点" : "事";
                    string name = node == null ? "[空]" : (string.IsNullOrEmpty(node.title) ? node.id : node.title);
                    if (GUILayout.Toggle(nodeIndex == i, $"[{kind}] {name}", "Button"))
                    {
                        nodeIndex = i;
                        edgeIndex = -1;
                    }
                }
            }

            GUILayout.Space(12);
            EditorGUILayout.LabelField("连线", EditorStyles.boldLabel);
            if (GUILayout.Button("新建连线") && current.nodes != null && current.nodes.Count >= 2)
            {
                current.edges.Add(new RouteMapEdge
                {
                    fromId = current.nodes[0].id,
                    toId = current.nodes[1].id
                });
                edgeIndex = current.edges.Count - 1;
                nodeIndex = -1;
            }

            if (current.edges != null)
            {
                for (int i = 0; i < current.edges.Count; i++)
                {
                    RouteMapEdge edge = current.edges[i];
                    string label = edge == null ? "[空]" : $"{edge.fromId} → {edge.toId}";
                    if (GUILayout.Toggle(edgeIndex == i, label, "Button"))
                    {
                        edgeIndex = i;
                        nodeIndex = -1;
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawRight()
    {
        EditorGUILayout.BeginVertical();
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        RouteMapChapter chapter = GetCurrentChapter();
        if (chapter == null)
        {
            EditorGUILayout.HelpBox("请选择或新建一个章节。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("章节信息", EditorStyles.boldLabel);
        chapter.id = EditorGUILayout.TextField("章节 ID", chapter.id);
        chapter.title = EditorGUILayout.TextField("标题", chapter.title);
        EditorGUILayout.LabelField("右上角提示");
        chapter.notes = EditorGUILayout.TextArea(chapter.notes, GUILayout.MinHeight(64));

        if (GUILayout.Button("删除此章节", GUILayout.Width(120)))
        {
            container.chapters.RemoveAt(chapterIndex);
            chapterIndex = Mathf.Max(0, chapterIndex - 1);
            nodeIndex = -1;
            edgeIndex = -1;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        GUILayout.Space(16);

        if (nodeIndex >= 0 && chapter.nodes != null && nodeIndex < chapter.nodes.Count)
            DrawNode(chapter, chapter.nodes[nodeIndex]);
        else if (edgeIndex >= 0 && chapter.edges != null && edgeIndex < chapter.edges.Count)
            DrawEdge(chapter, chapter.edges[edgeIndex]);
        else
            EditorGUILayout.HelpBox("选择左侧节点或连线进行编辑。\n位置以像素计，原点在地图左下角，建议 X 从左到右递增。", MessageType.None);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawNode(RouteMapChapter chapter, RouteMapNode node)
    {
        if (node == null) return;

        EditorGUILayout.LabelField("节点", EditorStyles.boldLabel);
        node.id = EditorGUILayout.TextField("节点 ID", node.id);
        node.title = EditorGUILayout.TextField("标题", node.title);
        node.kind = (RouteMapNodeKind)EditorGUILayout.EnumPopup("类型", node.kind);
        node.position = EditorGUILayout.Vector2Field("位置", node.position);
        node.pathBend = EditorGUILayout.FloatField("路径起伏", node.pathBend);
        node.startUnlocked = EditorGUILayout.Toggle("开局解锁", node.startUnlocked);
        node.unlockedSprite = (Sprite)EditorGUILayout.ObjectField("解锁缩略图", node.unlockedSprite, typeof(Sprite), false);
        node.lockedSprite = (Sprite)EditorGUILayout.ObjectField("未解锁占位图", node.lockedSprite, typeof(Sprite), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("可选回放（留空则只展示，不跳转）", EditorStyles.miniLabel);
        node.scriptName = EditorGUILayout.TextField("剧本名", node.scriptName);
        node.startLineID = EditorGUILayout.TextField("开始行 ID", node.startLineID);
        node.endLineID = EditorGUILayout.TextField("结束行 ID", node.endLineID);

        EditorGUILayout.Space();
        if (GUILayout.Button("删除此节点", GUILayout.Width(120)))
        {
            string removedId = node.id;
            chapter.nodes.RemoveAt(nodeIndex);
            if (chapter.edges != null)
                chapter.edges.RemoveAll(e => e != null && (e.fromId == removedId || e.toId == removedId));
            nodeIndex = -1;
        }
    }

    private void DrawEdge(RouteMapChapter chapter, RouteMapEdge edge)
    {
        if (edge == null) return;

        EditorGUILayout.LabelField("连线", EditorStyles.boldLabel);
        List<string> ids = new List<string>();
        if (chapter.nodes != null)
        {
            for (int i = 0; i < chapter.nodes.Count; i++)
            {
                if (chapter.nodes[i] != null && !string.IsNullOrEmpty(chapter.nodes[i].id))
                    ids.Add(chapter.nodes[i].id);
            }
        }

        edge.fromId = DrawIdPopup("起点", edge.fromId, ids);
        edge.toId = DrawIdPopup("终点", edge.toId, ids);

        if (GUILayout.Button("删除此连线", GUILayout.Width(120)))
        {
            chapter.edges.RemoveAt(edgeIndex);
            edgeIndex = -1;
        }
    }

    private static string DrawIdPopup(string label, string current, List<string> ids)
    {
        if (ids.Count == 0)
            return EditorGUILayout.TextField(label, current);

        int index = Mathf.Max(0, ids.IndexOf(current));
        index = EditorGUILayout.Popup(label, index, ids.ToArray());
        return ids[index];
    }

    private RouteMapChapter GetCurrentChapter()
    {
        if (container == null || container.chapters == null) return null;
        if (chapterIndex < 0 || chapterIndex >= container.chapters.Count) return null;
        return container.chapters[chapterIndex];
    }

    private void AddNode(RouteMapChapter chapter, RouteMapNodeKind kind)
    {
        if (chapter.nodes == null)
            chapter.nodes = new List<RouteMapNode>();

        float x = 200f + chapter.nodes.Count * 280f;
        var node = new RouteMapNode(kind == RouteMapNodeKind.Junction ? "fork_" + (chapter.nodes.Count + 1) : "node_" + (chapter.nodes.Count + 1))
        {
            title = kind == RouteMapNodeKind.Junction ? "" : "新节点",
            kind = kind,
            position = new Vector2(x, 450f),
            startUnlocked = chapter.nodes.Count == 0 && kind == RouteMapNodeKind.Event
        };
        chapter.nodes.Add(node);
        nodeIndex = chapter.nodes.Count - 1;
        edgeIndex = -1;
    }
}

public static class RouteMapPrefabBuilder
{
    public const string DefaultDataFolder = "Assets/Resources/VNovelizerRes/GalleryContent/RouteMap";
    public const string DefaultPrefabFolder = "Assets/Resources/VNovelizerRes/VNPrefabs/UI/RouteMap";

    public static void EnsureInProject()
    {
        if (!Directory.Exists(DefaultDataFolder))
            Directory.CreateDirectory(DefaultDataFolder);
        if (!Directory.Exists(DefaultPrefabFolder))
            Directory.CreateDirectory(DefaultPrefabFolder);

        string dataPath = DefaultDataFolder + "/RouteMapDataContainer.asset";
        var container = AssetDatabase.LoadAssetAtPath<RouteMapDataContainer>(dataPath);
        if (container == null)
        {
            container = ScriptableObject.CreateInstance<RouteMapDataContainer>();
            container.EnsureSampleIfEmpty();
            AssetDatabase.CreateAsset(container, dataPath);
        }
        else
        {
            container.EnsureSampleIfEmpty();
            EditorUtility.SetDirty(container);
        }

        string prefabPath = DefaultPrefabFolder + "/RouteMapPanel.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            GameObject root = new GameObject("RouteMapPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(RouteMapPanel));
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            root.GetComponent<UnityEngine.UI.Image>().color = new Color(0.07f, 0.06f, 0.05f, 0.97f);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
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
