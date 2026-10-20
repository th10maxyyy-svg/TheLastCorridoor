using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ADAPTIVE AI - Behavior Tracker + Skill Classifier + Difficulty Controller
/// (implements Section 7.4 of the FYP document: "Adaptive Difficulty Algorithm Specification").
///
/// 1. Records how the player plays: hiding spots, movement (stops), puzzle/progress time, deaths, health and close encounters.
/// 2. Every evaluation (every 'evaluationInterval' seconds and whenever a puzzle/door step is completed) it computes a
///    COMFORT SCORE between 0 and 100 from five factors (Table 7.3):
///        Hiding pattern 25 | Movement pattern 20 | Puzzle time 20 | Deaths in section 20 | Survival 15
/// 3. The score gives a SKILL CLASS (Table 7.4): Struggling 0-39, Balanced 40-69, Confident 70-100.
///    A class must come out twice in a row before it is confirmed (stops visible swings).
/// 4. A confirmed class moves enemy AGGRESSION (1-5) by at most ONE level (FR_60):
///        Struggling = -1 level, smaller search, extra hints | Balanced = no change | Confident = +1 level, wider search,
///        and at level 4+ the Joker checks the player's favourite hiding spot.
/// Class, score and aggression are stored in the save file, so a respawn/resume keeps the same difficulty.
/// Press F8 in game to show the live DDA panel (useful for testing and the viva demo).
/// </summary>
public class PlayerBehaviorTracker : MonoBehaviour
{
    public static PlayerBehaviorTracker Instance { get; private set; }

    [Header("Evaluation")]
    [Tooltip("Seconds between comfort-score evaluations (also evaluated after every puzzle/door step)")]
    [SerializeField] private float evaluationInterval = 30f;
    [Tooltip("How many evaluations in a row must give the same class before it is confirmed")]
    [SerializeField] private int confirmEvaluations = 2;

    [Header("Puzzle time (seconds per puzzle/progress step)")]
    [SerializeField] private float fastPuzzleTime = 120f;
    [SerializeField] private float slowPuzzleTime = 300f;

    [Header("Help for struggling players")]
    [SerializeField] private float hintCooldown = 90f;

    [Header("Debug")]
    [SerializeField] private bool showPanel = false;

    public enum SkillClass { Struggling, Balanced, Confident }

    // --- live values (shown on the F8 panel) ---
    public float ComfortScore { get; private set; } = 60f;
    public SkillClass CurrentClass { get; private set; } = SkillClass.Balanced;
    public int Aggression { get; private set; } = 3;
    private int sHide, sMove, sPuzzle, sDeath, sSurvive;

    private PlayerController playerController;
    private PlayerStats playerStats;
    private EnemyAI enemyAI;

    // window counters (reset after every evaluation)
    private float windowTime, stoppedTime, sprintTime, crouchTime;
    private int longStops, closeEncounters;
    private float currentStopLength;
    private float minHealthInWindow = 100f;
    private EnemyAI.AIState lastEnemyState;

    private float nextEvaluationTime;
    private float lastEvaluationTime = -999f;
    private SkillClass pendingClass = SkillClass.Balanced;
    private int pendingCount = 0;
    private float nextHintTime = 0f;
    private int hintIndex = 0;

    private class SpotUse { public string id; public int count; public Vector3 pos; }
    private readonly List<SpotUse> spots = new List<SpotUse>();

    private static readonly string[] hints =
    {
        "Hint: Crouch (Ctrl) to move silently - he hunts by sound.",
        "Hint: When you hear his footsteps, find a cupboard and press E to hide.",
        "Hint: Read the notes - they tell you where the next key or code is.",
        "Hint: Sprinting is loud and drains stamina. Walk when he is near.",
        "Hint: Broken glass on the floor makes noise. Step around it.",
    };

