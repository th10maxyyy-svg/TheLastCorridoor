using UnityEngine;
using UnityEditor;

public class StuckPlayerFixer : EditorWindow
{
    [MenuItem("Tools/Fix Stuck Player")]
    public static void FixPlayer()
    {
        // 1. Reset Time
        Time.timeScale = 1f;
        Debug.Log("Time.timeScale reset to 1.");

        // 2. Find Player
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.enabled = true;
            player.isCinematicPlaying = false;
            
            // We use reflection to set private fields just in case they are stuck
            var type = player.GetType();
            
            var isWakingUpField = type.GetField("isWakingUp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isWakingUpField != null) isWakingUpField.SetValue(player, false);
            
            // isHidden and isDead are properties with private setters. We can't easily write them if there's no setter logic, 
            // but we can try setting the backing fields if we really needed to. However, let's just re-enable the controller.
            
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            Debug.Log("PlayerController variables reset! You should be able to move now.");
        }
        else
        {
            Debug.LogError("Could not find PlayerController in the scene!");
        }
        
        // 3. Fix Cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
