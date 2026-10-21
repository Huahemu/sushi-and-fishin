namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Reflection;
using global::UnityEngine;

public static class UniqueIDManager
{
	private static Dictionary<string, HashSet<int>> _usedIds = new Dictionary<string, HashSet<int>>();

	public static void AssignUniqueIDs()
	{
		AssignMissingUniqueIDs();
	}

	public static void AssignMissingUniqueIDs()
	{
		_usedIds.Clear();
		List<ScriptableObject> list = GetAllScriptableObjects().ToList();
		int num = 0;
		int num2 = 0;
		foreach (ScriptableObject item in list)
		{
			num2 += ReserveExistingIDs(item);
		}
		foreach (ScriptableObject item2 in list)
		{
			if (AssignUniqueIDToObject(item2))
			{
				num++;
			}
		}
		if (num2 > 0)
		{
			Debug.LogError($"UniqueIDManager: Found {num2} duplicate existing IDs. Existing IDs were not changed.");
		}
		Debug.Log($"UniqueIDManager: Assigned missing unique IDs to {num} ScriptableObjects");
	}

	public static bool AssignUniqueIDToObject(ScriptableObject scriptableObject)
	{
		if (scriptableObject == null)
		{
			return false;
		}
		FieldInfo[] fields = scriptableObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		bool result = false;
		FieldInfo[] array = fields;
		foreach (FieldInfo fieldInfo in array)
		{
			UniqueIDAttribute customAttribute = fieldInfo.GetCustomAttribute<UniqueIDAttribute>();
			if (customAttribute != null && fieldInfo.FieldType == typeof(int))
			{
				string text = (string.IsNullOrEmpty(customAttribute.GroupName) ? "default" : customAttribute.GroupName);
				if (!_usedIds.ContainsKey(text))
				{
					_usedIds[text] = new HashSet<int>();
				}
				if ((int)fieldInfo.GetValue(scriptableObject) <= 0)
				{
					int nextUniqueID = GetNextUniqueID(text);
					fieldInfo.SetValue(scriptableObject, nextUniqueID);
					Debug.Log($"Assigned unique ID {nextUniqueID} to {scriptableObject.name}.{fieldInfo.Name} (Group: {text})");
					result = true;
				}
			}
		}
		return result;
	}

	public static bool ValidateUniqueIDs()
	{
		_usedIds.Clear();
		IEnumerable<ScriptableObject> allScriptableObjects = GetAllScriptableObjects();
		bool flag = false;
		foreach (ScriptableObject item in allScriptableObjects)
		{
			FieldInfo[] fields = item.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				UniqueIDAttribute customAttribute = fieldInfo.GetCustomAttribute<UniqueIDAttribute>();
				if (customAttribute == null || !(fieldInfo.FieldType == typeof(int)))
				{
					continue;
				}
				string text = (string.IsNullOrEmpty(customAttribute.GroupName) ? "default" : customAttribute.GroupName);
				if (!_usedIds.ContainsKey(text))
				{
					_usedIds[text] = new HashSet<int>();
				}
				int num = (int)fieldInfo.GetValue(item);
				if (num > 0)
				{
					if (_usedIds[text].Contains(num))
					{
						Debug.LogError($"Duplicate ID found: {item.name}.{fieldInfo.Name} has ID {num} (Group: {text})");
						flag = true;
					}
					else
					{
						_usedIds[text].Add(num);
					}
				}
			}
		}
		if (!flag)
		{
			Debug.Log("UniqueIDManager: All IDs are unique!");
		}
		return !flag;
	}

	private static int GetNextUniqueID(string groupName)
	{
		if (!_usedIds.ContainsKey(groupName))
		{
			_usedIds[groupName] = new HashSet<int>();
		}
		HashSet<int> hashSet = _usedIds[groupName];
		int i;
		for (i = 1; hashSet.Contains(i); i++)
		{
		}
		hashSet.Add(i);
		return i;
	}

	public static HashSet<int> GetUsedIDs(string groupName = "default")
	{
		if (_usedIds.ContainsKey(groupName))
		{
			return new HashSet<int>(_usedIds[groupName]);
		}
		return new HashSet<int>();
	}

	private static int ReserveExistingIDs(ScriptableObject scriptableObject)
	{
		if (scriptableObject == null)
		{
			return 0;
		}
		FieldInfo[] fields = scriptableObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		int num = 0;
		FieldInfo[] array = fields;
		foreach (FieldInfo fieldInfo in array)
		{
			UniqueIDAttribute customAttribute = fieldInfo.GetCustomAttribute<UniqueIDAttribute>();
			if (customAttribute != null && !(fieldInfo.FieldType != typeof(int)))
			{
				string text = (string.IsNullOrEmpty(customAttribute.GroupName) ? "default" : customAttribute.GroupName);
				if (!_usedIds.ContainsKey(text))
				{
					_usedIds[text] = new HashSet<int>();
				}
				int num2 = (int)fieldInfo.GetValue(scriptableObject);
				if (num2 > 0 && !_usedIds[text].Add(num2))
				{
					Debug.LogError($"Duplicate existing ID found: {scriptableObject.name}.{fieldInfo.Name} has ID {num2} (Group: {text}). Existing IDs were not changed.");
					num++;
				}
			}
		}
		return num;
	}

	private static IEnumerable<ScriptableObject> GetAllScriptableObjects()
	{
		ScriptableObject[] array = SushiAndFish.Casino.Original.CasinoResources.FindObjectsOfTypeAll<ScriptableObject>();
		ScriptableObject[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			yield return array2[i];
		}
	}
}

}
