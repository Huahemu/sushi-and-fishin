using System.Collections;
using System.Collections.Generic;
using GameAnalyticsSDK.Net;
using GameAnalyticsSDK.State;
using GameAnalyticsSDK.Utilities;
using UnityEngine;

namespace GameAnalyticsSDK.Wrapper
{
	public class GA_Wrapper
	{
		private class UnityRemoteConfigsListener : IRemoteConfigsListener
		{
			public void OnRemoteConfigsUpdated()
			{
				GameAnalytics.RemoteConfigsUpdated();
			}
		}

		private static readonly UnityRemoteConfigsListener unityRemoteConfigsListener = new UnityRemoteConfigsListener();

		private static void configureAvailableCustomDimensions01(string list)
		{
			IList<object> obj = GA_MiniJSON.Deserialize(list) as IList<object>;
			ArrayList arrayList = new ArrayList();
			foreach (object item in obj)
			{
				arrayList.Add(item);
			}
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureAvailableCustomDimensions01((string[])arrayList.ToArray(typeof(string)));
		}

		private static void configureAvailableCustomDimensions02(string list)
		{
			IList<object> obj = GA_MiniJSON.Deserialize(list) as IList<object>;
			ArrayList arrayList = new ArrayList();
			foreach (object item in obj)
			{
				arrayList.Add(item);
			}
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureAvailableCustomDimensions02((string[])arrayList.ToArray(typeof(string)));
		}

		private static void configureAvailableCustomDimensions03(string list)
		{
			IList<object> obj = GA_MiniJSON.Deserialize(list) as IList<object>;
			ArrayList arrayList = new ArrayList();
			foreach (object item in obj)
			{
				arrayList.Add(item);
			}
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureAvailableCustomDimensions03((string[])arrayList.ToArray(typeof(string)));
		}

		private static void configureAvailableResourceCurrencies(string list)
		{
			IList<object> obj = GA_MiniJSON.Deserialize(list) as IList<object>;
			ArrayList arrayList = new ArrayList();
			foreach (object item in obj)
			{
				arrayList.Add(item);
			}
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureAvailableResourceCurrencies((string[])arrayList.ToArray(typeof(string)));
		}

		private static void configureAvailableResourceItemTypes(string list)
		{
			IList<object> obj = GA_MiniJSON.Deserialize(list) as IList<object>;
			ArrayList arrayList = new ArrayList();
			foreach (object item in obj)
			{
				arrayList.Add(item);
			}
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureAvailableResourceItemTypes((string[])arrayList.ToArray(typeof(string)));
		}

		private static void configureSdkGameEngineVersion(string unitySdkVersion)
		{
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureSdkGameEngineVersion(unitySdkVersion);
		}

		private static void configureGameEngineVersion(string unityEngineVersion)
		{
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureGameEngineVersion(unityEngineVersion);
		}

		private static void configureBuild(string build)
		{
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureBuild(build);
		}

		private static void configureUserId(string userId)
		{
			GameAnalyticsSDK.Net.GameAnalytics.ConfigureUserId(userId);
		}

		private static void initialize(string gamekey, string gamesecret)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddRemoteConfigsListener(unityRemoteConfigsListener);
			GameAnalyticsSDK.Net.GameAnalytics.Initialize(gamekey, gamesecret);
		}

