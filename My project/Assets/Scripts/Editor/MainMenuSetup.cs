#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the main menu's bottom bar (RESUME / START / LOAD GAME / ACHIEVEMENTS / OPTIONS / QUIT) as normal UI objects in the
/// "main menue" SCENE - visible in the Hierarchy, Scene view and Game view, not only while playing. Also adds the dark floor fog,
/// embers, corner brackets and the footer text. Everything is a child of MainMenu_Screen, so it fades in with the intro cinematic.
/// Runs automatically once when the scene is open (it saves the scene when it changes something).
/// Manual: menu  The Last Corridor > Rebuild Main Menu Bar  (removes the old bar and builds it again).
/// </summary>
[InitializeOnLoad]
public static class MainMenuSetup
{
    private const string SceneName = "main menue";
    private const string SpriteFolder = "Assets/Resources/MenuButtons/";
    private const string BarName = "MenuBar";
    private const string FooterText = "A / D  -  SELECT        ·        ENTER  -  CONFIRM        ·        HOLLOWVEIN STUDIOS";
    private static readonly string[] Extras = { "Menu_Scrim", "Menu_Embers", "Menu_Corners", "Menu_Footer" };
    private static readonly string[] Names = { "resume", "start", "load_game", "achievements", "options", "quit" };

    static MainMenuSetup()
    {
        EditorApplication.delayCall += TryAuto;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += TryAuto;
    }

