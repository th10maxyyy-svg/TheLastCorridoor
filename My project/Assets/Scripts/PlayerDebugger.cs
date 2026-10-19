using UnityEngine;

public class PlayerDebugger : MonoBehaviour
{
    private CharacterController cc;
    private PlayerController pc;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        pc = GetComponent<PlayerController>();
    }

    void Update()
    {
        // Sirf Editor / Development build mein - release game mein P dabane se kuch na ho
        if (!Debug.isDebugBuild || cc == null || pc == null) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("--- PLAYER DEBUG INFO ---");
            Debug.Log($"Position: {transform.position}");
            Debug.Log($"Is Grounded: {cc.isGrounded}");
            Debug.Log($"Walk Speed in script: " + pc.GetType().GetField("walkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(pc));
            
            Vector2 input = new Vector2(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"));
            Debug.Log($"Input (W/S=X, A/D=Y): {input}");

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, cc.radius);
            foreach (var hit in hitColliders)
            {
                if (hit.gameObject != this.gameObject && !hit.isTrigger)
                {
                    Debug.Log($"OVERLAPPING COLLIDER: {hit.gameObject.name} (Tag: {hit.tag}, Layer: {LayerMask.LayerToName(hit.gameObject.layer)})");
                }
            }
            Debug.Log("-------------------------");
        }
    }
}
