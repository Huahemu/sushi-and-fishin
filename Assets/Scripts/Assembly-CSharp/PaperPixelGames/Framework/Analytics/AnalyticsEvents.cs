using System.Collections.Generic;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.Framework.Analytics
{
	public static class AnalyticsEvents
	{
		public static void Send(params object[] categories)
		{
			Send(null, categories);
		}

		public static void Send(IDictionary<string, object> fields, params object[] categories)
		{
			AnalyticsManager instance = Singleton<AnalyticsManager>.Instance;
			if (!(instance == null))
			{
				IEventValue eventValue2;
				if (fields == null || fields.Count <= 0)
				{
					IEventValue eventValue = new FloatValue(1f);
					eventValue2 = eventValue;
				}
				else
				{
					IEventValue eventValue = new CustomFieldsValue(fields);
					eventValue2 = eventValue;
				}
				IEventValue value = eventValue2;
				instance.DesignEvent(value, categories);
			}
		}

		public static bool SendMilestone(AnalyticsMilestoneKey key, IDictionary<string, object> fields, params object[] categories)
		{
			if (!TryGetMilestones(out var milestones))
			{
				return false;
			}
			if (milestones.IsCompleted(key))
			{
				return false;
			}
			milestones.SetCompleted(key);
			Send(fields, categories);
			return true;
		}

		public static Dictionary<string, object> Fields(params object[] keyValues)
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			if (keyValues == null)
			{
				return dictionary;
			}
			for (int i = 0; i + 1 < keyValues.Length; i += 2)
			{
				if (keyValues[i] is string text && !string.IsNullOrWhiteSpace(text))
				{
					dictionary[text] = keyValues[i + 1];
				}
			}
			return dictionary;
		}

		private static bool TryGetMilestones(out AnalyticsMilestoneData milestones)
		{
			milestones = null;
			if (Singleton<SaveManager>.Instance == null || !Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data))
			{
				return false;
			}
			SaveSlotData saveSlotData = data;
			if (saveSlotData.GameProgress == null)
			{
				saveSlotData.GameProgress = new GameProgressData();
			}
			GameProgressData gameProgress = data.GameProgress;
			if (gameProgress.AnalyticsMilestones == null)
			{
				gameProgress.AnalyticsMilestones = new AnalyticsMilestoneData();
			}
			milestones = data.GameProgress.AnalyticsMilestones;
			return true;
		}
	}
}
