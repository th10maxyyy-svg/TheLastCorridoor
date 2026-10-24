using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the extra HUD pieces on TLC_UI every frame (called by GameUI):
///   VITALS   - heart monitor (ECG line that speeds up with fear and low health), health bar with a "damage ghost",
///              stamina bar that fades away when full
///   DANGER   - red vignette that pulses with the heartbeat when the clown is near / chasing, or health is low
///   HIDDEN   - dark vignette + "HIDDEN" while the player is in a hiding spot
///   SUBTITLES - the current spoken line (see Subtitles)
/// </summary>
public static class TLCHud
{
    private static Texture2D ecgTex, vignetteTex;
    private static Color32[] ecgPx;
    private static float[] ecgY;
    private static float ecgCursor, beatPhase;
    private const int EW = 116, EH = 76;

    private static float shownHp = 1f, ghostHp = 1f, ghostHold, lastHp = 1f, hitFlash;
    private static float staminaFullFor, staminaAlpha = 1f;
    private static float danger, vignetteAlpha;
    private static EnemyAI enemy;
    private static float nextEnemyLook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { enemy = null; shownHp = ghostHp = lastHp = 1f; ecgY = null; }

    public static void Tick(TLCCanvas ui, PlayerController pc, bool hudVisible)
    {
        if (ui == null) return;
        UpdateSubtitles(ui);
        if (!hudVisible || pc == null) return;

        PlayerStats ps = pc.GetComponent<PlayerStats>();
        float hp = ps != null ? Mathf.Clamp01(ps.GetHealth() / Mathf.Max(1f, ps.MaxHealth)) : 1f;
        float st = ps != null ? Mathf.Clamp01(ps.GetStamina() / Mathf.Max(1f, ps.MaxStamina)) : 1f;
        bool dead = (ps != null && ps.IsDead()) || pc.isDead;
        float dt = Time.unscaledDeltaTime;

        // ---- how scared is the player? (the clown close by / chasing)
        if (Time.unscaledTime >= nextEnemyLook || enemy == null)
        {
            nextEnemyLook = Time.unscaledTime + 1f;
            if (enemy == null) enemy = Object.FindAnyObjectByType<EnemyAI>();
        }
        float target = 0f;
        if (enemy != null && enemy.isActiveAndEnabled && enemy.State != EnemyAI.AIState.Dormant && enemy.State != EnemyAI.AIState.CinematicEscape)
        {
            bool sameFloor = Mathf.Abs(enemy.transform.position.y - pc.transform.position.y) < 2.2f;
            if (sameFloor || enemy.State == EnemyAI.AIState.Chase)
            {
                float d = Vector3.Distance(enemy.transform.position, pc.transform.position);
                target = Mathf.Clamp01(1f - (d - 3f) / 12f);
            }
            if (enemy.State == EnemyAI.AIState.Chase) target = Mathf.Max(target, 0.65f);
        }
        if (pc.isHidden) target *= 0.6f;
        danger = Mathf.MoveTowards(danger, target, dt * (target > danger ? 1.5f : 0.4f));

        float bpm = dead ? 0f : 68f + (1f - hp) * 45f + danger * 60f;
        UpdateVitals(ui, hp, st, bpm, dead, dt);
        UpdateVignette(ui, pc.isHidden, hp, bpm, dead, dt);
    }

    // ------------------------------------------------------------------ vitals
    private static void UpdateVitals(TLCCanvas ui, float hp, float st, float bpm, bool dead, float dt)
    {
        if (ui.vitals == null) return;

        // health: fast fill, slow "ghost" that shows what was just lost
        if (hp < lastHp - 0.001f) { ghostHold = 0.7f; hitFlash = 1f; }
        lastHp = hp;
        shownHp = Mathf.MoveTowards(shownHp, hp, dt * 1.5f);
        if (ghostHold > 0f) ghostHold -= dt; else ghostHp = Mathf.MoveTowards(ghostHp, shownHp, dt * 0.35f);
        if (ghostHp < shownHp) ghostHp = shownHp;
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 2.5f);
        SetWidth(ui.hpFill, shownHp);
        SetWidth(ui.hpGhost, ghostHp);

