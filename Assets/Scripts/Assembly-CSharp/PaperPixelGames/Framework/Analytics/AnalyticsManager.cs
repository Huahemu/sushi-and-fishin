using System;
using GameAnalyticsSDK;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Framework.Analytics
{
	public class AnalyticsManager : Singleton<AnalyticsManager>
	{
		private const string EventSubmissionPrefsKey = "Analytics.EventSubmissionEnabled";

		private bool _isInitialized;

		private bool _eventSubmissionEnabled = true;

		public void Initialize()
		{
			_eventSubmissionEnabled = PlayerPrefs.GetInt("Analytics.EventSubmissionEnabled", 1) == 1;
			GameAnalytics.SetBuildAllPlatforms(Application.version);
			GameAnalytics.SetEnabledEventSubmission(_eventSubmissionEnabled);
			GameAnalytics.Initialize();
			_isInitialized = true;
		}

		public void SetEventSubmissionEnabled(bool enabled, bool cacheWhileDisabled = false)
		{
			_eventSubmissionEnabled = enabled;
			PlayerPrefs.SetInt("Analytics.EventSubmissionEnabled", enabled ? 1 : 0);
			PlayerPrefs.Save();
			if (_isInitialized)
			{
				GameAnalytics.SetEnabledEventSubmission(enabled, cacheWhileDisabled);
			}
		}

		public void DesignEvent(IEventValue value, params object[] categories)
		{
			if (_isInitialized && _eventSubmissionEnabled && value != null && AnalyticsEventUtility.TryBuildEventName(categories, out var eventName))
			{
				value.SendEvent(eventName);
			}
		}

		[Obsolete("Use DesignEvent instead.")]
		public void DesingEvent(IEventValue value, params object[] categories)
		{
			DesignEvent(value, categories);
		}
	}
}
