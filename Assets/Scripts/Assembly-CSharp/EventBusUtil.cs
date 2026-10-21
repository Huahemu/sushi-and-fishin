using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class EventBusUtil
{
	public static IReadOnlyList<Type> EventTypes { get; set; }

	public static IReadOnlyList<Type> EventBusTypes { get; set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	public static void Initialize()
	{
		EventTypes = PredefinedAssemblyUtil.GetTypes(typeof(IEvent));
		EventBusTypes = InitializeAllBuses();
	}

	private static List<Type> InitializeAllBuses()
	{
		List<Type> list = new List<Type>();
		Type typeFromHandle = typeof(EventBus<>);
		foreach (Type eventType in EventTypes)
		{
			Type item = typeFromHandle.MakeGenericType(eventType);
			list.Add(item);
		}
		return list;
	}

	public static void ClearAllBuses()
	{
		for (int i = 0; i < EventBusTypes.Count; i++)
		{
			EventBusTypes[i].GetMethod("Clear", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
		}
	}
}
