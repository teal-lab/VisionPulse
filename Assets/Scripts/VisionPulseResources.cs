using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

public class VisionPulseResources : GenericSingleton<VisionPulseResources>
{
    public const int NumberOfKeysToCompleteScene = 1;

    public static readonly string TutorialSceneName = "UserStudyTutorialScene";

    public static readonly string UndiscoveredSection = "Undiscovered Section";

    public static readonly string InitializerCube = "Initializer Cube";

    public static readonly string FaderScreen = "Fader Screen";

    public static readonly string XROrigin = "XR Origin (XR Rig)";

    public static readonly string LeftControllerName = "Left Controller";

    public static readonly string RightControllerName = "Right Controller";

    public static readonly string HumSoundEffect = "Hum";

    public static readonly string DiscoveredRegionSoundEffect = "Discovered Region";

    public static readonly string SelectedMenuItemSoundEffect = "Selected Menu Item";

    public static readonly string VisionPulseRegionTag = "VisionPulseRegion";

    public static readonly string VisionPulseSectionTag = "VisionPulseSection";

    public static readonly string VisionPulseObjectTag = "VisionPulseObject";

    public static readonly string GameControllerTag = "GameController";

    public static readonly string[] SpeedOptions = { "1x Speed", "1.25x Speed", "1.5x Speed", "2x Speed", "4x Speed" };

    private static readonly string ParticipantName = "P1";

    private static readonly List<int> SceneNumbers = new() { 1, 2, 3, 4, 5, 6 };

    private static readonly List<UserStudyTask> UserStudyTasks = new() { UserStudyTask.STANDARD_AUDIOBEACON, UserStudyTask.VISIONPULSE_AUDIOBEACON, UserStudyTask.VISIONPULSE_HAPTICS_AUDIOBEACON };

    private static string sessionDataFileName = string.Empty;

    private readonly Dictionary<string, AudioClip> effectsAudio = new();

    private readonly List<AudioClip> runningOnDirtAudio = new();

    private readonly List<AudioClip> runningOnGrassAudio = new();

    private readonly List<AudioClip> runningOnMetalAudio = new();

    private readonly List<AudioClip> runningOnWoodAudio = new();

    private readonly Dictionary<string, AudioClip> ttsDescriptionsAudio = new();

    private readonly Dictionary<string, AudioClip> ttsMiscAudio = new();

    private readonly Dictionary<string, AudioClip> ttsNumbersAudio = new();

    private readonly Dictionary<string, AudioClip> ttsObjectsAudio = new();

    private readonly Dictionary<string, AudioClip> ttsSpeedMenuAudio = new();

    private SessionData sessionData = null!;

    public enum UserStudyTest
    {
        PREBUILT_MENU,
        DISCOVERY,
    }

    public enum UserStudyTask
    {
        STANDARD_AUDIOBEACON,
        VISIONPULSE_AUDIOBEACON,
        VISIONPULSE_HAPTICS_AUDIOBEACON,
    }

    public static bool IsUserStudy { get; private set; } = true;

    public static bool IsDebug { get; private set; } = false;

    public static SceneSetup SceneSetup { get; private set; } = null!;

    public static int IgnoreRaycastLayerValue { get; private set; } = -1;

    public static UserStudyTest CurrentTest { get; private set; } = UserStudyTest.DISCOVERY;

    public static UserStudyTask CurrentTask { get; private set; } = UserStudyTask.VISIONPULSE_HAPTICS_AUDIOBEACON;

    public void SetCompletionTimeInSceneData(string sceneName, double completionTime)
    {
        sessionData.SceneResults[sceneName].CompletionTime = completionTime;
    }

    public void SetDidInteractWithConsole(string sceneName, bool didInteractWithConsole)
    {
        sessionData.SceneResults[sceneName].DidInteractWithConsole = didInteractWithConsole;
    }

    public void AddPlayerTimeAtPosition(string sceneName, TimestampPosition timestampPosition)
    {
        sessionData.SceneResults[sceneName].PlayerTimeAtPosition.Add(timestampPosition);
    }