		private static void setCustomDimension01(string customDimension)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetCustomDimension01(customDimension);
		}

		private static void setCustomDimension02(string customDimension)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetCustomDimension02(customDimension);
		}

		private static void setCustomDimension03(string customDimension)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetCustomDimension03(customDimension);
		}

		private static void setGlobalCustomEventFields(string customFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetGlobalCustomEventFields(GA_MiniJSON.Deserialize(customFields) as IDictionary<string, object>);
		}

		private static void addBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddBusinessEvent(currency, amount, itemType, itemId, cartType, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void addResourceEvent(int flowType, string currency, float amount, string itemType, string itemId, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddResourceEvent((EGAResourceFlowType)flowType, currency, amount, itemType, itemId, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void addProgressionEvent(int progressionStatus, string progression01, string progression02, string progression03, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddProgressionEvent((EGAProgressionStatus)progressionStatus, progression01, progression02, progression03, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void addProgressionEventWithScore(int progressionStatus, string progression01, string progression02, string progression03, int score, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddProgressionEvent((EGAProgressionStatus)progressionStatus, progression01, progression02, progression03, score, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void addDesignEvent(string eventId, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddDesignEvent(eventId, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void addDesignEventWithValue(string eventId, float value, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddDesignEvent(eventId, value, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void addErrorEvent(int severity, string message, string fields, bool mergeFields)
		{
			GameAnalyticsSDK.Net.GameAnalytics.AddErrorEvent((EGAErrorSeverity)severity, message, GA_MiniJSON.Deserialize(fields) as IDictionary<string, object>, mergeFields);
		}

		private static void setEnabledInfoLog(bool enabled)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetEnabledInfoLog(enabled);
		}

		private static void setEnabledVerboseLog(bool enabled)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetEnabledVerboseLog(enabled);
		}

		private static void setManualSessionHandling(bool enabled)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetEnabledManualSessionHandling(enabled);
		}

		private static void setEventSubmission(bool enabled)
		{
			GameAnalyticsSDK.Net.GameAnalytics.SetEnabledEventSubmission(enabled);
		}

		private static void gameAnalyticsStartSession()
		{
			GameAnalyticsSDK.Net.GameAnalytics.StartSession();
		}

		private static void gameAnalyticsEndSession()
		{
			GameAnalyticsSDK.Net.GameAnalytics.EndSession();
		}

		private static string getRemoteConfigsValueAsString(string key, string defaultValue)
		{
			return GameAnalyticsSDK.Net.GameAnalytics.GetRemoteConfigsValueAsString(key, defaultValue);
		}

		private static bool isRemoteConfigsReady()
		{
			return GameAnalyticsSDK.Net.GameAnalytics.IsRemoteConfigsReady();
		}

		private static string getRemoteConfigsContentAsString()
		{
			return GameAnalyticsSDK.Net.GameAnalytics.GetRemoteConfigsAsString();
		}

		private static string getABTestingId()
		{
			return GameAnalyticsSDK.Net.GameAnalytics.GetABTestingId();
		}

		private static string getABTestingVariantId()
		{
			return GameAnalyticsSDK.Net.GameAnalytics.GetABTestingVariantId();
		}

		private static void configureAutoDetectAppVersion(bool flag)
		{
		}

		public static string getUserId()
		{
			return GameAnalyticsSDK.Net.GameAnalytics.GetUserId();
		}

		public static void SetAvailableCustomDimensions01(string list)
		{
			configureAvailableCustomDimensions01(list);
		}

		public static void SetAvailableCustomDimensions02(string list)
		{
			configureAvailableCustomDimensions02(list);
		}

		public static void SetAvailableCustomDimensions03(string list)
		{
			configureAvailableCustomDimensions03(list);
		}

		public static void SetAvailableResourceCurrencies(string list)
		{
			configureAvailableResourceCurrencies(list);
		}

		public static void SetAvailableResourceItemTypes(string list)
		{
			configureAvailableResourceItemTypes(list);
		}

		public static void SetUnitySdkVersion(string unitySdkVersion)
		{
			configureSdkGameEngineVersion(unitySdkVersion);
		}

		public static void SetUnityEngineVersion(string unityEngineVersion)
		{
			configureGameEngineVersion(unityEngineVersion);
		}

		public static void SetBuild(string build)
		{
			configureBuild(build);
		}

		public static void SetCustomUserId(string userId)
		{
			configureUserId(userId);
		}

		public static void SetEnabledManualSessionHandling(bool enabled)
		{
			setManualSessionHandling(enabled);
		}

		public static void SetEnabledEventSubmission(bool enabled)
		{
			setEventSubmission(enabled);
		}

		public static void SetEnabledEventSubmission(bool enabled, bool doCache)
		{
			setEventSubmission(enabled);
		}

		public static void SetAutoDetectAppVersion(bool flag)
		{
			configureAutoDetectAppVersion(flag);
		}

		public static void StartSession()
		{
			if (GAState.IsManualSessionHandlingEnabled())
			{
				gameAnalyticsStartSession();
			}
			else
			{
				Debug.Log("Manual session handling is not enabled. \nPlease check the \"Use manual session handling\" option in the \"Advanced\" section of the Settings object.");
			}
		}

		public static void EndSession()
		{
			if (GAState.IsManualSessionHandlingEnabled())
			{
				gameAnalyticsEndSession();
			}
			else
			{
				Debug.Log("Manual session handling is not enabled. \nPlease check the \"Use manual session handling\" option in the \"Advanced\" section of the Settings object.");
			}
		}

		public static void Initialize(string gamekey, string gamesecret)
		{
			initialize(gamekey, gamesecret);
		}

		public static void SetCustomDimension01(string customDimension)
		{
			setCustomDimension01(customDimension);
		}

		public static void SetCustomDimension02(string customDimension)
		{
			setCustomDimension02(customDimension);
		}

		public static void SetCustomDimension03(string customDimension)
		{
			setCustomDimension03(customDimension);
		}

		public static void SetGlobalCustomEventFields(IDictionary<string, object> customFields)
		{
			setGlobalCustomEventFields(DictionaryToJsonString(customFields));
		}

		public static void AddBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addBusinessEvent(currency, amount, itemType, itemId, cartType, fields2, mergeFields);
		}

		public static void AddResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addResourceEvent((int)flowType, currency, amount, itemType, itemId, fields2, mergeFields);
		}

		public static void AddProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addProgressionEvent((int)progressionStatus, progression01, progression02, progression03, fields2, mergeFields);
		}

		public static void AddProgressionEventWithScore(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, int score, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addProgressionEventWithScore((int)progressionStatus, progression01, progression02, progression03, score, fields2, mergeFields);
		}

		public static void AddDesignEvent(string eventID, float eventValue, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addDesignEventWithValue(eventID, eventValue, fields2, mergeFields);
		}

		public static void AddDesignEvent(string eventID, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addDesignEvent(eventID, fields2, mergeFields);
		}

		public static void AddErrorEvent(GAErrorSeverity severity, string message, IDictionary<string, object> fields, bool mergeFields)
		{
			string fields2 = DictionaryToJsonString(fields);
			addErrorEvent((int)severity, message, fields2, mergeFields);
		}

		public static void AddAdEventWithDuration(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, long duration, IDictionary<string, object> fields, bool mergeFields)
		{
			DictionaryToJsonString(fields);
		}

		public static void AddAdEventWithReason(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, GAAdError noAdReason, IDictionary<string, object> fields, bool mergeFields)
		{
			DictionaryToJsonString(fields);
		}

		public static void AddAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, IDictionary<string, object> fields, bool mergeFields)
		{
			DictionaryToJsonString(fields);
		}

		public static void SetInfoLog(bool enabled)
		{
			setEnabledInfoLog(enabled);
		}

		public static void SetVerboseLog(bool enabled)
		{
			setEnabledVerboseLog(enabled);
		}

		public static string GetRemoteConfigsValueAsString(string key, string defaultValue)
		{
			return getRemoteConfigsValueAsString(key, defaultValue);
		}

		public static bool IsRemoteConfigsReady()
		{
			return isRemoteConfigsReady();
		}

		public static string GetRemoteConfigsContentAsString()
		{
			return getRemoteConfigsContentAsString();
		}

		public static string GetRemoteConfigsContentAsJSON()
		{
			return GetRemoteConfigsContentAsString();
		}

		public static string GetABTestingId()
		{
			return getABTestingId();
		}

		public static string GetABTestingVariantId()
		{
			return getABTestingVariantId();
		}

		public static void SetExternalUserId(string userId)
		{
		}

		public static string GetExternalUserId()
		{
			return "";
		}

		private static string DictionaryToJsonString(IDictionary<string, object> dict)
		{
			Hashtable hashtable = new Hashtable();
			if (dict != null)
			{
				foreach (KeyValuePair<string, object> item in dict)
				{
					hashtable.Add(item.Key, item.Value);
				}
			}
			return GA_MiniJSON.Serialize(hashtable);
		}

		public static void StartTimer(string key)
		{
		}

		public static void PauseTimer(string key)
		{
		}

		public static void ResumeTimer(string key)
		{
		}

		public static long StopTimer(string key)
		{
			return 0L;
		}

		public static void EnableSDKInitEvent(bool flag)
		{
		}

		public static void EnableFpsHistogram(bool flag)
		{
		}

		public static void EnableMemoryHistogram(bool flag)
		{
		}

		public static void EnableHealthHardwareInfo(bool flag)
		{
		}
	}
}
