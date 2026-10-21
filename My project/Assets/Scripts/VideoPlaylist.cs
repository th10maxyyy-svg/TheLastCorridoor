using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;

[RequireComponent(typeof(VideoPlayer))]
public class VideoPlaylist : MonoBehaviour
{
    public VideoClip[] videos; // Put all 3 videos here in the Inspector
    public UnityEvent onPlaylistFinished;

    [Header("Safety")]
    [Tooltip("Player is key se intro video skip kar sakta hai")]
    public KeyCode skipKey = KeyCode.Space;
    [Tooltip("Agar video kisi wajah se khatam hone ka signal na de (codec error waghera) to itne extra seconds baad khud aagay barh jao")]
    public float safetyExtraSeconds = 10f;

    private VideoPlayer videoPlayer;
    private int currentVideoIndex = 0;
    private bool finished = false;
    private float safetyDeadline = -1f;

    void Awake()
    {
        // Respawn after death: never replay the projector video
        if (IntroState.skipIntro)
        {
            VideoPlayer vp = GetComponent<VideoPlayer>();
            if (vp != null) { vp.playOnAwake = false; vp.Stop(); }
            gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (IntroState.skipIntro) return;

        videoPlayer = GetComponent<VideoPlayer>();

        if (videos == null || videos.Length == 0)
        {
            // Nothing to play - never leave the intro waiting forever
            Finish(false);
            return;
        }

        // Subscribe to the event that triggers when a video finishes
        videoPlayer.loopPointReached += PlayNextVideo;
        videoPlayer.errorReceived += OnVideoError;

        float totalLength = 0f;
        foreach (VideoClip clip in videos)
        {
            if (clip != null) totalLength += (float)clip.length;
        }
        safetyDeadline = Time.time + totalLength + safetyExtraSeconds;

        // Play the first video
        PlayClip(0);
    }

    void Update()
    {
        if (finished || safetyDeadline < 0f) return;

        if (Input.GetKeyDown(skipKey))
        {
            Finish(true);
        }
        else if (Time.time > safetyDeadline)
        {
            Debug.LogWarning("VideoPlaylist: Video never reported that it finished - continuing the intro anyway.");
            Finish(true);
        }
    }

    /// <summary>"Press SPACE to skip": a real object on the TLC_UI canvas (IntroSkipHint).</summary>
    void LateUpdate()
    {
        bool show = !finished && safetyDeadline >= 0f;
        TLCCanvas ui = show ? TLCCanvas.Ensure() : TLCCanvas.Instance;
        if (ui == null || ui.introSkipHint == null) return;
        TLCCanvas.SetText(ui.introSkipHint, show ? "Press " + skipKey.ToString().ToUpper() + " to skip" : "");
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogWarning("VideoPlaylist: Video error (" + message + ") - skipping to the next one.");
        PlayNextVideo(vp);
    }

    private void PlayClip(int index)
    {
        currentVideoIndex = index;
        if (videos[index] == null)
        {
            PlayNextVideo(videoPlayer); // skip empty slots
            return;
        }
        videoPlayer.clip = videos[index];
        videoPlayer.Play();
    }

    void PlayNextVideo(VideoPlayer vp)
    {
        if (finished || videos.Length == 0) return;

        int next = currentVideoIndex + 1;

        // If we reached the end of the playlist
        if (next >= videos.Length)
        {
            Finish(false);
            return; // Stop playing
        }

        // Play the new video
        PlayClip(next);
    }

    private void Finish(bool stopVideo)
    {
        if (finished) return;
        finished = true;

        if (stopVideo && videoPlayer != null) videoPlayer.Stop();

        if (onPlaylistFinished != null)
        {
            onPlaylistFinished.Invoke();
        }
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= PlayNextVideo;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}
