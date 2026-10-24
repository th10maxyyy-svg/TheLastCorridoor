using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Key Settings")]
    [SerializeField] private string keyName = "RedKey";
    [SerializeField] private string promptMessage = "Press E to pick up Key";

    [Header("Horror Events")]
    [Tooltip("If true, picking up this key will wake up the enemy and start the Nightmare Phase!")]
    [SerializeField] private bool wakesUpEnemy = false;

    [Header("Custom Event")]
    [Tooltip("Add things here that should happen when this key is picked up (e.g. Turn off lights, Spawn enemies)")]
    public UnityEngine.Events.UnityEvent onPickUpEvent;

    private bool hasFiredPickupEvent = false;

    void Start()
    {
        // WORLD STATE CHECK: Agar ye key pehle hi uthai ja chuki hai (aur na zameen par giri hai na haath mein hai) to delete kar do
        if (SaveManager.instance != null && !SaveManager.instance.ShouldItemExistInWorld(SaveManager.instance.GetItemId(gameObject)))
        {
            Destroy(gameObject);
            Debug.Log("[WorldState] KeyItem deleted because it was already collected: " + keyName);
            return;
        }
        FitColliderToModel();
        if (GetComponent<ItemGlint>() == null) gameObject.AddComponent<ItemGlint>();   // a small glint so it can be spotted
    }

    /// <summary>
    /// BUG FIX (scalpel etc.): the pick-up box sometimes sat beside / inside the shelf instead of around the model,
    /// so the crosshair hit the shelf and the item could not be grabbed. If the box doesn't wrap the visible model,
    /// it is refitted around it (a little padded, never thinner than 10 cm).
    /// </summary>
    private void FitColliderToModel()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        bool any = false; Vector3 mn = Vector3.zero, mx = Vector3.zero;
        void Add(Vector3 world)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            if (!any) { mn = mx = l; any = true; } else { mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l); }
        }
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            Bounds b; Transform t;
            if (mf != null && mf.sharedMesh != null) { b = mf.sharedMesh.bounds; t = mf.transform; }
            else if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) { b = sk.sharedMesh.bounds; t = sk.transform; }
            else continue;
            for (int i = 0; i < 8; i++)
                Add(t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        }
        if (!any) return;

        Vector3 size = mx - mn, center = (mn + mx) * 0.5f;
        Vector3 ls = transform.lossyScale;
        float Vol(Vector3 v) => Mathf.Abs(v.x * ls.x * v.y * ls.y * v.z * ls.z);
        Bounds cur = new Bounds(bc.center, bc.size);
        bool wraps = cur.Contains(center) && Vol(bc.size) <= Mathf.Max(Vol(size) * 8f, 0.002f) && Vol(bc.size) >= Vol(size) * 0.5f;
        if (wraps) return;

        for (int a = 0; a < 3; a++)
        {
            float s = Mathf.Abs(ls[a]) < 1e-5f ? 1f : Mathf.Abs(ls[a]);
            size[a] = Mathf.Max(size[a] * 1.2f, 0.1f / s);
        }
        bc.center = center; bc.size = size;
    }

    public string GetKeyName()
    {
        return keyName;
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }

    public void Interact(PlayerStats playerStats)
    {
        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null)
        {
            // Instead of destroying, pick up the physical object
            inventory.PickUpItem(this.gameObject);

            // Trigger custom events only the FIRST time (not every time the key is dropped and picked up again)
            // BUG FIX: remembered in the save, so dropping it / dying and picking it up again never replays the event (ambush etc.)
            SaveManager sm = SaveManager.instance;
            string evtFlag = "KeyEvent_" + (sm != null ? sm.GetItemId(gameObject) : name);
            if (!hasFiredPickupEvent && (sm == null || !sm.HasFlag(evtFlag)))
            {
                hasFiredPickupEvent = true;
                if (sm != null) sm.SetFlag(evtFlag);
                onPickUpEvent?.Invoke();
            }

            // Show notification on screen
            InteractionRaycast.Notify(playerStats, "Picked up: " + keyName);

            // Wake up enemy if this key triggers the nightmare phase
            if (wakesUpEnemy || keyName == "Staff Key")
            {
                ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                if (pm != null)
                {
                    // Starts the Nightmare phase (wakens enemy)
                    pm.StartNightmarePhase();
                }
            }
        }
    }
}
