using UnityEngine;

#nullable enable

public class ConsoleObjectTTS : MonoBehaviour
{
    [SerializeField]
    private string funFactName = null!;

    private AudioSource humAudioSource = null!;

    private SoundQueue soundQueue = null!;

    private Player player = null!;

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        humAudioSource = Utils.AddComponentAudioSourceWithHum(gameObject);
        soundQueue = gameObject.AddComponent<SoundQueue>();
    }

    private void Start()
    {
        player = VisionPulseResources.SceneSetup.GetPlayer();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(VisionPulseResources.GameControllerTag))
        {
            return;
        }

        string sceneName = VisionPulseResources.Instance.GetActiveSceneName();

        if (sceneName != VisionPulseResources.TutorialSceneName)
        {
            VisionPulseResources.Instance.SetDidInteractWithConsole(sceneName, true);
        }

        humAudioSource.Stop();

        if (soundQueue.IsRunning())
        {
            return;
        }

        player.FoundTargetObjectIfMatch(gameObject);

        AudioClip funFactClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip(funFactName);
        soundQueue.Enqueue(funFactClip);
    }
}
