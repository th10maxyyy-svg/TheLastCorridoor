using UnityEngine;
using UnityEditor;

public class FixSparks : EditorWindow
{
    [MenuItem("Tools/Fix Exploding Sparks")]
    public static void Fix()
    {
        GameObject sparksObj = GameObject.Find("Electric_Sparks");
        if (sparksObj == null)
        {
            Debug.LogError("Electric_Sparks nahi mila.");
            return;
        }

        ParticleSystem ps = sparksObj.GetComponent<ParticleSystem>();
        var main = ps.main;
        
        // Size bara kar dein taake easily nazar aayen
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        
        // Trails ko filhal band kar dein (kabhi kabhi trails ki wajah se gayab ho jatay hain)
        var trails = ps.trails;
        trails.enabled = false;

        // Renderer ko Theek Karein
        ParticleSystemRenderer renderer = sparksObj.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard; // Stretch ke bajaye normal
        
        // Ohi material lagayen jo dhund (gas) par laga tha (Kyunke wo 100% chal raha hai)
        Material softMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        if (softMat != null)
        {
            renderer.material = softMat;
        }

        Debug.Log("Sparks Fixed! Ab lazmi nazar aayenge.");
    }
}
