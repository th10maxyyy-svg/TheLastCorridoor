using UnityEngine;
using UnityEngine.UI;

/// <summary>References inside a list row / cell template, so scripts can fill it without searching by name.</summary>
public class TLCRefs : MonoBehaviour
{
    public Text[] texts;
    public Image[] images;
    public RectTransform[] rects;
    public TLCCard card;
    public Button button;
}
