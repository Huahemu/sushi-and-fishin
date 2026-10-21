using PaperPixelGames.Core.Data.Ai.NPCs;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentScheduleController
	{
		private readonly ResidentDataSO _residentData;

		public ResidentScheduleController(ResidentDataSO residentData)
		{
			_residentData = residentData;
		}

		public bool TryFindActiveEntry(int hour, int minute, out int entryIndex, out ResidentScheduleSO.Entry entry)
		{
			entryIndex = -1;
			entry = default;
			if (_residentData == null || _residentData.Schedule == null || _residentData.Schedule.Entries == null)
			{
				return false;
			}
			int currentMinuteOfDay = ToMinuteOfDay(hour, minute);
			ResidentScheduleSO.Entry[] entries = _residentData.Schedule.Entries;
			for (int i = 0; i < entries.Length; i++)
			{
				if (IsCurrentTimeInsideEntry(currentMinuteOfDay, entries[i]))
				{
					entryIndex = i;
					entry = entries[i];
					return true;
				}
			}
			return false;
		}

		private static bool IsCurrentTimeInsideEntry(int currentMinuteOfDay, ResidentScheduleSO.Entry entry)
		{
			int num = ToMinuteOfDay(entry.StartTime, entry.StartMinute);
			int num2 = ToMinuteOfDay(Mathf.FloorToInt(entry.EndTime), entry.EndMinute);
			if (num == num2)
			{
				return false;
			}
			if (num < num2)
			{
				if (currentMinuteOfDay >= num)
				{
					return currentMinuteOfDay < num2;
				}
				return false;
			}
			if (currentMinuteOfDay < num)
			{
				return currentMinuteOfDay < num2;
			}
			return true;
		}

		private static int ToMinuteOfDay(int hour, int minute)
		{
			hour = Mathf.Clamp(hour, 0, 24);
			minute = Mathf.Clamp(minute, 0, 59);
			if (hour != 24)
			{
				return hour * 60 + minute;
			}
			return 1440;
		}
	}
}
