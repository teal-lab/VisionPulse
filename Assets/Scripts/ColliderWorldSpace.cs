using UnityEngine;

#nullable enable

public class ColliderWorldSpace : MonoBehaviour
{
    [SerializeField]
    private GameObject mainCube = null!;

    [SerializeField]
    private GameObject addtionalCube = null!;

    private void OnValidate()
    {
        Utils.CheckSerializedFields(this);
    }

    private void Awake()
    {
        BoxCollider main = mainCube.GetComponent<BoxCollider>();
        BoxCollider addtional = addtionalCube.GetComponent<BoxCollider>();

        Vector3 worldCenter = addtional.transform.TransformPoint(addtional.center);
        Vector3 worldSize = Vector3.Scale(addtional.size, addtional.transform.lossyScale);

        Vector3 localCenter = main.transform.InverseTransformPoint(worldCenter);
        Vector3 localSize = new(
            worldSize.x / main.transform.lossyScale.x,
            worldSize.y / main.transform.lossyScale.y,
            worldSize.z / main.transform.lossyScale.z);

        Debug.Log($"Center: {localCenter}");
        Debug.Log($"Size: {localSize}");
    }
}
