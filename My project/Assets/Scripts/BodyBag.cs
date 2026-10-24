using System.Collections;
using UnityEngine;

/// <summary>
/// Body bag in the Head Office. One bag holds the fuse, the others hide a nasty surprise:
///   unzip  ->  the bag rips open and slumps flat  ->  the thing inside (skull / leg / fingers / bear trap) is flung out,
///   smacks the player in the face (damage, camera jolt, the player staggers back)  ->  it drops to the floor and stays there.
/// An opened scare bag stays opened after a reload (no repeated scare). The fuse bag works exactly as before.
/// </summary>
public class BodyBag : MonoBehaviour, IInteractable
{
    [Header("Bag Contents")]
    [Tooltip("Tick this if this bag contains the real item")]
    public bool containsFuses = false;
    [Tooltip("Tick this if opening this bag triggers a jumpscare")]
    public bool containsJumpscare = false;

    [Header("References")]
    public GameObject fusesItem; // Andar rakha hua item (Fuse Box)
    public GameObject jumpscareZombie; // Andar rakha hua cheez (skull / leg / bear trap ...)
    public Animator bagAnimator;

    [Header("Audio")]
    public AudioClip unzipSound;
    public AudioClip jumpscareSound;

    [Header("Scare")]
    public float scareDamage = 5f;

    private bool isOpened = false;
    private string saveFlag;
    private static AudioClip sUnzip, sBurst, sHit;

    void Start()
    {
        if (fusesItem != null) fusesItem.SetActive(false);
        if (jumpscareZombie != null) jumpscareZombie.SetActive(false);

        saveFlag = "BagOpened_" + SaveManager.GetSceneObjectId(transform);
        if (containsJumpscare && !containsFuses && SaveManager.instance != null && SaveManager.instance.HasFlag(saveFlag))
        {
            isOpened = true;
            Collider c = GetComponent<Collider>(); if (c != null) c.enabled = false;
            Vector3 a = SquashAxis(); transform.localScale = Vector3.Scale(transform.localScale, SquashScale(a, 1f));
        }
    }

    public string GetInteractPrompt()
    {
        return isOpened ? "" : "Press E to Unzip Body Bag";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isOpened) return;
        isOpened = true;
        LoadClips();

        Play(unzipSound != null ? unzipSound : sUnzip, transform.position, 0.9f, 1f);
        if (bagAnimator != null) bagAnimator.SetTrigger("Unzip"); // Agar zip khulne ki animation hai

