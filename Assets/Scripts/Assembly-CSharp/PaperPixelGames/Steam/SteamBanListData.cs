using System;
using System.Collections.Generic;

namespace PaperPixelGames.Steam
{
	[Serializable]
	public class SteamBanListData
	{
		public List<ulong> BanList;

		public SteamBanListData()
		{
			BanList = new List<ulong>();
		}
	}
}
