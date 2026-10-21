using System.Collections.Generic;
using GameAnalyticsSDK.Wrapper;

namespace GameAnalyticsSDK.Events
{
	public static class GA_Ads
	{
		public static void NewEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, long duration, IDictionary<string, object> fields, bool mergeFields)
		{
			GA_Wrapper.AddAdEventWithDuration(adAction, adType, adSdkName, adPlacement, duration, fields, mergeFields);
		}

		public static void NewEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, GAAdError noAdReason, IDictionary<string, object> fields, bool mergeFields = false)
		{
			GA_Wrapper.AddAdEventWithReason(adAction, adType, adSdkName, adPlacement, noAdReason, fields, mergeFields);
		}

		public static void NewEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, IDictionary<string, object> fields, bool mergeFields = false)
		{
			GA_Wrapper.AddAdEvent(adAction, adType, adSdkName, adPlacement, fields, mergeFields);
		}
	}
}
