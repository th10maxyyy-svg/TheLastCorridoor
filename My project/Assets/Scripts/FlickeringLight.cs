using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField] private float minIntensity = 0.1f;
    [SerializeField] private float maxIntensity = 1.5f;
    [SerializeField] private float minFlickerSpeed = 0.05f;
    [SerializeField] private float maxFlickerSpeed = 0.2f;

    private Light localLight;

    void Start()
    {
        localLight = GetComponent<Light>();
        StartCoroutine(FlickerRoutine());
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            localLight.intensity = Random.Range(minIntensity, maxIntensity);
            float delay = Random.Range(minFlickerSpeed, maxFlickerSpeed);
            yield return new WaitForSeconds(delay);
        }
    }
}
