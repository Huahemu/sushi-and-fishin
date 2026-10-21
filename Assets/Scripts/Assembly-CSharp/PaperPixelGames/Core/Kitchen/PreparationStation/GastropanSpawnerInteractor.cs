using DG.Tweening;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.PreparationStation
{
	public class GastropanSpawnerInteractor : PrepStationUIHandlerBase
	{
		private Vector3 _baseEuler;

		protected override bool RequiresValidStateForClick => false;

		protected override bool RequiresValidStateForHover => true;

		private void Awake()
		{
			if (_modelObj != null)
			{
				_baseEuler = _modelObj.transform.localEulerAngles;
			}
		}

		protected override void OnHoverEnter()
		{
			if (_modelObj == null)
			{
				return;
			}
			_modelObj.transform.DOKill();
			_modelObj.transform.DOLocalRotate(new Vector3(_baseEuler.x, _baseEuler.y, _baseEuler.z + 5f), 0.2f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine)
				.OnComplete(() =>
				{
					if (_modelObj != null)
					{
						_modelObj.transform.DOLocalRotate(_baseEuler, 0.1f).SetEase(Ease.OutSine);
					}
				});
		}

		protected override void OnHoverExit()
		{
			if (!(_modelObj == null))
			{
				_modelObj.transform.DOKill();
				_modelObj.transform.DOLocalRotate(_baseEuler, 0.1f).SetEase(Ease.OutSine);
			}
		}

		protected override void OnClick()
		{
			_gastroPanSpawner.ServerTrySpawnGastroPanRpc();
		}
	}
}
