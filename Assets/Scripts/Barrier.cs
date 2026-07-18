using UnityEngine;

#nullable enable

public class Barrier : MonoBehaviour
{
    private const float Amplitude = 1.0f;

    private const float Duration = 0.05f;

    private AudioSource audioSource = null!;

    private ControllerHaptics? leftControllerHaptics = null;

    private ControllerHaptics? rightControllerHaptics = null;

    private void Start()
    {
        AudioClip hitWallEffect = VisionPulseResources.Instance.GetEffectAudioClip("Hit Wall");
        audioSource = Utils.AddComponentAudioSource(gameObject, hitWallEffect);

        InitializeControllerHaptics();
    }

    private void Update()
    {
        if (leftControllerHaptics != null && rightControllerHaptics != null)
        {
            return;
        }

        InitializeControllerHaptics();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        if (leftControllerHaptics != null && collision.gameObject.name == VisionPulseResources.LeftControllerName)
        {
            HandleHaptics(leftControllerHaptics);
        }

        if (rightControllerHaptics != null && collision.gameObject.name == VisionPulseResources.RightControllerName)
        {
            HandleHaptics(rightControllerHaptics);
        }
    }

    private void InitializeControllerHaptics()
    {
        GameObject[] controllers = GameObject.FindGameObjectsWithTag(VisionPulseResources.GameControllerTag);

        if (controllers.Length is < 1 or > 2)
        {
            return;
        }

        foreach (GameObject controller in controllers)
        {
            AssignControllerHaptics(controller);
        }
    }

    private void AssignControllerHaptics(GameObject controller)
    {
        if (controller.name == VisionPulseResources.LeftControllerName)
        {
            leftControllerHaptics = controller.GetComponent<ControllerHaptics>();
        }
        else
        {
            rightControllerHaptics = controller.GetComponent<ControllerHaptics>();
        }
    }

    private void HandleHaptics(ControllerHaptics controllerHaptics)
    {
        if (controllerHaptics.IsHapticRunning)
        {
            controllerHaptics.StopAndEnqueue((0.0f, Duration * 2), (Amplitude, Duration), (0.0f, Duration * 2));
        }
        else
        {
            controllerHaptics.Send(Amplitude, Duration);
        }
    }
}
