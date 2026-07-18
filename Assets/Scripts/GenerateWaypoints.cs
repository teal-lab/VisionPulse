using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#nullable enable

public class GenerateWaypoints : MonoBehaviour
{
    private const float CornerAngleThreshold = 135.0f;

    private const float CornerPushDistance = 1.5f;

    private const float SlightForwardOffset = 0.5f;

    private const float MinWaypointsDistance = 3.5f;

    private const float MaxWaypointsDistance = 10.0f;

    private const float WaypointRemovalAngleThreshold = 50.0f;

    private const float WaypointDoubleSpawnThreshold = Player.VibrationDetectionThreshold / 2.0f;

    private static GameObject waypointObject = null!;

    private static Material testingWaypointMaterialNormal = null!;

    private static Material testingWaypointMaterialCorner = null!;

    private static Material testingWaypointMaterialExtra = null!;

    private NavMeshPath path = null!;

    private Player player = null!;

    public (List<(Vector3, Material)>, float) ToObject(Vector3 currentPosition, Vector3 targetPosition, bool isToUndiscoveredSection = false)
    {
        // ToObject is called in other objects' Start, so let's just do a double check just in case
        if (player == null)
        {
            player = VisionPulseResources.SceneSetup.GetPlayer();
        }

        // If you are getting weird paths that don't make any sense, make sure to rebake
        if (!IsValidNavMeshPosition(currentPosition, out Vector3 startPosition))
        {
            Debug.LogError($"{nameof(currentPosition)} is NOT on the NavMesh!");
            return default;
        }

        if (!IsValidNavMeshPosition(targetPosition, out Vector3 endPosition))
        {
            Debug.LogError($"{nameof(targetPosition)} is NOT on the NavMesh!");
            return default;
        }

        if (!NavMesh.CalculatePath(startPosition, endPosition, NavMesh.AllAreas, path))
        {
            Debug.LogError("Path NOT found.");
            return default;
        }

        if (path.corners.Length == 0)
        {
            Debug.LogError("Calculated NavMesh path has no corners.");
            return default;
        }

        List<(Vector3, Material)> waypoints = GenerateBetterPath(isToUndiscoveredSection);
        float distance = GetDistanceFromStartAndEndPosition(currentPosition, waypoints, targetPosition);

        return (waypoints, distance);
    }

    public (List<(Vector3, Material)>, float) ToUndiscoveredSection(Vector3 currentPosition, GameObject undiscovedSection)
    {
        (List<(Vector3, Material)> waypoints, _) = ToObject(currentPosition, undiscovedSection.transform.position, true);

        // It's ok if we have zero waypoints for an object because the hum and vibration
        // will still happen on it. We can't say the same for a section tho
        if (waypoints.Count == 0)
        {
            Debug.LogError("No waypoints generated for sections.");
        }

        RemoveWaypointsInUndiscoveredSection(waypoints, undiscovedSection);

        float distance = GetDistanceFromStartAndEndPosition(currentPosition, waypoints, undiscovedSection.transform.position);

        return (waypoints, distance);
    }

    private void Awake()
    {
        if (waypointObject == null)
        {
            GameObject waypointPrefab = Resources.Load<GameObject>("Prefabs/Waypoint");
            waypointObject = Instantiate(waypointPrefab, Vector3.zero, Quaternion.identity);
            waypointObject.SetActive(false);
        }

        if (testingWaypointMaterialNormal == null)
        {
            testingWaypointMaterialNormal = Resources.Load<Material>("Materials/Yellow");
        }

        if (VisionPulseResources.IsUserStudy)
        {
            if (testingWaypointMaterialCorner == null)
            {
                testingWaypointMaterialCorner = testingWaypointMaterialNormal;
            }

            if (testingWaypointMaterialExtra == null)
            {
                testingWaypointMaterialExtra = testingWaypointMaterialNormal;
            }
        }
        else
        {
            if (testingWaypointMaterialCorner == null)
            {
                testingWaypointMaterialCorner = Resources.Load<Material>("Materials/Red");
            }

            if (testingWaypointMaterialExtra == null)
            {
                testingWaypointMaterialExtra = Resources.Load<Material>("Materials/Blue");
            }
        }

        path = new();
    }

    private void Start()
    {
        if (player == null)
        {
            player = VisionPulseResources.SceneSetup.GetPlayer();
        }
    }

