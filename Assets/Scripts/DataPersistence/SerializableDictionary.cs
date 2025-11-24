using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{

    [SerializeField] private List<TKey> keys = new List<TKey>();
    [SerializeField] private List<TValue> values = new List<TValue>();

    // save the dictionary to lists
    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (KeyValuePair<TKey, TValue> pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    // load the dictionary from lists
    public void OnAfterDeserialize()
    {
        this.Clear();

        if (keys.Count != values.Count)
        {
            Debug.LogError("Tried to deserialize a SerializableDictionary, but the amount of keys ("
                + keys.Count + ") does not match the number of values (" + values.Count
                + ") which indicates that something went wrong");
        }

        int pairsToProcess = Math.Min(keys.Count, values.Count);
        if (pairsToProcess == 0) return;

        var seenKeys = new HashSet<TKey>();

        for (int i = 0; i < pairsToProcess; i++)
        {
            TKey k = keys[i];
            TValue v = values[i];

            if (k == null)
            {
                Debug.LogWarning($"SerializableDictionary: skipping null key at index {i} during deserialization.");
                continue;
            }

            if (seenKeys.Contains(k) || this.ContainsKey(k))
            {
                // Duplicate key encountered in serialized data. Keep the first occurrence and skip subsequent ones.
                Debug.LogWarning($"SerializableDictionary: duplicate key '{k}' found at index {i}; skipping duplicate during deserialization.");
                continue;
            }

            try
            {
                this.Add(k, v);
                seenKeys.Add(k);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SerializableDictionary: failed to add deserialized entry for key '{k}' at index {i}: {ex.Message}");
            }
        }
    }

}
