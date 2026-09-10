using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class IllusionEnterTrigger : MonoBehaviour
{
    public UnityEvent onEnter = new UnityEvent();
    private bool triggered = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggered && other.CompareTag("Player"))
        {
            triggered = true;
            if (onEnter != null) onEnter.Invoke();
        }
    }
}
