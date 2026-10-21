using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction.ToolTip
{
	public sealed class InteractableTooltip : MonoBehaviour, ITooltipProvider, ITooltipWorldAnchor
	{
		[Header("Tooltip")]
		[SerializeField]
		private bool _isEnabled = true;

		[SerializeField]
		private TooltipData _tooltipData;

		[SerializeField]
		private Transform _anchor;

		private bool _hasRuntimeTooltipData;

		private TooltipData _runtimeTooltipData;

		public bool IsEnabled
		{
			get
			{
				return _isEnabled;
			}
			set
			{
				_isEnabled = value;
			}
		}

		public TooltipData GetTooltip()
		{
			if (!_hasRuntimeTooltipData)
			{
				return _tooltipData;
			}
			return _runtimeTooltipData;
		}

		public TooltipData GetBaseTooltip()
		{
			return _tooltipData;
		}

		public void SetRuntimeTooltip(TooltipData tooltipData)
		{
			_runtimeTooltipData = tooltipData;
			_hasRuntimeTooltipData = true;
		}

		public void ClearRuntimeTooltip()
		{
			_hasRuntimeTooltipData = false;
			_runtimeTooltipData = default;
		}

		public Vector3 GetWorldPosition()
		{
			return ((_anchor != null) ? _anchor : base.transform).position;
		}

		private void OnDisable()
		{
			if (Singleton<ToolTipHandler>.Instance != null)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(this);
			}
		}
	}
}
