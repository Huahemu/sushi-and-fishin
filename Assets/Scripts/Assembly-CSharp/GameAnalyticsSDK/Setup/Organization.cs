using System.Collections.Generic;

namespace GameAnalyticsSDK.Setup
{
	public class Organization
	{
		public string Name { get; private set; }

		public string ID { get; private set; }

		public List<Studio> Studios { get; private set; }

		public Organization(string name, string id)
		{
			Name = name;
			ID = id;
			Studios = new List<Studio>();
		}

		public static string[] GetOrganizationNames(List<Organization> organizations, bool addFirstEmpty = true)
		{
			if (organizations == null)
			{
				return new string[1] { "-" };
			}
			if (addFirstEmpty)
			{
				string[] array = new string[organizations.Count + 1];
				array[0] = "-";
				string text = "";
				for (int i = 0; i < organizations.Count; i++)
				{
					array[i + 1] = organizations[i].Name + text;
					text += " ";
				}
				return array;
			}
			string[] array2 = new string[organizations.Count];
			string text2 = "";
			for (int j = 0; j < organizations.Count; j++)
			{
				array2[j] = organizations[j].Name + text2;
				text2 += " ";
			}
			return array2;
		}
	}
}
