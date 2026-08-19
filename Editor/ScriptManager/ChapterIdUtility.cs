using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按当前顺序重写 Chapter / Segment / Content / Option ID，并改写跳转与路线图引用。
/// Segment = {chapter}-0001，Content = {segment}-00000，Option = {content}-01。
/// </summary>
public static class ChapterIdUtility
{
    public static string SegmentId(string chapterId, int index)
    {
        return NormalizeChapterId(chapterId) + "-" + (index + 1).ToString("D4");
    }

    public static string ContentId(string segmentId, int index)
    {
        return segmentId + "-" + index.ToString("D5");
    }

    public static string OptionId(string contentId, int index)
    {
        return contentId + "-" + (index + 1).ToString("D2");
    }

    public static string NormalizeChapterId(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId)) return "001";
        return chapterId.Trim();
    }

    public static Dictionary<string, string> Remap(ChapterData chapter)
    {
        var map = new Dictionary<string, string>();
        if (chapter == null) return map;

        string chapterId = NormalizeChapterId(chapter.id);
        if (!string.IsNullOrEmpty(chapter.id) && chapter.id != chapterId)
            map[chapter.id] = chapterId;
        chapter.id = chapterId;
        if (chapter.segments == null) chapter.segments = new List<SegmentData>();

        var newSegmentIds = new string[chapter.segments.Count];
        for (int i = 0; i < chapter.segments.Count; i++)
        {
            SegmentData segment = chapter.segments[i];
            if (segment == null) continue;
            newSegmentIds[i] = SegmentId(chapterId, i);
            Remember(map, segment.id, newSegmentIds[i]);
        }

        for (int i = 0; i < chapter.segments.Count; i++)
        {
            SegmentData segment = chapter.segments[i];
            if (segment == null) continue;
            segment.id = newSegmentIds[i];
            if (segment.content == null) segment.content = new List<DialogueContent>();

            for (int n = 0; n < segment.content.Count; n++)
            {
                DialogueContent content = segment.content[n];
                if (content == null) continue;
                string newContentId = ContentId(segment.id, n);
                Remember(map, content.id, newContentId);
                content.id = newContentId;
                if (content.options == null) content.options = new List<DialogueOptionData>();

                for (int o = 0; o < content.options.Count; o++)
                {
                    DialogueOptionData option = content.options[o];
                    if (option == null) continue;
                    string newOptionId = OptionId(content.id, o);
                    Remember(map, option.id, newOptionId);
                    option.id = newOptionId;
                }
            }
        }

        chapter.entrySegmentId = Rewrite(map, chapter.entrySegmentId);
        if (string.IsNullOrEmpty(chapter.entrySegmentId) && chapter.segments.Count > 0 && chapter.segments[0] != null)
            chapter.entrySegmentId = chapter.segments[0].id;

        for (int i = 0; i < chapter.segments.Count; i++)
        {
            SegmentData segment = chapter.segments[i];
            if (segment == null) continue;
            segment.nextSegmentId = Rewrite(map, segment.nextSegmentId);
            if (segment.content == null) continue;
            for (int n = 0; n < segment.content.Count; n++)
            {
                DialogueContent content = segment.content[n];
                if (content == null || content.options == null) continue;
                for (int o = 0; o < content.options.Count; o++)
                {
                    DialogueOptionData option = content.options[o];
                    if (option == null) continue;
                    option.result = Rewrite(map, option.result);
                }
            }
        }

        return map;
    }

    public static void RemapRouteMap(string oldChapterId, ChapterData chapter, Dictionary<string, string> map)
    {
        if (chapter == null || map == null || map.Count == 0) return;

        string newPath = RouteMapGraph.GraphAssetPath(chapter.id);
        string oldPath = RouteMapGraph.GraphAssetPath(string.IsNullOrEmpty(oldChapterId) ? chapter.id : oldChapterId);
        RouteMapChapter graph = LoadGraph(oldPath);
        if (graph == null) graph = LoadGraph(newPath);
        if (graph == null) return;

        graph.id = chapter.id ?? "";
        graph.title = chapter.title ?? graph.title;
        RewriteNodeIds(graph, map);

        if (!Directory.Exists(RouteMapGraph.DefaultFolder))
            Directory.CreateDirectory(RouteMapGraph.DefaultFolder);

        File.WriteAllText(newPath, RouteMapGraph.ToJson(graph), new UTF8Encoding(false));
        if (!string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), System.StringComparison.OrdinalIgnoreCase)
            && File.Exists(oldPath))
        {
            File.Delete(oldPath);
            if (File.Exists(oldPath + ".meta")) File.Delete(oldPath + ".meta");
        }
    }

    private static RouteMapChapter LoadGraph(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        return RouteMapGraph.FromJson(File.ReadAllText(path));
    }

    private static void RewriteNodeIds(RouteMapChapter graph, Dictionary<string, string> map)
    {
        if (graph.nodes != null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                RouteMapNode node = graph.nodes[i];
                if (node == null) continue;
                node.id = Rewrite(map, node.id);
                node.startLineID = Rewrite(map, node.startLineID);
                node.endLineID = Rewrite(map, node.endLineID);
            }
        }

        if (graph.edges == null) return;
        for (int i = 0; i < graph.edges.Count; i++)
        {
            RouteMapEdge edge = graph.edges[i];
            if (edge == null) continue;
            edge.fromId = Rewrite(map, edge.fromId);
            edge.toId = Rewrite(map, edge.toId);
        }
    }

    private static void Remember(Dictionary<string, string> map, string oldId, string newId)
    {
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId) || oldId == newId) return;
        map[oldId] = newId;
    }

    private static string Rewrite(Dictionary<string, string> map, string value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        string mapped;
        return map.TryGetValue(value, out mapped) ? mapped : value;
    }
}
