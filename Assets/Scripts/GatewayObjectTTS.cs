using System.Collections;
using UnityEngine;

#nullable enable

public class GatewayObjectTTS : MonoBehaviour
{
    private AudioSource humAudioSource = null!;

    private SoundQueue soundQueue = null!;

    private CoroutineHandler taskCompletedCoroutine = null!;

    private FadeScreen fadeScreen = null!;

    private Player player = null!;

    private void Awake()
    {
        humAudioSource = Utils.AddComponentAudioSourceWithHum(gameObject);
        soundQueue = gameObject.AddComponent<SoundQueue>();
        taskCompletedCoroutine = new(this);
    }

    private void Start()
    {
        fadeScreen = VisionPulseResources.SceneSetup.GetFadeScreen();
        player = VisionPulseResources.SceneSetup.GetPlayer();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag(VisionPulseResources.GameControllerTag)
            && !collision.gameObject.CompareTag("MainCamera"))
        {
            return;
        }

        humAudioSource.Stop();

        if (soundQueue.IsRunning())
        {
            return;
        }

        player.StopSoundQueue();
        player.FoundTargetObjectIfMatch(gameObject);

        if (player.InventoryCount == VisionPulseResources.NumberOfKeysToCompleteScene)
        {
            taskCompletedCoroutine.Start(PlayTaskCompletedAndLoadNextScene());
        }
        else
        {
            AudioClip objectsNotCollectedClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("The Key Has Not Been Collected");
            soundQueue.Enqueue(objectsNotCollectedClip);
        }
    }

    private IEnumerator PlayTaskCompletedAndLoadNextScene()
    {
        string activeSceneName = VisionPulseResources.Instance.GetActiveSceneName();

        if (activeSceneName != VisionPulseResources.TutorialSceneName)
        {
            double elapsedSeconds = VisionPulseResources.SceneSetup.StopAndGetElapsedSeconds();
            VisionPulseResources.Instance.SetCompletionTimeInSceneData(activeSceneName, elapsedSeconds);
            VisionPulseResources.Instance.WriteOutSessionData();
        }

        if (activeSceneName == VisionPulseResources.TutorialSceneName)
        {
            AudioClip clip = VisionPulseResources.Instance.GetTTSMiscAudioClip("The Key Has Been Collected");
            soundQueue.Enqueue(clip);

            yield return new WaitForSeconds(clip.length);

            Destroy(gameObject);
            yield break;
        }

        bool isHalfwayDone = VisionPulseResources.Instance.IsHalfwayDone();
        int? nextScene = VisionPulseResources.Instance.SetupNextSceneAndGetNumber();

        if (nextScene == null)
        {
            AudioClip congratsdClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip("Congratulations");
            soundQueue.Enqueue(congratsdClip);
            yield break;
        }

        AudioClip objectsCollectedClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("The Key Has Been Collected");
        float totalTime = objectsCollectedClip.length;

        soundQueue.Enqueue(objectsCollectedClip);

        AudioClip movingOnClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Moving On To The Next Scene");
        totalTime += movingOnClip.length;

        soundQueue.Enqueue(movingOnClip);

        yield return new WaitForSeconds(totalTime);

        string sceneName = (nextScene == -1) ? VisionPulseResources.TutorialSceneName : $"UserStudyScene{nextScene}";

        fadeScreen.FadeOut();
        yield return VisionPulseResources.Instance.LoadScene(sceneName, fadeScreen.GetFadeDuration());
    }
}
