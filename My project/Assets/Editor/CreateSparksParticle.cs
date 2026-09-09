using UnityEngine;
using UnityEditor;

public class CreateSparksParticle : EditorWindow
{
    [MenuItem("Tools/Create Exploding Sparks")]
    public static void CreateSparks()
    {
        // Create the GameObject
        GameObject sparksObj = new GameObject("Electric_Sparks");
        
        // Add Particle System
        ParticleSystem ps = sparksObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        
        // Main Settings
        main.duration = 1f;
        main.loop = false; // Sirf ek bar phatna hai
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(1f, 0.9f, 0.2f, 1f); // Bright Yellow/Orange
        main.gravityModifier = 2f; // Taake sparks zameen par giren
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false; // Script khud play karegi

        // Emission (Ek dam se burst)
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50, 100) });

        // Shape (Choti si jagah se niklenge)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Collision (Zameen se takrayen)
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0.5f;
        collision.dampen = 0.2f;

        // Trails (Choti si line bane gi aag ki tarah)
        var trails = ps.trails;
        trails.enabled = true;
        trails.ratio = 0.5f;
        trails.lifetimeMultiplier = 0.05f;
        trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0f, 1f));

        // Renderer
        ParticleSystemRenderer renderer = sparksObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2f;
        
        // Use default particle material
        Material defaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (defaultMat != null)
        {
            renderer.material = defaultMat;
            renderer.trailMaterial = defaultMat;
        }

        // Auto-select in editor
        Selection.activeGameObject = sparksObj;
        
        Debug.Log("Exploding Sparks created successfully!");
    }
}
