namespace PaperPixelGames.Core.Interaction
{
	public class TestDesk : BaseUsable
	{
		public override void ClientUseEnter(Interactor interactor)
		{
			base.ClientUseEnter(interactor);
			interactor.Player.SwitchTo("PrepStation", cursorVisible: true);
		}

		public override void ClientUseExit(Interactor interactor)
		{
			base.ClientUseExit(interactor);
			interactor.Player.SwitchTo("Player", cursorVisible: false);
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
			return "TestDesk";
		}
	}
}
