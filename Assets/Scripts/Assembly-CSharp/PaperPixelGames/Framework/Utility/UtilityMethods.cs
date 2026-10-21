using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public static class UtilityMethods
	{
		private static int uniqueIdCounter;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void ConfigureReleaseLogging()
		{
			if (!UnityEngine.Debug.isDebugBuild)
			{
				Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
			}
		}

		public static string GetDescription(this Enum value)
		{
			return value.GetAttribute<DescriptionAttribute>()?.Description;
		}

		private static TAttribute GetAttribute<TAttribute>(this Enum value) where TAttribute : Attribute
		{
			Type type = value.GetType();
			string name = Enum.GetName(type, value);
			return type.GetField(name).GetCustomAttributes(inherit: false).OfType<TAttribute>()
				.SingleOrDefault();
		}

		public static float MapFloatValue(this float inputValue, float inputRangeStart, float inputRangeEnd, float outputRangeStart, float outputRangeEnd)
		{
			return (inputValue - inputRangeStart) / (inputRangeEnd - inputRangeStart) * (outputRangeEnd - outputRangeStart) + outputRangeStart;
		}

		public static void LogCallerInfo(object obj, string prefix = "[INFO]")
		{
			string name = new StackTrace().GetFrame(1).GetMethod().Name;
			string name2 = obj.GetType().Name;
			Log(prefix + " - " + name2 + " -> " + name);
		}

		[Conditional("ENABLE_VERBOSE_GAMEPLAY_LOG")]
		public static void Trace(object message)
		{
			Log(message);
		}

		public static void Log(object debug, LogType logType = LogType.Log)
		{
			switch (logType)
			{
			case LogType.Error:
				UnityEngine.Debug.LogError(debug);
				break;
			case LogType.Warning:
				UnityEngine.Debug.LogWarning(debug);
				break;
			case LogType.Log:
				UnityEngine.Debug.Log(debug);
				break;
			case LogType.Exception:
				if (debug is Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
				else
				{
					UnityEngine.Debug.LogError(debug);
				}
				break;
			default:
				UnityEngine.Debug.Log(debug);
				break;
			case LogType.Assert:
				break;
			}
		}

		public static char GetAlphabetLetter(this int num)
		{
			return Convert.ToChar(num + 64);
		}

		public static void CopyToClipboard(this string str)
		{
			GUIUtility.systemCopyBuffer = str;
		}

		public static void SetLeft(this RectTransform rt, float left)
		{
			rt.offsetMin = new Vector2(left, rt.offsetMin.y);
		}

		public static void SetRight(this RectTransform rt, float right)
		{
			rt.offsetMax = new Vector2(0f - right, rt.offsetMax.y);
		}

		public static void SetTop(this RectTransform rt, float top)
		{
			rt.offsetMax = new Vector2(rt.offsetMax.x, 0f - top);
		}

		public static void SetBottom(this RectTransform rt, float bottom)
		{
			rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
		}

		public static ushort GenerateUniqueIdentifier()
		{
			while (true)
			{
				try
				{
					long num = DateTimeOffset.Now.ToUnixTimeMilliseconds();
					using RNGCryptoServiceProvider rNGCryptoServiceProvider = new RNGCryptoServiceProvider();
					byte[] array = new byte[4];
					rNGCryptoServiceProvider.GetBytes(array);
					return (ushort)(((num << 32) | BitConverter.ToUInt32(array, 0)) & 0xFFFF);
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogWarning("GenerateUniqueIdentifier error: " + ex.Message);
				}
			}
		}

		public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
		{
			foreach (T item in source)
			{
				action(item);
			}
			return source;
		}

		public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
		{
			return (value - fromMin) / (fromMax - fromMin) * (toMax - toMin) + toMin;
		}

		public static int GenerateUniqueId()
		{
			return (Guid.NewGuid().ToString().GetHashCode() & 0xFFFFFFF) ^ uniqueIdCounter++;
		}

		public static void SetLayerRecursive(GameObject obj, string layer, bool includeChildren)
		{
			obj.layer = LayerMask.NameToLayer(layer);
			if (!includeChildren)
			{
				return;
			}
			foreach (Transform item in obj.transform)
			{
				SetLayerRecursive(item.gameObject, layer, includeChildren: true);
			}
		}

		public static Tweener DOBlendShapeWeight(this SkinnedMeshRenderer renderer, int index, float endValue, float duration, float delay = 0f)
		{
			float startValue = renderer.GetBlendShapeWeight(index);
			return DOTween.To(() => startValue, (float x) =>
			{
				startValue = x;
				renderer.SetBlendShapeWeight(index, x);
			}, endValue, duration).SetDelay(delay);
		}

		public static void FireAndForget(this UniTask task, string tag)
		{
			task.Forget((Exception e) =>
			{
				Log($"{tag}\n  <color=#F92672>Task Faulted</color>\n    {e}");
			});
		}
	}
}
