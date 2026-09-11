using UnityEngine;
using System.Collections;

[RequireComponent(typeof(BoxCollider))]
public class SimpleClassroomIllusion : MonoBehaviour, IInteractable
{
    [Header("Environments")]
    public GameObject normalEnvironment; // Jin cheezon ko andhere mein ghaib karna hai
    public GameObject nightmareEnvironment; // Jin bhootia cheezon ko andhere mein dikhana hai
    public GameObject realDoor; // Asal darwaza
    public GameObject fakeWall; // Nakli deewar jo darwaze ki jagah aayegi
    
    [Header("Lights & Audio")]
    public Light[] classroomLights;
    public AudioSource audioSource;
    public AudioClip radioStartSound; // Nayi sound (Radio on hone ki aawaz)
    public float radioPlayTime = 4f; // Kitni dair radio chalay ga andhera hone se pehle
    public AudioClip lightsOutSound;
    
    [Header("The Scare")]
    public GameObject scareGhost; // Jo bhoot chabi le kar khara hoga
    public GameObject officeKey;

    private bool hasTriggered = false;
    private bool keyAssigned = false;
    private Collider keyCollider;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (officeKey != null)
        {
            keyAssigned = true;
            keyCollider = officeKey.GetComponent<Collider>();
            officeKey.SetActive(false);
        }

        if (nightmareEnvironment != null) nightmareEnvironment.SetActive(false);
        if (fakeWall != null) fakeWall.SetActive(false);
        if (scareGhost != null) scareGhost.SetActive(false);
    }

    void Update()
    {
        if (!hasTriggered) return;

        bool isKeyPickedUp = false;

        // Jab player chabi utha leta hai, to inventory system uska collider band kar deta hai!
        if (keyAssigned)
        {
            if (officeKey == null) 
            {
                isKeyPickedUp = true; // Agar chabi delete ho gayi
            }
            else if (keyCollider != null && !keyCollider.enabled)
            {
                isKeyPickedUp = true; // Agar player ne utha li (Inventory ne collider band kar diya)
            }
        }

        // Phir hum fake wall ko hata kar darwaza wapas le ayenge!
        if (isKeyPickedUp && fakeWall != null && fakeWall.activeSelf)
        {
            // Darwaza theek karo
            fakeWall.SetActive(false);
            if (realDoor != null) realDoor.SetActive(true);
            
            // Kursiyan aur table wapas apni theek jagah par le aao!
            if (nightmareEnvironment != null) nightmareEnvironment.SetActive(false);
            if (normalEnvironment != null) normalEnvironment.SetActive(true);
            
            // Lights wapas on kar dein
            foreach (Light l in classroomLights)
            {
                if (l != null) l.enabled = true;
            }
        }
    }

    public string GetInteractPrompt()
    {
        if (hasTriggered) return "";
        return "Press E to Turn On Radio";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (hasTriggered) return;
        hasTriggered = true;
        
        StartCoroutine(NightmareSequence());
    }

    private IEnumerator NightmareSequence()
    {
        // 1. Pehle Radio Chalayen (Build Tension)
        if (audioSource != null && radioStartSound != null)
        {
            audioSource.clip = radioStartSound;
            audioSource.Play();
            yield return new WaitForSeconds(radioPlayTime); // Kuch seconds radio chalne dein
            audioSource.Stop(); // Achanak radio band!
        }

        // 2. Andhera (Lights Out)
        if (audioSource != null && lightsOutSound != null)
            audioSource.PlayOneShot(lightsOutSound);

        foreach (Light l in classroomLights)
        {
            if (l != null) l.enabled = false;
        }

        // 2. Kamra badal do (Swap Environments)
        if (normalEnvironment != null) normalEnvironment.SetActive(false);
        if (nightmareEnvironment != null) nightmareEnvironment.SetActive(true);
        
        // Darwaza ghaib karo aur Fake Wall laga do taake player qaid ho jaye
        if (realDoor != null) realDoor.SetActive(false);
        if (fakeWall != null) fakeWall.SetActive(true);

        // 3. Player 3.5 seconds tak andhere mein darrta rahega
        yield return new WaitForSeconds(3.5f); 

        // 4. Bhoot (Angel) samne aa jayega!
        // Chabi abhi nahi aayegi (wo Angel ko chhune ke baad aayegi)
        // Aur Fake Wall (Almariyan) abhi bhi darwaze par rahengi taake player bhag na sake!
        if (scareGhost != null) scareGhost.SetActive(true);
        
        // Illusion yahan aakar ruk jayega. Baki kaam Angel aur Key script karengi!
    }
}
