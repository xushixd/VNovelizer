using System.Collections.Generic;
using LitJson;
using UnityEngine;

public enum RouteMapUnvisitedMode
{
    Hide = 0,
    Lock = 1
}

/// <summary>
/// 一章路线图的布局文件。节点默认来自 Segment，位置和手工改动存在 graph.json。
/// </summary>
public static class RouteMapGraph
{
    public const string DefaultFolder = "Assets/Resources/VNovelizerRes/GalleryContent/RouteMap";
    public const string ResourcesFolder = "VNovelizerRes/GalleryContent/RouteMap";

    public static string GraphAssetPath(string chapterId)
    {
        return DefaultFolder + "/" + FileName(chapterId);
    }

    public static string GraphResourcePath(string chapterId)
    {
        return ResourcesFolder + "/" + System.IO.Path.GetFileNameWithoutExtension(FileName(chapterId));
    }

    public static string FileName(string chapterId)
    {
        string id = string.IsNullOrEmpty(chapterId) ? "chapter" : chapterId.Trim();
        return id + ".graph.json";
    }

    public static RouteMapChapter CreateFromChapter(ChapterData chapter, string scriptName)
    {
        var graph = new RouteMapChapter
        {
            id = chapter != null ? chapter.id : "",
            title = chapter != null ? chapter.title : "",
            notes = "",
            nodes = new List<RouteMapNode>(),
            edges = new List<RouteMapEdge>()
        };
        if (chapter == null || chapter.segments == null) return graph;

        graph.scriptName = scriptName ?? "";

        var depths = new Dictionary<string, int>();
        var lanes = new Dictionary<string, int>();
        ComputeLayout(chapter, depths, lanes);

        for (int i = 0; i < chapter.segments.Count; i++)
        {
            SegmentData segment = chapter.segments[i];
            if (segment == null || string.IsNullOrEmpty(segment.id)) continue;

            int depth;
            int lane;
            if (!depths.TryGetValue(segment.id, out depth)) depth = i;
            if (!lanes.TryGetValue(segment.id, out lane)) lane = 0;

            graph.nodes.Add(new RouteMapNode(segment.id)
            {
                title = string.IsNullOrEmpty(segment.title) ? segment.id : segment.title,
                kind = RouteMapNodeKind.Event,
                position = new Vector2(180f + depth * 320f, 420f + lane * 220f),
                startUnlocked = segment.id == chapter.entrySegmentId,
                scriptName = scriptName ?? "",
                startLineID = FirstContentId(segment)
            });
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < chapter.segments.Count; i++)
        {
            SegmentData segment = chapter.segments[i];
            if (segment == null || string.IsNullOrEmpty(segment.id)) continue;

            if (!string.IsNullOrEmpty(segment.nextSegmentId))
                AddEdge(graph, seen, segment.id, segment.nextSegmentId);

            if (segment.content == null) continue;
            for (int n = 0; n < segment.content.Count; n++)
            {
                DialogueContent content = segment.content[n];
                if (content == null || content.options == null) continue;
                for (int o = 0; o < content.options.Count; o++)
                {
                    DialogueOptionData option = content.options[o];
                    if (option == null || string.IsNullOrEmpty(option.result)) continue;
                    AddEdge(graph, seen, segment.id, option.result);
                }
            }
        }

        return graph;
    }

    public static RouteMapChapter FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        JsonData root;
        try { root = JsonMapper.ToObject(json); }
        catch { return null; }
        if (root == null || !root.IsObject) return null;

        var graph = new RouteMapChapter
        {
            id = ReadString(root, "id"),
            title = ReadString(root, "title"),
            notes = ReadString(root, "notes"),
            scriptName = ReadString(root, "scriptName"),
            nodes = new List<RouteMapNode>(),
            edges = new List<RouteMapEdge>()
        };

        if (root.ContainsKey("nodes") && root["nodes"] != null && root["nodes"].IsArray)
        {
            JsonData array = root["nodes"];
            for (int i = 0; i < array.Count; i++)
            {
                JsonData item = array[i];
                if (item == null || !item.IsObject) continue;
                graph.nodes.Add(new RouteMapNode(ReadString(item, "id"))
                {
                    title = ReadString(item, "title"),
                    kind = ReadKind(ReadString(item, "kind")),
                    position = new Vector2(ReadFloat(item, "x"), ReadFloat(item, "y")),
                    pathBend = item.ContainsKey("pathBend") ? ReadFloat(item, "pathBend") : 80f,
                    startUnlocked = ReadBool(item, "startUnlocked"),
                    scriptName = ReadString(item, "scriptName"),
                    startLineID = ReadString(item, "startLineID"),
                    endLineID = ReadString(item, "endLineID")
                });
            }
        }