    public void AddWaypointTimeAtPosition(string sceneName, TimestampPosition timestampPosition)
    {
        sessionData.SceneResults[sceneName].WaypointsTimeAtPosition.Add(timestampPosition);
    }

    public void ResetSceneData(string sceneName)
    {
        sessionData.SceneResults[sceneName].Reset();
    }

    public void WriteOutSessionData()
    {
        // If you want want to write out the session data,
        // remove the "return" statement
#pragma warning disable CS0162
        return;
        JsonSerializerSettings settings = new()
        {
            Converters = { new StringEnumConverter() },
        };

        string json = JsonConvert.SerializeObject(sessionData, Formatting.Indented, settings);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(projectRoot, sessionDataFileName);

        File.WriteAllText(path, json);
#pragma warning restore CS0162
    }

    public string GetActiveSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    public bool IsTutorialScene()
    {
        return GetActiveSceneName() == TutorialSceneName;
    }

    public bool IsOnFirstScene()
    {
        return SceneNumbers.Count == 8;
    }

    public bool IsHalfwayDone()
    {
        return SceneNumbers.Count == 3;
    }

    public IEnumerator LoadScene(string sceneNameToLoad, float duration)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneNameToLoad);
        operation.allowSceneActivation = false;

        float timer = 0.0f;

        while (timer <= duration && !operation.isDone)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;
    }

    public int? SetupNextSceneAndGetNumber()
    {
        if (IsUserStudy && IsHalfwayDone())
        {
            CurrentTest = Utils.GetNextEnumValue(CurrentTest);
            Debug.Log($"New Test: {CurrentTest}");
        }

        UserStudyTask? nextTask = UserStudyTasks.Pop(0);

        if (IsUserStudy && nextTask != null)
        {
            CurrentTask = (UserStudyTask)nextTask;
        }

        return SceneNumbers.Pop(0);
    }

    public AudioClip GetEffectAudioClip(string name)
    {
        return effectsAudio[name];
    }

    public AudioClip GetRandomRunningOnDirtClip()
    {
        return Utils.RandomChoice(runningOnDirtAudio);
    }

    public AudioClip GetRandomRunningOnGrassClip()
    {
        return Utils.RandomChoice(runningOnGrassAudio);
    }

    public AudioClip GetRandomRunningOnMetalClip()
    {
        return Utils.RandomChoice(runningOnMetalAudio);
    }

    public AudioClip GetRandomRunningOnWoodClip()
    {
        return Utils.RandomChoice(runningOnWoodAudio);
    }

    public AudioClip GetTTSDescriptionAudioClip(string name)
    {
        return ttsDescriptionsAudio[name];
    }

    public AudioClip GetTTSDescriptionOfCurrentTestNameAudioClip()
    {
        switch (CurrentTest)
        {
            case UserStudyTest.PREBUILT_MENU:
                return GetTTSDescriptionAudioClip("Prebuilt Test");
            case UserStudyTest.DISCOVERY:
                return GetTTSDescriptionAudioClip("Discovery Test");
            default:
                break;
        }

        Debug.LogError($"Could not find clip for {CurrentTest}");
        return null!;
    }

    public AudioClip GetTTSDescriptionOfCurrentTaskNameAudioClip()
    {
        switch (CurrentTask)
        {
            case UserStudyTask.STANDARD_AUDIOBEACON:
                return GetTTSDescriptionAudioClip("Audio Beacon");
            case UserStudyTask.VISIONPULSE_AUDIOBEACON:
                return GetTTSDescriptionAudioClip("Responsive Audio Beacon");
            case UserStudyTask.VISIONPULSE_HAPTICS_AUDIOBEACON:
                return GetTTSDescriptionAudioClip("Responsive Audio Beacon + Haptics");
            default:
                break;
        }

        Debug.LogError($"Could not find clip for {CurrentTask}");
        return null!;
    }

    public AudioClip[] GetTTSDescriptionOfCurrentTaskDescriptionAudioClip()
    {
        switch (CurrentTask)
        {
            case UserStudyTask.STANDARD_AUDIOBEACON:
                return new[] { GetTTSDescriptionAudioClip("Audio Beacon Description") };
            case UserStudyTask.VISIONPULSE_AUDIOBEACON:
                return new[] { GetTTSDescriptionAudioClip("Responsive Audio Beacon Description") };
            case UserStudyTask.VISIONPULSE_HAPTICS_AUDIOBEACON:
                return new[] { GetTTSDescriptionAudioClip("Responsive Audio Beacon Description"), GetTTSDescriptionAudioClip("Haptics Description") };
            default:
                break;
        }

        Debug.LogError($"Could not find clip for {CurrentTask}");
        return null!;
    }

    public AudioClip GetTTSMiscAudioClip(string name)
    {
        return ttsMiscAudio[name];
    }

    public AudioClip GetTTSNumberAudioClip(int number)
    {
        return GetTTSNumberAudioClip(number.ToString());
    }

    public AudioClip GetTTSNumberAudioClip(string name)
    {
        return ttsNumbersAudio[name];
    }

    public bool ContainsTTSObjectAudioName(string name)
    {
        return ttsObjectsAudio.ContainsKey(name);
    }

    public AudioClip GetTTSObjectAudioClip(string name)
    {
        return ttsObjectsAudio[name];
    }

    public void SetTTSSpeed(string speedOption)
    {
        ttsDescriptionsAudio.Clear();
        ttsMiscAudio.Clear();
        ttsNumbersAudio.Clear();
        ttsObjectsAudio.Clear();

        ReadInAudioClips($"Sounds/TTSDescriptions/{speedOption}", ttsDescriptionsAudio);
        ReadInAudioClips($"Sounds/TTSMisc/{speedOption}", ttsMiscAudio);
        ReadInAudioClips($"Sounds/TTSNumbers/{speedOption}", ttsNumbersAudio);
        ReadInAudioClips($"Sounds/TTSObjects/{speedOption}", ttsObjectsAudio);
    }

    public AudioClip GetTTSSpeedMenuAudioClip(string name)
    {
        return ttsSpeedMenuAudio[name];
    }

    protected override void Awake()
    {
        base.Awake();

        Random.InitState((int)System.DateTime.Now.Ticks);

        sessionDataFileName = $"session_data_{System.DateTime.Now.Ticks}.json";

        string defaultSpeedOption = SpeedOptions[0];

        ReadInAudioClips("Sounds/Effects/Misc", effectsAudio);
        ReadInAudioClips("Sounds/Effects/RunningOnDirt", runningOnDirtAudio);
        ReadInAudioClips("Sounds/Effects/RunningOnGrass", runningOnGrassAudio);
        ReadInAudioClips("Sounds/Effects/RunningOnMetal", runningOnMetalAudio);
        ReadInAudioClips("Sounds/Effects/RunningOnWood", runningOnWoodAudio);
        ReadInAudioClips($"Sounds/TTSDescriptions/{defaultSpeedOption}", ttsDescriptionsAudio);
        ReadInAudioClips($"Sounds/TTSMisc/{defaultSpeedOption}", ttsMiscAudio);
        ReadInAudioClips($"Sounds/TTSNumbers/{defaultSpeedOption}", ttsNumbersAudio);
        ReadInAudioClips($"Sounds/TTSObjects/{defaultSpeedOption}", ttsObjectsAudio);
        ReadInAudioClips("Sounds/TTSSpeedMenu", ttsSpeedMenuAudio);
        SetIgnoreRaycastLayerValue();

        Utils.Shuffle(SceneNumbers);

        if (IsUserStudy)
        {
            CurrentTest = Utils.GetRandomEnumValue<UserStudyTest>();
        }

        Utils.Shuffle(UserStudyTasks);
        UserStudyTasks.AddRange(UserStudyTasks);

        SetupSessionData();

        int halfway = SceneNumbers.Count / 2;

        for (int i = halfway - 1; i >= 0; i--)
        {
            SceneNumbers.Insert(i, -1);
        }

        for (int i = halfway - 1; i >= 0; i--)
        {
            UserStudyTasks.Insert(i + 1, UserStudyTasks[i]);
        }

        Debug.Log(Utils.JoinValuesToStringByComma(SceneNumbers));
        Debug.Log(CurrentTest);
        Debug.Log(Utils.JoinValuesToStringByComma(UserStudyTasks));

        SceneNumbers.Pop(0);

        if (IsUserStudy)
        {
            CurrentTask = (UserStudyTask)UserStudyTasks.Pop(0)!;
        }
    }

    protected override void OnSceneLoaded()
    {
        SceneSetup = Utils.FindGameObjectAndGetComponent<SceneSetup>(InitializerCube);
    }

    private void SetupSessionData()
    {
        List<int> orderOfScenes = new(SceneNumbers);
        List<UserStudyTest> orderOfTests = Enumerable.Repeat(CurrentTest, 3).Concat(Enumerable.Repeat(Utils.GetNextEnumValue(CurrentTest), 3)).ToList();
        List<UserStudyTask> orderOfTasks = new(UserStudyTasks);

        if (!(orderOfScenes.Count == orderOfTests.Count
            && orderOfTests.Count == orderOfTasks.Count))
        {
            Debug.LogError("Should have all the same length");
            return;
        }

        OrderedDict<string, SceneData> sceneResults = new();

        for (int i = 0; i < orderOfScenes.Count; i++)
        {
            string sceneName = $"UserStudyScene{orderOfScenes[i]}";
            UserStudyTest test = orderOfTests[i];
            UserStudyTask task = orderOfTasks[i];

            SceneData sceneData = new()
            {
                Test = test,
                Task = task,
            };

            sceneResults[sceneName] = sceneData;
        }

        sessionData = new()
        {
            Name = ParticipantName,
            OrderOfScenes = orderOfScenes,
            OrderOfTests = orderOfTests,
            OrderOfTasks = orderOfTasks,
            SceneResults = sceneResults,
        };

        WriteOutSessionData();
    }

    private void ReadInAudioClips(string path, Dictionary<string, AudioClip> audioClipsByName)
    {
        AudioClip[] audioClips = Resources.LoadAll<AudioClip>(path);

        foreach (AudioClip clip in audioClips)
        {
            audioClipsByName.Add(clip.name, clip);
        }
    }

    private void ReadInAudioClips(string path, List<AudioClip> audioClipsList)
    {
        AudioClip[] audioClips = Resources.LoadAll<AudioClip>(path);

        foreach (AudioClip clip in audioClips)
        {
            audioClipsList.Add(clip);
        }
    }

    private void SetIgnoreRaycastLayerValue()
    {
        string name = "Ignore Raycast";
        List<string?> layerNames = Utils.GetAllLayerNames();
        IgnoreRaycastLayerValue = layerNames.IndexOf(name);

        if (IgnoreRaycastLayerValue < 0)
        {
            Debug.LogError($"Could not get layer value for layer name: {name}");
        }
    }

    internal class SessionData
    {
        public string Name { get; set; } = string.Empty;

        public List<int> OrderOfScenes { get; set; } = null!;

        public List<UserStudyTest> OrderOfTests { get; set; } = null!;

        public List<UserStudyTask> OrderOfTasks { get; set; } = null!;

        public OrderedDict<string, SceneData> SceneResults { get; set; } = null!;
    }

    internal class SceneData
    {
        public UserStudyTest Test { get; set; } = default;

        public UserStudyTask Task { get; set; } = default;

        public double CompletionTime { get; set; } = 0.0;

        public bool DidInteractWithConsole { get; set; } = false;

        public List<TimestampPosition> PlayerTimeAtPosition { get; set; } = new();

        public List<TimestampPosition> WaypointsTimeAtPosition { get; set; } = new();

        public void Reset()
        {
            CompletionTime = 0.0;
            DidInteractWithConsole = false;
            PlayerTimeAtPosition.Clear();
            WaypointsTimeAtPosition.Clear();
        }
    }
}
