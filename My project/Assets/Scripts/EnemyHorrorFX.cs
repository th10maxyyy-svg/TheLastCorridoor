using UnityEngine;

/// <summary>
/// Procedural "wrongness" layered ON TOP of the enemy's animations (it only has Idle / Walk / Run / Attack / Yell clips):
///  - head slowly tilts sideways like a broken doll, with sudden violent twitches
///  - while hunting / teasing he turns his head to stare straight at the player
///  - unnatural, uneven walking rhythm and a faster, frantic run
/// Added automatically by EnemyAI - no setup needed.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHorrorFX : MonoBehaviour
{
    [Header("Head")]
    [Tooltip("Kitni degree sar terha (sideways) jhuka rahe")]
    public float headTilt = 24f;
    [Tooltip("Achanak jhatke (twitch) ki taqat")]
    public float twitchStrength = 28f;
    [Tooltip("Player ki taraf sar ghuma kar ghoorne ki zyada se zyada degree")]
    public float maxStareAngle = 70f;

    [Header("Movement")]
    [Tooltip("Chalne ki raftaar mein be-tarteebi (0 = bilkul normal)")]
    public float gaitIrregularity = 0.25f;

    private EnemyAI ai;
    private UnityEngine.AI.NavMeshAgent agent;
    private Animator anim;
    private Transform head;
    private Transform neck;
    private Transform player;

    private float tilt;
    private float tiltTarget;
    private float nextTiltChange;
    private float twitchUntil;
    private float nextTwitch;
    private Quaternion twitchRot = Quaternion.identity;
    private float stareWeight;
    private float seed;

    void Start()
    {
        ai = GetComponent<EnemyAI>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        anim = ai != null ? ai.enemyAnimator : GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        seed = Random.Range(0f, 100f);
        tiltTarget = Random.Range(0.6f, 1f) * headTilt * (Random.value < 0.5f ? -1f : 1f);
        nextTwitch = Time.time + Random.Range(2f, 5f);
    }

    void Update()
    {
        if (ai == null || anim == null) return;

        // Uneven, "wrong" gait. Never touch the animator while stunned (it is frozen on purpose) or in cinematics.
        EnemyAI.AIState s = ai.State;
        if (s == EnemyAI.AIState.Stunned) return;
        if (s == EnemyAI.AIState.CinematicEscape || s == EnemyAI.AIState.Dormant) { anim.speed = 1f; return; }

        // The walk and run loops follow the real movement speed (no more skating), with a little unevenness on top.
        // The attack clip plays faster (a quick lunge); everything else (yell...) keeps its normal speed.
        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        if ((info.IsName("Hell Slammer A") || info.IsName("Slam")) && s == EnemyAI.AIState.Chase) { anim.speed = ai.AttackAnimSpeed; return; }

        bool locomotion = (info.IsName("Walking") || info.IsName("Run")) && !anim.IsInTransition(0);
        if (!locomotion) { anim.speed = 1f; return; }

        // FEET MATCH THE GROUND: the clip plays exactly as fast as he really moves, using the clip's own stride speed
        // (measured from the animation itself), so he never slides or "moonwalks". A tiny unevenness keeps it creepy.
        float vel = agent != null ? agent.velocity.magnitude : 0f;
        bool run = info.IsName("Run");
        float natural = ClipSpeed(run);
        float match = Mathf.Clamp(vel / natural, run ? 0.75f : 0.6f, run ? 1.9f : 2.2f);
        float n = Mathf.PerlinNoise(Time.time * 0.6f, seed) - 0.5f;
        anim.speed = match * (1f + n * gaitIrregularity * 0.3f);
    }

    private float walkClipSpeed = -1f, runClipSpeed = -1f;

    /// <summary>Metres per second the Walking / Run clip covers at playback speed 1.</summary>
    private float ClipSpeed(bool run)
    {
        float cached = run ? runClipSpeed : walkClipSpeed;
        if (cached > 0f) return cached;

        float measured = 0f;
        AnimatorClipInfo[] clips = anim.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0 && clips[0].clip != null)
        {
            measured = clips[0].clip.averageSpeed.magnitude;
            if (anim.isHuman) measured *= anim.humanScale;
        }
        // "in place" clips have no stride speed: use the values tuned for the clown's own Walking / Run clips
        float result = measured > 0.3f ? measured : (run ? 4.25f : 1.07f);
        if (run) runClipSpeed = result; else walkClipSpeed = result;
        return result;
    }

    void LateUpdate()
    {
        if (ai == null || head == null) return;
        if (!ai.isActiveAndEnabled) return;

        EnemyAI.AIState s = ai.State;
        if (s == EnemyAI.AIState.Stunned || s == EnemyAI.AIState.Dormant) return;

        // Don't fight the attack animation
        if (anim != null && (anim.GetCurrentAnimatorStateInfo(0).IsName("Hell Slammer A") || anim.GetCurrentAnimatorStateInfo(0).IsName("Slam"))) return;

        Vector3 faceDir = transform.forward;

        // 1. Stare: turn the head towards the player while hunting / teasing / in the ending
        bool wantsToStare = player != null &&
            (s == EnemyAI.AIState.Chase || s == EnemyAI.AIState.Tease || s == EnemyAI.AIState.Search ||
             s == EnemyAI.AIState.CinematicEscape || s == EnemyAI.AIState.InvestigateSound);
        stareWeight = Mathf.MoveTowards(stareWeight, wantsToStare ? 1f : 0f, Time.deltaTime * 2f);

        Quaternion stareRot = Quaternion.identity;
        if (stareWeight > 0.001f && player != null)
        {
            Vector3 toPlayer = (player.position + Vector3.up * 0.6f) - head.position;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Vector3 limited = Vector3.RotateTowards(faceDir, toPlayer.normalized, maxStareAngle * Mathf.Deg2Rad, 0f);
                stareRot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(faceDir, limited), stareWeight);
                faceDir = limited;
            }
        }

        // 2. Broken-doll tilt that slowly drifts from one side to the other
        if (Time.time >= nextTiltChange)
        {
            nextTiltChange = Time.time + Random.Range(3f, 7f);
            tiltTarget = Random.Range(0.4f, 1f) * headTilt * (Random.value < 0.5f ? -1f : 1f);
        }
        float tiltSpeed = s == EnemyAI.AIState.Chase ? 60f : 18f;
        tilt = Mathf.MoveTowards(tilt, tiltTarget, tiltSpeed * Time.deltaTime);
        Quaternion tiltRot = Quaternion.AngleAxis(tilt, faceDir);

        // 3. Sudden violent twitches
        if (Time.time >= nextTwitch)
        {
            bool hunting = s == EnemyAI.AIState.Chase || s == EnemyAI.AIState.Tease;
            nextTwitch = Time.time + (hunting ? Random.Range(0.8f, 2f) : Random.Range(2.5f, 6f));
            twitchUntil = Time.time + Random.Range(0.06f, 0.14f);
            twitchRot = Quaternion.Euler(Random.Range(-1f, 1f) * twitchStrength,
                                         Random.Range(-1f, 1f) * twitchStrength,
                                         Random.Range(-1f, 1f) * twitchStrength);
        }
        Quaternion twitch = Time.time < twitchUntil ? twitchRot : Quaternion.identity;

        // Spread the stare over neck + head so it doesn't snap at one joint
        if (neck != null)
        {
            neck.rotation = Quaternion.Slerp(Quaternion.identity, stareRot, 0.4f) * neck.rotation;
            head.rotation = twitch * tiltRot * Quaternion.Slerp(Quaternion.identity, stareRot, 0.6f) * head.rotation;
        }
        else
        {
            head.rotation = twitch * tiltRot * stareRot * head.rotation;
        }
    }
}
