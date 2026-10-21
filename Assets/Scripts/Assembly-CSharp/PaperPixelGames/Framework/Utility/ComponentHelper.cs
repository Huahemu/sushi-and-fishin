using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public static class ComponentHelper
	{
		public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
		{
			T component = gameObject.GetComponent<T>();
			if (component != null)
			{
				return component;
			}
			return gameObject.AddComponent<T>();
		}

		public static T GetOrAddComponent<T>(this Component component) where T : Component
		{
			return component.gameObject.GetOrAddComponent<T>();
		}
	}
}
