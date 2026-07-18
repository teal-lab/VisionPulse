using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

#nullable enable

public class GrabbableObjectTTS : MonoBehaviour
{
    private const float PulseDelay = 0.30f;

    private const float Amplitude = 0.25f;

    private const float Duration = 0.1f;

    [SerializeField]
    private XRGrabInteractable grabInteractable = null!;

    private AudioSource humAudioSource = null!;

    private SoundQueue soundQueue = null!;

    private bool suppressNextHoverWhenJustReleased = false;

    private CoroutineHandler playGrabbedObjectCoroutine = null!;

    private CoroutineHandler pulseControllerCoroutine = null!;

    private Player player = null!;

    private DiscoveredItems discoveredItems = null!;

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        humAudioSource = Utils.AddComponentAudioSourceWithHum(gameObject);
        soundQueue = gameObject.AddComponent<SoundQueue>();

        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.selectEntered.AddListener(OnGrabEntered);
        grabInteractable.selectExited.AddListener(OnGrabExited);

        playGrabbedObjectCoroutine = new(this);
        pulseControllerCoroutine = new(this);
    }

    private void Start()
    {
        player = VisionPulseResources.SceneSetup.GetPlayer();
        discoveredItems = VisionPulseResources.SceneSetup.GetDiscoveredItems();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (suppressNextHoverWhenJustReleased)
        {
            suppressNextHoverWhenJustReleased = false;
            return;
        }

        if (grabInteractable.isSelected)
        {
            return;
        }

        // Need to put this in a seperate variable because I want FoundTargetObjectIfMatch to be called
        // If I just call the method after the &&, && might short circuit
        bool foundObject = player.FoundTargetObjectIfMatch(gameObject);
        bool inNavigationMode = humAudioSource.isPlaying && foundObject;

        humAudioSource.Stop();

        if (inNavigationMode && args.interactorObject is XRBaseInputInteractor controllerInteractor)
        {
            pulseControllerCoroutine.Start(PulseController(controllerInteractor));
        }

        player.StopSoundQueue();
        HandleGrabbableObjectTTS();
    }

    private void OnGrabEntered(SelectEnterEventArgs args)
    {
        if (playGrabbedObjectCoroutine.IsRunning)
        {
            return;
        }

        IEnumerable<Region> regions = discoveredItems.GetRegions();

        foreach (Region region in regions)
        {
            if (region.RemoveInteractableObjectIfPresent(gameObject))
            {
                break;
            }
        }

        playGrabbedObjectCoroutine.Start(PlayObjectGrabbed());
    }

    private void OnGrabExited(SelectExitEventArgs args)
    {
        suppressNextHoverWhenJustReleased = true;

        if (VisionPulseResources.IsUserStudy)
        {
            return;
        }

        IEnumerable<Region> regions = discoveredItems.GetRegions();
        IEnumerator<Region> e = regions.GetEnumerator();

        if (!e.MoveNext())
        {
            return;
        }

        Region bestRegion = e.Current;
        float bestIntersectionVolume = bestRegion.GetIntersectionVolume(gameObject);

        while (e.MoveNext())
        {
            Region region = e.Current;
            float intersectionVolume = region.GetIntersectionVolume(gameObject);

            if (intersectionVolume > bestIntersectionVolume)
            {
                bestRegion = region;
                bestIntersectionVolume = intersectionVolume;
            }
        }

        bestRegion.AddInteractableObject(gameObject);
    }

    private void HandleGrabbableObjectTTS()
    {
        AudioClip grabbableObjectClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Grabbable Object");
        AudioClip objectNameClip = VisionPulseResources.Instance.GetTTSObjectAudioClip(gameObject.name);

        soundQueue.StopAndEnqueue(grabbableObjectClip, objectNameClip);
    }

    private IEnumerator PlayObjectGrabbed()
    {
        AudioClip objectGrabbed = VisionPulseResources.Instance.GetTTSMiscAudioClip("Object Grabbed");
        AudioClip objectNameClip = VisionPulseResources.Instance.GetTTSObjectAudioClip(gameObject.name);

        soundQueue.StopAndEnqueue(objectGrabbed, objectNameClip);

        yield return new WaitForSeconds(objectGrabbed.length + objectNameClip.length);

        player.AddObjectToInventory(gameObject);
    }

    private IEnumerator PulseController(XRBaseInputInteractor interactor)
    {
        yield return new WaitForSeconds(PulseDelay);
        interactor.SendHapticImpulse(Amplitude, Duration);
    }
}
