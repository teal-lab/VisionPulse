using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#nullable enable

public class DiscoveryMenu : MonoBehaviour
{
    private const float JoystickCooldown = 0.20f;

    private const int NumberOfPreallocatedButtons = 10;

    private static readonly MainMenuState MainMenu = new();

    private readonly List<ButtonEntry> buttonEntries = new();

    private readonly MenuSortingType[] sortingTypes = { MenuSortingType.CLOSEST_DISTANCE, MenuSortingType.CLOSEST_DISTANCE };

    private readonly GameObject[] preallocatedButtons = new GameObject[NumberOfPreallocatedButtons];

    [SerializeField]
    private GameObject menu = null!;

    private ScrollRect scrollRect = null!;

    private RectTransform content = null!;

    private GameObject buttonPrefab = null!;

    private IState menuState = MainMenu;

    private int selectedButtonIndex = 0;

    private float lastInputTime = 0.0f;

    private SoundQueue soundQueue = null!;

    private InputAction xriLeftDiscoveryMenuScroll = null!;

    private InputAction xriLeftDiscoveryMenuSelect = null!;

    private InputAction xriLeftDiscoveryMenuSort = null!;

    private GenerateWaypoints generateWaypoints = null!;

    private CoroutineHandler announceSortingTypeCoroutine = null!;

    private DiscoveredItems discoveredItems = null!;

    private Camera xrCamera = null!;

    private Player player = null!;

    internal enum MenuSortingType
    {
        MOST_RECENTLY_SEEN,
        CLOSEST_DISTANCE,
    }

    internal interface IState
    {
        public IEnumerable<string> GetButtonNames(DiscoveryMenu discoveryMenu);

        public int GetIndex();

        public void Next(DiscoveryMenu discoveryMenu, object? obj);

        public void Refresh(DiscoveryMenu discoveryMenu);
    }

    public bool IsOpen { get; private set; } = false;

    public bool RecordRegion(string regionName)
    {
        if (!VisionPulseResources.Instance.ContainsTTSObjectAudioName(VisionPulseResources.UndiscoveredSection))
        {
            Debug.LogError($"Could not find audio file: {VisionPulseResources.UndiscoveredSection}");
            return false;
        }

        EnsureRegionExists(regionName);
        return true;
    }

    public bool RecordObject(GameObject obj)
    {
        if (!VisionPulseResources.Instance.ContainsTTSObjectAudioName(obj.name))
        {
            Debug.LogError($"Could not find audio file: {obj.name}");
            return false;
        }

        if (!obj.CompareTag(VisionPulseResources.VisionPulseObjectTag))
        {
            return false;
        }

        // RecordObject might be called by SceneSetup's Start before
        // DiscoveryMenu's Start so we need to double check here
        if (discoveredItems == null)
        {
            discoveredItems = VisionPulseResources.SceneSetup.GetDiscoveredItems();
        }

        if (discoveredItems.ContainsInteractableObjectName(obj.name))
        {
            return false;
        }

        if (sortingTypes[menuState.GetIndex()] == MenuSortingType.MOST_RECENTLY_SEEN)
        {
            selectedButtonIndex = 0;
        }

        discoveredItems.AddInteractableObjectName(obj.name);
        return true;
    }

    public void HandleMenuNavigation()
    {
        if (player.IsReachedWaypointCoroutineRunning)
        {
            return;
        }

        // Let's open up the menu for the first time
        if (!IsOpen)
        {
            menuState.Refresh(this);
            Toggle();
            return;
        }

        // We are now going to close the menu
        if (menuState is MainMenuState)
        {
            ExitMenu();
            return;
        }

        if (menuState is RegionMenuState regionMenu)
        {
            regionMenu.Prev(this);
            ScrollToSelected();
            HighlightItem();
        }
    }

    public void DisableMenuActions()
    {
        xriLeftDiscoveryMenuScroll.Disable();
        xriLeftDiscoveryMenuSelect.performed -= OnSelectButtonClicked;
    }

    public void EnableMenuActions()
    {
        xriLeftDiscoveryMenuScroll.Enable();
        xriLeftDiscoveryMenuSelect.performed += OnSelectButtonClicked;
    }

