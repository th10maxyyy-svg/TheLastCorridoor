using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which end of an item is its "business end" (the blade tip of the scalpel, the bit of a key ...), as a local axis of
/// the item (+X/-X/+Y/-Y/+Z/-Z). Filled in automatically in the Editor (The Last Corridor / Find Item Tips) by looking at
/// the model: the end that narrows to a point is the tip. Used so the scalpel is never held the wrong way round.
/// </summary>
[DisallowMultipleComponent]
public class ItemTip : MonoBehaviour
{
    [Tooltip("Local direction (item space) from the middle of the model towards its tip")]
    public Vector3 localTip = Vector3.forward;
    [Tooltip("Ticked by the editor tool when the tip was found from the model itself")]
    public bool measured;
    [Tooltip("Local 'up' of the item (for a gun: from the grip towards the top of the barrel)")]
    public Vector3 localUp = Vector3.zero;

    public static bool TryGetUp(GameObject item, out Vector3 up)
    {
        up = Vector3.zero;
        ItemTip t = item != null ? item.GetComponent<ItemTip>() : null;
        if (t == null || !t.measured || t.localUp.sqrMagnitude < 0.5f) return false;
        up = t.localUp.normalized; return true;
    }

    /// <summary>Local tip axis of this item (from the component, or worked out now if the mesh can be read).</summary>
    public static bool TryGetLocal(GameObject item, out Vector3 local)
    {
        local = Vector3.zero;
        if (item == null) return false;
        ItemTip t = item.GetComponent<ItemTip>();
        if (t != null && t.measured && t.localTip.sqrMagnitude > 0.5f) { local = t.localTip.normalized; return true; }
        if (Compute(item, out local, false))
        {
            if (t == null) t = item.AddComponent<ItemTip>();
            t.localTip = local; t.measured = true; t.localUp = lastUp;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Looks at the model's vertices: along its longest side, the end whose cross-section is smallest is the tip.
    /// editorMode = true may read meshes that are not marked Read/Write (only possible in the Editor, outside Play).
    /// </summary>
    public static bool Compute(GameObject root, out Vector3 local, bool editorMode)
    {
        local = Vector3.zero;
        List<Vector3> pts = new List<Vector3>();
        Transform rt = root.transform;
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null) continue;
            if (!m.isReadable && !editorMode) return false;
            Vector3[] v;
            try { v = m.vertices; } catch { return false; }
            if (v == null || v.Length == 0) continue;
            int step = Mathf.Max(1, v.Length / 6000);
            for (int i = 0; i < v.Length; i += step) pts.Add(rt.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
        }
        if (pts.Count < 8) return false;

        Bounds b = new Bounds(pts[0], Vector3.zero);
        foreach (Vector3 p in pts) b.Encapsulate(p);
        Vector3 ls = rt.lossyScale;
        Vector3 ws = new Vector3(Mathf.Abs(b.size.x * ls.x), Mathf.Abs(b.size.y * ls.y), Mathf.Abs(b.size.z * ls.z));
        int ax = ws.x >= ws.y && ws.x >= ws.z ? 0 : ws.y >= ws.z ? 1 : 2;
        float min = b.min[ax], max = b.max[ax], len = max - min;
        if (len <= 1e-6f) return false;

        // widest point of the outer 8% at each end (perpendicular distance from the long centre line)
        float rLow = 0f, rHigh = 0f;
        foreach (Vector3 p in pts)
        {
            Vector3 d = p - b.center; d[ax] = 0f;
            d = Vector3.Scale(d, ls);
            float r = d.magnitude;
            float t = (p[ax] - min) / len;
            if (t < 0.08f) rLow = Mathf.Max(rLow, r);
            else if (t > 0.92f) rHigh = Mathf.Max(rHigh, r);
        }
        Vector3 axis = Vector3.zero; axis[ax] = 1f;
        local = rHigh <= rLow ? axis : -axis;
        lastUp = UpOf(pts, b, ls, ax, rHigh <= rLow);
        return true;
    }

    /// <summary>The 'up' found by the last Compute (Vector3.zero if unknown).</summary>
    public static Vector3 lastUp;

    // a gun / tool: the taller of the two short sides is vertical; the grip hangs down at the back (away from the tip)
    private static Vector3 UpOf(List<Vector3> pts, Bounds b, Vector3 ls, int ax, bool tipAtHigh)
    {
        int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
        int vert = Mathf.Abs(b.size[a1] * ls[a1]) >= Mathf.Abs(b.size[a2] * ls[a2]) ? a1 : a2;
        if (Mathf.Abs(b.size[vert] * ls[vert]) < Mathf.Abs(b.size[ax] * ls[ax]) * 0.2f) return Vector3.zero;   // flat thing: no up
        float min = b.min[ax], len = b.size[ax];
        double sum = 0; int n = 0;
        foreach (Vector3 p in pts)
        {
            float t = (p[ax] - min) / len;
            bool rear = tipAtHigh ? t < 0.45f : t > 0.55f;
            if (!rear) continue;
            sum += p[vert] - b.center[vert]; n++;
        }
        if (n == 0) return Vector3.zero;
        Vector3 up = Vector3.zero; up[vert] = sum / n < 0 ? 1f : -1f;   // the grip is below the centre
        return up;
    }
}
