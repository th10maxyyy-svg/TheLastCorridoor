#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates the studio intro SCENE (Assets/Scenes/Intro.unity) with every element as a normal object (Hierarchy: IntroCanvas)
/// and puts it FIRST in File > Build Settings, so the game opens with: HOLLOWVEIN STUDIOS - PRODUCTIONS PRESENTS - main menu.
/// Runs once automatically (only if the scene does not exist yet). Manual: menu  The Last Corridor > Rebuild Intro Scene.
/// </summary>
[InitializeOnLoad]
public static class IntroSetup
{
    private const string ScenePath = "Assets/Scenes/Intro.unity";
    private const string Folder = "Assets/Resources/Intro/";
    private const string MenuSceneName = "main menue";

    private const string PlayPref = "TLC_PlayFromIntro";
    private static int tries;
    private static double nextTry;

    static IntroSetup()
    {
        EditorApplication.update += Poll;
    }

    // Keeps trying for ~1 minute: right after a compile the new assets (font, sprites) may not be imported yet.
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextTry) return;
        nextTry = EditorApplication.timeSinceStartup + 1.5;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        if (System.IO.File.Exists(ScenePath)) { EnsureBuildSettings(); ApplyPlayStart(); EditorApplication.update -= Poll; return; }
        if (++tries > 40) { EditorApplication.update -= Poll; return; }
        if (Build(false)) { ApplyPlayStart(); EditorApplication.update -= Poll; }
    }

    [MenuItem("The Last Corridor/Rebuild Intro Scene")]
    private static void Manual() { if (Build(true)) ApplyPlayStart(); }

    // Play button always starts with the Intro (like the real game), whichever scene is open. Toggle in the menu.
    private static bool PlayFromIntro { get { return EditorPrefs.GetBool(PlayPref, true); } }

    private static void ApplyPlayStart()
    {
        SceneAsset intro = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorSceneManager.playModeStartScene = (PlayFromIntro && intro != null) ? intro : null;
    }

    [MenuItem("The Last Corridor/Play always starts with the Intro")]
    private static void TogglePlay()
    {
        EditorPrefs.SetBool(PlayPref, !PlayFromIntro);
        ApplyPlayStart();
        Debug.Log(PlayFromIntro ? "Play now always starts with the Intro scene." : "Play now starts from the scene that is open.");
    }

    [MenuItem("The Last Corridor/Play always starts with the Intro", true)]
    private static bool TogglePlayValidate()
    {
        Menu.SetChecked("The Last Corridor/Play always starts with the Intro", PlayFromIntro);
        return true;
    }

    private static Sprite LoadSprite(string file)
    {
        string path = Folder + file;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
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

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int idx = list.FindIndex(s => s.path == ScenePath);
        if (idx == 0) return;
        if (idx > 0) list.RemoveAt(idx);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        // the main menu must be in the build too
        string[] menu = AssetDatabase.FindAssets("t:Scene " + MenuSceneName);
        foreach (string guid in menu)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == MenuSceneName && !list.Exists(s => s.path == p))
                list.Add(new EditorBuildSettingsScene(p, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("IntroSetup: Intro scene is now the FIRST scene in Build Settings.");
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }

    private static Text NewText(string name, Transform parent, Font font, int size, string text, Color color, FontStyle style = FontStyle.Normal)
    {
        RectTransform rt = NewRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.text = text; t.color = color; t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static HorizontalLayoutGroup Row(RectTransform rt, float spacing)
    {
        HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return h;
    }

    private static bool Build(bool log)
    {
        Sprite glowS = LoadSprite("intro_glow.png"), vigS = LoadSprite("intro_vignette.png");
        Font font = AssetDatabase.LoadAssetAtPath<Font>(Folder + "Oswald.ttf");
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "intro_heartbeat.wav");
        if (glowS == null || vigS == null || font == null)
        {
            if (log) Debug.LogWarning("IntroSetup: intro assets are not imported yet - press Ctrl+R in Unity and try again.");
            return false;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // camera (black)
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        cam.orthographic = true; cam.cullingMask = 0;
        camGo.AddComponent<AudioListener>();
        SceneManager.MoveGameObjectToScene(camGo, scene);

        // canvas
        GameObject cgo = new GameObject("IntroCanvas", typeof(RectTransform));
        cgo.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        AudioSource audio = cgo.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.loop = false;
        IntroSequence seq = cgo.AddComponent<IntroSequence>();
        SceneManager.MoveGameObjectToScene(cgo, scene);
        Transform root = cgo.transform;

        Image bg = NewImage("Background_Black", root, null, Color.black); Stretch(bg.rectTransform);

        RectTransform contentRt = NewRect("Content", root); Stretch(contentRt);
        CanvasGroup group = contentRt.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;

        Image glow = NewImage("RedGlow", contentRt, glowS, new Color(0.78f, 0.16f, 0.08f, 0f));
        glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        glow.rectTransform.sizeDelta = new Vector2(1900f, 1000f); glow.rectTransform.anchoredPosition = new Vector2(0f, -30f);

        // heartbeat line (a soft wide copy underneath for the glow)
        IntroEcgLine ecgGlow = NewEcg("ECG_Glow", contentRt, 12f, new Color(0.9f, 0.2f, 0.1f, 0.28f));
        IntroEcgLine ecg = NewEcg("ECG_Line", contentRt, 3.2f, new Color(0.77f, 0.157f, 0.10f, 1f));

        // title: HOLLOW + VEIN, one Text per letter
        RectTransform title = NewRect("Title_HollowVein", contentRt);
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.5f); title.sizeDelta = new Vector2(1700f, 240f); title.anchoredPosition = new Vector2(0f, 100f);
        Row(title, 64f);
        List<Text> letters = new List<Text>();
        RectTransform hollow = NewRect("Word_HOLLOW", title); Row(hollow, 10f);
        foreach (char ch in "HOLLOW") letters.Add(NewText("letter_" + ch, hollow, font, 168, ch.ToString(), new Color(0.91f, 0.88f, 0.84f, 0f), FontStyle.Normal));
        RectTransform vein = NewRect("Word_VEIN", title); Row(vein, 10f);
        foreach (char ch in "VEIN") letters.Add(NewText("letter_" + ch, vein, font, 168, ch.ToString(), new Color(0.77f, 0.157f, 0.10f, 0f), FontStyle.Normal));

        // STUDIOS with a thin line on each side
        Text studios = NewText("Text_STUDIOS", contentRt, font, 38, "S    T    U    D    I    O    S", new Color(0.79f, 0.66f, 0.56f, 0f));
        studios.rectTransform.anchorMin = studios.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        studios.rectTransform.sizeDelta = new Vector2(700f, 60f); studios.rectTransform.anchoredPosition = new Vector2(0f, -68f);
        RectTransform fl = NewFlank("Line_Left", contentRt, -400f, new Vector2(1f, 0.5f));
        RectTransform fr = NewFlank("Line_Right", contentRt, 400f, new Vector2(0f, 0.5f));

        Text presents = NewText("Text_PRODUCTIONS_PRESENTS", contentRt, font, 22, "P R O D U C T I O N S      P R E S E N T S", new Color(0.55f, 0.48f, 0.41f, 0f));
        presents.rectTransform.anchorMin = presents.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        presents.rectTransform.sizeDelta = new Vector2(1000f, 40f); presents.rectTransform.anchoredPosition = new Vector2(0f, -177f);

        Image vig = NewImage("Vignette", root, vigS, Color.white); Stretch(vig.rectTransform);
        Image fade = NewImage("Fade_Black", root, null, new Color(0f, 0f, 0f, 0f)); Stretch(fade.rectTransform);

        seq.nextScene = MenuSceneName;
        seq.content = group; seq.glow = glow; seq.ecg = ecg; seq.ecgGlow = ecgGlow;
        seq.letters = letters; seq.veinFirstIndex = 6; seq.veinWord = vein;
        seq.studios = studios; seq.flankLeft = fl; seq.flankRight = fr; seq.presents = presents;
        seq.fadeOverlay = fade; seq.audioSource = audio; seq.heartbeat = clip;

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.Refresh();
        EnsureBuildSettings();
        Debug.Log("IntroSetup: Intro scene " + (ok ? "created at " + ScenePath : "could NOT be saved") + ". Double-click it in Assets/Scenes to see the Hierarchy; press Play from it to preview.");
        return ok;
    }

    private static IntroEcgLine NewEcg(string name, Transform parent, float thickness, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1740f, 200f); rt.anchoredPosition = Vector2.zero;
        IntroEcgLine e = rt.gameObject.AddComponent<IntroEcgLine>();
        e.thickness = thickness; e.color = color; e.raycastTarget = false; e.amplitude = 1.5f;
        return e;
    }

    private static RectTransform NewFlank(string name, Transform parent, float x, Vector2 pivot)
    {
        Image img = NewImage(name, parent, null, new Color(0.60f, 0.23f, 0.125f, 1f));
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot; rt.sizeDelta = new Vector2(255f, 2f);
        rt.anchoredPosition = new Vector2(x + (pivot.x > 0.5f ? 255f * 0.5f - 255f * 0.5f : 0f), -70f);
        rt.localScale = new Vector3(0f, 1f, 1f);
        return rt;
    }
}
#endif
