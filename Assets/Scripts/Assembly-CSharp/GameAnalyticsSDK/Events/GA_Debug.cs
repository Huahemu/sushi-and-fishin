using System.Collections.Generic;
using UnityEngine;

namespace GameAnalyticsSDK.Events
{
	public static class GA_Debug
	{
		public static int MaxErrorCount = 10;

		private static int _errorCount = 0;

		private static bool _showLogOnGUI = false;

		public static List<string> Messages;

		public static void HandleLog(string logString, string stackTrace, LogType type)
		{
			if (_showLogOnGUI)
			{
				if (Messages == null)
				{
					Messages = new List<string>();
				}
				Messages.Add(logString);
			}
			if (GameAnalytics.SettingsGA != null && GameAnalytics.SettingsGA.SubmitErrors && _errorCount < MaxErrorCount && type != LogType.Log)
			{
				if (string.IsNullOrEmpty(stackTrace))
				{
					stackTrace = "";
				}
				_errorCount++;
				string text = logString.Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' ');
				string text2 = stackTrace.Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' ');
				string text3 = text + " " + text2;
				if (text3.Length > 8192)
				{
					text3 = text3.Substring(0, 8191);
				}
				SubmitError(text3, type);
			}
		}

		private static void SubmitError(string message, LogType type)
		{
			GAErrorSeverity severity = GAErrorSeverity.Info;
			switch (type)
			{
			case LogType.Assert:
				severity = GAErrorSeverity.Info;
				break;
			case LogType.Error:
				severity = GAErrorSeverity.Error;
				break;
			case LogType.Exception:
				severity = GAErrorSeverity.Critical;
				break;
			case LogType.Log:
				severity = GAErrorSeverity.Debug;
				break;
			case LogType.Warning:
				severity = GAErrorSeverity.Warning;
				break;
			}
			GA_Error.NewEvent(severity, message, null, mergeFields: false);
		}

		public static void EnabledLog()
		{
			_showLogOnGUI = true;
		}
	}
}
