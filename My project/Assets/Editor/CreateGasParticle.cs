using UnityEngine;
using UnityEditor;

public class CreateGasParticle : EditorWindow
{
    [MenuItem("Tools/Create Green Gas Particle")]
    public static void CreateGas()
    {
        // Create the GameObject
        GameObject gasObj = new GameObject("Green_Toxic_Gas");
        
        // Add Particle System
        ParticleSystem ps = gasObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        
        // Main Settings
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.startColor = new Color(0.2f, 0.8f, 0.2f, 0.3f); // Translucent Green
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        // Emission
        var emission = ps.emission;
        emission.rateOverTime = 30f;

        // Shape
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(5f, 1f, 5f); // Spread it over a wide area

        // Color over Lifetime (Fade in and fade out)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.1f, 0.6f, 0.1f), 0.0f), new GradientColorKey(new Color(0.3f, 0.9f, 0.3f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        col.color = grad;

        // Size over Lifetime (Grow as it rises)
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f);
        curve.AddKey(1f, 2f);
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Rotation over Lifetime
        var rotOL = ps.rotationOverLifetime;
        rotOL.enabled = true;
        rotOL.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

        // Renderer
        ParticleSystemRenderer renderer = gasObj.GetComponent<ParticleSystemRenderer>();
        // Assign default particle material so it looks soft
        Material defaultMat = new Material(Shader.Find("Particles/Standard Unlit"));
        defaultMat.SetFloat("_Mode", 2); // Fade mode
        defaultMat.SetColor("_Color", new Color(1, 1, 1, 0.5f));
        renderer.material = defaultMat;
        renderer.sortMode = ParticleSystemSortMode.Distance;

        // Auto-select in editor
        Selection.activeGameObject = gasObj;
        
        Debug.Log("Green Toxic Gas Particle System created successfully!");
    }
}
