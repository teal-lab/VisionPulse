using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#nullable enable

public class SoundSpeedMenu : MonoBehaviour
{
    private const float JoystickCooldown = 0.20f;

    private readonly GameObject[] buttonEntries = new GameObject[VisionPulseResources.SpeedOptions.Length];

    [SerializeField]
    private GameObject menu = null!;

    private ScrollRect scrollRect = null!;

    private RectTransform content = null!;

    private GameObject buttonPrefab = null!;

    private int selectedButtonIndex = 0;

    private float lastInputTime = 0.0f;

    private SoundQueue soundQueue = null!;

    private InputAction xriRightSoundSpeedMenuScroll = null!;

    private InputAction xriRightSoundSpeedMenuSelect = null!;

    private CoroutineHandler announceSortingTypeCoroutine = null!;

    private Camera xrCamera = null!;

    private Player player = null!;

    public bool IsOpen { get; private set; } = false;

    public void HandleMenuNavigation()
    {
        if (player.IsReachedWaypointCoroutineRunning)
        {
            return;
        }

        if (!IsOpen)
        {
            Refresh();
            Toggle();
            return;
        }

        ExitMenu();
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

        announceSortingTypeCoroutine = new(this);

        for (int i = 0; i < VisionPulseResources.SpeedOptions.Length; i++)
        {
            buttonEntries[i] = Instantiate(buttonPrefab, Vector3.zero, Quaternion.identity);
            buttonEntries[i].transform.SetParent(this.content, false);
            buttonEntries[i].transform.localScale = Vector3.one;
            buttonEntries[i].transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(Vector3.zero));

            TextMeshProUGUI textComponent = buttonEntries[i].GetComponentInChildren<TextMeshProUGUI>();

            if (textComponent == null)
            {
                Debug.LogError($"GameObject '{buttonEntries[i].name}' does not have a child component of type: {Utils.ComptimeTypeName(textComponent)}.");
                return;
            }

            textComponent.text = VisionPulseResources.SpeedOptions[i];

            Button buttonComponent = buttonEntries[i].GetComponentInChildren<Button>();

            if (buttonComponent == null)
            {
                Debug.LogError($"GameObject '{buttonEntries[i].name}' does not have a child component of type: {Utils.ComptimeTypeName(buttonComponent)}.");
                return;
            }

            buttonEntries[i].SetActive(false);
        }
    }

    private void Start()
    {
        InputActionMap xriRightSoundSpeedMenu = VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Sound Menu");

        xriRightSoundSpeedMenuScroll = xriRightSoundSpeedMenu.FindAction("Scroll");
        xriRightSoundSpeedMenuScroll.Enable();

        xriRightSoundSpeedMenuSelect = xriRightSoundSpeedMenu.FindAction("Select");
        xriRightSoundSpeedMenuSelect.performed += OnSelectButtonClicked;

        xrCamera = VisionPulseResources.SceneSetup.GetMainCamera();
        player = VisionPulseResources.SceneSetup.GetPlayer();
    }

    private void OnDestroy()
    {
        xriRightSoundSpeedMenuSelect.performed -= OnSelectButtonClicked;
    }

    private void Update()
    {
        if (!menu.activeSelf
            || IsButtonsInactive()
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

        Vector2 thumbstickInput = xriRightSoundSpeedMenuScroll.ReadValue<Vector2>();

        if (thumbstickInput.y >= 0.01f)
        {
            selectedButtonIndex = (selectedButtonIndex - 1 + buttonEntries.Length) % buttonEntries.Length;
            ScrollToSelected();
            HighlightItem();
        }
        else if (thumbstickInput.y <= -0.01f)
        {
            selectedButtonIndex = (selectedButtonIndex + 1) % buttonEntries.Length;
            ScrollToSelected();
            HighlightItem();
        }

        lastInputTime = Time.time;
    }

    private void Toggle()
    {
        IsOpen = !IsOpen;

        if (IsOpen)
        {
            player.StopSoundQueue();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Locomotion Custom").Disable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Locomotion Custom").Disable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Interaction Persistent").Disable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Sound Menu").Enable();
            PositionMenu();
        }
        else
        {
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Locomotion Custom").Enable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Locomotion Custom").Enable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Left Interaction Persistent").Enable();
            VisionPulseResources.SceneSetup.GetCachedActionMap("XRI Right Sound Menu").Disable();
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
        if (IsButtonsInactive())
        {
            return;
        }

        Button buttonComponent = buttonEntries[selectedButtonIndex].GetComponentInChildren<Button>();

        RectTransform contentRect = scrollRect.content;
        RectTransform selectedItem = buttonComponent.GetComponent<RectTransform>();
        RectTransform viewportRect = scrollRect.viewport;

        float contentHeight = contentRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        float itemHeight = selectedItem.rect.height;
        float itemY = Mathf.Abs(selectedItem.anchoredPosition.y);

        float minVisibleY = itemY - (itemHeight * 0.5f);
        float normalizedPosition = Mathf.Clamp01((minVisibleY - (viewportHeight * 0.5f)) / (contentHeight - viewportHeight));
        scrollRect.verticalNormalizedPosition = 1 - normalizedPosition;
    }

    private void HighlightItem()
    {
        if (IsButtonsInactive())
        {
            return;
        }

        string speedOption = buttonEntries[selectedButtonIndex].GetComponentInChildren<TextMeshProUGUI>().text;
        AudioClip selectedMenuItemClip = VisionPulseResources.Instance.GetEffectAudioClip("Selected Menu Item");
        AudioClip speedOptionClip = VisionPulseResources.Instance.GetTTSSpeedMenuAudioClip(speedOption);

        soundQueue.StopAndEnqueue(selectedMenuItemClip, speedOptionClip);

        EventSystem.current.SetSelectedGameObject(buttonEntries[selectedButtonIndex]);
    }

    private void OnSelectButtonClicked(InputAction.CallbackContext context)
    {
        if (IsButtonsInactive() || !IsOpen)
        {
            return;
        }

        string speedOption = buttonEntries[selectedButtonIndex].GetComponentInChildren<TextMeshProUGUI>().text;

        VisionPulseResources.Instance.SetTTSSpeed(speedOption);

        // We just selected an item, so turn off the menu
        ExitMenu();
    }

    private void Refresh()
    {
        for (int i = 0; i < buttonEntries.Length; i++)
        {
            buttonEntries[i].SetActive(true);
        }
    }

    private void ExitMenu()
    {
        Toggle();
        ClearButtons();
    }

    private void ClearButtons()
    {
        for (int i = 0; i < buttonEntries.Length; i++)
        {
            buttonEntries[i].SetActive(false);
        }
    }

    private bool IsButtonsInactive()
    {
        return !buttonEntries[0].activeSelf;
    }
}
