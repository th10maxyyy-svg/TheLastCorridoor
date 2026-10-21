using UnityEngine;
using UnityEngine.UI;

/// <summary>Little embers drifting up over the bottom of the main menu. The ember Images are normal children (visible in the Hierarchy); this only moves them.</summary>
public class MenuEmbers : MonoBehaviour
{
    public float riseHeight = 280f;
    public float baseY = 100f;

    private RectTransform rt;
    private Image[] embers;
    private float[] xFrac, speed, phase, sway, size;

    void Awake()
    {
        rt = transform as RectTransform;
        embers = GetComponentsInChildren<Image>(true);
        int n = embers.Length;
        xFrac = new float[n]; speed = new float[n]; phase = new float[n]; sway = new float[n]; size = new float[n];
        System.Random r = new System.Random(11);
        for (int i = 0; i < n; i++)
        {
            xFrac[i] = (i + 0.5f) / n + ((float)r.NextDouble() - 0.5f) * 0.05f;
            speed[i] = 0.10f + (float)r.NextDouble() * 0.08f;
            phase[i] = (float)r.NextDouble();
            sway[i] = 8f + (float)r.NextDouble() * 18f;
            size[i] = 6f + (float)r.NextDouble() * 8f;
            embers[i].raycastTarget = false;
            embers[i].rectTransform.sizeDelta = new Vector2(size[i], size[i]);
        }
    }

    void Update()
    {
        if (rt == null) return;
        float w = rt.rect.width;
        for (int i = 0; i < embers.Length; i++)
        {
            float t = (Time.unscaledTime * speed[i] + phase[i]) % 1f;
            float x = xFrac[i] * w + Mathf.Sin(Time.unscaledTime * 0.9f + phase[i] * 20f) * sway[i];
            embers[i].rectTransform.anchoredPosition = new Vector2(x, baseY + t * riseHeight);
            float a = Mathf.Sin(t * Mathf.PI);
            embers[i].color = new Color(1f, 1f, 1f, a * 0.85f);
        }
    }
}
