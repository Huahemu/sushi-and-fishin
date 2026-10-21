using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class FishTrophyData
	{
		public string RecordFishId = string.Empty;

		public float RecordWeightKg;

		public ulong RecordSteamId;

		public int AudioFormatVersion = 1;

		public int SampleRate = 16000;

		public int AudioRevision;

		public byte[] AudioBytes = Array.Empty<byte>();

		public int FirstApproachPlaybackVersion;

		public bool FirstApproachPlaybackCompleted;
	}
}
