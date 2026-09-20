using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;

[RequireComponent(typeof(VideoPlayer))]
public class VideoPlaylist : MonoBehaviour
{
    public VideoClip[] videos; // Put all 3 videos here in the Inspector
    public UnityEvent onPlaylistFinished;

    private VideoPlayer videoPlayer;
    private int currentVideoIndex = 0;

    private static bool hasPlayedThisSession = false;

    void Start()
    {
        // CHECK: If player already saw the intro in this play session, disable projector!
        if (hasPlayedThisSession)
        {
            gameObject.SetActive(false);
            return;
        }

        hasPlayedThisSession = true;
        videoPlayer = GetComponent<VideoPlayer>();
        
        if (videos.Length > 0)
        {
            // Subscribe to the event that triggers when a video finishes
            videoPlayer.loopPointReached += PlayNextVideo;
            
            // Play the first video
            videoPlayer.clip = videos[0];
            videoPlayer.Play();
        }
    }

    void PlayNextVideo(VideoPlayer vp)
    {
        if (videos.Length == 0) return;

        currentVideoIndex++;
        
        // If we reached the end of the playlist
        if (currentVideoIndex >= videos.Length)
        {
            if (onPlaylistFinished != null)
            {
                onPlaylistFinished.Invoke();
            }
            return; // Stop playing
        }

        // Play the new video
        videoPlayer.clip = videos[currentVideoIndex];
        videoPlayer.Play();
    }
}
