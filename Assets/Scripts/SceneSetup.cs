using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

#nullable enable

public class SceneSetup : MonoBehaviour
{
    private readonly Dictionary<string, InputActionMap> cachedActionMaps = new();

    private readonly Dictionary<Type, Component> cachedComponents = new();

    [SerializeField]
    private InputActionAsset defaulInputActions = null!;

    [SerializeField]
    private bool restartScene = false;

    [SerializeField]
    private bool skipToNextScene = false;

    [SerializeField]
    private bool playHitWallSound = false;

    private bool ranUpdateOnce = false;

    private GameObject[] regionObjects = null!;

    private Camera? cachedCamera = null;

    private FadeScreen fadeScreen = null!;

    private System.Diagnostics.Stopwatch sw = null!;

    private AudioSource audioSource = null!;

    public double StopAndGetElapsedSeconds()
    {
        sw.Stop();
        return sw.Elapsed.TotalSeconds;
    }

    public InputActionMap GetCachedActionMap(string nameOrId)
    {
        if (!cachedActionMaps.TryGetValue(nameOrId, out InputActionMap actionMap))
        {
            actionMap = defaulInputActions.FindActionMap(nameOrId);
            cachedActionMaps[nameOrId] = actionMap;
        }

        return actionMap;
    }

    public DiscoveredItems GetDiscoveredItems()
    {
        return GetGameObjectCachedComponent<DiscoveredItems>(VisionPulseResources.InitializerCube);
    }

    public Camera GetMainCamera()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;

