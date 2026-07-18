using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#nullable enable

public class Player : MonoBehaviour
{
    public const float WaypointReachThresholdCamera = 0.80f;

    public const float WaypointReachThresholdController = 0.50f;

    public const float VibrationDetectionThreshold = WaypointReachThresholdController + 1.50f;

    private const float HeightOfCharacterController = 1.36144f;

    private const float GroundCheckDistance = HeightOfCharacterController + (HeightOfCharacterController / 2);

    private const float SpawnDelay = 2.0f;

    private const float RecordPositionInterval = 0.25f;

    private static readonly string[] LocomotionAndInteractionActionMapNames = { "XRI Left Locomotion Custom", "XRI Left Interaction Persistent", "XRI Right Locomotion Custom", "XRI Right Interaction Persistent" };

    [SerializeField]
    private GameObject leftController = null!;

    [SerializeField]
    private GameObject rightController = null!;

    private GameObject waypointPrefab = null!;

    private AudioSource walkingAudioSource = null!;

    private Queue<(Vector3, Material)> waypoints = new();

    private GenerateWaypoints generateWaypoints = null!;

    private ControllerHaptics leftControllerHaptics = null!;

    private ControllerHaptics rightControllerHaptics = null!;

    private GameObject? currentWaypoint = null;

    private GameObject? targetObject = null;

    private InputAction xriLeftInteractionPersistentToggleDiscoveryMenu = null!;

    private InputAction xriRightInteractionPersistentSoundSpeedMenu = null!;

    private SoundQueue soundQueue = null!;

    private PlanesCast planesCast = null!;

    private CoroutineHandler reachedWaypointCoroutine = null!;

    private float spawnTimer = SpawnDelay;

    private int numberOfRegionsSeen = 0;

    private int numberOfObjectsSeen = 0;

    private InputAction xriLeftLocomotionCustomMove = null!;

    private Terrain? terrain = null;

    private TerrainData? terrainData = null;

    private Vector3 terrainPos = Vector3.zero;

    private Camera xrCamera = null!;

    private FadeScreen fadeScreen = null!;

    private DiscoveryMenu discoveryMenu = null!;

    private SoundSpeedMenu soundSpeedMenu = null!;

    private bool shouldEnableActionMapsOnce = true;

    private (string, AudioClip)? regionDescription = null;

    private AudioClip discoveredRegionSoundEffectClip = null!;

    private string activeSceneName = string.Empty;

    private float nextRecordTime = 0.0f;

    public int InventoryCount { get; private set; } = 0;

    public bool IsReachedWaypointCoroutineRunning => reachedWaypointCoroutine.IsRunning;

    public void SetLocomotionAndInteractionActionMaps(bool enable)
    {
        foreach (string mapName in LocomotionAndInteractionActionMapNames)
        {
            InputActionMap actionMap = VisionPulseResources.SceneSetup.GetCachedActionMap(mapName);

            if (enable)
            {
                actionMap.Enable();
            }
            else
            {
                actionMap.Disable();
            }
        }
    }

    public void StartNavigationToTargetObject(GameObject obj)
    {
        DestroyCurrentWaypoint();

        (List<(Vector3, Material)> newWaypoints, _) = generateWaypoints.ToObject(transform.position, obj.transform.position);
        StartNavigationWithWaypoints(newWaypoints);

        targetObject = obj;

        if (currentWaypoint == null)
        {
            StartLoopingHumOnTargetObject();
        }
    }

    public void StartNavigationWithWaypoints(List<(Vector3, Material)> newWaypoints)
    {
        regionDescription = null;

        if (targetObject != null)
        {
            AudioSource humAudioSource = Utils.GetHumAudioSource(targetObject);
            humAudioSource.Stop();
            targetObject = null;
        }

        waypoints = new(newWaypoints);

        if (!waypoints.IsEmpty())
        {
            (Vector3 position, Material material) = waypoints.Dequeue();
            UpdateWaypointPosition(ref position);

            SpawnWaypoint(position, material);
        }

        numberOfRegionsSeen = 0;
        numberOfObjectsSeen = 0;
    }

