using System;
using System.Collections;
using System.Collections.Generic;

public class OrderedDict<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private readonly Dictionary<TKey, TValue> dict;
    private readonly List<TKey> order;

    public OrderedDict()
    {
        dict = new Dictionary<TKey, TValue>();
        order = new List<TKey>();
    }

    public int Count => dict.Count;

    public IEnumerable<TKey> Keys => order;

    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (TKey key in order)
            {
                yield return dict[key];
            }
        }
    }

    public TValue this[TKey key]
    {
        get => dict[key];
        set
        {
            if (!dict.ContainsKey(key))
            {
                order.Add(key);
            }

            dict[key] = value;
        }
    }

    public void Add(TKey key, TValue value)
    {
        if (dict.ContainsKey(key))
        {
            throw new ArgumentException("An element with the same key already exists.");
        }

        dict.Add(key, value);
        order.Add(key);
    }

    public bool ContainsKey(TKey key) => dict.ContainsKey(key);

    public bool TryGetValue(TKey key, out TValue value) => dict.TryGetValue(key, out value);

    public bool Remove(TKey key)
    {
        if (!dict.Remove(key))
        {
            return false;
        }

        order.Remove(key);
        return true;
    }

    public void Clear()
    {
        dict.Clear();
        order.Clear();
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (TKey key in order)
        {
            yield return new KeyValuePair<TKey, TValue>(key, dict[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
