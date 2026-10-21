using UnityEngine;
using UnityEngine.UI;

/// <summary>A heartbeat (ECG) line drawn as UI geometry. "progress" 0..1 draws it from left to right.</summary>
public class IntroEcgLine : MaskableGraphic
{
    [Range(0f, 1f)] public float progress = 0f;
    public float thickness = 3f;
    public float amplitude = 1.5f;   // vertical scale of the beat

    // path in the same units as the design mock (x 60..1220, y offset from the centre line)
    private static readonly Vector2[] Path =
    {
        new Vector2(60, 0), new Vector2(520, 0), new Vector2(546, 0), new Vector2(562, -38), new Vector2(584, 48),
        new Vector2(606, -64), new Vector2(626, 12), new Vector2(642, 0), new Vector2(1220, 0)
    };

    public void SetProgress(float p)
    {
        p = Mathf.Clamp01(p);
        if (!Mathf.Approximately(p, progress)) { progress = p; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (progress <= 0f) return;

        Rect r = GetPixelAdjustedRect();
        float x0 = Path[0].x, x1 = Path[Path.Length - 1].x;
        Vector2[] pts = new Vector2[Path.Length];
        for (int i = 0; i < Path.Length; i++)
            pts[i] = new Vector2(r.x + (Path[i].x - x0) / (x1 - x0) * r.width, r.center.y + Path[i].y * amplitude);

        float total = 0f;
        for (int i = 1; i < pts.Length; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
        float draw = total * progress, done = 0f;

        for (int i = 1; i < pts.Length && done < draw; i++)
        {
            Vector2 a = pts[i - 1], b = pts[i];
            float len = Vector2.Distance(a, b);
            if (len < 0.01f) continue;
            if (done + len > draw) b = Vector2.Lerp(a, b, (draw - done) / len);
            done += len;

            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            int v = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero);
            vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }
    }
}
