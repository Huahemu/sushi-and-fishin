using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	public sealed class DevConsoleContext
	{
		public ItemDatabaseSO ItemDatabase { get; }

		public Transform SpawnOrigin { get; set; }

		public DevConsoleContext(ItemDatabaseSO itemDatabase, Transform spawn)
		{
			ItemDatabase = itemDatabase;
			SpawnOrigin = spawn;
		}
	}
}
