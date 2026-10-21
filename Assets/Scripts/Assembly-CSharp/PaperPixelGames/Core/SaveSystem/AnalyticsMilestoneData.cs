using System;
using PaperPixelGames.Framework.Analytics;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class AnalyticsMilestoneData
	{
		public bool SaveCreated;

		public bool RestaurantEntered;

		public bool FirstOrderAccepted;

		public bool FirstDishServed;

		public bool FirstMoneyEarned;

		public bool IsCompleted(AnalyticsMilestoneKey key)
		{
			return key switch
			{
				AnalyticsMilestoneKey.SaveCreated => SaveCreated, 
				AnalyticsMilestoneKey.RestaurantEntered => RestaurantEntered, 
				AnalyticsMilestoneKey.FirstOrderAccepted => FirstOrderAccepted, 
				AnalyticsMilestoneKey.FirstDishServed => FirstDishServed, 
				AnalyticsMilestoneKey.FirstMoneyEarned => FirstMoneyEarned, 
				_ => false, 
			};
		}

		public void SetCompleted(AnalyticsMilestoneKey key)
		{
			switch (key)
			{
			case AnalyticsMilestoneKey.SaveCreated:
				SaveCreated = true;
				break;
			case AnalyticsMilestoneKey.RestaurantEntered:
				RestaurantEntered = true;
				break;
			case AnalyticsMilestoneKey.FirstOrderAccepted:
				FirstOrderAccepted = true;
				break;
			case AnalyticsMilestoneKey.FirstDishServed:
				FirstDishServed = true;
				break;
			case AnalyticsMilestoneKey.FirstMoneyEarned:
				FirstMoneyEarned = true;
				break;
			}
		}
	}
}
