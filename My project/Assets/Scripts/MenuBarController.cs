using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The bottom bar of the main menu: keeps the buttons fitted to the screen width, and lets A / D / arrow keys + Enter drive it.
/// (Mouse hover is handled by each MenuBarItem.)
/// </summary>
public class MenuBarController : MonoBehaviour
{
    public List<MenuBarItem> items = new List<MenuBarItem>();
    [Tooltip("Optional: the menu's fade-in group - the keyboard stays disabled until the intro cinematic has faded the menu in")]
    public CanvasGroup fadeGroup;

    private int index = -1;
    private RectTransform rt;
    private Canvas canvas;

    void Awake()
    {
        rt = transform as RectTransform;
        foreach (MenuBarItem it in items) if (it != null) it.bar = this;
        if (fadeGroup == null) fadeGroup = GetComponentInParent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void Select(MenuBarItem item)
    {
        int i = items.IndexOf(item);
        if (i >= 0) SetIndex(i);
    }

    private void SetIndex(int i)
    {
        index = i;
        for (int k = 0; k < items.Count; k++) if (items[k] != null) items[k].selected = (k == index);
    }

    private void Step(int dir)
    {
        if (items.Count == 0) return;
        int i = index;
        for (int n = 0; n < items.Count; n++)
        {
            i = (i + dir + items.Count) % items.Count;
            if (items[i] != null && items[i].Usable) { SetIndex(i); return; }
        }
    }

    void Update()
    {
        // pick a sensible default: RESUME when there is a save, otherwise the next usable button
        if (index < 0 || items[index] == null || !items[index].Usable)
        {
            for (int k = 0; k < items.Count; k++)
                if (items[k] != null && items[k].Usable) { SetIndex(k); break; }
        }

        if (fadeGroup != null && fadeGroup.alpha < 0.95f) return;   // still in the intro cinematic
        if (GameUI.IsMenuPanelOpen) return;                          // load / achievements / options panel is open

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && index >= 0 && items[index] != null)
            items[index].Click();
    }

    void LateUpdate()
    {
        // never wider than the screen
        if (rt == null || canvas == null) return;
        RectTransform root = canvas.rootCanvas.transform as RectTransform;
        float contentW = rt.sizeDelta.x * 1.1f;
        if (root == null || contentW <= 1f) return;
        float s = Mathf.Min(1f, (root.rect.width * 0.96f) / contentW);
        rt.localScale = new Vector3(s, s, 1f);
    }
}
