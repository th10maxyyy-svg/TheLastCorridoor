#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>One-click testing switch: The Last Corridor > Testing > Unlock All Doors (Ctrl+Shift+U). Click again to lock them.</summary>
public static class TestDoorsMenu
{
    private const string MenuPath = "The Last Corridor/Testing/Unlock All Doors %#u";
    private const string MenuName = "The Last Corridor/Testing/Unlock All Doors";

    [MenuItem(MenuPath, false, 1)]
    private static void Toggle()
    {
        bool on = !EditorPrefs.GetBool(TestDoorUnlock.PrefKey, false);
        EditorPrefs.SetBool(TestDoorUnlock.PrefKey, on);
        if (Application.isPlaying) TestDoorUnlock.SetNow(on);
        Debug.Log(on ? "[TEST] ALL DOORS UNLOCKED for testing. Click the menu again (Ctrl+Shift+U) to lock them."
                     : "[TEST] Doors are locked again - the game is back to normal.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleCheck()
    {
        Menu.SetChecked(MenuName, EditorPrefs.GetBool(TestDoorUnlock.PrefKey, false));
        return true;
    }
}
#endif
