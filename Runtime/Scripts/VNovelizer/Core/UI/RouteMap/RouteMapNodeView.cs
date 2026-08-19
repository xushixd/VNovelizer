using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 路线图节点。样式来自 Prefab，运行时只填数据和位置。
/// </summary>
public class RouteMapNodeView : MonoBehaviour
{
    public RouteMapNode Node { get; private set; }
    public bool IsUnlocked { get; private set; }

    private Image frame;
    private TextMeshProUGUI title;
    private Button button;
    private System.Action<RouteMapNode, bool> onClick;

    public void Init(RouteMapNode node, bool unlocked, TMP_FontAsset font, System.Action<RouteMapNode, bool> click)
    {
        Node = node;
        IsUnlocked = unlocked;
        onClick = click;

        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        bool isJunction = node.kind == RouteMapNodeKind.Junction;
        rt.sizeDelta = isJunction ? new Vector2(22f, 22f) : new Vector2(168f, 72f);
        rt.anchoredPosition = new Vector2(node.position.x, -node.position.y);

        frame = GetComponent<Image>();
        title = GetComponentInChildren<TextMeshProUGUI>(true);
        if (title != null && font != null)
            title.font = font;

        button = GetComponent<Button>();
        if (button == null) button = GetComponentInChildren<Button>(true);
        if (button != null)
        {
            if (frame != null) button.targetGraphic = frame;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }

        ApplyState();
    }

    public void SetUnlocked(bool unlocked)
    {
        IsUnlocked = unlocked;
        ApplyState();
    }

    private void ApplyState()
    {
        if (Node == null) return;
        bool unlocked = IsUnlocked || Node.startUnlocked;
        bool isJunction = Node.kind == RouteMapNodeKind.Junction;

        if (frame != null)
        {
            if (isJunction)
                frame.color = unlocked
                    ? new Color(0.93f, 0.82f, 0.55f, 1f)
                    : new Color(0.35f, 0.32f, 0.28f, 0.85f);
            else
                frame.color = unlocked
                    ? new Color(0.22f, 0.28f, 0.36f, 1f)
                    : new Color(0.16f, 0.18f, 0.22f, 1f);
        }

        if (title != null)
        {
            title.gameObject.SetActive(!isJunction);
            title.color = unlocked ? new Color(0.93f, 0.86f, 0.72f) : new Color(0.45f, 0.42f, 0.38f);
            title.text = unlocked ? Node.title : "???";
        }
    }

    private void HandleClick()
    {
        if (onClick != null)
            onClick(Node, IsUnlocked);
    }
}
