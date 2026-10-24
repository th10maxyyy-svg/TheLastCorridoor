using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// PRESENTATION MODE (for the FYP demo / viva): a hidden tool panel in the game level.
///   Open / close:  F9  (in the Unity Editor and in Development builds)   or   CTRL + SHIFT + F9  (any build)
///   - teleport straight into any room
///   - get any key / item
///   - God mode (no damage), freeze the clown, full health
///   - play the escape ending (test run: nothing is saved)
/// Nothing here is visible to a normal player. Created automatically in the game level by GameUI.
/// </summary>
public class PresentationMode : MonoBehaviour
{
    private bool open;
    private Vector2 scroll;
    private int tab;
    private PlayerController player;
    private readonly List<KeyValuePair<string, Transform>> rooms = new List<KeyValuePair<string, Transform>>();
    private bool built;
    private GUIStyle box, btn, head;
    private bool frozenEnemy;

    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsOpen = false; }

    void Update()
    {
        bool combo = Input.GetKeyDown(KeyCode.F9) && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift);
        bool quick = Input.GetKeyDown(KeyCode.F9) && (Application.isEditor || Debug.isDebugBuild);
        if (!(combo || quick)) return;
        if (!open && (Time.timeScale <= 0f || player == null && FindAnyObjectByType<PlayerController>() == null)) return;   // not while paused
        if (!open) { player = FindAnyObjectByType<PlayerController>(); if (player != null && (player.isDead || player.isCinematicPlaying || player.isHidden)) return; }
        Toggle(!open);
    }

    private void Toggle(bool on)
    {
        open = on; IsOpen = on;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (on) { Build(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (player != null) player.enabled = !on;
    }

    void OnDisable() { if (open) Toggle(false); }

    private void Build()
    {
        if (built) return;
        built = true;
        GameObject asylum = GameObject.Find("Asylum Variant");
        if (asylum == null) return;
        foreach (Transform floor in asylum.transform)
        {
            if (floor.name != "1stFloor" && floor.name != "2ndFloor" && floor.name != "Basement") continue;
            string fl = floor.name == "1stFloor" ? "Ground" : floor.name == "2ndFloor" ? "First" : "Basement";
            foreach (Transform room in floor) rooms.Add(new KeyValuePair<string, Transform>(fl + " - " + room.name, room));
        }
        rooms.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
    }

    void OnGUI()
    {
        if (!open) return;
        if (box == null)
        {
            box = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            head = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }
        float w = 460f, h = Mathf.Min(Screen.height - 40f, 720f);
        GUILayout.BeginArea(new Rect(20f, 20f, w, h), box);
        GUILayout.Label("PRESENTATION MODE   (F9 to close)", head);
        tab = GUILayout.Toolbar(tab, new[] { "Rooms", "Items", "Cheats" });
        scroll = GUILayout.BeginScrollView(scroll);
        if (tab == 0) foreach (var r in rooms) { if (r.Value != null && GUILayout.Button(r.Key, btn)) Teleport(r.Value); }
        else if (tab == 1) ItemsTab();
        else CheatsTab();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void ItemsTab()
    {
        HashSet<string> seen = new HashSet<string>();
        foreach (KeyItem k in FindObjectsByType<KeyItem>(FindObjectsInactive.Include))
        {
            string n = k.GetKeyName();
            if (string.IsNullOrEmpty(n) || !seen.Add(n.ToLower())) continue;
            if (GUILayout.Button("Get:  " + n, btn) && player != null)
            {
                Inventory inv = player.GetComponent<Inventory>();
                if (inv != null) { k.gameObject.SetActive(true); inv.PickUpItem(k.gameObject); }
            }
        }
    }

    private void CheatsTab()
    {
        PlayerStats ps = player != null ? player.GetComponent<PlayerStats>() : null;
        if (ps != null)
        {
            bool god = GUILayout.Toggle(ps.isInvulnerable, "  God mode (no damage)");
            if (god != ps.isInvulnerable) ps.isInvulnerable = god;
            if (GUILayout.Button("Full health", btn)) ps.Heal(1000f);
        }
        EnemyAI e = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);
        if (e != null)
        {
            bool f = GUILayout.Toggle(frozenEnemy, "  Freeze the clown (hidden)");
            if (f != frozenEnemy) { frozenEnemy = f; e.gameObject.SetActive(!f); }
        }
        // VIVA: open every locked room door for the demo (nothing is saved; untick to lock them again)
        bool doors = GUILayout.Toggle(TestDoorUnlock.IsOn, "  Unlock all room doors (viva demo - not saved)");
        if (doors != TestDoorUnlock.IsOn) TestDoorUnlock.SetNow(doors);
        GUILayout.Space(8f);
        if (GUILayout.Button("Play the escape ending (test - nothing is saved)", btn)) { Toggle(false); EscapeCinematicManager.PlayNow(); }
        GUILayout.Label("Objectives, the map and notes are in the journal (TAB).");
    }

    private void Teleport(Transform room)
    {
        if (player == null) return;
        Bounds b = new Bounds(room.position, Vector3.zero); bool any = false;
        foreach (Renderer r in room.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        Vector3 target = b.center;
        if (NavMesh.SamplePosition(new Vector3(target.x, b.min.y + 0.5f, target.z), out NavMeshHit hit, 6f, NavMesh.AllAreas)) target = hit.position;
        else target.y = b.min.y + 0.2f;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = target + Vector3.up * 1.05f;
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
        Toggle(false);
        InteractionRaycast.Notify(player, "Teleported: " + room.name, 2f);
    }
}
