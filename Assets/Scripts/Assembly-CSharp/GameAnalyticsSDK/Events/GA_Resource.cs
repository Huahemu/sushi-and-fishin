using System.Collections.Generic;
using GameAnalyticsSDK.Wrapper;

namespace GameAnalyticsSDK.Events
{
	public static class GA_Resource
	{
		public static void NewEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId, IDictionary<string, object> fields, bool mergeFields)
		{
			GA_Wrapper.AddResourceEvent(flowType, currency, amount, itemType, itemId, fields, mergeFields);
		}
	}
}
