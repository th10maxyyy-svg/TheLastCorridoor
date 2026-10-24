using UnityEngine;
using System.Collections;

public class TaserGun : MonoBehaviour, IInteractable
{
    [Header("Taser Settings")]
    [SerializeField] private float range = 15f;
    [SerializeField] private float stunDuration = 10f;
    [SerializeField] private Vector3 equippedPosition = new Vector3(0.5f, -0.4f, 1f); // Bottom right of camera
    [SerializeField] private Vector3 equippedRotation = new Vector3(0, -90, 0); // Adjust based on your gun model

    [Header("Effects")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip zapSound;
    [SerializeField] private AudioClip emptyClickSound;
    [SerializeField] private ParticleSystem sparkVFX; // Optional

    private bool isEquipped = false;
    private bool hasFired = false;
    private AudioSource audioSource;
    private Rigidbody rb;
    private Collider col;
    private PlayerController owner;
    private string saveId;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // WORLD STATE: respawn ke baad taser haath mein hi rahe (aur agar shot chal chuka hai to khali hi rahe)
        saveId = "Taser_" + SaveManager.GetSceneObjectId(transform);
        if (SaveManager.instance != null)
        {
            hasFired = SaveManager.instance.HasFlag(saveId + "_Fired");
            if (SaveManager.instance.HasFlag(saveId + "_Equipped")) EquipTaser(true);
        }
    }

    public string GetInteractPrompt()
    {
        if (!isEquipped)
            return "Press E to pick up Taser (1 Shot Only)";
        return "";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isEquipped) return; // Already picked up

