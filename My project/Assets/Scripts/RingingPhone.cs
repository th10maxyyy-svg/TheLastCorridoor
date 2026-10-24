using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class RingingPhone : MonoBehaviour, IInteractable
{
    [Header("Phone Settings")]
    [SerializeField] private string interactPrompt = "Answer Phone";
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip ringingSound;
    [SerializeField] private AudioClip voiceMessageSound;

    [Header("Events")]
    public UnityEvent onCallEnded;

    private bool isRinging = true;
    private bool isCallFinished = false;
    private string SaveFlag => "PhoneAnswered_" + SaveManager.GetSceneObjectId(transform);

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (SaveManager.instance != null && SaveManager.instance.HasFlag(SaveFlag))
        {
            isRinging = false;
            isCallFinished = true;
            onCallEnded?.Invoke();
            return;
        }

        // Start ringing immediately when the game (or room) loads
        StartRinging();
    }

    private void StartRinging()
    {
        if (ringingSound != null)
        {
            audioSource.clip = ringingSound;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f; // 3D sound so player hears it from afar
            audioSource.Play();
        }
    }

    public string GetInteractPrompt()
    {
        if (isCallFinished) return ""; // Nothing happens after the call ends
        return isRinging ? interactPrompt : "Listening...";
    }

    public void Interact(PlayerStats player)
    {
        if (!isRinging || isCallFinished) return;

        // Player answers the phone
        isRinging = false;
        audioSource.Stop();
        
        StartCoroutine(PlayVoiceMessage());
    }

    private IEnumerator PlayVoiceMessage()
    {
        Debug.Log("Player answered the phone.");

        if (voiceMessageSound != null)
        {
            audioSource.clip = voiceMessageSound;
            audioSource.loop = false;
            audioSource.Play();
            Subtitles.ForClip(voiceMessageSound);
            
            // Wait for the creepy message to finish
            yield return new WaitForSeconds(voiceMessageSound.length);
        }
        else
        {
            // If no sound is attached, just wait 3 seconds
            yield return new WaitForSeconds(3f);
        }

        Debug.Log("Call ended. Triggering events (like opening a drawer).");
        isCallFinished = true;
        if (SaveManager.instance != null) SaveManager.instance.SetFlag(SaveFlag);
        
        // Trigger whatever horror event or puzzle completion (like opening the drawer)
        onCallEnded?.Invoke();

        // HIDDEN LORE: the voicemail is written into the journal (FR_64)
        GameProgress.LoreFound(this, null);
    }
}
