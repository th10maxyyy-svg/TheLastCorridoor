using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class DeadBodyPuzzle : MonoBehaviour, IInteractable
{
    [Header("Puzzle Settings")]
    [Tooltip("Check this if this is the correct body that holds the key and crank.")]
    public bool isCorrectBody = false;
    [Tooltip("How long it takes to cut open the body.")]
    public float cutDuration = 4.0f;
    [Tooltip("Player is se zyada door chala jaye to kaatna ruk jata hai (meters)")]
    public float cancelDistance = 3.5f;

    [Header("Items to Spawn (If Correct)")]
    public GameObject rustyCrank;
    public GameObject examRoomKey;

    [Header("Audio Settings")]
    public AudioClip squelchingSound;
    private AudioSource audioSource;

    [Header("Enemy Attraction")]
    [Tooltip("The EnemyAI that will be alerted by the loud cutting noise.")]
    public EnemyAI enemyAI;

    private bool isCutOpen = false;
    /// <summary>Read by the objective / achievement system (GameProgress): the RIGHT body has been cut open.</summary>
    public bool IsSolved => isCutOpen && isCorrectBody;
    private bool isCutting = false;
    private string SaveFlag => "BodyCut_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isCutOpen = true;
            Collider myCollider = GetComponent<Collider>();
            if (myCollider != null) myCollider.enabled = false;
            if (isCorrectBody)
            {
                if (rustyCrank != null) rustyCrank.SetActive(true);
                if (examRoomKey != null) examRoomKey.SetActive(true);
            }
            return;
        }
        
        // Hide the items initially
        if (isCorrectBody)
        {
            if (rustyCrank != null) rustyCrank.SetActive(false);
            if (examRoomKey != null) examRoomKey.SetActive(false);
        }
    }

    public string GetInteractPrompt()
    {
        if (isCutOpen) return "Already cut open.";
        if (isCutting) return "Cutting...";
        return "Press E to cut open the stomach (Needs Scalpel)";
    }

    public void Interact(PlayerStats player)
    {
        if (isCutOpen || isCutting) return;

        Inventory inventory = player.GetComponent<Inventory>();
        InteractionRaycast raycast = player.GetComponentInChildren<InteractionRaycast>();

        if (inventory != null && inventory.HasItem("Scalpel"))
        {
            StartCoroutine(CutBodyRoutine(player));
        }
        else
        {
            if (raycast != null)
            {
                raycast.ShowNotification("The skin is tough... I need something very sharp.", 4f);
            }
        }
    }

    /// <summary>True while a body is being cut (the scalpel can not be dropped then).</summary>
    public static bool AnyCutting { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { AnyCutting = false; }

    /// <summary>
    /// THE CUT (short cinematic):
    ///   the player steps up to the table and looks down at the belly, leans over, and his own right hand brings the
    ///   scalpel down (blade first, held like a pen). He presses the tip in, then draws it slowly across the stomach in
    ///   two passes - a wet gash opens behind the blade with blood beading along it. When the blade comes out the cut
    ///   gapes open, blood wells up, runs across the body and down its side, drips to the floor and pools there.
    /// The clown hears it when the cut is done. If he comes for the player during it, the scene stops at once.
    /// </summary>
    private IEnumerator CutBodyRoutine(PlayerStats player)
    {
        PlayerController pc = player.GetComponent<PlayerController>();
        Camera cam = player.GetComponentInChildren<Camera>();
        Inventory inv = player.GetComponent<Inventory>();
        EnemyAI enemy = enemyAI != null ? enemyAI : FindAnyObjectByType<EnemyAI>();
        if (enemy != null && enemy.isActiveAndEnabled && enemy.State == EnemyAI.AIState.Chase)
        {
            InteractionRaycast.Notify(player, "Not now... he's right behind me!", 2.5f);
            yield break;
        }
        if (cam == null) yield break;

        isCutting = true; AnyCutting = true;
        Collider bodyCol = GetComponent<Collider>();
        Transform pt = player.transform, ct = cam.transform;
        CharacterController cc = player.GetComponent<CharacterController>();
        Vector3 up = Vector3.up;

        // ---- where: along the body, across the middle of the belly, on the side facing the player
        Bounds bb = bodyCol != null ? bodyCol.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        Vector3 lenAxis = Vector3.ProjectOnPlane(transform.right, up);
        if (lenAxis.sqrMagnitude < 0.01f) lenAxis = bb.size.x >= bb.size.z ? Vector3.right : Vector3.forward;
        lenAxis.Normalize();
        Vector3 side = Vector3.ProjectOnPlane(pt.position - bb.center, up);
        side -= lenAxis * Vector3.Dot(side, lenAxis);
        if (side.sqrMagnitude < 0.0004f) side = Vector3.Cross(lenAxis, up);
        side.Normalize();
        Vector3 facing = -side;
        Vector3 rightOfPlayer = Vector3.Cross(up, facing).normalized;
        Vector3 a = Surface(bodyCol, bb.center - lenAxis * 0.15f + side * 0.03f, bb);
        Vector3 b = Surface(bodyCol, bb.center + lenAxis * 0.15f + side * 0.03f, bb);
        if (Vector3.Dot(b - a, rightOfPlayer) < 0f) { Vector3 t = a; a = b; b = t; }   // cut left -> right, like a right-handed person
        Vector3 cutCenter = (a + b) * 0.5f;
        Vector3 cutDir = Vector3.ProjectOnPlane(b - a, up).normalized;

        // ---- where the player stands: right at the table edge, facing the body
        float halfW = Mathf.Abs(side.x) * bb.extents.x + Mathf.Abs(side.z) * bb.extents.z;
        Vector3 stand = new Vector3(cutCenter.x, pt.position.y, cutCenter.z) + side * (halfW + 0.3f);
        if (!SpotFree(stand, cc, bodyCol, pt)) stand = pt.position;
        Quaternion yaw1 = Quaternion.LookRotation(facing, up);

        // ---- take control
        if (pc != null) pc.isCinematicPlaying = true;
        Vector3 camLocalPos0 = ct.localPosition; Quaternion camLocalRot0 = ct.localRotation;
        Animator anim = player.GetComponentInChildren<Animator>();
        Transform model = anim != null ? anim.transform : null;
        Vector3 modelLocal0 = model != null ? model.localPosition : Vector3.zero;

        HandGrip grip = player.GetComponent<HandGrip>();
        Vector3 tipP = Vector3.zero, tipD = Vector3.forward;
        bool handOk = grip != null && grip.Ready && grip.Held != null && grip.HeldTip(out tipP, out tipD);
        ArmIK ik = handOk ? ArmIK.For(player.gameObject, grip.Hand) : null;
        if (ik != null) { ik.useHandPose = false; ik.owner = this; }
        List<Renderer> hiddenR = new List<Renderer>();
        Transform blade = null; float bladeLen = 0.15f;
        if (ik != null)
        {
            ik.tipInHand = grip.Hand.InverseTransformPoint(tipP);
            ik.dirInHand = grip.Hand.InverseTransformDirection(tipD);
            ik.weight = 0f;
            // the scalpel itself is placed exactly on the cut every frame (always in view), the arm reaches for it
            drivenItem = grip.Held.transform;
            drivenParent = drivenItem.parent; drivenLocalPos = drivenItem.localPosition; drivenLocalRot = drivenItem.localRotation;
            tipLocalItem = drivenItem.InverseTransformPoint(tipP);
            dirLocalItem = drivenItem.InverseTransformDirection(tipD);
            foreach (Renderer r in drivenItem.GetComponentsInChildren<Renderer>(true)) if (!r.enabled) { r.enabled = true; reEnabled.Add(r); }
            grip.KeepWhileDetached = true;
            drivenItem.SetParent(null, true);
        }
        else
        {
            GameObject held = inv != null ? inv.GetCurrentItem() : null;
            if (held != null) foreach (Renderer r in held.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hiddenR.Add(r); }
            blade = MakeBladeCopy(held, out bladeLen);
        }

        GameObject fx = new GameObject("CutFX");
        LineRenderer gash = BloodFX.Line(fx.transform, BloodFX.Wet, "Gash");
        LineRenderer gashInner = BloodFX.Line(fx.transform, BloodFX.Dark, "GashInner");
        ParticleSystem beads = BloodFX.Drops(fx.transform, "Beads", 250, 0f, false);
        ParticleSystem spray = BloodFX.Drops(fx.transform, "Spray", 150);
        AudioClip slice = Resources.Load<AudioClip>("HorrorAudio/TLC_Body_Slice");
        AudioClip gushClip = Resources.Load<AudioClip>("HorrorAudio/TLC_Blood_Gush");
        bool cancelled = false;

        // 1) step up to the body and look down at the belly (0.8 s)
        if (cc != null) cc.enabled = false;
        Vector3 p0 = pt.position; Quaternion y0 = pt.rotation; Quaternion camW0 = ct.rotation;
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / 0.8f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            pt.position = Vector3.Lerp(p0, stand, e);
            pt.rotation = Quaternion.Slerp(y0, yaw1, e);
            ct.rotation = Quaternion.Slerp(camW0, Quaternion.LookRotation(cutCenter - ct.position), e);
            yield return null;
        }

        // how far he has to lean over so the hand reaches the belly
        Vector3 hover = a + up * 0.07f;
        Vector3 lean = Vector3.zero;
        if (ik != null)
        {
            float arm = (ik.upper.position - ik.lower.position).magnitude + (ik.lower.position - ik.hand.position).magnitude;
            float tool = grip.HeldTip(out tipP, out tipD) ? (tipP - ik.hand.position).magnitude : 0.1f;
            Vector3 toHover = hover - ik.upper.position;
            float reach = arm * 0.86f + tool * 0.7f;
            if (toHover.magnitude > reach) lean = toHover.normalized * (toHover.magnitude - reach);
        }
        else lean = (cutCenter + up * 0.55f + side * 0.45f) - ct.position;
        Vector3 leanFlat = Vector3.ProjectOnPlane(lean, up);
        if (leanFlat.magnitude > 0.4f) leanFlat = leanFlat.normalized * 0.4f;
        lean = leanFlat + up * Mathf.Clamp(lean.y, -0.42f, 0.05f);

        Vector3 tipDir = (Vector3.down * 0.82f + cutDir * 0.42f - side * 0.18f).normalized;   // blade down into the skin, leaning along the cut
        Vector3 modelW0 = model != null ? model.position : Vector3.zero;
        Vector3 camP1 = ct.position;
        Vector3 tipStart = tipP, dirStart = tipD;
        if (ik != null) ik.poleHint = ik.upper.position + Vector3.down * 0.45f + rightOfPlayer * 0.35f - facing * 0.1f;

        // 2) lean over and bring the scalpel to the skin (0.8 s)
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / 0.8f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (model != null) model.position = modelW0 + lean * e;
            ct.position = camP1 + lean * e * 0.95f;
            ct.rotation = Quaternion.LookRotation(Vector3.Lerp(cutCenter, a, 0.35f) - ct.position);
            if (ik != null)
            {
                ik.weight = e;
                SetTip(ik, null, Vector3.Lerp(tipStart + lean * e, hover, e), Vector3.Slerp(dirStart, tipDir, e));
            }
            else if (blade != null) PlaceBlade(blade, Vector3.Lerp(hover + up * 0.08f, hover, e), tipDir);
            yield return null;
        }
        Vector3 camHold = ct.position;

        // 3) press the tip in (0.45 s) - a bead of blood wells up around it
        if (squelchingSound != null) audioSource.PlayOneShot(squelchingSound, 0.7f);
        for (float t = 0f; t < 1f && !cancelled;)
        {
            if (Interrupted(player, enemy)) { cancelled = true; break; }
            t += Time.deltaTime / 0.45f;
            float e = Mathf.Clamp01(t); e = e * e;
            Vector3 tip = Vector3.Lerp(hover, a - up * 0.012f, e);
            SetTip(ik, blade, tip, tipDir);
            if (t > 0.7f && Random.value < 0.5f) BloodFX.Emit(beads, a + Random.insideUnitSphere * 0.004f + up * 0.002f, Vector3.zero, Random.Range(0.006f, 0.01f), 25f);
            ct.position = camHold + Random.insideUnitSphere * 0.0015f * e;
            yield return null;
        }

        // 4) two slow passes across the belly: the first opens the skin, the second goes deeper
        float[] passTime = { Mathf.Max(1.4f, cutDuration * 0.45f), Mathf.Max(1.1f, cutDuration * 0.35f) };
        float[] depth = { 0.012f, 0.022f };
        for (int pass = 0; pass < 2 && !cancelled; pass++)
        {
            if (slice != null) audioSource.PlayOneShot(slice, pass == 0 ? 0.9f : 1f);
            if (pass == 1)
            {
                // back to the start of the cut, blade just above the skin
                for (float t = 0f; t < 1f;)
                {
                    t += Time.deltaTime / 0.35f;
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                    Vector3 p = Vector3.Lerp(b, a, e) + up * (0.03f * Mathf.Sin(e * Mathf.PI));
                    SetTip(ik, blade, p, tipDir);
                    yield return null;
                }
            }
            for (float t = 0f; t < 1f;)
            {
                if (Interrupted(player, enemy)) { cancelled = true; break; }
                t += Time.deltaTime / passTime[pass];
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                float drag = e + Mathf.Sin(e * 40f) * 0.004f;   // the blade catches and drags
                Vector3 tip = Surface(bodyCol, Vector3.Lerp(a, b, Mathf.Clamp01(drag)), bb) - up * depth[pass];
                SetTip(ik, blade, tip, tipDir);
                float reached = pass == 0 ? e : 1f;
                DrawGash(gash, gashInner, bodyCol, bb, a, b, reached, 0.005f + pass * 0.004f + e * 0.002f);

                // blood beads up along the fresh cut behind the blade
                if (Random.value < 0.6f)
                {
                    float k = Random.Range(0f, Mathf.Max(0.02f, pass == 0 ? e - 0.04f : 1f));
                    Vector3 on = Surface(bodyCol, Vector3.Lerp(a, b, k), bb) + up * 0.003f + side * Random.Range(-0.004f, 0.004f);
                    BloodFX.Emit(beads, on, Vector3.zero, Random.Range(0.006f, 0.012f), 25f);
                }
                if (pass == 1 && Random.value < 0.18f)
                    BloodFX.Emit(spray, tip + up * 0.01f, (up * 1.1f + Random.insideUnitSphere * 0.5f - cutDir * 0.3f) * Random.Range(0.4f, 0.9f), Random.Range(0.004f, 0.008f), 1.2f);

                ct.position = camHold + Random.insideUnitSphere * 0.001f;
                ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(Vector3.Lerp(cutCenter, tip, 0.45f) - ct.position), Time.deltaTime * 4f);
                yield return null;
            }
        }

        if (!cancelled)
        {
            // 5) the blade comes out, the wound gapes open and the blood comes
            if (gushClip != null) audioSource.PlayOneShot(gushClip, 1f);
            Vector3 lift0 = b - up * depth[1];
            BloodFX flow = fx.AddComponent<BloodFX>();
            flow.StartFlow(bodyCol, bb, a, b, 6);
            GameObject pool = BloodFX.Puddle(fx.transform, cutCenter + up * 0.003f, lenAxis, BloodFX.Wet, name.GetHashCode());
            flow.AddPool(pool.transform, 0.3f, 0.09f);
            for (float t = 0f; t < 1f;)
            {
                t += Time.deltaTime / 1.8f;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                float out1 = Mathf.Clamp01(t * 3f);
                SetTip(ik, blade, Vector3.Lerp(lift0, lift0 + up * 0.12f - cutDir * 0.03f, out1), tipDir);
                DrawGash(gash, gashInner, bodyCol, bb, a, b, 1f, 0.014f + 0.022f * e);
                if (Random.value < 0.5f) BloodFX.Emit(beads, Vector3.Lerp(a, b, Random.value) + up * 0.004f, up * Random.Range(0.002f, 0.01f), Random.Range(0.008f, 0.014f), 25f);
                ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(cutCenter - ct.position), Time.deltaTime * 3f);
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
        }

        // 6) straighten up and give the control back (0.6 s)
        Vector3 camP2 = ct.position; Quaternion camR2 = ct.rotation;
        Vector3 modelW2 = model != null ? model.position : Vector3.zero;
        float w0 = ik != null ? ik.weight : 0f;
        if (blade != null) Destroy(blade.gameObject);
        for (float t = 0f; t < 1f;)
        {
            t += Time.deltaTime / 0.6f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            if (model != null) model.position = Vector3.Lerp(modelW2, model.parent != null ? model.parent.TransformPoint(modelLocal0) : modelW2, e);
            Vector3 camTarget = ct.parent != null ? ct.parent.TransformPoint(camLocalPos0) : camP2;
            Quaternion camTargetR = ct.parent != null ? ct.parent.rotation * camLocalRot0 : camR2;
            ct.position = Vector3.Lerp(camP2, camTarget, e);
            ct.rotation = Quaternion.Slerp(camR2, camTargetR, e);
            if (ik != null) ik.weight = w0 * (1f - e);
            if (drivenItem != null && drivenParent != null)
            {
                drivenItem.position = Vector3.Lerp(drivenItem.position, drivenParent.TransformPoint(drivenLocalPos), e);
                drivenItem.rotation = Quaternion.Slerp(drivenItem.rotation, drivenParent.rotation * drivenLocalRot, e);
            }
            yield return null;
        }
        if (drivenItem != null)
        {
            if (drivenParent != null) { drivenItem.SetParent(drivenParent, false); drivenItem.localPosition = drivenLocalPos; drivenItem.localRotation = drivenLocalRot; }
            foreach (Renderer r in reEnabled) if (r != null) r.enabled = false;
            reEnabled.Clear();
            drivenItem = null;
        }
        if (grip != null) grip.KeepWhileDetached = false;
        if (model != null) model.localPosition = modelLocal0;
        ct.localPosition = camLocalPos0; ct.localRotation = camLocalRot0;
        if (ik != null) { ik.weight = 0f; if (ik.owner == this) ik.owner = null; }
        foreach (Renderer r in hiddenR) if (r != null) r.enabled = true;
        if (cc != null) cc.enabled = true;
        if (pc != null) pc.isCinematicPlaying = false;
        isCutting = false; AnyCutting = false;

        if (cancelled)
        {
            Destroy(fx);
            if (audioSource != null) audioSource.Stop();
            if (!player.IsDead()) InteractionRaycast.Notify(player, "Cutting interrupted...", 2f);
            yield break;
        }
        fx.transform.SetParent(transform, true);   // the wound and the blood stay

        isCutOpen = true;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);
        if (enemy != null) enemy.ForceInvestigate(player.transform.position, 15f);   // the noise brings him

        // BUG FIX: Disable the dead body's collider so it doesn't block you from clicking the Crank/Key!
        if (bodyCol != null) bodyCol.enabled = false;

        if (isCorrectBody)
        {
            if (rustyCrank != null) rustyCrank.SetActive(true);
            if (examRoomKey != null) examRoomKey.SetActive(true);
            if (inv != null) inv.RemoveItem("Scalpel", false);   // USED UP: the scalpel is left in the body
            InteractionRaycast.Notify(player, "Found something inside!", 3f);
        }
        else
        {
            InteractionRaycast.Notify(player, "Nothing but organs here...", 3f);
        }
    }

    private Transform drivenItem, drivenParent;
    private Vector3 drivenLocalPos, tipLocalItem, dirLocalItem = Vector3.forward;
    private Quaternion drivenLocalRot;
    private readonly List<Renderer> reEnabled = new List<Renderer>();

    private void SetTip(ArmIK ik, Transform blade, Vector3 tip, Vector3 dir)
    {
        if (drivenItem != null)
        {
            Quaternion r = Quaternion.FromToRotation(drivenItem.rotation * dirLocalItem, dir.normalized) * drivenItem.rotation;
            drivenItem.rotation = r;
            drivenItem.position = tip - r * Vector3.Scale(tipLocalItem, drivenItem.lossyScale);
        }
        if (ik != null) { ik.tipTarget = tip; ik.tipDirection = dir; }
        else if (blade != null) PlaceBlade(blade, tip, dir);
    }

    private bool Interrupted(PlayerStats player, EnemyAI enemy)
    {
        if (player == null || player.IsDead()) return true;
        if (enemy != null && enemy.isActiveAndEnabled && enemy.State == EnemyAI.AIState.Chase &&
            Vector3.Distance(enemy.transform.position, player.transform.position) < 14f) return true;
        return false;
    }

    /// <summary>Room for the player to stand there (nothing solid but the floor, the body and the player himself).</summary>
    private static bool SpotFree(Vector3 p, CharacterController cc, Collider body, Transform player)
    {
        float r = cc != null ? cc.radius * 0.9f : 0.25f;
        foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * (r + 0.25f), p + Vector3.up * 1.6f, r, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c == body || c.transform.IsChildOf(player)) continue;
            return false;
        }
        return Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------ helpers

    private static Vector3 Surface(Collider c, Vector3 p, Bounds bb)
    {
        Vector3 from = new Vector3(p.x, bb.max.y + 0.5f, p.z);
        if (c != null && c.enabled && c.Raycast(new Ray(from, Vector3.down), out RaycastHit h, 3f)) return h.point;
        return new Vector3(p.x, bb.max.y, p.z);
    }

    private static void DrawGash(LineRenderer outer, LineRenderer inner, Collider c, Bounds bb, Vector3 a, Vector3 b, float reached, float width)
    {
        int n = 16;
        outer.positionCount = n; inner.positionCount = n;
        Vector3 across = Vector3.Cross(b - a, Vector3.up).normalized;
        for (int i = 0; i < n; i++)
        {
            float k = reached * i / (n - 1f);
            Vector3 p = Surface(c, Vector3.Lerp(a, b, k), bb) + Vector3.up * 0.0035f;
            p += across * (Mathf.Sin(k * 23f) * 0.0025f + Mathf.Sin(k * 61f) * 0.001f);   // a ragged, hand-made line
            outer.SetPosition(i, p);
            inner.SetPosition(i, p + Vector3.up * 0.0012f);
        }
        AnimationCurve w = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.2f, 0.8f), new Keyframe(0.55f, 1f), new Keyframe(0.85f, 0.7f), new Keyframe(1f, 0.12f));
        outer.widthCurve = w; outer.widthMultiplier = width;
        inner.widthCurve = w; inner.widthMultiplier = width * 0.5f;
    }

    /// <summary>A plain visual copy of the held scalpel (meshes only) - used only if the player's hand can not be posed.</summary>
    private static Transform MakeBladeCopy(GameObject held, out float length)
    {
        length = 0.15f;
        if (held == null) return null;
        GameObject root = new GameObject("ScalpelCut");
        root.transform.SetPositionAndRotation(held.transform.position, held.transform.rotation);
        Bounds lb = new Bounds(); bool any = false;
        foreach (MeshFilter mf in held.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;
            GameObject g = new GameObject(mf.name);
            g.transform.SetParent(root.transform, false);
            g.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            g.transform.localScale = Div(mf.transform.lossyScale, root.transform.lossyScale);
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            g.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = root.transform.InverseTransformPoint(mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (!any) { lb = new Bounds(p, Vector3.zero); any = true; } else lb.Encapsulate(p);
            }
        }
        if (!any) { Destroy(root); return null; }
        // pivot: origin at the blade tip, local +Z from the tip towards the handle
        Vector3 tipAxis;
        if (!ItemTip.TryGetLocal(held, out tipAxis))
        {
            int ax = lb.size.x >= lb.size.y && lb.size.x >= lb.size.z ? 0 : lb.size.y >= lb.size.z ? 1 : 2;
            tipAxis = ax == 0 ? Vector3.right : ax == 1 ? Vector3.up : Vector3.forward;
        }
        int a2 = Mathf.Abs(tipAxis.x) > 0.5f ? 0 : Mathf.Abs(tipAxis.y) > 0.5f ? 1 : 2;
        Vector3 tipLocal = lb.center + tipAxis * lb.extents[a2];
        GameObject pivot = new GameObject("ScalpelPivot");
        pivot.transform.SetPositionAndRotation(root.transform.TransformPoint(tipLocal), Quaternion.LookRotation(root.transform.TransformDirection(-tipAxis)));
        root.transform.SetParent(pivot.transform, true);
        length = lb.size[a2];
        return pivot.transform;
    }

    private static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / Mathf.Max(1e-5f, b.x), a.y / Mathf.Max(1e-5f, b.y), a.z / Mathf.Max(1e-5f, b.z));

    /// <summary>Fallback blade: tip on the point, pointing along 'dir' (handle behind it).</summary>
    private static void PlaceBlade(Transform pivot, Vector3 tip, Vector3 dir)
    {
        pivot.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        pivot.position = tip;
    }
}
