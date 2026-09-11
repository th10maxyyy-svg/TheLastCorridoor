using UnityEngine;

public class ParanormalFloat : MonoBehaviour
{
    [Header("Float Settings")]
    public float floatSpeed = 1f; // Kitni tezi se oopar neechay ho
    public float floatAmount = 0.3f; // Kitna oopar neechay ho (distance)
    
    [Header("Rotation Settings")]
    public float rotateSpeed = 15f; // Ghoomne ki speed
    
    private Vector3 startPos;
    private Vector3 randomRotationAxis;
    private float randomTimeOffset;

    void Start()
    {
        startPos = transform.localPosition;
        
        // Har object alag alag disha mein ghoome
        randomRotationAxis = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        
        // Har object ki speed thori c mukhtalif (different) ho taake sab ek sath na hilein
        floatSpeed += Random.Range(-0.3f, 0.3f);
        rotateSpeed += Random.Range(-5f, 5f);
        randomTimeOffset = Random.Range(0f, 10f); // Wave ko random karne ke liye
    }

    void Update()
    {
        // Oopar neechay terna (Bobbing up and down)
        float newY = startPos.y + Mathf.Sin((Time.time + randomTimeOffset) * floatSpeed) * floatAmount;
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
        
        // Hawa mein ahesta ahesta ghoomna (Slow random rotation)
        transform.Rotate(randomRotationAxis * rotateSpeed * Time.deltaTime, Space.World);
    }
}
