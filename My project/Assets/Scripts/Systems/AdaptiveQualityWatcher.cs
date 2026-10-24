using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Watches the real frame rate (Auto quality only). If it stays well below the target for a few seconds of normal play,
/// the quality goes down one step. Loading moments and the first seconds of a scene are ignored.
/// </summary>
public class AdaptiveQualityWatcher : MonoBehaviour
{
    private const float Window = 4f;          // seconds per measurement
    private float ignoreUntil;
    private float sum; private int frames;
    private int slowWindows;

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; ignoreUntil = Time.unscaledTime + 8f; }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }
    private void OnLoaded(Scene s, LoadSceneMode m) { ignoreUntil = Time.unscaledTime + 8f; sum = 0f; frames = 0; slowWindows = 0; }

    void Update()
    {
        GraphicsQuality.Tick();      // delayed heavy graphics changes from the Settings menu
        if (GraphicsQuality.Chosen != GraphicsQuality.Level.Auto) { sum = 0f; frames = 0; return; }
        if (Time.unscaledTime < ignoreUntil || Time.timeScale <= 0.01f) { sum = 0f; frames = 0; return; }

        float dt = Time.unscaledDeltaTime;
        if (dt > 0.5f) return;                 // a single hitch (loading, alt-tab) is not "slow"
        sum += dt; frames++;
        if (sum < Window) return;

        float fps = frames / sum;
        sum = 0f; frames = 0;
        int target = GraphicsQuality.TargetFps;
        float limit = target > 0 ? Mathf.Min(40f, target * 0.75f) : 40f;
        if (fps < limit)
        {
            slowWindows++;
            if (slowWindows >= 2)                      // slow for ~8 s in a row
            {
                slowWindows = 0;
                if (GraphicsQuality.StepDownAuto()) ignoreUntil = Time.unscaledTime + 3f;
            }
        }
        else slowWindows = 0;
    }
}
