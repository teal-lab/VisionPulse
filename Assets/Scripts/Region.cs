using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable enable

public class Region : MonoBehaviour
{
    private readonly HashSet<GameObject> interactableObjectsInRegion = new();

    [SerializeField]
    private string regionDescription = string.Empty;

    private bool alreadyEntered = false;

    private DiscoveredItems discoveredItems = null!;

    private FadeScreen fadeScreen = null!;

    private Player player = null!;

    public bool IsObjectInside(GameObject obj)
    {
        return GetIntersectionVolume(obj) > 0.0f;
    }

    public float GetIntersectionVolume(GameObject obj)
    {
        Bounds regionBounds = Utils.GetBounds(gameObject);
        Bounds objBounds = Utils.GetBounds(obj);

        if (!regionBounds.Intersects(objBounds))
        {
            return 0.0f;
        }

        Vector3 min = Vector3.Max(regionBounds.min, objBounds.min);
        Vector3 max = Vector3.Min(regionBounds.max, objBounds.max);
        Vector3 size = max - min;

        return Mathf.Max(0.0f, size.x * size.y * size.z);
    }

    public List<(Vector3, Material)> GetWaypointsToClosestUndiscovedSection(GenerateWaypoints generateWaypoints, Player player)
    {
        IEnumerable<GameObject> undiscovedSections = EnumerateUndiscovedSections();
        IEnumerator<GameObject> e = undiscovedSections.GetEnumerator();

        if (!e.MoveNext())
        {
            Debug.LogError($"Region {name} has 0 undiscoved sections.");
            return new();
        }

        GameObject closestSection = e.Current;
        (List<(Vector3, Material)> closestWaypoints, float closestDistance) = generateWaypoints.ToUndiscoveredSection(player.transform.position, closestSection);

        while (closestWaypoints.IsEmpty() && e.MoveNext())
        {
            closestSection = e.Current;
            (closestWaypoints, closestDistance) = generateWaypoints.ToUndiscoveredSection(player.transform.position, closestSection);
        }

        while (e.MoveNext())
        {
            (List<(Vector3, Material)> waypoints, float distance) = generateWaypoints.ToUndiscoveredSection(player.transform.position, e.Current);

            if (waypoints.IsEmpty())
            {
                continue;
            }

            if (distance < closestDistance)
            {
                closestSection = e.Current;
                closestWaypoints = waypoints;
                closestDistance = distance;
            }
        }

        if (closestWaypoints.IsEmpty())
        {
            Debug.LogError("We got a BIG problem");
            return new();
        }

        if (VisionPulseResources.IsDebug)
        {
            Debug.Log($"{closestSection.name}");
        }

        return closestWaypoints;
    }

    public bool HasUndiscovedSections()
    {
        return EnumerateUndiscovedSections().Any();
    }

    public void AddInteractableObject(GameObject interactableObject)
    {
        interactableObjectsInRegion.Add(interactableObject);
    }

    public bool RemoveInteractableObjectIfPresent(GameObject interactableObject)
    {
        return interactableObjectsInRegion.Remove(interactableObject);
    }

    public GameObject GetDiscoveredInteractableObject(string name)
    {
        GameObject? discoveredInteractableObject = EnumerateDiscoveredInteractableObjects().FirstOrDefault(obj => obj.name == name);

        if (discoveredInteractableObject == null)
        {
            Debug.LogError($"Unable to find object {name} in region");
            return null!;
        }

        return discoveredInteractableObject;
    }

    public IEnumerable<string> GetDiscoveredInteractableObjectNames()
    {
        return EnumerateDiscoveredInteractableObjects().Select(obj => obj.name);
    }

    public int GetDiscoveredInteractableObjectsCount()
    {
        return EnumerateDiscoveredInteractableObjects().Count();
    }

    public bool HasDiscoveredInteractableObjects()
    {
        return EnumerateDiscoveredInteractableObjects().Any();
    }

    public bool IsEmpty()
    {
        return !HasUndiscovedSections() && !HasDiscoveredInteractableObjects();
    }

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Start()
    {
        fadeScreen = VisionPulseResources.SceneSetup.GetFadeScreen();
        discoveredItems = VisionPulseResources.SceneSetup.GetDiscoveredItems();
        player = VisionPulseResources.SceneSetup.GetPlayer();
    }

    private void OnTriggerStay(Collider other)
    {
        if (alreadyEntered)
        {
            return;
        }

        if (regionDescription == string.Empty)
        {
            alreadyEntered = true;
            return;
        }

        if (fadeScreen.IsRunning() || other.name != VisionPulseResources.XROrigin)
        {
            return;
        }

        if (VisionPulseResources.IsDebug)
        {
            Utils.LogWithMethod($"{gameObject.name}: {other.name}");
        }

        AudioClip regionDescriptionClip = VisionPulseResources.Instance.GetTTSDescriptionAudioClip(regionDescription);
        player.SetRegionDescription(gameObject.name, regionDescriptionClip);

        alreadyEntered = true;
    }

    private IEnumerable<GameObject> EnumerateUndiscovedSections()
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                if (!child.gameObject.CompareTag(VisionPulseResources.VisionPulseSectionTag))
                {
                    Debug.LogError($"In Region {name}, the section {child.gameObject.name} is missing the {VisionPulseResources.VisionPulseSectionTag} tag");
                    yield break;
                }

                yield return child.gameObject;
            }
        }
    }

    private IEnumerable<GameObject> EnumerateDiscoveredInteractableObjects()
    {
        GameObject[] interactableObjects = GameObject.FindGameObjectsWithTag(VisionPulseResources.VisionPulseObjectTag);

        foreach (GameObject interactableObject in interactableObjects)
        {
            if (discoveredItems.ContainsInteractableObjectName(interactableObject.name)
                && interactableObjectsInRegion.Contains(interactableObject))
            {
                yield return interactableObject;
            }
        }
    }
}