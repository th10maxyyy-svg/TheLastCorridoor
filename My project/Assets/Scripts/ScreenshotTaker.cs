using UnityEngine;

public class ScreenshotTaker : MonoBehaviour
{
    [Header("Screenshot Settings")]
    public KeyCode captureKey = KeyCode.K; // Changed to K
    [Tooltip("Increase this number to take higher resolution screenshots (e.g., 2 = double resolution)")]
    public int resolutionMultiplier = 2;

    void Update()
    {
        if (Input.GetKeyDown(captureKey))
        {
            TakeScreenshot();
        }
    }

    [ContextMenu("Take Screenshot Now")] // This allows taking a screenshot from the Inspector
    void TakeScreenshot()
    {
        // Generate a unique filename using the current date and time
        string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"Screenshot_{timeStamp}.png";

        // Take the screenshot
        ScreenCapture.CaptureScreenshot(filename, resolutionMultiplier);
        
        Debug.Log($"<color=green>Screenshot Saved Successfully!</color> Saved as: {filename} in your main project folder.");
    }
}
