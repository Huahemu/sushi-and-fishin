using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using GameAnalyticsSDK.Events;
using GameAnalyticsSDK.Net;
using GameAnalyticsSDK.Setup;
using GameAnalyticsSDK.State;
using GameAnalyticsSDK.Wrapper;
using UnityEngine;

namespace GameAnalyticsSDK
{
	[RequireComponent(typeof(GA_SpecialEvents))]
	[ExecuteInEditMode]
	public class GameAnalytics : MonoBehaviour
	{
		private static Settings _settings;

		private static GameAnalytics _instance;

		private static bool _hasInitializeBeenCalled;

		public static Settings SettingsGA
		{
			get
			{
				if (_settings == null)
				{
					InitAPI();
				}
				return _settings;
			}
			private set
			{
				_settings = value;
			}
		}

		public static bool Initialized => _hasInitializeBeenCalled;

		public static event EventHandler<bool> onInitialize;

		public static event Action OnRemoteConfigsUpdatedEvent;

		private void OnEnable()
		{
			Application.logMessageReceived += GA_Debug.HandleLog;
		}

		private void OnDisable()
		{
			Application.logMessageReceived -= GA_Debug.HandleLog;
		}

		public void Awake()
		{
			if (Application.isPlaying)
			{
				if (_instance != null)
				{
					Debug.LogWarning("Destroying duplicate GameAnalytics object - only one is allowed per scene!");
					UnityEngine.Object.Destroy(base.gameObject);
				}
				else
				{
					_instance = this;
					UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
				}
			}
		}

		private void OnDestroy()
		{
			if (Application.isPlaying && _instance == this)
			{
				_instance = null;
			}
		}

		private void OnApplicationQuit()
		{
			GameAnalyticsSDK.Net.GameAnalytics.OnQuit();
			Thread.Sleep(1500);
		}

		private static void InitAPI()
		{
			try
			{
				_settings = (Settings)Resources.Load("GameAnalytics/Settings", typeof(Settings));
				GAState.Init();
			}
			catch (Exception ex)
			{
				Debug.Log("Error getting Settings in InitAPI: " + ex.Message);
			}
		}

		private static void InternalInitialize()
		{
			if (!Application.isPlaying)
			{
				return;
			}
			if (SettingsGA.InfoLogBuild)
			{
				GA_Setup.SetInfoLog(enabled: true);
			}
			if (SettingsGA.VerboseLogBuild)
			{
				GA_Setup.SetVerboseLog(enabled: true);
			}
			int platformIndex = GetPlatformIndex();
			GA_Wrapper.SetUnitySdkVersion("unity " + Settings.VERSION);
			GA_Wrapper.SetUnityEngineVersion("unity " + GetUnityVersion());
			if (platformIndex >= 0)
			{
				if (SettingsGA.UsePlayerSettingsBuildNumber)
				{
					for (int i = 0; i < SettingsGA.Platforms.Count; i++)
					{
						if (SettingsGA.Platforms[i] == RuntimePlatform.Android || SettingsGA.Platforms[i] == RuntimePlatform.IPhonePlayer)
						{
							SettingsGA.Build[i] = Application.version;
						}
					}
					if (SettingsGA.Platforms[platformIndex] == RuntimePlatform.Android || SettingsGA.Platforms[platformIndex] == RuntimePlatform.IPhonePlayer)
					{
						GA_Wrapper.SetAutoDetectAppVersion(flag: true);
					}
					else
					{
						GA_Wrapper.SetBuild(SettingsGA.Build[platformIndex]);
					}
				}
				else
				{
					GA_Wrapper.SetBuild(SettingsGA.Build[platformIndex]);
				}
			}
			if (SettingsGA.CustomDimensions01.Count > 0)
			{
				GA_Setup.SetAvailableCustomDimensions01(SettingsGA.CustomDimensions01);
			}
			if (SettingsGA.CustomDimensions02.Count > 0)
			{
				GA_Setup.SetAvailableCustomDimensions02(SettingsGA.CustomDimensions02);
			}
			if (SettingsGA.CustomDimensions03.Count > 0)
			{
				GA_Setup.SetAvailableCustomDimensions03(SettingsGA.CustomDimensions03);
			}
			if (SettingsGA.ResourceItemTypes.Count > 0)
			{
				GA_Setup.SetAvailableResourceItemTypes(SettingsGA.ResourceItemTypes);
			}
			if (SettingsGA.ResourceCurrencies.Count > 0)
			{
				GA_Setup.SetAvailableResourceCurrencies(SettingsGA.ResourceCurrencies);
			}
			if (SettingsGA.UseManualSessionHandling)
			{
				SetEnabledManualSessionHandling(enabled: true);
			}
			EnableSDKInitEvent(SettingsGA.EnableSDKInitEvent);
			EnableFpsHistogram(SettingsGA.EnableFPSHistogram);
			EnableMemoryHistogram(SettingsGA.EnableMemoryHistogram);
			EnableHealthHardwareInfo(SettingsGA.EnableHardwareTracking);
		}

