using System;

namespace PaperPixelGames.Core.NetworkDevelopment
{
	public sealed class BootstrapCompletionState
	{
		private uint generation;

		private bool busy;

		public bool ProfileReady { get; private set; }

		public bool Reported { get; private set; }

		public bool Started { get; private set; }

		public bool InvalidateSnapshot()
		{
			if (Started)
			{
				return false;
			}
			generation++;
			bool flag = (Reported = false);
			busy = flag;
			return true;
		}

		public void Reset()
		{
			generation++;
			bool flag = (Started = false);
			bool flag3 = (Reported = flag);
			bool flag5 = (ProfileReady = flag3);
			busy = flag5;
		}

		public bool TryReport(bool prerequisitesReady, Func<bool> loadProfile, Func<bool> report)
		{
			if (!prerequisitesReady || busy || Reported)
			{
				return false;
			}
			uint num = generation;
			busy = true;
			try
			{
				if (!ProfileReady)
				{
					if (!loadProfile() || num != generation)
					{
						return false;
					}
					ProfileReady = true;
				}
				if (!report() || num != generation)
				{
					return false;
				}
				Reported = true;
				return true;
			}
			finally
			{
				if (num == generation)
				{
					busy = false;
				}
			}
		}

		public bool TryStart(bool prerequisitesReady, Func<bool> start)
		{
			if (!prerequisitesReady || busy || !ProfileReady || !Reported || Started)
			{
				return false;
			}
			uint num = generation;
			busy = true;
			try
			{
				if (!start() || num != generation)
				{
					return false;
				}
				Started = true;
				return true;
			}
			finally
			{
				if (num == generation)
				{
					busy = false;
				}
			}
		}
	}
}
