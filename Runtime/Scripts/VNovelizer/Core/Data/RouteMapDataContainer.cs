using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RouteMapDataContainer", menuName = "VNovelizer/Route Map Data Container")]
public class RouteMapDataContainer : ScriptableObject
{
    public List<RouteMapChapter> chapters = new List<RouteMapChapter>();

    public RouteMapChapter GetChapter(string chapterId)
    {
        if (chapters == null || string.IsNullOrEmpty(chapterId)) return null;
        for (int i = 0; i < chapters.Count; i++)
        {
            if (chapters[i] != null && chapters[i].id == chapterId)
                return chapters[i];
        }
        return null;
    }

    public RouteMapNode FindNode(string nodeId)
    {
        if (chapters == null || string.IsNullOrEmpty(nodeId)) return null;
        for (int i = 0; i < chapters.Count; i++)
        {
            RouteMapNode node = chapters[i] != null ? chapters[i].FindNode(nodeId) : null;
            if (node != null) return node;
        }
        return null;
    }

    /// <summary>
    /// 容器为空时填入一章示意数据，方便第一次打开路线图。
    /// </summary>
    public void EnsureSampleIfEmpty()
    {
        if (chapters == null)
            chapters = new List<RouteMapChapter>();
        if (chapters.Count > 0) return;

        var chapter = new RouteMapChapter
        {
            id = "chapter_01",
            title = "第一章",
            notes = "本章节请留意：\n关键选择\n人物立场"
        };

        chapter.nodes.Add(new RouteMapNode("intro")
        {
            title = "序章",
            kind = RouteMapNodeKind.Event,
            position = new Vector2(180, 420),
            startUnlocked = true,
            pathBend = 40f
        });
        chapter.nodes.Add(new RouteMapNode("meeting")
        {
            title = "初次会面",
            kind = RouteMapNodeKind.Event,
            position = new Vector2(520, 520),
            pathBend = -70f
        });
        chapter.nodes.Add(new RouteMapNode("fork")
        {
            title = "",
            kind = RouteMapNodeKind.Junction,
            position = new Vector2(860, 470),
            pathBend = 0f
        });
        chapter.nodes.Add(new RouteMapNode("trust")
        {
            title = "选择相信",
            kind = RouteMapNodeKind.Event,
            position = new Vector2(1200, 640),
            pathBend = 90f
        });
        chapter.nodes.Add(new RouteMapNode("refuse")
        {
            title = "选择拒绝",
            kind = RouteMapNodeKind.Event,
            position = new Vector2(1200, 280),
            pathBend = -90f
        });
        chapter.nodes.Add(new RouteMapNode("merge")
        {
            title = "",
            kind = RouteMapNodeKind.Junction,
            position = new Vector2(1560, 460),
            pathBend = 0f
        });
        chapter.nodes.Add(new RouteMapNode("finale")
        {
            title = "终章",
            kind = RouteMapNodeKind.Event,
            position = new Vector2(1920, 460),
            pathBend = 50f
        });

        chapter.edges.Add(new RouteMapEdge { fromId = "intro", toId = "meeting" });
        chapter.edges.Add(new RouteMapEdge { fromId = "meeting", toId = "fork" });
        chapter.edges.Add(new RouteMapEdge { fromId = "fork", toId = "trust" });
        chapter.edges.Add(new RouteMapEdge { fromId = "fork", toId = "refuse" });
        chapter.edges.Add(new RouteMapEdge { fromId = "trust", toId = "merge" });
        chapter.edges.Add(new RouteMapEdge { fromId = "refuse", toId = "merge" });
        chapter.edges.Add(new RouteMapEdge { fromId = "merge", toId = "finale" });

        chapters.Add(chapter);
    }
}
