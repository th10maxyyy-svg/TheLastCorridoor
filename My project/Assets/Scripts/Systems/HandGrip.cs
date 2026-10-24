using UnityEngine;

/// <summary>
/// REAL GRIP for everything the player carries (keys, fuses, crank, scalpel, ...).
///   - the item is placed IN the right palm, its long side lying across the palm, its flat side against the palm
///   - the fingers and thumb curl around it (more for thin things, less for thick things) and open again when it is dropped
///   - an item that is too big for a hand is shown a bit smaller while carried (its real size comes back on drop)
/// Works with any humanoid / Mixamo hand - the palm direction is worked out from the bones, no hand-tuned numbers.
/// Added to the player automatically by Inventory.
/// </summary>
public class HandGrip : MonoBehaviour
{
    private const float MaxHeldSize = 0.26f;   // metres: longest side of an item in the hand

    private Transform hand;
    private Transform[][] fingers;   // index, middle, ring, little, thumb - each [proximal, intermediate, distal, tip(optional)]
    private Quaternion[][] baseRot, written;
    private bool[][] hasWritten;

    private Vector3 palmNLocal, acrossLocal;
    private GameObject held;
    private Vector3 heldWorldScale;
    private bool scaledDown;
    private float thickness = 0.03f;
    private float weight;
    private bool small;
    private Vector3 heldTipLocal;
    private Bounds heldBounds;

    /// <summary>World position of the held item's tip and the direction it points (from handle to tip).</summary>
    public bool HeldTip(out Vector3 tipPoint, out Vector3 tipDir)
    {
        tipPoint = Vector3.zero; tipDir = Vector3.forward;
        if (held == null) return false;
        Transform t = held.transform;
        Vector3 axis = heldTipLocal.sqrMagnitude > 0.5f ? heldTipLocal : Vector3.zero;
        if (axis == Vector3.zero)
        {
            Vector3 sz = heldBounds.size;
            axis = sz.x >= sz.y && sz.x >= sz.z ? Vector3.right : sz.y >= sz.z ? Vector3.up : Vector3.forward;
        }
        int a = Mathf.Abs(axis.x) > 0.5f ? 0 : Mathf.Abs(axis.y) > 0.5f ? 1 : 2;
        tipPoint = t.TransformPoint(heldBounds.center + axis * heldBounds.extents[a]);
        tipDir = t.TransformDirection(axis).normalized;
        return true;
    }

    public GameObject Held => held;
    /// <summary>Set by short scenes that move the held item themselves (e.g. cutting): the hand keeps its grip.</summary>
    public bool KeepWhileDetached { get; set; }
    public Transform Hand => hand;

    public bool Ready => hand != null;

    public void Init(Animator anim)
    {
        if (anim == null) return;
        if (anim.isHuman)
        {
            hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            fingers = new[]
            {
                Chain(anim, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal),
                Chain(anim, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal),
                Chain(anim, HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal),
                Chain(anim, HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal),
                Chain(anim, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal),
            };
        }
        else
        {
            Transform[] all = anim.transform.GetComponentsInChildren<Transform>(true);
            hand = FindName(all, "righthand", null);
            if (hand != null)
            {
                string[] n = { "index", "middle", "ring", "pinky", "thumb" };
                fingers = new Transform[5][];
                for (int i = 0; i < 5; i++)
                    fingers[i] = new[] { FindName(all, "righthand" + n[i] + "1", hand), FindName(all, "righthand" + n[i] + "2", hand), FindName(all, "righthand" + n[i] + "3", hand) };
            }
        }
        if (hand == null || fingers == null || fingers[0][0] == null || fingers[1][0] == null || fingers[3][0] == null) { hand = null; return; }

        // add the tip (end bone) so the last joint has a direction too
        for (int i = 0; i < fingers.Length; i++)
        {
            Transform d = fingers[i][2];
            Transform tip = d != null && d.childCount > 0 ? d.GetChild(0) : null;
            fingers[i] = new[] { fingers[i][0], fingers[i][1], fingers[i][2], tip };
        }
        baseRot = new Quaternion[5][]; written = new Quaternion[5][]; hasWritten = new bool[5][];
        for (int i = 0; i < 5; i++) { baseRot[i] = new Quaternion[3]; written[i] = new Quaternion[3]; hasWritten[i] = new bool[3]; }
    }