    private List<(Vector3, Material)> GenerateBetterPath(bool isToUndiscoveredSection)
    {
        List<(Vector3, Material)> newWaypoints = new();

        for (int i = 0; i < path.corners.Length; i++)
        {
            newWaypoints.Add((path.corners[i], testingWaypointMaterialNormal));
        }

        int counter = 0;
        int size = newWaypoints.Count;

        for (int i = 0; i < newWaypoints.Count - 2; i++)
        {
            Vector3 a = newWaypoints[i].Item1;
            Vector3 b = newWaypoints[i + 1].Item1;
            Vector3 c = newWaypoints[i + 2].Item1;

            float distAB = Vector3.Distance(a, b);

            if (distAB <= MinWaypointsDistance)
            {
                Vector3 dir1 = (b - a).normalized;
                Vector3 dir2 = (c - b).normalized;
                float angle = Vector3.Angle(dir1, dir2);

                if (angle > WaypointRemovalAngleThreshold)
                {
                    counter++;
                    newWaypoints.RemoveAt(i + 1);
                    i--;
                }
            }
        }

        if (VisionPulseResources.IsDebug)
        {
            Debug.Log($"Waypoints Removed: {counter} out of {size}");
        }

        List<(Vector3, Material)> adjustedWaypoints = new() { newWaypoints[0] };

        for (int i = 1; i < newWaypoints.Count - 1; i++)
        {
            Vector3 prev = adjustedWaypoints[^1].Item1;
            Vector3 curr = newWaypoints[i].Item1;
            Vector3 next = newWaypoints[i + 1].Item1;

            Vector3 dirToPrev = (prev - curr).normalized;
            Vector3 dirToNext = (next - curr).normalized;

            float angle = Vector3.Angle(dirToPrev, dirToNext);

            if (angle < CornerAngleThreshold)
            {
                // Compute a bisector direction and initial push direction
                // Only apply horizontal push (on XZ plane so we push away from a corner)
                Vector3 bisector = FlattenAndNormalize(-(dirToPrev + dirToNext));
                Vector3 alongPath = FlattenAndNormalize(next - prev);
                Vector3 nextAlongPath = FlattenAndNormalize(next - curr);

                Vector3 pushedCurr = curr + (bisector * CornerPushDistance) + (alongPath * SlightForwardOffset);
                Vector3 pushedNext = next + (bisector * CornerPushDistance) + (nextAlongPath * (SlightForwardOffset + 0.75f));

                AddToWaypoints(adjustedWaypoints, pushedCurr, testingWaypointMaterialCorner);
                AddToWaypoints(adjustedWaypoints, pushedNext, testingWaypointMaterialCorner);

                i++;
            }
            else
            {
                adjustedWaypoints.Add((curr, testingWaypointMaterialNormal));
            }
        }

        if (adjustedWaypoints[^1] != newWaypoints[^1])
        {
            adjustedWaypoints.Add(newWaypoints[^1]);
        }

        int minWaypoints = isToUndiscoveredSection ? 1 : 0;

        // Remove the waypoints that are close to us
        while (adjustedWaypoints.Count > minWaypoints && Vector3.Distance(player.transform.position, adjustedWaypoints[0].Item1) < WaypointDoubleSpawnThreshold)
        {
            adjustedWaypoints.RemoveAt(0);
        }

        // Add some extra waypoints in between
        for (int i = 0; i < adjustedWaypoints.Count - 1; i++)
        {
            float distance = Vector3.Distance(adjustedWaypoints[i].Item1, adjustedWaypoints[i + 1].Item1);

            if (distance >= MaxWaypointsDistance)
            {
                Vector3 midpoint = (adjustedWaypoints[i].Item1 + adjustedWaypoints[i + 1].Item1) / 2.0f;
                AddToWaypoints(adjustedWaypoints, midpoint, testingWaypointMaterialExtra, i + 1);
                i--;
            }
        }

        return adjustedWaypoints;
    }

    private bool IsValidNavMeshPosition(Vector3 position, out Vector3 validPosition, float maxDistance = 4.0f)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
        {
            validPosition = hit.position;
            return true;
        }

        validPosition = position;
        return false;
    }

    private bool AddToWaypoints(List<(Vector3, Material)> waypoints, Vector3 position, Material testingMaterial, int index = -1)
    {
        if (IsValidNavMeshPosition(position, out Vector3 validPosition))
        {
            index = (index == -1) ? waypoints.Count : index;
            waypoints.Insert(index, (validPosition, testingMaterial));
            return true;
        }

        Debug.LogWarning("Could not add waypoint");
        return false;
    }

    private void RemoveWaypointsInUndiscoveredSection(List<(Vector3, Material)> newWaypoints, GameObject undiscoveredSection)
    {
        waypointObject.SetActive(true);

        // We want to make sure that we at least have 1 waypoint
        for (int i = newWaypoints.Count - 1; i >= 1; i--)
        {
            waypointObject.transform.position = newWaypoints[i].Item1;

            Bounds a = Utils.GetBounds(undiscoveredSection);
            Bounds b = Utils.GetBounds(waypointObject);

            if (a.Intersects(b))
            {
                newWaypoints.RemoveAt(i);
            }
        }

        waypointObject.SetActive(false);
    }

    private float GetDistanceFromStartAndEndPosition(Vector3 startPosition, List<(Vector3, Material)> waypoints, Vector3 endPosition)
    {
        waypoints.Insert(0, (startPosition, null!));
        waypoints.Add((endPosition, null!));

        float distance = CalculateTotalDistance(waypoints);

        waypoints.RemoveAt(0);
        waypoints.RemoveAt(waypoints.Count - 1);

        return distance;
    }

    private float CalculateTotalDistance(List<(Vector3, Material)> waypoints)
    {
        float total = 0.0f;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            total += Vector3.Distance(waypoints[i].Item1, waypoints[i + 1].Item1);
        }

        return total;
    }

    private Vector3 FlattenAndNormalize(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }
}