            if (cachedCamera == null)
            {
                Debug.Log($"{nameof(Camera.main)} is null");
                return null!;
            }
        }

        return cachedCamera;
    }

    public FadeScreen GetFadeScreen()
    {
        return GetGameObjectCachedComponent<FadeScreen>(VisionPulseResources.FaderScreen);
    }

    public Player GetPlayer()
    {
        return GetGameObjectCachedComponent<Player>(VisionPulseResources.XROrigin);
    }

    public DiscoveryMenu GetDiscoveryMenu()
    {
        return GetGameObjectCachedComponent<DiscoveryMenu>(VisionPulseResources.InitializerCube);
    }

    public SoundSpeedMenu GetSoundSpeedMenu()
    {
        return GetGameObjectCachedComponent<SoundSpeedMenu>(VisionPulseResources.InitializerCube);
    }

    public GameObject GetRegionObject(string regionName)
    {
        GameObject? regionObject = regionObjects.FirstOrDefault(regionObject => regionObject.name == regionName);

        if (regionObject == null)
        {
            Debug.LogError($"Region object '{regionName}' not found.");
            return null!;
        }

        return regionObject;
    }

    public IEnumerable<GameObject> GetRegionObjects()
    {
        return regionObjects;
    }

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        sw = System.Diagnostics.Stopwatch.StartNew();
        regionObjects = VerifyRegionsAndSections();
        audioSource = Utils.AddComponentAudioSource(gameObject);
    }

    private void Start()
    {
        fadeScreen = VisionPulseResources.SceneSetup.GetFadeScreen();

        GameObject[] interactableObjects = GameObject.FindGameObjectsWithTag(VisionPulseResources.VisionPulseObjectTag);

        AssignInteractableObjectsToBestRegions(interactableObjects);

        if (VisionPulseResources.CurrentTest == VisionPulseResources.UserStudyTest.PREBUILT_MENU)
        {
            PopulateDiscoveryMenu(interactableObjects);
        }
    }

    private void Update()
    {
        if (ranUpdateOnce)
        {
            return;
        }

        if (restartScene)
        {
            string sceneName = VisionPulseResources.Instance.GetActiveSceneName();

            if (sceneName != VisionPulseResources.TutorialSceneName)
            {
                VisionPulseResources.Instance.ResetSceneData(sceneName);
            }

            fadeScreen.FadeOut();
            StartCoroutine(VisionPulseResources.Instance.LoadScene(sceneName, fadeScreen.GetFadeDuration()));

            ranUpdateOnce = true;
            return;
        }

        if (skipToNextScene)
        {
            string activeSceneName = VisionPulseResources.Instance.GetActiveSceneName();

            if (activeSceneName != VisionPulseResources.TutorialSceneName)
            {
                VisionPulseResources.Instance.SetCompletionTimeInSceneData(activeSceneName, -1.0);
                VisionPulseResources.Instance.WriteOutSessionData();
            }

            int? nextScene = VisionPulseResources.Instance.SetupNextSceneAndGetNumber();

            if (nextScene == null)
            {
                Debug.Log("We are on the last scene");
                ranUpdateOnce = true;
                return;
            }

            string sceneName = (nextScene == -1) ? VisionPulseResources.TutorialSceneName : $"UserStudyScene{nextScene}";

            fadeScreen.FadeOut();
            StartCoroutine(VisionPulseResources.Instance.LoadScene(sceneName, fadeScreen.GetFadeDuration()));

            ranUpdateOnce = true;
            return;
        }

        if (playHitWallSound)
        {
            AudioClip hitWallEffect = VisionPulseResources.Instance.GetEffectAudioClip("Hit Wall");
            audioSource.PlayOneShot(hitWallEffect);

            playHitWallSound = false;
            return;
        }
    }

    private GameObject[] VerifyRegionsAndSections()
    {
        GameObject[] regionObjs = GameObject.FindGameObjectsWithTag(VisionPulseResources.VisionPulseRegionTag);
        bool foundError = false;

        foreach (GameObject regionObj in regionObjs)
        {
            if (!regionObj.CompareTag(VisionPulseResources.VisionPulseRegionTag))
            {
                Debug.LogError($"{regionObj.name} does not contain the tag: {VisionPulseResources.VisionPulseRegionTag}");
                foundError = true;
                continue;
            }

            if (regionObj.layer != VisionPulseResources.IgnoreRaycastLayerValue)
            {
                Debug.LogError($"{regionObj.name} does not have the layer value: {nameof(VisionPulseResources.IgnoreRaycastLayerValue)}");
                foundError = true;
                continue;
            }

            if (!regionObj.TryGetComponent(out BoxCollider regionCollider))
            {
                Debug.LogError($"{regionObj.name} does not have the Component: {Utils.ComptimeTypeName(regionCollider)}");
                foundError = true;
                continue;
            }

            if (!regionCollider.enabled)
            {
                Debug.LogError($"{regionObj.name} {Utils.ComptimeTypeName(regionCollider)} is not enabled");
                foundError = true;
                continue;
            }

            if (!regionCollider.isTrigger)
            {
                Debug.LogError($"{regionObj.name} should have {nameof(regionCollider.isTrigger)} set to true");
                foundError = true;
                continue;
            }

            foreach (Transform child in regionObj.transform)
            {
                GameObject section = child.gameObject;

                if (!section.activeSelf)
                {
                    continue;
                }

                if (!section.CompareTag(VisionPulseResources.VisionPulseSectionTag))
                {
                    Debug.LogError($"{section.name} of {regionObj.name} region does not contain the tag: {VisionPulseResources.VisionPulseSectionTag}");
                    foundError = true;
                    continue;
                }

                if (!section.TryGetComponent(out BoxCollider sectionCollider))
                {
                    Debug.LogError($"{section.name} of {regionObj.name} region does not have the Component: {Utils.ComptimeTypeName(sectionCollider)}");
                    foundError = true;
                    continue;
                }

                if (!sectionCollider.enabled)
                {
                    Debug.LogError($"{section.name} of {regionObj.name} region {Utils.ComptimeTypeName(sectionCollider)} is not enabled");
                    foundError = true;
                    continue;
                }

                if (!sectionCollider.isTrigger)
                {
                    Debug.LogError($"{section.name} of {regionObj.name} region should have {nameof(sectionCollider.isTrigger)} set to true");
                    foundError = true;
                    continue;
                }
            }
        }

        return foundError ? null! : regionObjs;
    }

    private void AssignInteractableObjectsToBestRegions(GameObject[] interactableObjects)
    {
        foreach (GameObject interactableObject in interactableObjects)
        {
            Region bestRegion = Utils.GetRegionScript(regionObjects[0]);
            float bestIntersectionVolume = bestRegion.GetIntersectionVolume(interactableObject);

            for (int i = 1; i < regionObjects.Length; i++)
            {
                Region region = Utils.GetRegionScript(regionObjects[i]);
                float intersectionVolume = region.GetIntersectionVolume(interactableObject);

                if (intersectionVolume > bestIntersectionVolume)
                {
                    bestRegion = region;
                    bestIntersectionVolume = intersectionVolume;
                }
            }

            bestRegion.AddInteractableObject(interactableObject);
        }
    }

    private T GetGameObjectCachedComponent<T>(string nameOfObject)
        where T : Component
    {
        Type type = typeof(T);

        if (cachedComponents.TryGetValue(type, out Component existing))
        {
            if (existing.name != nameOfObject)
            {
                Debug.LogError($"Component '{typeof(T).Name}' was cached from GameObject '{existing.name}', but requested from '{nameOfObject}'");
                return default!;
            }

            return (T)existing;
        }

        T resolved = Utils.FindGameObjectAndGetComponent<T>(nameOfObject);
        cachedComponents[type] = resolved;
        return resolved;
    }

    private void PopulateDiscoveryMenu(GameObject[] interactableObjects)
    {
        DiscoveryMenu discoveryMenu = GetDiscoveryMenu();

        foreach (GameObject regionObject in regionObjects)
        {
            discoveryMenu.RecordRegion(regionObject.name);

            foreach (Transform child in regionObject.transform)
            {
                GameObject section = child.gameObject;
                section.SetActive(false);
                Destroy(section);
            }
        }

        foreach (GameObject interactableObject in interactableObjects)
        {
            discoveryMenu.RecordObject(interactableObject);
        }
    }
}
