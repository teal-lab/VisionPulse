using System.Collections;
using UnityEngine;

public class DoubleDoorController : MonoBehaviour
{
    private const float DoorOpenAngle = -90.0f;

    private const float DoorCloseAngle = 90.0f;

    private const float DoorsMoveTime = 1.0f;

    private const float Smooth = 1.0f;

    private const float OpenSpeed = 2f;

    [SerializeField]
    private Transform leftDoor = null!;

    [SerializeField]
    private Transform rightDoor = null!;

    private CoroutineHandler doorCoroutine = null!;

    private AudioSource doorAudio = null!;

    private AudioClip openDoor = null!;

    private AudioClip closeDoor = null!;

    private bool isOpen = true;

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        doorCoroutine = new(this);
        doorAudio = Utils.AddComponentAudioSource(gameObject);
        openDoor = VisionPulseResources.Instance.GetEffectAudioClip("Door_Open");
        closeDoor = VisionPulseResources.Instance.GetEffectAudioClip("Door_Close");

        isOpen = false;
        doorCoroutine.Start(MoveDoors());
    }

    private void OnTriggerEnter(Collider other)
    {
        ToggleDoor();
    }

    private void ToggleDoor()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = !isOpen;
        doorCoroutine.RestartWith(MoveDoors());
        PlayDoorAudio();
    }

    private IEnumerator MoveDoors()
    {
        float timeElapsed = 0.0f;

        float leftAngle = isOpen ? DoorOpenAngle : DoorCloseAngle;
        Quaternion leftStartRotation = leftDoor.transform.rotation;
        Quaternion leftEndRotation = leftStartRotation * Quaternion.Euler(0, leftAngle, 0);

        float rightAngle = isOpen ? DoorOpenAngle : DoorCloseAngle;
        Quaternion rightStartRotation = rightDoor.transform.rotation;
        Quaternion rightEndRotation = rightStartRotation * Quaternion.Euler(0, rightAngle, 0);

        while (timeElapsed < DoorsMoveTime)
        {
            leftDoor.transform.rotation = Quaternion.Slerp(leftStartRotation, leftEndRotation, timeElapsed / DoorsMoveTime);
            rightDoor.transform.rotation = Quaternion.Slerp(rightStartRotation, rightEndRotation, timeElapsed / DoorsMoveTime);

            timeElapsed += Time.deltaTime * Smooth * OpenSpeed;
            yield return null;
        }

        leftDoor.transform.rotation = leftEndRotation;
        rightDoor.transform.rotation = rightEndRotation;
    }

    private void PlayDoorAudio()
    {
        if (doorAudio.isPlaying)
        {
            return;
        }

        doorAudio.clip = isOpen ? openDoor : closeDoor;
        doorAudio.Play();
    }
}