using UnityEngine;

namespace NGS.SLO.Shared
{
	public static class UnityAPI
	{
		public static T[] FindObjectsOfType<T>() where T : Object
		{
			return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
		}

		public static T FindObjectOfType<T>() where T : Object
		{
			return Object.FindAnyObjectByType<T>();
		}
	}
}
