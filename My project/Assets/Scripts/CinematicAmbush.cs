using UnityEngine;
using UnityEngine.AI;

public class CinematicAmbush : MonoBehaviour
{
    [Header("Enemy Setup")]
    [Tooltip("The Enemy that will ambush the player")]
    public EnemyAI enemy;
    [Tooltip("Where the enemy should spawn when the ambush starts")]
    public Transform ambushSpawnPoint;

    [Header("Tease / Jumpscare Settings")]
    [Tooltip("The scary sound to play right when he spawns!")]
    public AudioClip ambushScream;
    [Tooltip("If true, plays a Jumpscare animation (Tease) instead of chasing.")]
    public bool useTeaseInsteadOfChase = true;
    [Tooltip("How long the enemy yells/teases before walking away")]
    public float teaseDuration = 3f;
    [Tooltip("Optional: Where the enemy walks away to after the jumpscare")]
    public Transform teaseLeaveTarget;

    [Header("Drag to Basement (Story Transition)")]
    [Tooltip("Agar true hai, to enemy player ko maar kar game over nahi karega, balke basement mein phenk dega!")]
    public bool isBasementTransition = false;
    [Tooltip("Wo khali jagah (Empty GameObject) jahan player basement mein giray ga")]
    public Transform basementSpawnPoint;

