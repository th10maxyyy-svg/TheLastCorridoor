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
    private string PendulumFlag => "ClockPendulum_" + SaveManager.GetSceneObjectId(transform);
    private string DoneFlag => "ClockDone_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(DoneFlag))
        {
            hasPendulum = true;
            isSolved = true;
            currentHour = targetHour;
            currentMinute = targetMinute;
            if (pendulumVisual != null) pendulumVisual.SetActive(true);
            if (glassPane != null) glassPane.SetActive(false);
            if (stockRoomKey != null)
            {
                PlaceReward();
            }
            UpdateVisualHands();
            return;
        }

        if (stockRoomKey != null) stockRoomKey.SetActive(false);
        if (SaveManager.instance != null && SaveManager.instance.HasFlag(PendulumFlag))
        {
            hasPendulum = true;
            if (pendulumVisual != null) pendulumVisual.SetActive(true);
        }
        else
        {
            if (pendulumVisual != null) pendulumVisual.SetActive(false);
        }
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
                if (SaveManager.instance != null) SaveManager.instance.SetFlag(PendulumFlag);
                inventory.RemoveItem(pendulumName, false);
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
            PlaceReward();
        }
        
        if (tickSound != null) AudioSource.PlayClipAtPoint(tickSound, transform.position); 

        isSolved = true;
        isShifting = false;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(DoneFlag);
    }

    /// <summary>The reward (the stock room key) lies on the floor in front of the clock, where the glass fell.</summary>
    private void PlaceReward()
    {
        if (stockRoomKey == null) return;
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 p = transform.position + fwd.normalized * 0.5f;
        stockRoomKey.transform.SetParent(transform.parent, true);
        stockRoomKey.SetActive(true);
        stockRoomKey.transform.position = p + Vector3.up * 1.5f;
        Physics.SyncTransforms();
        if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(stockRoomKey.transform))
        {
            Bounds b = new Bounds(stockRoomKey.transform.position, Vector3.zero); bool any = false;
            foreach (Renderer r in stockRoomKey.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            stockRoomKey.transform.position += Vector3.up * (hit.point.y + 0.004f - (any ? b.min.y : stockRoomKey.transform.position.y));
        }
        else stockRoomKey.transform.position = p;
    }
}
