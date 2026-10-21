using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ONE RUNTIME UI FOR ALL THE NEW SYSTEMS - created automatically, no scene or prefab setup needed.
///   In the game level:
///     ESC  - Pause menu: Resume / Save (3 slots) / Load / Journal / Achievements / Settings / Main Menu / Quit   (FR_38, FR_42, FR_47)
///     TAB  - Journal: Objectives / Items (inspect) / Notes (re-read) / Statistics                               (FR_34, FR_35, FR_37, FR_49, FR_51)
///     HUD  - current objective (top-right) + pop-ups for new objectives, completed objectives and achievements (FR_49, FR_52)
///     Ending summary screen after the escape or the final death                                                 (FR_56)
///   In the main menu: LOAD GAME / ACHIEVEMENTS / OPTIONS buttons (bottom-right)                                 (FR_42, FR_53)
/// Drawn with Unity's immediate-mode GUI, scaled to a virtual 1920x1080 screen.
/// </summary>
public class GameUI : MonoBehaviour
{
    // ============================================================================================ bootstrap
    private static GameUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        toasts.Clear();
        summary = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        GameObject go = new GameObject("TLC_GameUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameUI>();
    }

    private enum Mode { None, Menu, Game }
    private enum Panel { None, Pause, Save, Load, Settings, Journal, Achievements, ConfirmMenu, ConfirmQuit }

