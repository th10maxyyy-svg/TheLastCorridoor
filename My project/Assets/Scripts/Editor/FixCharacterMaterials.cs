using UnityEngine;
using UnityEditor;

public class FixCharacterMaterials : EditorWindow
{
    [MenuItem("Tools/1-Click Fix Character Colors")]
    public static void FixColors()
    {
        GameObject obj = Selection.activeGameObject;
        if (obj == null)
        {
            EditorUtility.DisplayDialog("Error", "Pehle Scene mein apne character par click kar ke usay select karein!", "OK");
            return;
        }

        string[] keywords = { "Body", "Bottom", "Top", "Shoe", "Hair", "Eyelash" };
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        int count = 0;

        foreach (Renderer r in renderers)
        {
            Material[] newMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                Material currentMat = r.sharedMaterials[i];
                string meshName = r.gameObject.name;
                string searchWord = "";

                // Mesh ya Purane material ke naam se andaza lagao ke ye kya hai
                foreach (string kw in keywords)
                {
                    if (meshName.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                        (currentMat != null && currentMat.name.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        searchWord = kw;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(searchWord)) 
                {
                    newMats[i] = currentMat;
                    continue;
                }

                // Is hissay ke liye project mein Material dhoondo
                string[] matGuids = AssetDatabase.FindAssets("t:Material " + searchWord + "mat");
                Material targetMat = null;
                if (matGuids.Length > 0)
                {
                    targetMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuids[0]));
                }

                // Agar material nahi mila to naya bana lo!
                if (targetMat == null)
                {
                    targetMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    targetMat.name = searchWord + "mat";
                    AssetDatabase.CreateAsset(targetMat, "Assets/" + searchWord + "mat.mat");
                }

                // Ab iski Textures dhoondo
                string[] texGuids = AssetDatabase.FindAssets("t:Texture2D " + searchWord);
                foreach (string guid in texGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                    {
                        string texName = tex.name.ToLower();
                        if (texName.Contains("diffuse") || texName.Contains("albedo") || texName.Contains("basecolor") || texName.Contains("color") || texName.Contains("base_color"))
                        {
                            if (targetMat.HasProperty("_BaseMap")) targetMat.SetTexture("_BaseMap", tex);
                            if (targetMat.HasProperty("_MainTex")) targetMat.SetTexture("_MainTex", tex);
                            if (targetMat.HasProperty("_BaseColor")) targetMat.SetColor("_BaseColor", Color.white);
                            if (targetMat.HasProperty("_Color")) targetMat.SetColor("_Color", Color.white);
                        }
                        if (texName.Contains("normal") || texName.Contains("bump"))
                        {
                            if (targetMat.HasProperty("_BumpMap")) targetMat.SetTexture("_BumpMap", tex);
                            targetMat.EnableKeyword("_NORMALMAP");
                        }
                    }
                }
                
                EditorUtility.SetDirty(targetMat);
                newMats[i] = targetMat;
                count++;
            }
            r.sharedMaterials = newMats;
        }

        AssetDatabase.SaveAssets();

        if (count > 0)
        {
            EditorUtility.DisplayDialog("Boom!", $"Zabardast! {count} parts ke colours force-fix ho gaye hain. Ab check karein!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Oops", "Kuch nahi mila. Kya character par koi textures as-sign hone wali hain?", "Try Again");
        }
    }
}
