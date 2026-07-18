using System.Collections.Generic;
using System.Linq;

#nullable enable

public static class CollectionExtensions
{
    public static T? Pop<T>(this List<T> list, int index)
       where T : struct
    {
        if (list == null || index < 0 || index >= list.Count)
        {
            return null;
        }

        T value = list[index];
        list.RemoveAt(index);
        return value;
    }

    public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable == null)
        {
            return true;
        }

        if (enumerable is ICollection<T> collection)
        {
            return collection.Count < 1;
        }

        return !enumerable.Any();
    }

    public static int IndexOf<T>(this IReadOnlyList<T> list, T value)
    {
        if (list == null)
        {
            return -1;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;

        for (int i = 0; i < list.Count; i++)
        {
            if (comparer.Equals(list[i], value))
            {
                return i;
            }
        }

        return -1;
    }

    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
    {
        if (dict == null)
        {
            return defaultValue;
        }

        return dict.TryGetValue(key, out TValue? value) ? value : defaultValue;
    }
}
