using DG.Tweening;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Kitchen.PreparationStation.Station;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaperPixelGames.Core.Kitchen.PreparationStation
{
	public abstract class PrepStationUIHandlerBase : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
	{
		[Header("Station References")]
		[SerializeField]
		protected GastroPanSpawner _gastroPanSpawner;

		[SerializeField]
		protected PrepStation _prepStation;

		[Header("State")]
		[SerializeField]
		protected PrepStationState _validState;

		[Header("Visual")]
		[SerializeField]
		protected GameObject _modelObj;

		protected bool IsInValidState
		{
			get
			{
				if (_prepStation != null)
				{
					return _prepStation.CurrentState == _validState;
				}
				return false;
			}
		}

		protected virtual bool RequiresValidStateForClick => true;

		protected virtual bool RequiresValidStateForHover => false;

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (CanHandleHover())
			{
				OnHoverEnter();
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			OnHoverExit();
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (CanHandleClick(eventData))
			{
				OnClick();
			}
		}

		protected virtual void OnHoverEnter()
		{
		}

		protected virtual void OnHoverExit()
		{
		}

		protected abstract void OnClick();

		protected void KillModelTweensAndReset(Vector3? localEuler = null)
		{
			if (!(_modelObj == null))
			{
				Transform transform = _modelObj.transform;
				transform.DOKill();
				if (localEuler.HasValue)
				{
					transform.localEulerAngles = localEuler.Value;
				}
			}
		}

		private bool CanHandleHover()
		{
			if (_prepStation == null)
			{
				return false;
			}
			if (RequiresValidStateForHover && !IsInValidState)
			{
				return false;
			}
			return true;
		}

		private bool CanHandleClick(PointerEventData eventData)
		{
			if (_prepStation == null)
			{
				return false;
			}
			if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
			{
				return false;
			}
			if (RequiresValidStateForClick && !IsInValidState)
			{
				return false;
			}
			return true;
		}
	}
}
