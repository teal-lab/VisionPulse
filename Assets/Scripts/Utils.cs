using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable

public class Utils
{
    private static readonly Dictionary<Type, Array> EnumCache = new();

    private static readonly Material LineMaterial = new(Shader.Find("Sprites/Default"));

    public static AudioSource AddComponentAudioSource(GameObject obj)
    {
        AudioSource audioSource = obj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        return audioSource;
    }

    public static AudioSource AddComponentAudioSource(GameObject obj, AudioClip clip)
    {
        AudioSource audioSource = AddComponentAudioSource(obj);
        audioSource.clip = clip;

        return audioSource;
    }

    public static AudioSource AddComponentAudioSourceWithHum(GameObject obj)
    {
        AudioClip humEffectClip = VisionPulseResources.Instance.GetEffectAudioClip(VisionPulseResources.HumSoundEffect);
        AudioSource audioSource = AddComponentAudioSource(obj, humEffectClip);

        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 10.0f;
        audioSource.maxDistance = 25.0f;
        audioSource.loop = true;

        return audioSource;
    }

    public static T FindGameObjectAndGetComponent<T>(string nameOfObject)
        where T : Component
    {
        GameObject? obj = GameObject.Find(nameOfObject);

        if (obj == null)
        {
            Debug.LogError($"Could not find GameObject '{nameOfObject}'.");
            return null!;
        }

        if (!obj.TryGetComponent(out T component))
        {
            Debug.LogError($"GameObject '{nameOfObject}' does not have a component of type: {typeof(T).Name}.");
            return null!;
        }

        return component;
    }

    public static AudioSource GetHumAudioSource(GameObject obj)
    {
        AudioSource[] audioSources = obj.GetComponents<AudioSource>();

        if (audioSources.Length == 0)
        {
            Debug.LogError($"{obj.name}: No AudioSource components found!");
            return null!;
        }

        foreach (AudioSource source in audioSources)
        {
            if (source.clip.name == VisionPulseResources.HumSoundEffect)
            {
                return source;
            }
        }

        Debug.LogError($"{obj.name}: Does not contain an audio source with clip {nameof(VisionPulseResources.HumSoundEffect)}");
        return null!;
    }

    public static Region GetRegionScript(GameObject obj)
    {
        if (obj.TryGetComponent(out Region region))
        {
            return region;
        }

        Debug.LogError("Please make sure to attach the Region script to the Region object");
        return null!;
    }

    public static List<string?> GetAllLayerNames()
    {
        List<string?> layerNames = new();

        for (int i = 0; i < 32; i++)
        {
            layerNames.Add(LayerMask.LayerToName(i));
        }

        return layerNames;
    }

    public static Bounds GetBounds(GameObject obj)
    {
        Collider[] colliders = obj.GetComponents<Collider>();

        if (colliders.Length > 0)
        {
            Bounds combined = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
            {
                combined.Encapsulate(colliders[i].bounds);
            }

            return combined;
        }

        if (obj.TryGetComponent(out Renderer rend))
        {
            return rend.bounds;
        }

        Debug.LogError($"GameObject '{obj.name}' does not have a Collider or Renderer, so bounds cannot be determined.");
        return default;
    }

    public static void CheckSerializedFields<T>(T instance)
        where T : UnityEngine.Object
    {
        Type type = typeof(T);
        FieldInfo[] allFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (FieldInfo field in allFields)
        {
            if (Attribute.IsDefined(field, typeof(SerializeField)))
            {
                object? value = field.GetValue(instance);

                if (IsPrimitive(value))
                {
                    continue;
                }

                UnityEngine.Object? obj = value as UnityEngine.Object;

                if (obj == null)
                {
                    Debug.LogError($"Field '{field.Name}' is not assigned on '{type.Name}'", instance);
                }
            }
        }
    }

#pragma warning disable SA1313
    public static string ComptimeTypeName<T>(T _) => typeof(T).Name;
#pragma warning restore SA1313

    public static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }

    public static string JoinValuesToStringByComma<T>(IEnumerable<T> values) => string.Join(", ", values.Select(v => v?.ToString() ?? "null"));

    public static void LogWithMethod(object message, [CallerMemberName] string method = "", [CallerFilePath] string file = "")
    {
        string className = System.IO.Path.GetFileNameWithoutExtension(file);
        Debug.Log($"[{className}.{method}] {message}");
    }

    public static T GetNextEnumValue<T>(T value)
        where T : Enum
    {
        IReadOnlyList<T> values = GetEnumValues<T>();
        int index = values.IndexOf(value);

        if (index == -1)
        {
            Debug.LogError($"Value '{value}' is not a valid member of enum {typeof(T).Name}.");
            return value;
        }

        int nextIndex = (index + 1) % values.Count;

        return values[nextIndex];
    }

    public static T GetRandomEnumValue<T>()
        where T : Enum
    {
        return RandomChoice(GetEnumValues<T>());
    }

    public static T RandomChoice<T>(IReadOnlyList<T> list)
    {
        if (list.IsEmpty())
        {
            Debug.LogError("list is empty");
            return default!;
        }

        int index = UnityEngine.Random.Range(0, list.Count);
        return list[index];
    }

    public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration)
    {
        GameObject lineObj = new("Line");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        lr.material = LineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.01f;
        lr.endWidth = 0.01f;

        lr.positionCount = 2;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        UnityEngine.Object.Destroy(lineObj, duration);
    }

    public static void DrawBounds(GameObject obj, Color color, float duration)
    {
        DrawBounds(GetBounds(obj), color, duration);
    }

    public static void DrawBounds(Bounds bounds, Color color, float duration)
    {
        GameObject lineObj = new("Line");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();

        lr.material = LineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.01f;
        lr.endWidth = 0.01f;
        lr.positionCount = 16;

        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        Vector3[] p = new Vector3[8];
        p[0] = c + new Vector3(-e.x, -e.y, -e.z);
        p[1] = c + new Vector3(e.x, -e.y, -e.z);
        p[2] = c + new Vector3(e.x, -e.y, e.z);
        p[3] = c + new Vector3(-e.x, -e.y, e.z);

        p[4] = c + new Vector3(-e.x, e.y, -e.z);
        p[5] = c + new Vector3(e.x, e.y, -e.z);
        p[6] = c + new Vector3(e.x, e.y, e.z);
        p[7] = c + new Vector3(-e.x, e.y, e.z);

        Vector3[] linePoints = new Vector3[]
        {
            p[0], p[1], p[2], p[3], p[0],
            p[4], p[5], p[6], p[7], p[4],
            p[5], p[1],
            p[2], p[6],
            p[7], p[3],
        };

        lr.positionCount = linePoints.Length;
        lr.SetPositions(linePoints);

        UnityEngine.Object.Destroy(lineObj, duration);
    }

    private static bool IsPrimitive(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        Type type = obj.GetType();
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
    }

    private static IReadOnlyList<T> GetEnumValues<T>()
        where T : Enum
    {
        Type type = typeof(T);

        if (!EnumCache.TryGetValue(type, out Array array))
        {
            array = Enum.GetValues(type);
            EnumCache[type] = array;
        }

        return (IReadOnlyList<T>)array;
    }
}