using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.Core.Kitchen.ChefDesk.ChefDeskUIHandler
{
	public class ChefDeskGastropanInteractor : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
	{
		[Header("ToolTip")]
		[SerializeField]
		private GameObject _toolTipGO;

		[SerializeField]
		private TextMeshProUGUI _ingredientNameText;

		[SerializeField]
		private TextMeshProUGUI _ingredientCount;

		[Header("ToolTip_01")]
		[SerializeField]
		private GameObject _toolTipGO_01;

		[SerializeField]
		private TextMeshProUGUI _ingredientNameText_01;

		[SerializeField]
		private TextMeshProUGUI _ingredientCount_01;

		private ChefDeskGastroPan _chefDeskGastroPan;

		private float _hoverScaleMultiplier = 1.03f;

		private float _hoverLiftAmount = 0.006f;

		private float _hoverDuration = 0.15f;

		private Vector3 _defaultScale;

		private Vector3 _defaultLocalPosition;

		private ProcessedIngredientSO _processedIngredientSO;

		private bool _hovered;

		private float _nextHintRefreshTime;

		private bool _placeHintVisible;

		private bool _returnHintVisible;

		public GameObject ToolTipGO_01 => _toolTipGO_01;

		public void ClientInit(ChefDeskGastroPan chefDeskGastroPan)
		{
			_chefDeskGastroPan = chefDeskGastroPan;
			Transform transform = _chefDeskGastroPan.Graphic.transform;
			_defaultScale = transform.localScale;
			_defaultLocalPosition = transform.localPosition;
			_toolTipGO.SetActive(value: false);
			_toolTipGO_01.SetActive(value: false);
		}

		private void LateUpdate()
		{
			if (_hovered && Time.unscaledTime >= _nextHintRefreshTime)
			{
				_nextHintRefreshTime = Time.unscaledTime + 0.1f;
				RefreshDeskToolTip();
			}
		}

		private void OnDisable()
		{
			_hovered = false;
			RefreshActionHints(canPlace: false, canReturn: false);
			if (_toolTipGO != null)
			{
				_toolTipGO.SetActive(value: false);
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (_chefDeskGastroPan.ChefDesk.CurrentState != ChefDeskState.Topping && _chefDeskGastroPan.ChefDesk.CurrentState != ChefDeskState.TransferToEdge)
			{
				Transform target = _chefDeskGastroPan.Graphic.transform;
				target.DOKill();
				target.DOScale(_defaultScale * _hoverScaleMultiplier, _hoverDuration).SetEase(Ease.OutBack);
				target.DOLocalMoveY(_defaultLocalPosition.y + _hoverLiftAmount, _hoverDuration).SetEase(Ease.OutQuad);
			}
			_hovered = true;
			RefreshDeskToolTip();
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			_chefDeskGastroPan.ChefDesk.ClientEndGastropanHoldPlacement(_chefDeskGastroPan);
			Transform target = _chefDeskGastroPan.Graphic.transform;
			target.DOKill();
			target.DOScale(_defaultScale, _hoverDuration).SetEase(Ease.OutBack);
			target.DOLocalMoveY(_defaultLocalPosition.y, _hoverDuration).SetEase(Ease.OutQuad);
			_toolTipGO.SetActive(value: false);
			_hovered = false;
			RefreshActionHints(canPlace: false, canReturn: false);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (_chefDeskGastroPan.ChefDesk.CurrentState != ChefDeskState.Topping && _chefDeskGastroPan.ChefDesk.CurrentState != ChefDeskState.TransferToEdge && eventData.button == PointerEventData.InputButton.Right)
			{
				UtilityMethods.Log("Sag tik algilandi");
				OnClickRightMouseButton();
			}
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Left && CanUseGastropanInput())
			{
				OnClickLeftMouseButton();
				_chefDeskGastroPan.ChefDesk.ClientBeginGastropanHoldPlacement(_chefDeskGastroPan);
			}
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			if (eventData.button == PointerEventData.InputButton.Left)
			{
				_chefDeskGastroPan.ChefDesk.ClientEndGastropanHoldPlacement(_chefDeskGastroPan);
			}
		}

		private bool CanUseGastropanInput()
		{
			if (_chefDeskGastroPan == null)
			{
				return false;
			}
			if (_chefDeskGastroPan.ChefDesk == null)
			{
				return false;
			}
			if (_chefDeskGastroPan.ChefDesk.CurrentState != ChefDeskState.Topping)
			{
				return _chefDeskGastroPan.ChefDesk.CurrentState != ChefDeskState.TransferToEdge;
			}
			return false;
		}

		private void OnClickLeftMouseButton()
		{
			_chefDeskGastroPan.ChefDesk.ServerPlaceProductOnTableRpc(_chefDeskGastroPan.Index);
		}

		private void OnClickRightMouseButton()
		{
			_chefDeskGastroPan.ChefDesk.ServerRetrieveProductFromTableRpc(_chefDeskGastroPan.Index);
		}

		private void RefreshDeskToolTip()
		{
			if (_chefDeskGastroPan == null || _chefDeskGastroPan.ChefDesk == null)
			{
				return;
			}
			ChefDesk chefDesk = _chefDeskGastroPan.ChefDesk;
			ChefDeskState currentState = chefDesk.CurrentState;
			int num;
			int num2;
			if (currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected)
			{
				if (chefDesk.IsCustomSushi)
				{
					num = ((!chefDesk.IsCustomCompositionLocked) ? 1 : 0);
					if (num == 0)
					{
						goto IL_0068;
					}
				}
				else
				{
					num = 1;
				}
				num2 = (chefDesk.ClientCanPlaceIngredientFromPan(_chefDeskGastroPan) ? 1 : 0);
				goto IL_0069;
			}
			num = 0;
			goto IL_0068;
			IL_0069:
			bool canPlace = (byte)num2 != 0;
			bool canReturn = num != 0 && chefDesk.ClientCanReturnIngredientToPan(_chefDeskGastroPan);
			string text = _chefDeskGastroPan.CurrentIngredientID.ToString();
			if (string.IsNullOrEmpty(text))
			{
				text = _chefDeskGastroPan.LastEmptiedPlacementIngredientId.ToString();
			}
			if (!(chefDesk.ItemDatabaseSO?.FindById(text) is ProcessedIngredientSO processedIngredientSO))
			{
				_toolTipGO.SetActive(value: false);
				RefreshActionHints(canPlace: false, canReturn: false);
				return;
			}
			_ingredientNameText.text = processedIngredientSO.LocalizedName;
			_ingredientCount.text = $"{_chefDeskGastroPan.IngredientCount}/{processedIngredientSO.MaxPanCapacity}";
			_toolTipGO.SetActive(value: true);
			RefreshActionHints(canPlace, canReturn);
			return;
			IL_0068:
			num2 = 0;
			goto IL_0069;
		}

		private void RefreshActionHints(bool canPlace, bool canReturn)
		{
			HintsManager instance = Singleton<HintsManager>.Instance;
			if (instance == null)
			{
				_placeHintVisible = false;
				_returnHintVisible = false;
				return;
			}
			if (_placeHintVisible != canPlace)
			{
				if (canPlace)
				{
					instance.AddHint("LeftMouseMechanic", "key_put");
				}
				else
				{
					instance.RemoveHint("LeftMouseMechanic");
				}
				_placeHintVisible = canPlace;
			}
			if (_returnHintVisible != canReturn)
			{
				if (canReturn)
				{
					instance.AddHint("RightMouseMechanic", "key_back");
				}
				else
				{
					instance.RemoveHint("RightMouseMechanic");
				}
				_returnHintVisible = canReturn;
			}
		}

		public void ClientSetToolTipData(ProcessedIngredientSO processedIngredientSO, byte ingredientCount)
		{
			if (!(processedIngredientSO == null) && !(_chefDeskGastroPan == null))
			{
				_processedIngredientSO = processedIngredientSO;
				_ingredientNameText.text = processedIngredientSO.LocalizedName;
				_ingredientCount.text = ingredientCount + "/" + processedIngredientSO.MaxPanCapacity;
				_ingredientNameText_01.text = processedIngredientSO.LocalizedName;
				_ingredientCount_01.text = ingredientCount + "/" + processedIngredientSO.MaxPanCapacity;
			}
		}

		public void ClientSetToolTipData(string processedIngredientID, byte ingredientCount)
		{
			if (!(_chefDeskGastroPan == null) && _chefDeskGastroPan.ChefDesk.ItemDatabaseSO.FindById(processedIngredientID) is ProcessedIngredientSO processedIngredientSO)
			{
				_processedIngredientSO = processedIngredientSO;
				_ingredientNameText.text = processedIngredientSO.LocalizedName;
				_ingredientCount.text = ingredientCount + "/" + processedIngredientSO.MaxPanCapacity;
				_ingredientNameText_01.text = processedIngredientSO.LocalizedName;
				_ingredientCount_01.text = ingredientCount + "/" + processedIngredientSO.MaxPanCapacity;
			}
		}

		public void ClientToolTipIngredientCountUpdate(byte ingredientCount)
		{
			if (!(_processedIngredientSO == null))
			{
				_ingredientCount.text = ingredientCount + "/" + _processedIngredientSO.MaxPanCapacity;
				_ingredientCount_01.text = ingredientCount + "/" + _processedIngredientSO.MaxPanCapacity;
			}
		}

		public void ClientClearToolTipData()
		{
			_processedIngredientSO = null;
			_toolTipGO.SetActive(value: false);
			_toolTipGO_01.SetActive(value: false);
			_ingredientNameText.text = string.Empty;
			_ingredientCount.text = string.Empty;
			_ingredientNameText_01.text = string.Empty;
			_ingredientCount_01.text = string.Empty;
		}

		public void ChefDeskToolTip(bool value)
		{
			if (!value)
			{
				_toolTipGO_01.SetActive(value: false);
			}
			else if (_chefDeskGastroPan.IngredientCount <= 0)
			{
				_toolTipGO_01.SetActive(value: false);
			}
			else
			{
				_toolTipGO_01.SetActive(value: true);
			}
		}
	}
}
