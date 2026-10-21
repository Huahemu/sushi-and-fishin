namespace PaperPixelGames.Core.Kitchen.PreparationStation
{
	public class GastropanCenterInteractor : PrepStationUIHandlerBase
	{
		protected override bool RequiresValidStateForClick => false;

		protected override bool RequiresValidStateForHover => true;

		protected override void OnHoverEnter()
		{
			base.OnHoverEnter();
			if (base.IsInValidState)
			{
				_gastroPanSpawner.ClientHoverEnterCenterPan();
			}
		}

		protected override void OnHoverExit()
		{
			base.OnHoverExit();
			_gastroPanSpawner.ClientHoverExitCenterPan();
		}

		protected override void OnClick()
		{
			_gastroPanSpawner.ServerMoveActiveGastroPanToNextTargetRpc();
		}
	}
}
