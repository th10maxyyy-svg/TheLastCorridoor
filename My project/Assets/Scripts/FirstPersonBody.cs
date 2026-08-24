using UnityEngine;

public class FirstPersonBody : MonoBehaviour
{
    [Header("Visual Fixes")]
    [Tooltip("Apne character ka 'Head' ya 'Neck' bone yahan drag karein. Ye script usay gayab kar degi taake camera ke samnay daant ya aankhein na ayen!")]
    public Transform headBone;

    void LateUpdate()
    {
        if (headBone != null)
        {
            // Sir ko bilkul chota (gayab) kar do taake camera ke aagay na aye, lekin baqi jism chalta rahay!
            headBone.localScale = Vector3.zero;
        }
    }
}
