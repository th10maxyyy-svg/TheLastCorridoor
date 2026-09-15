using UnityEngine;

public class ScrollingLiquid : MonoBehaviour
{
    [Header("Scroll Settings")]
    public float scrollSpeedX = 0.05f;
    public float scrollSpeedY = 0.05f;

    private Renderer rend;
    private Material mat;
    private Vector2 currentOffset;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // Material ki copy bana lete hain
            mat = rend.material; 
            if (mat.HasProperty("_BaseMap"))
            {
                currentOffset = mat.GetTextureOffset("_BaseMap");
            }
        }
    }

    void Update()
    {
        if (mat != null)
        {
            // Time.time ki jagah deltaTime use karte hain taake perfectly smooth chalay
            currentOffset.x += scrollSpeedX * Time.deltaTime;
            currentOffset.y += scrollSpeedY * Time.deltaTime;
            
            // 1. Standard Unity method
            mat.mainTextureOffset = currentOffset;
            
            // 2. URP method
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureOffset("_BaseMap", currentOffset);
            }
            
            // 3. Normal Map
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTextureOffset("_BumpMap", currentOffset);
            }
        }
    }
}