    // ------------------------------------------------------------------------------------------------ lifecycle
    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerStats = GetComponent<PlayerStats>();
        enemyAI = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);
        LoadFromSave();
        nextEvaluationTime = Time.time + evaluationInterval;
        if (playerStats != null) minHealthInWindow = playerStats.GetHealth();
        ApplyToEnemy();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8)) showPanel = !showPanel;
        if (playerStats == null || playerStats.IsDead()) return;

        float dt = Time.deltaTime;
        if (Save != null) Save.playTime += dt;
        windowTime += dt;

        // movement pattern (ignored while hidden - hiding is measured separately)
        if (playerController != null && !playerController.isHidden)
        {
            if (playerController.IsSprinting) sprintTime += dt;
            if (playerController.IsCrouching) crouchTime += dt;
            if (playerController.GetCurrentSpeed() < 0.2f)
            {
                stoppedTime += dt;
                currentStopLength += dt;
            }
            else
            {
                if (currentStopLength > 4f) longStops++;
                currentStopLength = 0f;
            }
        }

        minHealthInWindow = Mathf.Min(minHealthInWindow, playerStats.GetHealth());

        // close encounters = every time the Joker starts a chase
        if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>();
        if (enemyAI != null)
        {
            if (enemyAI.State == EnemyAI.AIState.Chase && lastEnemyState != EnemyAI.AIState.Chase) closeEncounters++;
            lastEnemyState = enemyAI.State;
        }

        if (Time.time >= nextEvaluationTime) Evaluate();
    }

    // ------------------------------------------------------------------------------------------------ static reports
    /// <summary>Called by SaveManager whenever a door is unlocked or a one-time event happens (= a puzzle/section step).</summary>
    public static void ReportProgress()
    {
        if (Instance == null || Instance.Save == null) return;
        SaveData s = Instance.Save;
        s.lastPuzzleDuration = s.playTime - s.lastProgressTime;
        s.lastProgressTime = s.playTime;
        s.deathsAtLastProgress = Instance.TotalDeaths();
        // a completed step is a natural checkpoint for an evaluation (but not more often than every 10 s)
        if (Time.time - Instance.lastEvaluationTime > 10f) Instance.Evaluate();
    }

    /// <summary>Called by HidingSpot when the player hides.</summary>
    public static void ReportHide(Component spot)
    {
        if (Instance == null || spot == null) return;
        string id = SaveManager.GetSceneObjectId(spot.transform);
        SpotUse use = Instance.spots.Find(u => u.id == id);
        if (use == null)
        {
            use = new SpotUse { id = id, count = 0, pos = spot.transform.position };
            Instance.spots.Add(use);
        }
        use.count++;
        Instance.WriteSpotsToSave();
        Instance.ApplyToEnemy();
    }

    // ------------------------------------------------------------------------------------------------ the algorithm
    private void Evaluate()
    {
        nextEvaluationTime = Time.time + evaluationInterval;
        if (windowTime < 5f || playerStats == null) return; // not enough data yet
        lastEvaluationTime = Time.time;

        // 1) HIDING PATTERN (25): same place repeatedly 25 | varied places 12 | rare or no hiding 5
        int totalHides = 0; SpotUse fav = FavouriteSpot();
        foreach (SpotUse u in spots) totalHides += u.count;
        if (fav != null && fav.count >= 3 && fav.count >= totalHides * 0.6f) sHide = 25;
        else if (totalHides >= 2) sHide = 12;
        else sHide = 5;

        // 2) MOVEMENT PATTERN (20): steady 20 | mixed 12 | hesitant with frequent long stops 5
        float stopRatio = stoppedTime / Mathf.Max(1f, windowTime);
        if (stopRatio < 0.25f && longStops <= 1) sMove = 20;
        else if (stopRatio < 0.5f && longStops <= 3) sMove = 12;
        else sMove = 5;

        // 3) PUZZLE COMPLETION TIME (20): faster than expected 20 | expected range 12 | slower 5
        float puzzleTime = Save != null ? Save.lastPuzzleDuration : -1f;
        float sinceLast = Save != null ? Save.playTime - Save.lastProgressTime : 0f;
        if (sinceLast > slowPuzzleTime) sPuzzle = 5;               // stuck on the current puzzle right now
        else if (puzzleTime < 0f) sPuzzle = 12;                   // no puzzle finished yet
        else if (puzzleTime <= fastPuzzleTime) sPuzzle = 20;
        else if (puzzleTime <= slowPuzzleTime) sPuzzle = 12;
        else sPuzzle = 5;

        // 4) DEATHS IN THE CURRENT SECTION (20): none 20 | one or two 12 | three or more 5
        int sectionDeaths = TotalDeaths() - (Save != null ? Save.deathsAtLastProgress : 0);
        if (sectionDeaths <= 0) sDeath = 20;
        else if (sectionDeaths <= 2) sDeath = 12;
        else sDeath = 5;

        // 5) SURVIVAL (15): high health + few close encounters 15 | moderate 9 | low health / many encounters 3
        float hp = playerStats.GetHealth();
        if (hp >= 70f && minHealthInWindow >= 50f && closeEncounters <= 1) sSurvive = 15;
        else if (hp >= 35f && closeEncounters <= 3) sSurvive = 9;
        else sSurvive = 3;

        ComfortScore = sHide + sMove + sPuzzle + sDeath + sSurvive; // 23..100

        SkillClass result = ComfortScore < 40f ? SkillClass.Struggling : (ComfortScore < 70f ? SkillClass.Balanced : SkillClass.Confident);

        // confirm the class over several evaluations
        if (result == pendingClass) pendingCount++;
        else { pendingClass = result; pendingCount = 1; }

        if (pendingCount >= confirmEvaluations)
        {
            CurrentClass = pendingClass;
            pendingCount = 0; // the next step needs a fresh confirmation (max one level per confirmed result)
            if (CurrentClass == SkillClass.Confident) Aggression = Mathf.Min(5, Aggression + 1);
            else if (CurrentClass == SkillClass.Struggling) Aggression = Mathf.Max(1, Aggression - 1);
        }

        if (CurrentClass == SkillClass.Struggling && Time.time >= nextHintTime)
        {
            nextHintTime = Time.time + hintCooldown;
            InteractionRaycast.Notify(playerStats, hints[hintIndex % hints.Length], 5f);
            hintIndex++;
        }

        ApplyToEnemy();
        WriteToSave();

        Debug.Log($"[DDA] Comfort {ComfortScore} (hide {sHide}, move {sMove}, puzzle {sPuzzle}, deaths {sDeath}, survival {sSurvive}) " +
                  $"-> {result} | confirmed {CurrentClass} | aggression {Aggression}");

        // reset the window
        windowTime = stoppedTime = sprintTime = crouchTime = 0f;
        longStops = closeEncounters = 0;
        minHealthInWindow = hp;
    }

    private void ApplyToEnemy()
    {
        if (enemyAI == null) enemyAI = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);
        if (enemyAI == null) return;

        // small play-style modifiers on top of the aggression level (kept from the first DDA version)
        float w = Mathf.Max(1f, windowTime);
        float speedBonus = 0f, rangeBonus = 0f;
        if (sprintTime / w > 0.4f) rangeBonus += 3f;          // constant sprinting is loud
        if (crouchTime / w > 0.5f) rangeBonus -= 2f;          // careful, quiet play
        if (playerStats != null && playerStats.GetHealth() < 30f) { speedBonus -= 0.5f; rangeBonus -= 3f; } // mercy at low health

        enemyAI.ApplyAggression(Aggression, speedBonus, rangeBonus);

        SpotUse fav = FavouriteSpot();
        enemyAI.SetFavouriteHidingSpot(fav != null && fav.count >= 2, fav != null ? fav.pos : Vector3.zero);
    }

    // ------------------------------------------------------------------------------------------------ helpers
    private SaveData Save => SaveManager.instance != null ? SaveManager.instance.currentSaveData : null;

    private int TotalDeaths() => Save != null ? Mathf.Max(0, 3 - Save.lives) : 0;

    private SpotUse FavouriteSpot()
    {
        SpotUse best = null;
        foreach (SpotUse u in spots) if (best == null || u.count > best.count) best = u;
        return best;
    }

    private void LoadFromSave()
    {
        SaveData s = Save;
        if (s == null) return;
        Aggression = Mathf.Clamp(s.aggressionLevel <= 0 ? 3 : s.aggressionLevel, 1, 5);
        ComfortScore = s.comfortScore;
        if (System.Enum.TryParse(s.skillClass, out SkillClass c)) { CurrentClass = c; pendingClass = c; }

        spots.Clear();
        if (!string.IsNullOrEmpty(s.hidingSpotsCSV))
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var fl = System.Globalization.NumberStyles.Float;
            foreach (string e in s.hidingSpotsCSV.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = e.Split('|');
                if (p.Length < 5) continue;
                if (int.TryParse(p[1], out int n) && float.TryParse(p[2], fl, inv, out float x) &&
                    float.TryParse(p[3], fl, inv, out float y) && float.TryParse(p[4], fl, inv, out float z))
                    spots.Add(new SpotUse { id = p[0], count = n, pos = new Vector3(x, y, z) });
            }
        }
    }

    private void WriteToSave()
    {
        SaveData s = Save;
        if (s == null) return;
        s.aggressionLevel = Aggression;
        s.skillClass = CurrentClass.ToString();
        s.comfortScore = ComfortScore;
        WriteSpotsToSave();
    }

    private void WriteSpotsToSave()
    {
        SaveData s = Save;
        if (s == null) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (SpotUse u in spots)
            sb.Append(u.id).Append('|').Append(u.count).Append('|').Append(u.pos.x.ToString(inv)).Append('|')
              .Append(u.pos.y.ToString(inv)).Append('|').Append(u.pos.z.ToString(inv)).Append(';');
        s.hidingSpotsCSV = sb.ToString();
    }

    // ------------------------------------------------------------------------------------------------ F8 debug panel
    void OnGUI()
    {
        if (!showPanel) return;
        GUILayout.BeginArea(new Rect(10, 10, 340, 240), GUI.skin.box);
        GUILayout.Label("ADAPTIVE AI  (press F8 to hide)");
        GUILayout.Label($"Comfort score: {ComfortScore:0} / 100");
        GUILayout.Label($"  hiding {sHide}/25   movement {sMove}/20   puzzle {sPuzzle}/20");
        GUILayout.Label($"  deaths {sDeath}/20   survival {sSurvive}/15");
        GUILayout.Label($"Skill class: {CurrentClass}   (next: {pendingClass} x{pendingCount})");
        GUILayout.Label($"Enemy aggression: {Aggression} / 5");
        if (enemyAI != null)
            GUILayout.Label($"Chase speed {enemyAI.CurrentChaseSpeed:0.0}   detection {enemyAI.CurrentDetectionRange:0.0} m");
        SpotUse fav = FavouriteSpot();
        GUILayout.Label(fav != null ? $"Favourite hiding spot used {fav.count}x" : "No hiding spot used yet");
        GUILayout.Label($"Next evaluation in {Mathf.Max(0f, nextEvaluationTime - Time.time):0} s");
        GUILayout.EndArea();
    }
}
