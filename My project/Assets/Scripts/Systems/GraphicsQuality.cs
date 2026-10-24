using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// GRAPHICS QUALITY - makes the game run smoothly on weak AND strong PCs.
///   Quality  : Auto / Low / Medium / High / Ultra
///              Auto looks at the PC once (graphics card, its memory, RAM, CPU cores) and picks a level,
///              then keeps watching the FPS: if the game stays slow it drops one level by itself (and remembers it).
///   FPS Limit: 30 / 60 / 120 / Unlimited  (a limit keeps the frame time steady = no stutter, and the GPU cooler)
///   VSync    : Off / On
///   Display  : Fullscreen / Borderless / Windowed
///   Resolution: Native or any resolution the screen supports
/// Each level changes: render scale, shadow distance / resolution / cascades / soft shadows, extra-light shadows,
/// how many lamps may cast shadows (LightShadowBudget), HDR, texture quality and LOD distance.
/// Ultra = the project's own render settings, unchanged.
/// Works by cloning the project's URP asset at runtime, so nothing in the project is ever modified.
/// Applied automatically at startup; the options are in SETTINGS > GRAPHICS. Saved in PlayerPrefs.
/// </summary>
public static class GraphicsQuality
{
    public enum Level { Auto = 0, Low = 1, Medium = 2, High = 3, Ultra = 4 }
    public enum Display { Fullscreen = 0, Borderless = 1, Windowed = 2 }

    private const string KeyLevel = "TLC_GfxLevel", KeyAutoTier = "TLC_GfxAutoTier", KeyFps = "TLC_GfxFps",
                         KeyVsync = "TLC_GfxVsync", KeyDisplay = "TLC_GfxDisplay", KeyRes = "TLC_GfxRes";

    public static readonly int[] FpsOptions = { 30, 60, 120, -1 };       // -1 = unlimited

    public static Level Chosen { get; private set; } = Level.Auto;
    /// <summary>The level actually in use (Auto resolved).</summary>
    public static Level Active { get; private set; } = Level.High;
    public static Level AutoTier { get; private set; } = Level.High;
    public static int FpsIndex { get; private set; } = 1;
    public static bool VSync { get; private set; } = false;
    public static Display DisplayMode { get; private set; } = Display.Fullscreen;
    public static int ResolutionIndex { get; private set; } = 0;           // 0 = native, else index+1 into Resolutions()

    /// <summary>How many lamps may cast shadows at once / up to what distance (read by LightShadowBudget).</summary>
    public static int MaxShadowedLights { get; private set; } = 4;
    public static float ShadowLightDistance { get; private set; } = 25f;

    private static bool loaded;
    private static UniversalRenderPipelineAsset original;   // the project's asset - never modified
    private static float lowExtraScale = 1f;                 // Auto: last resort, lowers the render scale a bit more

    // ------------------------------------------------------------------ startup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        loaded = false; original = null; lowExtraScale = 1f;
        pipelineCache.Clear(); resolutionCache = null; pendingLevelAt = pendingScreenAt = pendingSaveAt = -1f;
        Load();
        Apply(true);
        GameObject go = new GameObject("[GraphicsQuality]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<AdaptiveQualityWatcher>();
        Application.quitting += RestoreOriginal;
    }

