using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
	public class TexturePlacer : ITexturePlacer
	{
		private List<Rect> _freeRectangles;

		public int MaxWidth { get; private set; }

		public int MaxHeight { get; private set; }

		public IReadOnlyList<Rect> FreeRectangles => _freeRectangles;

		public TexturePlacer(int maxWidth, int maxHeight)
		{
			MaxWidth = maxWidth;
			MaxHeight = maxHeight;
			_freeRectangles = new List<Rect>
			{
				new Rect(0f, 0f, maxWidth, maxHeight)
			};
		}

		public bool CanInsert(int width, int height)
		{
			return TryFindBestCandidate(width, height).HasValue;
		}

		public bool TryInsert(int width, int height, out Rect resultRect)
		{
			Rect? rect = TryFindBestCandidate(width, height);
			if (!rect.HasValue)
			{
				resultRect = default;
				return false;
			}
			resultRect = rect.Value;
			UpdateFreeRectangles(resultRect);
			return true;
		}

		private Rect? TryFindBestCandidate(int width, int height)
		{
			Rect? result = null;
			int num = int.MaxValue;
			int num2 = int.MaxValue;
			foreach (Rect freeRectangle in _freeRectangles)
			{
				if ((float)width <= freeRectangle.width && (float)height <= freeRectangle.height)
				{
					int a = (int)freeRectangle.width - width;
					int b = (int)freeRectangle.height - height;
					int num3 = Mathf.Min(a, b);
					int num4 = Mathf.Max(a, b);
					if (num3 < num || (num3 == num && num4 < num2))
					{
						result = new Rect(freeRectangle.x, freeRectangle.y, width, height);
						num = num3;
						num2 = num4;
					}
				}
			}
			return result;
		}

		private void UpdateFreeRectangles(Rect placedRect)
		{
			List<Rect> list = new List<Rect>();
			foreach (Rect freeRectangle in _freeRectangles)
			{
				if (freeRectangle.Overlaps(placedRect))
				{
					float num = freeRectangle.x + freeRectangle.width - (placedRect.x + placedRect.width);
					if (num > 0f)
					{
						Rect item = new Rect(placedRect.x + placedRect.width, freeRectangle.y, num, freeRectangle.height);
						list.Add(item);
					}
					float num2 = freeRectangle.y + freeRectangle.height - (placedRect.y + placedRect.height);
					if (num2 > 0f)
					{
						Rect item2 = new Rect(freeRectangle.x, placedRect.y + placedRect.height, freeRectangle.width, num2);
						list.Add(item2);
					}
				}
				else
				{
					list.Add(freeRectangle);
				}
			}
			_freeRectangles = RemoveRedundantRectangles(list);
		}

		private List<Rect> RemoveRedundantRectangles(List<Rect> rectangles)
		{
			for (int i = 0; i < rectangles.Count; i++)
			{
				Rect rect = rectangles[i];
				for (int j = i + 1; j < rectangles.Count; j++)
				{
					Rect rect2 = rectangles[j];
					if (IsContainedIn(rect, rect2))
					{
						rectangles.RemoveAt(i);
						i--;
						break;
					}
					if (IsContainedIn(rect2, rect))
					{
						rectangles.RemoveAt(j);
						j--;
					}
				}
			}
			return rectangles;
		}

		private bool IsContainedIn(Rect a, Rect b)
		{
			if (a.x >= b.x && a.y >= b.y && a.x + a.width <= b.x + b.width)
			{
				return a.y + a.height <= b.y + b.height;
			}
			return false;
		}
	}
}
