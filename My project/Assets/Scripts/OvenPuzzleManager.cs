using UnityEngine;
using UnityEngine.Events;

public class OvenPuzzleManager : MonoBehaviour
{
    [Header("Oven Dials")]
    public OvenDial oven1;
    public OvenDial oven2;
    public OvenDial oven3;

    [Header("Target Recipe (Clues)")]
    public int targetTemp1 = 400;
    public int targetTemp2 = 200;
    public int targetTemp3 = 500;

    [Header("Climax Settings")]
    public GameObject charredCorpsePrefab;
    public Transform corpseSpawnPoint;
    public GameObject classroomKey;
    public AudioSource audioSource;
    public AudioClip ovenBurstSound;
    public ParticleSystem fireBurstParticle;

    [Header("Events")]
    public UnityEvent onPuzzleSolved;

    private bool isSolved = false;
    /// <summary>Read by the objective / achievement system (GameProgress).</summary>
    public bool IsSolved => isSolved;
    private string SaveFlag => "OvenDone_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isSolved = true;
            if (oven1 != null) oven1.LockDial();
            if (oven2 != null) oven2.LockDial();
            if (oven3 != null) oven3.LockDial();
            if (classroomKey != null) classroomKey.SetActive(true);
            return;
        }
        
        if (classroomKey != null) classroomKey.SetActive(false); // Hide the key initially
    }

    public void CheckPuzzle()
    {
        if (isSolved) return;

        if (oven1.GetCurrentTemp() == targetTemp1 &&
            oven2.GetCurrentTemp() == targetTemp2 &&
            oven3.GetCurrentTemp() == targetTemp3)
        {
            SolvePuzzle();
        }
    }

    private void SolvePuzzle()
    {
        isSolved = true;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);
        Debug.Log("Oven Puzzle Solved! Burn them!");

        // Lock all dials so player can't mess with them anymore
        oven1.LockDial();
        oven2.LockDial();
        oven3.LockDial();

        // Play burst effects
        if (ovenBurstSound != null) audioSource.PlayOneShot(ovenBurstSound);
        if (fireBurstParticle != null) fireBurstParticle.Play();

        // Spawn the charred corpse
        if (charredCorpsePrefab != null && corpseSpawnPoint != null)
        {
            Instantiate(charredCorpsePrefab, corpseSpawnPoint.position, corpseSpawnPoint.rotation);
        }

        // Show the Classroom Key inside or near the oven
        if (classroomKey != null)
        {
            classroomKey.SetActive(true);
        }

        // Trigger any cinematic events (like Ambush or Lights flickering)
        if (onPuzzleSolved != null)
        {
            onPuzzleSolved.Invoke();
        }
    }
}
