using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class CrankGeneratorPuzzle : MonoBehaviour, IInteractable
{
    [Header("Generator Settings")]
    [Tooltip("How many times the player needs to press E to fully charge it.")]
    public int clicksRequired = 12;
    private int currentCharge = 0;
    private bool isPowered = false;
    private bool enemyAlerted = false;

    [Header("X-Ray Board Settings")]
    public Light xrayLight;
    public MeshRenderer xrayBoardRenderer;
    [Tooltip("The material that shows the numbers 3-7-9")]
    public Material xrayMaterialOn;

    [Header("Audio")]
    public AudioClip crankingSound;
    public AudioClip powerOnSound;
    private AudioSource audioSource;

    [Header("Enemy Attraction")]
    public EnemyAI enemyAI;
    [Tooltip("How long the enemy will search the room")]
    public float enemySearchTime = 15f;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Ensure X-ray is off initially
        if (xrayLight != null) xrayLight.enabled = false;
    }

    public string GetInteractPrompt()
    {
        if (isPowered) return "Generator is running.";
        if (enemyAlerted) return $"Press E rapidly to charge! ({currentCharge}/{clicksRequired})";
        return "Press E to attach Crank and Charge";
    }

    public void Interact(PlayerStats player)
    {
        if (isPowered) return;

        Inventory inventory = player.GetComponent<Inventory>();
        InteractionRaycast raycast = player.GetComponentInChildren<InteractionRaycast>();

        if (inventory != null && inventory.HasItem("Rusty Crank"))
        {
            // First time they click with the crank
            if (!enemyAlerted)
            {
                enemyAlerted = true;
                
                // Alert the enemy IMMEDIATELY! High tension starts.
                if (enemyAI != null)
                {
                    enemyAI.ForceInvestigate(transform.position, enemySearchTime);
                }
                
                if (raycast != null) 
                {
                    raycast.ShowNotification("It's so loud! He's coming! Keep cranking!", 4f);
                }
            }

            // Charge the generator
            currentCharge++;

            // Play cranking sound and increase pitch slightly to show progress
            if (crankingSound != null)
            {
                audioSource.pitch = 0.8f + (currentCharge * 0.05f); // Pitch goes up as it charges
                audioSource.PlayOneShot(crankingSound);
            }

            // Check if fully charged
            if (currentCharge >= clicksRequired)
            {
                PowerOn(raycast);
            }
        }
        else
        {
            if (raycast != null)
            {
                raycast.ShowNotification("The generator is missing a manual crank...", 3f);
            }
        }
    }

    private void PowerOn(InteractionRaycast raycast)
    {
        isPowered = true;
        
        // Reset pitch and play power on sound
        audioSource.pitch = 1f;
        if (powerOnSound != null)
        {
            audioSource.PlayOneShot(powerOnSound);
        }

        // Turn on X-Ray Light
        if (xrayLight != null)
        {
            xrayLight.enabled = true;
        }

        // Change the X-Ray material to show the code (3-7-9)
        if (xrayBoardRenderer != null && xrayMaterialOn != null)
        {
            // Note: Make sure the X-Ray board uses Material index 0, or change this accordingly.
            Material[] mats = xrayBoardRenderer.materials;
            mats[0] = xrayMaterialOn;
            xrayBoardRenderer.materials = mats;
        }

        if (raycast != null)
        {
            raycast.ShowNotification("Power Restored! Look at the X-Ray!", 4f);
        }
    }
}
