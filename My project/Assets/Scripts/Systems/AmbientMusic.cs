using UnityEngine;

/// <summary>
/// BACKGROUND MUSIC for the game level:
///   - a low, creepy drone everywhere, a little different on every floor (the basement is deeper and louder)
///   - while the clown is chasing, the drone drops and heavy percussion comes in; it fades out again after the chase
///   - silent during cinematics, paused with the game
/// Volume follows the Master Volume setting (AudioListener). Created automatically in the game level by GameUI.
/// </summary>
public class AmbientMusic : MonoBehaviour
{
    private AudioSource drone, chase;
    private PlayerController player;
    private EnemyAI enemy;
    private float nextLook, chaseHold;

    void Start()
    {
        drone = MakeSource(Resources.Load<AudioClip>("HorrorAudio/TLC_Ambience_Drone"));
        chase = MakeSource(Resources.Load<AudioClip>("HorrorAudio/TLC_Chase_Percussion"));
        if (drone != null) drone.Play();
        if (chase != null) chase.Play();
    }

    private AudioSource MakeSource(AudioClip clip)
    {
        if (clip == null) return null;
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.volume = 0f; s.spatialBlend = 0f; s.playOnAwake = false; s.priority = 40;
        return s;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + 1f;
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (enemy == null) enemy = FindAnyObjectByType<EnemyAI>();
        }

        bool cinematic = player == null || player.isCinematicPlaying || player.isDead || ConsumedEndingCinematic.IsPlaying;
        bool chasing = enemy != null && enemy.isActiveAndEnabled && enemy.State == EnemyAI.AIState.Chase;
        chaseHold = chasing ? 4f : Mathf.Max(0f, chaseHold - Time.unscaledDeltaTime);

        float y = player != null ? player.transform.position.y : 0f;
        float floorVol = y < -6f ? 0.34f : y > 2.5f ? 0.26f : 0.22f;
        float floorPitch = y < -6f ? 0.86f : y > 2.5f ? 0.95f : 1f;

        float droneT = cinematic ? 0f : chaseHold > 0f ? 0.1f : floorVol;
        float chaseT = cinematic ? 0f : chaseHold > 0f ? 0.42f : 0f;
        float dt = Time.unscaledDeltaTime;
        if (drone != null)
        {
            drone.volume = Mathf.MoveTowards(drone.volume, droneT, dt * 0.15f);
            drone.pitch = Mathf.MoveTowards(drone.pitch, floorPitch, dt * 0.05f);
        }
        if (chase != null) chase.volume = Mathf.MoveTowards(chase.volume, chaseT, dt * (chaseT > chase.volume ? 0.6f : 0.12f));
    }
}
