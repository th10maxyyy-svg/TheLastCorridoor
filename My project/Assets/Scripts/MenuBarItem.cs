using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>One button of the bottom menu bar: dim when idle, bright + slightly bigger + glowing underline when selected (mouse or keyboard).</summary>
[RequireComponent(typeof(Button))]
public class MenuBarItem : MonoBehaviour, IPointerEnterHandler
{
    public Image icon;
    public Image underline;
    [Tooltip("The featured button (RESUME) pulses softly even when it is not selected")]
    public bool featured;
    [HideInInspector] public MenuBarController bar;
    [HideInInspector] public bool selected;

    private Button button;
    private float glow;
    private Vector3 baseScale = Vector3.one;
    private static readonly Color Ember = new Color(0.95f, 0.38f, 0.13f, 1f);

    void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    public bool Usable { get { if (button == null) button = GetComponent<Button>(); return button != null && button.interactable; } }

    public void Click() { if (Usable) button.onClick.Invoke(); }

    public void OnPointerEnter(PointerEventData e)
    {
        if (bar != null && Usable) bar.Select(this);
    }

    void Update()
    {
        bool usable = Usable;
        float target = (selected && usable) ? 1f : 0f;
        glow = Mathf.MoveTowards(glow, target, Time.unscaledDeltaTime * 6f);

        float c = usable ? Mathf.Lerp(0.74f, 1f, glow) : 0.38f;
        if (icon != null) icon.color = new Color(c, c, c, 1f);
        transform.localScale = baseScale * Mathf.Lerp(1f, 1.08f, glow);

        if (underline != null)
        {
            float w = glow;
            float a = glow;
            if (featured && usable && glow < 1f)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
                w = Mathf.Max(glow, 0.30f + 0.12f * p);
                a = Mathf.Max(glow, 0.35f + 0.25f * p);
            }
            Vector3 s = underline.rectTransform.localScale;
            underline.rectTransform.localScale = new Vector3(w, 1f, 1f);
            underline.color = new Color(Ember.r, Ember.g, Ember.b, a);
        }
    }
}