        if (ui.hpFillImage != null)
        {
            float low = hp < 0.3f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * bpm / 60f * Mathf.PI * 2f) : 0f;
            Color baseC = Color.Lerp(new Color(0.62f, 0.04f, 0.04f), new Color(0.48f, 0.03f, 0.03f), 1f - hp);
            ui.hpFillImage.color = Color.Lerp(baseC, new Color(0.95f, 0.15f, 0.1f), low * 0.6f + hitFlash * 0.5f);
        }
        if (ui.hpFrame != null)
        {
            Outline o = ui.hpFrame.GetComponent<Outline>();
            if (o != null) o.effectColor = Color.Lerp(new Color(0.75f, 0.68f, 0.6f, 0.35f), new Color(1f, 0.85f, 0.8f, 0.9f), hitFlash);
        }
        SetText(ui.vitalsHp, Mathf.CeilToInt(hp * 100f).ToString());
        SetText(ui.vitalsBpm, dead ? "- - -" : Mathf.RoundToInt(bpm) + " BPM");
        if (ui.vitalsLabel != null) SetText(ui.vitalsLabel, hp < 0.3f ? "CRITICAL" : danger > 0.5f ? "PANIC" : "VITALS");

        // stamina: only visible while it is being used / refilling
        SetWidth(ui.stFill, st);
        staminaFullFor = st >= 0.995f ? staminaFullFor + dt : 0f;
        staminaAlpha = Mathf.MoveTowards(staminaAlpha, staminaFullFor > 1.5f ? 0f : 1f, dt * 2f);
        if (ui.staminaGroup != null) ui.staminaGroup.alpha = staminaAlpha;

        DrawEcg(ui.vitalsEcg, bpm, dead, dt);
    }

    private static void SetWidth(RectTransform r, float v)
    {
        if (r == null) return;
        Vector2 m = r.anchorMax; m.x = Mathf.Clamp01(v);
        if (r.anchorMax != m) r.anchorMax = m;
    }

    private static void SetText(Text t, string s) { if (t != null && t.text != s) t.text = s; }

    // a real sweeping heart monitor: the line is written left to right, a small gap erases the old trace ahead of it
    private static void DrawEcg(RawImage img, float bpm, bool dead, float dt)
    {
        if (img == null) return;
        if (ecgTex == null)
        {
            ecgTex = new Texture2D(EW, EH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            ecgPx = new Color32[EW * EH];
        }
        if (ecgY == null) { ecgY = new float[EW]; for (int i = 0; i < EW; i++) ecgY[i] = float.NaN; }
        if (img.texture != ecgTex) img.texture = ecgTex;

        const float speed = 70f;   // pixels per second
        float before = ecgCursor;
        ecgCursor += speed * dt;
        int from = Mathf.FloorToInt(before), to = Mathf.FloorToInt(ecgCursor);
        for (int c = from + 1; c <= to; c++)
        {
            beatPhase += (bpm / 60f) / speed;
            if (beatPhase > 1f) beatPhase -= 1f;
            int x = ((c % EW) + EW) % EW;
            ecgY[x] = dead ? 0f : Wave(beatPhase);
            for (int g = 1; g <= 7; g++) ecgY[(x + g) % EW] = float.NaN;   // the gap in front of the cursor
        }
        if (ecgCursor > EW * 1000f) ecgCursor -= EW * 1000f;

        // redraw: faint grid + the trace
        Color32 bg = new Color32(0, 10, 4, 255), grid = new Color32(30, 48, 30, 255);
        for (int y = 0; y < EH; y++)
            for (int x = 0; x < EW; x++)
                ecgPx[y * EW + x] = (x % 12 == 0 || y % 12 == 0) ? grid : bg;
        int head = ((Mathf.FloorToInt(ecgCursor) % EW) + EW) % EW;
        Color32 line = dead ? new Color32(150, 150, 150, 255) : new Color32(255, 70, 55, 255);
        for (int x = 1; x < EW; x++)
        {
            if (float.IsNaN(ecgY[x]) || float.IsNaN(ecgY[x - 1])) continue;
            int y0 = Mathf.RoundToInt(EH * 0.42f + ecgY[x - 1] * EH * 0.42f), y1 = Mathf.RoundToInt(EH * 0.42f + ecgY[x] * EH * 0.42f);
            int lo = Mathf.Clamp(Mathf.Min(y0, y1), 0, EH - 1), hi = Mathf.Clamp(Mathf.Max(y0, y1), 0, EH - 1);
            int age = (head - x + EW) % EW;
            byte fade = (byte)Mathf.Clamp(255 - age * 1.4f, 70, 255);
            Color32 c = new Color32(line.r, line.g, line.b, 255);
            c.r = (byte)(c.r * fade / 255); c.g = (byte)(c.g * fade / 255); c.b = (byte)(c.b * fade / 255);
            for (int y = lo; y <= hi; y++)
            {
                ecgPx[y * EW + x] = c;
                if (y + 1 < EH) ecgPx[(y + 1) * EW + x] = Color32.Lerp(ecgPx[(y + 1) * EW + x], c, 0.45f);   // a little glow
            }
        }
        // bright dot at the cursor
        if (!float.IsNaN(ecgY[head]))
        {
            int hy = Mathf.Clamp(Mathf.RoundToInt(EH * 0.42f + ecgY[head] * EH * 0.42f), 1, EH - 2);
            for (int dy = -1; dy <= 1; dy++) ecgPx[(hy + dy) * EW + head] = new Color32(255, 220, 200, 255);
        }
        ecgTex.SetPixels32(ecgPx);
        ecgTex.Apply(false);
    }

    // one heart beat (P wave, QRS spike, T wave) over phase 0..1
    private static float Wave(float p)
    {
        float y = 0f;
        y += 0.10f * Bump(p, 0.12f, 0.035f);
        y -= 0.12f * Bump(p, 0.235f, 0.010f);
        y += 1.00f * Bump(p, 0.255f, 0.012f);
        y -= 0.28f * Bump(p, 0.278f, 0.012f);
        y += 0.22f * Bump(p, 0.45f, 0.06f);
        return y;
    }
    private static float Bump(float p, float c, float w) { float d = (p - c) / w; return Mathf.Exp(-d * d); }

    // ------------------------------------------------------------------ vignette + hidden
    private static void UpdateVignette(TLCCanvas ui, bool hidden, float hp, float bpm, bool dead, float dt)
    {
        if (ui.hiddenLabel != null && ui.hiddenLabel.gameObject.activeSelf != hidden) ui.hiddenLabel.gameObject.SetActive(hidden);
        if (ui.dangerVignette == null) return;
        if (vignetteTex == null) vignetteTex = MakeVignette();
        if (ui.dangerVignette.texture != vignetteTex) ui.dangerVignette.texture = vignetteTex;

        float want = (!dead && hidden) ? 0.75f : 0f;
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, want, dt * 2.5f);
        ui.dangerVignette.color = new Color(0f, 0f, 0f, vignetteAlpha);
        bool on = vignetteAlpha > 0.005f;
        if (ui.dangerVignette.enabled != on) ui.dangerVignette.enabled = on;
    }

    private static Texture2D MakeVignette()
    {
        const int S = 256;
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.85f + dy * dy * 1.1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1.25f, r));
                px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(255f * a), 0, 255));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    // ------------------------------------------------------------------ subtitles
    private static void UpdateSubtitles(TLCCanvas ui)
    {
        if (ui.subtitleGroup == null || ui.subtitleText == null) return;
        float now = Time.unscaledTime;
        bool on = now < Subtitles.Until && !string.IsNullOrEmpty(Subtitles.Current);
        if (on && ui.subtitleText.text != Subtitles.Current)
        {
            ui.subtitleText.text = Subtitles.Current;
            if (ui.subtitleBack != null)
                ui.subtitleBack.rectTransform.sizeDelta = new Vector2(Mathf.Min(ui.subtitleText.preferredWidth + 48f, 1300f), 52f);
        }
        float a = on ? Mathf.Clamp01(Mathf.Min((now - Subtitles.StartedAt) * 6f, (Subtitles.Until - now) * 3f)) : 0f;
        ui.subtitleGroup.alpha = Mathf.MoveTowards(ui.subtitleGroup.alpha, a, Time.unscaledDeltaTime * 6f);
    }
}
