using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	public class PieChart : MaskableGraphic
	{
		[Serializable]
		public class PieChartDataNode
		{
			public string name = "Chart Item";

			public float value = 10f;

			public Color32 color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

			public Image indicatorImage;

			public TextMeshProUGUI indicatorText;
		}

		[SerializeField]
		public List<PieChartDataNode> chartData = new List<PieChartDataNode>();

		[Range(-75f, 150f)]
		public float borderThickness = 5f;

		[SerializeField]
		private Color borderColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

		public Transform indicatorParent;

		public string valuePrefix = "(";

		public string valueSuffix = ")";

		public bool addValueToIndicator = true;

		public bool enableBorderColor;

		private float fillAmount = 1f;

		private int segments = 720;

		protected override void Awake()
		{
			base.Awake();
			UpdateIndicators();
		}

		private void Update()
		{
			borderThickness = Mathf.Clamp(borderThickness, -75f, base.rectTransform.rect.width / 3.333f);
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			if (chartData.Count == 0)
			{
				return;
			}
			float num = (0f - base.rectTransform.pivot.x) * base.rectTransform.rect.width;
			float num2 = (0f - base.rectTransform.pivot.x) * base.rectTransform.rect.width + borderThickness;
			float num3 = (0f - base.rectTransform.pivot.x) * base.rectTransform.rect.width * 0.6f;
			float num4 = (0f - base.rectTransform.pivot.x) * base.rectTransform.rect.width * 0.6f + borderThickness;
			vh.Clear();
			Vector2 vector = Vector2.zero;
			Vector2 vector2 = Vector2.zero;
			Vector2 vector3 = new Vector2(0f, 0f);
			Vector2 vector4 = new Vector2(0f, 1f);
			Vector2 vector5 = new Vector2(1f, 1f);
			Vector2 vector6 = new Vector2(1f, 0f);
			float num5 = fillAmount;
			float num6 = 360f / (float)segments;
			int num7 = (int)((float)(segments + 1) * num5);
			int num8 = 0;
			float total = 0f;
			float num9 = chartData[0].value;
			chartData.ForEach((PieChartDataNode s) =>
			{
				total += s.value;
			});
			Color32 color = chartData[0].color;
			for (int num10 = 0; num10 < num7; num10++)
			{
				float f = MathF.PI / 180f * ((float)num10 * num6);
				float num11 = Mathf.Cos(f);
				float num12 = Mathf.Sin(f);
				vector3 = new Vector2(0f, 1f);
				vector4 = new Vector2(1f, 1f);
				vector5 = new Vector2(1f, 0f);
				vector6 = new Vector2(0f, 0f);
				Vector2 vector7 = vector;
				Vector2 vector8 = new Vector2(num * num11, num * num12);
				Vector2 vector9 = new Vector2(num2 * num11, num2 * num12);
				Vector2 vector10 = vector2;
				if ((float)num10 > num9 / total * (float)segments && num8 < chartData.Count - 1)
				{
					num8++;
					num9 += chartData[num8].value;
					color = chartData[num8].color;
				}
				vh.AddUIVertexQuad(SetVbo(new Vector2[4]
				{
					vector7,
					vector8,
					vector9 * num4 / num2,
					vector10 * num4 / num2
				}, new Vector2[4] { vector3, vector4, vector5, vector6 }, color));
				if (enableBorderColor)
				{
					vh.AddUIVertexQuad(SetVbo(new Vector2[4] { vector7, vector8, vector9, vector10 }, new Vector2[4] { vector3, vector4, vector5, vector6 }, borderColor));
					vh.AddUIVertexQuad(SetVbo(new Vector2[4]
					{
						vector7 * num3 / num,
						vector8 * num3 / num,
						vector9 * num4 / num2,
						vector10 * num4 / num2
					}, new Vector2[4] { vector3, vector4, vector5, vector6 }, borderColor));
				}
				vector = vector8;
				vector2 = vector9;
			}
		}

		public void SetData(List<PieChartDataNode> data)
		{
			chartData = data;
			SetVerticesDirty();
		}

		protected UIVertex[] SetVbo(Vector2[] vertices, Vector2[] uvs, Color32 color)
		{
			UIVertex[] array = new UIVertex[4];
			for (int i = 0; i < vertices.Length; i++)
			{
				UIVertex simpleVert = UIVertex.simpleVert;
				simpleVert.color = color;
				simpleVert.position = vertices[i];
				simpleVert.uv0 = uvs[i];
				array[i] = simpleVert;
			}
			return array;
		}

		public void UpdateIndicators()
		{
			for (int i = 0; i < chartData.Count; i++)
			{
				if (chartData[i].indicatorImage != null)
				{
					chartData[i].indicatorImage.color = chartData[i].color;
				}
				if (chartData[i].indicatorText != null && addValueToIndicator)
				{
					chartData[i].indicatorText.text = chartData[i].name + valuePrefix + chartData[i].value + valueSuffix;
				}
				else if (chartData[i].indicatorText != null && !addValueToIndicator)
				{
					chartData[i].indicatorText.text = chartData[i].name;
				}
			}
			if (indicatorParent != null)
			{
				StartCoroutine("UpdateIndicatorLayout");
			}
		}

		public void ChangeValue(int itemIndex, float itemValue)
		{
			chartData[itemIndex].value = itemValue;
			base.enabled = false;
			base.enabled = true;
		}

		public void AddNewItem()
		{
			PieChartDataNode pieChartDataNode = new PieChartDataNode();
			if (indicatorParent.childCount != 0)
			{
				int index = indicatorParent.childCount - 1;
				GameObject gameObject = UnityEngine.Object.Instantiate(indicatorParent.GetChild(index).gameObject, new Vector3(0f, 0f, 0f), Quaternion.identity);
				gameObject.transform.SetParent(indicatorParent, worldPositionStays: false);
				gameObject.gameObject.name = "Item " + index + " Indicator";
				pieChartDataNode.indicatorImage = gameObject.GetComponentInChildren<Image>();
				pieChartDataNode.indicatorText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
				pieChartDataNode.name = "Chart Item " + index;
			}
			chartData.Add(pieChartDataNode);
		}

		private IEnumerator UpdateIndicatorLayout()
		{
			yield return new WaitForSeconds(0.1f);
			LayoutRebuilder.ForceRebuildLayoutImmediate(indicatorParent.GetComponentInParent<RectTransform>());
		}
	}
}
