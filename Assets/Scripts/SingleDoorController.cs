using System.Collections;
using UnityEngine;

#nullable enable

public class SingleDoorController : MonoBehaviour
{
    [SerializeField]
    private Transform door = null!;

    [SerializeField]
    private float openDistance = 0.0f;

    [SerializeField]
    private float speed = 2f;

    private Vector3 doorClosedPos = Vector3.zero;

    private Vector3 doorOpenPos = Vector3.zero;

    private AudioSource doorAudio = null!;

    private CoroutineHandler doorCoroutine = null!;

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        doorClosedPos = door.position;
        doorOpenPos = doorClosedPos + new Vector3(0, openDistance, 0);
        doorAudio = door.GetComponent<AudioSource>();

        doorCoroutine = new(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (door.position == doorOpenPos)
        {
            return;
        }

        doorCoroutine.RestartWith(MoveDoor(doorOpenPos));
        PlayDoorAudio();
    }

    private void OnTriggerStay(Collider other)
    {
        if (door.position == doorOpenPos)
        {
            return;
        }

        doorCoroutine.RestartWith(MoveDoor(doorOpenPos));
    }

    private void OnTriggerExit(Collider other)
    {
        if (door.position == doorClosedPos)
        {
            return;
        }

        doorCoroutine.RestartWith(MoveDoor(doorClosedPos));
        PlayDoorAudio();
    }

    private IEnumerator MoveDoor(Vector3 target)
    {
        float t = 0.0f;

        Vector3 start = door.position;

        while (t < 1.0f)
        {
            t += Time.deltaTime * speed;
            door.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
    }

    private void PlayDoorAudio()
    {
        if (!doorAudio.isPlaying)
        {
            doorAudio.Play();
        }
    }
}
