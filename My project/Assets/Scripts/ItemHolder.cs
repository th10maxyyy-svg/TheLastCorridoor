using UnityEngine;

public class ItemHolder : MonoBehaviour
{
    [Header("Holder Settings")]
    [Tooltip("Ye object Main Camera ke child ke tor par hona chahiye")]
    public Transform holdPosition;
    
    private GameObject currentItem;

    void Start()
    {
        // GUARANTEED FIX: Agar RightHand bone ka naam alag hai to hum seedha 'hold position' ko
        // camera ke aagay Right Hand ke oopar move kar dete hain taake har haal mein item hath mein aye.
        if (holdPosition != null && holdPosition.parent != null)
        {
            // Right Hand ki exact jagah (Camera ke mutabiq)
            holdPosition.localPosition = new Vector3(0.45f, -0.3f, 0.6f); 
        }
    }

    public void HoldItem(GameObject itemPrefab)
    {
        // Agar pehle se kuch pakra hua hai to usay hata do
        if (currentItem != null)
        {
            Destroy(currentItem);
        }

        if (itemPrefab != null && holdPosition != null)
        {
            // Nayi cheez ko hold position par paida karo
            currentItem = Instantiate(itemPrefab, holdPosition.position, holdPosition.rotation);
            currentItem.transform.SetParent(holdPosition);
            
            // Uske saare colliders aur physics band kar do taake wo hawa mein tikey
            Rigidbody rb = currentItem.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider[] colliders = currentItem.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }
        }
    }

    public void DropItem()
    {
        if (currentItem != null)
        {
            Destroy(currentItem);
        }
    }
}
