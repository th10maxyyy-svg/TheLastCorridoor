using UnityEngine;
using System.Collections;

public class GrandfatherClockPuzzle : MonoBehaviour, IInteractable
{
    [Header("Requirements (Item Names)")]
    public string pendulumName = "Pendulum";

    [Header("Time Setting")]
    public int targetHour = 3;
    public int targetMinute = 15;
    private int currentHour = 12;
    private int currentMinute = 0;

    [Header("Visuals & Audio")]
    public Transform hourHandPivot;
    public Transform minuteHandPivot;
    public GameObject pendulumVisual;
    public GameObject clockHandsVisual;
    public AudioClip tickSound;
    public AudioClip chimeSound;
    public AudioClip backwardsCreepySound;

    [Header("Paranormal Event")]
    public Light[] roomLights; // Kamre ki lights assign karein (Laal hone ke liye)
    public EnemyAI stalker;
    public Transform officeDoor; // Stalker yahan aaye ga
    public GameObject stockRoomKey;
    public GameObject glassPane; // Ghari ka sheesha jo tootega

    private bool hasPendulum = false;
    private bool isSolved = false;
    /// <summary>Read by the objective / achievement system (GameProgress).</summary>
    public bool IsSolved => isSolved;
    private bool isShifting = false;

    void Start()
    {
        if (stockRoomKey != null) stockRoomKey.SetActive(false);
        if (pendulumVisual != null) pendulumVisual.SetActive(false);
        if (clockHandsVisual != null) clockHandsVisual.SetActive(false);
        UpdateVisualHands();
    }

    public string GetInteractPrompt()
    {
        if (isSolved || isShifting) return "";

        if (!hasPendulum)
        {
            return "Missing parts: " + pendulumName + " (Press E to insert)";
        }
        else
        {
            return "Press E to advance time. Current: " + currentHour.ToString("00") + ":" + currentMinute.ToString("00");
        }
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isSolved || isShifting) return;

        Inventory inventory = playerStats.GetComponent<Inventory>();
        if (inventory == null) return;

        // 1. Missing parts lagayen
        if (!hasPendulum)
        {
            if (inventory.HasItem(pendulumName))
            {
                hasPendulum = true;
                inventory.RemoveItem(pendulumName);
                if (pendulumVisual != null) pendulumVisual.SetActive(true);
            }
            return;
        }

        // 2. Time change karo (15 minute aagay)
        currentMinute += 15;
        if (currentMinute >= 60)
        {
            currentMinute = 0;
            currentHour++;
            if (currentHour > 12) currentHour = 1;
        }

        UpdateVisualHands();

        if (tickSound != null) AudioSource.PlayClipAtPoint(tickSound, transform.position);

        // 3. Check karo ke time theek set ho gaya hai?
        if (currentHour == targetHour && currentMinute == targetMinute)
        {
            StartCoroutine(ParanormalShiftSequence());
        }
    }

    private void UpdateVisualHands()
    {
        if (hourHandPivot != null)
        {
            float hourAngle = (currentHour % 12) * 30f + (currentMinute / 60f) * 30f;
            hourHandPivot.localEulerAngles = new Vector3(0, 0, hourAngle); // Adjust rotation axis in inspector if needed
        }
        if (minuteHandPivot != null)
        {
            float minAngle = currentMinute * 6f;
            minuteHandPivot.localEulerAngles = new Vector3(0, 0, minAngle);
        }
    }

    private IEnumerator ParanormalShiftSequence()
    {
        isShifting = true;
        
        // 1. Loud Bong
        if (chimeSound != null) AudioSource.PlayClipAtPoint(chimeSound, transform.position, 1f);
        
        yield return new WaitForSeconds(1.5f);

        // 2. Red Lights & Creepy Sound
        if (backwardsCreepySound != null)
        {
            AudioSource.PlayClipAtPoint(backwardsCreepySound, transform.position, 1f);
        }

        Color originalColor = Color.white;
        if (roomLights != null && roomLights.Length > 0 && roomLights[0] != null) originalColor = roomLights[0].color;

        if (roomLights != null)
        foreach(Light l in roomLights)
        {
            if (l != null) l.color = Color.red;
        }

        // 3. Alert Stalker to the door
        if (stalker != null && officeDoor != null)
        {
            stalker.ForceInvestigate(officeDoor.position, 15f);
        }

        // 4. Hands spin wildly backwards
        float spinTimer = 0f;
        while(spinTimer < 5f)
        {
            spinTimer += Time.deltaTime;
            if (minuteHandPivot != null) minuteHandPivot.Rotate(0, 0, -1000f * Time.deltaTime);
            if (hourHandPivot != null) hourHandPivot.Rotate(0, 0, -200f * Time.deltaTime);
            yield return null;
        }

        // Wait for stalker to leave (approx 10 more seconds of hiding)
        yield return new WaitForSeconds(10f);

        // 5. Restore Room
        if (roomLights != null)
        foreach(Light l in roomLights)
        {
            if (l != null) l.color = originalColor;
        }

        // 6. Glass breaks, key spawns
        if (glassPane != null) glassPane.SetActive(false); 
        
        if (stockRoomKey != null)
        {
            stockRoomKey.transform.position = transform.position + transform.forward * 0.5f;
            stockRoomKey.SetActive(true);
        }
        
        if (tickSound != null) AudioSource.PlayClipAtPoint(tickSound, transform.position); 

        isSolved = true;
        isShifting = false;
    }
}
