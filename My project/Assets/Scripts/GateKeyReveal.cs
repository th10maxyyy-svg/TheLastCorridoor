using UnityEngine;

/// <summary>
/// Holds one GATE KEY (its child object, hidden at first) and shows it when its puzzle is done:
///   - solvedPuzzle   : any puzzle script with an IsSolved property (AcidDrainPuzzle, OvenPuzzleManager...)
///   - openedDoor     : a door / safe door that must be open
///   - goneObject     : an object that must be gone (e.g. the classroom angel after you touch it)
///   - objectiveId    : or the saved objective event (OBJ_id), so the key also appears after a death / load
/// When it appears it is put on the first surface under 'dropPoint' (or above the anchor) and gets a faint gold glow
/// so the player can find it. Created by the menu: The Last Corridor / Setup Main Gate (5 keys).
/// </summary>
public class GateKeyReveal : MonoBehaviour
{
    public GameObject key;
    public MonoBehaviour solvedPuzzle;
    public DoorController openedDoor;
    public GameObject goneObject;
    public string objectiveId;
    [Tooltip("Where the key appears (it is dropped onto the surface below this point)")]
    public Transform dropPoint;
    public bool trackGone;

    private bool revealed;
    private System.Reflection.PropertyInfo solvedProp;

    void Start()
    {
        if (solvedPuzzle != null) solvedProp = solvedPuzzle.GetType().GetProperty("IsSolved");
        if (key != null && key.transform.parent == transform) key.SetActive(false);
    }

    void Update()
    {
        if (revealed || key == null) return;          // key == null: already collected / used
        if (key.transform.parent != transform) { if (!key.activeSelf) key.SetActive(true); revealed = true; return; } // in the hand / dropped
        if (!Condition()) return;
        Reveal();
    }

    private bool Condition()
    {
        if (solvedProp != null && solvedPuzzle != null && solvedProp.GetValue(solvedPuzzle) is bool b && b) return true;
        if (openedDoor != null && openedDoor.IsOpen) return true;
        if (trackGone && goneObject == null) return true;    // destroyed (e.g. the angel after you touch her) - hidden at the start does not count
        if (!string.IsNullOrEmpty(objectiveId) && SaveManager.instance != null && SaveManager.instance.HasEvent(ObjectiveTracker.DonePrefix + objectiveId)) return true;
        return false;
    }

    private void Reveal()
    {
        revealed = true;
        Vector3 p = dropPoint != null ? dropPoint.position : transform.position;
        // drop onto the first solid surface below (floor, table, top of the safe...)
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 1.2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(key.transform)) continue;
            if (h.collider.GetComponentInParent<PlayerController>() != null || h.collider.GetComponentInParent<EnemyAI>() != null) continue;
            p = h.point + Vector3.up * 0.04f;
            break;
        }
        key.transform.position = p;
        key.SetActive(true);

        // faint gold glow so it can be found in the dark
        GameObject g = new GameObject("GateKeyGlow");
        g.transform.SetParent(key.transform, false);
        g.transform.localPosition = Vector3.up * 0.15f;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.3f); l.range = 1.4f; l.intensity = 1.6f; l.shadows = LightShadows.None;

        if (Time.timeSinceLevelLoad > 3f)      // not when it is simply restored after a death / load
            InteractionRaycast.Notify(FindAnyObjectByType<PlayerStats>(), "The MAIN GATE KEY appeared nearby!", 3f);
    }
}
