using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.GastronormPanIngredients
{
	public class RicePanIngredientGraphics : PanIngredientGraphics
	{
		[SerializeField]
		private ProcessedIngredientSO _cookedRiceSO;

		[SerializeField]
		private GameObject _visualRoot;

		[SerializeField]
		private RiceLatticeController _riceLatticeController;

		public override void Initialize()
		{
			base.Initialize();
			if (_visualRoot != null)
			{
				_visualRoot.SetActive(value: true);
			}
		}

		public override void SyncToCountStaggered(int previous, int target, float interval)
		{
			CancelStaggeredSync();
			SyncToCount(target);
		}

		public override void SyncToCount(int currentUses)
		{
			int num = Mathf.Max(1, _cookedRiceSO.MaxPanCapacity);
			currentUses = Mathf.Clamp(currentUses, 0, num);
			if (currentUses == 0)
			{
				if (_visualRoot.activeSelf)
				{
					_visualRoot.SetActive(value: false);
				}
				_riceLatticeController.SetFillByUses(0, num);
			}
			else
			{
				if (!_visualRoot.activeSelf)
				{
					_visualRoot.SetActive(value: true);
				}
				_riceLatticeController.SetFillByUses(currentUses, num);
			}
		}

		public override void ResetAll()
		{
			base.ResetAll();
			if (_visualRoot != null)
			{
				_visualRoot.SetActive(value: false);
			}
			if (_riceLatticeController != null && _cookedRiceSO != null)
			{
				_riceLatticeController.SetFillByUses(0, _cookedRiceSO.MaxPanCapacity);
			}
		}
	}
}
