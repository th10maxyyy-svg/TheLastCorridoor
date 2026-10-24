using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private Transform holdPosition; // Assign a 'Hand' empty object under Main Camera in Unity
    [SerializeField] private float throwForce = 5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource inventoryAudioSource;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip dropSound;

    private GameObject currentItem;
    // Exactly the colliders we switched off on pickup, so drop turns the same ones back on
    private readonly List<Collider> disabledColliders = new List<Collider>();
    private PlayerController playerController;
    private CharacterController characterController;
    private HandGrip grip;   // real palm grip + curled fingers (falls back to the old hold point if the hand has no bones)

    void Start()
    {
        if (!string.IsNullOrEmpty(ReturnedNote)) { StartCoroutine(ShowReturnedNote(ReturnedNote)); ReturnedNote = null; }
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();

        if (inventoryAudioSource == null)
        {
            inventoryAudioSource = gameObject.AddComponent<AudioSource>();
            inventoryAudioSource.spatialBlend = 0f; // 2D sound for player UI
        }
        // FINAL GRIP FIX: Hum cheez ko hamesha ke liye haath ki haddi (bone) ke sath jor rahe hain!
        // Is se cheez hamesha haath mein rahegi. Agar aap oopar dekhenge, to cheez aur haath dono neechay rahenge.
        Animator anim = transform.root.GetComponentInChildren<Animator>();
        Transform rHand = null;

        grip = GetComponent<HandGrip>();
        if (grip == null) grip = gameObject.AddComponent<HandGrip>();
        grip.Init(anim);

        if (anim != null && anim.isHuman)
        {
            // Ungliyon ke darmiyan laane ke liye seedha Middle Finger par attach karo!
            rHand = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (rHand == null) rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        }
        else
        {
            Transform[] allBones = transform.root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allBones)
            {
                if (t.name.ToLower().Contains("rightmiddle1") || t.name.ToLower().Contains("right_middle"))
                {
                    rHand = t;
                    break;
                }
            }
            if (rHand == null)
            {
                foreach (Transform t in allBones)
                {
                    if (t.name.ToLower().Contains("righthand") || t.name.ToLower().Contains("right_hand"))
                    {
                        rHand = t;
                        break;
                    }
                }
            }
        }

        if (rHand != null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                Transform hp = cam.transform.Find("hold position");
                if (hp != null) Destroy(hp.gameObject);
            }

            // Naya hold position direct ungliyon (fingers) ke andar banao
            GameObject newHp = new GameObject("Hand_HoldPosition");
            newHp.transform.SetParent(rHand);

            // Hold position ko bilkul zero par rakho, hum cheez par direct apply karenge offset!
            newHp.transform.localPosition = Vector3.zero;
            newHp.transform.localRotation = Quaternion.identity;

            holdPosition = newHp.transform;
        }
    }

    private IEnumerator ShowReturnedNote(string note)
    {
        yield return new WaitForSeconds(3.5f);   // after the wake-up
        InteractionRaycast.Notify(this, note, 4.5f);
    }

    void Update()
    {
        // Drop item on pressing G
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (currentItem == null)
            {
                // nothing from the inventory in the hand: drop the taser if it is held, otherwise say so
                TaserGun taser = TaserGun.Equipped;
                if (taser != null && GetDropBlockedReason() == null) taser.Drop();
                else InteractionRaycast.Notify(this, "There is nothing in your hand.", 1.5f);
            }
            else
            {
                string blockedReason = GetDropBlockedReason();
                if (blockedReason == null)
                {
                    DropCurrentItem();
                }
                else
                {
                    Debug.Log("[Inventory] G pressed but drop blocked: " + blockedReason);
                }
            }
        }
    }

    /// <summary>Null when dropping is allowed. Only blocked while dead or while the keypad UI is open.</summary>
    private string GetDropBlockedReason()
    {
        // NOTE: Cursor.lockState / cinematic flags are NOT checked - in the Unity Editor the cursor unlocks after ESC,
        // and a stale flag must never make the G key silently stop working.
        if (KeypadController.IsAnyKeypadOpen) return "keypad open";
        if (DeadBodyPuzzle.AnyCutting) return "cutting";
        if (playerController != null && playerController.isDead) return "player dead";
        if (playerController != null && playerController.isHidden) return "hidden";   // the item would fall inside the locker and could never be picked up
        return null;
    }

    public void PickUpItem(GameObject item) => PickUpItem(item, false);

    public void PickUpItem(GameObject item, bool silent)
    {
        if (item == null || item == currentItem) return;

        // remember where it was first found (it comes back there if the player dies holding it)
        if (SaveManager.instance != null && !item.transform.IsChildOf(transform)) SaveManager.instance.RecordItemHome(item);

        // If already holding an item, drop it first
        if (currentItem != null)
        {
            DropCurrentItem();
        }

        currentItem = item;
        if (!item.activeSelf) item.SetActive(true);

        // Play pickup sound
        if (!silent && inventoryAudioSource != null && pickupSound != null)
        {
            inventoryAudioSource.PlayOneShot(pickupSound);
        }

        // Disable physics
        Rigidbody rb = currentItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Disable ALL colliders (even in children) to prevent getting stuck in doors
        disabledColliders.Clear();
        foreach (Collider col in currentItem.GetComponentsInChildren<Collider>())
        {
            if (col.enabled)
            {
                col.enabled = false;
                disabledColliders.Add(col);
            }
        }

        // Attach: held properly in the palm with the fingers closed around it
        if (grip != null && grip.Ready && grip.Hold(currentItem))
        {
        }
        else if (holdPosition != null)
        {
            currentItem.transform.SetParent(holdPosition);

            // Aapne jo exact Inspector mein dekha hai, wohi direct CHEEZ par apply ho raha hai!
            currentItem.transform.localPosition = new Vector3(-0.03f, 0.24f, 0.15f);
            currentItem.transform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning("HOLD POSITION GAYAB HAI! Player ke andar Hold Position assign nahi kiya gaya. Item camera ke agay set kar raha hoon.");
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                currentItem.transform.SetParent(cam.transform);
                currentItem.transform.localPosition = new Vector3(0.3f, -0.3f, 0.6f); // Default hand position
                currentItem.transform.localRotation = Quaternion.identity;
            }
        }

        // WORLD STATE: picked up -> not on the floor any more, now in the hand
        if (SaveManager.instance != null)
        {
            SaveManager.instance.OnItemPickedUp(item);
        }
        GameProgress.ItemPickedUp(item); // JOURNAL: list of every item found (inspect it with TAB)

        Debug.Log("Picked up " + item.name);
    }

    public void DropCurrentItem()
    {
        if (currentItem == null) return;

        GameObject item = currentItem;
        currentItem = null;

        // Play drop sound
        if (inventoryAudioSource != null && dropSound != null)
        {
            inventoryAudioSource.PlayOneShot(dropSound);
        }

        item.transform.SetParent(null);
        if (grip != null) grip.Release(item);   // real size back (big items are shown smaller in the hand)

        // Make sure the item is not inside a wall: if a wall is between the camera and the hand, pull it back
        Camera cam = GetComponentInChildren<Camera>();
        Vector3 throwDir = cam != null ? cam.transform.forward : transform.forward;
        if (cam != null)
        {
            Vector3 from = cam.transform.position;
            Vector3 to = item.transform.position;
            if (Physics.Linecast(from, to, out RaycastHit wallHit, ~0, QueryTriggerInteraction.Ignore) && !wallHit.transform.IsChildOf(transform))
            {
                item.transform.position = wallHit.point + wallHit.normal * 0.15f;
            }
        }

        // BUG FIX: colliders wapas ON karo, warna item farsh ke aar paar gir jata tha
        foreach (Collider col in disabledColliders)
        {
            if (col == null) continue;
            col.enabled = true;
            if (characterController != null) StartCoroutine(IgnorePlayerCollisionBriefly(col));
        }
        disabledColliders.Clear();

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // Throw it slightly forward
            rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
        }
        else if (Physics.Raycast(item.transform.position, Vector3.down, out RaycastHit floorHit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            // No physics on this item: put it on the floor instead of leaving it floating in the air
            item.transform.position = floorHit.point + Vector3.up * 0.05f;
        }

        ThrowableItem throwable = item.GetComponent<ThrowableItem>();
        if (throwable != null)
        {
            throwable.MarkAsDropped();
        }

        if (SaveManager.instance != null)
        {
            SaveManager.instance.OnItemDropped(item);
        }

        Debug.Log("Dropped " + item.name);
    }

    // The item starts right next to the player - don't let it shove the CharacterController around
    private IEnumerator IgnorePlayerCollisionBriefly(Collider col)
    {
        Physics.IgnoreCollision(col, characterController, true);
        yield return new WaitForSeconds(0.75f);
        if (col != null && characterController != null) Physics.IgnoreCollision(col, characterController, false);
    }

    /// <summary>Name of the item in the player's hand ("" = empty hand) - shown in the journal.</summary>
    public string GetHeldItemDisplayName() => currentItem != null ? GameProgress.DisplayNameOf(currentItem) : "";

    public bool HasItem()
    {
        return currentItem != null;
    }

    /// <summary>Exact (case-insensitive) match on the KeyItem's keyName, or on the object name for items without a KeyItem.</summary>
    public bool HasItem(string itemName)
    {
        if (currentItem == null || string.IsNullOrEmpty(itemName)) return false;

        KeyItem key = currentItem.GetComponent<KeyItem>();
        if (key != null)
        {
            return string.Equals(key.GetKeyName().Trim(), itemName.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(StripCloneSuffix(currentItem.name), StripCloneSuffix(itemName), System.StringComparison.OrdinalIgnoreCase);
    }

    // "Fuse (1)" -> "Fuse"
    private static string StripCloneSuffix(string name)
    {
        name = name.Trim();
        if (name.EndsWith(")"))
        {
            int open = name.LastIndexOf(" (");
            if (open > 0) name = name.Substring(0, open);
        }
        return name.Trim();
    }

    /// <param name="respawnOnReload">True for puzzle items (puzzles reset on death, so the item must come back). False for door keys (the door stays unlocked).</param>
    public void RemoveItem(string itemName, bool respawnOnReload = true)
    {
        if (!HasItem(itemName)) return;

        GameObject item = currentItem;
        currentItem = null;
        disabledColliders.Clear();
        if (grip != null) grip.Release(null);

        if (SaveManager.instance != null)
        {
            SaveManager.instance.OnItemConsumed(item, respawnOnReload);
        }

        Destroy(item);
        Debug.Log("Removed and destroyed item: " + itemName);
    }

    /// <summary>Message shown after the respawn ("the key is back where you found it").</summary>
    public static string ReturnedNote;

    /// <summary>
    /// The player died holding something: instead of lying wherever he fell (hard to find in the dark), it goes back to
    /// the exact place where it was first found. Falls back to a normal drop if that place is unknown.
    /// </summary>
    public void ReturnHeldItemHome()
    {
        if (currentItem == null) return;
        GameObject item = currentItem;
        SaveManager sm = SaveManager.instance;
        string id = sm != null ? sm.GetItemId(item) : "";
        if (sm == null || !sm.TryGetItemHome(id, out Vector3 home)) { DropCurrentItem(); return; }

        currentItem = null;
        item.transform.SetParent(null, true);
        if (grip != null) grip.Release(item);
        item.transform.position = home;
        foreach (Collider col in disabledColliders) if (col != null) col.enabled = true;
        disabledColliders.Clear();
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        sm.OnItemDropped(item);   // saved as lying at its first spot - the reload puts it there
        ReturnedNote = "The " + GameProgress.DisplayNameOf(item) + " is back where you found it.";
    }

    public GameObject GetCurrentItem()
    {
        return currentItem;
    }
}
