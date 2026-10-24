using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// THE GAME'S MENUS AND HUD LOGIC. The screens themselves are real uGUI objects in the Hierarchy (TLC_UI canvas, see TLCCanvas);
/// this script decides what is shown and fills in the texts.
///   In the game level:
///     ESC  - Pause menu: Resume / Save (3 slots) / Load / Journal / Achievements / Settings / Main Menu / Quit   (FR_38, FR_42, FR_47)
///     TAB  - Journal: Objectives / Items (inspect) / Notes (re-read) / Statistics                               (FR_34, FR_35, FR_37, FR_49, FR_51)
///     HUD  - current objective (top-right) + pop-ups for new objectives, completed objectives and achievements (FR_49, FR_52)
///     Ending summary screen after the escape or the final death                                                 (FR_56)
///   In the main menu: LOAD GAME / ACHIEVEMENTS / OPTIONS panels                                                  (FR_42, FR_53)
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
    private enum Panel { None, Pause, Save, Load, Settings, Journal, Achievements, ConfirmMenu, ConfirmQuit, Controls }

    private Mode mode = Mode.None;
    private Panel panel = Panel.None;
    private Panel returnPanel = Panel.Pause;
    private bool paused;
    private float savedTimeScale = 1f;
    private readonly List<Behaviour> disabledWhilePaused = new List<Behaviour>();
    private bool keypadOpenLastFrame;
    private int journalTab;
    private int selectedItem = -1, selectedNote = -1;
    private string panelMessage = "";
    private float panelMessageTime;
    private PlayerController player;
    private TLCCanvas ui;
    private bool controlsChecked;

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode) { SetupScene(); }

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

        ui = null;
        if (mode != Mode.None) BindUI();

        if (mode == Mode.Menu)
        {
            HookMenuSliders();
            PrepareMenu();
        }

        if (mode == Mode.Game && ObjectiveTracker.Instance == null)
        {
            GameObject go = new GameObject("TLC_ObjectiveTracker");
            go.AddComponent<ObjectiveTracker>();
            go.AddComponent<HorrorDirector>();
            go.AddComponent<HudBarStyler>();
            go.AddComponent<ScareDirector>();
            go.AddComponent<AmbientMusic>();
            go.AddComponent<PresentationMode>();
        }
        controlsChecked = false;
    }

    public static void OpenFromMenu(int which)
    {
        if (instance == null) Boot();
        if (instance == null || instance.mode != Mode.Menu) return;
        Panel[] targets = { Panel.Load, Panel.Achievements, Panel.Settings };
        instance.OpenPanel(targets[Mathf.Clamp(which, 0, 2)], Panel.None);
    }

    public static bool IsMenuPanelOpen { get { return instance != null && instance.mode == Mode.Menu && instance.panel != Panel.None; } }

    private GraphicRaycaster[] menuRays;
    private float raycastReenableAt;

    private void PrepareMenu()
    {
        List<GraphicRaycaster> list = new List<GraphicRaycaster>();
        foreach (GraphicRaycaster gr in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include))
            if (gr.GetComponent<TLCCanvas>() == null) list.Add(gr);    // never switch off our own canvas
        menuRays = list.ToArray();
        raycastReenableAt = 0f;
    }

    private void HookMenuSliders()
    {
        foreach (Slider slider in FindObjectsByType<Slider>(FindObjectsInactive.Include))
        {
            if (slider.GetComponentInParent<TLCCanvas>(true) != null) continue;
            string n = slider.gameObject.name.ToLowerInvariant();
            Slider sl = slider;
            if (n.Contains("volume"))
            {
                sl.SetValueWithoutNotify(Mathf.Lerp(sl.minValue, sl.maxValue, GameSettings.MasterVolume));
                sl.onValueChanged.AddListener(v => { GameSettings.SetMasterVolume(Mathf.InverseLerp(sl.minValue, sl.maxValue, v)); GameSettings.Save(); });
            }
            else if (n.Contains("sensitivity"))
            {
                sl.SetValueWithoutNotify(Mathf.Lerp(sl.minValue, sl.maxValue, Mathf.InverseLerp(0.3f, 2.5f, GameSettings.MouseSensitivity)));
                sl.onValueChanged.AddListener(v => { GameSettings.SetMouseSensitivity(Mathf.Lerp(0.3f, 2.5f, Mathf.InverseLerp(sl.minValue, sl.maxValue, v))); GameSettings.Save(); });
            }
        }
    }

    // ============================================================================================ toasts
    private class ToastMsg { public string title, text; public float start; public bool gold; public CanvasGroup view; }
    private static readonly List<ToastMsg> toasts = new List<ToastMsg>();
    private const float ToastTime = 4.5f;

    public static void Toast(string title, string text, bool gold = false)
    {
        if (toasts.Count > 6) { if (toasts[0].view != null) Destroy(toasts[0].view.gameObject); toasts.RemoveAt(0); }
        toasts.Add(new ToastMsg { title = title, text = text, gold = gold, start = -1f });
    }

    // ============================================================================================ ending summary (FR_56)
    private static GameProgress.RunSummary summary;
    public static bool IsSummaryOpen => summary != null;

    public static void ShowEndingSummary(GameProgress.RunSummary s)
    {
        if (instance == null) Boot();
        summary = s;
        if (instance != null) instance.FillSummary();
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
        if (PresentationMode.IsOpen || SceneLoader.IsLoading) return false;
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
        if (b != null && b.enabled) { b.enabled = false; disabledWhilePaused.Add(b); }
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
        confirmOverwrite = -1;
        FillPanel();
    }

    private void Back()
    {
        if (panel == Panel.Settings) GameSettings.Save();
        if (mode == Mode.Game)
        {
            if (panel == Panel.Pause || panel == Panel.Controls || (panel == Panel.Journal && returnPanel == Panel.None)) Resume();
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
        SceneLoader.Load("main menue");
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
            else if (Input.GetKeyDown(KeyCode.F1))
            {
                if (paused && panel == Panel.Controls) Resume();
                else if (!paused && CanPause()) { Pause(Panel.Controls); returnPanel = Panel.None; }
            }
            else if (!controlsChecked && !paused && Time.timeSinceLevelLoad > 2f && CanPause() && !player.IsWakingUp)
            {
                // CONTROLS SCREEN: shown once, the first time the player is in control
                controlsChecked = true;
                if (PlayerPrefs.GetInt("TLC_ControlsSeen", 0) == 0)
                {
                    PlayerPrefs.SetInt("TLC_ControlsSeen", 1); PlayerPrefs.Save();
                    Pause(Panel.Controls); returnPanel = Panel.None;
                }
            }
            else if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.J))
            {
                if (paused && panel == Panel.Journal && returnPanel == Panel.None) Resume();
                else if (!paused && CanPause()) { journalTab = 0; Pause(Panel.Journal); returnPanel = Panel.None; }
            }

            if (paused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Time.timeScale != 0f) Time.timeScale = 0f;
            }
            keypadOpenLastFrame = keypadOpen;
        }
        else if (mode == Mode.Menu)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && panel != Panel.None) Back();

            bool open = panel != Panel.None;
            if (open) raycastReenableAt = Time.unscaledTime + 0.25f;
            bool want = !open && Time.unscaledTime >= raycastReenableAt;
            if (menuRays != null)
                foreach (GraphicRaycaster gr in menuRays)
                    if (gr != null && gr.enabled != want) gr.enabled = want;
        }
    }

    private float lastUiError = -10f;

    void LateUpdate()
    {
        if (mode == Mode.None) return;
        if (ui == null) BindUI();
        if (ui == null) return;
        try { RefreshUI(); TLCHud.Tick(ui, player, mode == Mode.Game && ui.hud != null && ui.hud.activeInHierarchy); }
        catch (System.Exception e)
        {
            // a broken UI piece must never freeze the game: log at most once every 5 s and carry on
            if (Time.unscaledTime - lastUiError > 5f) { lastUiError = Time.unscaledTime; Debug.LogWarning("[TLC] UI problem (game keeps running): " + e.Message); }
        }
    }

    private void Message(string text)
    {
        panelMessage = text;
        panelMessageTime = Time.unscaledTime;
    }

    // ============================================================================================ wiring the canvas
    private void BindUI()
    {
        ui = TLCCanvas.Ensure();
        if (ui == null) return;

        for (int i = 0; i < ui.pauseButtons.Length; i++) { int k = i; Listen(ui.pauseButtons[i], () => PauseButton(k)); }
        for (int i = 0; i < ui.slotRows.Length; i++) { int s = i; Listen(ui.slotRows[i].button, () => SlotClicked(s)); }
        Listen(ui.slotsBack, Back);
        Listen(ui.settingsBack, Back);
        Listen(ui.achBack, Back);
        Listen(ui.journalBack, Back);
        Listen(ui.settingsReset, () => { GameSettings.ResetToDefaults(); GraphicsQuality.ResetToDefaults(); SyncSettings(); });
        if (ui.graphicsRows != null)
            for (int i = 0; i < ui.graphicsRows.Length; i++)
            {
                int k = i;
                Listen(ui.graphicsRows[i].prev, () => { GraphicsQuality.Cycle(k, -1); RefreshSettingValues(); });
                Listen(ui.graphicsRows[i].next, () => { GraphicsQuality.Cycle(k, +1); RefreshSettingValues(); });
            }
        for (int i = 0; i < ui.journalTabs.Length; i++) { int k = i; Listen(ui.journalTabs[i], () => { journalTab = k; FillJournal(); }); }
        Listen(ui.confirmYes, () => { if (panel == Panel.ConfirmQuit) QuitGame(); else LeaveToMainMenu(); });
        Listen(ui.confirmNo, () => OpenPanel(Panel.Pause, Panel.Pause));
        Listen(ui.sumContinue, () => summary = null);
        Listen(ui.controlsContinue, () => { if (mode == Mode.Game) Resume(); else panel = Panel.None; });

        float[,] ranges = { { 0f, 1f }, { -1f, 1.5f }, { 0f, 1f }, { 0.3f, 2.5f } };
        for (int i = 0; i < ui.settingRows.Length && i < 4; i++)
        {
            Slider s = ui.settingRows[i].slider;
            s.minValue = ranges[i, 0]; s.maxValue = ranges[i, 1];
            s.onValueChanged.RemoveAllListeners();
            int k = i;
            s.onValueChanged.AddListener(v =>
            {
                if (k == 0) GameSettings.SetMasterVolume(v);
                else if (k == 1) GameSettings.SetBrightness(v);
                else if (k == 2) GameSettings.SetEffectsIntensity(v);
                else GameSettings.SetMouseSensitivity(v);
            });
        }
        foreach (ToastMsg t in toasts) t.view = null;
    }

    private static void Listen(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }

    private void PauseButton(int i)
    {
        switch (i)
        {
            case 0: Resume(); break;
            case 1: OpenPanel(Panel.Save, Panel.Pause); break;
            case 2: OpenPanel(Panel.Load, Panel.Pause); break;
            case 3: journalTab = 0; OpenPanel(Panel.Journal, Panel.Pause); break;
            case 4: OpenPanel(Panel.Achievements, Panel.Pause); break;
            case 5: OpenPanel(Panel.Settings, Panel.Pause); break;
            case 6: OpenPanel(Panel.ConfirmMenu, Panel.Pause); break;
            case 7: OpenPanel(Panel.ConfirmQuit, Panel.Pause); break;
        }
    }

    // ============================================================================================ every frame
    private void RefreshUI()
    {
        bool game = mode == Mode.Game && !ConsumedEndingCinematic.IsPlaying;
        bool showPanels = summary == null && ((game && paused) || (mode == Mode.Menu && panel != Panel.None));
        Panel shown = game && paused && panel == Panel.None ? Panel.Pause : panel;

        TLCCanvas.SetActive(ui.hud, game && !paused && summary == null);
        TLCCanvas.SetActive(ui.panels, showPanels);
        TLCCanvas.SetActive(ui.pausePanel, showPanels && shown == Panel.Pause);
        TLCCanvas.SetActive(ui.slotsPanel, showPanels && (shown == Panel.Save || shown == Panel.Load));
        TLCCanvas.SetActive(ui.settingsPanel, showPanels && shown == Panel.Settings);
        TLCCanvas.SetActive(ui.achievementsPanel, showPanels && shown == Panel.Achievements);
        TLCCanvas.SetActive(ui.journalPanel, showPanels && shown == Panel.Journal);
        TLCCanvas.SetActive(ui.confirmPanel, showPanels && (shown == Panel.ConfirmMenu || shown == Panel.ConfirmQuit));
        TLCCanvas.SetActive(ui.controlsPanel, showPanels && shown == Panel.Controls);
        TLCCanvas.SetActive(ui.summaryPanel, summary != null);

        RefreshObjective(game);
        RefreshToasts();

        if (showPanels)
        {
            if (shown == Panel.Pause)
            {
                ObjectiveTracker t = ObjectiveTracker.Instance;
                ObjectiveTracker.Objective cur = t != null ? t.Current() : null;
                TLCCanvas.SetText(ui.pauseObjective, cur != null ? "<b>Objective:</b> " + t.TitleWithProgress(cur) : "");
            }
            if (shown == Panel.Save || shown == Panel.Load)
                TLCCanvas.SetText(ui.slotsMessage, Time.unscaledTime - panelMessageTime > 6f ? "" : panelMessage);
            if (shown == Panel.Settings) RefreshSettingValues();
        }
    }

    private void RefreshObjective(bool game)
    {
        bool show = game && !paused && summary == null && player != null && player.enabled && !player.isDead && !player.isCinematicPlaying;
        ObjectiveTracker tracker = ObjectiveTracker.Instance;
        if (tracker == null) show = false;
        TLCCanvas.SetActive(ui.objectivePanel, show);
        if (!show) return;

        ObjectiveTracker.Objective cur = tracker.Current();
        string header = tracker.IsGated ? "OBJECTIVE" : "OBJECTIVE   " + tracker.DoneCount() + "/" + tracker.Objectives.Count;
        string body = cur != null ? tracker.TitleWithProgress(cur) : "Survive.";
        int colon = body.IndexOf(": ");
        if (colon > 0 && body.Substring(0, colon) == body.Substring(0, colon).ToUpper())
        {
            header = body.Substring(0, colon);
            body = body.Substring(colon + 2);
            if (body.Length > 0) body = char.ToUpper(body[0]) + body.Substring(1);
        }
        if (ui.objHeader.text != header) ui.objHeader.text = header;
        if (ui.objBody.text != body) ui.objBody.text = body;
    }

    private void RefreshToasts()
    {
        if (ui.toastTemplate == null) return;
        for (int i = 0; i < toasts.Count; i++)
        {
            ToastMsg t = toasts[i];
            if (t.start < 0f) t.start = Time.unscaledTime;
            float age = Time.unscaledTime - t.start;
            if (age > ToastTime)
            {
                if (t.view != null) Destroy(t.view.gameObject);
                toasts.RemoveAt(i); i--;
                continue;
            }
            if (t.view == null)
            {
                TLCRefs r = TLCCanvas.Spawn<TLCRefs>(ui.toastTemplate, ui.topRight);
                if (r == null || r.texts == null || r.texts.Length < 2 || r.images == null || r.images.Length < 1 || r.GetComponent<CanvasGroup>() == null)
                {
                    if (r != null) Destroy(r.gameObject);
                    toasts.RemoveAt(i); i--;          // broken template: drop the pop-up instead of trying again every frame
                    continue;
                }
                r.texts[0].text = t.title;
                r.texts[0].color = t.gold ? TLCUIFactory.Gold : TLCUIFactory.Blood;
                r.texts[1].text = t.text;
                r.images[0].color = t.gold ? new Color(0.45f, 0.33f, 0.05f, 0.95f) : TLCUIFactory.Red;
                t.view = r.GetComponent<CanvasGroup>();
            }
            t.view.alpha = Mathf.Clamp01(Mathf.Min(age * 4f, (ToastTime - age) * 2f));
        }
    }

    // ============================================================================================ filling panels
    private void FillPanel()
    {
        if (ui == null) BindUI();
        if (ui == null) return;
        switch (panel)
        {
            case Panel.Save: FillSlots(true); break;
            case Panel.Load: FillSlots(false); break;
            case Panel.Settings: SyncSettings(); break;
            case Panel.Achievements: FillAchievements(); break;
            case Panel.Journal: FillJournal(); break;
            case Panel.ConfirmMenu:
                ui.confirmQuestion.text = "Return to the main menu?";
                ui.confirmInfo.text = "Your progress will be saved at your last safe position.";
                break;
            case Panel.ConfirmQuit:
                ui.confirmQuestion.text = "Quit the game?";
                ui.confirmInfo.text = "Your progress will be saved at your last safe position.";
                break;
        }
        RefreshUI();
    }

    // ---- save / load (FR_38, FR_42)
    private int confirmOverwrite = -1;

    private void FillSlots(bool saving)
    {
        TLCCanvas.SetActive(ui.headingSave, saving);
        TLCCanvas.SetActive(ui.headingLoad, !saving);
        TLCCanvas.SetActive(ui.slotsNote.gameObject, !saving);
        for (int slot = 0; slot < ui.slotRows.Length; slot++)
        {
            TLCCanvas.SlotRow row = ui.slotRows[slot];
            bool visible = !(saving && slot == 0);
            TLCCanvas.SetActive(row.root, visible);
            if (!visible) continue;
            SaveSlots.SlotInfo info = SaveSlots.GetInfo(slot);
            if (row.card != null) row.card.Set(info.exists);
            row.label.text = info.label;
            row.details.text = info.details;
            if (saving)
            {
                row.buttonLabel.text = confirmOverwrite == slot ? "SURE?" : (info.exists ? "OVERWRITE" : "SAVE");
                row.button.interactable = true;
            }
            else
            {
                row.buttonLabel.text = "LOAD";
                row.button.interactable = info.exists && info.valid;
            }
        }
    }

    private void SlotClicked(int slot)
    {
        if (panel == Panel.Save)
        {
            SaveSlots.SlotInfo info = SaveSlots.GetInfo(slot);
            if (info.exists && confirmOverwrite != slot) confirmOverwrite = slot;
            else
            {
                confirmOverwrite = -1;
                SaveSlots.SaveToSlot(slot, out string msg);
                Message(msg);
            }
            FillSlots(true);
        }
        else if (panel == Panel.Load)
        {
            if (SaveSlots.LoadSlot(slot, out string msg))
            {
                paused = false;
                disabledWhilePaused.Clear();
            }
            else Message(msg);
        }
    }

    // ---- settings (FR_47)
    private void SyncSettings()
    {
        float[] v = { GameSettings.MasterVolume, GameSettings.Brightness, GameSettings.EffectsIntensity, GameSettings.MouseSensitivity };
        for (int i = 0; i < ui.settingRows.Length && i < 4; i++) ui.settingRows[i].slider.SetValueWithoutNotify(v[i]);
        RefreshSettingValues();
    }

    private void RefreshSettingValues()
    {
        if (ui.settingRows.Length < 4) return;
        ui.settingRows[0].value.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
        ui.settingRows[1].value.text = (GameSettings.Brightness >= 0 ? "+" : "") + GameSettings.Brightness.ToString("0.0");
        ui.settingRows[2].value.text = Mathf.RoundToInt(GameSettings.EffectsIntensity * 100f) + "%";
        ui.settingRows[3].value.text = GameSettings.MouseSensitivity.ToString("0.0") + "x";
        if (ui.graphicsRows != null)
            for (int i = 0; i < ui.graphicsRows.Length; i++)
                if (ui.graphicsRows[i] != null && ui.graphicsRows[i].value != null) ui.graphicsRows[i].value.text = GraphicsQuality.ValueText(i);
        if (ui.graphicsInfo != null) ui.graphicsInfo.text = GraphicsQuality.InfoText();
    }

    // ---- achievements (FR_52, FR_53)
    private void FillAchievements()
    {
        IReadOnlyList<GameProgress.AchievementDef> list = GameProgress.Achievements;
        int unlocked = GameProgress.UnlockedCount();
        ui.achSummary.text = unlocked + " / " + list.Count + " unlocked        ·        Endings discovered: " + GameProgress.EndingsSeen() + " / " + GameProgress.EndingCount;
        ui.achBarFill.anchorMax = new Vector2((float)unlocked / Mathf.Max(1, list.Count), 1f);

        TLCCanvas.Clear(ui.achContent, ui.achTemplate);
        SaveData save = GameProgress.CurrentSave;
        foreach (GameProgress.AchievementDef a in list)
        {
            TLCRefs c = TLCCanvas.Spawn<TLCRefs>(ui.achTemplate, ui.achContent);
            if (c == null || c.card == null || c.texts == null || c.texts.Length < 5) { if (c != null) Destroy(c.gameObject); break; }
            bool got = GameProgress.IsUnlocked(a.id);
            bool secret = !got && a.hidden;
            c.card.Set(got);
            c.texts[0].text = secret ? "???" : a.title;
            c.texts[0].color = got ? new Color(1f, 0.86f, 0.52f) : new Color(0.80f, 0.74f, 0.68f);
            c.texts[1].text = got ? "UNLOCKED" : "LOCKED";
            c.texts[1].color = got ? new Color(1f, 0.70f, 0.22f) : new Color(0.50f, 0.42f, 0.38f);
            c.texts[2].text = secret ? "Hidden - keep playing to discover this one." : a.description;
            c.texts[3].text = got ? GameProgress.UnlockDate(a.id) : "";
            bool bar = !got && a.progress != null && save != null;
            c.images[0].gameObject.SetActive(bar);
            c.texts[4].gameObject.SetActive(bar);
            if (bar)
            {
                int target = GameProgress.TargetOf(a);
                int cur = Mathf.Min(target, a.progress(save));
                c.rects[0].anchorMax = new Vector2((float)cur / Mathf.Max(1, target), 1f);
                c.texts[4].text = cur + " / " + target;
            }
        }
    }

    // ---- journal (FR_34, FR_35, FR_37, FR_49, FR_51)
    private void FillJournal()
    {
        for (int i = 0; i < ui.journalTabs.Length; i++) TLCUIFactory.SetButtonColors(ui.journalTabs[i], i == journalTab);
        for (int i = 0; i < ui.journalPages.Length; i++) TLCCanvas.SetActive(ui.journalPages[i], i == journalTab);
        switch (journalTab)
        {
            case 0: FillObjectives(); break;
            case 1: FillItems(); break;
            case 2: FillNotes(); break;
            case 3: FillStats(); break;
            case 4: FillMap(); break;
        }
    }

    // ---- MAP: every room of the asylum, floor by floor, with its state
    private void FillMap()
    {
        if (ui.mapColumns == null || ui.mapColumns.Length < 3) return;
        Inventory inv = player != null ? player.GetComponent<Inventory>() : null;
        // room -> (floor, locked?, key, seen?)
        Dictionary<string, string[]> rooms = new Dictionary<string, string[]>();
        foreach (DoorController d in FindObjectsByType<DoorController>(FindObjectsInactive.Include))
        {
            string room = d.RoomName;
            if (string.IsNullOrEmpty(room)) continue;
            if (!rooms.TryGetValue(room, out string[] r)) rooms[room] = r = new[] { d.FloorName, "open", "", "" };
            if (d.IsLocked) { r[1] = "locked"; r[2] = d.RequiredKey ?? ""; }
            else if (r[1] != "locked" && (d.PlayerDiscovered || d.IsOpen)) r[3] = "seen";
            if (d.PlayerDiscovered) r[3] = "seen";
        }
        string[] floors = { "First Floor", "Ground Floor", "Basement" };
        for (int i = 0; i < 3; i++)
        {
            List<string> names = new List<string>();
            foreach (var kv in rooms) if (kv.Value[0] == floors[i]) names.Add(kv.Key);
            names.Sort(System.StringComparer.OrdinalIgnoreCase);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string n in names)
            {
                string[] r = rooms[n];
                if (r[1] == "locked")
                {
                    string key = r[2];
                    bool haveKey = inv != null && !string.IsNullOrEmpty(key) && !key.ToUpper().Contains("GATE") && inv.HasItem(key);
                    string need = string.IsNullOrEmpty(key) ? "opened by a puzzle" : key.ToUpper().Contains("GATE") ? "key + 8-digit code" : "needs the " + key;
                    sb.Append(haveKey ? "<color=#f2c040>■  " + n + "</color>   <size=17><color=#f2c040>key in hand!</color></size>\n"
                                      : "<color=#c8503a>■  " + n + "</color>   <size=17><color=#8a7f76>" + need + "</color></size>\n");
                }
                else if (r[3] == "seen") sb.Append("<color=#e0d6c8>●  " + n + "</color>\n");
                else sb.Append("<color=#8a7f76>○  " + n + "</color>\n");
            }
            ui.mapColumns[i].text = sb.Length > 0 ? sb.ToString() : "<color=#8a7f76>-</color>";
        }
        if (ui.mapLegend != null)
            ui.mapLegend.text = "●  explored      ○  not explored yet      <color=#c8503a>■  locked</color>      <color=#f2c040>■  you have the key</color>";
    }

    private void FillObjectives()
    {
        TLCCanvas.Clear(ui.jObjContent, ui.jObjTemplate, ui.jObjMore.gameObject);
        ObjectiveTracker t = ObjectiveTracker.Instance;
        if (t == null) { ui.jObjMore.text = "No objectives yet."; return; }

        List<ObjectiveTracker.Objective> shown = new List<ObjectiveTracker.Objective>();
        foreach (ObjectiveTracker.Objective o in t.Objectives) if (o.discovered || o.done) shown.Add(o);
        shown.Sort((a, b) => a.done == b.done ? a.order.CompareTo(b.order) : (a.done ? 1 : -1));
        foreach (ObjectiveTracker.Objective o in shown)
        {
            TLCRefs r = TLCCanvas.Spawn<TLCRefs>(ui.jObjTemplate, ui.jObjContent);
            if (r == null) break;
            r.images[0].color = o.done ? TLCUIFactory.RedDark : TLCUIFactory.Gray;
            r.texts[0].text = o.done ? "DONE" : "OPEN";
            r.texts[0].color = o.done ? TLCUIFactory.Dim : TLCUIFactory.Blood;
            r.texts[1].text = o.done ? "<color=#9a9088>" + t.TitleWithProgress(o) + "</color>" : t.TitleWithProgress(o);
        }
        int hidden = t.UndiscoveredCount();
        ui.jObjMore.text = hidden > 0 ? "??? " + hidden + " more objective(s) still to discover - explore the asylum." : "";
        ui.jObjMore.transform.SetAsLastSibling();
    }

    private void FillItems()
    {
        SaveData d = GameProgress.CurrentSave;
        List<string> items = d != null ? SaveManager.SplitList(d.itemsFoundCSV) : new List<string>();
        items.Sort(System.StringComparer.OrdinalIgnoreCase);   // organised alphabetically (FR_37)

        string inHand = "";
        if (player != null)
        {
            Inventory inv = player.GetComponent<Inventory>();
            if (inv != null) inHand = inv.GetHeldItemDisplayName();
        }
        ui.jItemsInHand.text = "In your hand:  <b>" + (string.IsNullOrEmpty(inHand) ? "nothing" : inHand) + "</b>      (G to drop)";

        TLCCanvas.Clear(ui.jItemsContent, ui.jItemTemplate);
        ui.jItemsEmpty.gameObject.SetActive(items.Count == 0);
        if (items.Count == 0)
        {
            ui.jItemName.text = ""; ui.jItemDesc.text = ""; ui.jItemFoot.text = "";
            return;
        }
        if (selectedItem >= items.Count) selectedItem = -1;
        if (selectedItem < 0 && !string.IsNullOrEmpty(inHand)) selectedItem = items.FindIndex(s => string.Equals(s, inHand, System.StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < items.Count; i++)
        {
            Button b = TLCCanvas.Spawn<Button>(ui.jItemTemplate, ui.jItemsContent);
            if (b == null) break;
            bool held = string.Equals(items[i], inHand, System.StringComparison.OrdinalIgnoreCase);
            b.GetComponentInChildren<Text>().text = (held ? "> " : "") + items[i];
            TLCUIFactory.SetButtonColors(b, i == selectedItem);
            int k = i;
            b.onClick.AddListener(() => { selectedItem = k; FillItems(); });
        }

        if (selectedItem >= 0 && selectedItem < items.Count)
        {
            string name = items[selectedItem];
            bool held = string.Equals(name, inHand, System.StringComparison.OrdinalIgnoreCase);
            ui.jItemName.text = name.ToUpperInvariant();
            ui.jItemDesc.text = GameProgress.DescribeItem(name);
            ui.jItemFoot.text = held ? "You are holding this." : "You only have one free hand - pick it up again where you left it.";
        }
        else
        {
            ui.jItemName.text = "";
            ui.jItemDesc.text = "<color=#9e948a>Select an item to inspect it.</color>";
            ui.jItemFoot.text = "";
        }
    }

    private class NoteEntry { public string id, text, title; }

    private List<NoteEntry> ReadNotes()
    {
        List<NoteEntry> notes = new List<NoteEntry>();
        SaveData d = GameProgress.CurrentSave;
        if (d == null) return notes;
        Dictionary<string, string> texts = new Dictionary<string, string>();
        foreach (ReadableNote n in FindObjectsByType<ReadableNote>(FindObjectsInactive.Include))
        {
            string id = SaveManager.GetSceneObjectId(n.transform);
            if (!texts.ContainsKey(id)) texts[id] = n.noteText;
        }
        foreach (string id in SaveManager.SplitList(d.notesReadCSV))
            if (texts.TryGetValue(id, out string text)) notes.Add(new NoteEntry { id = id, text = text });

        List<GameProgress.LoreEntry> lore = GameProgress.AllLoreInScene();   // hidden lore (FR_64) after the notes
        foreach (string id in SaveManager.SplitList(d.loreFoundCSV))
        {
            GameProgress.LoreEntry e = lore.Find(l => l.id == id);
            if (e != null) notes.Add(new NoteEntry { id = id, text = e.text, title = e.title });
        }
        return notes;
    }

    private void FillNotes()
    {
        List<NoteEntry> notes = ReadNotes();
        ui.jNotesCounts.text = "Notes found: " + GameProgress.NotesRead() + " / " + GameProgress.CountNotesInScene() +
                               "        Hidden lore: " + GameProgress.LoreFoundCount() + " / " + GameProgress.AllLoreInScene().Count;
        TLCCanvas.Clear(ui.jNotesContent, ui.jNoteTemplate);
        ui.jNotesEmpty.gameObject.SetActive(notes.Count == 0);
        if (notes.Count == 0) { ui.jNoteHeading.text = ""; ui.jNoteText.text = ""; return; }
        if (selectedNote < 0 || selectedNote >= notes.Count) selectedNote = notes.Count - 1;

        for (int i = 0; i < notes.Count; i++)
        {
            string preview = notes[i].title ?? notes[i].text.Replace("\n", " ");
            if (preview.Length > 30) preview = preview.Substring(0, 30) + "...";
            Button b = TLCCanvas.Spawn<Button>(ui.jNoteTemplate, ui.jNotesContent);
            if (b == null) break;
            b.GetComponentInChildren<Text>().text = notes[i].title != null ? "*  " + preview : (i + 1) + ".  " + preview;
            TLCUIFactory.SetButtonColors(b, i == selectedNote);
            int k = i;
            b.onClick.AddListener(() => { selectedNote = k; FillNotes(); });
        }
        NoteEntry n = notes[selectedNote];
        ui.jNoteHeading.text = n.title != null ? "HIDDEN LORE - " + n.title.ToUpperInvariant() : "NOTE " + (selectedNote + 1);
        ui.jNoteText.text = "<i>\"" + n.text + "\"</i>";
    }

    private void FillStats()
    {
        TLCCanvas.Clear(ui.jStatsContent, ui.jStatTemplate);
        SaveData d = GameProgress.CurrentSave;
        if (d == null) { AddRow(ui.jStatsContent, ui.jStatTemplate, 0, "No statistics yet.", ""); return; }
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
        for (int i = 0; i < rows.GetLength(0); i++) AddRow(ui.jStatsContent, ui.jStatTemplate, i, rows[i, 0], rows[i, 1]);
    }

    private static void AddRow(Transform parent, GameObject template, int i, string label, string value)
    {
        TLCRefs r = TLCCanvas.Spawn<TLCRefs>(template, parent);
        if (r == null) return;
        r.images[0].color = i % 2 == 0 ? TLCUIFactory.Gray : TLCUIFactory.RedDark;
        r.texts[0].text = label;
        r.texts[1].text = value;
    }

    // ---- ending summary (FR_56)
    private void FillSummary()
    {
        if (ui == null) BindUI();
        if (ui == null || summary == null) return;
        GameProgress.RunSummary s = summary;
        ui.sumTitle.text = s.title;
        ui.sumTagline.text = s.tagline;
        ui.sumDesc.text = s.description;
        ui.sumReason.text = "<b>Why this ending:</b> " + s.reason;

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
        TLCCanvas.Clear(ui.sumRows, ui.sumRowTemplate);
        for (int i = 0; i < rows.GetLength(0); i++) AddRow(ui.sumRows, ui.sumRowTemplate, i, rows[i, 0], rows[i, 1]);

        List<string> names = new List<string>();
        foreach (string id in s.newAchievements)
            foreach (GameProgress.AchievementDef a in GameProgress.Achievements) if (a.id == id) names.Add(a.title);
        ui.sumNewAch.text = names.Count > 0 ? "New achievements: " + string.Join(", ", names.ToArray()) : "";
        RefreshUI();
    }
}
