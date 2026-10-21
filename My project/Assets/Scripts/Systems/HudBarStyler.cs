using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HEALTH / STAMINA HUD RESTYLE (minimal) - turns the two default Unity sliders (big white bars with drag handles)
/// into two thin, flat bars at runtime. No panel, no labels, no effects: just a dark track and a thin fill.
/// PlayerStats still sets the slider values exactly as before - only the look changes.
/// Created automatically by GameUI in the game level.
/// </summary>
public class HudBarStyler : MonoBehaviour
{
    private static readonly Color Track = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color HealthColor = new Color(0.78f, 0.16f, 0.16f, 1f);
    private static readonly Color StaminaColor = new Color(0.92f, 0.92f, 0.92f, 0.9f);

    private bool styled;

    void Start()
    {
        TryStyle();
    }

    void Update()
    {
        if (!styled) TryStyle();
    }

    private void TryStyle()
    {
        PlayerStats ps = FindAnyObjectByType<PlayerStats>();
        if (ps == null) return;
        Slider health = ps.HealthSlider;
        Slider stamina = ps.StaminaSlider;
        styled = true;
        if (health == null && stamina == null) return;

        float k = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f); // HUD canvas uses constant pixel size
        try
        {
            if (health != null) StyleBar(health, 30f, 8f, HealthColor, k);
            if (stamina != null) StyleBar(stamina, 18f, 5f, StaminaColor, k);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("HudBarStyler: could not restyle the bars (" + e.Message + ")");
        }
    }

    private static void StyleBar(Slider s, float y, float height, Color fillColor, float k)
    {
        s.interactable = false;
        s.transition = Selectable.Transition.None;
        if (s.handleRect != null) s.handleRect.gameObject.SetActive(false);
        s.handleRect = null;

        RectTransform rt = (RectTransform)s.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(24f, y) * k;
        rt.sizeDelta = new Vector2(220f, height) * k;

        Transform bg = s.transform.Find("Background");
        if (bg != null)
        {
            Image bgi = bg.GetComponent<Image>();
            if (bgi != null) { bgi.sprite = null; bgi.color = Track; }
            Stretch((RectTransform)bg);
        }

        if (s.fillRect != null)
        {
            RectTransform area = s.fillRect.parent as RectTransform;
            if (area != null) Stretch(area);
            s.fillRect.sizeDelta = Vector2.zero;
            s.fillRect.anchoredPosition = Vector2.zero;
            Image fill = s.fillRect.GetComponent<Image>();
            if (fill != null) { fill.sprite = null; fill.color = fillColor; }
        }
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
