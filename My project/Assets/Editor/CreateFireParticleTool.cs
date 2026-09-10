using UnityEngine;
using UnityEditor;

public class CreateFireParticleTool : EditorWindow
{
    [MenuItem("Tools/Create Fire Burst Particle")]
    public static void CreateFire()
    {
        GameObject fireObj = new GameObject("Oven_Fire_Burst");
        ParticleSystem ps = fireObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        
        main.duration = 2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startColor = new Color(1f, 0.4f, 0f, 1f); // Bright Orange/Red
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false; 

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 100, 200) }); // Dhamaka

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.5f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.yellow, 0.0f), new GradientColorKey(Color.red, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = grad;

        ParticleSystemRenderer renderer = fireObj.GetComponent<ParticleSystemRenderer>();
        Material softMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        if (softMat != null) renderer.material = softMat;

        Selection.activeGameObject = fireObj;
        Debug.Log("Fire Burst Particle Created!");
    }
}
