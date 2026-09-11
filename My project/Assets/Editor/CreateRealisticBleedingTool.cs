using UnityEngine;
using UnityEditor;

public class CreateRealisticBleedingTool : EditorWindow
{
    [MenuItem("Tools/Create Realistic Bleeding Effect")]
    public static void CreateBleeding()
    {
        // Create the main particle object
        GameObject bloodObj = new GameObject("Realistic_Bleeding_Effect");
        ParticleSystem ps = bloodObj.AddComponent<ParticleSystem>();

        // Renderer Settings (Material & Liquid Look)
        ParticleSystemRenderer renderer = bloodObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = new Color(0.5f, 0.0f, 0.0f, 1f); // Dark Blood Red
        
        // Secret for Liquid Look: Stretch the particles along their falling velocity!
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4f; // Lambay drops
        renderer.velocityScale = 0.2f;

        // Main Module
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0f, 0f, 1f), new Color(0.3f, 0f, 0f, 1f));
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f); // Small drops
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startLifetime = 3f; // Lives long enough to hit the floor
        main.gravityModifier = 1.5f; // Falls heavily like liquid
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Leaves trail when moved
        main.maxParticles = 1000;

        // Emission Module
        var emission = ps.emission;
        emission.rateOverTime = 80f; // Heavy bleeding

        // Shape Module
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f; // Very tight source (like from hands/eyes)

        // Collision Module (Splats on the floor!)
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = 0f; // Blood doesn't bounce
        collision.dampen = 1f; // Stops completely on the floor
        collision.lifetimeLoss = 0.5f; // Disappears a bit after hitting the floor

        // Size over Lifetime (Shrinks as it drips)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Position it nicely in front of the camera if possible
        if (SceneView.lastActiveSceneView != null)
        {
            bloodObj.transform.position = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * 2f;
        }

        // Select it
        Selection.activeGameObject = bloodObj;

        Debug.Log("Realistic Bleeding Effect created! Drag it to your Angel's hands.");
    }
}
