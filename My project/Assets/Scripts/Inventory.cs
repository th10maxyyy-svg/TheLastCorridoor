using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private Transform holdPosition; // Assign a 'Hand' empty object under Main Camera in Unity
    [SerializeField] private float throwForce = 5f;

    private GameObject currentItem;

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
        
        // Disable physics while holding
        Rigidbody rb = currentItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Parent to hand and reset position
        currentItem.transform.SetParent(holdPosition);
        currentItem.transform.localPosition = Vector3.zero;
        currentItem.transform.localRotation = Quaternion.identity;

        Debug.Log("Picked up " + item.name);
    }

    public void DropCurrentItem()
    {
        if (currentItem == null) return;

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
