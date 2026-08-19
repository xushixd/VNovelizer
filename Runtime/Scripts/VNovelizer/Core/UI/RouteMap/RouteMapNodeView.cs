using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 路线图节点视图。Event 显示缩略图和标题，Junction 显示小圆点。
/// </summary>
public class RouteMapNodeView : MonoBehaviour
{
    public RouteMapNode Node { get; private set; }
    public bool IsUnlocked { get; private set; }

    private Image frame;
    private Image thumbnail;
    private TextMeshProUGUI title;
    private Button button;
    private System.Action<RouteMapNode, bool> onClick;

    public void Init(RouteMapNode node, bool unlocked, TMP_FontAsset font, System.Action<RouteMapNode, bool> click)
    {
        Node = node;
        IsUnlocked = unlocked;
        onClick = click;

        RectTransform rt = GetComponent<RectTransform>();
        bool isJunction = node.kind == RouteMapNodeKind.Junction;
        rt.sizeDelta = isJunction ? new Vector2(22f, 22f) : new Vector2(168f, 128f);
        rt.anchoredPosition = node.position;

        frame = CreateImage("Frame", rt, Vector2.zero, rt.sizeDelta, Color.white);
        frame.raycastTarget = true;

        if (isJunction)
        {
            frame.color = unlocked
                ? new Color(0.93f, 0.82f, 0.55f, 1f)
                : new Color(0.35f, 0.32f, 0.28f, 0.85f);
        }
        else
        {
            frame.color = new Color(0.18f, 0.16f, 0.13f, 0.95f);

            thumbnail = CreateImage("Thumb", rt, new Vector2(0f, 12f), new Vector2(148f, 84f), Color.white);
            thumbnail.raycastTarget = false;
            ApplyThumbnail();

            GameObject titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(rt, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 0f);
            titleRt.pivot = new Vector2(0.5f, 0f);
            titleRt.anchoredPosition = new Vector2(0f, 4f);
            titleRt.sizeDelta = new Vector2(-8f, 28f);
            title = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.fontSize = 18;
            title.alignment = TextAlignmentOptions.Center;
            title.color = unlocked ? new Color(0.93f, 0.86f, 0.72f) : new Color(0.45f, 0.42f, 0.38f);
            title.raycastTarget = false;
            title.text = unlocked || node.startUnlocked ? node.title : "???";
        }

        button = gameObject.GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }

    public void SetUnlocked(bool unlocked)
    {
        IsUnlocked = unlocked;
        if (Node == null) return;

        if (Node.kind == RouteMapNodeKind.Junction)
        {
            if (frame != null)
                frame.color = unlocked
                    ? new Color(0.93f, 0.82f, 0.55f, 1f)
                    : new Color(0.35f, 0.32f, 0.28f, 0.85f);
            return;
        }

        ApplyThumbnail();
        if (title != null)
        {
            title.color = unlocked ? new Color(0.93f, 0.86f, 0.72f) : new Color(0.45f, 0.42f, 0.38f);
            title.text = unlocked || Node.startUnlocked ? Node.title : "???";
        }
    }

    private void ApplyThumbnail()
    {
        if (thumbnail == null || Node == null) return;

        if (IsUnlocked && Node.unlockedSprite != null)
        {
            thumbnail.sprite = Node.unlockedSprite;
            thumbnail.color = Color.white;
        }
        else if (Node.lockedSprite != null)
        {
            thumbnail.sprite = Node.lockedSprite;
            thumbnail.color = Color.white;
        }
        else
        {
            thumbnail.sprite = null;
            thumbnail.color = IsUnlocked
                ? new Color(0.42f, 0.36f, 0.28f, 1f)
                : new Color(0.16f, 0.14f, 0.12f, 1f);
        }
    }

    private void HandleClick()
    {
        if (onClick != null)
            onClick(Node, IsUnlocked);
    }

    private static Image CreateImage(string name, RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }
}
