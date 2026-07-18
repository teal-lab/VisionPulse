using UnityEngine;

#nullable enable

public class Waypoint : MonoBehaviour
{
    private AudioSource audioSource = null!;

    private void Awake()
    {
        audioSource = Utils.AddComponentAudioSourceWithHum(gameObject);
        audioSource.Play();
    }
}
