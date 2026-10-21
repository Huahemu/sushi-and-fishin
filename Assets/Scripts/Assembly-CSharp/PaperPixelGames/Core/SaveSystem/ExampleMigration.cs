using PaperPixelGames.Framework.Utility;

namespace PaperPixelGames.Core.SaveSystem
{
	public class ExampleMigration : ISaveMigration
	{
		public int FromVersion => 0;

		public int ToVersion => 0;

		public void Migrate(SaveSlotData data)
		{
			if (data != null)
			{
				UtilityMethods.Log("[SaveMigrations] Migrating 0 -> 1");
				data.Version = ToVersion;
			}
		}
	}
}