    private static void TryAuto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName) return;
        Build(scene, false, false);
    }

    [MenuItem("The Last Corridor/Rebuild Main Menu Bar")]
    private static void Manual()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName) { Debug.LogWarning("MainMenuSetup: open the '" + SceneName + "' scene first."); return; }
        Build(scene, true, true);
    }

    private static Sprite LoadSprite(string file)
    {
        string path = SpriteFolder + file;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Transform FindButton(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null) t = parent.Find(BarName + "/" + name);
        return t;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static bool Build(Scene scene, bool log, bool rebuild)
    {
        MainMenuManager mgr = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (mgr == null || mgr.mainMenuScreen == null) return false;
        Transform parent = mgr.mainMenuScreen.transform;

        // keep the footer text up to date (studio name) without rebuilding the bar
        Transform footerOld = parent.Find("Menu_Footer");
        if (footerOld != null)
        {
            Text ft = footerOld.GetComponent<Text>();
            if (ft != null && ft.text != FooterText)
            {
                Undo.RecordObject(ft, "Menu footer text");
                ft.text = FooterText;
                EditorUtility.SetDirty(ft);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        // MainMenu_Screen was a 100x100 box in the middle of the canvas, so "bottom" meant the middle of the screen.
        // Make it fill the canvas (children are centre-anchored, so their positions do not change).
        RectTransform prt = parent as RectTransform;
        bool needStretch = prt != null && (prt.anchorMin != Vector2.zero || prt.anchorMax != Vector2.one);
        if (parent.Find(BarName) != null && !rebuild && !needStretch) return false;   // already built
        if (needStretch)
        {
            Undo.RecordObject(prt, "Stretch MainMenu_Screen");
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            prt.localScale = Vector3.one;
        }

        // sprites
        Sprite[] sprites = new Sprite[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            sprites[i] = LoadSprite("menu_" + Names[i] + ".png");
            if (sprites[i] == null) { if (log) Debug.LogWarning("MainMenuSetup: sprite menu_" + Names[i] + ".png is not imported yet - try again in a moment."); return false; }
        }
        Sprite scrimSprite = LoadSprite("menu_scrim.png");
        Sprite emberSprite = LoadSprite("menu_ember.png");
        if (scrimSprite == null || emberSprite == null) return false;

        // the gear (settings) button is gone - settings are under OPTIONS
        Transform gear = parent.Find("settings");
        if (gear != null) Undo.DestroyObjectImmediate(gear.gameObject);

        // take the buttons out of an old bar, then remove the old bar + extras
        Transform oldBar = parent.Find(BarName);
        if (oldBar != null)
        {
            foreach (string n in Names)
            {
                Transform b = oldBar.Find(n);
                if (b != null) b.SetParent(parent, false);
            }
            Undo.DestroyObjectImmediate(oldBar.gameObject);
        }
        foreach (string n in Extras)
        {
            Transform t = parent.Find(n);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // ---- floor fog (behind everything) ----
        Image scrim = NewImage("Menu_Scrim", parent, scrimSprite, Color.white);
        scrim.rectTransform.anchorMin = new Vector2(0f, 0f);
        scrim.rectTransform.anchorMax = new Vector2(1f, 0f);
        scrim.rectTransform.pivot = new Vector2(0.5f, 0f);
        scrim.rectTransform.sizeDelta = new Vector2(0f, 360f);
        scrim.rectTransform.anchoredPosition = Vector2.zero;
        scrim.rectTransform.SetSiblingIndex(0);

        // ---- the bar ----
        RectTransform bar = NewRect(BarName, parent);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 78f);

        float[] heights = { 54f, 44f, 44f, 44f, 44f, 47f };   // RESUME is the featured one
        const float gap = 60f;
        float[] widths = new float[Names.Length];
        float total = 0f;
        for (int i = 0; i < Names.Length; i++)
        {
            widths[i] = heights[i] * sprites[i].rect.width / sprites[i].rect.height;
            total += widths[i];
        }
        total += gap * (Names.Length - 1);
        bar.sizeDelta = new Vector2(total, 90f);

        MenuBarController controller = bar.gameObject.AddComponent<MenuBarController>();
        controller.fadeGroup = Object.FindFirstObjectByType<MenuCameraSway>(FindObjectsInactive.Include) != null
            ? Object.FindFirstObjectByType<MenuCameraSway>(FindObjectsInactive.Include).uiCanvasGroup : null;

        UnityAction[] newActions = { null, null, mgr.OpenLoadGame, mgr.OpenAchievements, mgr.OpenOptions, null };
        float x = -total * 0.5f;
        for (int i = 0; i < Names.Length; i++)
        {
            Transform t = FindButton(parent, Names[i]);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = NewRect(Names[i], parent).gameObject;
                Image created = go.AddComponent<Image>();
                Button cb = go.AddComponent<Button>();
                cb.targetGraphic = created;
                if (newActions[i] != null) UnityEventTools.AddPersistentListener(cb.onClick, newActions[i]);
            }

            Undo.RecordObject(go, "Menu bar");
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(widths[i], heights[i]);
            rt.anchoredPosition = new Vector2(x + widths[i] * 0.5f, 0f);
            rt.localScale = Vector3.one;
            x += widths[i] + gap;

            Image img = go.GetComponent<Image>();
            img.sprite = sprites[i];
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = true;
            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            Navigation nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            if (Names[i] == "resume") mgr.resumeButton = btn;

            // glowing underline
            Transform oldU = go.transform.Find("underline");
            if (oldU != null) Object.DestroyImmediate(oldU.gameObject);
            Image under = NewImage("underline", go.transform, emberSprite, new Color(0.95f, 0.38f, 0.13f, 0f));
            under.rectTransform.anchorMin = under.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            under.rectTransform.pivot = new Vector2(0.5f, 1f);
            under.rectTransform.sizeDelta = new Vector2(widths[i] * 1.05f, 14f);
            under.rectTransform.anchoredPosition = new Vector2(0f, -6f);

            MenuBarItem item = go.GetComponent<MenuBarItem>();
            if (item == null) item = go.AddComponent<MenuBarItem>();
            item.icon = img;
            item.underline = under;
            item.featured = Names[i] == "resume";
            controller.items.Add(item);
        }

        // ---- embers ----
        RectTransform embers = NewRect("Menu_Embers", parent);
        embers.anchorMin = Vector2.zero; embers.anchorMax = Vector2.one; embers.offsetMin = embers.offsetMax = Vector2.zero;
        for (int i = 0; i < 12; i++)
        {
            Image e = NewImage("ember_" + (i + 1), embers, emberSprite, new Color(1f, 1f, 1f, 0f));
            e.rectTransform.anchorMin = e.rectTransform.anchorMax = Vector2.zero;
            e.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        embers.gameObject.AddComponent<MenuEmbers>();

        // ---- corner brackets ----
        RectTransform corners = NewRect("Menu_Corners", parent);
        corners.anchorMin = Vector2.zero; corners.anchorMax = Vector2.one; corners.offsetMin = corners.offsetMax = Vector2.zero;
        Color cc = new Color(0.48f, 0.23f, 0.125f, 1f);
        for (int c = 0; c < 4; c++)
        {
            bool right = (c & 1) == 1, top = (c & 2) == 2;
            Vector2 a = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            float px = right ? -28f : 28f, py = top ? -28f : 28f;
            RectTransform corner = NewRect("corner_" + (top ? "T" : "B") + (right ? "R" : "L"), corners);
            corner.anchorMin = corner.anchorMax = a; corner.pivot = a;
            corner.sizeDelta = new Vector2(34f, 34f); corner.anchoredPosition = new Vector2(px, py);
            Image h = NewImage("h", corner, null, cc);
            h.rectTransform.anchorMin = h.rectTransform.anchorMax = h.rectTransform.pivot = a;
            h.rectTransform.sizeDelta = new Vector2(34f, 2f); h.rectTransform.anchoredPosition = Vector2.zero;
            Image v = NewImage("v", corner, null, cc);
            v.rectTransform.anchorMin = v.rectTransform.anchorMax = v.rectTransform.pivot = a;
            v.rectTransform.sizeDelta = new Vector2(2f, 34f); v.rectTransform.anchoredPosition = Vector2.zero;
        }

        // ---- footer ----
        RectTransform footer = NewRect("Menu_Footer", parent);
        footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f); footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, 24f); footer.anchoredPosition = new Vector2(0f, 30f);
        Text txt = footer.gameObject.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.55f, 0.48f, 0.41f, 1f);
        txt.raycastTarget = false;
        txt.text = FooterText;

        // the logo stays above everything else in order; bar/extras are drawn over the scrim
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MainMenuSetup: bottom menu bar built in the scene (RESUME / START / LOAD GAME / ACHIEVEMENTS / OPTIONS / QUIT). Scene saved.");
        return true;
    }
}
#endif
