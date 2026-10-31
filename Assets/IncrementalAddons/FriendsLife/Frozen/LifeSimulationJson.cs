using System;
#if LIFE_SIMULATION_STANDALONE
using System.Text.Json;
#else
using UnityEngine;
#endif

namespace TomodachiLife.Simulation
{
    public static class LifeSimulationJson
    {
        public static T Read<T>(string json)
        {
#if LIFE_SIMULATION_STANDALONE
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true });
#else
            return JsonUtility.FromJson<T>(json);
#endif
        }
        public static string Write(object value, bool pretty = true)
        {
#if LIFE_SIMULATION_STANDALONE
            return JsonSerializer.Serialize(value, value.GetType(), new JsonSerializerOptions { IncludeFields = true, WriteIndented = pretty });
#else
            return JsonUtility.ToJson(value, pretty);
#endif
        }
    }
}
