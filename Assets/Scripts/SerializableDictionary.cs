using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class SerializableDictionary<Key, Value> : ISerializationCallbackReceiver
{
    [Serializable]
    public struct KeyValuePair
    {
        public Key key;
        public Value value;
    }

    public Dictionary<Key, Value> dictionary = new();
    public KeyValuePair[] data;

    public void OnBeforeSerialize()
    {
        data = dictionary.Select(x => new KeyValuePair { key = x.Key, value = x.Value }).ToArray();
    }

    public void OnAfterDeserialize()
    {
        dictionary = data.ToDictionary(x => x.key, x => x.value);
    }
}
