using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在 UGUI 里画一条二次贝塞尔路线。
/// </summary>
public class RouteMapPathGraphic : MaskableGraphic
{
    [SerializeField] private Vector2 start;
    [SerializeField] private Vector2 control;
    [SerializeField] private Vector2 end;
    [SerializeField] private float thickness = 4f;

    private const int Segments = 24;

    public void SetPath(Vector2 pathStart, Vector2 pathControl, Vector2 pathEnd, float pathThickness, Color pathColor)
    {
        start = pathStart;
        control = pathControl;
        end = pathEnd;
        thickness = pathThickness;
        color = pathColor;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (thickness <= 0f) return;

        Vector2 prev = Evaluate(0f);
        for (int i = 1; i <= Segments; i++)
        {
            Vector2 next = Evaluate(i / (float)Segments);
            AddQuad(vh, prev, next, thickness);
            prev = next;
        }
    }

    private Vector2 Evaluate(float t)
    {
        float u = 1f - t;
        return u * u * start + 2f * u * t * control + t * t * end;
    }

    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, float width)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
        Color32 c = color;
        int index = vh.currentVertCount;

        vh.AddVert(a - n, c, Vector2.zero);
        vh.AddVert(a + n, c, Vector2.zero);
        vh.AddVert(b + n, c, Vector2.zero);
        vh.AddVert(b - n, c, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