        if (root.ContainsKey("edges") && root["edges"] != null && root["edges"].IsArray)
        {
            JsonData array = root["edges"];
            for (int i = 0; i < array.Count; i++)
            {
                JsonData item = array[i];
                if (item == null || !item.IsObject) continue;
                graph.edges.Add(new RouteMapEdge
                {
                    fromId = FirstString(item, "from", "fromId"),
                    toId = FirstString(item, "to", "toId")
                });
            }
        }

        return graph;
    }

    public static string ToJson(RouteMapChapter graph)
    {
        JsonData root = NewObject();
        if (graph == null) graph = new RouteMapChapter();
        root["id"] = graph.id ?? "";
        root["title"] = graph.title ?? "";
        root["scriptName"] = graph.scriptName ?? "";
        root["notes"] = graph.notes ?? "";

        JsonData nodes = NewArray();
        if (graph.nodes != null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                RouteMapNode node = graph.nodes[i];
                if (node == null) continue;
                JsonData item = NewObject();
                item["id"] = node.id ?? "";
                item["title"] = node.title ?? "";
                item["kind"] = node.kind == RouteMapNodeKind.Junction ? "Junction" : "Event";
                item["x"] = node.position.x;
                item["y"] = node.position.y;
                item["pathBend"] = node.pathBend;
                item["startUnlocked"] = node.startUnlocked;
                item["scriptName"] = node.scriptName ?? "";
                item["startLineID"] = node.startLineID ?? "";
                item["endLineID"] = node.endLineID ?? "";
                nodes.Add(item);
            }
        }
        root["nodes"] = nodes;

        JsonData edges = NewArray();
        if (graph.edges != null)
        {
            for (int i = 0; i < graph.edges.Count; i++)
            {
                RouteMapEdge edge = graph.edges[i];
                if (edge == null) continue;
                JsonData item = NewObject();
                item["from"] = edge.fromId ?? "";
                item["to"] = edge.toId ?? "";
                edges.Add(item);
            }
        }
        root["edges"] = edges;

        var writer = ScriptParser.CreatePrettyWriter();
        JsonMapper.ToJson(root, writer);
        return writer.ToString();
    }

    public static void GetCubicControls(Vector2 start, Vector2 end, out Vector2 c1, out Vector2 c2)
    {
        float pull = Mathf.Max(80f, Mathf.Abs(end.x - start.x) * 0.45f);
        c1 = start + new Vector2(pull, 0f);
        c2 = end - new Vector2(pull, 0f);
    }

    public static Vector2 EvaluateCubic(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
    }

    public static void SampleCubic(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, Vector3[] dest)
    {
        if (dest == null || dest.Length < 2) return;
        int last = dest.Length - 1;
        for (int i = 0; i <= last; i++)
            dest[i] = EvaluateCubic(a, c1, c2, b, i / (float)last);
    }

    public static RouteMapChapter LoadFromResources(string chapterId)
    {
        TextAsset asset = Resources.Load<TextAsset>(GraphResourcePath(chapterId));
        return asset != null ? FromJson(asset.text) : null;
    }

    public static List<RouteMapChapter> LoadAllFromResources()
    {
        var list = new List<RouteMapChapter>();
        TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourcesFolder);
        if (assets == null) return list;
        for (int i = 0; i < assets.Length; i++)
        {
            TextAsset asset = assets[i];
            if (asset == null || string.IsNullOrEmpty(asset.name)) continue;
            if (!asset.name.Contains("graph")) continue;
            RouteMapChapter graph = FromJson(asset.text);
            if (graph != null) list.Add(graph);
        }
        return list;
    }

    private static void AddEdge(RouteMapChapter graph, HashSet<string> seen, string from, string to)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to) return;
        string key = from + "->" + to;
        if (!seen.Add(key)) return;
        graph.edges.Add(new RouteMapEdge { fromId = from, toId = to });
    }

    private static string FirstContentId(SegmentData segment)
    {
        if (segment == null || segment.content == null) return "";
        for (int i = 0; i < segment.content.Count; i++)
        {
            if (segment.content[i] != null && !string.IsNullOrEmpty(segment.content[i].id))
                return segment.content[i].id;
        }
        return "";
    }

    private static void ComputeLayout(ChapterData chapter, Dictionary<string, int> depths, Dictionary<string, int> lanes)
    {
        var outgoing = new Dictionary<string, List<string>>();
        for (int i = 0; i < chapter.segments.Count; i++)
        {
            SegmentData segment = chapter.segments[i];
            if (segment == null || string.IsNullOrEmpty(segment.id)) continue;
            if (!outgoing.ContainsKey(segment.id))
                outgoing[segment.id] = new List<string>();

            if (!string.IsNullOrEmpty(segment.nextSegmentId))
                outgoing[segment.id].Add(segment.nextSegmentId);
            if (segment.content == null) continue;
            for (int n = 0; n < segment.content.Count; n++)
            {
                DialogueContent content = segment.content[n];
                if (content == null || content.options == null) continue;
                for (int o = 0; o < content.options.Count; o++)
                {
                    if (content.options[o] != null && !string.IsNullOrEmpty(content.options[o].result))
                        outgoing[segment.id].Add(content.options[o].result);
                }
            }
        }

        string start = chapter.entrySegmentId;
        if (string.IsNullOrEmpty(start) && chapter.segments.Count > 0)
            start = chapter.segments[0].id;

        var queue = new Queue<string>();
        if (!string.IsNullOrEmpty(start))
        {
            depths[start] = 0;
            lanes[start] = 0;
            queue.Enqueue(start);
        }

        var laneCursor = new Dictionary<int, int>();
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            int depth = depths[current];
            List<string> next;
            if (!outgoing.TryGetValue(current, out next) || next == null) continue;

            int branch = 0;
            for (int i = 0; i < next.Count; i++)
            {
                string id = next[i];
                if (string.IsNullOrEmpty(id) || depths.ContainsKey(id)) continue;
                depths[id] = depth + 1;
                int used;
                if (!laneCursor.TryGetValue(depth + 1, out used)) used = 0;
                int lane = used + (next.Count > 1 ? branch : 0);
                lanes[id] = lane;
                laneCursor[depth + 1] = Mathf.Max(used, lane + 1);
                queue.Enqueue(id);
                branch++;
            }
        }
    }

    private static RouteMapNodeKind ReadKind(string value)
    {
        if (!string.IsNullOrEmpty(value) && value.Equals("Junction", System.StringComparison.OrdinalIgnoreCase))
            return RouteMapNodeKind.Junction;
        return RouteMapNodeKind.Event;
    }

    private static JsonData NewObject()
    {
        JsonData data = new JsonData();
        data.SetJsonType(LitJson.JsonType.Object);
        return data;
    }

    private static JsonData NewArray()
    {
        JsonData data = new JsonData();
        data.SetJsonType(LitJson.JsonType.Array);
        return data;
    }

    private static string FirstString(JsonData data, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            string value = ReadString(data, keys[i]);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "";
    }

    private static string ReadString(JsonData data, string key)
    {
        if (data == null || !data.IsObject || !data.ContainsKey(key) || data[key] == null)
            return "";
        JsonData value = data[key];
        if (value.IsString) return (string)value;
        if (value.IsBoolean) return ((bool)value) ? "true" : "false";
        return value.ToString();
    }

    private static float ReadFloat(JsonData data, string key)
    {
        if (data == null || !data.IsObject || !data.ContainsKey(key) || data[key] == null)
            return 0f;
        JsonData value = data[key];
        if (value.IsDouble) return (float)(double)value;
        if (value.IsInt) return (int)value;
        if (value.IsLong) return (long)value;
        float parsed;
        return float.TryParse(ReadString(data, key), out parsed) ? parsed : 0f;
    }

    private static bool ReadBool(JsonData data, string key)
    {
        if (data == null || !data.IsObject || !data.ContainsKey(key) || data[key] == null)
            return false;
        JsonData value = data[key];
        if (value.IsBoolean) return (bool)value;
        string text = ReadString(data, key);
        return text.Equals("true", System.StringComparison.OrdinalIgnoreCase) || text == "1";
    }
}
