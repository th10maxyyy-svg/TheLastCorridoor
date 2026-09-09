using UnityEngine;
using UnityEditor;

public class UpgradeGasParticle : EditorWindow
{
    [MenuItem("Tools/Upgrade Gas to Realistic Smoke")]
    public static void UpgradeGas()
    {
        GameObject gasObj = GameObject.Find("Green_Toxic_Gas");
        if (gasObj == null)
        {
            Debug.LogError("Green_Toxic_Gas nahi mila! Pehle gas create karein.");
            return;
        }

        ParticleSystem ps = gasObj.GetComponent<ParticleSystem>();
        var main = ps.main;
        
        // Make particles live longer and grow larger to fill the room
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f); // Baday badal (large clouds)
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f); // Halka halka urray (slow movement)
        
        // Color adjustment (Make it very soft translucent green)
        main.startColor = new Color(0.1f, 0.6f, 0.1f, 0.15f); // Boht halka color taake realistic lagay

        // Emission: zyada particles niklein taake room bhar jaye
        var emission = ps.emission;
        emission.rateOverTime = 80f;

        // Shape: Poore room mein phelane ke liye scale bara karein
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8f, 1f, 8f); // Bara area

        // Soft Material apply karein taake dabbe (squares) na lagain
        ParticleSystemRenderer renderer = gasObj.GetComponent<ParticleSystemRenderer>();
        
        // Find Unity's built-in soft particle material
        Material softMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        if (softMat != null)
        {
            renderer.material = softMat;
        }
        else
        {
            // Fallback: Default-Particle
            Material fallbackMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (fallbackMat != null) renderer.material = fallbackMat;
        }

        Debug.Log("Gas has been upgraded to Realistic Soft Smoke and spread across the room!");
    }
}
