#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes the Console spam "SerializationException: Unable to find type: 'SaveSystem'" / "'MainMenuController'".
/// Those two old scripts were deleted long ago, but the Visual Scripting node library cache
/// (Assets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db) still lists them, so Visual Scripting
/// complains about every one of their members each time the editor loads.
/// This regenerates that cache ONCE (same as Project Settings > Visual Scripting > Regenerate Nodes).
/// Can be run again from the menu: The Last Corridor / Regenerate Visual Scripting Nodes.
/// </summary>
[InitializeOnLoad]
public static class VisualScriptingCacheFix
{
    private const string PrefKey = "TLC_VSNodesRegenerated_v1_";

    static VisualScriptingCacheFix()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false)) return;
        // wait a few seconds so Visual Scripting has finished its own start-up first
        double at = EditorApplication.timeSinceStartup + 5.0;
        EditorApplication.CallbackFunction wait = null;
        wait = () =>
        {
            if (EditorApplication.timeSinceStartup < at || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= wait;
            if (Regenerate()) EditorPrefs.SetBool(key, true);
        };
        EditorApplication.update += wait;
    }

    [MenuItem("The Last Corridor/Regenerate Visual Scripting Nodes")]
    private static void MenuRegenerate() { Regenerate(); }

    private static bool Regenerate()
    {
        Type unitBase = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            unitBase = a.GetType("Unity.VisualScripting.UnitBase");
            if (unitBase != null) break;
        }
        if (unitBase == null)
        {
            Debug.Log("[TLC] Visual Scripting is not loaded - nothing to regenerate.");
            return true;
        }

        try
        {
            MethodInfo rebuild = unitBase.GetMethod("Rebuild", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (rebuild != null)
            {
                rebuild.Invoke(null, null);
            }
            else
            {
                MethodInfo build = unitBase.GetMethod("Build", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (build == null)
                {
                    Debug.LogWarning("[TLC] Could not find Visual Scripting's Rebuild method. Use Edit > Project Settings > Visual Scripting > Regenerate Nodes once.");
                    return false;
                }
                build.Invoke(null, new object[] { true });
            }
            Debug.Log("[TLC] Visual Scripting node library regenerated - the old 'Unable to find type SaveSystem / MainMenuController' warnings will not come back.");
            return true;
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException ?? e;
            Debug.LogWarning("[TLC] Visual Scripting regenerate failed: " + inner.Message + "\nUse Edit > Project Settings > Visual Scripting > Regenerate Nodes once.");
            return false;
        }
    }
}
#endif
