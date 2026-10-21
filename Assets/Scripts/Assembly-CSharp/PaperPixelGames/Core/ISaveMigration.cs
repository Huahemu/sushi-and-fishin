using PaperPixelGames.Core.SaveSystem;

namespace PaperPixelGames.Core
{
	public interface ISaveMigration
	{
		int FromVersion { get; }

		int ToVersion { get; }

		void Migrate(SaveSlotData data);
	}
}
