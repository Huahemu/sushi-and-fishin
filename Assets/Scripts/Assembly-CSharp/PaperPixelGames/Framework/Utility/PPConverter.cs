using System.IO;
using UnityEngine;

namespace PaperPixelGames.Framework.Utility
{
	public static class PPConverter
	{
		public static string Serialize<T>(T obj)
		{
			return JsonUtility.ToJson(obj);
		}

		public static T Deserialize<T>(string json)
		{
			return JsonUtility.FromJson<T>(json);
		}

		public static T LoadFromFile<T>(string fileName)
		{
			T result = default;
			string text = Path.Combine(Application.streamingAssetsPath, fileName);
			if (File.Exists(text))
			{
				return Deserialize<T>(File.ReadAllText(text));
			}
			Debug.LogError("File not found at path: " + text);
			return result;
		}
	}
}
