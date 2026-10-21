using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityUtils
{
	public static class TransformExtensions
	{
		public static IEnumerable<Transform> Children(this Transform parent)
		{
			foreach (Transform item in parent)
			{
				yield return item;
			}
		}

		public static void Reset(this Transform transform)
		{
			transform.position = Vector3.zero;
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale = Vector3.one;
		}

		public static void DestroyChildren(this Transform parent)
		{
			parent.ForEveryChild((Transform child) =>
			{
				UnityEngine.Object.Destroy(child.gameObject);
			});
		}

		public static void DestroyChildrenImmediate(this Transform parent)
		{
			parent.ForEveryChild((Transform child) =>
			{
				UnityEngine.Object.DestroyImmediate(child.gameObject);
			});
		}

		public static void EnableChildren(this Transform parent)
		{
			parent.ForEveryChild((Transform child) =>
			{
				child.gameObject.SetActive(value: true);
			});
		}

		public static void DisableChildren(this Transform parent)
		{
			parent.ForEveryChild((Transform child) =>
			{
				child.gameObject.SetActive(value: false);
			});
		}

		public static void ForEveryChild(this Transform parent, Action<Transform> action)
		{
			for (int num = parent.childCount - 1; num >= 0; num--)
			{
				action(parent.GetChild(num));
			}
		}

		[Obsolete("Renamed to ForEveryChild")]
		private static void PerformActionOnChildren(this Transform parent, Action<Transform> action)
		{
			parent.ForEveryChild(action);
		}
	}
}