    private Mode mode = Mode.None;
    private Panel panel = Panel.None;
    private Panel returnPanel = Panel.Pause;  // where "Back" goes (Pause in game, None in the menu)
    private bool paused;
    private float savedTimeScale = 1f;
    private readonly List<Behaviour> disabledWhilePaused = new List<Behaviour>();
    private bool keypadOpenLastFrame;
    private int journalTab;
    private int selectedItem = -1, selectedNote = -1;
    private Vector2 scrollA, scrollB;
    private string panelMessage = "";
    private float panelMessageTime;
    private PlayerController player;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        GameSettings.Load();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetupScene();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        SetupScene();
    }

    private void SetupScene()
    {
        paused = false;
        panel = Panel.None;
        disabledWhilePaused.Clear();
        summary = null;
        player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player != null || RespawnManager.instance != null) mode = Mode.Game;
        else if (FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include) != null) mode = Mode.Menu;
        else mode = Mode.None;

        GameSettings.Apply();

        if (mode == Mode.Menu)
        {
            HookMenuSliders();
            PrepareMenu();
        }

        if (mode == Mode.Game && ObjectiveTracker.Instance == null)
        {
            GameObject go = new GameObject("TLC_ObjectiveTracker");
            go.AddComponent<ObjectiveTracker>(); // scene object: rebuilt on every (re)load
            go.AddComponent<HorrorDirector>();   // unscripted horror events, session variation, enemy restore
            go.AddComponent<HudBarStyler>();     // slim health / stamina bars
        }
    }

    /// <summary>Called by the LOAD GAME / ACHIEVEMENTS / OPTIONS buttons of the main menu scene (0 = load, 1 = achievements, 2 = options).</summary>
    public static void OpenFromMenu(int which)
    {
        if (instance == null) Boot();
        if (instance == null || instance.mode != Mode.Menu) return;
        Panel[] targets = { Panel.Load, Panel.Achievements, Panel.Settings };
        instance.OpenPanel(targets[Mathf.Clamp(which, 0, 2)], Panel.None);
    }

    /// <summary>True while the LOAD / ACHIEVEMENTS / OPTIONS panel is open in the main menu.</summary>
    public static bool IsMenuPanelOpen { get { return instance != null && instance.mode == Mode.Menu && instance.panel != Panel.None; } }

    private UnityEngine.UI.GraphicRaycaster[] menuRays;
    private float raycastReenableAt;

    private void PrepareMenu()
    {
        menuRays = FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        raycastReenableAt = 0f;
    }

    /// <summary>The main menu's own Settings screen has "volume" and "sensitivity" sliders - connect them to GameSettings.</summary>
    private void HookMenuSliders()
    {
        foreach (UnityEngine.UI.Slider slider in FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsInactive.Include))
        {
            string n = slider.gameObject.name.ToLowerInvariant();
            UnityEngine.UI.Slider sl = slider;
            if (n.Contains("volume"))
            {
                sl.SetValueWithoutNotify(Mathf.Lerp(sl.minValue, sl.maxValue, GameSettings.MasterVolume));
                sl.onValueChanged.AddListener(v =>
                {
                    GameSettings.SetMasterVolume(Mathf.InverseLerp(sl.minValue, sl.maxValue, v));
                    GameSettings.Save();
                });
            }
            else if (n.Contains("sensitivity"))
            {
                sl.SetValueWithoutNotify(Mathf.Lerp(sl.minValue, sl.maxValue, Mathf.InverseLerp(0.3f, 2.5f, GameSettings.MouseSensitivity)));
                sl.onValueChanged.AddListener(v =>
                {
                    GameSettings.SetMouseSensitivity(Mathf.Lerp(0.3f, 2.5f, Mathf.InverseLerp(sl.minValue, sl.maxValue, v)));
                    GameSettings.Save();
                });
            }
        }
    }

    // ============================================================================================ toasts
    private class ToastMsg { public string title, text; public float start; public bool gold; }
    private static readonly List<ToastMsg> toasts = new List<ToastMsg>();
    private const float ToastTime = 4.5f;

    public static void Toast(string title, string text, bool gold = false)
    {
        if (toasts.Count > 6) toasts.RemoveAt(0);
        toasts.Add(new ToastMsg { title = title, text = text, gold = gold, start = -1f });
    }

    // ============================================================================================ ending summary (FR_56)
    private static GameProgress.RunSummary summary;
    public static bool IsSummaryOpen => summary != null;

    /// <summary>Shows the ending summary. Callers wait with: while (GameUI.IsSummaryOpen) yield return null;</summary>
    public static void ShowEndingSummary(GameProgress.RunSummary s)
    {
        if (instance == null) Boot();
        summary = s;
        if (instance != null) instance.scrollA = Vector2.zero;
    }

    // ============================================================================================ pause handling
    private bool CanPause()
    {
        if (mode != Mode.Game || paused || summary != null) return false;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null || !player.enabled || player.isDead || player.isCinematicPlaying) return false;
        if (KeypadController.IsAnyKeypadOpen || keypadOpenLastFrame) return false;
        if (GameManager.instance != null && GameManager.instance.IsGameOver()) return false;
        if (Time.timeScale <= 0f) return false;
        PlayerStats ps = player.GetComponent<PlayerStats>();
        if (ps != null && ps.IsDead()) return false;
        return true;
    }

    private void Pause(Panel open)
    {
        if (!CanPause()) return;
        paused = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        disabledWhilePaused.Clear();
        GameObject p = player.gameObject;
        DisableWhilePaused(player);
        foreach (InteractionRaycast r in p.GetComponentsInChildren<InteractionRaycast>()) DisableWhilePaused(r);
        foreach (Inventory i in p.GetComponentsInChildren<Inventory>()) DisableWhilePaused(i);
        foreach (HeadlampController h in p.GetComponentsInChildren<HeadlampController>()) DisableWhilePaused(h);
        foreach (TaserGun t in p.GetComponentsInChildren<TaserGun>()) DisableWhilePaused(t);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        OpenPanel(open, Panel.Pause);
    }

    private void DisableWhilePaused(Behaviour b)
    {
        if (b != null && b.enabled)
        {
            b.enabled = false;
            disabledWhilePaused.Add(b);
        }
    }

    private void Resume()
    {
        if (!paused) return;
        paused = false;
        panel = Panel.None;
        Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
        AudioListener.pause = false;
        foreach (Behaviour b in disabledWhilePaused) if (b != null) b.enabled = true;
        disabledWhilePaused.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameSettings.Save();
    }

    private void OpenPanel(Panel p, Panel back)
    {
        panel = p;
        returnPanel = back;
        panelMessage = "";
        scrollA = scrollB = Vector2.zero;
    }

    private void Back()
    {
        if (panel == Panel.Settings) GameSettings.Save();
        if (mode == Mode.Game)
        {
            if (panel == Panel.Pause || (panel == Panel.Journal && returnPanel == Panel.None)) Resume();
            else OpenPanel(Panel.Pause, Panel.Pause);
        }
        else panel = Panel.None;
    }

    private void LeaveToMainMenu()
    {
        if (SaveManager.instance != null && SaveManager.instance.IsInGameLevel)
        {
            if (!SaveManager.instance.TryManualCheckpoint()) SaveManager.instance.SaveWorldState();
        }
        IntroState.skipIntro = true;
        paused = false;
        panel = Panel.None;
        disabledWhilePaused.Clear();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSettings.Save();
        SceneManager.LoadScene(0);
    }

    private void QuitGame()
    {
        if (SaveManager.instance != null && SaveManager.instance.IsInGameLevel)
        {
            if (!SaveManager.instance.TryManualCheckpoint()) SaveManager.instance.SaveWorldState();
        }
        GameSettings.Save();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void Update()
    {
        if (mode == Mode.Game)
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            bool keypadOpen = KeypadController.IsAnyKeypadOpen;

            if (summary != null)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && !keypadOpen && !keypadOpenLastFrame)
            {
                if (paused) Back();
                else Pause(Panel.Pause);
            }
            else if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.J))
            {
                if (paused && panel == Panel.Journal && returnPanel == Panel.None) Resume();
                else if (!paused && CanPause()) { Pause(Panel.Journal); returnPanel = Panel.None; journalTab = 0; }
            }

            if (paused)
            {
                // other scripts sometimes lock the cursor - keep it free while a menu is open
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Time.timeScale != 0f) Time.timeScale = 0f;
            }
            keypadOpenLastFrame = keypadOpen;
        }
        else if (mode == Mode.Menu)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && panel != Panel.None) Back();

            // While one of our panels is open the menu's own buttons (START, QUIT...) must not receive the same mouse click
            bool open = panel != Panel.None;
            if (open) raycastReenableAt = Time.unscaledTime + 0.25f;
            bool want = !open && Time.unscaledTime >= raycastReenableAt;
            if (menuRays != null)
            {
                foreach (UnityEngine.UI.GraphicRaycaster gr in menuRays)
                    if (gr != null && gr.enabled != want) gr.enabled = want;
            }
        }
    }

    // ============================================================================================ GUI styles
    private const float VW = 1920f, VH = 1080f;
    private float vw;
    private GUIStyle sTitle, sHeader, sText, sSmall, sButton, sTab, sTabOn, sBox, sToastTitle, sToastText, sCenter, sRight, sBig, sListItem, sListItemOn, sSliderTrack, sSliderThumb;
    private Texture2D tBlack, tPanel, tRed, tRedDark, tGray, tWhite, tGold, tCard, tCardHi;
    private GUISkin sSkin;
    private static readonly Color Blood = new Color(0.78f, 0.34f, 0.16f);   // rust orange (matches the menu sprites)
    private static readonly Color Bone = new Color(0.88f, 0.84f, 0.78f);
    private static readonly Color Dim = new Color(0.62f, 0.58f, 0.54f);
    private static readonly Color Gold = new Color(0.95f, 0.75f, 0.25f);

    private static Texture2D Tex(Color c)
    {
        Texture2D t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private void BuildStyles()
    {
        if (sTitle != null && tBlack != null) return;
        tBlack = Tex(new Color(0f, 0f, 0f, 0.82f));
        tPanel = Tex(new Color(0.045f, 0.038f, 0.036f, 0.97f));
        tRed = Tex(new Color(0.50f, 0.17f, 0.08f, 0.97f));
        tRedDark = Tex(new Color(0.24f, 0.10f, 0.06f, 0.95f));
        tGray = Tex(new Color(0.13f, 0.105f, 0.095f, 0.95f));
        tWhite = Tex(Color.white);
        tGold = Tex(new Color(0.45f, 0.33f, 0.05f, 0.95f));
        tCard = Tex(new Color(0.080f, 0.066f, 0.060f, 1f));
        tCardHi = Tex(new Color(0.165f, 0.108f, 0.045f, 1f));

        // a copy of the default skin with a slim rust scrollbar (so the global IMGUI skin is not touched)
        sSkin = Object.Instantiate(GUI.skin);
        sSkin.verticalScrollbar.normal.background = Tex(new Color(0.10f, 0.08f, 0.07f, 1f));
        sSkin.verticalScrollbar.fixedWidth = 12f;
        sSkin.verticalScrollbarThumb.normal.background = tRed;
        sSkin.verticalScrollbarThumb.hover.background = Tex(new Color(0.75f, 0.33f, 0.14f, 1f));
        sSkin.verticalScrollbarThumb.active.background = sSkin.verticalScrollbarThumb.hover.background;
        sSkin.verticalScrollbarThumb.fixedWidth = 12f;
        sSkin.verticalScrollbarUpButton.fixedHeight = 0f; sSkin.verticalScrollbarUpButton.fixedWidth = 0f;
        sSkin.verticalScrollbarDownButton.fixedHeight = 0f; sSkin.verticalScrollbarDownButton.fixedWidth = 0f;

        sTitle = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sTitle.normal.textColor = Blood;
        sBig = new GUIStyle(sTitle) { fontSize = 46 };
        sHeader = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
        sHeader.normal.textColor = Bone;
        sText = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, richText = true };
        sText.normal.textColor = Bone;
        sSmall = new GUIStyle(sText) { fontSize = 19 };
        sSmall.normal.textColor = Dim;
        sCenter = new GUIStyle(sText) { alignment = TextAnchor.MiddleCenter };
        sRight = new GUIStyle(sText) { alignment = TextAnchor.MiddleRight };

        sButton = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sButton.normal.background = tGray; sButton.normal.textColor = new Color(0.82f, 0.66f, 0.54f);
        sButton.hover.background = tRed; sButton.hover.textColor = new Color(1f, 0.93f, 0.85f);
        sButton.active.background = tRedDark; sButton.active.textColor = Color.white;
        sButton.focused.background = tGray; sButton.focused.textColor = Bone;

        sTab = new GUIStyle(sButton) { fontSize = 22 };
        sTabOn = new GUIStyle(sTab);
        sTabOn.normal.background = tRed; sTabOn.normal.textColor = Color.white;

        sListItem = new GUIStyle(sButton) { fontSize = 21, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, wordWrap = false, clipping = TextClipping.Clip };
        sListItem.padding = new RectOffset(14, 10, 4, 4);
        sListItemOn = new GUIStyle(sListItem);
        sListItemOn.normal.background = tRedDark; sListItemOn.normal.textColor = Color.white;

        sSliderTrack = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 8f, margin = new RectOffset(0, 0, 11, 11) };
        sSliderTrack.normal.background = Tex(new Color(0.20f, 0.13f, 0.09f, 1f));
        sSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 18f, fixedHeight = 30f };
        sSliderThumb.normal.background = tRed; sSliderThumb.hover.background = Tex(new Color(0.75f, 0.33f, 0.14f, 1f)); sSliderThumb.active.background = sSliderThumb.hover.background;

        sBox = new GUIStyle(GUI.skin.box);
        sBox.normal.background = tPanel;

        sToastTitle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        sToastTitle.normal.textColor = Blood;
        sToastText = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true };
        sToastText.normal.textColor = Bone;
    }

    private bool Button(Rect r, string text) => GUI.Button(r, text, sButton);

    private void Message(string text)
    {
        panelMessage = text;
        panelMessageTime = Time.unscaledTime;
    }

    // ============================================================================================ OnGUI
    void OnGUI()
    {
        if (mode == Mode.None) return;
        BuildStyles();

        float scale = Screen.height / VH;
        if (scale <= 0f) return;
        vw = Screen.width / scale;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        GUI.depth = -1000;

        if (summary != null) { DrawSummary(); return; }

        if (mode == Mode.Game)
        {
            if (!paused) DrawHud();
            else DrawPanel();
        }
        else if (mode == Mode.Menu)
        {
            if (panel != Panel.None) DrawPanel();   // the three menu buttons are real scene objects now (see MainMenuSetup)
        }
        DrawToasts();
    }

    // ------------------------------------------------------------------------------------ HUD
    private void DrawHud()
    {
        if (player == null || !player.enabled || player.isDead || player.isCinematicPlaying) return;
        ObjectiveTracker tracker = ObjectiveTracker.Instance;
        if (tracker == null) return;

        ObjectiveTracker.Objective cur = tracker.Current();
        float w = 470f, x = vw - w - 24f, y = 24f;

        // "ESCAPE THE BASEMENT: find the key..." -> the chapter becomes the header, the task the text
        string header = "OBJECTIVE   " + tracker.DoneCount() + "/" + tracker.Objectives.Count;
        if (tracker.IsGated) header = "OBJECTIVE";
        string body = cur != null ? tracker.TitleWithProgress(cur) : "Survive.";
        int colon = body.IndexOf(": ");
        if (colon > 0 && body.Substring(0, colon) == body.Substring(0, colon).ToUpper())
        {
            header = body.Substring(0, colon);
            body = body.Substring(colon + 2);
            if (body.Length > 0) body = char.ToUpper(body[0]) + body.Substring(1);
        }

        float th = Mathf.Max(28f, sToastText.CalcHeight(new GUIContent(body), w - 24f));
        float h = 30f + th + 30f;
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, w, h), tBlack);
        GUI.DrawTexture(new Rect(x, y, 4f, h), tRed);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 16f, y + 6f, w - 24f, 26f), header, sToastTitle);
        GUI.Label(new Rect(x + 16f, y + 30f, w - 24f, th), body, sToastText);
        GUI.Label(new Rect(x + 16f, y + 32f + th, w - 24f, 24f), "TAB  Journal      ESC  Menu", sSmall);
        hudBottom = y + h;
    }

    private float hudBottom = 122f;

    private void DrawToasts()
    {
        if (toasts.Count == 0) return;
        float w = 470f, x = vw - w - 24f, y = mode == Mode.Game && !paused ? hudBottom + 14f : 24f;
        for (int i = 0; i < toasts.Count; i++)
        {
            ToastMsg t = toasts[i];
            if (t.start < 0f) t.start = Time.unscaledTime;
            float age = Time.unscaledTime - t.start;
            if (age > ToastTime) { toasts.RemoveAt(i); i--; continue; }
            float a = Mathf.Clamp01(Mathf.Min(age * 4f, (ToastTime - age) * 2f));
            GUI.color = new Color(1f, 1f, 1f, a);
            float tth = Mathf.Max(44f, sToastText.CalcHeight(new GUIContent(t.text), w - 24f));
            float tbh = 30f + tth + 4f;
            GUI.DrawTexture(new Rect(x, y, w, tbh), tPanel);
            GUI.DrawTexture(new Rect(x, y, 4f, tbh), t.gold ? tGold : tRed);
            Color old = sToastTitle.normal.textColor;
            if (t.gold) sToastTitle.normal.textColor = Gold;
            GUI.Label(new Rect(x + 16f, y + 6f, w - 24f, 24f), t.title, sToastTitle);
            sToastTitle.normal.textColor = old;
            GUI.Label(new Rect(x + 16f, y + 28f, w - 24f, tth), t.text, sToastText);
            GUI.color = Color.white;
            y += tbh + 8f;
        }
    }

    // ------------------------------------------------------------------------------------ main menu
    // ------------------------------------------------------------------------------------ panels
    private Rect Window(float w, float h)
    {
        GUI.DrawTexture(new Rect(0, 0, vw, VH), tBlack);
        Rect r = new Rect((vw - w) * 0.5f, (VH - h) * 0.5f, w, h);
        GUI.Box(r, GUIContent.none, sBox);
        // rusty double frame + accent bar on top
        Color rust = new Color(0.42f, 0.22f, 0.12f, 1f);
        Color rustDim = new Color(0.25f, 0.13f, 0.08f, 1f);
        Color old = GUI.color;
        GUI.color = rust;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), tWhite);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 3f, r.width, 3f), tWhite);
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), tWhite);
        GUI.DrawTexture(new Rect(r.xMax - 3f, r.y, 3f, r.height), tWhite);
        GUI.color = rustDim;
        GUI.DrawTexture(new Rect(r.x + 10f, r.y + 10f, r.width - 20f, 1.5f), tWhite);
        GUI.DrawTexture(new Rect(r.x + 10f, r.yMax - 11.5f, r.width - 20f, 1.5f), tWhite);
        GUI.DrawTexture(new Rect(r.x + 10f, r.y + 10f, 1.5f, r.height - 20f), tWhite);
        GUI.DrawTexture(new Rect(r.xMax - 11.5f, r.y + 10f, 1.5f, r.height - 20f), tWhite);
        GUI.color = old;
        return r;
    }

    // ---- headings: the same rusty 3D lettering as the main menu (falls back to plain text if a sprite is missing)
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> headTex = new System.Collections.Generic.Dictionary<string, Texture2D>();

    private void Heading(Rect area, string text, GUIStyle fallback)
    {
        string key = text.ToLowerInvariant().Replace(' ', '_');
        if (!headTex.TryGetValue(key, out Texture2D t))
        {
            t = Resources.Load<Texture2D>("MenuButtons/title_" + key);
            headTex[key] = t;
        }
        if (t == null) { GUI.Label(area, text, fallback); return; }
        float h = Mathf.Min(area.height, 78f);
        float w = h * t.width / t.height;
        GUI.DrawTexture(new Rect(area.center.x - w * 0.5f, area.center.y - h * 0.5f, w, h), t, ScaleMode.StretchToFill, true);
    }

    /// <summary>Dark card with a thin rust (or ember-gold when highlighted) outline and a coloured stripe on the left.</summary>
    private void Card(Rect r, bool hi)
    {
        GUI.DrawTexture(r, hi ? tCardHi : tCard);
        Color old = GUI.color;
        GUI.color = hi ? new Color(0.90f, 0.62f, 0.20f, 0.55f) : new Color(0.45f, 0.22f, 0.11f, 0.60f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), tWhite);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), tWhite);
        GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), tWhite);
        GUI.color = hi ? new Color(1f, 0.70f, 0.22f, 1f) : new Color(0.50f, 0.20f, 0.10f, 1f);
        GUI.DrawTexture(new Rect(r.x, r.y, 5f, r.height), tWhite);
        GUI.color = old;
    }

    private void DrawPanel()
    {
        GUISkin oldSkin = GUI.skin;
        if (sSkin != null) GUI.skin = sSkin;
        DrawPanelInner();
        GUI.skin = oldSkin;
    }

    private void DrawPanelInner()
    {
        switch (panel)
        {
            case Panel.Pause: DrawPause(); break;
            case Panel.Save: DrawSlots(true); break;
            case Panel.Load: DrawSlots(false); break;
            case Panel.Settings: DrawSettings(); break;
            case Panel.Journal: DrawJournal(); break;
            case Panel.Achievements: DrawAchievements(); break;
            case Panel.ConfirmMenu: DrawConfirm("Return to the main menu?", "Your progress will be saved at your last safe position.", LeaveToMainMenu); break;
            case Panel.ConfirmQuit: DrawConfirm("Quit the game?", "Your progress will be saved at your last safe position.", QuitGame); break;
            default: if (mode == Mode.Game) DrawPause(); break;
        }
    }

    private void DrawPause()
    {
        Rect r = Window(560f, 820f);
        Heading(new Rect(r.x, r.y + 20f, r.width, 80f), "PAUSED", sTitle);
        string[] labels = { "RESUME", "SAVE GAME", "LOAD GAME", "JOURNAL", "ACHIEVEMENTS", "SETTINGS", "MAIN MENU", "QUIT GAME" };
        float bx = r.x + 80f, bw = r.width - 160f, by = r.y + 120f, bh = 58f;
        for (int i = 0; i < labels.Length; i++)
        {
            if (!Button(new Rect(bx, by + i * (bh + 10f), bw, bh), labels[i])) continue;
            switch (i)
            {
                case 0: Resume(); break;
                case 1: OpenPanel(Panel.Save, Panel.Pause); break;
                case 2: OpenPanel(Panel.Load, Panel.Pause); break;
                case 3: OpenPanel(Panel.Journal, Panel.Pause); journalTab = 0; break;
                case 4: OpenPanel(Panel.Achievements, Panel.Pause); break;
                case 5: OpenPanel(Panel.Settings, Panel.Pause); break;
                case 6: OpenPanel(Panel.ConfirmMenu, Panel.Pause); break;
                case 7: OpenPanel(Panel.ConfirmQuit, Panel.Pause); break;
            }
        }
        ObjectiveTracker t = ObjectiveTracker.Instance;
        ObjectiveTracker.Objective cur = t != null ? t.Current() : null;
        if (cur != null) GUI.Label(new Rect(r.x + 30f, r.yMax - 66f, r.width - 60f, 56f), "<b>Objective:</b> " + t.TitleWithProgress(cur), sSmall);
    }

    private void DrawConfirm(string question, string info, System.Action onYes)
    {
        Rect r = Window(700f, 300f);
        GUI.Label(new Rect(r.x, r.y + 30f, r.width, 60f), question, sHeader.Center());
        GUI.Label(new Rect(r.x + 40f, r.y + 100f, r.width - 80f, 60f), info, sCenter);
        if (Button(new Rect(r.x + 80f, r.yMax - 100f, 240f, 60f), "YES")) onYes();
        if (Button(new Rect(r.xMax - 320f, r.yMax - 100f, 240f, 60f), "NO")) OpenPanel(Panel.Pause, Panel.Pause);
    }

    private void DrawBackButton(Rect r)
    {
        if (Button(new Rect(r.x + (r.width - 260f) * 0.5f, r.yMax - 80f, 260f, 56f), "BACK")) Back();
    }

    private void DrawPanelMessage(Rect r, float y)
    {
        if (string.IsNullOrEmpty(panelMessage) || Time.unscaledTime - panelMessageTime > 6f) return;
        GUI.Label(new Rect(r.x + 30f, y, r.width - 60f, 60f), panelMessage, sCenter);
    }

    // ---- save / load (FR_38, FR_42)
    private int confirmOverwrite = -1;

    private void DrawSlots(bool saving)
    {
        Rect r = Window(1000f, 700f);
        Heading(new Rect(r.x, r.y + 20f, r.width, 70f), saving ? "SAVE GAME" : "LOAD GAME", sBig);
        float y = r.y + 110f;
        int first = saving ? 1 : 0;
        for (int slot = first; slot <= SaveSlots.SlotCount; slot++)
        {
            SaveSlots.SlotInfo info = SaveSlots.GetInfo(slot);
            Rect row = new Rect(r.x + 40f, y, r.width - 80f, 92f);
            Card(row, info.exists);
            Rect badge = new Rect(row.x + 22f, row.y + 16f, 60f, 60f);
            GUI.DrawTexture(badge, tBlack);
            GUI.Label(badge, slot == 0 ? "A" : slot.ToString(), sBig.Sized(34));
            GUI.Label(new Rect(row.x + 100f, row.y + 8f, 600f, 36f), info.label, sHeader);
            GUI.Label(new Rect(row.x + 100f, row.y + 48f, 600f, 36f), info.details, sSmall);

            Rect btn = new Rect(row.xMax - 200f, row.y + 18f, 180f, 56f);
            if (saving)
            {
                string label = confirmOverwrite == slot ? "SURE?" : (info.exists ? "OVERWRITE" : "SAVE");
                if (Button(btn, label))
                {
                    if (info.exists && confirmOverwrite != slot) confirmOverwrite = slot;
                    else
                    {
                        confirmOverwrite = -1;
                        SaveSlots.SaveToSlot(slot, out string msg);
                        Message(msg);
                    }
                }
            }
            else
            {
                GUI.enabled = info.exists && info.valid;
                if (Button(btn, "LOAD"))
                {
                    if (SaveSlots.LoadSlot(slot, out string msg))
                    {
                        paused = false;               // the level reloads - nothing to restore
                        disabledWhilePaused.Clear();
                    }
                    else Message(msg);
                }
                GUI.enabled = true;
            }
            y += 104f;
        }
        if (!saving) GUI.Label(new Rect(r.x + 40f, y + 4f, r.width - 80f, 30f), "Loading discards anything you have not saved.", sSmall);
        DrawPanelMessage(r, r.yMax - 150f);
        DrawBackButton(r);
    }

    // ---- settings (FR_47)
    private void DrawSettings()
    {
        Rect r = Window(900f, 640f);
        Heading(new Rect(r.x, r.y + 20f, r.width, 70f), "SETTINGS", sBig);
        float x = r.x + 60f, y = r.y + 120f, lw = 300f, sw = r.width - 120f - lw - 110f;

        GameSettings.SetMasterVolume(Slider(x, ref y, lw, sw, "Master Volume", GameSettings.MasterVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%"));
        GameSettings.SetBrightness(Slider(x, ref y, lw, sw, "Brightness", GameSettings.Brightness, -1f, 1.5f, (GameSettings.Brightness >= 0 ? "+" : "") + GameSettings.Brightness.ToString("0.0")));
        GameSettings.SetEffectsIntensity(Slider(x, ref y, lw, sw, "Camera Effects (head-bob)", GameSettings.EffectsIntensity, 0f, 1f, Mathf.RoundToInt(GameSettings.EffectsIntensity * 100f) + "%"));
        GameSettings.SetMouseSensitivity(Slider(x, ref y, lw, sw, "Mouse Sensitivity", GameSettings.MouseSensitivity, 0.3f, 2.5f, GameSettings.MouseSensitivity.ToString("0.0") + "x"));

        if (Button(new Rect(r.x + (r.width - 300f) * 0.5f, y + 10f, 300f, 52f), "RESET DEFAULTS")) GameSettings.ResetToDefaults();
        GUI.Label(new Rect(r.x + 40f, y + 72f, r.width - 80f, 30f), "Settings are saved automatically.", sSmall.Center());
        DrawBackButton(r);
    }

    private float Slider(float x, ref float y, float lw, float sw, string label, float value, float min, float max, string shown)
    {
        Card(new Rect(x - 20f, y - 8f, lw + sw + 140f, 58f), false);
        GUI.Label(new Rect(x, y, lw, 40f), label, sText);
        float v = GUI.HorizontalSlider(new Rect(x + lw, y + 14f, sw, 30f), value, min, max, sSliderTrack, sSliderThumb);
        GUI.Label(new Rect(x + lw + sw + 20f, y, 100f, 40f), shown, sText);
        y += 70f;
        return v;
    }

    // ---- achievements (FR_52, FR_53)
    private void DrawAchievements()
    {
        Rect r = Window(1240f, 900f);
        IReadOnlyList<GameProgress.AchievementDef> list = GameProgress.Achievements;
        Heading(new Rect(r.x, r.y + 16f, r.width, 70f), "ACHIEVEMENTS", sBig);
        int unlocked = GameProgress.UnlockedCount();
        GUI.Label(new Rect(r.x, r.y + 82f, r.width, 30f),
            unlocked + " / " + list.Count + " unlocked        ·        Endings discovered: " + GameProgress.EndingsSeen() + " / " + GameProgress.EndingCount, sCenter.Sized(21));
        Rect total = new Rect(r.center.x - 220f, r.y + 118f, 440f, 6f);
        GUI.DrawTexture(total, tBlack);
        Color oc = GUI.color; GUI.color = new Color(1f, 0.62f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(total.x, total.y, total.width * unlocked / Mathf.Max(1, list.Count), total.height), tWhite);
        GUI.color = oc;

        SaveData save = GameProgress.CurrentSave;
        Rect view = new Rect(r.x + 30f, r.y + 142f, r.width - 60f, r.height - 242f);
        float rowH = 92f, colW = (view.width - 34f) * 0.5f;
        int rows = (list.Count + 1) / 2;
        Rect content = new Rect(0, 0, view.width - 18f, rows * (rowH + 10f));
        scrollA = GUI.BeginScrollView(view, scrollA, content);
        GUIStyle tStyle = sHeader.Sized(23);
        GUIStyle dStyle = new GUIStyle(sSmall) { fontSize = 18, clipping = TextClipping.Clip };
        GUIStyle chip = new GUIStyle(sSmall) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        for (int i = 0; i < list.Count; i++)
        {
            GameProgress.AchievementDef a = list[i];
            bool got = GameProgress.IsUnlocked(a.id);
            Rect cell = new Rect((i % 2) * (colW + 10f), (i / 2) * (rowH + 10f), colW, rowH);
            Card(cell, got);
            bool secret = !got && a.hidden;
            string title = secret ? "???" : a.title;
            string desc = secret ? "Hidden - keep playing to discover this one." : a.description;

            tStyle.normal.textColor = got ? new Color(1f, 0.86f, 0.52f) : new Color(0.80f, 0.74f, 0.68f);
            GUI.Label(new Rect(cell.x + 22f, cell.y + 8f, cell.width - 150f, 32f), title, tStyle);
            chip.normal.textColor = got ? new Color(1f, 0.70f, 0.22f) : new Color(0.50f, 0.42f, 0.38f);
            GUI.Label(new Rect(cell.xMax - 130f, cell.y + 12f, 116f, 22f), got ? "UNLOCKED" : "LOCKED", chip);

            bool side = got || (a.progress != null && save != null);
            GUI.Label(new Rect(cell.x + 22f, cell.y + 42f, cell.width - 44f - (side ? 150f : 0f), 46f), desc, dStyle);

            if (got)
            {
                GUI.Label(new Rect(cell.xMax - 150f, cell.y + 62f, 136f, 24f), GameProgress.UnlockDate(a.id), sSmall.Right().Sized(16));
            }
            else if (a.progress != null && save != null)
            {
                int target = GameProgress.TargetOf(a);
                int cur = Mathf.Min(target, a.progress(save));
                Rect bar = new Rect(cell.xMax - 150f, cell.y + 74f, 134f, 8f);
                GUI.DrawTexture(bar, tBlack);
                Color pc = GUI.color; GUI.color = new Color(0.78f, 0.30f, 0.12f, 1f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * cur / Mathf.Max(1, target), bar.height), tWhite);
                GUI.color = pc;
                GUI.Label(new Rect(bar.x, bar.y - 28f, bar.width, 24f), cur + " / " + target, sSmall.Right().Sized(16));
            }
        }
        GUI.EndScrollView();
        DrawBackButton(r);
    }

    // ---- journal (FR_34, FR_35, FR_37, FR_49, FR_51)
    private void DrawJournal()
    {
        Rect r = Window(1400f, 900f);
        Heading(new Rect(r.x, r.y + 14f, r.width, 70f), "JOURNAL", sBig);
        string[] tabs = { "OBJECTIVES", "ITEMS", "NOTES", "STATISTICS" };
        float tw = 260f, tx = r.x + (r.width - tabs.Length * (tw + 10f)) * 0.5f;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (GUI.Button(new Rect(tx + i * (tw + 10f), r.y + 90f, tw, 48f), tabs[i], journalTab == i ? sTabOn : sTab))
            {
                journalTab = i;
                scrollA = scrollB = Vector2.zero;
            }
        }

        Rect body = new Rect(r.x + 40f, r.y + 160f, r.width - 80f, r.height - 260f);
        switch (journalTab)
        {
            case 0: DrawObjectives(body); break;
            case 1: DrawItems(body); break;
            case 2: DrawNotes(body); break;
            case 3: DrawStats(body); break;
        }
        GUI.Label(new Rect(r.x + 40f, r.yMax - 70f, 420f, 40f), "TAB  close", sSmall);
        DrawBackButton(r);
    }

    private void DrawObjectives(Rect body)
    {
        ObjectiveTracker t = ObjectiveTracker.Instance;
        if (t == null) { GUI.Label(body, "No objectives yet.", sText); return; }

        List<ObjectiveTracker.Objective> shown = new List<ObjectiveTracker.Objective>();
        foreach (ObjectiveTracker.Objective o in t.Objectives) if (o.discovered || o.done) shown.Add(o);
        // unfinished first, then finished
        shown.Sort((a, b) => a.done == b.done ? a.order.CompareTo(b.order) : (a.done ? 1 : -1));

        Rect content = new Rect(0, 0, body.width - 24f, (shown.Count + 2) * 52f);
        scrollA = GUI.BeginScrollView(body, scrollA, content);
        float y = 0f;
        foreach (ObjectiveTracker.Objective o in shown)
        {
            GUI.DrawTexture(new Rect(0, y, content.width, 46f), o.done ? tRedDark : tGray);
            GUI.Label(new Rect(16f, y + 6f, 130f, 34f), o.done ? "DONE" : "OPEN", o.done ? sSmall : sToastTitle);
            GUI.Label(new Rect(130f, y + 4f, content.width - 150f, 38f), o.done ? "<color=#9a9088>" + t.TitleWithProgress(o) + "</color>" : t.TitleWithProgress(o), sText);
            y += 52f;
        }
        int hidden = t.UndiscoveredCount();
        if (hidden > 0) GUI.Label(new Rect(16f, y + 8f, content.width, 34f), "??? " + hidden + " more objective(s) still to discover - explore the asylum.", sSmall);
        GUI.EndScrollView();
    }

    private void DrawItems(Rect body)
    {
        SaveData d = GameProgress.CurrentSave;
        List<string> items = d != null ? SaveManager.SplitList(d.itemsFoundCSV) : new List<string>();
        items.Sort(System.StringComparer.OrdinalIgnoreCase); // organised alphabetically (FR_37)

        string inHand = "";
        if (player != null)
        {
            Inventory inv = player.GetComponent<Inventory>();
            if (inv != null) inHand = inv.GetHeldItemDisplayName();
        }

        GUI.Label(new Rect(body.x, body.y, body.width, 36f), "In your hand:  <b>" + (string.IsNullOrEmpty(inHand) ? "nothing" : inHand) + "</b>      (G to drop)", sText);

        Rect list = new Rect(body.x, body.y + 50f, 440f, body.height - 50f);
        Rect detail = new Rect(list.xMax + 30f, list.y, body.width - list.width - 30f, list.height);
        GUI.DrawTexture(detail, tGray);

        if (items.Count == 0)
        {
            GUI.Label(list, "You have not picked anything up yet.", sSmall);
            return;
        }
        if (selectedItem >= items.Count) selectedItem = -1;
        if (selectedItem < 0 && !string.IsNullOrEmpty(inHand)) selectedItem = items.FindIndex(s => string.Equals(s, inHand, System.StringComparison.OrdinalIgnoreCase));

        Rect content = new Rect(0, 0, list.width - 24f, items.Count * 48f);
        scrollA = GUI.BeginScrollView(list, scrollA, content);
        for (int i = 0; i < items.Count; i++)
        {
            bool held = string.Equals(items[i], inHand, System.StringComparison.OrdinalIgnoreCase);
            if (GUI.Button(new Rect(0, i * 48f, content.width, 42f), (held ? "> " : "") + items[i], selectedItem == i ? sListItemOn : sListItem)) selectedItem = i;
        }
        GUI.EndScrollView();

        if (selectedItem >= 0 && selectedItem < items.Count)
        {
            string name = items[selectedItem];
            GUI.Label(new Rect(detail.x + 24f, detail.y + 20f, detail.width - 48f, 44f), name.ToUpperInvariant(), sHeader);
            GUI.Label(new Rect(detail.x + 24f, detail.y + 76f, detail.width - 48f, 200f), GameProgress.DescribeItem(name), sText);
            bool held = string.Equals(name, inHand, System.StringComparison.OrdinalIgnoreCase);
            GUI.Label(new Rect(detail.x + 24f, detail.yMax - 60f, detail.width - 48f, 40f), held ? "You are holding this." : "You only have one free hand - pick it up again where you left it.", sSmall);
        }
        else GUI.Label(new Rect(detail.x + 24f, detail.y + 20f, detail.width - 48f, 60f), "Select an item to inspect it.", sSmall);
    }

    private class NoteEntry { public string id, text, title; }
    private List<NoteEntry> noteCache;
    private int noteCacheFrame = -1;

    private List<NoteEntry> ReadNotes()
    {
        if (noteCache != null && Time.frameCount - noteCacheFrame < 30) return noteCache;
        noteCacheFrame = Time.frameCount;
        noteCache = new List<NoteEntry>();
        SaveData d = GameProgress.CurrentSave;
        if (d == null) return noteCache;
        Dictionary<string, string> texts = new Dictionary<string, string>();
        foreach (ReadableNote n in FindObjectsByType<ReadableNote>(FindObjectsInactive.Include))
        {
            string id = SaveManager.GetSceneObjectId(n.transform);
            if (!texts.ContainsKey(id)) texts[id] = n.noteText;
        }
        foreach (string id in SaveManager.SplitList(d.notesReadCSV))
            if (texts.TryGetValue(id, out string text)) noteCache.Add(new NoteEntry { id = id, text = text });

        // hidden lore (FR_64) comes after the notes
        List<GameProgress.LoreEntry> lore = GameProgress.AllLoreInScene();
        foreach (string id in SaveManager.SplitList(d.loreFoundCSV))
        {
            GameProgress.LoreEntry e = lore.Find(l => l.id == id);
            if (e != null) noteCache.Add(new NoteEntry { id = id, text = e.text, title = e.title });
        }
        return noteCache;
    }

    private void DrawNotes(Rect body)
    {
        List<NoteEntry> notes = ReadNotes();
        int total = GameProgress.CountNotesInScene();
        int loreTotal = GameProgress.AllLoreInScene().Count;
        GUI.Label(new Rect(body.x, body.y, body.width, 36f), "Notes found: " + GameProgress.NotesRead() + " / " + total +
            "        Hidden lore: " + GameProgress.LoreFoundCount() + " / " + loreTotal, sText);

        Rect list = new Rect(body.x, body.y + 50f, 440f, body.height - 50f);
        Rect page = new Rect(list.xMax + 30f, list.y, body.width - list.width - 30f, list.height);
        GUI.DrawTexture(page, tGray);

        if (notes.Count == 0)
        {
            GUI.Label(list, "You have not read any notes yet. Look for papers on desks, walls and bodies.", sSmall);
            return;
        }
        if (selectedNote < 0 || selectedNote >= notes.Count) selectedNote = notes.Count - 1;

        Rect content = new Rect(0, 0, list.width - 24f, notes.Count * 48f);
        scrollA = GUI.BeginScrollView(list, scrollA, content);
        for (int i = 0; i < notes.Count; i++)
        {
            string preview = notes[i].title ?? notes[i].text.Replace("\n", " ");
            if (preview.Length > 30) preview = preview.Substring(0, 30) + "...";
            string label = notes[i].title != null ? "*  " + preview : (i + 1) + ".  " + preview;
            if (GUI.Button(new Rect(0, i * 48f, content.width, 42f), label, selectedNote == i ? sListItemOn : sListItem)) selectedNote = i;
        }
        GUI.EndScrollView();

        string heading = notes[selectedNote].title != null ? "HIDDEN LORE - " + notes[selectedNote].title.ToUpperInvariant() : "NOTE " + (selectedNote + 1);
        GUI.Label(new Rect(page.x + 24f, page.y + 20f, page.width - 48f, 40f), heading, sHeader);
        GUI.Label(new Rect(page.x + 24f, page.y + 76f, page.width - 48f, page.height - 100f), "<i>\"" + notes[selectedNote].text + "\"</i>", sText);
    }

    private void DrawStats(Rect body)
    {
        SaveData d = GameProgress.CurrentSave;
        if (d == null) { GUI.Label(body, "No statistics yet.", sText); return; }
        ObjectiveTracker t = ObjectiveTracker.Instance;
        string[,] rows =
        {
            { "Time played", SaveSlots.FormatTime(d.playTime) },
            { "Lives left", d.lives + " / 3" },
            { "Deaths", GameProgress.Deaths(d).ToString() },
            { "Objectives completed", (t != null ? t.DoneCount() + " / " + t.Objectives.Count : GameProgress.CountSolvedObjectives(d).ToString()) },
            { "Doors unlocked", SaveManager.CountList(d.unlockedDoorsCSV).ToString() },
            { "Notes read", GameProgress.NotesRead() + " / " + GameProgress.CountNotesInScene() },
            { "Hidden lore found", GameProgress.LoreFoundCount() + " / " + GameProgress.AllLoreInScene().Count },
            { "Different items found", SaveManager.CountList(d.itemsFoundCSV).ToString() },
            { "Times hidden", d.timesHidden.ToString() },
            { "Taser shots", d.taserShots.ToString() },
            { "Adaptive AI - your skill class", d.skillClass + "   (comfort score " + Mathf.RoundToInt(d.comfortScore) + ")" },
            { "Adaptive AI - enemy aggression", d.aggressionLevel + " / 5" },
            { "Achievements", GameProgress.UnlockedCount() + " / " + GameProgress.Achievements.Count },
        };
        float y = body.y;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            GUI.DrawTexture(new Rect(body.x, y, body.width, 42f), i % 2 == 0 ? tGray : tRedDark);
            GUI.Label(new Rect(body.x + 20f, y + 2f, 520f, 38f), rows[i, 0], sText);
            GUI.Label(new Rect(body.x + 540f, y + 2f, body.width - 560f, 38f), "<b>" + rows[i, 1] + "</b>", sText);
            y += 46f;
        }
    }

    // ---- ending summary (FR_56)
    private void DrawSummary()
    {
        GameProgress.RunSummary s = summary;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, vw, VH), Tex2Black());
        float w = Mathf.Min(1300f, vw - 80f), x = (vw - w) * 0.5f;

        GUI.Label(new Rect(x, 50f, w, 80f), s.title, sTitle);
        GUI.Label(new Rect(x, 130f, w, 40f), "<i>" + s.tagline + "</i>", sCenter);
        GUI.Label(new Rect(x + 60f, 185f, w - 120f, 110f), s.description, sCenter);
        GUI.Label(new Rect(x + 60f, 295f, w - 120f, 40f), "<b>Why this ending:</b> " + s.reason, sSmall.Center());

        string[,] rows =
        {
            { "Time", SaveSlots.FormatTime(s.playTime) },
            { "Deaths", s.deaths + " / 3" },
            { "Objectives", s.objectivesDone + " / " + s.objectivesTotal },
            { "Notes read", s.notesRead + " / " + s.notesTotal },
            { "Doors unlocked", s.doorsUnlocked.ToString() },
            { "Items found", s.itemsFound.ToString() },
            { "Times hidden", s.timesHidden.ToString() },
            { "Taser shots", s.taserShots.ToString() },
            { "Final skill class", s.skillClass + "  (aggression " + s.aggression + "/5)" },
            { "Achievements", s.achievementsUnlocked + " / " + s.achievementsTotal },
            { "Endings discovered", s.endingsSeen + " / " + GameProgress.EndingCount },
        };
        float tw = 760f, tx = (vw - tw) * 0.5f, y = 355f;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            GUI.DrawTexture(new Rect(tx, y, tw, 40f), i % 2 == 0 ? tGray : tRedDark);
            GUI.Label(new Rect(tx + 20f, y + 2f, 340f, 36f), rows[i, 0], sText);
            GUI.Label(new Rect(tx + 360f, y + 2f, tw - 380f, 36f), "<b>" + rows[i, 1] + "</b>", sText);
            y += 43f;
        }

        if (s.newAchievements.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (string id in s.newAchievements)
            {
                foreach (GameProgress.AchievementDef a in GameProgress.Achievements) if (a.id == id) names.Add(a.title);
            }
            GUI.Label(new Rect(x, y + 8f, w, 40f), "<color=#f2c040>New achievements: " + string.Join(", ", names.ToArray()) + "</color>", sCenter);
        }

        if (Button(new Rect((vw - 340f) * 0.5f, VH - 100f, 340f, 62f), "CONTINUE")) summary = null;
    }

    private Texture2D solidBlack;
    private Texture2D Tex2Black()
    {
        if (solidBlack == null) solidBlack = Tex(new Color(0f, 0f, 0f, 0.97f));
        return solidBlack;
    }
}

/// <summary>Small helpers to derive GUIStyles without repeating code.</summary>
internal static class GUIStyleExt
{
    public static GUIStyle Center(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };
    public static GUIStyle Right(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleRight };
    public static GUIStyle Sized(this GUIStyle s, int size) => new GUIStyle(s) { fontSize = size };
}
