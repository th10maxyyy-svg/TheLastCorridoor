using UnityEngine;
using UnityEditor;

public class CreateAcidBathtubTool
{
    [MenuItem("Tools/The Last Corridor/Create Acid Liquid Plane")]
    public static void CreateAcid()
    {
        // 1. Create the Plane Mesh
        GameObject acidPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        acidPlane.name = "Acid Liquid Surface";
        
        // Remove Collider so player doesn't collide with the liquid surface
        Collider col = acidPlane.GetComponent<Collider>();
        if (col != null)
        {
            Object.DestroyImmediate(col);
        }

        // 2. Create or Find the Acid Material
        string matPath = "Assets/Materials/AcidLiquidMat.mat";
        Material acidMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        
        if (acidMat == null)
        {
            // Create a new URP Lit material
            acidMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            acidMat.SetFloat("_Surface", 1); // 1 = Transparent
            acidMat.SetColor("_BaseColor", new Color(0.2f, 0.8f, 0.1f, 0.8f)); // Greenish and slightly transparent
            acidMat.SetFloat("_Smoothness", 0.8f); // Make it shiny like liquid
            
            // Setup blending for URP transparency
            acidMat.SetFloat("_Blend", 0); // Standard Alpha
            acidMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            acidMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            acidMat.SetFloat("_ZWrite", 0);
            acidMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            acidMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Save the material
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            AssetDatabase.CreateAsset(acidMat, matPath);
            AssetDatabase.SaveAssets();
        }

        // Apply Material
        Renderer rend = acidPlane.GetComponent<Renderer>();
        rend.sharedMaterial = acidMat;

        // 3. Attach the Scrolling Script
        acidPlane.AddComponent<ScrollingLiquid>();

        // 4. Create the Bubbles Particle System
        GameObject bubbles = new GameObject("Acid Bubbles");
        bubbles.transform.SetParent(acidPlane.transform);
        bubbles.transform.localPosition = Vector3.zero;
        bubbles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Point upwards

        ParticleSystem ps = bubbles.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.5f;
        main.startSpeed = 1f; // Slow floating bubbles
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f); // Small bubbles
        main.startColor = new Color(0.5f, 1f, 0.5f, 0.6f); // Light green
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var emission = ps.emission;
        emission.rateOverTime = 20;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8f, 8f, 0.1f); // Cover the plane area

        var colOverLife = ps.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLife.color = grad;

        // 5. Select the object in the editor
        Selection.activeGameObject = acidPlane;
        
        Debug.Log("Acid Liquid Surface created! Place it in your bathtub, scale it to fit, and assign it to the AcidDrainPuzzle script.");
    }
}
