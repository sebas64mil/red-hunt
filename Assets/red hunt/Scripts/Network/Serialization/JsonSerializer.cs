using System;
using UnityEngine;

public class JsonSerializer : ISerializer
{
    public string Serialize<T>(T data)
    {
        try
        {
            return JsonUtility.ToJson(data);
        }
        catch (Exception e)
        {
            Debug.LogError("[Serializer] Error serializing: " + e.Message);
            return null;
        }
    }

    public T Deserialize<T>(string json)
    {
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[Serializer] Error deserializing: " + e.Message);
            return default;
        }
    }
}
