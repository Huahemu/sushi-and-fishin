using System.Collections.Generic;
using GameAnalyticsSDK.Utilities;
using GameAnalyticsSDK.Validators;
using GameAnalyticsSDK.Wrapper;

namespace GameAnalyticsSDK.Events
{
	public static class GA_Setup
	{
		public static void SetAvailableCustomDimensions01(List<string> customDimensions)
		{
			if (GAValidator.ValidateCustomDimensions(customDimensions.ToArray()))
			{
				GA_Wrapper.SetAvailableCustomDimensions01(GA_MiniJSON.Serialize(customDimensions));
			}
		}

		public static void SetAvailableCustomDimensions02(List<string> customDimensions)
		{
			if (GAValidator.ValidateCustomDimensions(customDimensions.ToArray()))
			{
				GA_Wrapper.SetAvailableCustomDimensions02(GA_MiniJSON.Serialize(customDimensions));
			}
		}

		public static void SetAvailableCustomDimensions03(List<string> customDimensions)
		{
			if (GAValidator.ValidateCustomDimensions(customDimensions.ToArray()))
			{
				GA_Wrapper.SetAvailableCustomDimensions03(GA_MiniJSON.Serialize(customDimensions));
			}
		}

		public static void SetAvailableResourceCurrencies(List<string> resourceCurrencies)
		{
			if (GAValidator.ValidateResourceCurrencies(resourceCurrencies.ToArray()))
			{
				GA_Wrapper.SetAvailableResourceCurrencies(GA_MiniJSON.Serialize(resourceCurrencies));
			}
		}

		public static void SetAvailableResourceItemTypes(List<string> resourceItemTypes)
		{
			if (GAValidator.ValidateResourceItemTypes(resourceItemTypes.ToArray()))
			{
				GA_Wrapper.SetAvailableResourceItemTypes(GA_MiniJSON.Serialize(resourceItemTypes));
			}
		}

		public static void SetInfoLog(bool enabled)
		{
			GA_Wrapper.SetInfoLog(enabled);
		}

		public static void SetVerboseLog(bool enabled)
		{
			GA_Wrapper.SetVerboseLog(enabled);
		}

		public static void SetCustomDimension01(string customDimension)
		{
			GA_Wrapper.SetCustomDimension01(customDimension);
		}

		public static void SetCustomDimension02(string customDimension)
		{
			GA_Wrapper.SetCustomDimension02(customDimension);
		}

		public static void SetCustomDimension03(string customDimension)
		{
			GA_Wrapper.SetCustomDimension03(customDimension);
		}

		public static void SetGlobalCustomEventFields(IDictionary<string, object> customFields)
		{
			GA_Wrapper.SetGlobalCustomEventFields(customFields);
		}

		public static void EnableSDKInitEvent(bool flag)
		{
			GA_Wrapper.EnableSDKInitEvent(flag);
		}

		public static void EnableFpsHistogram(bool flag)
		{
			GA_Wrapper.EnableFpsHistogram(flag);
		}

		public static void EnableMemoryHistogram(bool flag)
		{
			GA_Wrapper.EnableMemoryHistogram(flag);
		}

		public static void EnableHealthHardwareInfo(bool flag)
		{
			GA_Wrapper.EnableHealthHardwareInfo(flag);
		}
	}
}
