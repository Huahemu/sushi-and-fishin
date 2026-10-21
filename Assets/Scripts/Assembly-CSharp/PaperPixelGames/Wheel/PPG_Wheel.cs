using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Wheel
{
	[ExecuteAlways]
	[RequireComponent(typeof(RectTransform))]
	public class PPG_Wheel : MonoBehaviour
	{
		public Action<IWheelSlice> SelectSlice;

		[SerializeField]
		private RectTransform _rect;

		[SerializeField]
		private List<PPG_WheelElement> _elements;

		[SerializeField]
		private float radius = 200f;

		[SerializeField]
		private float startAngle;

		[SerializeField]
		private bool clockwise = true;

		[SerializeField]
		[Range(0f, 1f)]
		private float spacingFactor = 1f;

		[SerializeField]
		private float enterDuration = 1f;

		[SerializeField]
		private float exitDuration = 0.5f;

		[SerializeField]
		private float perItemDelay = 0.05f;

		[SerializeField]
		private Ease enterEase = Ease.OutBack;

		[SerializeField]
		private Ease exitEase = Ease.InBack;

		[SerializeField]
		private float enterAngleSweep = 270f;

		[SerializeField]
		private float exitAngleSweep = -90f;

		private bool _wheelStatus;

		private float _angleAnim;

		private float _radiusFactor = 1f;

		private Tweener _angleTween;

		private Tweener _radiusTween;

		private Tweener _exitTween;

		private readonly List<PPG_WheelElement> _runtimeElements = new List<PPG_WheelElement>();

		public bool WheelStatus => _wheelStatus;

		public PPG_WheelElement ElementTemplate => _elements?.FirstOrDefault((PPG_WheelElement element) => element != null);

		public int SelectableElementCount => _elements?.Count((PPG_WheelElement element) => element != null && element.Unlocked) ?? 0;

		public bool CanShow()
		{
			if (!_wheelStatus)
			{
				return SelectableElementCount > 0;
			}
			return false;
		}

		public void RegisterElement(PPG_WheelElement element)
		{
			if (!(element == null))
			{
				if (_elements == null)
				{
					_elements = new List<PPG_WheelElement>();
				}
				if (!_elements.Contains(element))
				{
					_elements.Add(element);
				}
			}
		}

		public void PopulateRuntimeElements(PPG_WheelElement template, IEnumerable<ScriptableObject> dataObjects)
		{
			if (template == null || dataObjects == null)
			{
				return;
			}
			for (int i = 0; i < _runtimeElements.Count; i++)
			{
				PPG_WheelElement pPG_WheelElement = _runtimeElements[i];
				if (!(pPG_WheelElement == null))
				{
					_elements?.Remove(pPG_WheelElement);
					if (Application.isPlaying)
					{
						UnityEngine.Object.Destroy(pPG_WheelElement.gameObject);
					}
					else
					{
						UnityEngine.Object.DestroyImmediate(pPG_WheelElement.gameObject);
					}
				}
			}
			_runtimeElements.Clear();
			foreach (ScriptableObject dataObject in dataObjects)
			{
				if (dataObject is IWheelSlice)
				{
					PPG_WheelElement pPG_WheelElement2 = UnityEngine.Object.Instantiate(template, base.transform);
					pPG_WheelElement2.ConfigureRuntime(this, dataObject, unlocked: true);
					_runtimeElements.Add(pPG_WheelElement2);
					RegisterElement(pPG_WheelElement2);
				}
			}
			ArrangeImmediate();
		}

		private int GetElementsCount()
		{
			return _elements.Count((PPG_WheelElement x) => x.Unlocked);
		}

		private float UpdateRadius(int count)
		{
			if (count <= 4)
			{
				return 220f;
			}
			return count switch
			{
				5 => 240f, 
				6 => 270f, 
				_ => 300f + (float)(count - 6) * 12.5f, 
			};
		}

		private void ArrangeImmediate()
		{
			int elementsCount = GetElementsCount();
			if (elementsCount <= 0)
			{
				return;
			}
			radius = UpdateRadius(elementsCount);
			float num = 360f / (float)elementsCount * spacingFactor;
			float num2 = (clockwise ? (-1f) : 1f);
			IEnumerable<PPG_WheelElement> enumerable = _elements.Where((PPG_WheelElement x) => x.Unlocked);
			int num3 = 0;
			foreach (PPG_WheelElement item in enumerable)
			{
				if (item.TryGetComponent<RectTransform>(out var component))
				{
					float f = (startAngle + _angleAnim + num * (float)num3++) * (MathF.PI / 180f) * num2;
					component.anchoredPosition = new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * (radius * _radiusFactor);
					component.localRotation = Quaternion.identity;
				}
			}
		}

		public void Show()
		{
			if (_wheelStatus)
			{
				return;
			}
			_wheelStatus = true;
			KillTweens();
			for (int i = 0; i < _elements.Count; i++)
			{
				PPG_WheelElement pPG_WheelElement = _elements[i];
				if (pPG_WheelElement.Unlocked)
				{
					pPG_WheelElement.ShowElement(enterDuration * 0.6f, (float)i * perItemDelay, enterEase);
				}
			}
			_angleAnim = enterAngleSweep;
			_radiusFactor = 0f;
			ArrangeImmediate();
			_angleTween = DOTween.To(() => _angleAnim, (float x) =>
			{
				_angleAnim = x;
				ArrangeImmediate();
			}, 0f, enterDuration).SetEase(enterEase).Play();
			_radiusTween = DOTween.To(() => _radiusFactor, (float x) =>
			{
				_radiusFactor = x;
				ArrangeImmediate();
			}, 1f, enterDuration).SetEase(Ease.OutCubic).Play();
			QuestReferenceHub.Instance.QuestComplete(QuestId.OpenTheToolWheel);
		}

		public void Hide(bool selectHovered = true)
		{
			if (!_wheelStatus)
			{
				return;
			}
			_wheelStatus = false;
			KillTweens();
			PPG_WheelElement pPG_WheelElement = null;
			int count = _elements.Count;
			_exitTween = DOTween.To(() => _angleAnim, (float x) =>
			{
				_angleAnim = x;
				ArrangeImmediate();
			}, exitAngleSweep, exitDuration).SetEase(exitEase);
			IEnumerable<PPG_WheelElement> enumerable = _elements.Where((PPG_WheelElement x) => x.Unlocked);
			enumerable.Reverse();
			int num = 0;
			foreach (PPG_WheelElement item in enumerable)
			{
				item.HideElement(exitDuration * 0.5f, (float)(count - 1 - num++) * perItemDelay, exitEase);
				if (item.HoverSelect)
				{
					pPG_WheelElement = item;
				}
			}
			if (selectHovered && pPG_WheelElement != null)
			{
				SelectSlice?.Invoke(pPG_WheelElement.Data);
			}
		}

		private void KillTweens()
		{
			_angleTween?.Kill();
			_radiusTween?.Kill();
			_exitTween?.Kill();
			for (int i = 0; i < _elements.Count; i++)
			{
				PPG_WheelElement pPG_WheelElement = _elements[i];
				if (pPG_WheelElement.Unlocked)
				{
					pPG_WheelElement.transform.DOKill();
				}
			}
		}

		private void OnDisable()
		{
			KillTweens();
		}
	}
}
