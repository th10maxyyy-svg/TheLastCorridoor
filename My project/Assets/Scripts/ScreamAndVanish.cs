using System.Collections;
using UnityEngine;

/// <summary>
/// The weeping statue (classroom). While it stands there it weeps - blood runs from both eyes down its face in two
/// glossy streaks, drips off its chin and pools on the floor.
/// Touch it: it SHRIEKS, the world goes black at once, the player is thrown back and the view shakes; when the
/// darkness lifts the statue is gone and the key lies on the floor. (Picking up the key puts the room back to normal,
/// as before - see SimpleClassroomIllusion.)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ScreamAndVanish : MonoBehaviour, IInteractable
{
    [Header("Sounds")]
    public AudioClip weepingSound; // Rone ki aawaz (Loop)
    public AudioClip screamSound;  // Chhune par cheekh ki aawaz

    [Header("Key")]
    public GameObject keyToReveal; // Zameen par mojood chabi

    [Header("Blood tears")]
    [Tooltip("Optional: a point between the eyes. If empty, the 'Realistic_Bleeding_Effect' child is used.")]
    public Transform eyes;
    public float eyeSpacing = 0.056f;
    public float tearLength = 0.17f;

    private bool interacted = false;
    private GameObject tearsRoot;

    void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (keyToReveal != null) keyToReveal.SetActive(false); // Shuru mein chabi chupa do

        // Jaise hi Angel show hogi, rona shuru kar degi!
        if (weepingSound != null)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = weepingSound;
            source.loop = true;
            source.spatialBlend = 1f; // 3D sound taake kareeb jane par awaz teez ho
            source.Play();
        }
        StartCoroutine(Weep());
    }

    // ------------------------------------------------------------------ blood from the eyes

    private IEnumerator Weep()
    {
        yield return null;
        Transform e = eyes != null ? eyes : transform.Find("Realistic_Bleeding_Effect");
        Vector3 center = e != null ? e.position : transform.position + Vector3.up * 0.7f;
        Camera cam = Camera.main;
        Vector3 toViewer = cam != null ? Vector3.ProjectOnPlane(cam.transform.position - center, Vector3.up) : transform.forward;
        if (toViewer.sqrMagnitude < 0.001f) toViewer = transform.forward;
        toViewer.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toViewer);

        tearsRoot = new GameObject("BloodTears");
        tearsRoot.transform.SetParent(transform, true);
        LineRenderer[] tears = new LineRenderer[2];
        Vector3[] start = { center - right * eyeSpacing * 0.5f, center + right * eyeSpacing * 0.5f };
        for (int i = 0; i < 2; i++)
        {
            tears[i] = BloodFX.Line(tearsRoot.transform, BloodFX.Wet, "Tear");
            tears[i].transform.rotation = Quaternion.LookRotation(-toViewer);   // the streak faces the viewer
            tears[i].numCapVertices = 4;
        }
        ParticleSystem drips = BloodFX.Drops(tearsRoot.transform, "TearDrips", 120);
        GameObject pool = null;
        float[] seed = { Random.value * 10f, Random.value * 10f };
        float grow = 0f;
        float nextDrip = 2.5f;

        while (!interacted)
        {
            grow = Mathf.Min(1f, grow + Time.deltaTime / 7f);
            for (int i = 0; i < 2; i++)
            {
                int n = 14;
                tears[i].positionCount = n;
                float len = tearLength * Mathf.SmoothStep(0f, 1f, grow) * (i == 0 ? 1f : 0.86f);
                for (int k = 0; k < n; k++)
                {
                    float f = k / (n - 1f);
                    float wig = Mathf.Sin(f * 9f + seed[i]) * 0.004f * f;
                    tears[i].SetPosition(k, start[i] + toViewer * (0.006f + f * 0.008f) + right * wig - Vector3.up * len * f);
                }
                // fuller under the eye, a fat drop gathering at the end
                tears[i].widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(0.15f, 0.55f), new Keyframe(0.8f, 0.45f), new Keyframe(1f, 0.85f));
                tears[i].widthMultiplier = 0.009f + 0.003f * Mathf.Sin(Time.time * 1.7f + i) * grow;
            }

            // once the tears reach the chin, drops fall and pool on the floor
            if (grow > 0.9f)
            {
                nextDrip -= Time.deltaTime;
                if (nextDrip <= 0f)
                {
                    nextDrip = Random.Range(0.6f, 1.6f);
                    int i = Random.Range(0, 2);
                    Vector3 end = tears[i].GetPosition(tears[i].positionCount - 1);
                    BloodFX.Emit(drips, end, new Vector3(0f, -0.15f, 0f) + toViewer * 0.05f, Random.Range(0.007f, 0.011f), 2.5f);
                    if (pool == null && Physics.Raycast(end + toViewer * 0.25f, Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        pool = BloodFX.Puddle(tearsRoot.transform, h.point + Vector3.up * 0.003f, right, BloodFX.Wet, name.GetHashCode());
                    }
                    if (pool != null)
                    {
                        float s = Mathf.Min(0.24f, pool.transform.localScale.x + 0.012f);
                        pool.transform.localScale = new Vector3(s, 1f, s * 0.85f);
                    }
                }
            }
            yield return null;
        }
    }

    // ------------------------------------------------------------------ touching it

    public string GetInteractPrompt()
    {
        return interacted ? "" : "Press E to Touch";
    }

    public void Interact(PlayerStats playerStats)
    {
        if (interacted) return;
        interacted = true;

        // the shriek, right in the player's ears
        AudioClip scream = screamSound != null ? screamSound : Resources.Load<AudioClip>("HorrorAudio/dragon-studio-female-zombie-screams-324744");
        if (scream != null)
        {
            GameObject tempAudioObj = new GameObject("TempScreamAudio");
            tempAudioObj.transform.position = transform.position;
            AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
            tempSource.clip = scream;
            tempSource.spatialBlend = 0f; // 2D (Har taraf barabar aawaz aaye)
            tempSource.volume = 1f;
            tempSource.Play();
            Destroy(tempAudioObj, 5f);   // Theek 5 seconds baad cheekh ki aawaz cut!
        }

        // instant darkness, the player is thrown back, the world shakes
        ScreenFlash.Blackout(1.5f, 1.3f);
        FxRunner.Run(Shock(playerStats, transform.position));

        if (keyToReveal != null) keyToReveal.SetActive(true); // Farsh wali chabi show kar do (it is there when the dark lifts)

        // Angel, khoon, aur rone ki aawaz sab ek dum Delete! (hidden by the blackout)
        Destroy(gameObject);
    }

    private static IEnumerator Shock(PlayerStats player, Vector3 statuePos)
    {
        if (player == null) yield break;
        Camera cam = player.GetComponentInChildren<Camera>();
        CharacterController cc = player.GetComponent<CharacterController>();
        player.TriggerHeavyBreathing(6f);
        Vector3 back = Vector3.ProjectOnPlane(player.transform.position - statuePos, Vector3.up);
        back = back.sqrMagnitude > 0.001f ? back.normalized : -player.transform.forward;
        float t = 0f, dur = 1.6f;
        while (t < dur)
        {
            yield return null;   // after PlayerController has set the look for this frame
            t += Time.deltaTime;
            float k = 1f - t / dur;
            if (cam != null)
                cam.transform.localRotation *= Quaternion.Euler(Random.Range(-7f, 7f) * k - 6f * k * k, Random.Range(-7f, 7f) * k, Random.Range(-5f, 5f) * k);
            if (cc != null && cc.enabled && t < 0.35f) cc.Move(back * (2.2f * Time.deltaTime));   // stumbles back
        }
    }
}
