using UnityEngine;
using System.Collections;

public class TaserGun : MonoBehaviour, IInteractable
{
    [Header("Taser Settings")]
    [SerializeField] private float range = 15f;
    [SerializeField] private float stunDuration = 10f;
    [SerializeField] private Vector3 equippedPosition = new Vector3(0.5f, -0.4f, 1f); // Bottom right of camera
    [SerializeField] private Vector3 equippedRotation = new Vector3(0, -90, 0); // Adjust based on your gun model

    [Header("Effects")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip zapSound;
    [SerializeField] private AudioClip emptyClickSound;
    [SerializeField] private ParticleSystem sparkVFX; // Optional

    private bool isEquipped = false;
    private bool hasFired = false;
    private AudioSource audioSource;
    private Rigidbody rb;
    private Collider col;
    private PlayerController owner;
    private string saveId;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // WORLD STATE: respawn ke baad taser haath mein hi rahe (aur agar shot chal chuka hai to khali hi rahe)
        saveId = "Taser_" + SaveManager.GetSceneObjectId(transform);
        if (SaveManager.instance != null)
        {
            hasFired = SaveManager.instance.HasFlag(saveId + "_Fired");
            if (SaveManager.instance.HasFlag(saveId + "_Equipped")) EquipTaser(true);
        }
    }

    public string GetInteractPrompt()
    {
        if (!isEquipped)
            return "Press E to pick up Taser (1 Shot Only)";
        return "";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (isEquipped) return; // Already picked up

        EquipTaser(false);
    }

    private void EquipTaser(bool silent)
    {
        // Find the main camera to attach the gun to
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        isEquipped = true;
        owner = mainCam.GetComponentInParent<PlayerController>();
        transform.SetParent(mainCam.transform);

        // Disable physics so it doesn't fall out of hand
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        if (col != null) col.enabled = false;

        // Move to hand position
        transform.localPosition = equippedPosition;
        transform.localEulerAngles = equippedRotation;

        if (!silent && pickupSound != null) audioSource.PlayOneShot(pickupSound);
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveId + "_Equipped");
    }

    void Update()
    {
        // Only allow shooting if equipped, and NOT while a UI (keypad) has the mouse, or during cinematics/death
        if (isEquipped && Input.GetMouseButtonDown(0) && CanFire())
        {
            ShootTaser();
        }
    }

    private bool CanFire()
    {
        if (KeypadController.IsAnyKeypadOpen || Time.timeScale <= 0f) return false;
        if (owner == null) return true;
        return owner.enabled && !owner.isDead && !owner.isCinematicPlaying && !owner.isHidden;
    }

    private void ShootTaser()
    {
        if (hasFired)
        {
            // Already fired, play empty click sound
            if (emptyClickSound != null) audioSource.PlayOneShot(emptyClickSound);
            return;
        }

        hasFired = true; // Use up the 1 shot!
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(saveId + "_Fired");

        if (zapSound != null) audioSource.PlayOneShot(zapSound);
        if (sparkVFX != null) sparkVFX.Play();

        // Shoot raycast from center of screen
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, range))
            {
                // Check if we hit the enemy
                EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    Debug.Log("Taser hit Enemy! Stunned for " + stunDuration + " seconds.");
                    enemy.TakeStun(stunDuration);
                }
                else
                {
                    Debug.Log("Taser missed the enemy. Hit: " + hit.collider.name);
                }
            }
        }
    }
}
