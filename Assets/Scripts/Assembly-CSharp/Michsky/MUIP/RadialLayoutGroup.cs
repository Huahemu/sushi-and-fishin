using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[AddComponentMenu("Modern UI Pack/Layout Group/Radial Layout Group")]
	public class RadialLayoutGroup : LayoutGroup
	{
		public enum Direction
		{
			Clockwise = 0,
			Counterclockwise = 1,
			Bidirectional = 2
		}

		public enum ConstraintMode
		{
			Interval = 0,
			Range = 1
		}

		[SerializeField]
		private Direction refLayoutDir;

		[SerializeField]
		private float refRadiusStart = 200f;

		[SerializeField]
		private float refRadiusDelta;

		[SerializeField]
		private float refRadiusRange;

		[SerializeField]
		private float refAngleDelta;

		[SerializeField]
		private float refAngleStart;

		[SerializeField]
		private float refAngleCenter;

		[SerializeField]
		private float refAngleRange = 200f;

		[SerializeField]
		private bool refChildRotate;

		private List<RectTransform> childList = new List<RectTransform>();

		private List<ILayoutIgnorer> ignoreList = new List<ILayoutIgnorer>();

		private static readonly Vector2 center = new Vector2(0.5f, 0.5f);

		public Direction layoutDir
		{
			get
			{
				return refLayoutDir;
			}
			set
			{
				SetProperty(ref refLayoutDir, value);
			}
		}

		public float radiusStart
		{
			get
			{
				return refRadiusStart;
			}
			set
			{
				SetProperty(ref refRadiusStart, value);
			}
		}

		public float radiusDelta
		{
			get
			{
				return refRadiusDelta;
			}
			set
			{
				SetProperty(ref refRadiusDelta, value);
			}
		}

		public float radiusRange
		{
			get
			{
				return refRadiusRange;
			}
			set
			{
				SetProperty(ref refRadiusRange, value);
			}
		}

		public float angleDelta
		{
			get
			{
				return refAngleDelta;
			}
			set
			{
				SetProperty(ref refAngleDelta, value);
			}
		}

		public float angleStart
		{
			get
			{
				return refAngleStart;
			}
			set
			{
				SetProperty(ref refAngleStart, value);
			}
		}

		public float angleCenter
		{
			get
			{
				return refAngleCenter;
			}
			set
			{
				SetProperty(ref refAngleCenter, value);
			}
		}

		public float angleRange
		{
			get
			{
				return refAngleRange;
			}
			set
			{
				SetProperty(ref refAngleRange, value);
			}
		}

		public bool childRotate
		{
			get
			{
				return refChildRotate;
			}
			set
			{
				SetProperty(ref refChildRotate, value);
			}
		}

		public override void CalculateLayoutInputVertical()
		{
		}

		public override void CalculateLayoutInputHorizontal()
		{
		}

		public override void SetLayoutHorizontal()
		{
			CalculateChildrenPositions();
		}

		public override void SetLayoutVertical()
		{
			CalculateChildrenPositions();
		}

		private void CalculateChildrenPositions()
		{
			m_Tracker.Clear();
			childList.Clear();
			for (int i = 0; i < base.transform.childCount; i++)
			{
				RectTransform rectTransform = base.transform.GetChild(i) as RectTransform;
				if (!rectTransform.gameObject.activeSelf)
				{
					continue;
				}
				ignoreList.Clear();
				rectTransform.GetComponents(ignoreList);
				if (ignoreList.Count == 0)
				{
					childList.Add(rectTransform);
					continue;
				}
				for (int j = 0; j < ignoreList.Count; j++)
				{
					if (!ignoreList[j].ignoreLayout)
					{
						childList.Add(rectTransform);
						break;
					}
				}
				ignoreList.Clear();
			}
			EnsureParameters(childList.Count);
			for (int k = 0; k < childList.Count; k++)
			{
				RectTransform child = childList[k];
				float num = (float)k * angleDelta;
				float angle = ((layoutDir == Direction.Clockwise) ? (angleStart - num) : (angleStart + num));
				ProcessOneChild(child, angle, radiusStart + (float)k * radiusDelta);
			}
			childList.Clear();
		}

		private void EnsureParameters(int childCount)
		{
			EnsureAngleParameters(childCount);
			EnsureRadiusParameters(childCount);
		}

		private void EnsureAngleParameters(int childCount)
		{
			int num = childCount - 1;
			switch (layoutDir)
			{
			case Direction.Clockwise:
				if (num > 0)
				{
					angleDelta = angleRange / (float)num;
				}
				else
				{
					angleDelta = 0f;
				}
				break;
			case Direction.Counterclockwise:
				if (num > 0)
				{
					angleDelta = angleRange / (float)num;
				}
				else
				{
					angleDelta = 0f;
				}
				break;
			case Direction.Bidirectional:
				if (num > 0)
				{
					angleDelta = angleRange / (float)num;
				}
				else
				{
					angleDelta = 0f;
				}
				angleStart = angleCenter - angleRange * 0.5f;
				break;
			}
		}

		private void EnsureRadiusParameters(int childCount)
		{
			int num = childCount - 1;
			switch (layoutDir)
			{
			case Direction.Clockwise:
				if (num > 0)
				{
					radiusDelta = radiusRange / (float)num;
				}
				else
				{
					radiusDelta = 0f;
				}
				break;
			case Direction.Counterclockwise:
			case Direction.Bidirectional:
				if (num > 0)
				{
					radiusDelta = radiusRange / (float)num;
				}
				else
				{
					radiusDelta = 0f;
				}
				break;
			}
		}

		private void ProcessOneChild(RectTransform child, float angle, float radius)
		{
			Vector3 vector = new Vector3(Mathf.Cos(angle * (MathF.PI / 180f)), Mathf.Sin(angle * (MathF.PI / 180f)), 0f);
			child.localPosition = vector * radius;
			DrivenTransformProperties drivenProperties = DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Pivot | DrivenTransformProperties.Rotation;
			m_Tracker.Add(this, child, drivenProperties);
			child.anchorMin = center;
			child.anchorMax = center;
			child.pivot = center;
			if (childRotate)
			{
				child.localEulerAngles = new Vector3(0f, 0f, angle);
			}
			else
			{
				child.localEulerAngles = Vector3.zero;
			}
		}
	}
}