    private static Transform[] Chain(Animator a, HumanBodyBones p, HumanBodyBones m, HumanBodyBones d)
        => new[] { a.GetBoneTransform(p), a.GetBoneTransform(m), a.GetBoneTransform(d) };

    private static Transform FindName(Transform[] all, string key, Transform under)
    {
        foreach (Transform t in all)
        {
            string s = t.name.ToLower().Replace("_", "").Replace(":", "").Replace("mixamorig", "");
            if (s == key && (under == null || t.IsChildOf(under))) return t;
        }
        foreach (Transform t in all)
            if (t.name.ToLower().Replace("_", "").Contains(key) && (under == null || t.IsChildOf(under))) return t;
        return null;
    }

    // ------------------------------------------------------------------ palm geometry

    private void Palm(out Vector3 center, out Vector3 palmN, out Vector3 across, out Vector3 along)
    {
        Vector3 h = hand.position;
        Vector3 idx = fingers[0][0].position, mid = fingers[1][0].position, lit = fingers[3][0].position;
        across = (idx - lit).normalized;                                  // little finger -> index finger
        along = Vector3.ProjectOnPlane(mid - h, across).normalized;      // wrist -> fingers
        palmN = Vector3.Cross(along, across).normalized;
        // Which side is the palm? 1) the way the fingers already bend (a relaxed hand is always a little curled
        // towards the palm), 2) if the fingers are dead straight: the side the thumb sits on.
        float bend = 0f;
        for (int f = 0; f < 4; f++)
        {
            Transform p1 = fingers[f][0], p2 = fingers[f][1], p3 = fingers[f][2], tip = fingers[f][3];
            if (p1 == null || p2 == null || p3 == null) continue;
            Vector3 v1 = (p2.position - p1.position).normalized;
            Vector3 v2 = ((tip != null ? tip.position : p3.position + (p3.position - p2.position)) - p2.position).normalized;
            bend += Vector3.Dot(v2 - v1, palmN);
        }
        if (Mathf.Abs(bend) > 0.08f) { if (bend < 0f) palmN = -palmN; }
        else
        {
            Transform th = fingers[4][1] != null ? fingers[4][1] : fingers[4][0];
            if (th != null && Vector3.Dot(th.position - h, palmN) < 0f) palmN = -palmN;
        }
        center = Vector3.Lerp(h, mid, 0.85f);
    }

    // ------------------------------------------------------------------ hold / release

    /// <summary>Puts the item in the palm (true), or false if this player has no usable hand bones.</summary>
    public bool Hold(GameObject item)
    {
        if (hand == null || item == null) return false;
        Transform t = item.transform;
        held = item;
        heldWorldScale = t.lossyScale;
        scaledDown = false;

        // the visible model's box, in the item's own space
        if (!LocalBounds(t, out Bounds lb)) lb = new Bounds(Vector3.zero, Vector3.one * 0.05f);
        Vector3 ls = t.lossyScale;
        Vector3 size = new Vector3(Mathf.Abs(lb.size.x * ls.x), Mathf.Abs(lb.size.y * ls.y), Mathf.Abs(lb.size.z * ls.z));
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (longest > MaxHeldSize)
        {
            float k = MaxHeldSize / longest;
            t.SetParent(null, true);
            t.localScale = t.localScale * k;
            size *= k; scaledDown = true;
        }

        int iLong = 0, iThin = 0;
        for (int a = 1; a < 3; a++) { if (size[a] > size[iLong]) iLong = a; if (size[a] < size[iThin]) iThin = a; }
        if (iLong == iThin) iThin = (iLong + 1) % 3;
        int iMid = 3 - iLong - iThin;
        thickness = Mathf.Clamp(size[iThin] > 0.004f ? Mathf.Max(size[iThin], size[iMid] * 0.5f) : size[iMid] * 0.5f, 0.01f, 0.09f);

        Palm(out Vector3 c, out Vector3 palmN, out Vector3 across, out Vector3 along);
        palmNLocal = hand.InverseTransformDirection(palmN);
        acrossLocal = hand.InverseTransformDirection(across);

        // world direction for each of the item's local axes: long side across the palm, flat side against it
        Vector3[] col = new Vector3[3];
        // the business end (scalpel blade, key bit) comes out on the thumb / index-finger side - never backwards
        heldTipLocal = Vector3.zero;
        if (ItemTip.TryGetLocal(item, out Vector3 tipL)) heldTipLocal = tipL;
        col[iLong] = heldTipLocal[iLong] < 0f ? -across : across;
        col[iThin] = palmN;
        col[iMid] = Vector3.Cross(across, palmN);
        if (Vector3.Dot(Vector3.Cross(col[0], col[1]), col[2]) < 0f) col[iMid] = -col[iMid];
        Quaternion rot = Quaternion.LookRotation(col[2], col[1]);

        // rests ON the palm skin (not inside the hand); small things stick out past the index finger so they stay visible
        float longW = size[iLong];
        Vector3 grip = c + palmN * (size[iThin] * 0.5f + 0.026f) + along * 0.01f;
        if (longW < 0.14f) grip += across * (longW * 0.38f);
        else grip += across * (longW * 0.2f);   // long things (scalpel): the handle sits in the palm, the blade sticks out
        small = longW < 0.14f;
        heldBounds = lb;
        t.rotation = rot;
        t.position = grip - rot * Vector3.Scale(lb.center, t.lossyScale);
        t.SetParent(hand, true);
        return true;
    }

