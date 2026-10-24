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
            t.localTip = local; t.measured = true;
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
        return true;
    }
}