    public static void Load()
    {
        Chosen = (Level)Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel, 0), 0, 4);
        int auto = PlayerPrefs.GetInt(KeyAutoTier, 0);
        AutoTier = auto >= 1 && auto <= 4 ? (Level)auto : DetectTier();
        FpsIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyFps, 1), 0, FpsOptions.Length - 1);
        VSync = PlayerPrefs.GetInt(KeyVsync, 0) == 1;
        DisplayMode = (Display)Mathf.Clamp(PlayerPrefs.GetInt(KeyDisplay, 0), 0, 2);
        ResolutionIndex = Mathf.Max(0, PlayerPrefs.GetInt(KeyRes, 0));
        loaded = true;
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(KeyLevel, (int)Chosen);
        PlayerPrefs.SetInt(KeyAutoTier, (int)AutoTier);
        PlayerPrefs.SetInt(KeyFps, FpsIndex);
        PlayerPrefs.SetInt(KeyVsync, VSync ? 1 : 0);
        PlayerPrefs.SetInt(KeyDisplay, (int)DisplayMode);
        PlayerPrefs.SetInt(KeyRes, ResolutionIndex);
        PlayerPrefs.Save();
    }

    /// <summary>Looks at the hardware once and picks a sensible level.</summary>
    public static Level DetectTier()
    {
        int vram = SystemInfo.graphicsMemorySize;       // MB
        int ram = SystemInfo.systemMemorySize;          // MB
        int cores = SystemInfo.processorCount;
        string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
        bool integrated = gpu.Contains("intel") || gpu.Contains("uhd") || gpu.Contains("iris") || gpu.Contains("vega")
                          || (gpu.Contains("radeon") && gpu.Contains("graphics") && !gpu.Contains("rx"))
                          || gpu.Contains("microsoft basic");

        if (integrated || vram < 2000 || ram < 6000 || cores < 4) return Level.Low;
        if (vram < 4000 || ram < 12000 || cores < 6) return Level.Medium;
        if (vram < 8000) return Level.High;
        return Level.Ultra;
    }

    // ------------------------------------------------------------------ changing options (used by the Settings menu)
    public const int RowQuality = 0, RowFps = 1, RowVsync = 2, RowDisplay = 3, RowResolution = 4;
    public static readonly string[] RowNames = { "Graphics Quality", "FPS Limit", "VSync", "Display Mode", "Resolution" };

    public static void Cycle(int row, int dir)
    {
        if (!loaded) Load();
        switch (row)
        {
            case RowQuality: Chosen = (Level)Wrap((int)Chosen + dir, 5); break;
            case RowFps: FpsIndex = Wrap(FpsIndex + dir, FpsOptions.Length); break;
            case RowVsync: VSync = !VSync; break;
            case RowDisplay: DisplayMode = (Display)Wrap((int)DisplayMode + dir, 3); break;
            case RowResolution: ResolutionIndex = Wrap(ResolutionIndex + dir, Resolutions().Count + 1); break;
        }
        if (row == RowQuality && Chosen == Level.Auto) lowExtraScale = 1f;

        // LAG FIX: changing the render pipeline / textures / screen is expensive (a hitch every click).
        // Cheap options apply at once; the heavy ones only once the player STOPS clicking (so cycling
        // Low > Medium > High rebuilds the renderer once, not three times).
        if (row == RowQuality) { Active = Chosen == Level.Auto ? AutoTier : Chosen; pendingLevelAt = Time.unscaledTime + 0.5f; }
        else if (row == RowFps || row == RowVsync) ApplyFramePacing();
        else pendingScreenAt = Time.unscaledTime + 0.7f;
        pendingSaveAt = Time.unscaledTime + 1f;
    }

    private static float pendingLevelAt = -1f, pendingScreenAt = -1f, pendingSaveAt = -1f;

    /// <summary>Called every frame by AdaptiveQualityWatcher: applies the delayed heavy changes.</summary>
    public static void Tick()
    {
        float now = Time.unscaledTime;
        if (pendingLevelAt > 0f && now >= pendingLevelAt) { pendingLevelAt = -1f; ApplyLevel(); }
        if (pendingScreenAt > 0f && now >= pendingScreenAt) { pendingScreenAt = -1f; ApplyScreen(); }
        if (pendingSaveAt > 0f && now >= pendingSaveAt) { pendingSaveAt = -1f; Save(); }
    }

    public static string ValueText(int row)
    {
        if (!loaded) Load();
        switch (row)
        {
            case RowQuality: return Chosen == Level.Auto ? "AUTO (" + Active.ToString().ToUpper() + ")" : Chosen.ToString().ToUpper();
            case RowFps: return FpsOptions[FpsIndex] < 0 ? "UNLIMITED" : FpsOptions[FpsIndex] + " FPS";
            case RowVsync: return VSync ? "ON" : "OFF";
            case RowDisplay: return DisplayMode == Display.Borderless ? "BORDERLESS" : DisplayMode.ToString().ToUpper();
            case RowResolution:
                if (ResolutionIndex == 0) return "NATIVE";
                List<Vector2Int> r = Resolutions();
                int i = Mathf.Clamp(ResolutionIndex - 1, 0, Mathf.Max(0, r.Count - 1));
                return r.Count == 0 ? "NATIVE" : r[i].x + " x " + r[i].y;
        }
        return "";
    }

    private static string infoCache = "";
    public static string InfoText() { return infoCache; }
    private static void UpdateInfo()
    {
        infoCache = "Detected: " + SystemInfo.graphicsDeviceName + "  (" + SystemInfo.graphicsMemorySize + " MB)   -   running: " + Active.ToString().ToUpper();
    }

    public static void ResetToDefaults()
    {
        Chosen = Level.Auto;
        AutoTier = DetectTier();
        FpsIndex = 1; VSync = false; DisplayMode = Display.Fullscreen; ResolutionIndex = 0;
        lowExtraScale = 1f;
        Apply(true);
        Save();
    }

    private static int Wrap(int v, int n) { return ((v % n) + n) % n; }

    private static List<Vector2Int> resolutionCache;
    public static List<Vector2Int> Resolutions()
    {
        if (resolutionCache != null) return resolutionCache;
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (v.y >= 600 && !list.Contains(v)) list.Add(v);
        }
        list.Sort((a, b) => (b.x * b.y).CompareTo(a.x * a.y));      // biggest first
        resolutionCache = list;
        return list;
    }

    // ------------------------------------------------------------------ Auto: drop a level when the game stays slow
    /// <summary>Called by the watcher when the FPS has been too low for a while. Returns false when nothing is left to lower.</summary>
    public static bool StepDownAuto()
    {
        if (Chosen != Level.Auto) return false;
        if (AutoTier > Level.Low) AutoTier = AutoTier - 1;
        else if (lowExtraScale > 0.7f) lowExtraScale -= 0.15f;
        else return false;
        ApplyLevel();
        Save();
        Debug.Log("[TLC] Graphics: the game was running slowly - quality lowered to " + Active + (lowExtraScale < 1f ? " (render scale " + lowExtraScale.ToString("0.00") + ")" : ""));
        return true;
    }

    public static int TargetFps => VSync ? Mathf.Max(30, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value)) : FpsOptions[FpsIndex];

    // ------------------------------------------------------------------ applying
    public static void Apply(bool screenToo)
    {
        if (!loaded) Load();
        ApplyFramePacing();
        ApplyLevel();
        if (screenToo) ApplyScreen();
    }

    private static void ApplyFramePacing()
    {
        int vs = VSync ? 1 : 0, fps = VSync ? -1 : FpsOptions[FpsIndex];
        if (QualitySettings.vSyncCount != vs) QualitySettings.vSyncCount = vs;
        if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;
    }

    private static void ApplyLevel()
    {
        Active = Chosen == Level.Auto ? AutoTier : Chosen;
        UpdateInfo();

        // general quality (only touched when it really changes: a new texture limit reloads every texture)
        int mip = Active == Level.Low ? 1 : 0;                                                   // half-size textures on Low
        if (QualitySettings.globalTextureMipmapLimit != mip) QualitySettings.globalTextureMipmapLimit = mip;
        QualitySettings.lodBias = Active == Level.Low ? 0.7f : Active == Level.Medium ? 1f : Active == Level.High ? 1.5f : 2f;
        AnisotropicFiltering af = Active >= Level.High ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
        if (QualitySettings.anisotropicFiltering != af) QualitySettings.anisotropicFiltering = af;

        // lamps that may cast shadows (each point-light shadow = 6 shadow maps!)
        MaxShadowedLights = Active == Level.Low ? 0 : Active == Level.Medium ? 2 : 3;   // 3 point lights = 18 shadow maps: fits the shadow atlas (4 overflowed it every frame)
        ShadowLightDistance = Active == Level.Low ? 0f : Active == Level.Medium ? 14f : Active == Level.High ? 20f : 25f;

        ApplyPipeline();
    }

    private static void ApplyPipeline()
    {
        if (original == null)
        {
            original = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (original == null) original = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        }
        if (original == null) return;

        if (Active == Level.Ultra && lowExtraScale >= 1f)
        {
            if (QualitySettings.renderPipeline != original) QualitySettings.renderPipeline = original;
            return;
        }

        // each level's renderer settings are made ONCE and reused (no new asset - and no hitch - on every change)
        string key = Active + "_" + lowExtraScale.ToString("0.00");
        if (pipelineCache.TryGetValue(key, out UniversalRenderPipelineAsset cached) && cached != null)
        {
            if (QualitySettings.renderPipeline != cached) QualitySettings.renderPipeline = cached;
            return;
        }

        UniversalRenderPipelineAsset a = Object.Instantiate(original);
        a.name = original.name + " (" + Active + ")";
        switch (Active)
        {
            case Level.Low:
                a.renderScale = 0.75f * lowExtraScale;
                a.shadowDistance = 12f;
                a.shadowCascadeCount = 1;
                a.supportsHDR = false;
                a.msaaSampleCount = 1;
                SetField(a, "m_MainLightShadowmapResolution", 512);
                SetField(a, "m_AdditionalLightShadowsSupported", false);
                SetField(a, "m_AdditionalLightsShadowmapResolution", 512);
                SetField(a, "m_SoftShadowsSupported", false);
                SetField(a, "m_AdditionalLightsPerObjectLimit", 2);
                SetField(a, "m_UpscalingFilter", 1);      // linear upscale (cheap and smooth)
                break;
            case Level.Medium:
                a.renderScale = 0.9f * lowExtraScale;
                a.shadowDistance = 18f;
                a.shadowCascadeCount = 2;
                a.msaaSampleCount = 1;
                SetField(a, "m_MainLightShadowmapResolution", 1024);
                SetField(a, "m_AdditionalLightsShadowmapResolution", 1024);
                SetField(a, "m_SoftShadowsSupported", false);
                SetField(a, "m_AdditionalLightsPerObjectLimit", 4);
                break;
            case Level.High:
                a.renderScale = 1f * lowExtraScale;
                a.shadowDistance = 25f;
                a.shadowCascadeCount = 2;
                SetField(a, "m_MainLightShadowmapResolution", 2048);
                SetField(a, "m_AdditionalLightsShadowmapResolution", 2048);
                break;
            default:   // Ultra with a lowered scale (only Auto can get here)
                a.renderScale = lowExtraScale;
                break;
        }

        pipelineCache[key] = a;
        QualitySettings.renderPipeline = a;
    }

    private static readonly Dictionary<string, UniversalRenderPipelineAsset> pipelineCache = new Dictionary<string, UniversalRenderPipelineAsset>();

    private static void SetField(object o, string field, object value)
    {
        FieldInfo f = typeof(UniversalRenderPipelineAsset).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null) return;
        try
        {
            if (f.FieldType.IsEnum) f.SetValue(o, System.Enum.ToObject(f.FieldType, value));
            else f.SetValue(o, System.Convert.ChangeType(value, f.FieldType));
        }
        catch { /* a field that does not exist in this URP version: just skip it */ }
    }

    private static void ApplyScreen()
    {
        if (Application.isEditor) return;    // the editor Game view decides its own size
        FullScreenMode mode = DisplayMode == Display.Fullscreen ? FullScreenMode.ExclusiveFullScreen
                            : DisplayMode == Display.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        int w, h;
        List<Vector2Int> r = Resolutions();
        if (ResolutionIndex > 0 && ResolutionIndex - 1 < r.Count) { w = r[ResolutionIndex - 1].x; h = r[ResolutionIndex - 1].y; }
        else { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }
        if (mode == FullScreenMode.Windowed && ResolutionIndex == 0) { w = Mathf.RoundToInt(w * 0.8f); h = Mathf.RoundToInt(h * 0.8f); }
        Screen.SetResolution(w, h, mode);
    }

    /// <summary>Editor: give the project its own render asset back when Play mode stops (the runtime clone must never stay in the settings).</summary>
    private static void RestoreOriginal()
    {
        if (original != null && QualitySettings.renderPipeline != original) QualitySettings.renderPipeline = original;
    }
}
