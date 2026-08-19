using System;
using System.Collections.Generic;
using UnityEngine;

public enum RouteMapNodeKind
{
    Event = 0,
    Junction = 1
}

/// <summary>
/// 路线图上的一个节点。Event 带缩略图和标题；Junction 是分岔/汇合小点。
/// </summary>
[Serializable]
public class RouteMapNode
{
    [Tooltip("稳定 ID，用于解锁和连线")]
    public string id = "";

    [Tooltip("节点标题，未解锁时可显示为 ???")]
    public string title = "";

    public RouteMapNodeKind kind = RouteMapNodeKind.Event;

    [Tooltip("在地图内容区中的位置（像素，原点在左下）")]
    public Vector2 position = Vector2.zero;

    [Tooltip("路径起伏方向，正数向上、负数向下")]
    public float pathBend = 80f;

    [Tooltip("开局即可见，通常用于第一章第一个节点")]
    public bool startUnlocked;

    [Tooltip("解锁后显示的缩略图")]
    public Sprite unlockedSprite;

    [Tooltip("未解锁占位图；为空则显示暗色框")]
    public Sprite lockedSprite;

    [Tooltip("可选：点击已解锁节点后回放的剧本名")]
    public string scriptName = "";

    public string startLineID = "";
    public string endLineID = "";

    public RouteMapNode() { }

    public RouteMapNode(string nodeId)
    {
        id = nodeId;
        title = nodeId;
    }
}

/// <summary>
/// 两个节点之间的路线。
/// </summary>
[Serializable]
public class RouteMapEdge
{
    public string fromId = "";
    public string toId = "";
}

/// <summary>
/// 一章路线图。
/// </summary>
[Serializable]
public class RouteMapChapter
{
    public string id = "";
    public string title = "第一章";

    [Tooltip("右上角提示，例如本章需要留意的人物或条件")]
    [TextArea(2, 6)]
    public string notes = "";

    public List<RouteMapNode> nodes = new List<RouteMapNode>();
    public List<RouteMapEdge> edges = new List<RouteMapEdge>();

    public RouteMapNode FindNode(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].id == nodeId)
                return nodes[i];
        }
        return null;
    }
}