    public void DestroyCurrentWaypoint()
    {
        if (currentWaypoint == null)
        {
            return;
        }

        // The destruction happens at the end of the frame so lets set the active state to false just in case
        currentWaypoint.SetActive(false);
        Destroy(currentWaypoint);
        currentWaypoint = null;
    }

    public bool FoundTargetObjectIfMatch(GameObject obj)
    {
        if (targetObject == null || obj != targetObject)
        {
            return false;
        }

        DestroyCurrentWaypoint();
        waypoints.Clear();
        targetObject = null;

        return true;
    }

    public void AddObjectToInventory(GameObject obj)
    {
        if (VisionPulseResources.IsUserStudy)
        {
            Destroy(obj);
        }

        InventoryCount = Math.Min(InventoryCount + 1, VisionPulseResources.NumberOfKeysToCompleteScene);
    }

    public void SetRegionDescription(string name, AudioClip descriptionClip)
    {
        if (!VisionPulseResources.IsUserStudy)
        {
            return;
        }

        regionDescription = new(name, descriptionClip);
    }

    public void StopSoundQueue()
    {
        soundQueue.Stop();
    }

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        leftControllerHaptics = leftController.GetComponent<ControllerHaptics>();
        rightControllerHaptics = rightController.GetComponent<ControllerHaptics>();

        soundQueue = gameObject.AddComponent<SoundQueue>();

        walkingAudioSource = Utils.AddComponentAudioSource(gameObject);
        walkingAudioSource.volume = 0.20f;

        generateWaypoints = gameObject.AddComponent<GenerateWaypoints>();

        planesCast = gameObject.AddComponent<PlanesCast>();

        waypointPrefab = Resources.Load<GameObject>("Prefabs/Waypoint");

        reachedWaypointCoroutine = new(this);

        terrain = Terrain.activeTerrain;

