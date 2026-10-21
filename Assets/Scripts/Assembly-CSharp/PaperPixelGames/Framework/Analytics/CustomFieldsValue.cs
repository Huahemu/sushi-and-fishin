using System.Collections.Generic;

namespace PaperPixelGames.Framework.Analytics
{
	public class CustomFieldsValue : IEventValue
	{
		private readonly IDictionary<string, object> _customFields;

		public CustomFieldsValue(IDictionary<string, object> customFields)
		{
			_customFields = AnalyticsEventUtility.SanitizeCustomFields(customFields);
		}

		public void SendEvent(string eventString)
		{
		}
	}
}
