using System;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingProgressRing : MaskableGraphic
	{
		private float _progress;

		public void SetProgress(float value)
		{
			value = Mathf.Clamp01(value);
			if (!Mathf.Approximately(value, _progress))
			{
				_progress = value;
				SetVerticesDirty();
			}
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			Rect rect = base.rectTransform.rect;
			float num = Mathf.Min(rect.width, rect.height) * 0.5f;
			float num2 = num * 0.72f;
			for (int i = 0; i < 48; i++)
			{
				Color color = this.color;
				if ((float)i / 48f >= _progress)
				{
					color.a *= 0.15f;
				}
				float f = MathF.PI / 2f - (float)i * MathF.PI * 2f / 48f;
				float f2 = MathF.PI / 2f - (float)(i + 1) * MathF.PI * 2f / 48f;
				Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
				Vector2 vector2 = new Vector2(Mathf.Cos(f2), Mathf.Sin(f2));
				int currentVertCount = vh.currentVertCount;
				vh.AddVert(rect.center + vector * num2, color, Vector2.zero);
				vh.AddVert(rect.center + vector * num, color, Vector2.zero);
				vh.AddVert(rect.center + vector2 * num, color, Vector2.zero);
				vh.AddVert(rect.center + vector2 * num2, color, Vector2.zero);
				vh.AddTriangle(currentVertCount, currentVertCount + 1, currentVertCount + 2);
				vh.AddTriangle(currentVertCount, currentVertCount + 2, currentVertCount + 3);
			}
		}
	}
}
