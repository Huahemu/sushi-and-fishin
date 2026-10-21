namespace PaperPixelGames.Core.Kitchen.ServedDishes
{
	public class ServedDishDessert : ServedDish
	{
		public override byte GetIndexByDish()
		{
			return 0;
		}

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
			return "ServedDishDessert";
		}
	}
}
