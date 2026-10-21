using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.Core.Interaction
{
	public class TestSphere : BaseInteractable
	{
		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "", "");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
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
			return "TestSphere";
		}
	}
}
