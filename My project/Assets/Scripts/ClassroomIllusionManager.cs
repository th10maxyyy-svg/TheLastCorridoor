using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class ClassroomIllusionManager : MonoBehaviour
{
    [Header("Environments")]
    public GameObject normalEnvironment; 
    public GameObject nightmareEnvironment; 
    public GameObject realDoor; 
    public GameObject fakeWall; 

    [Header("Lights & Audio")]
    public Light[] classroomLights;
    public AudioSource audioSource;
    public AudioClip lightsOutSound;
    public AudioClip doorSlamSound;

    [Header("The Climax")]
    public GameObject scareStatue; 
    public GameObject officeKey; 
    
    [Header("Events")]
    public UnityEvent onIllusionComplete;

    private bool isIllusionStarted = false;
    private bool isPlayerTrapped = false;
    private bool isCheckingForKey = false;

    void Start()
    {
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        
        if (nightmareEnvironment != null) nightmareEnvironment.SetActive(false);
        if (fakeWall != null) fakeWall.SetActive(false);
        if (scareStatue != null) scareStatue.SetActive(false);
        if (officeKey != null) officeKey.SetActive(false);
    }

    void Update()
    {
        // Jab key zameen par aa jaye aur player usay utha le (destroy ho jaye) to illusion khatam
        if (isCheckingForKey && officeKey == null)
        {
            isCheckingForKey = false;
            EndIllusion();
        }
    }

    // Called by TeaseTrigger when entering the room
    public void TrapPlayer()
    {
        if (isPlayerTrapped) return;
        isPlayerTrapped = true;

        if (realDoor != null) realDoor.SetActive(false);
        if (fakeWall != null) fakeWall.SetActive(true);
        
        if (audioSource != null && doorSlamSound != null)
            audioSource.PlayOneShot(doorSlamSound);
    }

    // Called by IllusionTriggerObject (Apple/Radio)
    public void StartNightmare()
    {
        if (isIllusionStarted) return;
        isIllusionStarted = true;

        StartCoroutine(NightmareSequence());
    }

    private IEnumerator NightmareSequence()
    {
        // 1. Andhera (Lights Out)
        if (audioSource != null && lightsOutSound != null)
            audioSource.PlayOneShot(lightsOutSound);

        foreach (Light l in classroomLights)
        {
            if (l != null) l.enabled = false;
        }

        // 2. Kamra Badal Do (Swap Environments in the dark)
        if (normalEnvironment != null) normalEnvironment.SetActive(false);
        if (nightmareEnvironment != null) nightmareEnvironment.SetActive(true);

        yield return new WaitForSeconds(3.5f); // Let them panic in the dark

        // 3. Statue aur Chabi peechay spawn karo
        if (fakeWall != null) fakeWall.SetActive(false);
        if (realDoor != null) realDoor.SetActive(true); // Darwaza wapas aa gaya
        
        if (scareStatue != null) scareStatue.SetActive(true);
        if (officeKey != null) officeKey.SetActive(true);
        
        isCheckingForKey = true;
    }

    private void EndIllusion()
    {
        if (scareStatue != null) scareStatue.SetActive(false); // Statue bhag gaya
        
        foreach (Light l in classroomLights)
        {
            if (l != null) l.enabled = true; // Lights wapas
        }

        if (onIllusionComplete != null) onIllusionComplete.Invoke(); 
    }
}