    public void HighlightItem()
    {
        if (buttonEntries.IsEmpty())
        {
            return;
        }

        string objectName = buttonEntries[selectedButtonIndex].GetButtonText();
        AudioClip selectedMenuItemClip = VisionPulseResources.Instance.GetEffectAudioClip("Selected Menu Item");
        AudioClip objectClip = VisionPulseResources.Instance.GetTTSObjectAudioClip(objectName);

        soundQueue.StopAndEnqueue(selectedMenuItemClip, objectClip);

        if (menuState is MainMenuState)
        {
            Region region = discoveredItems.GetRegion(objectName);

            if (region.HasUndiscovedSections())
            {
                AudioClip notFullyExploredClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Not Fully Explored");
                soundQueue.Enqueue(notFullyExploredClip);

                if (region.HasDiscoveredInteractableObjects())
                {
                    SoundQueueObjectsFound(region);
                }
            }
            else if (region.HasDiscoveredInteractableObjects())
            {
                SoundQueueObjectsFound(region);
            }
            else
            {
                AudioClip emptyClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Empty");
                soundQueue.Enqueue(emptyClip);
            }
        }

        EventSystem.current.SetSelectedGameObject(buttonEntries[selectedButtonIndex].Button.gameObject);
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
        ScrollRect scrollRect = menu.GetComponentInChildren<ScrollRect>();

        if (scrollRect == null)
        {
            Debug.LogError($"GameObject '{menu.name}' does not have a child component of type: {Utils.ComptimeTypeName(scrollRect)}.");
            return;
        }

        Transform contentTransform = scrollRect.transform.Find("Viewport/Content");

        if (contentTransform == null)
        {
            Debug.LogError($"GameObject '{scrollRect.name}' does not have a child component of type: {Utils.ComptimeTypeName(contentTransform)}.");
            return;
        }

        if (!contentTransform.TryGetComponent(out RectTransform content))
        {
            Debug.LogError($"GameObject '{contentTransform.name}' does not have a component of type: {Utils.ComptimeTypeName(content)}.");
            return;
        }

        this.scrollRect = scrollRect;
        this.content = content;

        buttonPrefab = Resources.Load<GameObject>("Prefabs/Button");

        soundQueue = gameObject.AddComponent<SoundQueue>();

        generateWaypoints = gameObject.AddComponent<GenerateWaypoints>();

        announceSortingTypeCoroutine = new(this);

        for (int i = 0; i < NumberOfPreallocatedButtons; i++)
        {
            preallocatedButtons[i] = Instantiate(buttonPrefab, Vector3.zero, Quaternion.identity);
            preallocatedButtons[i].transform.SetParent(this.content, false);
            preallocatedButtons[i].transform.SetSiblingIndex(0);
            preallocatedButtons[i].transform.localScale = Vector3.one;
            preallocatedButtons[i].transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(Vector3.zero));
            preallocatedButtons[i].SetActive(false);
        }
    }

    private void Start()
    {
        InputActionMap xriLeftDiscoveryMenu = VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Discovery Menu");

        xriLeftDiscoveryMenuScroll = xriLeftDiscoveryMenu.FindAction("Scroll");
        xriLeftDiscoveryMenuScroll.Enable();

        xriLeftDiscoveryMenuSelect = xriLeftDiscoveryMenu.FindAction("Select");
        xriLeftDiscoveryMenuSelect.performed += OnSelectButtonClicked;

        xriLeftDiscoveryMenuSort = xriLeftDiscoveryMenu.FindAction("Sort");

        // Uncomment the line below to allow sorting for the menus
        // xriLeftDiscoveryMenuSort.performed += OnTriggerSqueezed;
        discoveredItems = VisionPulseResources.SceneSetup.GetDiscoveredItems();
        xrCamera = VisionPulseResources.SceneSetup.GetMainCamera();
        player = VisionPulseResources.SceneSetup.GetPlayer();

        foreach (GameObject regionObject in VisionPulseResources.SceneSetup.GetRegionObjects())
        {
            // So... this helps with making the menu not lag when we open it for the first time.
            // Idk why but it just does...
            _ = generateWaypoints.ToObject(player.transform.position, regionObject.transform.position);
        }
    }

    private void OnDestroy()
    {
        xriLeftDiscoveryMenuSelect.performed -= OnSelectButtonClicked;
        xriLeftDiscoveryMenuSort.performed -= OnTriggerSqueezed;
    }

    private void Update()
    {
        if (!menu.activeSelf
            || buttonEntries.IsEmpty()
            || Time.time - lastInputTime < JoystickCooldown
            || announceSortingTypeCoroutine.IsRunning)
        {
            return;
        }

        AudioClip? playingAudioClip = soundQueue.GetPlayingAudioClip();

        // I wanna make sure that the SelectedMenuItem sound plays
        // out in it's entirety before the player moves on to the next object
        if (playingAudioClip != null && playingAudioClip.name == VisionPulseResources.SelectedMenuItemSoundEffect)
        {
            return;
        }

        Vector2 thumbstickInput = xriLeftDiscoveryMenuScroll.ReadValue<Vector2>();

        if (thumbstickInput.y >= 0.01f)
        {
            selectedButtonIndex = (selectedButtonIndex - 1 + buttonEntries.Count) % buttonEntries.Count;
            ScrollToSelected();
            HighlightItem();
        }
        else if (thumbstickInput.y <= -0.01f)
        {
            selectedButtonIndex = (selectedButtonIndex + 1) % buttonEntries.Count;
            ScrollToSelected();
            HighlightItem();
        }

        lastInputTime = Time.time;
    }

    private void EnsureRegionExists(string regionName)
    {
        // EnsureRegionExists might be called by SceneSetup's Start before
        // DiscoveryMenu's Start so we need to double check here
        if (discoveredItems == null)
        {
            discoveredItems = VisionPulseResources.SceneSetup.GetDiscoveredItems();
        }

        if (!discoveredItems.ContainsRegion(regionName))
        {
            GameObject regionObject = VisionPulseResources.SceneSetup.GetRegionObject(regionName);
            Region region = Utils.GetRegionScript(regionObject);

            discoveredItems.AddRegion(regionName, region);
        }
    }

    private void Toggle()
    {
        IsOpen = !IsOpen;

        if (IsOpen)
        {
            player.StopSoundQueue();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Locomotion Custom").Disable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Locomotion Custom").Disable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Interaction Persistent").Disable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Discovery Menu").Enable();
            PositionMenu();
        }
        else
        {
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Locomotion Custom").Enable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Locomotion Custom").Enable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Interaction Persistent").Enable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Discovery Menu").Disable();
            soundQueue.Stop();
        }

        menu.SetActive(IsOpen);

        if (IsOpen)
        {
            ScrollToSelected();
            HighlightItem();
        }
    }

    private void PositionMenu()
    {
        Vector3 forwardOffset = xrCamera.transform.forward * 2.0f;
        Vector3 menuPosition = xrCamera.transform.position + forwardOffset;

        menuPosition.y -= 0.15f;

        menu.transform.position = menuPosition;
        menu.transform.LookAt(xrCamera.transform);
        menu.transform.rotation = Quaternion.Euler(0, xrCamera.transform.eulerAngles.y, 0);
    }

    private void ScrollToSelected()
    {
        if (buttonEntries.Count == 0 || !buttonEntries[selectedButtonIndex].Button.gameObject.activeSelf)
        {
            return;
        }

        RectTransform contentRect = scrollRect.content;
        RectTransform selectedItem = buttonEntries[selectedButtonIndex].Button.GetComponent<RectTransform>();
        RectTransform viewportRect = scrollRect.viewport;

        float contentHeight = contentRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        float itemHeight = selectedItem.rect.height;
        float itemY = Mathf.Abs(selectedItem.anchoredPosition.y);

        float minVisibleY = itemY - (itemHeight * 0.5f);
        float normalizedPosition = Mathf.Clamp01((minVisibleY - (viewportHeight * 0.5f)) / (contentHeight - viewportHeight));
        scrollRect.verticalNormalizedPosition = 1 - normalizedPosition;
    }

    private void SoundQueueObjectsFound(Region region)
    {
        AudioClip discoveredObjectsClips = VisionPulseResources.Instance.GetTTSMiscAudioClip("Discovered Objects");
        AudioClip numberClip = VisionPulseResources.Instance.GetTTSNumberAudioClip(region.GetDiscoveredInteractableObjectsCount());

        soundQueue.Enqueue(discoveredObjectsClips, numberClip);
    }

    private void OnSelectButtonClicked(InputAction.CallbackContext context)
    {
        if (buttonEntries.IsEmpty() || !IsOpen)
        {
            return;
        }

        string buttonText = buttonEntries[selectedButtonIndex].GetButtonText();

        if (menuState is MainMenuState)
        {
            Region region = discoveredItems.GetRegion(buttonText);

            if (region.IsEmpty())
            {
                return;
            }

            menuState.Next(this, buttonText);
            ScrollToSelected();
            HighlightItem();
        }
        else if (menuState is RegionMenuState regionMenu)
        {
            Region region = discoveredItems.GetRegion(regionMenu.Name);

            if (buttonText == VisionPulseResources.UndiscoveredSection)
            {
                player.DestroyCurrentWaypoint();
                List<(Vector3, Material)> waypoints = region.GetWaypointsToClosestUndiscovedSection(generateWaypoints, player);
                player.StartNavigationWithWaypoints(waypoints);
            }
            else
            {
                GameObject obj = region.GetDiscoveredInteractableObject(buttonText);
                player.StartNavigationToTargetObject(obj);
            }

            regionMenu.Next(this);

            // We just selected an item, so turn off the menu
            ExitMenu();
        }
    }

    private void OnTriggerSqueezed(InputAction.CallbackContext context)
    {
        if (!menu.activeSelf || buttonEntries.Count == 0)
        {
            return;
        }

        announceSortingTypeCoroutine.RestartWith(AnnounceSortingTypeAndSortButtons());
    }

    private IEnumerator AnnounceSortingTypeAndSortButtons()
    {
        int menuIndex = menuState.GetIndex();
        sortingTypes[menuIndex] = Utils.GetNextEnumValue(sortingTypes[menuIndex]);

        string clipName = menuState is MainMenuState ? "Sorting Regions By" : "Sorting Objects By";
        AudioClip sortingByWhatClip = VisionPulseResources.Instance.GetTTSMiscAudioClip(clipName);
        float totalTime = sortingByWhatClip.length;

        soundQueue.StopAndEnqueue(sortingByWhatClip);

        AudioClip sortingClip;

        switch (sortingTypes[menuIndex])
        {
            case MenuSortingType.MOST_RECENTLY_SEEN:
                sortingClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Most Recently Seen");
                break;
            case MenuSortingType.CLOSEST_DISTANCE:
                sortingClip = VisionPulseResources.Instance.GetTTSMiscAudioClip("Closest Distance");
                break;
            default:
                yield break;
        }

        soundQueue.Enqueue(sortingClip);
        totalTime += sortingClip.length;

        yield return new WaitForSeconds(totalTime);

        SortAndReorderButtons();
        ScrollToSelected();
        HighlightItem();
    }

    private void SortAndReorderButtons()
    {
        switch (sortingTypes[menuState.GetIndex()])
        {
            case MenuSortingType.MOST_RECENTLY_SEEN:
                SortButtonsByMostRecentlySeen();
                break;
            case MenuSortingType.CLOSEST_DISTANCE:
                SortButtonsByClosestDistance();
                break;
            default:
                return;
        }

        ReorderButtonsAndStartOnFirstButton();
    }

    private void SortButtonsByMostRecentlySeen()
    {
        buttonEntries.Sort((a, b) =>
        {
            // Put "Undiscovered Section" at the very top
            bool aUndiscovered = a.GetButtonText() == VisionPulseResources.UndiscoveredSection;
            bool bUndiscovered = b.GetButtonText() == VisionPulseResources.UndiscoveredSection;

            if (aUndiscovered && !bUndiscovered)
            {
                return -1;
            }

            if (!aUndiscovered && bUndiscovered)
            {
                return 1;
            }

            // Otherwise, sort by time inserted
            return b.TimeInserted.CompareTo(a.TimeInserted);
        });
    }

    private void SortButtonsByClosestDistance()
    {
        if (menuState is RegionMenuState regionMenu)
        {
            foreach (ButtonEntry buttonEntry in buttonEntries)
            {
                string objectName = buttonEntry.GetButtonText();

                if (objectName == VisionPulseResources.UndiscoveredSection)
                {
                    continue;
                }

                Region region = discoveredItems.GetRegion(regionMenu.Name);
                GameObject interactableObj = region.GetDiscoveredInteractableObject(objectName);

                (_, float distance) = generateWaypoints.ToObject(player.transform.position, interactableObj.transform.position);
                buttonEntry.ClosestDistance = distance;
            }
        }
        else
        {
            bool foundClosestRegion = false;

            foreach (ButtonEntry buttonEntry in buttonEntries)
            {
                string regionName = buttonEntry.GetButtonText();
                Region region = discoveredItems.GetRegion(regionName);

                if (!foundClosestRegion && region.IsObjectInside(player.gameObject))
                {
                    buttonEntry.ClosestDistance = Mathf.NegativeInfinity;
                    foundClosestRegion = true;
                    continue;
                }

                (_, float distance) = generateWaypoints.ToObject(player.transform.position, region.transform.position);
                buttonEntry.ClosestDistance = distance;
            }
        }

        buttonEntries.Sort((a, b) =>
        {
            bool aUndiscovered = a.GetButtonText() == VisionPulseResources.UndiscoveredSection;
            bool bUndiscovered = b.GetButtonText() == VisionPulseResources.UndiscoveredSection;

            if (aUndiscovered && !bUndiscovered)
            {
                return -1;
            }

            if (!aUndiscovered && bUndiscovered)
            {
                return 1;
            }

            return a.ClosestDistance.CompareTo(b.ClosestDistance);
        });
    }

    private void ReorderButtonsAndStartOnFirstButton()
    {
        for (int i = 0; i < buttonEntries.Count; i++)
        {
            buttonEntries[i].Button.transform.SetSiblingIndex(i);
        }

        selectedButtonIndex = 0;
    }

    private void AddButtons(IEnumerable<string> buttonsText)
    {
        foreach (string buttonText in buttonsText)
        {
            AddButton(buttonText);
        }
    }

    private void AddButton(string buttonText)
    {
        if (buttonEntries.Count >= NumberOfPreallocatedButtons)
        {
            return;
        }

        GameObject button = preallocatedButtons[buttonEntries.Count];
        button.SetActive(true);

        TextMeshProUGUI textComponent = button.GetComponentInChildren<TextMeshProUGUI>();

        if (textComponent == null)
        {
            Debug.LogError($"GameObject '{button.name}' does not have a child component of type: {Utils.ComptimeTypeName(textComponent)}.");
            return;
        }

        textComponent.text = buttonText;

        Button buttonComponent = button.GetComponentInChildren<Button>();

        if (buttonComponent == null)
        {
            Debug.LogError($"GameObject '{button.name}' does not have a child component of type: {Utils.ComptimeTypeName(buttonComponent)}.");
            return;
        }

        buttonEntries.Insert(0, new ButtonEntry(buttonComponent, discoveredItems.GetTimeOfItem(buttonText)));
    }

    private void ExitMenu()
    {
        Toggle();
        ClearButtons();
    }

    private void ClearButtons()
    {
        for (int i = 0; i < buttonEntries.Count; i++)
        {
            buttonEntries[i].Button.gameObject.SetActive(false);
        }

        buttonEntries.Clear();
        selectedButtonIndex = 0;
    }

    internal class ButtonEntry
    {
        public ButtonEntry(Button button, float timeInserted)
        {
            Button = button;
            TimeInserted = timeInserted;
        }

        public Button Button { get; }

        public float TimeInserted { get; }

        public float ClosestDistance { get; set; } = 0.0f;

        public string GetButtonText()
        {
            return Button.GetComponentInChildren<TextMeshProUGUI>().text;
        }
    }

    internal class MainMenuState : IState
    {
        public IEnumerable<string> GetButtonNames(DiscoveryMenu discoveryMenu) => discoveryMenu.discoveredItems.GetRegionNames();

        public int GetIndex()
        {
            return 0;
        }

        public void Next(DiscoveryMenu discoveryMenu, object? obj)
        {
            if (obj == null || obj is not string name)
            {
                return;
            }

            discoveryMenu.menuState = new RegionMenuState(name);
            discoveryMenu.menuState.Refresh(discoveryMenu);
        }

        public void Refresh(DiscoveryMenu discoveryMenu)
        {
            discoveryMenu.ClearButtons();
            discoveryMenu.AddButtons(GetButtonNames(discoveryMenu));
            discoveryMenu.SortAndReorderButtons();
        }
    }

    internal class RegionMenuState : IState
    {
        public RegionMenuState(string name) => Name = name;

        public string Name { get; }

        public IEnumerable<string> GetButtonNames(DiscoveryMenu discoveryMenu)
        {
            Region region = discoveryMenu.discoveredItems.GetRegion(Name);
            List<string> buttonNames = new(region.GetDiscoveredInteractableObjectNames());

            if (region.HasUndiscovedSections())
            {
                buttonNames.Add(VisionPulseResources.UndiscoveredSection);
            }

            return buttonNames;
        }

        public int GetIndex()
        {
            return 1;
        }

        public void Next(DiscoveryMenu discoveryMenu, object? obj = null)
        {
            discoveryMenu.menuState = MainMenu;
            discoveryMenu.menuState.Refresh(discoveryMenu);
        }

        public void Refresh(DiscoveryMenu discoveryMenu)
        {
            discoveryMenu.ClearButtons();
            discoveryMenu.AddButtons(GetButtonNames(discoveryMenu));
            discoveryMenu.SortAndReorderButtons();
        }

        public void Prev(DiscoveryMenu discoveryMenu)
        {
            discoveryMenu.menuState = MainMenu;
            discoveryMenu.menuState.Refresh(discoveryMenu);
        }
    }
}
