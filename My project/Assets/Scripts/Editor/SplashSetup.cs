#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Turns off the "Made with Unity" splash screen (Project Settings > Player > Splash Image). Runs once automatically.</summary>
[InitializeOnLoad]
public static class SplashSetup
{
    static SplashSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("The Last Corridor/Disable Unity Splash Screen")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!PlayerSettings.SplashScreen.show && !PlayerSettings.SplashScreen.showUnityLogo) return;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.show = false;
        AssetDatabase.SaveAssets();
        Debug.Log("SplashSetup: the 'Made with Unity' splash screen is turned off.");
    }
}
#endif
