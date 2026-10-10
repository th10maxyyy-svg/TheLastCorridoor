using UnityEngine;

/// <summary>
/// Session-only flag: true after the player died and the scene is reloaded to respawn.
/// Reset automatically on every Editor Play / app start (works even with Domain Reload disabled).
/// </summary>
public static class IntroState
{
    public static bool skipIntro = false;

    /// <summary>Call when a brand-new game starts (menu) so the intro plays again.</summary>
    public static void ResetForNewGame()
    {
        skipIntro = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        skipIntro = false;
    }
}
