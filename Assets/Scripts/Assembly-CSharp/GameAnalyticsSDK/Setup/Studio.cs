using System.Collections.Generic;

namespace GameAnalyticsSDK.Setup
{
	public class Studio
	{
		public string Name { get; private set; }

		public string ID { get; private set; }

		public string OrganizationID { get; private set; }

		public List<Game> Games { get; private set; }

		public Studio(string name, string id, string orgId, List<Game> games)
		{
			Name = name;
			ID = id;
			OrganizationID = orgId;
			Games = games;
		}

		public static string[] GetStudioNames(List<Studio> studios, bool addFirstEmpty = true)
		{
			if (studios == null)
			{
				return new string[1] { "-" };
			}
			if (addFirstEmpty)
			{
				string[] array = new string[studios.Count + 1];
				array[0] = "-";
				for (int i = 0; i < studios.Count; i++)
				{
					int num = i + 1;
					array[num] = num + ". " + studios[i].Name;
				}
				return array;
			}
			string[] array2 = new string[studios.Count];
			for (int j = 0; j < studios.Count; j++)
			{
				array2[j] = j + 1 + ". " + studios[j].Name;
			}
			return array2;
		}

		public static string[] GetGameNames(int index, List<Studio> studios)
		{
			if (studios == null || studios[index].Games == null)
			{
				return new string[1] { "-" };
			}
			string[] array = new string[studios[index].Games.Count + 1];
			array[0] = "-";
			for (int i = 0; i < studios[index].Games.Count; i++)
			{
				int num = i + 1;
				array[num] = num + ". " + studios[index].Games[i].Name;
			}
			return array;
		}
	}
}
