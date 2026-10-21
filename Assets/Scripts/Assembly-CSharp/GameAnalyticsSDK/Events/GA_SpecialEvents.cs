using System.Collections;
using UnityEngine;

namespace GameAnalyticsSDK.Events
{
	public class GA_SpecialEvents : MonoBehaviour
	{
		private static int _frameCountAvg = 0;

		private static float _lastUpdateAvg = 0f;

		private int _frameCountCrit;

		private float _lastUpdateCrit;

		private static int _criticalFpsCount = 0;

		private static int _fpsWaitTimeMultiplier = 1;

		private static float _lastPauseStartTime;

		private static float _pauseDurationAvg;

		private static float _pauseDurationCrit;

		public void Start()
		{
			StartCoroutine(SubmitFPSRoutine());
			StartCoroutine(CheckCriticalFPSRoutine());
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (GameAnalytics.SettingsGA == null || (!GameAnalytics.SettingsGA.SubmitFpsAverage && !GameAnalytics.SettingsGA.SubmitFpsCritical))
			{
				return;
			}
			if (pauseStatus)
			{
				_lastPauseStartTime = Time.realtimeSinceStartup;
				return;
			}
			if (GameAnalytics.SettingsGA.SubmitFpsAverage)
			{
				_pauseDurationAvg += Time.realtimeSinceStartup - _lastPauseStartTime;
			}
			if (GameAnalytics.SettingsGA.SubmitFpsCritical)
			{
				_pauseDurationCrit += Time.realtimeSinceStartup - _lastPauseStartTime;
			}
		}

		private IEnumerator SubmitFPSRoutine()
		{
			while (Application.isPlaying && GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitFpsAverage)
			{
				int num = 30 * _fpsWaitTimeMultiplier;
				yield return new WaitForSecondsRealtime(num);
				_fpsWaitTimeMultiplier *= 2;
				SubmitFPS();
			}
		}

		private IEnumerator CheckCriticalFPSRoutine()
		{
			while (Application.isPlaying && GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitFpsCritical)
			{
				yield return new WaitForSecondsRealtime(GameAnalytics.SettingsGA.FpsCirticalSubmitInterval);
				CheckCriticalFPS();
			}
		}

		public void Update()
		{
			if (GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitFpsAverage)
			{
				_frameCountAvg++;
			}
			if (GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitFpsCritical)
			{
				_frameCountCrit++;
			}
		}

		public static void SubmitFPS()
		{
			if (GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitFpsAverage)
			{
				float num = Time.unscaledTime - _lastUpdateAvg - _pauseDurationAvg;
				_pauseDurationAvg = 0f;
				if (num > 1f)
				{
					_ = (float)_frameCountAvg / num;
					_lastUpdateAvg = Time.unscaledTime;
					_frameCountAvg = 0;
					_ = 0f;
				}
			}
			if (GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitFpsCritical && _criticalFpsCount > 0)
			{
				_criticalFpsCount = 0;
			}
		}

		public void CheckCriticalFPS()
		{
			if (!(GameAnalytics.SettingsGA != null) || !GameAnalytics.SettingsGA.SubmitFpsCritical)
			{
				return;
			}
			float num = Time.unscaledTime - _lastUpdateCrit - _pauseDurationCrit;
			_pauseDurationCrit = 0f;
			if (num >= 1f)
			{
				float num2 = (float)_frameCountCrit / num;
				_lastUpdateCrit = Time.unscaledTime;
				_frameCountCrit = 0;
				if (num2 <= (float)GameAnalytics.SettingsGA.FpsCriticalThreshold)
				{
					_criticalFpsCount++;
				}
			}
		}
	}
}
