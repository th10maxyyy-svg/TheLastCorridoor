using UnityEngine;

/// <summary>
/// Simple two-bone IK for the player's right arm, used by short scenes (cutting a body open):
/// puts the TIP of the held item exactly on a target point, pointing in a target direction, by bending
/// the real arm (shoulder - elbow - hand). Runs after the animation and the first-person arm pose.
/// </summary>
[DefaultExecutionOrder(2000)]
public class ArmIK : MonoBehaviour
{
    public Transform upper, lower, hand;
    [Range(0f, 1f)] public float weight;
    public Vector3 tipTarget;                    // where the item's tip must be (world)
    public Vector3 tipDirection = Vector3.down;  // which way the item must point (world, handle -> tip)
    public Vector3 poleHint;                     // the elbow bends towards this point (world)
    public Vector3 tipInHand;                    // the item's tip in the hand bone's local space
    public Vector3 dirInHand = Vector3.forward;  // the item's direction in the hand bone's local space
    public float tremble = 0.0015f;              // a slightly unsteady hand

    public static ArmIK For(GameObject player, Transform handBone)
    {
        if (handBone == null || handBone.parent == null || handBone.parent.parent == null) return null;
        ArmIK ik = player.GetComponent<ArmIK>();
        if (ik == null) ik = player.AddComponent<ArmIK>();
        ik.hand = handBone; ik.lower = handBone.parent; ik.upper = handBone.parent.parent;
        return ik;
    }

    void LateUpdate()
    {
        if (weight <= 0.001f || upper == null || lower == null || hand == null) return;
        Quaternion u0 = upper.rotation, l0 = lower.rotation, h0 = hand.rotation;

        // the hand rotation that makes the item point the right way (smallest turn from the animated pose)
        Vector3 curDir = hand.TransformDirection(dirInHand);
        Quaternion handRot = Quaternion.FromToRotation(curDir, tipDirection.normalized) * hand.rotation;
        Vector3 shake = new Vector3(Mathf.PerlinNoise(Time.time * 7f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.time * 6f) - 0.5f, Mathf.PerlinNoise(Time.time * 5f, 1.9f) - 0.5f) * (tremble * 2f);
        Vector3 handPos = tipTarget + shake - handRot * Vector3.Scale(tipInHand, hand.lossyScale);

        Solve(upper, lower, hand, handPos, poleHint);
        hand.rotation = handRot;

        if (weight < 0.999f)
        {
            upper.rotation = Quaternion.Slerp(u0, upper.rotation, weight);
            lower.rotation = Quaternion.Slerp(l0, lower.rotation, weight);
            hand.rotation = Quaternion.Slerp(h0, hand.rotation, weight);
        }
    }

    /// <summary>Analytic two-bone IK (law of cosines), elbow bending towards the pole.</summary>
    private static void Solve(Transform A, Transform B, Transform C, Vector3 t, Vector3 pole)
    {
        Vector3 a = A.position, b = B.position, c = C.position;
        float lab = (b - a).magnitude, lcb = (c - b).magnitude;
        if (lab < 1e-4f || lcb < 1e-4f) return;
        float lat = Mathf.Clamp((t - a).magnitude, 0.01f, lab + lcb - 0.002f);

        float ac_ab_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1f, 1f));
        float ba_bc_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
        float ac_ab_1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float ba_bc_1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 axis0 = Vector3.Cross(c - a, b - a);                       // keep the arm's own bend plane
        if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(c - a, pole - a);   // arm dead straight: bend towards the pole
        if (axis0.sqrMagnitude < 1e-8f) return;
        axis0.Normalize();

        A.rotation = Quaternion.AngleAxis((ac_ab_1 - ac_ab_0) * Mathf.Rad2Deg, axis0) * A.rotation;
        B.rotation = Quaternion.AngleAxis((ba_bc_1 - ba_bc_0) * Mathf.Rad2Deg, axis0) * B.rotation;

        c = C.position;
        Vector3 axis1 = Vector3.Cross(c - a, t - a);
        if (axis1.sqrMagnitude > 1e-10f)
        {
            float ang = Vector3.Angle(c - a, t - a);
            A.rotation = Quaternion.AngleAxis(ang, axis1.normalized) * A.rotation;
        }
    }
}
