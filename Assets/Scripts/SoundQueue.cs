using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable enable

public class SoundQueue : MonoBehaviour
{
    private readonly Queue<AudioClip> queue = new();

    private AudioSource audioSource = null!;

    private CoroutineHandler playQueueCoroutine = null!;

    public void Enqueue(AudioClip clip)
    {
        queue.Enqueue(clip);

        if (!playQueueCoroutine.IsRunning)
        {
            playQueueCoroutine.Start(PlayQueuedSounds());
        }
    }

    public void Enqueue(params AudioClip[] clips)
    {
        foreach (AudioClip clip in clips)
        {
            Enqueue(clip);
        }
    }

    public void StopAndEnqueue(AudioClip clip)
    {
        Stop();
        Enqueue(clip);
    }

    public void StopAndEnqueue(params AudioClip[] clips)
    {
        Stop();
        Enqueue(clips);
    }

    public bool IsRunning()
    {
        return audioSource.isPlaying || playQueueCoroutine.IsRunning || !queue.IsEmpty();
    }

    public AudioClip? GetPlayingAudioClip()
    {
        if (!audioSource.isPlaying)
        {
            return null;
        }

        return audioSource.clip;
    }

    public bool IsAudioClipInQueue(AudioClip searchingClip)
    {
        return queue.Any(clip => clip == searchingClip);
    }

    public void Stop()
    {
        audioSource.Stop();
        playQueueCoroutine.Stop();
        queue.Clear();

        ResetAudioSource();
    }

    private void Awake()
    {
        audioSource = Utils.AddComponentAudioSource(gameObject);
        playQueueCoroutine = new(this);
    }

    private IEnumerator PlayQueuedSounds()
    {
        while (!queue.IsEmpty())
        {
            audioSource.clip = queue.Dequeue();

            audioSource.Play();
            yield return new WaitForSeconds(audioSource.clip.length);
        }

        ResetAudioSource();
    }

    private void ResetAudioSource()
    {
        audioSource.clip = null;
    }
}
