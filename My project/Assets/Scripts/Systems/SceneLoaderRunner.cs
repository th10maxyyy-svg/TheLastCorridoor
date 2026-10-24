using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs the loading screen for SceneLoader (lives across scene loads while loading, then removes itself).</summary>
public class SceneLoaderRunner : MonoBehaviour
{
    public static bool Busy { get; private set; }
    private static SceneLoaderRunner current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Busy = false; current = null; }

    private CanvasGroup group;
    private Text dots;

    public static void Begin(string sceneName, int index)
    {
        if (Busy) return;
        Busy = true;
        GameObject go = new GameObject("[Loading Screen]");
        DontDestroyOnLoad(go);
        current = go.AddComponent<SceneLoaderRunner>();
        current.Build();
        current.StartCoroutine(current.Run(sceneName, index));
    }

    private void Build()
    {
        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 32000;
        CanvasScaler cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();

        Image bg = Make<Image>("Black", transform);
        bg.color = new Color(0.01f, 0.005f, 0.005f, 1f);
        Stretch(bg.rectTransform);

        Font f = Resources.Load<Font>("Intro/Oswald");
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Text title = Make<Text>("Loading", transform);
        title.font = f; title.fontSize = 44; title.color = new Color(0.7f, 0.12f, 0.08f); title.alignment = TextAnchor.LowerLeft;
        title.text = "LOADING";
        Place(title.rectTransform, 90f, 150f, 600f, 60f);
        dots = Make<Text>("Dots", transform);
        dots.font = f; dots.fontSize = 44; dots.color = title.color; dots.alignment = TextAnchor.LowerLeft;
        Place(dots.rectTransform, 280f, 150f, 200f, 60f);

        Image line = Make<Image>("Line", transform);
        line.color = new Color(0.5f, 0.1f, 0.06f, 0.8f);
        Place(line.rectTransform, 90f, 138f, 900f, 2f);

        Text tip = Make<Text>("Tip", transform);
        tip.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tip.fontSize = 26; tip.color = new Color(0.86f, 0.82f, 0.76f); tip.alignment = TextAnchor.UpperLeft;
        tip.text = "TIP:  " + SceneLoader.RandomTip();
        tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(tip.rectTransform, 90f, 60f, 1500f, 70f);
    }

    private IEnumerator Run(string sceneName, int index)
    {
        float shownAt = Time.unscaledTime;
        // the old scene is frozen and silent behind the loading screen (nothing can happen to the player meanwhile)
        Time.timeScale = 0f;
        AudioListener.pause = true;
        yield return null;   // draw the loading screen before the heavy work starts

        AsyncOperation op = sceneName != null ? SceneManager.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(index);
        if (op == null)
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            // scene missing from the build settings: fall back to the normal load so the game never gets stuck here
            if (sceneName != null) SceneManager.LoadScene(sceneName); else SceneManager.LoadScene(index);
        }
        else
        {
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - shownAt < 1.4f)
            {
                Animate();
                yield return null;
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            op.allowSceneActivation = true;
            while (!op.isDone) { Animate(); yield return null; }
        }

        // let the new scene settle (restore the world, place the player) behind the black screen, then fade out
        for (int i = 0; i < 3; i++) yield return null;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.6f;
            yield return null;
        }
        Busy = false;
        current = null;
        Destroy(gameObject);
    }

    private void Animate()
    {
        if (dots != null) dots.text = new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3);
    }

    private static T Make<T>(string name, Transform parent) where T : Component
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        T c = g.AddComponent<T>();
        if (c is Graphic gr) gr.raycastTarget = false;
        return c;
    }

    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero;
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
    }
}
