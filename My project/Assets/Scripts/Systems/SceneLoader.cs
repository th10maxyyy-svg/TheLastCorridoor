using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// LOADING SCREEN: loads a scene in the background behind a dark screen with a survival tip.
/// Use SceneLoader.Load(name or index) instead of SceneManager.LoadScene.
/// The screen is shown for at least ~1.4 s so the tip can be read, then fades away in the new scene.
/// </summary>
public static class SceneLoader
{
    public static readonly string[] Tips =
    {
        "He cannot see you in a hiding spot - unless he watched you get in.",
        "Running drains your stamina. Walk when he is far away, and he hears you less.",
        "Read every note. Codes and clues are hidden in them - TAB opens your journal.",
        "Medkits give back health. Save them for when it really hurts.",
        "Every key opens the next room. Lost? Check the MAP in your journal (TAB).",
        "Your heart monitor beats faster when he is close. Listen to it.",
        "Crouch (CTRL) to move quietly.",
        "Closed doors slow him down - but he learns which doors you use.",
        "The headlamp (F) helps you see, but darkness is also where you hide.",
        "You can save in three slots from the pause menu (ESC).",
        "The gate needs a key AND an 8-digit code. The digits are on notes all over the asylum.",
        "If you die, you wake up in the basement. You only have a few lives.",
    };

    public static bool IsLoading => SceneLoaderRunner.Busy;

    public static void Load(string sceneName) { SceneLoaderRunner.Begin(sceneName, -1); }
    public static void Load(int buildIndex) { SceneLoaderRunner.Begin(null, buildIndex); }

    public static string RandomTip() { return Tips[Random.Range(0, Tips.Length)]; }
}
