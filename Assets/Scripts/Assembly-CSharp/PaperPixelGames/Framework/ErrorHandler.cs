using System;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Framework
{
	public static class ErrorHandler
	{
		private const int MaxRetryCount = 3;

		public static void HandleError(string context, Exception ex, Action retryAction = null, int retryCount = 0, string filePath = null)
		{
			UtilityMethods.Log("Error in " + context + ": " + ex.Message, LogType.Error);
			if (retryCount >= 3)
			{
				UtilityMethods.Log($"Failed to handle {context} after {3} attempts.", LogType.Error);
				return;
			}
			if (!string.IsNullOrEmpty(filePath) && ES3.FileExists(filePath))
			{
				ES3.DeleteFile(filePath);
				UtilityMethods.Log("Deleted corrupted file: " + filePath, LogType.Warning);
			}
			retryAction?.Invoke();
		}

		public static void HandleSaveError(string context, Exception ex, string fileName)
		{
			UtilityMethods.Log(context + ": " + ex.Message, LogType.Error);
		}
	}
}
