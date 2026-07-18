using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DiscoveredItems : MonoBehaviour
{
    private readonly Dictionary<string, (float, Region)> regions = new();

    private readonly Dictionary<string, float> interactableObjects = new();

    public Region GetRegion(string regionName)
    {
        return regions[regionName].Item2;
    }

    public IEnumerable<Region> GetRegions()
    {
        return regions.Values.Select(v => v.Item2);
    }

    public IEnumerable<string> GetRegionNames()
    {
        return regions.Keys;
    }

    public void AddRegion(string regionName, Region region)
    {
        regions[regionName] = (Time.time + (regions.Count * 0.0001f), region);
    }

    public bool ContainsRegion(string regionName)
    {
        return regions.ContainsKey(regionName);
    }

    public void AddInteractableObjectName(string objectName)
    {
        interactableObjects[objectName] = Time.time + (interactableObjects.Count * 0.0001f);
    }

    public bool ContainsInteractableObjectName(string objectName)
    {
        return interactableObjects.ContainsKey(objectName);
    }

    public float GetTimeOfItem(string name)
    {
        if (name == VisionPulseResources.UndiscoveredSection)
        {
            return -1.0f;
        }

        if (interactableObjects.TryGetValue(name, out float time))
        {
            return time;
        }

        return regions[name].Item1;
    }
}