using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// THE GAME'S UI CANVAS ("TLC_UI") - a real uGUI Canvas that sits in the Hierarchy of the game level, the main menu and the intro.
/// Every screen of the game is a child object here, so it can be seen and edited in the Scene view:
///   HUD            crosshair, room name, interaction prompt, notifications
///   TopRight       current objective + pop-ups (new objective / achievement)
///   Panels         Pause, Save/Load, Settings, Achievements, Journal (Objectives / Items / Notes / Statistics), Confirm
///   Summary        ending summary (FR_56)
///   Keypad         security keypad
///   DeathMessage   1st / 2nd death text
///   AIPanel        adaptive AI live values (F8)
///   Cinematic      letterbox, subtitles, title card, blood and eyelids of the ending cinematic
/// The prefab is Assets/Resources/UI/TLC_UI.prefab (menu: The Last Corridor / Build UI In Hierarchy).
/// Scripts only fill in texts and switch objects on/off; the look is in the prefab.
/// </summary>
public class TLCCanvas : MonoBehaviour
{
    public static TLCCanvas Instance { get; private set; }

    [Header("HUD")]
    public GameObject hud;
    public Image crosshair;
    public Text notification, roomName, floorName, prompt;

    [Header("Top-right: objective + pop-ups")]
    public RectTransform topRight;
    public GameObject objectivePanel;
    public Text objHeader, objBody, objKeys;
    public GameObject toastTemplate;

    [Header("Panels")]
    public GameObject panels;
    public GameObject pausePanel;
    public Button[] pauseButtons;
    public Text pauseObjective;

    public GameObject slotsPanel, headingSave, headingLoad;
    public SlotRow[] slotRows;
    public Text slotsNote, slotsMessage;
    public Button slotsBack;

    public GameObject settingsPanel;
    public SettingRow[] settingRows;
    public Button settingsReset, settingsBack;

    public GameObject achievementsPanel;
    public Text achSummary;
    public RectTransform achBarFill, achContent;
    public GameObject achTemplate;
    public Button achBack;

    public GameObject journalPanel;
    public Button[] journalTabs;
    public GameObject[] journalPages;
    public Button journalBack;
    public RectTransform jObjContent; public GameObject jObjTemplate; public Text jObjMore;
    public Text jItemsInHand, jItemsEmpty, jItemName, jItemDesc, jItemFoot; public RectTransform jItemsContent; public GameObject jItemTemplate;
    public Text jNotesCounts, jNotesEmpty, jNoteHeading, jNoteText; public RectTransform jNotesContent; public GameObject jNoteTemplate;
    public RectTransform jStatsContent; public GameObject jStatTemplate;

    public GameObject confirmPanel;
    public Text confirmQuestion, confirmInfo;
    public Button confirmYes, confirmNo;

    [Header("Ending summary")]
    public GameObject summaryPanel;
    public Text sumTitle, sumTagline, sumDesc, sumReason, sumNewAch;
    public RectTransform sumRows; public GameObject sumRowTemplate;
    public Button sumContinue;

    [Header("Keypad")]
    public GameObject keypadPanel;
    public Text keypadDisplay;
    public Button[] keypadButtons;   // 1 2 3 4 5 6 7 8 9 C 0 ENT
    public Button keypadExit;

    [Header("Death message")]
    public CanvasGroup deathGroup;
    public Text deathTitle, deathLine, deathLives;

    [Header("Adaptive AI panel (F8)")]
    public GameObject aiPanel;
    public Text aiText;

    [Header("Cinematic overlay")]
    public GameObject cinematic;
    public Image cineBlack, cineRed;
    public RawImage cineBlood, lidTop, lidBottom;
    public Text cineSubtitle, cineTitle, cineTitleLine, cineSkip;

    [Header("Intro")]
    public Text introSkipHint;

    [System.Serializable] public class SlotRow { public GameObject root; public TLCCard card; public Text badge, label, details, buttonLabel; public Button button; }
    [System.Serializable] public class SettingRow { public Text label, value; public Slider slider; }

    // ------------------------------------------------------------------ lifetime
    /// <summary>The canvas of the current scene. Uses the one in the Hierarchy; only if it is missing a copy of the prefab is created.</summary>
    public static TLCCanvas Ensure()
    {
        if (Instance != null) return Instance;
        TLCCanvas found = FindAnyObjectByType<TLCCanvas>(FindObjectsInactive.Include);
        if (found == null)
        {
            GameObject prefab = Resources.Load<GameObject>("UI/TLC_UI");
            GameObject go = prefab != null ? Instantiate(prefab) : TLCUIFactory.Build();
            go.name = "TLC_UI";
            found = go.GetComponent<TLCCanvas>();
        }
        found.gameObject.SetActive(true);
        found.Init();
        return found;
    }

    private bool initDone;
    void Awake() { Init(); }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Init()
    {
        Instance = this;
        if (initDone) return;
        initDone = true;
        HideAll();
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (cineBlood != null && cineBlood.texture == null) cineBlood.texture = BloodTexture();
        if (lidTop != null && lidTop.texture == null) lidTop.texture = LidTexture();
        if (lidBottom != null && lidBottom.texture == null) lidBottom.texture = LidTexture();
    }

    public void HideAll()
    {
        SetActive(hud, false);
        SetActive(panels, false);
        SetActive(summaryPanel, false);
        SetActive(keypadPanel, false);
        SetActive(aiPanel, false);
        SetActive(cinematic, false);
        SetActive(objectivePanel, false);
        if (deathGroup != null) deathGroup.gameObject.SetActive(false);
        if (introSkipHint != null) introSkipHint.gameObject.SetActive(false);
        SetText(notification, ""); SetText(roomName, ""); SetText(floorName, ""); SetText(prompt, "");
    }

