using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ReadableNote : MonoBehaviour, IInteractable
{
    [TextArea(3, 10)]
    public string noteText = "This is a note.";
    
    // Using the interaction raycast system to show a long message
    public void Interact(PlayerStats playerStats)
    {
        InteractionRaycast raycast = playerStats.GetComponentInChildren<InteractionRaycast>();
        if (raycast != null)
        {
            // Show the note text for 6 seconds on screen
            raycast.ShowNotification(noteText, 6f);
        }

        // JOURNAL: remember the note so it can be re-read later (TAB) and counted for the endings / achievements
        GameProgress.NoteRead(this);
    }

    public string GetInteractPrompt()
    {
        return "Press E to Read Note";
    }
}
