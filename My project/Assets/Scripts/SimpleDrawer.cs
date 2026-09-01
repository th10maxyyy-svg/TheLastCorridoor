using UnityEngine;
using System.Collections;

public class SimpleDrawer : MonoBehaviour
{
    public enum SlideAxis { X, Y, Z }
    
    [Header("Drawer Settings")]
    [Tooltip("Which direction does the drawer open? Usually Z (forward/backward) or X (left/right)")]
    public SlideAxis slideAxis = SlideAxis.Z;
    [Tooltip("How far the drawer should slide open. Positive or negative number.")]
    public float openDistance = 0.5f;
    [Tooltip("How long it takes to open.")]
    public float openDuration = 1.0f;
    
    [Header("Audio (Optional)")]
    public AudioClip slideSound;
    private AudioSource audioSource;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool isOpen = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        closedPosition = transform.localPosition;
        
        Vector3 direction = Vector3.zero;
        if (slideAxis == SlideAxis.X) direction = Vector3.right;
        else if (slideAxis == SlideAxis.Y) direction = Vector3.up;
        else if (slideAxis == SlideAxis.Z) direction = Vector3.forward;

        openPosition = closedPosition + (direction * openDistance);
    }

    public void Open()
    {
        if (!isOpen)
        {
            isOpen = true;
            if (slideSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(slideSound);
            }
            StartCoroutine(SlideDrawer(openPosition));
        }
    }

    private IEnumerator SlideDrawer(Vector3 targetPosition)
    {
        float timeElapsed = 0;
        Vector3 startPos = transform.localPosition;

        while (timeElapsed < openDuration)
        {
            transform.localPosition = Vector3.Lerp(startPos, targetPosition, timeElapsed / openDuration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = targetPosition;
    }
}
