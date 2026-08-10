using UnityEngine;
using System.Collections;

public class ElevatorController : MonoBehaviour, IInteractable
{
    [Header("Elevator Settings")]
    [SerializeField] private float[] floorHeights = new float[] { 0f, 5f }; // Define the Y heights for each floor
    [SerializeField] private float moveSpeed = 2.0f;
    [SerializeField] private float waitTimeAtFloor = 2.0f;
    
    [Header("Elevator Doors (Optional)")]
    [SerializeField] private SlidingDoorController[] doors;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip movingSound;
    [SerializeField] private AudioClip arrivalSound;

    private int currentFloorIndex = 0;
    private bool isMoving = false;
    private GameObject playerInside;

    void Start()
    {
        // Snap to the first floor position on start
        if (floorHeights.Length > 0)
        {
            Vector3 pos = transform.position;
            pos.y = floorHeights[currentFloorIndex];
            transform.position = pos;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }
        }
    }

    public string GetInteractPrompt()
    {
        if (isMoving) return "Elevator is moving...";
        
        int nextFloor = (currentFloorIndex + 1) % floorHeights.Length;
        return "Press E to go to Floor " + nextFloor;
    }

    public void Interact(PlayerStats playerStats)
    {
        if (!isMoving && floorHeights.Length > 1)
        {
            int nextFloor = (currentFloorIndex + 1) % floorHeights.Length;
            StartCoroutine(MoveToFloor(nextFloor));
        }
    }

    private IEnumerator MoveToFloor(int targetFloorIndex)
    {
        isMoving = true;

        // 1. Close doors before moving
        SetDoorsState(false);
        yield return new WaitForSeconds(1.5f); // Wait for doors to close

        // 2. Play moving sound
        if (audioSource != null && movingSound != null)
        {
            audioSource.clip = movingSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // 3. Move the elevator
        float targetY = floorHeights[targetFloorIndex];
        
        while (Mathf.Abs(transform.position.y - targetY) > 0.05f)
        {
            Vector3 newPos = transform.position;
            newPos.y = Mathf.MoveTowards(transform.position.y, targetY, moveSpeed * Time.deltaTime);
            transform.position = newPos;
            yield return null;
        }

        // Snap to exact target height
        Vector3 finalPos = transform.position;
        finalPos.y = targetY;
        transform.position = finalPos;
        currentFloorIndex = targetFloorIndex;

        // 4. Stop moving sound, play arrival sound
        if (audioSource != null)
        {
            audioSource.Stop();
            if (arrivalSound != null)
            {
                audioSource.PlayOneShot(arrivalSound);
            }
        }

        // 5. Open doors at the new floor
        SetDoorsState(true);

        isMoving = false;
    }

    private void SetDoorsState(bool open)
    {
        if (doors != null)
        {
            foreach (var door in doors)
            {
                if (door != null)
                {
                    door.SetOpenState(open);
                }
            }
        }
    }

    // --- Player Parenting Logic to keep them from falling through the moving floor ---

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the player (assuming player has a CharacterController or Tag "Player")
        if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
        {
            playerInside = other.gameObject;
            playerInside.transform.SetParent(this.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == playerInside)
        {
            playerInside.transform.SetParent(null);
            playerInside = null;
        }
    }
}
