using UnityEngine;

public class FirstPersonBody : MonoBehaviour
{
    [Header("Visual Fixes")]
    public Transform headBone;
    public Transform neckBone;

    // FPP Arms Bones
    private Transform leftArm, leftForeArm, leftHand;
    private Transform rightArm, rightForeArm, rightHand;
    private Transform spine2;

    private Transform playerCamera;

    void Start()
    {
        if (Camera.main != null) playerCamera = Camera.main.transform;

        // Automatically find all required bones
        Transform[] allBones = GetComponentsInChildren<Transform>();
        foreach (Transform bone in allBones)
        {
            if (bone.name.Contains("LeftArm")) leftArm = bone;
            if (bone.name.Contains("LeftForeArm")) leftForeArm = bone;
            if (bone.name.Contains("LeftHand")) leftHand = bone;
            
            if (bone.name.Contains("RightArm")) rightArm = bone;
            if (bone.name.Contains("RightForeArm")) rightForeArm = bone;
            if (bone.name.Contains("RightHand")) rightHand = bone;

            if (bone.name.Contains("Spine2")) spine2 = bone;
        }
    }

    void LateUpdate()
    {
        // 1. Hide the head and neck to prevent camera clipping
        if (headBone != null) headBone.localScale = Vector3.zero;
        if (neckBone != null) neckBone.localScale = Vector3.zero;

        // 2. PUBG FPP VIEW - Force arms up into view (stable, no camera pitch)
        if (leftArm != null && leftForeArm != null)
        {
            // Arms ko mazeed oopar aur aagay uthaya hai taake har waqt clear nazar aayein
            leftArm.Rotate(transform.right, -35f, Space.World);
            leftForeArm.Rotate(transform.right, -60f, Space.World); 
        }

        if (rightArm != null && rightForeArm != null)
        {
            rightArm.Rotate(transform.right, -35f, Space.World);
            rightForeArm.Rotate(transform.right, -60f, Space.World); 
        }
    }
}