    // ------------------------------------------------------------------ helpers for the scripts
    public static void SetActive(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

    /// <summary>Sets a text and hides it when empty.</summary>
    public static void SetText(Text t, string s)
    {
        if (t == null) return;
        bool on = !string.IsNullOrEmpty(s);
        if (on && t.text != s) t.text = s;
        if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
    }

    /// <summary>Removes every child of a list except its (inactive) template.</summary>
    public static void Clear(Transform content, GameObject template, GameObject keep = null)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject c = content.GetChild(i).gameObject;
            if (c == template || c == keep) continue;
            c.SetActive(false);
            Destroy(c);
        }
    }

    /// <summary>Copies a list template. Returns null (and leaves nothing behind) if the template is broken.</summary>
    public static T Spawn<T>(GameObject template, Transform parent) where T : Component
    {
        if (template == null || parent == null || template.GetComponent<T>() == null) return null;
        GameObject g = Instantiate(template, parent, false);
        g.name = template.name.Replace("Template", "");
        g.SetActive(true);
        return g.GetComponent<T>();
    }

    // ------------------------------------------------------------------ procedural textures (cinematic)
    private static Texture2D bloodTex, lidTex;

    public static Texture2D LidTexture()
    {
        if (lidTex != null) return lidTex;
        lidTex = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < 128; y++) lidTex.SetPixel(0, y, new Color(0f, 0f, 0f, y >= 40 ? 1f : Mathf.SmoothStep(0f, 1f, y / 40f)));
        lidTex.Apply();
        return lidTex;
    }

    public static Texture2D BloodTexture()
    {
        if (bloodTex != null) return bloodTex;
        const int S = 256;
        bloodTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        float[] a = new float[S * S];
        System.Random r = new System.Random(1337);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float ex = Mathf.Min(x, S - 1 - x) / (float)S, ey = Mathf.Min(y, S - 1 - y) / (float)S;
                float edge = Mathf.Clamp01(1f - Mathf.Min(ex, ey) / 0.22f);
                a[y * S + x] = Mathf.Pow(edge, 1.6f) * (0.55f + 0.45f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f));
            }
        for (int k = 0; k < 34; k++)
        {
            float cx = (float)r.NextDouble() * S, cy = (float)r.NextDouble() * S;
            if (r.NextDouble() < 0.75) { if (r.NextDouble() < 0.5) cx = r.NextDouble() < 0.5 ? cx * 0.25f : S - cx * 0.25f; else cy = r.NextDouble() < 0.5 ? cy * 0.25f : S - cy * 0.25f; }
            float rad = 3f + (float)r.NextDouble() * (k < 8 ? 26f : 9f);
            for (int y = (int)(cy - rad - 2); y <= cy + rad + 2; y++)
                for (int x = (int)(cx - rad - 2); x <= cx + rad + 2; x++)
                {
                    if (x < 0 || y < 0 || x >= S || y >= S) continue;
                    float dd = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / rad;
                    if (dd < 0.85f + 0.3f * Mathf.PerlinNoise(x * 0.3f + k, y * 0.3f)) a[y * S + x] = Mathf.Max(a[y * S + x], 0.95f);
                }
        }
        for (int k = 0; k < 14; k++)
        {
            int x0 = r.Next(S), w = 1 + r.Next(3), len = 20 + r.Next(110);
            for (int i = 0; i < len; i++)
                for (int dx = -w; dx <= w; dx++)
                {
                    int x = x0 + dx, y = S - 1 - i;
                    if (x < 0 || x >= S || y < 0) continue;
                    a[y * S + x] = Mathf.Max(a[y * S + x], 0.9f * (1f - i / (float)len * 0.4f));
                }
        }
        Color32[] px = new Color32[S * S];
        for (int i = 0; i < px.Length; i++)
        {
            float v = a[i];
            px[i] = new Color32((byte)(95 + 80 * v), (byte)(4 + 6 * v), (byte)(6 + 4 * v), (byte)(255 * Mathf.Clamp01(v)));
        }
        bloodTex.SetPixels32(px); bloodTex.Apply();
        return bloodTex;
    }
}

/// <summary>
/// Builds the TLC_UI hierarchy. Used by the editor (menu "The Last Corridor / Build UI In Hierarchy" saves it as a prefab
/// and puts it into the scenes) and, only as a safety net, at runtime if the prefab is missing.
/// Layout is in a 1920 x 1080 reference space (the Canvas Scaler scales it to any screen).
/// </summary>
public static class TLCUIFactory
{
    public static readonly Color Blood = new Color(0.78f, 0.34f, 0.16f);
    public static readonly Color Bone = new Color(0.88f, 0.84f, 0.78f);
    public static readonly Color Dim = new Color(0.62f, 0.58f, 0.54f);
    public static readonly Color Gold = new Color(0.95f, 0.75f, 0.25f);
    public static readonly Color Black82 = new Color(0f, 0f, 0f, 0.82f);
    public static readonly Color PanelBg = new Color(0.045f, 0.038f, 0.036f, 0.97f);
    public static readonly Color Red = new Color(0.50f, 0.17f, 0.08f, 0.97f);
    public static readonly Color RedDark = new Color(0.24f, 0.10f, 0.06f, 0.95f);
    public static readonly Color Gray = new Color(0.13f, 0.105f, 0.095f, 0.95f);
    public static readonly Color CardBg = new Color(0.080f, 0.066f, 0.060f, 1f);
    public static readonly Color CardHi = new Color(0.165f, 0.108f, 0.045f, 1f);
    public static readonly Color Rust = new Color(0.42f, 0.22f, 0.12f, 1f);
    public static readonly Color RustDim = new Color(0.25f, 0.13f, 0.08f, 1f);

    private static Font font;
    public static Font UIFont
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // ================================================================== building blocks
    public static RectTransform Make(string name, Transform parent)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.layer = 5; // UI
        RectTransform rt = (RectTransform)g.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>Position like a GUI rect: x, y from the parent's top-left corner.</summary>
    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static void Center(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static Image Img(Transform p, string name, Color c, bool raycast = false)
    {
        RectTransform rt = Make(name, p);
        Image i = rt.gameObject.AddComponent<Image>();
        i.color = c;
        i.raycastTarget = raycast;
        return i;
    }

    public static Text Txt(Transform p, string name, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft, FontStyle fs = FontStyle.Normal, bool shadow = false)
    {
        RectTransform rt = Make(name, p);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = UIFont; t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = fs;
        t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (shadow) { Shadow sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.85f); sh.effectDistance = new Vector2(2f, -2f); }
        return t;
    }

    public static Button Btn(Transform p, string name, string label, int size, out Text lbl, bool listItem = false)
    {
        RectTransform rt = Make(name, p);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = Color.white;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        SetButtonColors(b, false);
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        lbl = Txt(rt, "Label", label, size, new Color(0.82f, 0.66f, 0.54f), listItem ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, listItem ? FontStyle.Normal : FontStyle.Bold);
        Stretch(lbl.rectTransform);
        if (listItem) { lbl.rectTransform.offsetMin = new Vector2(14f, 0f); lbl.rectTransform.offsetMax = new Vector2(-10f, 0f); lbl.horizontalOverflow = HorizontalWrapMode.Overflow; }
        rt.gameObject.AddComponent<TLCButtonFX>().label = lbl;
        return b;
    }

