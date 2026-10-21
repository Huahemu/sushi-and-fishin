using System;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffServiceGameplayApi
	{
		public static bool Earn(MonoBehaviour context, int dollars)
		{
			if (!ServiceLocator.ForSceneOf(context).TryGet<EconomyHandler>(out var service) || !service.ServerStaffEarn(dollars))
			{
				return false;
			}
			if (ServiceLocator.ForSceneOf(context).TryGet<DailyReportBroadcaster>(out var service2))
			{
				service2.ServerRecordStaffMoneyEarned(dollars);
			}
			return true;
		}

		public static void Record(StaffEmployeeEntry employee, int served, int payments, int cleaned, string sourceId)
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			double valueOrDefault = (instance?.ProgressionManager?.TotalXp).GetValueOrDefault();
			if (payments > 0)
			{
				instance?.ProgressionManager?.ServerGrantStaffXp(ProgressionXpSource.CustomerServed, sourceId);
			}
			if (cleaned > 0)
			{
				instance?.ProgressionManager?.ServerGrantStaffXp(ProgressionXpSource.DirtyTrayCleaned, sourceId);
			}
			double xp = Math.Max(0.0, (instance?.ProgressionManager?.TotalXp ?? valueOrDefault) - valueOrDefault);
			StaffManager.Instance?.Ledger.Service(employee, served, payments, cleaned, xp);
		}
	}
}
