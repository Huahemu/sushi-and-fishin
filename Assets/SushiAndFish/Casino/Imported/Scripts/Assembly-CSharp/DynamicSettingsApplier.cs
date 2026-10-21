namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Globalization;
using global::System.Reflection;
using global::UnityEngine;

public static class DynamicSettingsApplier
{
	[Serializable]
	private class AuxiliaryMoneyData
	{
		public float firstDay;

		public float secondDay;

		public float thirdDay;
	}

	[Serializable]
	private class ItemPricesWrapper
	{
		public ItemPriceDataJson[] items;
	}

	[Serializable]
	private class ItemPriceDataJson
	{
		public int basePrice;

		public int priceIncreasePerFloor;
	}

	[Serializable]
	private class JsonArrayWrapper<T>
	{
		public T[] items;
	}

	private static readonly Dictionary<Type, ScriptableObject> _settingsCache = new Dictionary<Type, ScriptableObject>();

	public static bool debugMode = false;

	public static void ApplySettings(SettingResponse[] settings)
	{
		GameSettings orLoadSettings = GetOrLoadSettings<GameSettings>("GameSettings");
		debugMode = orLoadSettings != null && orLoadSettings.apiDebugMode;
		foreach (SettingResponse settingResponse in settings)
		{
			try
			{
				ApplySetting(settingResponse);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[DynamicSettingsApplier] Error processing setting '" + settingResponse.setting_key + "': " + ex.Message);
			}
		}
		foreach (ScriptableObject value in _settingsCache.Values)
		{
			if (!(value is GameSettings { useAPIUpdates: false }))
			{
				value.GetType().GetMethod("NotifyChanged", BindingFlags.Instance | BindingFlags.Public)?.Invoke(value, null);
			}
		}
		_settingsCache.Clear();
	}

	private static void ApplySetting(SettingResponse setting)
	{
		string setting_key = setting.setting_key;
		string text = NormalizeKey(setting_key);
		if (text.StartsWith("floor") && setting.setting_type == "json")
		{
			ApplyFloorData(setting_key, setting.setting_value);
			return;
		}
		if (text == "itemprices" && setting.setting_type == "json")
		{
			ApplyItemPrices(setting.setting_value);
			return;
		}
		if ((text == "auxiliarymoneypercentage" || text == "auxillarymoneypercentage") && setting.setting_type == "json")
		{
			ApplyAuxiliaryMoneyPercentage(setting.setting_value);
			return;
		}
		ScriptableObject scriptableObject = null;
		FieldInfo fieldInfo = null;
		GameSettings orLoadSettings = GetOrLoadSettings<GameSettings>("GameSettings");
		if (orLoadSettings != null && orLoadSettings.useAPIUpdates)
		{
			fieldInfo = FindField(orLoadSettings.GetType(), text);
			if (fieldInfo != null)
			{
				scriptableObject = orLoadSettings;
			}
		}
		if (fieldInfo == null)
		{
			LobbySettings orLoadSettings2 = GetOrLoadSettings<LobbySettings>("LobbySettings");
			if (orLoadSettings2 != null)
			{
				fieldInfo = FindField(orLoadSettings2.GetType(), text);
				if (fieldInfo != null)
				{
					scriptableObject = orLoadSettings2;
				}
			}
		}
		if (fieldInfo == null)
		{
			PlayerSettings orLoadSettings3 = GetOrLoadSettings<PlayerSettings>("PlayerSettings");
			if (orLoadSettings3 != null)
			{
				fieldInfo = FindField(orLoadSettings3.GetType(), text);
				if (fieldInfo != null)
				{
					scriptableObject = orLoadSettings3;
				}
			}
		}
		if (fieldInfo == null)
		{
			ChallengeSettings orLoadSettings4 = GetOrLoadSettings<ChallengeSettings>("ChallengeSettings");
			if (orLoadSettings4 != null)
			{
				fieldInfo = FindField(orLoadSettings4.GetType(), text);
				if (fieldInfo != null)
				{
					scriptableObject = orLoadSettings4;
				}
			}
		}
		if (fieldInfo == null)
		{
			ItemPriceSettings orLoadSettings5 = GetOrLoadSettings<ItemPriceSettings>("ItemPriceSettings");
			if (orLoadSettings5 != null)
			{
				fieldInfo = FindField(orLoadSettings5.GetType(), text);
				if (fieldInfo != null)
				{
					scriptableObject = orLoadSettings5;
				}
			}
		}
		if (fieldInfo == null || scriptableObject == null)
		{
			Debug.LogWarning("[DynamicSettingsApplier] No matching field found for setting key: '" + setting_key + "'");
			return;
		}
		_settingsCache[scriptableObject.GetType()] = scriptableObject;
		object obj = ParseValue(setting.setting_value, setting.setting_type, fieldInfo.FieldType);
		if (obj != null)
		{
			fieldInfo.SetValue(scriptableObject, obj);
			LogDebug($"[DynamicSettingsApplier] Applied '{setting_key}' = {obj} to {scriptableObject.GetType().Name}.{fieldInfo.Name}");
		}
	}

