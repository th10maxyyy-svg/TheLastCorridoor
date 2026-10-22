using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Button label turns bright on hover (like the menu buttons) and dim when the button is disabled.</summary>
public class TLCButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Text label;
    public Color normal = new Color(0.82f, 0.66f, 0.54f), hover = new Color(1f, 0.93f, 0.85f), disabled = new Color(0.45f, 0.40f, 0.36f);
    private Button button;
    private bool over;
    void Awake() { button = GetComponent<Button>(); }
    public void OnPointerEnter(PointerEventData e) { over = true; }
    public void OnPointerExit(PointerEventData e) { over = false; }
    void OnDisable() { over = false; }
    void LateUpdate()
    {
        if (label == null) return;
        Color c = button != null && !button.interactable ? disabled : over ? hover : normal;
        if (label.color != c) label.color = c;
    }
}
