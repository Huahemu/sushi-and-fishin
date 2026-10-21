using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;

namespace PaperPixelGames.Core.Ocean
{
	public class Bait : BaseHoldable
	{
		public BaitSO BaitSO => _itemSO as BaitSO;

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "Bait";
		}
	}
}