    /// <summary>Call after the item has been unparented (drop). Gives it back its real size.</summary>
    public void Release(GameObject item)
    {
        if (item != null && item == held && scaledDown && item.transform.parent == null)
            item.transform.localScale = heldWorldScale;
        held = null;
        scaledDown = false;
    }

    private static bool LocalBounds(Transform root, out Bounds b)
    {
        b = new Bounds(); bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Mesh m = null;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null) m = mf.sharedMesh;
            else if (r is SkinnedMeshRenderer sk) m = sk.sharedMesh;
            if (m == null) continue;
            Bounds mb = m.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = root.InverseTransformPoint(r.transform.TransformPoint(p));
                if (!any) { b = new Bounds(l, Vector3.zero); any = true; } else b.Encapsulate(l);
            }
        }
        return any;
    }

    // ------------------------------------------------------------------ finger curl (after the animation each frame)

    void LateUpdate()
    {
        if (hand == null) return;
        bool holding = held != null && (KeepWhileDetached || held.transform.IsChildOf(hand));
        if (!holding) held = null;
        float target = holding ? 1f : 0f;
        if (weight <= 0f && target <= 0f) { RestoreIfStuck(); return; }
        weight = Mathf.MoveTowards(weight, target, Time.deltaTime * 7f);

        Vector3 palmN = hand.TransformDirection(palmNLocal).normalized;
        float open = Mathf.InverseLerp(0.01f, 0.08f, thickness);   // 0 = thin item (tight fist), 1 = thick item
        float a1 = Mathf.Lerp(62f, 35f, open), a2 = Mathf.Lerp(70f, 45f, open), a3 = Mathf.Lerp(40f, 28f, open);
        if (small) { a1 *= 0.85f; a2 *= 0.8f; a3 *= 0.8f; }

        for (int f = 0; f < 5; f++)
        {
            bool thumb = f == 4;
            float[] ang = thumb ? new[] { 10f, 18f, 20f } : new[] { a1, a2, a3 };
            for (int j = 0; j < 3; j++)
            {
                Transform b = fingers[f][j];
                if (b == null) continue;
                Quaternion cur = b.localRotation;
                // if the animation did not write this bone this frame, start again from the un-curled pose (no build-up)
                if (hasWritten[f][j] && Quaternion.Angle(cur, written[f][j]) < 0.01f) cur = baseRot[f][j];
                baseRot[f][j] = cur;
                b.localRotation = cur;

                Transform next = j < 2 ? fingers[f][j + 1] : fingers[f][3];
                Vector3 dir = next != null ? next.position - b.position : (j > 0 && fingers[f][j - 1] != null ? b.position - fingers[f][j - 1].position : Vector3.zero);
                if (dir.sqrMagnitude > 1e-8f)
                {
                    Vector3 axis = Vector3.Cross(dir.normalized, palmN);
                    if (axis.sqrMagnitude > 1e-6f) b.rotation = Quaternion.AngleAxis(ang[j] * weight, axis.normalized) * b.rotation;
                }
                written[f][j] = b.localRotation;
                hasWritten[f][j] = true;
            }
        }
    }

    private void RestoreIfStuck()
    {
        if (fingers == null) return;
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
            {
                if (!hasWritten[f][j]) continue;
                Transform b = fingers[f][j];
                if (b != null && Quaternion.Angle(b.localRotation, written[f][j]) < 0.01f) b.localRotation = baseRot[f][j];
                hasWritten[f][j] = false;
            }
    }
}
