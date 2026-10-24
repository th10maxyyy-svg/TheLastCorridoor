using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TESTING TOOL (Unity Editor only - never active in a built game):
///   menu  The Last Corridor > Testing > Unlock All Doors   (Ctrl+Shift+U)
/// ON  = every locked room door in the asylum opens without a key / code (nothing is written to the save).
/// OFF = one click locks them all again (right away if the game is running). Doors the player really unlocked stay unlocked.
/// While it is ON a small red "TEST: ALL DOORS UNLOCKED" label is shown so it is not forgotten.
/// </summary>
public class TestDoorUnlock : MonoBehaviour
{
    public const string PrefKey = "TLC_TestUnlockAllDoors";
    private static TestDoorUnlock inst;
    private static bool hooked;
    private GUIStyle style;

    /// <summary>True while the doors are unlocked right now (Editor switch or the F9 presentation panel).</summary>
    public static bool IsOn => inst != null;

    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(PrefKey, false);
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inst = null; hooked = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
#if UNITY_EDITOR
        if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => { if (Enabled) Ensure(); }; }
        if (Enabled) Ensure();
#endif
    }

    private static void Ensure()
    {
        if (inst != null) { inst.StartCoroutine(inst.ApplySoon(true)); return; }
        GameObject g = new GameObject("TestDoorUnlock (testing)");
        DontDestroyOnLoad(g);
        inst = g.AddComponent<TestDoorUnlock>();
        inst.StartCoroutine(inst.ApplySoon(true));
    }

    /// <summary>Called by the editor menu while the game is running.</summary>
    public static void SetNow(bool on)
    {
        if (on) Ensure();
        else
        {
            Apply(false);
            if (inst != null) Destroy(inst.gameObject);
            inst = null;
        }
    }

    private System.Collections.IEnumerator ApplySoon(bool on)
    {
        yield return null;   // after every door's Start (which reads the save)
        Apply(on);
    }

    private static void Apply(bool on)
    {
        int n = 0;
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include))
        {
            bool was = d.IsLocked;
            d.SetTestUnlocked(on);
            if (was != d.IsLocked) n++;
        }
        Debug.Log(on ? "[TEST] " + n + " locked doors unlocked for testing (not saved)." : "[TEST] " + n + " doors locked again.");
    }

    void OnGUI()
    {
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.25f, 0.2f) } };
        GUI.Label(new Rect(10f, 6f, 420f, 24f), "TEST: ALL DOORS UNLOCKED  (F9 panel / Ctrl+Shift+U to lock again)", style);
    }
}