        if (terrain != null)
        {
            terrainData = terrain.terrainData;
            terrainPos = terrain.transform.position;
        }
    }

    private void Start()
    {
        InputActionMap xriLeftInteractionPersistent = VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Interaction Persistent");
        xriLeftInteractionPersistentToggleDiscoveryMenu = xriLeftInteractionPersistent.FindAction("ToggleDiscoveryMenu");
        xriLeftInteractionPersistentToggleDiscoveryMenu.performed += ToggleDiscoveryMenu;

        InputActionMap xriLeftLocomotionCustom = VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Locomotion Custom");
        xriLeftLocomotionCustomMove = xriLeftLocomotionCustom.FindAction("Move");
        xriLeftLocomotionCustomMove.Enable();

        InputActionMap xriRightInteractionPersistent = VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Interaction Persistent");
        xriRightInteractionPersistentSoundSpeedMenu = xriRightInteractionPersistent.FindAction("ToggleSoundSpeedMenu");
        xriRightInteractionPersistentSoundSpeedMenu.performed += ToggleSoundSpeedMenu;

        SetLocomotionAndInteractionActionMaps(false);

        xrCamera = VisionPulseResources.SceneSetup.GetMainCamera();
        fadeScreen = VisionPulseResources.SceneSetup.GetFadeScreen();
        discoveryMenu = VisionPulseResources.SceneSetup.GetDiscoveryMenu();
        soundSpeedMenu = VisionPulseResources.SceneSetup.GetSoundSpeedMenu();

        discoveredRegionSoundEffectClip = VisionPulseResources.Instance.GetEffectAudioClip(VisionPulseResources.DiscoveredRegionSoundEffect);

        activeSceneName = VisionPulseResources.Instance.GetActiveSceneName();
    }

    private void Update()
    {
        if (fadeScreen.IsRunning())
        {
            SetLocomotionAndInteractionActionMaps(false);
            return;
        }

        if (shouldEnableActionMapsOnce)
        {
            SetLocomotionAndInteractionActionMaps(true);
            shouldEnableActionMapsOnce = false;
        }

        if (Time.timeSinceLevelLoad >= nextRecordTime && activeSceneName != VisionPulseResources.TutorialSceneName && IsWalking())
        {
            nextRecordTime = Time.timeSinceLevelLoad + RecordPositionInterval;

            VisionPulseResources.Instance.AddPlayerTimeAtPosition(activeSceneName, new TimestampPosition(Time.timeSinceLevelLoad, gameObject.transform.position));
        }

        PlayWalkingSoundEffects();

        if (VisionPulseResources.CurrentTest == VisionPulseResources.UserStudyTest.DISCOVERY)
        {
            ScanScene();
        }

        if (reachedWaypointCoroutine.IsRunning)
        {
            return;
        }

        if (currentWaypoint != null)
        {
            float minDistanceFromControllerToWaypoint = AdjustControllerHaptics(currentWaypoint);
            float distanceFromCameraToWaypoint = AdjustObjectHumVolumeAndPitch(currentWaypoint);

            if (minDistanceFromControllerToWaypoint <= WaypointReachThresholdController
                || distanceFromCameraToWaypoint <= WaypointReachThresholdCamera)
            {
                DestroyCurrentWaypoint();

                if (waypoints.IsEmpty())
                {
                    reachedWaypointCoroutine.Start(PlayReachedWaypointSoundNumberOfItemsSeenSoundAndStartHumOnTargetObjectSound());
                }
                else
                {
                    reachedWaypointCoroutine.Start(PlayReachedWaypointSoundAndSpawnNextWaypoint());
                }
            }
        }
        else if (targetObject != null)
        {
            AdjustControllerHaptics(targetObject);
            AdjustObjectHumVolumeAndPitch(targetObject);
        }
        else if (regionDescription != null && VisionPulseResources.IsUserStudy)
        {
            AudioClip enteredRegionClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip("Entered Region");
            AudioClip regionNameClip = VisionPulseResources.Instance.GetTTSObjectAudioClip(regionDescription.Value.Item1);
            AudioClip descriptionClip = regionDescription.Value.Item2;

            if (IsDiscoveredRegionSoundEffectPlayingOrInQueue())
            {
                soundQueue.StopAndEnqueue(enteredRegionClip, regionNameClip, descriptionClip, discoveredRegionSoundEffectClip);
            }
            else
            {
                soundQueue.StopAndEnqueue(enteredRegionClip, regionNameClip, descriptionClip);
            }

            regionDescription = null;
        }
    }

    private void OnDestroy()
    {
        xriLeftInteractionPersistentToggleDiscoveryMenu.performed -= ToggleDiscoveryMenu;
        xriRightInteractionPersistentSoundSpeedMenu.performed -= ToggleSoundSpeedMenu;
    }

    private void PlayWalkingSoundEffects()
    {
        if (walkingAudioSource.isPlaying || !IsWalking())
        {
            return;
        }

        if (terrain == null)
        {
            AudioClip runningClip;

            if (Physics.Raycast(xrCamera.transform.position, Vector3.down, out RaycastHit hit, 4.0f)
                && hit.collider.CompareTag("VisionPulseWood"))
            {
                runningClip = VisionPulseResources.Instance.GetRandomRunningOnWoodClip();
            }
            else
            {
                runningClip = VisionPulseResources.Instance.GetRandomRunningOnMetalClip();
            }

            walkingAudioSource.PlayOneShot(runningClip);
            return;
        }

        int textureIndex = GetMainTexture(transform.position);
        AudioClip runningOnTerrainClip = (textureIndex == 1) ? VisionPulseResources.Instance.GetRandomRunningOnDirtClip() : VisionPulseResources.Instance.GetRandomRunningOnGrassClip();

        walkingAudioSource.PlayOneShot(runningOnTerrainClip);
    }

    private bool IsWalking()
    {
        Vector2 thumbstickInput = xriLeftLocomotionCustomMove.ReadValue<Vector2>();
        return thumbstickInput.magnitude > 0.1f;
    }

    private bool IsInNavigationMode()
    {
        return !waypoints.IsEmpty() || currentWaypoint != null || reachedWaypointCoroutine.IsRunning || targetObject != null;
    }

    private void ScanScene()
    {
        spawnTimer += Time.deltaTime;

        Vector2 thumbstickInput = xriLeftLocomotionCustomMove.ReadValue<Vector2>();

        if (thumbstickInput == Vector2.zero && spawnTimer < SpawnDelay)
        {
            return;
        }

        if (planesCast.LookForUnexploredSection())
        {
            numberOfRegionsSeen++;

            if (!IsInNavigationMode()
                && !IsDiscoveredRegionSoundEffectPlayingOrInQueue()
                && !discoveryMenu.IsOpen
                && !soundSpeedMenu.IsOpen
                && !VisionPulseResources.Instance.IsTutorialScene())
            {
                soundQueue.Enqueue(discoveredRegionSoundEffectClip);
            }
        }

        List<string> discoveredObjectNames = planesCast.LookForDiscoverableObjects();

        foreach (string discoveredObjectName in discoveredObjectNames)
        {
            numberOfObjectsSeen++;

            if (!IsInNavigationMode()
                && !discoveryMenu.IsOpen
                && !soundSpeedMenu.IsOpen
                && !VisionPulseResources.Instance.IsTutorialScene())
            {
                AudioClip objectClip = VisionPulseResources.Instance.GetTTSObjectAudioClip(discoveredObjectName);
                soundQueue.Enqueue(objectClip);
            }
        }

        spawnTimer = 0.0f;
    }

    private float AdjustControllerHaptics(GameObject obj)
    {
        float leftControllerDistanceFromWaypoint = Vector3.Distance(leftController.transform.position, obj.transform.position);
        float rightControllerDistanceFromWaypoint = Vector3.Distance(rightController.transform.position, obj.transform.position);

        if (VisionPulseResources.CurrentTask == VisionPulseResources.UserStudyTask.VISIONPULSE_HAPTICS_AUDIOBEACON)
        {
            AdjustControllerHaptics(obj.transform.position, leftControllerDistanceFromWaypoint, leftController, leftControllerHaptics);
            AdjustControllerHaptics(obj.transform.position, rightControllerDistanceFromWaypoint, rightController, rightControllerHaptics);
        }

        return Mathf.Min(leftControllerDistanceFromWaypoint, rightControllerDistanceFromWaypoint);
    }

    private bool IsDiscoveredRegionSoundEffectPlayingOrInQueue()
    {
        if (soundQueue.IsAudioClipInQueue(discoveredRegionSoundEffectClip))
        {
            return true;
        }

        return soundQueue.GetPlayingAudioClip() == discoveredRegionSoundEffectClip;
    }

    private IEnumerator PlayReachedWaypointSoundAndSpawnNextWaypoint()
    {
        yield return PlayReachedWaypointSound();

        if (waypoints.IsEmpty())
        {
            yield break;
        }

        (Vector3 position, Material material) = waypoints.Dequeue();
        UpdateWaypointPosition(ref position);

        AudioClip remainingClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Remaining");
        AudioClip numberClip = VisionPulseResources.Instance.GetTTSNumberAudioClip(waypoints.Count + 1);

        soundQueue.Enqueue(remainingClip, numberClip);

        yield return new WaitForSeconds(remainingClip.length + numberClip.length);

        SpawnWaypoint(position, material);
    }

    private IEnumerator PlayReachedWaypointSoundNumberOfItemsSeenSoundAndStartHumOnTargetObjectSound(bool playReachedWaypoint = true)
    {
        if (playReachedWaypoint)
        {
            yield return PlayReachedWaypointSound();
        }

        float totalTime = 0.0f;

        if (regionDescription != null && VisionPulseResources.IsUserStudy)
        {
            AudioClip enteredRegionClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip("Entered Region");
            AudioClip regionNameClip = VisionPulseResources.Instance.GetTTSObjectAudioClip(regionDescription.Value.Item1);
            AudioClip descriptionClip = regionDescription.Value.Item2;

            soundQueue.Enqueue(enteredRegionClip, regionNameClip, descriptionClip);

            totalTime += enteredRegionClip.length + regionNameClip.length + descriptionClip.length;

            regionDescription = null;
        }

        if (numberOfRegionsSeen > 0 || numberOfObjectsSeen > 0)
        {
            AudioClip duringNavigationClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("During Navigation");
            soundQueue.Enqueue(duringNavigationClip);

            totalTime += duringNavigationClip.length;
        }

        float regionsSeenWaitTime = PlayNumberOfItemsSeenSound(numberOfRegionsSeen, "Number of Regions Seen");
        float objectsSeenWaitTime = PlayNumberOfItemsSeenSound(numberOfObjectsSeen, "Number of Objects Seen");

        totalTime += regionsSeenWaitTime + objectsSeenWaitTime;

        yield return new WaitForSeconds(totalTime);

        StartLoopingHumOnTargetObject();
    }

    private float PlayNumberOfItemsSeenSound(int numberOfItemsSeen, string itemDescription)
    {
        if (numberOfItemsSeen <= 0)
        {
            return 0.0f;
        }

        AudioClip numberOfItemsSeenClip = VisionPulseResources.Instance.GetTTSMiscAudioClip(itemDescription);
        AudioClip numberClip = VisionPulseResources.Instance.GetTTSNumberAudioClip(numberOfItemsSeen);

        soundQueue.Enqueue(numberOfItemsSeenClip, numberClip);

        return numberOfItemsSeenClip.length + numberClip.length;
    }

    private IEnumerator PlayReachedWaypointSound()
    {
        AudioClip reachedWaypointClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Reached Waypoint");
        soundQueue.Enqueue(reachedWaypointClip);

        yield return new WaitForSeconds(reachedWaypointClip.length);
    }

    private void StartLoopingHumOnTargetObject()
    {
        if (targetObject == null)
        {
            return;
        }

        AudioSource humAudioSource = Utils.GetHumAudioSource(targetObject);

        if (!humAudioSource.isPlaying)
        {
            humAudioSource.loop = true;
            humAudioSource.Play();
        }
    }

    private void SpawnWaypoint(Vector3 position, Material material)
    {
        GameObject? waypointObject = Instantiate(waypointPrefab, position, Quaternion.identity);

        if (waypointObject == null)
        {
            return;
        }

        if (activeSceneName != VisionPulseResources.TutorialSceneName)
        {
            VisionPulseResources.Instance.AddWaypointTimeAtPosition(activeSceneName, new TimestampPosition(Time.timeSinceLevelLoad, position));
        }

        if (waypointObject.TryGetComponent(out Renderer renderer))
        {
            renderer.material = material;
        }

        currentWaypoint = waypointObject;
    }

    private void UpdateWaypointPosition(ref Vector3 position)
    {
        // The waypoint is going to spawn in the ground so let's
        // automatically bump it up
        position.y += waypointPrefab.transform.localScale.y / 2f;

        float waypointHeightAdjustment = waypointPrefab.transform.localScale.y / 4f;

        if (Physics.Raycast(xrCamera.transform.position, Vector3.down, out RaycastHit camHit, GroundCheckDistance)
            && Physics.Raycast(position, Vector3.down, out RaycastHit posHit, GroundCheckDistance))
        {
            float groundDiff = Mathf.Abs(camHit.point.y - posHit.point.y);

            // If the player and the waypoint are at the same elevation
            // make the waypoint roughly eye level
            if (groundDiff < 0.1f)
            {
                position.y = xrCamera.transform.position.y - waypointHeightAdjustment;
                return;
            }
        }

        for (int i = 0; i < 10; i++)
        {
            // I don't want to raise it too high
            if (!Physics.Raycast(position, Vector3.down, HeightOfCharacterController))
            {
                break;
            }

            position.y += waypointHeightAdjustment;

            // Let's check up as well just to make sure we aren't hitting something above us
            if (Physics.Raycast(position, Vector3.up, HeightOfCharacterController))
            {
                break;
            }
        }
    }

    private void ToggleDiscoveryMenu(InputAction.CallbackContext context)
    {
        discoveryMenu.HandleMenuNavigation();
    }

    private void ToggleSoundSpeedMenu(InputAction.CallbackContext context)
    {
        soundSpeedMenu.HandleMenuNavigation();
    }

    private float AdjustObjectHumVolumeAndPitch(GameObject obj)
    {
        AudioSource humAudioSource = Utils.GetHumAudioSource(obj);
        float distance = Vector3.Distance(xrCamera.transform.position, obj.transform.position);

        if (!humAudioSource.isPlaying || VisionPulseResources.CurrentTask == VisionPulseResources.UserStudyTask.STANDARD_AUDIOBEACON)
        {
            return distance;
        }

        Vector3 toWaypoint = obj.transform.position - xrCamera.transform.position;
        Vector3 directionToObject = toWaypoint.normalized;

        if (distance <= VibrationDetectionThreshold)
        {
            // Ignore vertical (Y-axis) when close
            directionToObject = new Vector3(toWaypoint.x, 0, toWaypoint.z).normalized;
            Vector3 cameraForwardXZ = new Vector3(xrCamera.transform.forward.x, 0, xrCamera.transform.forward.z).normalized;
            SetAudioBasedOnAngle(humAudioSource, Vector3.Angle(cameraForwardXZ, directionToObject));
        }
        else
        {
            // Use full 3D direction when farther
            SetAudioBasedOnAngle(humAudioSource, Vector3.Angle(xrCamera.transform.forward, directionToObject));
        }

        return distance;
    }

    private void SetAudioBasedOnAngle(AudioSource source, float angle)
    {
        float volume = 0.2f;
        float pitch = 0.8f;

        if (angle <= 10f)
        {
            volume = 1f;
            pitch = 1.2f;
        }
        else if (angle <= 45f)
        {
            volume = 0.5f;
            pitch = 1.1f;
        }
        else if (angle <= 90f)
        {
            volume = 0.3f;
            pitch = 1.0f;
        }

        source.volume = volume;
        source.pitch = pitch;
    }

    private void AdjustControllerHaptics(Vector3 objPosition, float distance, GameObject controller, ControllerHaptics controllerHaptics)
    {
        if (distance > VibrationDetectionThreshold)
        {
            return;
        }

        Vector3 directionToObject = (objPosition - controller.transform.position).normalized;
        float dot = Vector3.Dot(controller.transform.forward, directionToObject);
        float angleThreshold = 90.0f;
        float minDotThreshold = Mathf.Cos(angleThreshold * Mathf.Deg2Rad);

        if (dot > minDotThreshold)
        {
            float distanceFactor = 1f - (distance / VibrationDetectionThreshold);
            float dotFactor = Mathf.Pow(dot, 2);
            float intensity = Mathf.Clamp01((dotFactor * 0.85f) + (distanceFactor * 0.15f));

            if (intensity > 0)
            {
                controllerHaptics.Send(intensity, 0.1f);
            }
        }
    }

    private int GetMainTexture(Vector3 worldPos)
    {
        if (terrainData == null)
        {
            return -1;
        }

        int mapX = (int)((worldPos.x - terrainPos.x) / terrainData.size.x * terrainData.alphamapWidth);
        int mapZ = (int)((worldPos.z - terrainPos.z) / terrainData.size.z * terrainData.alphamapHeight);

        float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
        float maxMix = 0f;
        int maxIndex = 0;

        for (int i = 0; i < terrainData.alphamapLayers; i++)
        {
            if (splatmapData[0, 0, i] > maxMix)
            {
                maxIndex = i;
                maxMix = splatmapData[0, 0, i];
            }
        }

        return maxIndex;
    }
}