        if (containsFuses)
        {
            // unchanged: the bag disappears and reveals the fuse
            MeshRenderer bagMesh = GetComponent<MeshRenderer>();
            if (bagMesh != null) bagMesh.enabled = false;
            Collider bagCollider = GetComponent<Collider>();
            if (bagCollider != null) bagCollider.enabled = false;
            if (fusesItem != null) fusesItem.SetActive(true);
            InteractionRaycast.Notify(playerStats, "You found the Fuses!");
        }
        else if (containsJumpscare)
        {
            if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveFlag);
            Collider bagCollider = GetComponent<Collider>();
            if (bagCollider != null) bagCollider.enabled = false;
            StartCoroutine(SlumpBag());
            StartCoroutine(JumpscareRoutine(playerStats));
        }
        else
        {
            StartCoroutine(SlumpBag());
        }
    }

    // ---------------------------------------------------------------- the scare

    private IEnumerator JumpscareRoutine(PlayerStats player)
    {
        Camera cam = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        if (cam == null) cam = Camera.main;
        yield return new WaitForSeconds(0.35f);   // the zip is half open...

        Bounds bag = BagBounds();
        Vector3 start = bag.center + Vector3.up * bag.extents.y * 0.4f;
        Play(jumpscareSound != null ? jumpscareSound : sBurst, start, 1f, 0.6f);

        if (jumpscareZombie == null)
        {
            yield return new WaitForSeconds(0.1f);
            HitPlayer(player, cam, start);
            yield break;
        }

        Transform z = jumpscareZombie.transform;
        Collider[] cols = jumpscareZombie.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in cols) c.enabled = false;
        Rigidbody rb = jumpscareZombie.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        Animator an = jumpscareZombie.GetComponentInChildren<Animator>();
        if (an != null) an.applyRootMotion = false;

        z.position = start;
        Quaternion rot0 = z.rotation;
        Vector3 spinAxis = Random.onUnitSphere;
        jumpscareZombie.SetActive(true);

        // 1) bursts up out of the bag
        Vector3 top = start + Vector3.up * 0.65f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.16f;
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            z.position = Vector3.Lerp(start, top, e);
            z.rotation = Quaternion.AngleAxis(e * 160f, spinAxis) * rot0;
            yield return null;
        }

        // 2) flung straight at the face
        Vector3 from = z.position; Quaternion rFrom = z.rotation;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.2f;
            float e = Mathf.Clamp01(t); e *= e;
            Vector3 face = cam != null ? cam.transform.position + cam.transform.forward * 0.42f - Vector3.up * 0.06f : from;
            z.position = Vector3.Lerp(from, face, e);
            Quaternion look = cam != null ? Quaternion.LookRotation(cam.transform.position - z.position) : rFrom;
            z.rotation = Quaternion.Slerp(rFrom, look, e * 0.7f);
            yield return null;
        }

        HitPlayer(player, cam, z.position);

        // 3) it bounces off and drops to the floor, and stays there
        foreach (Collider c in cols)
        {
            if (c is MeshCollider mc) mc.convex = true;
            c.isTrigger = false;
            c.enabled = true;
        }
        if (cols.Length == 0) AddBox(jumpscareZombie);
        cols = jumpscareZombie.GetComponentsInChildren<Collider>();
        if (player != null)
            foreach (Collider pc in player.GetComponentsInChildren<Collider>())
                foreach (Collider c in cols) Physics.IgnoreCollision(pc, c, true);

        if (rb == null) rb = jumpscareZombie.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Vector3 back = cam != null ? -cam.transform.forward : -transform.forward;
        back.y = 0f;
        rb.linearVelocity = back.normalized * 1.6f + Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f;
        rb.angularVelocity = Random.onUnitSphere * 7f;

        float wait = 0f;
        while (wait < 5f && !(wait > 0.8f && rb.IsSleeping())) { wait += Time.deltaTime; yield return null; }
        rb.isKinematic = true;   // rests on the floor for good (no physics cost)
    }

    private void HitPlayer(PlayerStats player, Camera cam, Vector3 at)
    {
        Play(sHit, cam != null ? cam.transform.position : at, 1f, 0f);
        if (player != null) player.TakeDamage(scareDamage);
        if (cam != null) StartCoroutine(CameraJolt(cam.transform));
        if (player != null) StartCoroutine(Stagger(player));
    }

    /// <summary>The camera snaps back and shakes for a moment (applied on top of the normal mouse look).</summary>
    private IEnumerator CameraJolt(Transform cam)
    {
        float t = 0f, dur = 0.45f;
        while (t < dur)
        {
            yield return null;   // runs after PlayerController.Update has set the look rotation
            if (cam == null) yield break;
            t += Time.deltaTime;
            float k = 1f - t / dur; k *= k;
            float kick = -9f * k;   // head thrown back
            float shake = 5f * k;
            cam.localRotation = cam.localRotation * Quaternion.Euler(kick + Random.Range(-shake, shake), Random.Range(-shake, shake), Random.Range(-shake, shake) * 0.6f);
        }
    }

    /// <summary>The player staggers a step backwards.</summary>
    private IEnumerator Stagger(PlayerStats player)
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) yield break;
        Vector3 dir = player.transform.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -player.transform.forward;
        dir.Normalize();
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            if (cc != null && cc.enabled) cc.Move(dir * (2.4f * Time.deltaTime));
            yield return null;
        }
    }

    // ---------------------------------------------------------------- the bag

    /// <summary>The bag rips open and slumps flat instead of vanishing.</summary>
    private IEnumerator SlumpBag()
    {
        Vector3 axis = SquashAxis();
        Vector3 s0 = transform.localScale;
        Vector3 s1 = Vector3.Scale(s0, SquashScale(axis, 1f));
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.5f;
            float e = Mathf.Clamp01(t);
            float wobble = Mathf.Sin(e * Mathf.PI * 3f) * 0.06f * (1f - e);
            transform.localScale = Vector3.LerpUnclamped(s0, s1, e) + s0 * wobble;
            yield return null;
        }
        transform.localScale = s1;
    }

    /// <summary>Local axis of the bag that points (most) up in the world.</summary>
    private Vector3 SquashAxis()
    {
        float x = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up));
        float y = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
        float zz = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.up));
        return x >= y && x >= zz ? Vector3.right : y >= zz ? Vector3.up : Vector3.forward;
    }

    private static Vector3 SquashScale(Vector3 axis, float amount)
    {
        Vector3 s = Vector3.one * Mathf.Lerp(1f, 1.12f, amount);   // spreads out a little ...
        s[axis == Vector3.right ? 0 : axis == Vector3.up ? 1 : 2] = Mathf.Lerp(1f, 0.38f, amount);   // ... and goes flat
        return s;
    }

    private Bounds BagBounds()
    {
        Renderer r = GetComponent<Renderer>();
        if (r == null) r = GetComponentInChildren<Renderer>();
        return r != null ? r.bounds : new Bounds(transform.position, Vector3.one * 0.3f);
    }

    private static void AddBox(GameObject go)
    {
        BoxCollider bc = go.AddComponent<BoxCollider>();
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { bc.size = Vector3.one * 0.15f; return; }
        Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Vector3 ls = go.transform.lossyScale;
        bc.center = go.transform.InverseTransformPoint(b.center);
        bc.size = new Vector3(b.size.x / Mathf.Max(Mathf.Abs(ls.x), 1e-4f), b.size.y / Mathf.Max(Mathf.Abs(ls.y), 1e-4f), b.size.z / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));
    }

    // ---------------------------------------------------------------- audio

    private static void LoadClips()
    {
        if (sUnzip == null) sUnzip = Resources.Load<AudioClip>("HorrorAudio/TLC_Bag_Unzip");
        if (sBurst == null) sBurst = Resources.Load<AudioClip>("HorrorAudio/TLC_Bag_Burst");
        if (sHit == null) sHit = Resources.Load<AudioClip>("HorrorAudio/TLC_Bag_Hit");
    }

    private static void Play(AudioClip clip, Vector3 pos, float volume, float spatial)
    {
        if (clip == null) return;
        GameObject g = new GameObject("BagSfx");
        g.transform.position = pos;
        AudioSource a = g.AddComponent<AudioSource>();
        a.clip = clip; a.volume = volume; a.spatialBlend = spatial; a.minDistance = 2f; a.maxDistance = 25f; a.priority = 30;
        a.Play();
        Destroy(g, clip.length + 0.2f);
    }
}
