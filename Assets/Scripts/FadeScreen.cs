using System.Collections;
using UnityEngine;

#nullable enable

public class FadeScreen : MonoBehaviour
{
    [SerializeField]
    private bool fadeOnStart = true;

    [SerializeField]
    private Color fadeColor = Color.white;

    [SerializeField]
    private float fadeDuration = 2.0f;

    [SerializeField]
    private string sceneGoal = string.Empty;

    private Renderer rend = null!;

    private CoroutineHandler fadeCoroutine = null!;

    private SoundQueue soundQueue = null!;

    private bool ranUpdateOnce = false;

    public void FadeIn()
    {
        Fade(1.0f, 0.0f);
    }

    public void FadeOut()
    {
        Fade(0.0f, 1.0f);
    }

    public float GetFadeDuration()
    {
        return fadeDuration;
    }

    public bool IsRunning()
    {
        return fadeCoroutine.IsRunning || !ranUpdateOnce || soundQueue.IsRunning();
    }

    private void Awake()
    {
        if (!TryGetComponent(out Renderer render))
        {
            Debug.LogError($"GameObject '{gameObject.name}' does not have a component of type: {Utils.ComptimeTypeName(render)}.");
            return;
        }

        rend = render;
        fadeCoroutine = new(this);
        soundQueue = gameObject.AddComponent<SoundQueue>();
    }

    private void Start()
    {
        if (fadeOnStart)
        {
            FadeIn();
        }
    }

    private void Update()
    {
        if (ranUpdateOnce || fadeCoroutine.IsRunning)
        {
            return;
        }

        if (!VisionPulseResources.IsUserStudy
            || sceneGoal == string.Empty
            || VisionPulseResources.Instance.IsTutorialScene())
        {
            ranUpdateOnce = true;
            return;
        }

        AudioClip feedbackModalityClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip("Feedback Modality");
        AudioClip currentTaskNameClip = VisionPulseResources.Instance.GetTTSDescriptionOfCurrentTaskNameAudioClip();

        soundQueue.Enqueue(feedbackModalityClip, currentTaskNameClip);

        AudioClip goalClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip("Goal of Scene");
        AudioClip sceneGoalClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip(sceneGoal);

        soundQueue.Enqueue(goalClip, sceneGoalClip);

        ranUpdateOnce = true;
    }

    private void Fade(float alphaIn, float alphaOut)
    {
        fadeCoroutine.Start(FadeRoutine(alphaIn, alphaOut));
    }

    private IEnumerator FadeRoutine(float alphaIn, float alphaOut)
    {
        float timer = 0.0f;

        while (timer <= fadeDuration)
        {
            Color newColor = fadeColor;
            newColor.a = Mathf.Lerp(alphaIn, alphaOut, timer / fadeDuration);

            rend.material.SetColor("_Color", newColor);

            timer += Time.deltaTime;
            yield return null;
        }

        Color newColor2 = fadeColor;
        newColor2.a = alphaOut;
        rend.material.SetColor("_Color", newColor2);
    }
}
