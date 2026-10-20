using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// PLAYER SETTINGS (FR_47): master volume, brightness, effect intensity and mouse sensitivity.
/// Stored in PlayerPrefs so they survive restarts and are shared by the main menu and the game.
///  - Volume      -> AudioListener.volume
///  - Brightness  -> a runtime URP Global Volume that only overrides "Post Exposure" (scene colour grading is untouched)
///  - Effects     -> head-bob amount (PlayerController) - 0 = off for players who get motion sick
///  - Sensitivity -> mouse look multiplier (PlayerController)
/// Applied automatically on every scene load by GameUI - no scene setup needed.
/// </summary>
public static class GameSettings
{
    private const string KeyVolume = "TLC_MasterVolume";
    private const string KeyBrightness = "TLC_Brightness";
    private const string KeyEffects = "TLC_EffectsIntensity";
    private const string KeySensitivity = "TLC_MouseSensitivity";

    public static float MasterVolume { get; private set; } = 1f;     // 0..1
    public static float Brightness { get; private set; } = 0f;       // -1..+1.5 exposure stops
    public static float EffectsIntensity { get; private set; } = 1f; // 0..1
    public static float MouseSensitivity { get; private set; } = 1f; // 0.3..2.5 multiplier

    private static bool loaded = false;
    private static Volume brightnessVolume;
    private static ColorAdjustments colorAdjustments;

    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
        Brightness = Mathf.Clamp(PlayerPrefs.GetFloat(KeyBrightness, 0f), -1f, 1.5f);
        EffectsIntensity = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyEffects, 1f));
        MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 1f), 0.3f, 2.5f);
        loaded = true;
    }

    private static void EnsureLoaded()
    {
        if (!loaded) Load();
    }

    public static void SetMasterVolume(float v) { EnsureLoaded(); MasterVolume = Mathf.Clamp01(v); Apply(); }
    public static void SetBrightness(float v) { EnsureLoaded(); Brightness = Mathf.Clamp(v, -1f, 1.5f); Apply(); }
    public static void SetEffectsIntensity(float v) { EnsureLoaded(); EffectsIntensity = Mathf.Clamp01(v); }
    public static void SetMouseSensitivity(float v) { EnsureLoaded(); MouseSensitivity = Mathf.Clamp(v, 0.3f, 2.5f); }

    public static void ResetToDefaults()
    {
        MasterVolume = 1f; Brightness = 0f; EffectsIntensity = 1f; MouseSensitivity = 1f;
        loaded = true;
        Apply();
        Save();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
        PlayerPrefs.SetFloat(KeyBrightness, Brightness);
        PlayerPrefs.SetFloat(KeyEffects, EffectsIntensity);
        PlayerPrefs.SetFloat(KeySensitivity, MouseSensitivity);
        PlayerPrefs.Save();
    }

    /// <summary>Applies volume and brightness to the current scene.</summary>
    public static void Apply()
    {
        EnsureLoaded();
        AudioListener.volume = MasterVolume;
        ApplyBrightness();
    }

    private static void ApplyBrightness()
    {
        try
        {
            if (brightnessVolume == null)
            {
                if (Mathf.Approximately(Brightness, 0f)) return; // nothing to do, don't touch the scene

                GameObject go = new GameObject("TLC_BrightnessVolume");
                Object.DontDestroyOnLoad(go);
                brightnessVolume = go.AddComponent<Volume>();
                brightnessVolume.isGlobal = true;
                brightnessVolume.priority = 1000f;
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                colorAdjustments = profile.Add<ColorAdjustments>(false); // false = only what we override below changes
                brightnessVolume.sharedProfile = profile;
            }

            if (colorAdjustments != null)
            {
                colorAdjustments.active = true;
                colorAdjustments.postExposure.Override(Brightness);
            }

            // Post exposure only shows up when the camera renders post-processing
            if (!Mathf.Approximately(Brightness, 0f))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GameSettings: brightness could not be applied (" + e.Message + ")");
        }
    }
}
