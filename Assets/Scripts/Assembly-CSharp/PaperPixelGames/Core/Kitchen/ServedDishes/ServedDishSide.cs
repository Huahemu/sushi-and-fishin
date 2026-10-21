using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.Core.Kitchen.ServedDishes
{
	public class ServedDishSide : ServedDish
	{
		public override byte GetIndexByDish()
		{
			return 1;
		}

		public override void EnableGazeHints()
		{
			if (!TryEnableTakeToTrayGazeHint())
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public override void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			RemoveHeldThrowHint();
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
			return "ServedDishSide";
		}
	}
}
