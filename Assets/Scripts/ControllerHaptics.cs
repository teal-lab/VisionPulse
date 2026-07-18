using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

#nullable enable

public class ControllerHaptics : MonoBehaviour
{
    private readonly Queue<(float, float)> queue = new();

    [SerializeField]
    private HapticImpulsePlayer controllerHaptics = null!;

    private CoroutineHandler hapticsQueueCoroutine = null!;

    private float currentHapticEndTime = 0f;

    public bool IsHapticRunning => Time.time < currentHapticEndTime;

    public bool IsQueueRunning => hapticsQueueCoroutine.IsRunning || !queue.IsEmpty();

    public void Send(float amplitude, float duration)
    {
        if (!gameObject.activeSelf || IsQueueRunning)
        {
            return;
        }

        controllerHaptics.SendHapticImpulse(amplitude, duration);
        currentHapticEndTime = Time.time + duration;
    }

    public void Enqueue(float amplitude, float duration)
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        queue.Enqueue((amplitude, duration));

        if (!hapticsQueueCoroutine.IsRunning)
        {
            hapticsQueueCoroutine.Start(StartQueuedHaptics());
        }
    }

    public void Enqueue(params (float, float)[] haptics)
    {
        foreach ((float, float) haptic in haptics)
        {
            Enqueue(haptic.Item1, haptic.Item2);
        }
    }

    public void StopAndEnqueue(params (float, float)[] haptics)
    {
        Stop();
        Enqueue(haptics);
    }

    public void Stop()
    {
        hapticsQueueCoroutine.Stop();
        queue.Clear();
    }

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        hapticsQueueCoroutine = new(this);
    }

    private IEnumerator StartQueuedHaptics()
    {
        while (!queue.IsEmpty())
        {
            (float, float) values = queue.Dequeue();

            controllerHaptics.SendHapticImpulse(values.Item1, values.Item2);
            currentHapticEndTime = Time.time + values.Item2;

            yield return new WaitForSeconds(values.Item2);
        }
    }
}