        EquipTaser(false);
    }

    private void EquipTaser(bool silent)
    {
        // Find the main camera to attach the gun to
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        isEquipped = true;
        owner = mainCam.GetComponentInParent<PlayerController>();
        transform.SetParent(mainCam.transform);

        // Disable physics so it doesn't fall out of hand
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        if (col != null) col.enabled = false;

        // Move to hand position: low on the right, close enough for the real right hand to hold it (see LateUpdate)
        transform.localPosition = HoldPoint;
        transform.localRotation = HeldRotation();
        renderers = GetComponentsInChildren<Renderer>(true);
        Equipped = this;

        if (!silent && pickupSound != null) audioSource.PlayOneShot(pickupSound);
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveId + "_Equipped");
    }

    // ------------------------------------------------------------------ held in the player's real right hand
    // The taser used to float in front of the camera while the empty hand hung beside it. Now the right arm reaches for
    // it and the fingers close around the grip (index finger on top, along the barrel). While the hand holds another
    // item (key, fuse...) the taser is put away and can not fire.
    [Tooltip("Tick/untick if the taser is held upside down")]
    [SerializeField] private bool flipGripUp = true;
    private static readonly Vector3 HoldPoint = new Vector3(0.2f, -0.24f, 0.46f);
    private Renderer[] renderers;
    private bool holstered;
    private HandGrip grip;
    private ArmIK ik;
    private Inventory inv;
    private float ikBlend;
    private bool palmKnown;
    private Vector3 palmC, palmAlong, palmAcross, palmN;

    /// <summary>The taser the player is holding right now (null if none).</summary>
    public static TaserGun Equipped { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEquipped() { Equipped = null; }

    /// <summary>G: the taser is put down on the floor in front of the player (it can be picked up again).</summary>
    public void Drop()
    {
        if (!isEquipped) return;
        isEquipped = false;
        if (Equipped == this) Equipped = null;
        if (ik != null && ik.owner == this) { ik.weight = 0f; ik.useHandPose = false; ik.owner = null; }
        if (grip != null) grip.ExternalGrip = false;
        ikBlend = 0f;
        if (renderers != null) foreach (Renderer r in renderers) if (r != null) r.enabled = true;
        holstered = false;

        Camera cam = Camera.main;
        Vector3 from = cam != null ? cam.transform.position : transform.position;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 spot = from + fwd * 0.6f;
        if (Physics.Linecast(from, spot, out RaycastHit wall, ~0, QueryTriggerInteraction.Ignore)) spot = wall.point - fwd * 0.15f;
        transform.SetParent(null, true);
        float y = spot.y - 1.2f;
        if (Physics.Raycast(spot, Vector3.down, out RaycastHit floor, 3f, ~0, QueryTriggerInteraction.Ignore)) y = floor.point.y;
        // lying on its side on the floor
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, 90f) * HeldRotation();
        transform.position = new Vector3(spot.x, y, spot.z);
        Bounds wb = WorldBounds();
        transform.position += Vector3.up * (y + 0.005f - wb.min.y);

        if (col != null) col.enabled = true;
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        if (audioSource != null && emptyClickSound != null) audioSource.PlayOneShot(emptyClickSound, 0.4f);
        if (SaveManager.instance != null) SaveManager.instance.ClearFlag(saveId + "_Equipped");
    }

    private Bounds WorldBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero); bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void LateUpdate()
    {
        if (!isEquipped || owner == null) return;
        if (inv == null) inv = owner.GetComponent<Inventory>();
        if (grip == null) grip = owner.GetComponent<HandGrip>();
        bool busy = inv != null && inv.HasItem();
        if (busy != holstered)
        {
            holstered = busy;
            if (renderers != null) foreach (Renderer r in renderers) if (r != null) r.enabled = !busy;
        }
        bool canHold = !busy && grip != null && grip.Ready && !owner.isCinematicPlaying && !owner.isHidden;
        if (ik == null && grip != null && grip.Ready) ik = ArmIK.For(owner.gameObject, grip.Hand);
        if (ik == null) return;

        if (!canHold)
        {
            if (ik.owner == this) { ikBlend = 0f; ik.weight = 0f; ik.useHandPose = false; ik.owner = null; }
            if (grip != null) grip.ExternalGrip = false;
            return;
        }
        if (ik.owner != null && ik.owner != this) return;   // a scene is using the arm
        ik.owner = this;
        if (!palmKnown) palmKnown = grip.PalmInHand(out palmC, out palmAlong, out palmAcross, out palmN);
        if (!palmKnown) return;

        // the grip: rear-bottom part of the taser, in camera space
        Transform cam = transform.parent;
        if (cam == null) return;
        Bounds lb = LocalBounds(cam);
        Vector3 gripCam = new Vector3(lb.center.x, lb.min.y + lb.size.y * 0.3f, lb.min.z + lb.size.z * 0.28f);
        Vector3 fwd = cam.forward, up = cam.up, right = cam.right;
        // hand frame wanted: fingers forward along the barrel, index finger on top, palm against the grip from the right
        Quaternion want = Quaternion.LookRotation(fwd, up) * Quaternion.Inverse(Quaternion.LookRotation(palmAlong, palmAcross));
        Vector3 palmTarget = cam.TransformPoint(gripCam) + right * (lb.size.x * 0.5f + 0.012f);
        ik.useHandPose = true;
        ik.handRotation = want;
        ik.handPosition = palmTarget - want * Vector3.Scale(palmC, grip.Hand.lossyScale);
        ik.tremble = 0.0008f;
        ik.poleHint = owner.transform.position + Vector3.down * 0.5f + right * 0.6f;
        ikBlend = Mathf.MoveTowards(ikBlend, 1f, Time.deltaTime * 4f);
        ik.weight = ikBlend;
        grip.ExternalGrip = true;
    }

    /// <summary>Barrel straight ahead, grip down - worked out from the model (ItemTip), else the old Inspector angles.</summary>
    private Quaternion HeldRotation()
    {
        if (ItemTip.TryGetLocal(gameObject, out Vector3 muzzle))
        {
            // the measured 'up' of this revolver points through the grip (tested in game: it came out grip-up) -> flip it
            Vector3 up = ItemTip.TryGetUp(gameObject, out Vector3 u) ? (flipGripUp ? -u : u) : Vector3.zero;
            if (up == Vector3.zero || Mathf.Abs(Vector3.Dot(up, muzzle)) > 0.5f)
                up = Mathf.Abs(muzzle.y) < 0.5f ? Vector3.up : Vector3.forward;
            // camera space: forward = (0,0,1), up = (0,1,0); a slight cant inwards and down, like a real grip
            return Quaternion.Euler(4f, -3f, 0f) * Quaternion.Inverse(Quaternion.LookRotation(muzzle, up));
        }
        return Quaternion.Euler(equippedRotation);
    }

    private Bounds LocalBounds(Transform space)
    {
        Bounds b = new Bounds(transform.localPosition, Vector3.one * 0.05f); bool any = false;
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            MeshFilter mf = r != null ? r.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    void Update()
    {
        // Only allow shooting if equipped, and NOT while a UI (keypad) has the mouse, or during cinematics/death
        if (isEquipped && Input.GetMouseButtonDown(0) && CanFire())
        {
            ShootTaser();
        }
    }

    private bool CanFire()
    {
        if (KeypadController.IsAnyKeypadOpen || Time.timeScale <= 0f) return false;
        if (holstered) return false;   // the hand is holding something else
        if (owner == null) return true;
        return owner.enabled && !owner.isDead && !owner.isCinematicPlaying && !owner.isHidden;
    }

    private void ShootTaser()
    {
        if (hasFired)
        {
            // Already fired, play empty click sound
            if (emptyClickSound != null) audioSource.PlayOneShot(emptyClickSound);
            return;
        }

        hasFired = true; // Use up the 1 shot!
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveId + "_Fired");
        GameProgress.TaserFired(); // ENDINGS: fighting back is remembered (the "Ghost" ending needs a run without the taser)

        if (zapSound != null) audioSource.PlayOneShot(zapSound);
        if (sparkVFX != null) sparkVFX.Play();

        // Shoot raycast from center of screen
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, range))
            {
                // Check if we hit the enemy
                EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    Debug.Log("Taser hit Enemy! Stunned for " + stunDuration + " seconds.");
                    enemy.TakeStun(stunDuration);
                }
                else
                {
                    Debug.Log("Taser missed the enemy. Hit: " + hit.collider.name);
                }
            }
        }
    }
}
