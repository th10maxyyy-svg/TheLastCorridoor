using UnityEngine;
using UnityEngine.UI;

/// <summary>Dark card with a rust (or ember-gold when highlighted) outline and stripe.</summary>
public class TLCCard : MonoBehaviour
{
    public Image background, stripe;
    public Image[] lines;
    public void Set(bool hi)
    {
        if (background != null) background.color = hi ? TLCUIFactory.CardHi : TLCUIFactory.CardBg;
        if (stripe != null) stripe.color = hi ? new Color(1f, 0.70f, 0.22f, 1f) : new Color(0.50f, 0.20f, 0.10f, 1f);
        if (lines != null) foreach (Image l in lines) if (l != null) l.color = hi ? new Color(0.90f, 0.62f, 0.20f, 0.55f) : new Color(0.45f, 0.22f, 0.11f, 0.60f);
    }
}