	private static void LogDebug(string message)
	{
		if (debugMode)
		{
			Debug.Log(message);
		}
	}

	private static T GetOrLoadSettings<T>(string resourceName) where T : ScriptableObject
	{
		return SushiAndFish.Casino.Original.CasinoResources.Load<T>(resourceName);
	}

	private static FieldInfo FindField(Type type, string normalizedKey)
	{
		FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (NormalizeKey(fieldInfo.Name) == normalizedKey)
			{
				return fieldInfo;
			}
		}
		return null;
	}

	private static string NormalizeKey(string key)
	{
		return key.ToLower().Replace("_", "").Replace("-", "");
	}

	private static object ParseValue(string value, string settingType, Type targetType)
	{
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		try
		{
			if (settingType == "json")
			{
				return ParseJsonValue(value, targetType);
			}
			if (targetType == typeof(int))
			{
				if (int.TryParse(value, out var result))
				{
					return result;
				}
			}
			else if (targetType == typeof(long))
			{
				if (long.TryParse(value, out var result2))
				{
					return result2;
				}
			}
			else if (targetType == typeof(float))
			{
				if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
				{
					return result3;
				}
			}
			else if (targetType == typeof(double))
			{
				if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
				{
					return result4;
				}
			}
			else if (targetType == typeof(bool))
			{
				if (bool.TryParse(value, out var result5))
				{
					return result5;
				}
			}
			else
			{
				if (targetType == typeof(string))
				{
					return value;
				}
				if (targetType.IsArray)
				{
					return ParseArrayValue(value, settingType, targetType);
				}
				if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
				{
					return ParseListValue(value, settingType, targetType);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[DynamicSettingsApplier] Error parsing value '" + value + "' to type " + targetType.Name + ": " + ex.Message);
		}
		return null;
	}

	private static object ParseJsonValue(string jsonValue, Type targetType)
	{
		if (targetType.IsArray)
		{
			return ParseArrayFromJson(jsonValue, targetType);
		}
		if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
		{
			return ParseListFromJson(jsonValue, targetType);
		}
		try
		{
			return JsonUtility.FromJson(jsonValue, targetType);
		}
		catch
		{
			Debug.LogWarning("[DynamicSettingsApplier] Could not parse JSON to type " + targetType.Name);
			return null;
		}
	}

	private static object ParseArrayValue(string value, string settingType, Type arrayType)
	{
		if (settingType == "json")
		{
			return ParseArrayFromJson(value, arrayType);
		}
		Type elementType = arrayType.GetElementType();
		string[] array = value.Split(',');
		Array array2 = Array.CreateInstance(elementType, array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			object obj = ParseValue(array[i].Trim(), "number", elementType);
			if (obj != null)
			{
				array2.SetValue(obj, i);
			}
		}
		return array2;
	}

	private static object ParseListValue(string value, string settingType, Type listType)
	{
		Type targetType = listType.GetGenericArguments()[0];
		object obj = Activator.CreateInstance(listType);
		if (settingType == "json")
		{
			return ParseListFromJson(value, listType);
		}
		MethodInfo method = listType.GetMethod("Add");
		string[] array = value.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			object obj2 = ParseValue(array[i].Trim(), "number", targetType);
			if (obj2 != null)
			{
				method.Invoke(obj, new object[1] { obj2 });
			}
		}
		return obj;
	}

	private static object ParseArrayFromJson(string jsonValue, Type arrayType)
	{
		Type elementType = arrayType.GetElementType();
		try
		{
			if (elementType == typeof(float) || elementType == typeof(int) || elementType == typeof(long))
			{
				return ParseNumberedFieldsArray(jsonValue, arrayType);
			}
			Type type = typeof(JsonArrayWrapper<>).MakeGenericType(elementType);
			object obj = JsonUtility.FromJson(jsonValue, type);
			FieldInfo field = type.GetField("items", BindingFlags.Instance | BindingFlags.Public);
			if (field != null)
			{
				object value = field.GetValue(obj);
				if (value != null && value is IList list)
				{
					Array array = Array.CreateInstance(elementType, list.Count);
					list.CopyTo(array, 0);
					return array;
				}
			}
		}
		catch
		{
		}
		return ParseNumberedFieldsArray(jsonValue, arrayType);
	}

	private static object ParseListFromJson(string jsonValue, Type listType)
	{
		Type type = listType.GetGenericArguments()[0];
		object obj = Activator.CreateInstance(listType);
		MethodInfo method = listType.GetMethod("Add");
		try
		{
			Type type2 = typeof(JsonArrayWrapper<>).MakeGenericType(type);
			object obj2 = JsonUtility.FromJson(jsonValue, type2);
			FieldInfo field = type2.GetField("items", BindingFlags.Instance | BindingFlags.Public);
			if (field != null && field.GetValue(obj2) is IList list)
			{
				foreach (object item in list)
				{
					method.Invoke(obj, new object[1] { item });
				}
				return obj;
			}
		}
		catch
		{
		}
		if (type == typeof(float) || type == typeof(int) || type == typeof(long))
		{
			object obj4 = ParseNumberedFieldsArray(jsonValue, type.MakeArrayType());
			if (obj4 != null)
			{
				foreach (object item2 in obj4 as IList)
				{
					method.Invoke(obj, new object[1] { item2 });
				}
				return obj;
			}
		}
		return null;
	}

	private static object ParseNumberedFieldsArray(string jsonValue, Type arrayType)
	{
		Type elementType = arrayType.GetElementType();
		List<object> list = new List<object>();
		bool flag = false;
		for (int i = 0; i < 100; i++)
		{
			string fieldName = $"quota{i}";
			float? num = ExtractFieldValue<float>(jsonValue, fieldName);
			if (num.HasValue)
			{
				list.Add(Convert.ChangeType(num.Value, elementType));
				flag = true;
			}
			else if (flag)
			{
				break;
			}
		}
		if (list.Count == 0)
		{
			string[] array = new string[3] { "firstDay", "secondDay", "thirdDay" };
			foreach (string fieldName2 in array)
			{
				float? num2 = ExtractFieldValue<float>(jsonValue, fieldName2);
				if (num2.HasValue)
				{
					list.Add(Convert.ChangeType(num2.Value, elementType));
				}
			}
		}
		if (list.Count > 0)
		{
			Array array2 = Array.CreateInstance(elementType, list.Count);
			for (int k = 0; k < list.Count; k++)
			{
				array2.SetValue(list[k], k);
			}
			return array2;
		}
		return null;
	}

	private static T? ExtractFieldValue<T>(string json, string fieldName) where T : struct
	{
		try
		{
			string value = "\"" + fieldName + "\"";
			int num = json.IndexOf(value, StringComparison.Ordinal);
			if (num == -1)
			{
				return null;
			}
			int num2 = json.IndexOf(':', num);
			if (num2 == -1)
			{
				return null;
			}
			int i;
			for (i = num2 + 1; i < json.Length && char.IsWhiteSpace(json[i]); i++)
			{
			}
			if (i >= json.Length)
			{
				return null;
			}
			int j = i;
			bool flag = false;
			for (; j < json.Length; j++)
			{
				char c = json[j];
				if (c == '"')
				{
					flag = !flag;
				}
				else if (!flag && (c == ',' || c == '}'))
				{
					break;
				}
			}
			if (j > i)
			{
				string s = json.Substring(i, j - i).Trim().TrimEnd(',', '}');
				if (typeof(T) == typeof(float) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					return (T)(object)result;
				}
				if (typeof(T) == typeof(int) && int.TryParse(s, out var result2))
				{
					return (T)(object)result2;
				}
				if (typeof(T) == typeof(long) && long.TryParse(s, out var result3))
				{
					return (T)(object)result3;
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[DynamicSettingsApplier] Error extracting field '" + fieldName + "': " + ex.Message);
		}
		return null;
	}

	private static void ApplyFloorData(string floorKey, string jsonValue)
	{
		GameSettings orLoadSettings = GetOrLoadSettings<GameSettings>("GameSettings");
		if (orLoadSettings == null || !orLoadSettings.useAPIUpdates)
		{
			return;
		}
		try
		{
			if (!int.TryParse(floorKey.Substring(5), out var result))
			{
				Debug.LogWarning("[DynamicSettingsApplier] Could not parse floor index from '" + floorKey + "'");
				return;
			}
			int num = result - 1;
			while (orLoadSettings.floorData.Count <= num)
			{
				orLoadSettings.floorData.Add(new GameSettings.CasinoFloorData());
			}
			GameSettings.CasinoFloorData obj = orLoadSettings.floorData[num];
			Type typeFromHandle = typeof(GameSettings.CasinoFloorData);
			FieldInfo[] fields = typeFromHandle.GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (FieldInfo fieldInfo in fields)
			{
				string name = fieldInfo.Name;
				if (fieldInfo.FieldType == typeof(float))
				{
					float? num2 = ExtractFieldValue<float>(jsonValue, name);
					if (num2.HasValue)
					{
						fieldInfo.SetValue(obj, num2.Value);
					}
				}
				else if (fieldInfo.FieldType == typeof(int))
				{
					int? num3 = ExtractFieldValue<int>(jsonValue, name);
					if (num3.HasValue)
					{
						fieldInfo.SetValue(obj, num3.Value);
					}
				}
				else if (fieldInfo.FieldType == typeof(long))
				{
					long? num4 = ExtractFieldValue<long>(jsonValue, name);
					if (num4.HasValue)
					{
						fieldInfo.SetValue(obj, num4.Value);
					}
				}
			}
			FieldInfo field = typeFromHandle.GetField("shreddingBodyPrice");
			if (field != null)
			{
				float? num5 = ExtractFieldValue<float>(jsonValue, "ShreddingBodyPrice");
				float? num6 = ExtractFieldValue<float>(jsonValue, "shreddingBodyPrice");
				if (num5.HasValue && num5.Value != 0f)
				{
					field.SetValue(obj, num5.Value);
				}
				else if (num6.HasValue)
				{
					field.SetValue(obj, num6.Value);
				}
			}
			_settingsCache[typeof(GameSettings)] = orLoadSettings;
			LogDebug($"[DynamicSettingsApplier] Updated floor {num} from '{floorKey}'");
		}
		catch (Exception ex)
		{
			Debug.LogError("[DynamicSettingsApplier] Error parsing floor data '" + floorKey + "': " + ex.Message);
		}
	}

	private static void ApplyItemPrices(string jsonValue)
	{
		ItemPriceSettings orLoadSettings = GetOrLoadSettings<ItemPriceSettings>("ItemPriceSettings");
		if (orLoadSettings == null)
		{
			return;
		}
		try
		{
			Type typeFromHandle = typeof(ItemPriceSettings);
			FieldInfo field = typeFromHandle.GetField("itemPrices", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
			{
				Debug.LogError("[DynamicSettingsApplier] Could not find itemPrices field in ItemPriceSettings");
				return;
			}
			if (!(field.GetValue(orLoadSettings) is IList list))
			{
				Debug.LogError("[DynamicSettingsApplier] itemPrices field is not a list");
				return;
			}
			Type nestedType = typeFromHandle.GetNestedType("ItemPriceData", BindingFlags.Public | BindingFlags.NonPublic);
			if (nestedType == null)
			{
				Debug.LogError("[DynamicSettingsApplier] Could not find ItemPriceData nested type");
				return;
			}
			FieldInfo field2 = nestedType.GetField("basePrice");
			FieldInfo field3 = nestedType.GetField("priceIncreasePerFloor");
			int num = 0;
			for (int i = 0; i < 100; i++)
			{
				string text = i.ToString();
				string value = "\"" + text + "\"";
				int num2 = jsonValue.IndexOf(value, StringComparison.Ordinal);
				if (num2 == -1)
				{
					break;
				}
				int num3 = jsonValue.IndexOf(':', num2);
				if (num3 == -1)
				{
					continue;
				}
				int j;
				for (j = num3 + 1; j < jsonValue.Length && char.IsWhiteSpace(jsonValue[j]); j++)
				{
				}
				if (j >= jsonValue.Length || jsonValue[j] != '"')
				{
					continue;
				}
				j++;
				int k;
				for (k = j; k < jsonValue.Length && (jsonValue[k] != '"' || (k != 0 && jsonValue[k - 1] == '\\')); k++)
				{
				}
				if (k >= jsonValue.Length)
				{
					continue;
				}
				string text2 = jsonValue.Substring(j, k - j);
				text2 = text2.Replace("\\\"", "\"");
				string text3 = "{" + text2 + "}";
				try
				{
					ItemPriceDataJson itemPriceDataJson = JsonUtility.FromJson<ItemPriceDataJson>(text3);
					if (itemPriceDataJson == null || i >= list.Count)
					{
						continue;
					}
					object obj = list[i];
					if (obj != null)
					{
						if (field2 != null)
						{
							field2.SetValue(obj, itemPriceDataJson.basePrice);
						}
						if (field3 != null)
						{
							field3.SetValue(obj, itemPriceDataJson.priceIncreasePerFloor);
						}
						num++;
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[DynamicSettingsApplier] Failed to parse item price at index {i}: {text3}. Error: {ex.Message}");
				}
			}
			FieldInfo field4 = typeFromHandle.GetField("_priceCache", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field4 != null)
			{
				field4.SetValue(orLoadSettings, null);
			}
			_settingsCache[typeof(ItemPriceSettings)] = orLoadSettings;
			LogDebug($"[DynamicSettingsApplier] Updated {num} item prices");
		}
		catch (Exception ex2)
		{
			Debug.LogError("[DynamicSettingsApplier] Error parsing item prices: " + ex2.Message + "\n" + ex2.StackTrace);
		}
	}

	private static void ApplyAuxiliaryMoneyPercentage(string jsonValue)
	{
		GameSettings orLoadSettings = GetOrLoadSettings<GameSettings>("GameSettings");
		if (orLoadSettings == null || !orLoadSettings.useAPIUpdates)
		{
			return;
		}
		try
		{
			AuxiliaryMoneyData auxiliaryMoneyData = JsonUtility.FromJson<AuxiliaryMoneyData>(jsonValue);
			if (auxiliaryMoneyData == null)
			{
				Debug.LogWarning("[DynamicSettingsApplier] Could not parse auxiliaryMoneyPercentage JSON");
				return;
			}
			if (orLoadSettings.auxiliaryMoneyPercentage == null || orLoadSettings.auxiliaryMoneyPercentage.Length < 3)
			{
				orLoadSettings.auxiliaryMoneyPercentage = new float[3];
			}
			orLoadSettings.auxiliaryMoneyPercentage[0] = auxiliaryMoneyData.firstDay;
			orLoadSettings.auxiliaryMoneyPercentage[1] = auxiliaryMoneyData.secondDay;
			orLoadSettings.auxiliaryMoneyPercentage[2] = auxiliaryMoneyData.thirdDay;
			_settingsCache[typeof(GameSettings)] = orLoadSettings;
			LogDebug($"[DynamicSettingsApplier] Updated auxiliaryMoneyPercentage: [{auxiliaryMoneyData.firstDay}, {auxiliaryMoneyData.secondDay}, {auxiliaryMoneyData.thirdDay}]");
		}
		catch (Exception ex)
		{
			Debug.LogError("[DynamicSettingsApplier] Error parsing auxiliaryMoneyPercentage: " + ex.Message + "\n" + ex.StackTrace);
		}
	}
}

}