    private AudioSource audioSource;
    private bool hasAmbushed = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    [Header("Trigger Settings")]
    [Tooltip("Agar true hai, to player iske qareeb jayega to ye trigger ho jayega (like hallway jumpscare). Agar false hai to sirf Door khulne par chalega!")]
    public bool triggerOnWalk = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnWalk && other.CompareTag("Player"))
        {
            TriggerAmbush();
        }
    }

    // Call this from DoorController's "OnLockedInteract" Event or just walk into the Trigger!
    public void TriggerAmbush(bool forceChase = false)
    {
        Debug.Log("TriggerAmbush called on GameObject: " + gameObject.name + " | Force Chase: " + forceChase);
        
        if (hasAmbushed && !forceChase) 
        {
            Debug.LogWarning("Ambush SKIPPED on " + gameObject.name + " because it was already triggered earlier!");
            return; // Only ambush once, unless forced!
        }
        hasAmbushed = true;

        // Play the sudden scream!
        if (ambushScream != null)
        {
            audioSource.PlayOneShot(ambushScream);
        }

        if (enemy != null && ambushSpawnPoint != null)
        {
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            
            // Fix: NavMeshAgent ko pehle band kar ke position set karni parti hai, warna Unity usay disable kar deta hai aur wo putla ban jata hai!
            if (agent != null) agent.enabled = false;
            
            enemy.transform.position = ambushSpawnPoint.position;
            enemy.gameObject.SetActive(true);
            
            if (agent != null) 
            {
                agent.enabled = true;
                agent.Warp(ambushSpawnPoint.position);
            }

            // Tell the enemy to Jumpscare or Chase!
            Transform playerTransform = null;
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();

            if (stats != null) 
            {
                playerTransform = stats.transform;
            }

            // Tareeqa 2: Name se dhoondo (Hierarchy mein jo naam hai)
            if (playerTransform == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p != null) playerTransform = p.transform;
            }

            // Tareeqa 3: Tag se dhoondo
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            if (playerTransform != null)
            {
                // NAYA: Story Transition Check!
                if (isBasementTransition && basementSpawnPoint != null)
                {
                    // WORLD STATE: Mark Nightmare as active so the enemy stays awake while player is in basement!
                    ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                    if (pm != null) pm.StartNightmarePhase();

                    // Jumpscare karega, uske baad player behosh hoga!
                    StartCoroutine(ExecuteBasementTransition(playerTransform));
                }
                else if (useTeaseInsteadOfChase && !forceChase)
                {
                    enemy.StartTeasing(playerTransform, teaseDuration, teaseLeaveTarget);
                    // WORLD STATE: Mark Nightmare as active even if teasing, so it doesn't go back to sleep!
                    ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                    if (pm != null) pm.StartNightmarePhase();
                }
                else
                {
                    enemy.ForceAwaken(playerTransform.position);
                    
                    // WORLD STATE: Mark Nightmare as active so it survives death!
                    ProgressionManager pm = FindAnyObjectByType<ProgressionManager>();
                    if (pm != null) pm.StartNightmarePhase();
                }
            }
            else
            {
                Debug.LogWarning("Ambush script couldn't find the Player by Script, Name, OR Tag!");
            }
            
            Debug.Log("Cinematic Ambush Started!");
        }
        else
        {
            Debug.LogWarning("Ambush script is missing the Enemy or Spawn Point reference!");
        }
    }

    private System.Collections.IEnumerator ExecuteBasementTransition(Transform playerTransform)
    {
        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null) pc.isCinematicPlaying = true; // Paralyze player entirely!

        // 0. PSYCHO JOKER EFFECT: Teleport enemy directly into the player's face!
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();

        // Player ke bilkul aagay (0.45 units away)
        Vector3 psychoPosition = playerTransform.position + playerTransform.forward * 0.45f;
        psychoPosition.y = playerTransform.position.y; // Keep it on the ground
        
        if (agent != null)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(psychoPosition, out hit, 1.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                agent.Warp(psychoPosition); 
            }
        }
        else
        {
            enemy.transform.position = psychoPosition;
        }
        
        // Force enemy to stare directly into the player's soul
        Vector3 lookDir = (playerTransform.position - enemy.transform.position).normalized;
        lookDir.y = 0; // Don't tilt up/down
        if (lookDir != Vector3.zero)
        {
            enemy.transform.rotation = Quaternion.LookRotation(lookDir);
        }

        // 1. Force the Jumpscare! Enemy chillayega player ke samne
        enemy.StartTeasing(playerTransform, teaseDuration, null);

        // 2. Camera zoom directly into the enemy's face! (Only see the face)
        Camera cam = playerTransform.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            float t = 0;
            float zoomTime = 0.25f; // Super fast zoom (less glitchy!)
            float startFOV = cam.fieldOfView;
            
            Vector3 originalLocalPos = cam.transform.localPosition;
            Quaternion originalLocalRot = cam.transform.localRotation;
            
            Vector3 originalCamWorldPos = cam.transform.position;
            Quaternion originalCamWorldRot = cam.transform.rotation;
            
            // Find head position
            Vector3 targetHeadPos = enemy.transform.position + Vector3.up * 1.6f;

            while (t < teaseDuration)
            {
                t += Time.deltaTime;
                
                if (enemy.enemyAnimator != null)
                {
                    Transform headBone = enemy.enemyAnimator.GetBoneTransform(HumanBodyBones.Head);
                    if (headBone != null) targetHeadPos = headBone.position;
                }

                // CAMERA ANGLE FIX: Low-angle psycho shot
                Vector3 faceFront = targetHeadPos + enemy.transform.forward * 0.4f - Vector3.up * 0.25f;
                
                // Target Rotation
                Quaternion targetRot = Quaternion.LookRotation(targetHeadPos - faceFront);

                // Calculate exact percentage of the zoom (0 to 1)
                float movePercent = Mathf.Clamp01(t / zoomTime);
                float curve = Mathf.SmoothStep(0f, 1f, movePercent);

                // Use the curve to Lerp perfectly without trailing/glitchy lag!
                cam.transform.position = Vector3.Lerp(originalCamWorldPos, faceFront, curve);
                cam.transform.rotation = Quaternion.Slerp(originalCamWorldRot, targetRot, curve);
                cam.fieldOfView = Mathf.Lerp(startFOV, 35f, curve);
                
                // IMPORTANT: Wait for End Of Frame so the Animator finishes moving the head BEFORE we place the camera.
                // This removes ALL jitter!
                yield return new WaitForEndOfFrame();
            }
            
            // Restore camera before fainting
            cam.fieldOfView = startFOV;
            cam.transform.localPosition = originalLocalPos;
            cam.transform.localRotation = originalLocalRot;
        }
        else
        {
            yield return new WaitForSeconds(teaseDuration);
        }

        if (pc != null) pc.isCinematicPlaying = false; // Restore normal control logic


        // 3. Player faints and is dragged to basement!
        if (pc != null)
        {
            pc.TriggerDragToBasementCinematic(basementSpawnPoint, enemy.gameObject);
        }
    }
}
