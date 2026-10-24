#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// TLC BRANDING + WEBSITE BUILD:
///   - game name "The Last Corridor", company "HollowVein Studios"
///   - no "Made with Unity" splash screen, no Unity logo
///   - the game icon (exe, taskbar, window) is the TLC logo (Assets/Branding/TLC_Icon.png)
///   - menu "The Last Corridor / Build Windows Game (for the website)": builds the game and puts a ready zip in
///     Website/downloads/TheLastCorridor_Windows.zip (+ version.json, read by the website's Download section)
/// Branding runs once by itself; run again from: The Last Corridor / Apply TLC Branding.
/// </summary>
[InitializeOnLoad]
public static class BrandingSetup
{
    private const string PrefKey = "TLC_Branding_v2_";
    private const string IconPath = "Assets/Branding/TLC_Icon.png";
    private const string LogoPath = "Assets/Branding/TLC_Logo.png";
    private const string ProductName = "The Last Corridor";
    private const string CompanyName = "HollowVein Studios";
    private const string ZipName = "TheLastCorridor_Windows.zip";
    private static bool triedThisSession;

    static BrandingSetup() { EditorApplication.delayCall += TryAuto; }

    private static void TryAuto()
    {
        string key = PrefKey + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetBool(key, false) || triedThisSession) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += TryAuto; return; }
        triedThisSession = true;
        if (Apply()) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("The Last Corridor/Apply TLC Branding (icon, name, no Unity splash)")]
    private static void Menu() { Apply(); }

    private static void PrepareTexture(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool dirty = false;
        if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
        if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
        if (ti.alphaIsTransparency == false) { ti.alphaIsTransparency = true; dirty = true; }
        if (ti.maxTextureSize < 1024) { ti.maxTextureSize = 1024; dirty = true; }
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }

    public static bool Apply()
    {
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(LogoPath, ImportAssetOptions.ForceSynchronousImport);
        PrepareTexture(IconPath);
        PrepareTexture(LogoPath);
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { Debug.LogWarning("[TLC] Branding: " + IconPath + " not found yet."); return false; }

        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;

        // no "Made with Unity"
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        // default icon (used for every platform) + the Windows icon sizes explicitly
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        try
        {
            int n = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length;
            if (n > 0) PlayerSettings.SetIcons(NamedBuildTarget.Standalone, Enumerable.Repeat(icon, n).ToArray(), IconKind.Any);
        }
        catch (Exception e) { Debug.Log("[TLC] Standalone icon sizes: " + e.Message); }

        AssetDatabase.SaveAssets();
        Debug.Log("[TLC] Branding applied: name '" + ProductName + "', TLC icon, Unity splash OFF.");
        return true;
    }

    [MenuItem("The Last Corridor/Build Windows Game (for the website)")]
    private static void BuildForWebsite()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TLC] Stop Play mode first."); return; }
        if (!Apply()) { EditorUtility.DisplayDialog("The Last Corridor", "TLC icon not found in Assets/Branding.", "OK"); return; }

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { EditorUtility.DisplayDialog("The Last Corridor", "No scenes are ticked in File > Build Profiles (Scene List).", "OK"); return; }

        string project = Path.GetDirectoryName(Application.dataPath);
        string buildDir = Path.Combine(project, "Builds", "TheLastCorridor");
        if (Directory.Exists(buildDir)) { try { Directory.Delete(buildDir, true); } catch { } }
        Directory.CreateDirectory(buildDir);

        BuildPlayerOptions opt = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(buildDir, ProductName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport rep = BuildPipeline.BuildPlayer(opt);
        if (rep.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("The Last Corridor", "Build failed - see the Console.", "OK");
            return;
        }

        string dlDir = Path.Combine(project, "Website", "downloads");
        Directory.CreateDirectory(dlDir);
        string zipPath = Path.Combine(dlDir, ZipName);
        try
        {
            EditorUtility.DisplayProgressBar("The Last Corridor", "Zipping the game for the website...", 0.5f);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (FileStream fs = new FileStream(zipPath, FileMode.Create))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(buildDir.Length + 1).Replace('\\', '/');
                    if (rel.Contains("_DoNotShip") || rel.Contains("_ButDontShipItWithYourGame")) continue;
                    zip.CreateEntryFromFile(file, "The Last Corridor/" + rel, System.IO.Compression.CompressionLevel.Optimal);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        double mb = new FileInfo(zipPath).Length / (1024.0 * 1024.0);
        string json = "{\n  \"version\": \"" + PlayerSettings.bundleVersion + "\",\n  \"sizeMB\": " + mb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                      ",\n  \"date\": \"" + DateTime.Now.ToString("yyyy-MM-dd") + "\",\n  \"file\": \"" + ZipName + "\"\n}\n";
        File.WriteAllText(Path.Combine(dlDir, "version.json"), json);

        EditorUtility.RevealInFinder(zipPath);
        EditorUtility.DisplayDialog("The Last Corridor",
            "Game built and zipped (" + mb.ToString("0") + " MB):\nWebsite/downloads/" + ZipName +
            (mb > 95 ? "\n\nNOTE: over 100 MB - GitHub Pages will not host it. Upload the zip to Google Drive / itch.io / GitHub Releases and put that link in Website/script.js (DOWNLOAD_URL)." : ""),
            "OK");
    }
}
#endif
