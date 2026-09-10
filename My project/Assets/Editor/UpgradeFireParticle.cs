using UnityEngine;
using UnityEditor;

public class UpgradeFireParticle : EditorWindow
{
    [MenuItem("Tools/Upgrade To Realistic Fire")]
    public static void UpgradeFire()
    {
        GameObject fireObj = GameObject.Find("Oven_Fire_Burst");
        if (fireObj == null)
        {
            Debug.LogError("Pehle 'Tools -> Create Fire Burst Particle' chalayen.");
            return;
        }

        ParticleSystem ps = fireObj.GetComponent<ParticleSystem>();
        
        // 1. Realistic Color & Lifetime
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f); // Rotate base
        main.gravityModifier = -0.5f; // Aag oopar ko udti hai

        // 2. Emission Burst + Roar
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 150, 250) });
        
        // 3. Shape modification (Tight explosion out of the oven)
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.3f;

        // 4. Color over Lifetime (White hot -> Yellow -> Orange -> Smoke/Black)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0.0f), 
                new GradientColorKey(Color.yellow, 0.2f), 
                new GradientColorKey(new Color(1f, 0.4f, 0f), 0.5f), 
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1.0f) // Smoke at the end
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1.0f, 0.0f), 
                new GradientAlphaKey(1.0f, 0.7f), 
                new GradientAlphaKey(0.0f, 1.0f) 
            }
        );
        colorOverLifetime.color = grad;

        // 5. Size over Lifetime (Starts small, expands into fireball, then shrinks to smoke)
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0.0f, 0.5f);
        sizeCurve.AddKey(0.2f, 1.0f);
        sizeCurve.AddKey(1.0f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // 6. Rotation over Lifetime (Twisting flames)
        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

        // 7. Noise (Turbulence for realistic flickering)
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 1f;
        noise.scrollSpeed = 1f;

        // 8. Renderer Settings
        ParticleSystemRenderer renderer = fireObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        
        // Try to use a better default material if available (Default-Particle is softer than Default-ParticleSystem)
        Material softMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (softMat != null) renderer.material = softMat;

        Debug.Log("Fire upgraded to AAA Realistic Fireball!");
    }
}
