using System.Collections.Generic;
using GameAnalyticsSDK.Wrapper;

namespace GameAnalyticsSDK.Events
{
	public static class GA_Business
	{
		public static void NewEvent(string currency, int amount, string itemType, string itemId, string cartType, IDictionary<string, object> fields, bool mergeFields)
		{
			GA_Wrapper.AddBusinessEvent(currency, amount, itemType, itemId, cartType, fields, mergeFields);
		}
	}
}