		public static void Initialize()
		{
			InternalInitialize();
			int platformIndex = GetPlatformIndex();
			if (platformIndex >= 0)
			{
				GA_Wrapper.Initialize(SettingsGA.GetGameKey(platformIndex), SettingsGA.GetSecretKey(platformIndex));
				_hasInitializeBeenCalled = true;
				onInitialize?.Invoke(typeof(GameAnalytics), e: true);
			}
			else
			{
				_hasInitializeBeenCalled = true;
				Debug.LogWarning("GameAnalytics: Unsupported platform (events will not be sent in editor; or missing platform in settings): " + Application.platform);
				onInitialize?.Invoke(typeof(GameAnalytics), e: false);
			}
		}

		public static void NewBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Business.NewEvent(currency, amount, itemType, itemId, cartType, null, mergeFields: false);
			}
		}

		public static void NewBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Business.NewEvent(currency, amount, itemType, itemId, cartType, customFields, mergeFields);
			}
		}

		public static void NewDesignEvent(string eventName)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Design.NewEvent(eventName, null, mergeFields: false);
			}
		}

		public static void NewDesignEvent(string eventName, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Design.NewEvent(eventName, customFields, mergeFields);
			}
		}

		public static void NewDesignEvent(string eventName, float eventValue)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Design.NewEvent(eventName, eventValue, null, mergeFields: false);
			}
		}

		public static void NewDesignEvent(string eventName, float eventValue, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Design.NewEvent(eventName, eventValue, customFields, mergeFields);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, null, mergeFields: false);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, customFields, mergeFields);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, null, mergeFields: false);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, customFields, mergeFields);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, progression03, null, mergeFields: false);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, progression03, customFields, mergeFields);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, int score)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, score, null, mergeFields: false);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, int score, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, score, customFields, mergeFields);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, int score)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, score, null, mergeFields: false);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, int score, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, score, customFields, mergeFields);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, int score)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, progression03, score, null, mergeFields: false);
			}
		}

		public static void NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, int score, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Progression.NewEvent(progressionStatus, progression01, progression02, progression03, score, customFields, mergeFields);
			}
		}

		public static void NewResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Resource.NewEvent(flowType, currency, amount, itemType, itemId, null, mergeFields: false);
			}
		}

		public static void NewResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Resource.NewEvent(flowType, currency, amount, itemType, itemId, customFields, mergeFields);
			}
		}

		public static void NewErrorEvent(GAErrorSeverity severity, string message)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Error.NewEvent(severity, message, null, mergeFields: false);
			}
		}

		public static void NewErrorEvent(GAErrorSeverity severity, string message, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Error.NewEvent(severity, message, customFields, mergeFields);
			}
		}

		public static void NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, long duration)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Ads.NewEvent(adAction, adType, adSdkName, adPlacement, duration, null, mergeFields: false);
			}
		}

		public static void NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, long duration, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Ads.NewEvent(adAction, adType, adSdkName, adPlacement, duration, customFields, mergeFields);
			}
		}

		public static void NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, GAAdError noAdReason)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Ads.NewEvent(adAction, adType, adSdkName, adPlacement, noAdReason, null);
			}
		}

		public static void NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, GAAdError noAdReason, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Ads.NewEvent(adAction, adType, adSdkName, adPlacement, noAdReason, customFields, mergeFields);
			}
		}

		public static void NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Ads.NewEvent(adAction, adType, adSdkName, adPlacement, null);
			}
		}

		public static void NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, IDictionary<string, object> customFields, bool mergeFields = false)
		{
			if (!_hasInitializeBeenCalled)
			{
				Debug.LogError("GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW");
			}
			else
			{
				GA_Ads.NewEvent(adAction, adType, adSdkName, adPlacement, customFields, mergeFields);
			}
		}

		public static void SetCustomId(string userId)
		{
			Debug.Log("Initializing with custom id: " + userId);
			GA_Wrapper.SetCustomUserId(userId);
		}

		public static string GetUserId()
		{
			return GA_Wrapper.getUserId();
		}

		public static string GetExternalUserId()
		{
			return GA_Wrapper.GetExternalUserId();
		}

		public static void SetExternalUserId(string externalUserId)
		{
			GA_Wrapper.SetExternalUserId(externalUserId);
		}

		public static void SetEnabledManualSessionHandling(bool enabled)
		{
			GA_Wrapper.SetEnabledManualSessionHandling(enabled);
		}

		public static void SetEnabledEventSubmission(bool enabled)
		{
			GA_Wrapper.SetEnabledEventSubmission(enabled);
		}

		public static void SetEnabledEventSubmission(bool enabled, bool doCache)
		{
			GA_Wrapper.SetEnabledEventSubmission(enabled, doCache);
		}

		public static void StartSession()
		{
			GA_Wrapper.StartSession();
		}

		public static void EndSession()
		{
			GA_Wrapper.EndSession();
		}

		public static void SetCustomDimension01(string customDimension)
		{
			GA_Setup.SetCustomDimension01(customDimension);
		}

		public static void SetCustomDimension02(string customDimension)
		{
			GA_Setup.SetCustomDimension02(customDimension);
		}

		public static void SetCustomDimension03(string customDimension)
		{
			GA_Setup.SetCustomDimension03(customDimension);
		}

		public static void SetGlobalCustomEventFields(IDictionary<string, object> customFields)
		{
			GA_Setup.SetGlobalCustomEventFields(customFields);
		}

		public void OnRemoteConfigsUpdated()
		{
			if (OnRemoteConfigsUpdatedEvent != null)
			{
				OnRemoteConfigsUpdatedEvent();
			}
		}

		public static void RemoteConfigsUpdated()
		{
			if (OnRemoteConfigsUpdatedEvent != null)
			{
				OnRemoteConfigsUpdatedEvent();
			}
		}

		public static string GetRemoteConfigsValueAsString(string key)
		{
			return GetRemoteConfigsValueAsString(key, null);
		}

		public static string GetRemoteConfigsValueAsString(string key, string defaultValue)
		{
			return GA_Wrapper.GetRemoteConfigsValueAsString(key, defaultValue);
		}

		public static bool IsRemoteConfigsReady()
		{
			return GA_Wrapper.IsRemoteConfigsReady();
		}

		public static string GetRemoteConfigsContentAsString()
		{
			return GA_Wrapper.GetRemoteConfigsContentAsString();
		}

		public static string GetRemoteConfigsContentAsJSON()
		{
			return GA_Wrapper.GetRemoteConfigsContentAsJSON();
		}

		public static string GetABTestingId()
		{
			return GA_Wrapper.GetABTestingId();
		}

		public static string GetABTestingVariantId()
		{
			return GA_Wrapper.GetABTestingVariantId();
		}

		public static void StartTimer(string key)
		{
			GA_Wrapper.StartTimer(key);
		}

		public static void PauseTimer(string key)
		{
			GA_Wrapper.PauseTimer(key);
		}

		public static void ResumeTimer(string key)
		{
			GA_Wrapper.ResumeTimer(key);
		}

		public static long StopTimer(string key)
		{
			return GA_Wrapper.StopTimer(key);
		}

		public static void EnableSDKInitEvent(bool flag)
		{
			GA_Setup.EnableSDKInitEvent(flag);
		}

		public static void EnableFpsHistogram(bool flag)
		{
			GA_Setup.EnableFpsHistogram(flag);
		}

		public static void EnableMemoryHistogram(bool flag)
		{
			GA_Setup.EnableMemoryHistogram(flag);
		}

		public static void EnableHealthHardwareInfo(bool flag)
		{
			GA_Setup.EnableHealthHardwareInfo(flag);
		}

		public static void RequestTrackingAuthorization(IGameAnalyticsATTListener listener)
		{
		}

		public static void EnableAdvertisingIdTracking(bool flag)
		{
		}

		private static string GetUnityVersion()
		{
			string text = "";
			string[] array = Application.unityVersion.Split('.');
			for (int i = 0; i < array.Length; i++)
			{
				if (int.TryParse(array[i], out var result))
				{
					text = ((i != 0) ? (text + "." + array[i]) : array[i]);
					continue;
				}
				string[] array2 = Regex.Split(array[i], "[^\\d]+");
				if (array2.Length != 0 && int.TryParse(array2[0], out result))
				{
					text = text + "." + array2[0];
				}
			}
			return text;
		}

		private static int GetPlatformIndex()
		{
			int num = -1;
			RuntimePlatform platform = Application.platform;
			switch (platform)
			{
			case RuntimePlatform.IPhonePlayer:
				if (!SettingsGA.Platforms.Contains(platform))
				{
					return SettingsGA.Platforms.IndexOf(RuntimePlatform.tvOS);
				}
				return SettingsGA.Platforms.IndexOf(platform);
			case RuntimePlatform.tvOS:
				if (!SettingsGA.Platforms.Contains(platform))
				{
					return SettingsGA.Platforms.IndexOf(RuntimePlatform.IPhonePlayer);
				}
				return SettingsGA.Platforms.IndexOf(platform);
			default:
				if (platform != RuntimePlatform.MetroPlayerARM && platform != RuntimePlatform.MetroPlayerX64 && platform != RuntimePlatform.MetroPlayerX86)
				{
					return SettingsGA.Platforms.IndexOf(platform);
				}
				goto case RuntimePlatform.MetroPlayerX86;
			case RuntimePlatform.MetroPlayerX86:
			case RuntimePlatform.MetroPlayerX64:
			case RuntimePlatform.MetroPlayerARM:
				return SettingsGA.Platforms.IndexOf(RuntimePlatform.MetroPlayerARM);
			}
		}

		public static void SetBuildAllPlatforms(string build)
		{
			for (int i = 0; i < SettingsGA.Build.Count; i++)
			{
				SettingsGA.Build[i] = build;
			}
		}
	}
}
