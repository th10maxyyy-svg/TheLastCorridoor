using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyFootIK : MonoBehaviour
{
    private Animator animator;

    [Header("IK Settings (Paon zameen pe set karne ke liye)")]
    public bool enableIK = true;
    [Tooltip("Stairs aur Zameen ka layer select karein")]
    public LayerMask groundLayer = ~0; // By default everything
    [Tooltip("Paon zameen se kitne oopar hone chahiye (agar paon zameen mein dhans rahe hon to isay barhayen)")]
    [Range(0, 0.5f)] public float footOffset = 0.1f; 

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !enableIK) return;

        // Set weights (1 means fully controlled by script)
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 1f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 1f);

        // Dono paon (feet) par physics lagayen
        AdjustFootIK(AvatarIKGoal.LeftFoot);
        AdjustFootIK(AvatarIKGoal.RightFoot);
    }

    private void AdjustFootIK(AvatarIKGoal foot)
    {
        // Animation ke hisab se paon kahan hai?
        Vector3 footPosition = animator.GetIKPosition(foot);
        
        // Paon se thora oopar se neechay ki taraf Ray (laser) phainkain
        RaycastHit hit;
        if (Physics.Raycast(footPosition + Vector3.up * 1.5f, Vector3.down, out hit, 2.5f, groundLayer))
        {
            // Zameen jahan mili, paon wahan rakh do
            Vector3 newPosition = footPosition;
            newPosition.y = hit.point.y + footOffset;
            animator.SetIKPosition(foot, newPosition);

            // Paon ka angle zameen (stairs/slope) ke hisab se terha kar do
            Quaternion footRotation = animator.GetIKRotation(foot);
            Quaternion groundSlopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            animator.SetIKRotation(foot, groundSlopeRotation * footRotation);
        }
    }
}
