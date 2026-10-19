using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SmashableJar : MonoBehaviour, IInteractable
{
    [Header("Jar Settings")]
    [Tooltip("The item that will spawn when the jar breaks (e.g. Clock Hands)")]
    public GameObject itemInside;
    [Tooltip("Optional: Shards to spawn when it breaks")]
    public GameObject brokenJarPrefab;
    public AudioClip smashSound;
    public float breakForceThreshold = 1.5f; // Kitni zoor se girne par tootega

    private bool isBroken = false;

    void Start()
    {
        if (itemInside != null) itemInside.SetActive(false);
    }

    public string GetInteractPrompt()
    {
        return "Press E to Pick Up Jar (Drop to Smash)";
    }

    public void Interact(PlayerStats playerStats)
    {
        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory != null)
        {
            // Pick it up using the existing inventory system
            inventory.PickUpItem(this.gameObject);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Jab jar inventory se drop ho kar zameen se takrayega to uski speed force hogi
        if (!isBroken && collision.relativeVelocity.magnitude >= breakForceThreshold)
        {
            BreakJar();
        }
    }

    private void BreakJar()
    {
        isBroken = true;

        // 1. Aawaz
        if (smashSound != null)
        {
            AudioSource.PlayClipAtPoint(smashSound, transform.position, 1f);
        }

        // 2. Andar wali cheez spawn karo
        if (itemInside != null)
        {
            itemInside.transform.position = transform.position;
            itemInside.SetActive(true);
            itemInside.transform.SetParent(null); // Jar se alag kar do

            // Thora sa uchalo taake realistic lagay
            Rigidbody rb = itemInside.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);

            // WORLD STATE: the item now lies on the floor on its own (otherwise it would vanish with the jar on reload)
            if (SaveManager.instance != null) SaveManager.instance.OnItemReleased(itemInside);
        }

        // 3. Shards (Toota hua shisha)
        if (brokenJarPrefab != null)
        {
            Instantiate(brokenJarPrefab, transform.position, transform.rotation);
        }

        // 4. Agar player ke haath mein hi toot gaya kisi waja se to haath se gira do
        Inventory inv = FindAnyObjectByType<Inventory>();
        if (inv != null && inv.GetCurrentItem() == this.gameObject)
        {
            inv.DropCurrentItem();
        }

        // 5. Jar khatam (stays "collected" so it doesn't come back on reload)
        if (SaveManager.instance != null) SaveManager.instance.OnItemDestroyed(gameObject);
        Destroy(gameObject);
    }
}
