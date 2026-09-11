using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ScreamAndVanish : MonoBehaviour, IInteractable
{
    [Header("Sounds")]
    public AudioClip weepingSound; // Rone ki aawaz (Loop)
    public AudioClip screamSound;  // Chhune par cheekh ki aawaz
    
    [Header("Key")]
    public GameObject keyToReveal; // Zameen par mojood chabi
    
    private bool interacted = false;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (keyToReveal != null) keyToReveal.SetActive(false); // Shuru mein chabi chupa do

        // Jaise hi Angel show hogi, rona shuru kar degi!
        if (weepingSound != null)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = weepingSound;
            source.loop = true;
            source.spatialBlend = 1f; // 3D sound taake kareeb jane par awaz teez ho
            source.Play();
        }
    }

    public string GetInteractPrompt()
    {
        return interacted ? "" : "Press E to Touch";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (interacted) return;
        interacted = true;

        if (screamSound != null)
        {
            // Naya object bana kar sound chalayen taake hum usay 5 second baad maar sakein!
            GameObject tempAudioObj = new GameObject("TempScreamAudio");
            tempAudioObj.transform.position = transform.position;
            
            AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
            tempSource.clip = screamSound;
            tempSource.spatialBlend = 0f; // 2D (Har taraf barabar aawaz aaye)
            tempSource.volume = 1f;
            tempSource.Play();
            
            // Theek 5 seconds baad cheekh ki aawaz cut!
            Destroy(tempAudioObj, 5f);
        }

        if (keyToReveal != null)
        {
            keyToReveal.SetActive(true); // Farsh wali chabi show kar do
        }

        // Angel, khoon, aur rone ki aawaz sab ek dum Delete!
        Destroy(gameObject);
    }
}
