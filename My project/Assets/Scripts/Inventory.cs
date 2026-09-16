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

    void Start()
    {
        if (inventoryAudioSource == null)
        {
            inventoryAudioSource = gameObject.AddComponent<AudioSource>();
            inventoryAudioSource.spatialBlend = 0f; // 2D sound for player UI
        }
        // FINAL GRIP FIX: Hum cheez ko hamesha ke liye haath ki haddi (bone) ke sath jor rahe hain!
        // Is se cheez hamesha haath mein rahegi. Agar aap oopar dekhenge, to cheez aur haath dono neechay rahenge.
        Animator anim = transform.root.GetComponentInChildren<Animator>();
        Transform rHand = null;
        
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

    void Update()
    {
        // Drop item on pressing G
        if (Input.GetKeyDown(KeyCode.G) && currentItem != null)
        {
            DropCurrentItem();
        }
    }

    public void PickUpItem(GameObject item)
    {
        // If already holding an item, drop it first
        if (currentItem != null)
        {
            DropCurrentItem();
        }

        currentItem = item;
        
        // Play pickup sound
        if (inventoryAudioSource != null && pickupSound != null)
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
        Collider[] colliders = currentItem.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Attach to hold position
        if (holdPosition != null)
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

        // WORLD STATE CHECK: Jab koi cheez uthai jaye, to usay save kar lo taake reload par wahan wapas na aaye!
        if (SaveManager.instance != null)
        {
            SaveManager.instance.CollectItem(item.name);
        }

        Debug.Log("Picked up " + item.name);
    }

    public void DropCurrentItem()
    {
        if (currentItem == null) return;

        // Play drop sound
        if (inventoryAudioSource != null && dropSound != null)
        {
            inventoryAudioSource.PlayOneShot(dropSound);
        }

        currentItem.transform.SetParent(null);
        
        Rigidbody rb = currentItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // Throw it slightly forward
            rb.AddForce(transform.forward * throwForce, ForceMode.Impulse);
        }

        ThrowableItem throwable = currentItem.GetComponent<ThrowableItem>();
        if (throwable != null)
        {
            throwable.MarkAsDropped();
        }

        Debug.Log("Dropped " + currentItem.name);
        currentItem = null;
    }

    public bool HasItem()
    {
        return currentItem != null;
    }

    public bool HasItem(string itemName)
    {
        if (currentItem != null)
        {
            KeyItem key = currentItem.GetComponent<KeyItem>();
            if (key != null && key.GetKeyName() == itemName)
            {
                return true;
            }
            return currentItem.name.Contains(itemName);
        }
        return false;
    }

    public void RemoveItem(string itemName)
    {
        if (HasItem(itemName) && currentItem != null)
        {
            Destroy(currentItem);
            currentItem = null;
            Debug.Log("Removed and destroyed item: " + itemName);
        }
    }

    public GameObject GetCurrentItem()
    {
        return currentItem;
    }

    public System.Collections.Generic.List<string> GetAllItems()
    {
        System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
        if (currentItem != null)
        {
            KeyItem key = currentItem.GetComponent<KeyItem>();
            list.Add(key != null ? key.GetKeyName() : currentItem.name);
        }
        return list;
    }

    public void LoadInventory(System.Collections.Generic.List<string> savedItems)
    {
        // TODO: Implement physical item spawning from a prefab manager based on saved string names
        if (savedItems != null && savedItems.Count > 0)
        {
            Debug.LogWarning("Inventory Load requested for: " + savedItems[0] + ". Physical spawning system needs prefab references to restore the item.");
        }
    }
}
