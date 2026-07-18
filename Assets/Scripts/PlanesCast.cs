using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

#nullable enable

public class PlanesCast : MonoBehaviour
{
    private static readonly Vector3[] BoundOffsets =
    {
        new(-1, -1, -1),
        new(-1, -1, 1),
        new(-1, 1, -1),
        new(-1, 1, 1),
        new(1, -1, -1),
        new(1, -1, 1),
        new(1, 1, -1),
        new(1, 1, 1),
    };

    private int allLayerMasksExceptIgnoreRaycast = 0;

    private DiscoveredItems discoveredItems = null!;

    private Camera xrCamera = null!;

    private DiscoveryMenu discoveryMenu = null!;

    public List<string> LookForDiscoverableObjects()
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(xrCamera);
        GameObject[] interactableObjects = GameObject.FindGameObjectsWithTag(VisionPulseResources.VisionPulseObjectTag);

        List<string> discoveredObjectNames = new();

        foreach (GameObject interactableObject in interactableObjects)
        {
            if (interactableObject.TryGetComponent(out XRGrabInteractable grabInteractable) && grabInteractable.isSelected)
            {
                continue;
            }

            Bounds bounds = Utils.GetBounds(interactableObject);

            if (GeometryUtility.TestPlanesAABB(planes, bounds))
            {
                if (IsVisible(interactableObject, bounds))
                {
                    if (VisionPulseResources.IsDebug)
                    {
                        Utils.DrawBounds(bounds, Color.green, 5.0f);
                    }

                    if (discoveryMenu.RecordObject(interactableObject))
                    {
                        discoveredObjectNames.Add(interactableObject.name);
                    }
                }
                else
                {
                    if (VisionPulseResources.IsDebug)
                    {
                        Utils.DrawBounds(bounds, Color.red, 10.0f);
                    }
                }
            }
        }

        return discoveredObjectNames;
    }

    public bool LookForUnexploredSection()
    {
        bool foundNewRegion = false;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(xrCamera);

        foreach (GameObject regionObject in VisionPulseResources.SceneSetup.GetRegionObjects())
        {
            foreach (Transform child in regionObject.transform)
            {
                GameObject section = child.gameObject;

                if (!section.activeSelf)
                {
                    continue;
                }

                Bounds sectionBounds = Utils.GetBounds(section);

                if (sectionBounds.Intersects(Utils.GetBounds(xrCamera.gameObject))
                    || (GeometryUtility.TestPlanesAABB(planes, sectionBounds) && IsVisible(section, sectionBounds)))
                {
                    if (!discoveredItems.ContainsRegion(regionObject.name))
                    {
                        foundNewRegion = true;
                        discoveryMenu.RecordRegion(regionObject.name);
                    }

                    section.SetActive(false);
                    Destroy(section);
                }
            }
        }

        return foundNewRegion;
    }

    private void Awake()
    {
        List<string?> layerNames = Utils.GetAllLayerNames();
        layerNames.Remove("Ignore Raycast");

        allLayerMasksExceptIgnoreRaycast = LayerMask.GetMask(layerNames.ToArray());
    }

    private void Start()
    {
        discoveredItems = VisionPulseResources.SceneSetup.GetDiscoveredItems();
        xrCamera = VisionPulseResources.SceneSetup.GetMainCamera();
        discoveryMenu = VisionPulseResources.SceneSetup.GetDiscoveryMenu();
    }

    private bool IsVisible(GameObject obj, Bounds boundsOfObj)
    {
        if (HasLineOfSightToPointOrObject(obj, boundsOfObj.center))
        {
            return true;
        }

        foreach (Vector3 boundOffset in BoundOffsets)
        {
            if (HasLineOfSightToPointOrObject(obj, boundsOfObj.center + Vector3.Scale(boundsOfObj.extents, boundOffset)))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasLineOfSightToPointOrObject(GameObject obj, Vector3 point)
    {
        Vector3 direction = point - xrCamera.transform.position;
        float distance = direction.magnitude;

        // Just giving it a little bit more
        distance += distance;

        // Kinda weird but lemme explain. We already know that the camera is facing some object
        // because of the GeometryUtility.TestPlanesAABB check. So if we didn't hit anything
        // and we know that we are facing the object, it means our line of sight is not blocked and
        // we can return true. This is more of a backup case
        if (!Physics.Raycast(xrCamera.transform.position, direction, out RaycastHit hit, distance, allLayerMasksExceptIgnoreRaycast))
        {
            return true;
        }

        // If we did hit something, it could be the object we want
        return hit.collider.gameObject == obj;
    }
}