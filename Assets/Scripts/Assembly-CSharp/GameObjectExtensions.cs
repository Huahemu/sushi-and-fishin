using UnityEngine;

public static class GameObjectExtensions
{
	public static T OrNull<T>(this T obj) where T : Object
	{
		if (!obj)
		{
			return null;
		}
		return obj;
	}
}