    /// <summary>Normal rust-grey button, or the "selected" look (tab that is open / list item that is chosen).</summary>
    public static void SetButtonColors(Button b, bool on)
    {
        ColorBlock cb = b.colors;
        cb.normalColor = on ? Red : Gray;
        cb.highlightedColor = on ? new Color(0.62f, 0.22f, 0.10f, 1f) : Red;
        cb.pressedColor = RedDark;
        cb.selectedColor = cb.normalColor;
        cb.disabledColor = new Color(0.10f, 0.08f, 0.07f, 0.7f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
    }

    private static Image Edge(Transform p, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
    {
        Image i = Img(p, name, c);
        RectTransform rt = i.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return i;
    }

    public static TLCCard Card(Transform p, string name)
    {
        Image bg = Img(p, name, CardBg);
        TLCCard card = bg.gameObject.AddComponent<TLCCard>();
        card.background = bg;
        card.lines = new[]
        {
            Edge(bg.transform, "LineTop", Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -0.5f)),
            Edge(bg.transform, "LineBottom", Color.white, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 0.5f)),
            Edge(bg.transform, "LineRight", Color.white, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0), new Vector2(-0.5f, 0)),
        };
        card.stripe = Edge(bg.transform, "Stripe", Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(5, 0), new Vector2(2.5f, 0));
        card.Set(false);
        return card;
    }

    /// <summary>Full-screen dim + centred window with the rusty double frame. Returns the window.</summary>
    public static RectTransform Window(Transform panel, float w, float h)
    {
        Image dim = Img(panel, "Dim", Black82, true);
        Stretch(dim.rectTransform);
        Image win = Img(panel, "Window", PanelBg, true);
        RectTransform r = win.rectTransform;
        Center(r, 0f, 0f, w, h);
        Edge(r, "FrameTop", Rust, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 3), new Vector2(0, -1.5f));
        Edge(r, "FrameBottom", Rust, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 3), new Vector2(0, 1.5f));
        Edge(r, "FrameLeft", Rust, new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0), new Vector2(1.5f, 0));
        Edge(r, "FrameRight", Rust, new Vector2(1, 0), new Vector2(1, 1), new Vector2(3, 0), new Vector2(-1.5f, 0));
        Edge(r, "InnerTop", RustDim, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-20, 1.5f), new Vector2(0, -10.75f));
        Edge(r, "InnerBottom", RustDim, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-20, 1.5f), new Vector2(0, 10.75f));
        Edge(r, "InnerLeft", RustDim, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1.5f, -20), new Vector2(10.75f, 0));
        Edge(r, "InnerRight", RustDim, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1.5f, -20), new Vector2(-10.75f, 0));
        return r;
    }

    /// <summary>Rusty 3D lettering from the main menu (Resources/MenuButtons/title_*), or plain text if the picture is missing.</summary>
    public static GameObject Heading(Transform p, string key, string text, float x, float y, float w, float h, int fallbackSize)
    {
        Texture2D t = Resources.Load<Texture2D>("MenuButtons/title_" + key);
        if (t != null)
        {
            RectTransform rt = Make("Heading_" + key, p);
            RawImage ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = t; ri.raycastTarget = false;
            float hh = Mathf.Min(h, 78f), ww = hh * t.width / t.height;
            Place(rt, x + (w - ww) * 0.5f, y + (h - hh) * 0.5f, ww, hh);
            return rt.gameObject;
        }
        Text tx = Txt(p, "Heading_" + key, text, fallbackSize, Blood, TextAnchor.MiddleCenter, FontStyle.Bold);
        Place(tx.rectTransform, x, y, w, h);
        return tx.gameObject;
    }

    public static ScrollRect Scroll(Transform p, string name, float x, float y, float w, float h, out RectTransform content, int gridColumns = 0, Vector2 cell = default, float spacing = 6f)
    {
        RectTransform rt = Make(name, p);
        Place(rt, x, y, w, h);
        Image hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        ScrollRect sr = rt.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f; sr.inertia = false;

        RectTransform vp = Make("Viewport", rt);
        vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.pivot = new Vector2(0f, 1f);
        vp.offsetMin = Vector2.zero; vp.offsetMax = new Vector2(-18f, 0f);
        vp.gameObject.AddComponent<RectMask2D>();

        content = Make("Content", vp);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
        if (gridColumns > 0)
        {
            GridLayoutGroup g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = new Vector2(10f, 10f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = gridColumns;
        }
        else
        {
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        }
        ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = vp; sr.content = content;

        RectTransform sbRt = Make("Scrollbar", rt);
        sbRt.anchorMin = new Vector2(1f, 0f); sbRt.anchorMax = new Vector2(1f, 1f); sbRt.pivot = new Vector2(1f, 0.5f);
        sbRt.sizeDelta = new Vector2(12f, 0f); sbRt.anchoredPosition = Vector2.zero;
        Image sbBg = sbRt.gameObject.AddComponent<Image>(); sbBg.color = new Color(0.10f, 0.08f, 0.07f, 1f);
        Scrollbar sb = sbRt.gameObject.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        RectTransform area = Make("Sliding Area", sbRt); Stretch(area);
        Image handle = Img(area, "Handle", Red, true); Stretch(handle.rectTransform);
        sb.handleRect = handle.rectTransform; sb.targetGraphic = handle;
        sb.navigation = new Navigation { mode = Navigation.Mode.None };
        sr.verticalScrollbar = sb;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return sr;
    }

    public static Slider MakeSlider(Transform p, float x, float y, float w, float h)
    {
        RectTransform rt = Make("Slider", p);
        Place(rt, x, y, w, h);
        Slider s = rt.gameObject.AddComponent<Slider>();
        Image bg = Img(rt, "Background", new Color(0.20f, 0.13f, 0.09f, 1f));
        bg.rectTransform.anchorMin = new Vector2(0f, 0.5f); bg.rectTransform.anchorMax = new Vector2(1f, 0.5f); bg.rectTransform.sizeDelta = new Vector2(0f, 8f);
        RectTransform fillArea = Make("Fill Area", rt);
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f); fillArea.sizeDelta = new Vector2(0f, 8f);
        Image fill = Img(fillArea, "Fill", new Color(0.70f, 0.30f, 0.13f, 1f));
        fill.rectTransform.sizeDelta = Vector2.zero;
        RectTransform handleArea = Make("Handle Slide Area", rt);
        Stretch(handleArea); handleArea.offsetMin = new Vector2(9f, 0f); handleArea.offsetMax = new Vector2(-9f, 0f);
        Image handle = Img(handleArea, "Handle", Red, true);
        handle.rectTransform.sizeDelta = new Vector2(18f, 30f);
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        ColorBlock cb = s.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.4f, 1.3f, 1.2f, 1f); cb.colorMultiplier = 1.5f; s.colors = cb;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        return s;
    }

    private static Button WindowButton(Transform win, string name, string label, float x, float y, float w, float h, int size = 26)
    {
        Button b = Btn(win, name, label, size, out _);
        Place((RectTransform)b.transform, x, y, w, h);
        return b;
    }

    private static Button BackButton(RectTransform win, float w, float h)
    {
        return WindowButton(win, "BackButton", "BACK", (w - 260f) * 0.5f, h - 80f, 260f, 56f);
    }

    private static RectTransform Panel(Transform parent, string name)
    {
        RectTransform rt = Make(name, parent);
        Stretch(rt);
        return rt;
    }

    // ================================================================== the whole canvas
    public static GameObject Build()
    {
        GameObject root = new GameObject("TLC_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = 5;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler cs = root.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 1f;
        TLCCanvas ui = root.AddComponent<TLCCanvas>();
        Transform t = root.transform;

        BuildHud(ui, t);
        BuildTopRight(ui, t);
        BuildAIPanel(ui, t);
        RectTransform panels = Panel(t, "Panels");
        ui.panels = panels.gameObject;
        BuildPause(ui, panels);
        BuildSlots(ui, panels);
        BuildSettings(ui, panels);
        BuildAchievements(ui, panels);
        BuildJournal(ui, panels);
        BuildConfirm(ui, panels);
        BuildSummary(ui, t);
        BuildKeypad(ui, t);
        BuildDeath(ui, t);
        BuildCinematic(ui, t);
        Text skip = Txt(t, "IntroSkipHint", "Press SPACE to skip", 16, new Color(1f, 1f, 1f, 0.6f), TextAnchor.LowerRight);
        RectTransform sr = skip.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(1f, 0f); sr.sizeDelta = new Vector2(300f, 30f); sr.anchoredPosition = new Vector2(-20f, 10f);
        ui.introSkipHint = skip;

        ui.HideAll();
        return root;
    }

    // ------------------------------------------------------------------ HUD (screen centre)
    private static void BuildHud(TLCCanvas ui, Transform t)
    {
        RectTransform hud = Panel(t, "HUD");
        ui.hud = hud.gameObject;
        ui.crosshair = Img(hud, "Crosshair", Color.white);
        Center(ui.crosshair.rectTransform, 0f, 0f, 5f, 5f);
        ui.notification = Txt(hud, "Notification", "", 24, Color.yellow, TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Center(ui.notification.rectTransform, 0f, 130f, 800f, 80f);
        ui.roomName = Txt(hud, "RoomName", "", 30, new Color(0.93f, 0.89f, 0.82f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Center(ui.roomName.rectTransform, 0f, 80f, 800f, 40f);
        ui.floorName = Txt(hud, "FloorName", "", 15, new Color(0.8f, 0.25f, 0.2f, 0.95f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Center(ui.floorName.rectTransform, 0f, 53f, 800f, 22f);
        ui.prompt = Txt(hud, "InteractPrompt", "", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Center(ui.prompt.rectTransform, 0f, -70f, 700f, 40f);
    }

    // ------------------------------------------------------------------ objective + toasts (top-right)
    private static void BuildTopRight(TLCCanvas ui, Transform t)
    {
        RectTransform tr = Make("TopRight", t);
        tr.anchorMin = tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(1f, 1f);
        tr.anchoredPosition = new Vector2(-24f, -24f); tr.sizeDelta = new Vector2(470f, 1000f);
        VerticalLayoutGroup v = tr.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 14f; v.childAlignment = TextAnchor.UpperRight;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        ui.topRight = tr;

        Image obj = Img(tr, "ObjectivePanel", new Color(0f, 0f, 0f, 0.74f));
        VerticalLayoutGroup ov = obj.gameObject.AddComponent<VerticalLayoutGroup>();
        ov.padding = new RectOffset(16, 8, 6, 8); ov.spacing = 2f;
        ov.childControlWidth = true; ov.childControlHeight = true; ov.childForceExpandWidth = true; ov.childForceExpandHeight = false;
        Image accent = Edge(obj.transform, "Accent", Red, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0), new Vector2(2, 0));
        accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ui.objectivePanel = obj.gameObject;
        ui.objHeader = Txt(obj.transform, "Header", "OBJECTIVE", 18, Blood, TextAnchor.UpperLeft, FontStyle.Bold);
        ui.objBody = Txt(obj.transform, "Objective", "Survive.", 21, Bone);
        ui.objKeys = Txt(obj.transform, "Keys", "TAB  Journal      ESC  Menu", 19, Dim);

        Image toast = Img(tr, "ToastTemplate", PanelBg);
        toast.gameObject.AddComponent<CanvasGroup>();
        VerticalLayoutGroup tv = toast.gameObject.AddComponent<VerticalLayoutGroup>();
        tv.padding = new RectOffset(16, 8, 6, 8); tv.spacing = 2f;
        tv.childControlWidth = true; tv.childControlHeight = true; tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;
        Image stripe = Edge(toast.transform, "Stripe", Red, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0), new Vector2(2, 0));
        stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        TLCRefs refs = toast.gameObject.AddComponent<TLCRefs>();
        refs.texts = new[] { Txt(toast.transform, "Title", "TITLE", 18, Blood, TextAnchor.UpperLeft, FontStyle.Bold), Txt(toast.transform, "Text", "Text", 21, Bone) };
        refs.images = new[] { stripe };
        toast.gameObject.SetActive(false);
        ui.toastTemplate = toast.gameObject;
    }

    private static void BuildAIPanel(TLCCanvas ui, Transform t)
    {
        Image bg = Img(t, "AIPanel", new Color(0f, 0f, 0f, 0.75f));
        Place(bg.rectTransform, 10f, 10f, 430f, 300f);
        ui.aiPanel = bg.gameObject;
        ui.aiText = Txt(bg.transform, "Text", "", 17, Bone);
        Stretch(ui.aiText.rectTransform);
        ui.aiText.rectTransform.offsetMin = new Vector2(12f, 8f); ui.aiText.rectTransform.offsetMax = new Vector2(-8f, -8f);
    }

    // ------------------------------------------------------------------ pause
    private static void BuildPause(TLCCanvas ui, Transform panels)
    {
        RectTransform p = Panel(panels, "PausePanel");
        ui.pausePanel = p.gameObject;
        const float W = 560f, H = 820f;
        RectTransform win = Window(p, W, H);
        Heading(win, "paused", "PAUSED", 0f, 20f, W, 80f, 64);
        string[] labels = { "RESUME", "SAVE GAME", "LOAD GAME", "JOURNAL", "ACHIEVEMENTS", "SETTINGS", "MAIN MENU", "QUIT GAME" };
        ui.pauseButtons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            ui.pauseButtons[i] = WindowButton(win, "Button_" + labels[i].Replace(' ', '_'), labels[i], 80f, 120f + i * 68f, W - 160f, 58f);
        ui.pauseObjective = Txt(win, "Objective", "", 19, Dim);
        Place(ui.pauseObjective.rectTransform, 30f, H - 66f, W - 60f, 56f);
    }

    // ------------------------------------------------------------------ save / load
    private static void BuildSlots(TLCCanvas ui, Transform panels)
    {
        RectTransform p = Panel(panels, "SaveLoadPanel");
        ui.slotsPanel = p.gameObject;
        const float W = 1000f, H = 760f;
        RectTransform win = Window(p, W, H);
        ui.headingSave = Heading(win, "save_game", "SAVE GAME", 0f, 20f, W, 70f, 46);
        ui.headingLoad = Heading(win, "load_game", "LOAD GAME", 0f, 20f, W, 70f, 46);

        RectTransform list = Make("Slots", win);
        Place(list, 40f, 110f, W - 80f, 430f);
        VerticalLayoutGroup v = list.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 12f; v.childControlWidth = true; v.childControlHeight = false; v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        ui.slotRows = new TLCCanvas.SlotRow[SaveSlots.SlotCount + 1];
        for (int s = 0; s <= SaveSlots.SlotCount; s++)
        {
            TLCCard card = Card(list, s == 0 ? "Slot_Autosave" : "Slot_" + s);
            RectTransform row = card.GetComponent<RectTransform>();
            row.sizeDelta = new Vector2(0f, 92f);
            Image badgeBg = Img(row, "Badge", new Color(0f, 0f, 0f, 0.82f));
            Place(badgeBg.rectTransform, 22f, 16f, 60f, 60f);
            Text badge = Txt(badgeBg.transform, "Number", s == 0 ? "A" : s.ToString(), 34, Blood, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(badge.rectTransform);
            Text label = Txt(row, "Label", "Empty slot", 30, Bone, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(label.rectTransform, 100f, 8f, 600f, 36f);
            Text details = Txt(row, "Details", "", 19, Dim);
            Place(details.rectTransform, 100f, 48f, 600f, 36f);
            Button b = Btn(row, "Button", "SAVE", 26, out Text bl);
            RectTransform br = (RectTransform)b.transform;
            br.anchorMin = br.anchorMax = new Vector2(1f, 0.5f); br.pivot = new Vector2(1f, 0.5f);
            br.sizeDelta = new Vector2(180f, 56f); br.anchoredPosition = new Vector2(-20f, 0f);
            ui.slotRows[s] = new TLCCanvas.SlotRow { root = card.gameObject, card = card, badge = badge, label = label, details = details, button = b, buttonLabel = bl };
        }
        ui.slotsNote = Txt(win, "Note", "Loading discards anything you have not saved.", 19, Dim);
        Place(ui.slotsNote.rectTransform, 40f, 560f, W - 80f, 30f);
        ui.slotsMessage = Txt(win, "Message", "", 24, Bone, TextAnchor.MiddleCenter);
        Place(ui.slotsMessage.rectTransform, 30f, H - 150f, W - 60f, 60f);
        ui.slotsBack = BackButton(win, W, H);
    }

    // ------------------------------------------------------------------ settings
    private static void BuildSettings(TLCCanvas ui, Transform panels)
    {
        RectTransform p = Panel(panels, "SettingsPanel");
        ui.settingsPanel = p.gameObject;
        const float W = 900f, H = 640f;
        RectTransform win = Window(p, W, H);
        Heading(win, "settings", "SETTINGS", 0f, 20f, W, 70f, 46);
        string[] names = { "Master Volume", "Brightness", "Camera Effects (head-bob)", "Mouse Sensitivity" };
        float x = 60f, y = 120f, lw = 300f, sw = W - 120f - lw - 110f;
        ui.settingRows = new TLCCanvas.SettingRow[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            TLCCard card = Card(win, "Row_" + names[i].Split(' ')[0] + (i == 2 ? "Effects" : ""));
            Place((RectTransform)card.transform, x - 20f, y - 8f, lw + sw + 140f, 58f);
            Text label = Txt(card.transform, "Label", names[i], 24, Bone, TextAnchor.MiddleLeft);
            Place(label.rectTransform, 20f, 8f, lw, 40f);
            Slider s = MakeSlider(card.transform, 20f + lw, 14f, sw, 30f);
            Text value = Txt(card.transform, "Value", "", 24, Bone, TextAnchor.MiddleLeft);
            Place(value.rectTransform, 20f + lw + sw + 20f, 8f, 100f, 40f);
            ui.settingRows[i] = new TLCCanvas.SettingRow { label = label, slider = s, value = value };
            y += 70f;
        }
        ui.settingsReset = WindowButton(win, "ResetButton", "RESET DEFAULTS", (W - 300f) * 0.5f, y + 10f, 300f, 52f);
        Text info = Txt(win, "Info", "Settings are saved automatically.", 19, Dim, TextAnchor.MiddleCenter);
        Place(info.rectTransform, 40f, y + 72f, W - 80f, 30f);
        ui.settingsBack = BackButton(win, W, H);
    }

    // ------------------------------------------------------------------ achievements
    private static void BuildAchievements(TLCCanvas ui, Transform panels)
    {
        RectTransform p = Panel(panels, "AchievementsPanel");
        ui.achievementsPanel = p.gameObject;
        const float W = 1240f, H = 900f;
        RectTransform win = Window(p, W, H);
        Heading(win, "achievements", "ACHIEVEMENTS", 0f, 16f, W, 70f, 46);
        ui.achSummary = Txt(win, "Summary", "", 21, Bone, TextAnchor.MiddleCenter);
        Place(ui.achSummary.rectTransform, 0f, 82f, W, 30f);
        Image barBg = Img(win, "ProgressBar", new Color(0f, 0f, 0f, 0.82f));
        Place(barBg.rectTransform, W * 0.5f - 220f, 118f, 440f, 6f);
        Image fill = Img(barBg.transform, "Fill", new Color(1f, 0.62f, 0.2f, 1f));
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        ui.achBarFill = fill.rectTransform;

        float vwid = W - 60f, colW = (vwid - 34f) * 0.5f;
        Scroll(win, "List", 30f, 142f, vwid, H - 242f, out RectTransform content, 2, new Vector2(colW, 92f));
        ui.achContent = content;

        TLCCard cell = Card(content, "AchievementTemplate");
        RectTransform c = (RectTransform)cell.transform;
        TLCRefs refs = cell.gameObject.AddComponent<TLCRefs>();
        Text title = Txt(c, "Title", "Title", 23, Bone, TextAnchor.UpperLeft, FontStyle.Bold); Place(title.rectTransform, 22f, 8f, colW - 150f, 32f);
        Text chip = Txt(c, "State", "LOCKED", 14, Dim, TextAnchor.MiddleRight, FontStyle.Bold); Place(chip.rectTransform, colW - 130f, 12f, 116f, 22f);
        Text desc = Txt(c, "Description", "", 18, Dim); Place(desc.rectTransform, 22f, 42f, colW - 194f, 46f);
        desc.verticalOverflow = VerticalWrapMode.Truncate;
        Text date = Txt(c, "Date", "", 16, Dim, TextAnchor.MiddleRight); Place(date.rectTransform, colW - 150f, 62f, 136f, 24f);
        Image pb = Img(c, "ProgressBar", new Color(0f, 0f, 0f, 0.82f)); Place(pb.rectTransform, colW - 150f, 74f, 134f, 8f);
        Image pf = Img(pb.transform, "Fill", new Color(0.78f, 0.30f, 0.12f, 1f));
        pf.rectTransform.anchorMin = Vector2.zero; pf.rectTransform.anchorMax = new Vector2(0.5f, 1f); pf.rectTransform.offsetMin = pf.rectTransform.offsetMax = Vector2.zero;
        Text pt = Txt(c, "ProgressText", "", 16, Dim, TextAnchor.MiddleRight); Place(pt.rectTransform, colW - 150f, 46f, 134f, 24f);
        refs.texts = new[] { title, chip, desc, date, pt };
        refs.images = new[] { pb };
        refs.rects = new[] { pf.rectTransform };
        refs.card = cell;
        cell.gameObject.SetActive(false);
        ui.achTemplate = cell.gameObject;
        ui.achBack = BackButton(win, W, H);
    }

    // ------------------------------------------------------------------ journal
    private static void BuildJournal(TLCCanvas ui, Transform panels)
    {
        RectTransform p = Panel(panels, "JournalPanel");
        ui.journalPanel = p.gameObject;
        const float W = 1400f, H = 900f;
        RectTransform win = Window(p, W, H);
        Heading(win, "journal", "JOURNAL", 0f, 14f, W, 70f, 46);
        string[] tabs = { "OBJECTIVES", "ITEMS", "NOTES", "STATISTICS" };
        float tw = 260f, tx = (W - tabs.Length * (tw + 10f)) * 0.5f;
        ui.journalTabs = new Button[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
            ui.journalTabs[i] = WindowButton(win, "Tab_" + tabs[i], tabs[i], tx + i * (tw + 10f), 90f, tw, 48f, 22);

        float bx = 40f, by = 160f, bw = W - 80f, bh = H - 260f;
        ui.journalPages = new GameObject[4];

        // objectives
        RectTransform po = Make("Page_Objectives", win); Place(po, bx, by, bw, bh);
        ui.journalPages[0] = po.gameObject;
        Scroll(po, "List", 0f, 0f, bw, bh, out RectTransform oc, 0, default, 6f);
        ui.jObjContent = oc;
        Image orow = Img(oc, "ObjectiveTemplate", Gray);
        orow.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        TLCRefs orefs = orow.gameObject.AddComponent<TLCRefs>();
        Text ost = Txt(orow.transform, "State", "OPEN", 18, Blood, TextAnchor.MiddleLeft, FontStyle.Bold);
        ost.rectTransform.anchorMin = new Vector2(0f, 0f); ost.rectTransform.anchorMax = new Vector2(0f, 1f); ost.rectTransform.pivot = new Vector2(0f, 0.5f);
        ost.rectTransform.sizeDelta = new Vector2(110f, 0f); ost.rectTransform.anchoredPosition = new Vector2(16f, 0f);
        Text otx = Txt(orow.transform, "Title", "", 24, Bone, TextAnchor.MiddleLeft);
        Stretch(otx.rectTransform); otx.rectTransform.offsetMin = new Vector2(130f, 0f); otx.rectTransform.offsetMax = new Vector2(-16f, 0f);
        orefs.texts = new[] { ost, otx }; orefs.images = new[] { orow };
        orow.gameObject.SetActive(false);
        ui.jObjTemplate = orow.gameObject;
        ui.jObjMore = Txt(oc, "MoreToDiscover", "", 19, Dim);
        ui.jObjMore.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        // items
        RectTransform pi = Make("Page_Items", win); Place(pi, bx, by, bw, bh);
        ui.journalPages[1] = pi.gameObject;
        ui.jItemsInHand = Txt(pi, "InHand", "", 24, Bone); Place(ui.jItemsInHand.rectTransform, 0f, 0f, bw, 36f);
        BuildListAndDetail(pi, bw, bh, "Items", out ui.jItemsContent, out ui.jItemTemplate, out ui.jItemsEmpty, out ui.jItemName, out ui.jItemDesc, out ui.jItemFoot);

        // notes
        RectTransform pn = Make("Page_Notes", win); Place(pn, bx, by, bw, bh);
        ui.journalPages[2] = pn.gameObject;
        ui.jNotesCounts = Txt(pn, "Counts", "", 24, Bone); Place(ui.jNotesCounts.rectTransform, 0f, 0f, bw, 36f);
        BuildListAndDetail(pn, bw, bh, "Notes", out ui.jNotesContent, out ui.jNoteTemplate, out ui.jNotesEmpty, out ui.jNoteHeading, out ui.jNoteText, out _);

        // statistics
        RectTransform ps = Make("Page_Statistics", win); Place(ps, bx, by, bw, bh);
        ui.journalPages[3] = ps.gameObject;
        Scroll(ps, "List", 0f, 0f, bw, bh, out RectTransform sc, 0, default, 4f);
        ui.jStatsContent = sc;
        ui.jStatTemplate = StatRow(sc, "StatTemplate", 520f, 42f);

        Text close = Txt(win, "CloseHint", "TAB  close", 19, Dim);
        Place(close.rectTransform, 40f, H - 70f, 420f, 40f);
        ui.journalBack = BackButton(win, W, H);
    }

    private static void BuildListAndDetail(RectTransform page, float bw, float bh, string what, out RectTransform content, out GameObject template,
                                           out Text empty, out Text heading, out Text body, out Text foot)
    {
        Scroll(page, "List", 0f, 50f, 440f, bh - 50f, out content, 0, default, 6f);
        Button b = Btn(content, "EntryTemplate", "Entry", 21, out _, true);
        b.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        b.gameObject.SetActive(false);
        template = b.gameObject;
        empty = Txt(page, "Empty", what == "Items" ? "You have not picked anything up yet." : "You have not read any notes yet. Look for papers on desks, walls and bodies.", 19, Dim);
        Place(empty.rectTransform, 0f, 50f, 440f, 80f);

        Image detail = Img(page, "Detail", Gray);
        Place(detail.rectTransform, 470f, 50f, bw - 470f, bh - 50f);
        heading = Txt(detail.transform, "Heading", "", 30, Bone, TextAnchor.UpperLeft, FontStyle.Bold);
        Place(heading.rectTransform, 24f, 20f, bw - 470f - 48f, 44f);
        body = Txt(detail.transform, "Text", "", 24, Bone);
        Place(body.rectTransform, 24f, 76f, bw - 470f - 48f, bh - 50f - 150f);
        foot = Txt(detail.transform, "Footer", "", 19, Dim);
        Place(foot.rectTransform, 24f, bh - 50f - 60f, bw - 470f - 48f, 40f);
    }

    private static GameObject StatRow(Transform parent, string name, float labelW, float h)
    {
        Image row = Img(parent, name, Gray);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        TLCRefs r = row.gameObject.AddComponent<TLCRefs>();
        Text l = Txt(row.transform, "Label", "", 24, Bone, TextAnchor.MiddleLeft);
        l.rectTransform.anchorMin = new Vector2(0f, 0f); l.rectTransform.anchorMax = new Vector2(0f, 1f); l.rectTransform.pivot = new Vector2(0f, 0.5f);
        l.rectTransform.sizeDelta = new Vector2(labelW, 0f); l.rectTransform.anchoredPosition = new Vector2(20f, 0f);
        Text v = Txt(row.transform, "Value", "", 24, Bone, TextAnchor.MiddleLeft, FontStyle.Bold);
        Stretch(v.rectTransform); v.rectTransform.offsetMin = new Vector2(labelW + 20f, 0f); v.rectTransform.offsetMax = new Vector2(-20f, 0f);
        r.texts = new[] { l, v }; r.images = new[] { row };
        row.gameObject.SetActive(false);
        return row.gameObject;
    }

    // ------------------------------------------------------------------ confirm
    private static void BuildConfirm(TLCCanvas ui, Transform panels)
    {
        RectTransform p = Panel(panels, "ConfirmPanel");
        ui.confirmPanel = p.gameObject;
        const float W = 700f, H = 300f;
        RectTransform win = Window(p, W, H);
        ui.confirmQuestion = Txt(win, "Question", "", 30, Bone, TextAnchor.MiddleCenter, FontStyle.Bold);
        Place(ui.confirmQuestion.rectTransform, 0f, 30f, W, 60f);
        ui.confirmInfo = Txt(win, "Info", "", 24, Bone, TextAnchor.MiddleCenter);
        Place(ui.confirmInfo.rectTransform, 40f, 100f, W - 80f, 60f);
        ui.confirmYes = WindowButton(win, "YesButton", "YES", 80f, H - 100f, 240f, 60f);
        ui.confirmNo = WindowButton(win, "NoButton", "NO", W - 320f, H - 100f, 240f, 60f);
    }

    // ------------------------------------------------------------------ ending summary
    private static void BuildSummary(TLCCanvas ui, Transform t)
    {
        Image bg = Img(t, "EndingSummary", new Color(0f, 0f, 0f, 0.97f), true);
        Stretch(bg.rectTransform);
        ui.summaryPanel = bg.gameObject;
        RectTransform c = Make("Content", bg.transform);
        Center(c, 0f, 0f, 1300f, 1080f);
        ui.sumTitle = Txt(c, "Title", "", 64, Blood, TextAnchor.MiddleCenter, FontStyle.Bold); Place(ui.sumTitle.rectTransform, 0f, 50f, 1300f, 80f);
        ui.sumTagline = Txt(c, "Tagline", "", 24, Bone, TextAnchor.MiddleCenter, FontStyle.Italic); Place(ui.sumTagline.rectTransform, 0f, 130f, 1300f, 40f);
        ui.sumDesc = Txt(c, "Description", "", 24, Bone, TextAnchor.MiddleCenter); Place(ui.sumDesc.rectTransform, 60f, 185f, 1180f, 110f);
        ui.sumReason = Txt(c, "Reason", "", 19, Dim, TextAnchor.MiddleCenter); Place(ui.sumReason.rectTransform, 60f, 295f, 1180f, 40f);
        RectTransform rows = Make("Statistics", c);
        Place(rows, 270f, 355f, 760f, 480f);
        VerticalLayoutGroup v = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 3f; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        ui.sumRows = rows;
        ui.sumRowTemplate = StatRow(rows, "RowTemplate", 340f, 40f);
        ui.sumNewAch = Txt(c, "NewAchievements", "", 24, Gold, TextAnchor.MiddleCenter); Place(ui.sumNewAch.rectTransform, 0f, 840f, 1300f, 40f);
        ui.sumContinue = Btn(bg.transform, "ContinueButton", "CONTINUE", 26, out _);
        RectTransform br = (RectTransform)ui.sumContinue.transform;
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f); br.pivot = new Vector2(0.5f, 0f); br.sizeDelta = new Vector2(340f, 62f); br.anchoredPosition = new Vector2(0f, 38f);
    }

    // ------------------------------------------------------------------ keypad
    private static void BuildKeypad(TLCCanvas ui, Transform t)
    {
        RectTransform root = Panel(t, "Keypad");
        ui.keypadPanel = root.gameObject;
        const float s = 1.35f, W = 300f * s, H = 500f * s;
        Image panel = Img(root, "Panel", new Color(0.07f, 0.07f, 0.08f, 0.97f), true);
        Center(panel.rectTransform, 0f, 0f, W, H);
        RectTransform pr = panel.rectTransform;
        Text title = Txt(pr, "Title", "SECURITY KEYPAD", Mathf.RoundToInt(14 * s), new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Place(title.rectTransform, 0f, 8f * s, W, 24f * s);
        Image disp = Img(pr, "Display", new Color(0.02f, 0.03f, 0.03f, 1f));
        Place(disp.rectTransform, 25f * s, 40f * s, 250f * s, 60f * s);
        ui.keypadDisplay = Txt(disp.transform, "Text", "_ _ _ _", Mathf.RoundToInt(34 * s), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(ui.keypadDisplay.rectTransform);
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "ENT" };
        ui.keypadButtons = new Button[labels.Length];
        const float bw = 76f, bh = 64f, gap = 11f, left = 25f, top = 116f;
        for (int i = 0; i < labels.Length; i++)
        {
            int row = i / 3, col = i % 3;
            bool special = labels[i] == "C" || labels[i] == "ENT";
            Button b = Btn(pr, "Key_" + labels[i], labels[i], Mathf.RoundToInt((special ? 18 : 26) * s), out _);
            Place((RectTransform)b.transform, (left + col * (bw + gap)) * s, (top + row * (bh + gap)) * s, bw * s, bh * s);
            ui.keypadButtons[i] = b;
        }
        ui.keypadExit = Btn(pr, "ExitButton", "EXIT", Mathf.RoundToInt(18 * s), out _);
        Place((RectTransform)ui.keypadExit.transform, 25f * s, 428f * s, 250f * s, 48f * s);
    }

    // ------------------------------------------------------------------ death message
    private static void BuildDeath(TLCCanvas ui, Transform t)
    {
        RectTransform root = Panel(t, "DeathMessage");
        ui.deathGroup = root.gameObject.AddComponent<CanvasGroup>();
        ui.deathGroup.blocksRaycasts = false;
        ui.deathTitle = Txt(root, "Title", "", 64, new Color(0.72f, 0.06f, 0.05f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Center(ui.deathTitle.rectTransform, 0f, 65f, 1700f, 110f);
        ui.deathLine = Txt(root, "Line", "", 30, new Color(0.88f, 0.84f, 0.76f), TextAnchor.MiddleCenter, FontStyle.Italic, true);
        Center(ui.deathLine.rectTransform, 0f, -25f, 1700f, 50f);
        ui.deathLives = Txt(root, "Lives", "", 22, new Color(0.72f, 0.06f, 0.05f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Center(ui.deathLives.rectTransform, 0f, -87f, 1700f, 34f);
    }

    // ------------------------------------------------------------------ cinematic overlay
    private static void BuildCinematic(TLCCanvas ui, Transform t)
    {
        RectTransform root = Panel(t, "CinematicOverlay");
        ui.cinematic = root.gameObject;
        RawImage blood = Make("Blood", root).gameObject.AddComponent<RawImage>();
        Stretch(blood.rectTransform); blood.raycastTarget = false; blood.color = new Color(1f, 1f, 1f, 0f);
        ui.cineBlood = blood;
        ui.cineRed = Img(root, "RedFlash", new Color(0.55f, 0f, 0f, 0f)); Stretch(ui.cineRed.rectTransform);

        RawImage lt = Make("EyelidTop", root).gameObject.AddComponent<RawImage>();
        lt.raycastTarget = false;
        lt.rectTransform.anchorMin = new Vector2(0f, 1f); lt.rectTransform.anchorMax = new Vector2(1f, 1f); lt.rectTransform.pivot = new Vector2(0.5f, 1f);
        lt.rectTransform.sizeDelta = new Vector2(0f, 0f); lt.rectTransform.anchoredPosition = Vector2.zero;
        ui.lidTop = lt;
        RawImage lb = Make("EyelidBottom", root).gameObject.AddComponent<RawImage>();
        lb.raycastTarget = false; lb.uvRect = new Rect(0f, 1f, 1f, -1f);
        lb.rectTransform.anchorMin = new Vector2(0f, 0f); lb.rectTransform.anchorMax = new Vector2(1f, 0f); lb.rectTransform.pivot = new Vector2(0.5f, 0f);
        lb.rectTransform.sizeDelta = new Vector2(0f, 0f); lb.rectTransform.anchoredPosition = Vector2.zero;
        ui.lidBottom = lb;

        ui.cineBlack = Img(root, "Black", Color.black); Stretch(ui.cineBlack.rectTransform);

        Image top = Img(root, "LetterboxTop", Color.black);
        top.rectTransform.anchorMin = new Vector2(0f, 0.89f); top.rectTransform.anchorMax = Vector2.one; top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
        Image bottom = Img(root, "LetterboxBottom", Color.black);
        bottom.rectTransform.anchorMin = Vector2.zero; bottom.rectTransform.anchorMax = new Vector2(1f, 0.11f); bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;

        ui.cineSubtitle = Txt(root, "Subtitle", "", 32, new Color(0.92f, 0.86f, 0.76f), TextAnchor.MiddleCenter, FontStyle.Italic, true);
        RectTransform st = ui.cineSubtitle.rectTransform;
        st.anchorMin = new Vector2(0.08f, 0.11f); st.anchorMax = new Vector2(0.92f, 0.11f); st.pivot = new Vector2(0.5f, 0f);
        st.sizeDelta = new Vector2(0f, 80f); st.anchoredPosition = new Vector2(0f, 15f);
        ui.cineTitle = Txt(root, "Title", "", 96, new Color(0.72f, 0.18f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
        Center(ui.cineTitle.rectTransform, 0f, 70f, 1600f, 130f);
        ui.cineTitleLine = Txt(root, "TitleLine", "", 28, new Color(0.85f, 0.8f, 0.72f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
        Center(ui.cineTitleLine.rectTransform, 0f, -60f, 1300f, 60f);
        ui.cineSkip = Txt(root, "SkipHint", "Press any key to skip", 28, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleRight);
        RectTransform sk = ui.cineSkip.rectTransform;
        sk.anchorMin = sk.anchorMax = new Vector2(1f, 0f); sk.pivot = new Vector2(1f, 0f); sk.sizeDelta = new Vector2(400f, 40f); sk.anchoredPosition = new Vector2(-20f, 40f);
    }
}
